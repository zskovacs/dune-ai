using System.Globalization;
using System.Runtime.InteropServices;
using Dune.Engine.Toy;
using Dune.Cli;
using Dune.Simulation;

const string usage = "Usage: Dune.Cli [--seed INT] | --turn-preview [--seed INT] [--players 3|4] | --help";
if (args.Length > 0 && args[0] == "--turn-preview") return TurnPreviewCommand.Run(args[1..]);
var seed = 42;
if (args.Length == 1 && args[0] == "--help")
{
    Console.WriteLine(usage);
    Console.WriteLine("Run an invented two-player toy token game. Default seed: 42.");
    Console.WriteLine("--turn-preview runs sourced Dune turn mechanics with a visibly synthetic inventory and stops on unsupported content.");
    return 0;
}
if (args.Length != 0 && (args.Length != 2 || args[0] != "--seed" ||
    !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)))
{
    Console.Error.WriteLine("Invalid arguments. --seed requires a 32-bit integer.");
    Console.Error.WriteLine(usage);
    return 2;
}

var config = new ToyConfig();
var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
    () => new ToyGame(), (_, policySeed) => new RandomAgent<ToyObservation, ToyAction>(policySeed));
var episode = runner.Run(seed, config);
var replay = new ToyReplay(ToyGame.EngineVersion, ToyGame.RulesVersion,
    RuntimeInformation.FrameworkDescription, seed, config, episode.Actions);
var replayed = ToyGame.Replay(replay);
if (!replayed.IsTerminal || replayed.GetResult() != episode.Result ||
    !replayed.CreateReplay().Actions.SequenceEqual(episode.Actions))
{
    Console.Error.WriteLine("Toy replay verification failed.");
    return 1;
}

Console.WriteLine("Invented toy token game (independent of Dune rules)");
Console.WriteLine($"Seed: {episode.Seed}; policy seed: {episode.AgentSeed}");
Console.WriteLine($"Winner: player {episode.Result.WinnerPlayerId}; turns: {episode.Result.TurnsPlayed}");
Console.WriteLine($"Actions: {string.Join(", ", episode.Actions.Select(action => $"P{action.PlayerId} takes {action.Amount}"))}");
Console.WriteLine("Replay verified from recorded actions.");
return 0;
