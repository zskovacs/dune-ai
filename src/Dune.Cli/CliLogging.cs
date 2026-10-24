using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Json;

namespace Dune.Cli;

// One instance owns the CLI process's logger and SelfLog callback. No global Log.Logger.
internal sealed class CliLogging : IDisposable
{
    private readonly Serilog.Core.Logger? _logger;
    private readonly ILoggerFactory _factory;
    private int _sinkFailure;

    private CliLogging(string command, LogEventLevel level, bool disabled, string directory)
    {
        if (disabled)
        {
            _factory = NullLoggerFactory.Instance;
            return;
        }
        Directory.CreateDirectory(directory);
        // Rolling sinks open lazily. Validate the actual daily target even when the
        // configured minimum level suppresses the startup event.
        var initialPath = Path.Combine(directory, "dune-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
        using (new FileStream(initialPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { }
        SelfLog.Enable(ReportSinkFailure);
        try
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .Enrich.WithProperty("RunId", Guid.NewGuid().ToString("N"))
                .Enrich.WithProperty("Command", command)
                .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(new JsonFormatter(), Path.Combine(directory, "dune-.jsonl"),
                    rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true, retainedFileCountLimit: 14, shared: true)
                .CreateLogger();
            if (HasSinkFailure)
                throw new IOException("The required rolling log file could not be initialized.");
            _factory = new SerilogLoggerFactory(_logger, dispose: false);
        }
        catch
        {
            try { _logger?.Dispose(); }
            finally { SelfLog.Disable(); }
            throw;
        }
    }

    private void ReportSinkFailure(string message)
    {
        Interlocked.Exchange(ref _sinkFailure, 1);
        // Never route sink diagnostics back through the failing logging pipeline.
        try { Console.Error.WriteLine("Logging sink failure: " + message[..Math.Min(message.Length, 2000)].TrimEnd()); }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    internal bool HasSinkFailure => Volatile.Read(ref _sinkFailure) != 0;

    internal static CliLogging Create(string command, string? minimumLevel, string? directory)
    {
        var level = (minimumLevel ?? "Information").Trim().ToUpperInvariant() switch
        {
            "TRACE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "CRITICAL" or "NONE" => LogEventLevel.Fatal,
            _ => throw new ArgumentException("DUNE_LOG_LEVEL must be Trace, Debug, Information, Warning, Error, Critical or None."),
        };
        return new CliLogging(command, level, string.Equals(minimumLevel?.Trim(), "None", StringComparison.OrdinalIgnoreCase),
            directory ?? Path.Combine("artifacts", "logs"));
    }

    internal ILogger<T> CreateLogger<T>() => _factory.CreateLogger<T>();

    public void Dispose()
    {
        try
        {
            _factory.Dispose();
            _logger?.Dispose();
        }
        catch (Exception exception)
        {
            ReportSinkFailure(exception.ToString());
        }
        finally
        {
            if (_logger is not null) SelfLog.Disable();
        }
    }
}
