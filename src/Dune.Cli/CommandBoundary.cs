using Microsoft.Extensions.Logging;

namespace Dune.Cli;

internal sealed class CommandBoundary
{
    private CommandBoundary() { }

    internal static int Run(ILogger<CommandBoundary> logger, Func<int> command)
    {
        CliLog.CommandStarted(logger);
        int exitCode;
        try
        {
            exitCode = command();
        }
        catch (OperationCanceledException)
        {
            CliLog.CommandCancelled(logger);
            exitCode = 130;
        }
        catch (Exception exception)
        {
            CliLog.CommandFailed(logger, exception);
            exitCode = 1;
        }
        CliLog.CommandCompleted(logger, exitCode);
        return exitCode;
    }
}
