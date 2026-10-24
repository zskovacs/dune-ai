using System.Globalization;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using Dune.Simulation;
using Microsoft.Extensions.Logging;

namespace Dune.Cli;

internal sealed class LifecyclePreviewCommand
{
    private LifecyclePreviewCommand() { }
    internal static int Run(string[] args, ILogger<LifecyclePreviewCommand> logger)
    {
        var seed = 42;
        var players = 4;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value)) return Usage(logger);
            switch (args[i])
            {
                case "--seed": seed = value; break;
                case "--players" when value is 3 or 4: players = value; break;
                default: return Usage(logger);
            }
        }

        LifecycleFixtureGame? played = null;
        var runner = new EpisodeRunner<LifecycleFixtureConfiguration, TurnObservation, GameAction, DuneGameResult>(
            () => played = new LifecycleFixtureGame(), (_, _) => new LifecycleFixtureAgent());
        var episode = runner.Run(seed, new(players), agentSeed: 17, maxSteps: 2_000);
        var game = played ?? throw new InvalidOperationException("Runner did not construct a game.");
        var replay = game.CaptureReplay();
        var reproduced = TurnEngine.Replay(replay, () => LifecycleFixture.CreateScenarioState(players, seed), game.Rules);
        if (!reproduced.IsSuccess || reproduced.Engine is null ||
            reproduced.Engine.GetResult().ReplayIdentity != episode.Result.ReplayIdentity ||
            reproduced.Engine.CaptureTrustedSnapshot().Hash != replay.FinalStateHash ||
            !replay.Actions.Skip(1).SequenceEqual(episode.Actions))
        {
            CliLog.ReplayFailed(logger);
            Console.Error.WriteLine("Synthetic lifecycle replay verification failed.");
            return 1;
        }

        var timings = game.Rules.Lifecycle!.Intrigues.ToDictionary(x => x.Id, x => x.Timing);
        var plays = episode.Actions.OfType<PlayIntrigueAction>().ToArray();
        var combatPlays = plays.Count(x => timings[x.Card.Definition] == IntrigueTiming.Combat);
        var postWinPlays = plays.Count(x => timings[x.Card.Definition] == IntrigueTiming.PostWin);
        Console.WriteLine("SYNTHETIC DEVELOPMENT SCENARIO — invented main decks, leaders and Conflict rewards; sourced board-space rules and supported reserves.");
        Console.WriteLine("Original Imperium lifecycle mechanics; no authentic complete-game or RL-ready claim.");
        Console.WriteLine($"Players: {players}; environment seed: {seed}; independent policy seed: {episode.AgentSeed}");
        Console.WriteLine($"Coverage: placement {episode.Actions.OfType<PlaceAgentAction>().Count()}, deployment {episode.Actions.OfType<DeployAction>().Count()}, observed Combat Intrigue plays {combatPlays}, observed PostWin Intrigue plays {postWinPlays}, optional choices {episode.Actions.OfType<ChooseOptionAction>().Count()}, reward/priority passes {episode.Actions.OfType<PassPriorityAction>().Count()}.");
        Console.WriteLine("Coverage counts describe accepted actions in this run; zero means that path was not observed.");
        Console.WriteLine("Automatic round consequences: Makers and Recall complete through normal round progression; no policy boundary in those phases.");
        Console.WriteLine($"Result: {episode.Result.Rounds} rounds; terminal reason {episode.Result.TerminalReason}; winner(s) {string.Join(", ", episode.Result.WinnerIds.Select(x => $"P{x}"))}.");
        Console.WriteLine($"Replay verified: {replay.Actions.Length} accepted actions including StartRound; replay identity {episode.Result.ReplayIdentity}.");
        Console.WriteLine("Authentic-content blockers: incomplete official inventories, leader abilities and board abilities; Plot play (including Calculated Hire); missing official Conflict classifications and PostWin numeric faces; Alliance holder loss; tied positive shared Intrigue draw order; contested Mentat; unsupported composite effect shapes.");
        CliLog.LifecycleSummary(logger, players, episode.Result.Rounds, episode.Actions.Count);
        return 0;
    }

    private static int Usage(ILogger logger)
    {
        CliLog.ArgumentsRejected(logger);
        Console.Error.WriteLine("Usage: Dune.Cli --lifecycle-preview [--seed INT] [--players 3|4]");
        return 2;
    }
}
