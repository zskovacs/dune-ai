using System.Globalization;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;

namespace Dune.Cli;

internal static class TurnPreviewCommand
{
    internal static int Run(string[] args)
    {
        var seed = 42;
        var players = 4;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value))
                return Usage();
            switch (args[i])
            {
                case "--seed": seed = value; break;
                case "--players" when value is 3 or 4: players = value; break;
                default: return Usage();
            }
        }

        Console.WriteLine("DEVELOPMENT PREVIEW — original Imperium (2020) turn mechanics");
        Console.WriteLine("SYNTHETIC shared inventory, Conflict identities and leaders; incomplete official card boxes.");
        Console.WriteLine("No authentic complete game or RL-ready configuration. Unsupported mechanics stop execution.");
        Console.WriteLine($"Seed: {seed}; players: {players}; rules: {OriginalImperiumTurnRules.Version}");
        GameState Create() => OriginalImperiumFixture.Create(players, seed);
        var initial = Create();
        var rules = OriginalImperiumTurnRules.Create() with
        {
            // These are invented fixture identities with no abilities, never replacements for real leaders.
            Leaders = [.. initial.Definition.EligibleLeaders.Select(id => new LeaderRule(id, true, [], Synthetic: true))],
        };
        var engine = new TurnEngine(initial, rules);
        var start = engine.StartRound();
        if (!start.IsSuccess)
        {
            PrintDiagnostics(start.Diagnostics);
            return 1;
        }
        Console.WriteLine("Round 1: revealed a synthetic Conflict; each player drew five cards.");
        var stoppedUnsupported = false;
        var steps = 0;
        while (engine.Phase == GamePhase.PlayerTurns)
        {
            if (++steps > 300)
                throw new InvalidOperationException("Preview exceeded the bounded decision count.");
            var owner = engine.DecisionOwnerId!.Value;
            var observation = engine.Observe(owner);
            var legal = observation.LegalActions;
            Console.WriteLine($"Active P{observation.Game.CurrentPlayerId}; decision P{owner}; legal actions: {legal.Length}");
            foreach (var candidate in legal)
                Console.WriteLine("  legal " + Describe(candidate));
            if (legal.IsEmpty)
            {
                var diagnostics = engine.SupportDiagnostics(owner);
                if (diagnostics.IsEmpty)
                    throw new InvalidOperationException("No legal decision or support diagnostic at Player Turns.");
                PrintDiagnostics(diagnostics);
                stoppedUnsupported = true;
                break;
            }
            // Deterministic demonstration policy; troop deployment is still an explicit selected action.
            var action = legal.FirstOrDefault(x => x is PlaceAgentAction) ??
                legal.FirstOrDefault(x => x is ResolveEffectAction) ??
                legal.FirstOrDefault(x => x is ChooseOptionAction { Option: 0 }) ??
                legal.FirstOrDefault(x => x is DeployAction { Troops: 0 }) ??
                legal.FirstOrDefault(x => x is AcquireCardAction) ?? legal[0];
            var phase = engine.Phase;
            var result = engine.Step(action);
            if (!result.IsSuccess)
                throw new InvalidOperationException("Generated legal action failed: " + Describe(action));
            Console.WriteLine("  action " + Describe(action));
            if (engine.Phase != phase) Console.WriteLine($"  phase {phase} -> {engine.Phase}");
        }
        var record = engine.CaptureReplay();
        var replay = TurnEngine.Replay(record, Create, rules);
        if (!replay.IsSuccess || replay.Engine!.CaptureTrustedSnapshot().Hash != engine.CaptureTrustedSnapshot().Hash)
        {
            PrintDiagnostics(replay.Diagnostics);
            Console.Error.WriteLine("Preview replay verification failed.");
            return 1;
        }
        Console.WriteLine($"Replay verified: {record.Actions.Length} accepted actions; {record.FinalStateHash}");
        Console.WriteLine(stoppedUnsupported
            ? "PARTIAL EXECUTION: stopped at an unsupported content boundary before completing Player Turns."
            : "Stopped at the Combat boundary; Combat, Makers, Recall and game completion are not implemented.");
        return 0;
    }

    private static string Describe(GameAction action) => JsonSerializer.Serialize(action, ConfigurationJson.Options);

    private static void PrintDiagnostics(IEnumerable<TurnDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            Console.WriteLine($"  {diagnostic.Status} [{diagnostic.Code}]: {diagnostic.Message}");
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: Dune.Cli --turn-preview [--seed INT] [--players 3|4]");
        return 2;
    }
}
