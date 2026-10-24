using System.Diagnostics;
using System.Text.Json;
using Dune.Cli;
using Dune.Engine.Turns;
using Microsoft.Extensions.Logging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dune.Cli.Tests;

public sealed class LoggingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dune-cli-tests-" + Guid.NewGuid().ToString("N"));

    public LoggingTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Toy_process_flushes_structured_events_and_separates_output()
    {
        var result = await Run("Information", "--seed", "42");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("Invented toy token game", result.Stdout);
        Assert.Contains("Replay verified from recorded actions.", result.Stdout);
        Assert.DoesNotContain("Command started", result.Stdout);
        Assert.Contains("Command started", result.Stderr);
        Assert.DoesNotContain("Seed:", result.Stderr);
        var events = Events();
        Assert.Equal(new[] { "CommandStarted", "ToySummary", "CommandCompleted" }, events.Select(EventName));
        var runIds = events.Select(e => Properties(e).GetProperty("RunId").GetString()).Distinct().ToArray();
        Assert.Single(runIds);
        Assert.True(Guid.TryParseExact(runIds[0], "N", out _));
        Assert.All(events, e =>
        {
            Assert.Equal("Toy", Properties(e).GetProperty("Command").GetString());
            Assert.True(e.TryGetProperty("Timestamp", out _));
            Assert.Equal("Information", e.GetProperty("Level").GetString());
            Assert.InRange(Properties(e).GetProperty("EventId").GetProperty("Id").GetInt32(), 1000, 1201);
            Assert.All(Properties(e).EnumerateObject(), property =>
                Assert.Contains(property.Name, new[] { "RunId", "Command", "SourceContext", "EventId", "TurnsPlayed", "ExitCode" }));
        });
        Assert.Equal(0, Properties(events[^1]).GetProperty("ExitCode").GetInt32());
    }

    [Fact]
    public async Task Preview_unsupported_boundary_is_warning_and_partial_success()
    {
        var result = await Run("Information", "--turn-preview");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PARTIAL EXECUTION", result.Stdout);
        Assert.Contains("Replay verified:", result.Stdout);
        var events = Events();
        var diagnostics = events.Where(e => EventName(e) == "PreviewDiagnostic").ToArray();
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, e =>
        {
            Assert.Equal("Warning", e.GetProperty("Level").GetString());
            Assert.Equal("Unsupported", Properties(e).GetProperty("Status").GetString());
            Assert.False(string.IsNullOrEmpty(Properties(e).GetProperty("Code").GetString()));
            Assert.False(Properties(e).TryGetProperty("Message", out _));
        });
        Assert.DoesNotContain(events, e => e.GetProperty("Level").GetString() is "Error" or "Fatal");
        var summary = Assert.Single(events, e => EventName(e) == "PreviewSummary");
        Assert.True(Properties(summary).GetProperty("StoppedUnsupported").GetBoolean());
        Assert.All(events, e => Assert.All(Properties(e).EnumerateObject(), property =>
            Assert.Contains(property.Name, new[] { "RunId", "Command", "SourceContext", "EventId", "Status", "Code", "PlayerCount", "AcceptedActions", "StoppedUnsupported", "ExitCode" })));
    }

    [Fact]
    public async Task Concurrent_processes_share_valid_jsonl_with_distinct_run_ids()
    {
        var results = await Task.WhenAll(Run("Information", "--seed", "1"), Run("Information", "--seed", "2"));
        Assert.All(results, result => Assert.Equal(0, result.ExitCode));
        var runs = Events().GroupBy(e => Properties(e).GetProperty("RunId").GetString()).ToArray();
        Assert.Equal(2, runs.Length);
        Assert.All(runs, run => Assert.Equal(new[] { "CommandStarted", "ToySummary", "CommandCompleted" }, run.Select(EventName)));
    }

    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("Debug", LogLevel.Debug)]
    [InlineData("Information", LogLevel.Information)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("Critical", LogLevel.Critical)]
    public void Configured_level_is_the_logger_threshold(string configured, LogLevel minimum)
    {
        using var logging = CliLogging.Create("Toy", configured, _directory);
        var logger = logging.CreateLogger<CommandBoundary>();
        Assert.All(Enum.GetValues<LogLevel>(), level =>
            Assert.Equal(level >= minimum && level != LogLevel.None, logger.IsEnabled(level)));
    }

    [Fact]
    public async Task Warning_filters_lifecycle_but_keeps_expected_diagnostics()
    {
        var result = await Run("warning", "--turn-preview");
        Assert.Equal(0, result.ExitCode);
        Assert.All(Events(), e => Assert.Equal("PreviewDiagnostic", EventName(e)));
        Assert.NotEmpty(Events());
        Assert.DoesNotContain("Command started", result.Stderr);
    }

    [Fact]
    public async Task Error_filters_expected_unsupported_diagnostics()
    {
        var result = await Run("Error", "--turn-preview");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(Events());
        Assert.Empty(result.Stderr);
    }

    [Fact]
    public async Task Help_and_invalid_arguments_keep_exit_and_output_contracts()
    {
        var help = await Run("Information", "--help");
        Assert.Equal(0, help.ExitCode);
        Assert.StartsWith("Usage: Dune.Cli", help.Stdout);
        Assert.All(Events(), e => Assert.Equal("Help", Properties(e).GetProperty("Command").GetString()));
        DeleteLogs();
        var invalid = await Run("Information", "private-invalid-argument");
        Assert.Equal(2, invalid.ExitCode);
        Assert.Empty(invalid.Stdout);
        Assert.Contains("Invalid arguments.", invalid.Stderr);
        Assert.DoesNotContain("private-invalid-argument", File.ReadAllText(Directory.GetFiles(_directory, "*.jsonl").Single()));
        Assert.Equal(new[] { "CommandStarted", "ArgumentsRejected", "CommandCompleted" }, Events().Select(EventName));
        Assert.Equal(2, Properties(Events()[^1]).GetProperty("ExitCode").GetInt32());
    }

    [Fact]
    public void Unexpected_exception_is_persisted_once_with_stack_and_failure_exit()
    {
        int exitCode;
        using (var logging = CliLogging.Create("Toy", "Information", _directory))
            exitCode = CommandBoundary.Run(logging.CreateLogger<CommandBoundary>(), () =>
            {
                File.ReadAllText(Path.Combine(_directory, "missing-input"));
                return 0;
            });
        Assert.Equal(1, exitCode);
        var events = Events();
        var failure = Assert.Single(events, e => e.TryGetProperty("Exception", out _));
        Assert.Equal("CommandFailed", EventName(failure));
        Assert.Equal("Error", failure.GetProperty("Level").GetString());
        var exception = failure.GetProperty("Exception").GetString()!;
        Assert.Contains("System.IO.FileNotFoundException", exception);
        Assert.Contains(nameof(Unexpected_exception_is_persisted_once_with_stack_and_failure_exit), exception);
        Assert.Equal(1, Properties(events[^1]).GetProperty("ExitCode").GetInt32());
    }

    [Fact]
    public void Cancellation_is_distinct_from_unexpected_failure()
    {
        int exitCode;
        using (var logging = CliLogging.Create("TurnPreview", "Information", _directory))
            exitCode = CommandBoundary.Run(logging.CreateLogger<CommandBoundary>(), () => throw new OperationCanceledException());
        Assert.Equal(130, exitCode);
        var events = Events();
        Assert.Equal(new[] { "CommandStarted", "CommandCancelled", "CommandCompleted" }, events.Select(EventName));
        Assert.DoesNotContain(events, e => e.TryGetProperty("Exception", out _));
        Assert.Equal("Warning", events[1].GetProperty("Level").GetString());
    }

    [Theory]
    [InlineData(StepStatus.Unsupported, "Warning")]
    [InlineData(StepStatus.Illegal, "Warning")]
    [InlineData(StepStatus.ContentError, "Error")]
    public void Typed_preview_diagnostics_keep_status_and_code_without_private_message(StepStatus status, string level)
    {
        using (var logging = CliLogging.Create("TurnPreview", "Information", _directory))
            TurnPreviewCommand.LogDiagnostics([new(status, "test.code", "private diagnostic detail")], logging.CreateLogger<TurnPreviewCommand>());
        var entry = Assert.Single(Events());
        Assert.Equal(level, entry.GetProperty("Level").GetString());
        Assert.Equal(status.ToString(), Properties(entry).GetProperty("Status").GetString());
        Assert.Equal("test.code", Properties(entry).GetProperty("Code").GetString());
        Assert.DoesNotContain("private diagnostic detail", entry.GetRawText());
    }

    [Fact]
    public async Task Invalid_level_fails_visibly_before_command_output()
    {
        var result = await Run("not-a-level", "--help");
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("DUNE_LOG_LEVEL must be", result.Stderr);
        Assert.Empty(Events());
    }

    [Fact]
    public async Task Invalid_directory_fails_visibly_before_command_output()
    {
        var file = Path.Combine(_directory, "file");
        File.WriteAllText(file, "occupied");
        var result = await RunWithDirectory(file, "Information", "--help");
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Unable to initialize persistent logging", result.Stderr);
    }

    [Fact]
    public async Task Sink_open_failure_is_visible_and_nonzero()
    {
        for (var offset = -1; offset <= 1; offset++)
            Directory.CreateDirectory(Path.Combine(_directory, $"dune-{DateTime.Now.AddDays(offset):yyyyMMdd}.jsonl"));
        var result = await Run("Information", "--help");
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Unable to initialize persistent logging", result.Stderr);
    }

    [Fact]
    public async Task Runtime_sink_write_failure_uses_stderr_fallback_and_failure_exit()
    {
        // The repository targets Linux: /dev/full opens successfully but rejects writes.
        for (var offset = -1; offset <= 1; offset++)
            File.CreateSymbolicLink(Path.Combine(_directory, $"dune-{DateTime.Now.AddDays(offset):yyyyMMdd}.jsonl"), "/dev/full");
        var result = await Run("Information", "--help");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Logging sink failure", result.Stderr);
    }

    [Fact]
    public async Task None_disables_all_events_and_file_initialization()
    {
        var file = Path.Combine(_directory, "occupied");
        File.WriteAllText(file, "occupied");
        var result = await RunWithDirectory(file, "None", "--help");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("Usage:", result.Stdout);
        Assert.Empty(result.Stderr);
        using var logging = CliLogging.Create("Toy", "None", file);
        var logger = logging.CreateLogger<CommandBoundary>();
        Assert.All(Enum.GetValues<LogLevel>(), level => Assert.False(logger.IsEnabled(level)));
    }

    private Task<CommandResult> Run(string level, params string[] args) => RunWithDirectory(_directory, level, args);

    private static async Task<CommandResult> RunWithDirectory(string directory, string level, params string[] args)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "dune-ai.slnx")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root.FullName,
        };
        start.ArgumentList.Add(Path.Combine(root.FullName, "src", "Dune.Cli", "bin", configuration, "net10.0", "Dune.Cli.dll"));
        foreach (var argument in args) start.ArgumentList.Add(argument);
        start.Environment["DUNE_LOG_LEVEL"] = level;
        start.Environment["DUNE_LOG_DIRECTORY"] = directory;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        return new(process.ExitCode, await stdout, await stderr);
    }

    private JsonElement[] Events() => Directory.GetFiles(_directory, "*.jsonl").Order()
        .SelectMany(File.ReadLines).Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => { using var document = JsonDocument.Parse(line); return document.RootElement.Clone(); }).ToArray();

    private void DeleteLogs()
    {
        foreach (var file in Directory.GetFiles(_directory, "*.jsonl")) File.Delete(file);
    }

    private static JsonElement Properties(JsonElement entry) => entry.GetProperty("Properties");
    private static string EventName(JsonElement entry) => Properties(entry).GetProperty("EventId").GetProperty("Name").GetString()!;
    public void Dispose() => Directory.Delete(_directory, recursive: true);
    private sealed record CommandResult(int ExitCode, string Stdout, string Stderr);
}
