using Dune.Engine.Turns;
using Microsoft.Extensions.Logging;

namespace Dune.Cli;

internal static partial class CliLog
{
    [LoggerMessage(1000, LogLevel.Information, "Command started", EventName = "CommandStarted")]
    internal static partial void CommandStarted(ILogger logger);

    [LoggerMessage(1001, LogLevel.Information, "Command completed with exit code {ExitCode}", EventName = "CommandCompleted")]
    internal static partial void CommandCompleted(ILogger logger, int exitCode);

    [LoggerMessage(1002, LogLevel.Error, "Command failed unexpectedly", EventName = "CommandFailed")]
    internal static partial void CommandFailed(ILogger logger, Exception exception);

    [LoggerMessage(1003, LogLevel.Warning, "Command cancelled", EventName = "CommandCancelled")]
    internal static partial void CommandCancelled(ILogger logger);

    [LoggerMessage(1004, LogLevel.Warning, "Command arguments rejected", EventName = "ArgumentsRejected")]
    internal static partial void ArgumentsRejected(ILogger logger);

    [LoggerMessage(1100, LogLevel.Error, "Replay verification failed", EventName = "ReplayFailed")]
    internal static partial void ReplayFailed(ILogger logger);

    [LoggerMessage(1101, LogLevel.Information, "Toy episode verified after {TurnsPlayed} turns", EventName = "ToySummary")]
    internal static partial void ToySummary(ILogger logger, int turnsPlayed);

    [LoggerMessage(EventId = 1200, Message = "Preview diagnostic {Status} [{Code}]", EventName = "PreviewDiagnostic")]
    internal static partial void PreviewDiagnostic(ILogger logger, LogLevel level, StepStatus status, string code);

    [LoggerMessage(1201, LogLevel.Information, "Preview verified for {PlayerCount} players after {AcceptedActions} actions; unsupported boundary: {StoppedUnsupported}", EventName = "PreviewSummary")]
    internal static partial void PreviewSummary(ILogger logger, int playerCount, int acceptedActions, bool stoppedUnsupported);

    [LoggerMessage(1300, LogLevel.Information, "Synthetic lifecycle verified for {PlayerCount} players after {Rounds} rounds and {AgentActions} policy actions", EventName = "LifecycleSummary")]
    internal static partial void LifecycleSummary(ILogger logger, int playerCount, int rounds, int agentActions);
}
