namespace Dune.Cli;

internal static class CliApplication
{
    internal static int Run(string[] args)
    {
        var command = args.Length > 0 && args[0] == "--turn-preview" ? "TurnPreview"
            : args.Length == 1 && args[0] == "--help" ? "Help" : "Toy";
        CliLogging logging;
        try
        {
            logging = CliLogging.Create(command, Environment.GetEnvironmentVariable("DUNE_LOG_LEVEL"),
                Environment.GetEnvironmentVariable("DUNE_LOG_DIRECTORY"));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unable to initialize persistent logging: {exception.Message}");
            return 1;
        }

        int exitCode;
        using (logging)
        {
            exitCode = CommandBoundary.Run(logging.CreateLogger<CommandBoundary>(), () =>
            {
                var result = command == "TurnPreview"
                    ? TurnPreviewCommand.Run(args[1..], logging.CreateLogger<TurnPreviewCommand>())
                    : ToyCommand.Run(args, logging.CreateLogger<ToyCommand>());
                return logging.HasSinkFailure ? 1 : result;
            });
        }
        return logging.HasSinkFailure ? 1 : exitCode;
    }
}
