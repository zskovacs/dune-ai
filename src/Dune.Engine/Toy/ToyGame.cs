using System.Runtime.InteropServices;

namespace Dune.Engine.Toy;

/// <summary>Invented two-player token game, independent of Dune rules. Call Reset before using state.</summary>
/// <remarks>Own one instance per episode; instances are not thread-safe. System.Random replay requires the pinned runtime.</remarks>
public sealed class ToyGame : IGame<ToyConfig, ToyObservation, ToyAction, ToyResult>
{
    public const string EngineVersion = "1";
    public const string RulesVersion = "toy-tokens-v1";
    private readonly List<ToyAction> _history = [];
    private State? _state;

    public int PlayerCount => 2;
    public bool IsTerminal => CurrentState.RemainingTokens == 0;
    public int? CurrentPlayerId => IsTerminal ? null : CurrentState.ActivePlayerId;
    public int? DecisionOwnerId => CurrentPlayerId;
    public ToyPhase CurrentPhase => IsTerminal ? ToyPhase.Finished : ToyPhase.TakingTokens;
    public string Phase => CurrentPhase.ToString();
    private State CurrentState => _state ?? throw new InvalidOperationException("Call Reset before using game state.");

    public void Reset(int seed, ToyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var random = new Random(seed);
        var state = new State(random.Next(config.MinInitialTokens, config.MaxInitialTokens + 1),
            0, random.Next(), random.Next(), seed, config);
        _state = state;
        _history.Clear();
    }

    public ToyObservation Observe(int playerId)
    {
        var state = CurrentState;
        ValidatePlayer(playerId);
        return new ToyObservation(playerId, playerId == 0 ? state.FirstMarker : state.SecondMarker,
            state.RemainingTokens, _history.Count, CurrentPlayerId, DecisionOwnerId, CurrentPhase, IsTerminal);
    }

    public IReadOnlyList<ToyAction> LegalActions(int playerId)
    {
        var state = CurrentState;
        ValidatePlayer(playerId);
        if (IsTerminal || playerId != state.ActivePlayerId)
        {
            return Array.AsReadOnly(Array.Empty<ToyAction>());
        }

        return Array.AsReadOnly(Enumerable.Range(1, Math.Min(3, state.RemainingTokens))
            .Select(amount => new ToyAction(playerId, amount)).ToArray());
    }

    public void Step(ToyAction action)
    {
        var state = CurrentState;
        if (IsTerminal)
        {
            throw new InvalidOperationException("The game has finished.");
        }

        ValidatePlayer(action.PlayerId);
        if (action.PlayerId != state.ActivePlayerId)
        {
            throw new ArgumentException("The action belongs to an inactive player.", nameof(action));
        }

        if (action.Amount < 1 || action.Amount > 3 || action.Amount > state.RemainingTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Take between one and three available tokens.");
        }

        _state = state with
        {
            RemainingTokens = state.RemainingTokens - action.Amount,
            ActivePlayerId = 1 - state.ActivePlayerId
        };
        _history.Add(action);
    }

    public ToyResult GetResult()
    {
        if (!IsTerminal)
        {
            throw new InvalidOperationException("A result is available only after the game finishes.");
        }

        return new ToyResult(1 - CurrentState.ActivePlayerId, _history.Count);
    }

    /// <summary>Creates a detached privileged replay record, unsuitable for agent input.</summary>
    public ToyReplay CreateReplay()
    {
        var state = CurrentState;
        return new ToyReplay(EngineVersion, RulesVersion, RuntimeInformation.FrameworkDescription,
            state.Seed, state.Config, _history);
    }

    /// <summary>Reconstructs a game only when engine, rules and runtime versions match.</summary>
    public static ToyGame Replay(ToyReplay replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        if (replay.EngineVersion != EngineVersion || replay.RulesVersion != RulesVersion)
        {
            throw new NotSupportedException("Unsupported toy engine or rules version.");
        }

        var runtimeVersion = RuntimeInformation.FrameworkDescription;
        if (replay.RuntimeVersion != runtimeVersion)
        {
            throw new NotSupportedException(
                $"Replay requires runtime '{replay.RuntimeVersion}'; current runtime is '{runtimeVersion}'. Run replay using the recorded runtime.");
        }

        var game = new ToyGame();
        game.Reset(replay.Seed, replay.Config);
        foreach (var action in replay.Actions)
        {
            game.Step(action);
        }

        return game;
    }

    private static void ValidatePlayer(int playerId)
    {
        if (playerId is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }
    }

    private sealed record State(
        int RemainingTokens,
        int ActivePlayerId,
        int FirstMarker,
        int SecondMarker,
        int Seed,
        ToyConfig Config);
}
