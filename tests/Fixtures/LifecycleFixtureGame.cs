using Dune.Engine;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using Dune.Simulation;

namespace Dune.Engine.Fixtures;

/// <summary>Development-only configuration for the invented lifecycle scenario.</summary>
internal sealed record LifecycleFixtureConfiguration(int Players);

/// <summary>Development-only adapter for the existing generic episode runner.</summary>
internal sealed class LifecycleFixtureGame : IGame<LifecycleFixtureConfiguration, TurnObservation, GameAction, DuneGameResult>
{
    private readonly Func<GameState, TurnRuleSet> _rulesFactory;
    private TurnEngine? _engine;
    internal LifecycleFixtureGame(Func<GameState, TurnRuleSet>? rulesFactory = null) =>
        _rulesFactory = rulesFactory ?? LifecycleFixture.ScenarioRules;
    internal TurnRuleSet Rules { get; private set; } = null!;
    internal TurnReplay CaptureReplay() => Engine.CaptureReplay();
    private TurnEngine Engine => _engine ?? throw new InvalidOperationException("Reset must precede gameplay.");

    public int PlayerCount { get; private set; }
    public int? CurrentPlayerId => Engine.CurrentPlayerId;
    public int? DecisionOwnerId => Engine.DecisionOwnerId;
    public string Phase => Engine.Phase.ToString();
    public bool IsTerminal => Engine.IsTerminal;

    public void Reset(int seed, LifecycleFixtureConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Players is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(config));
        _engine = null;
        PlayerCount = 0;
        Rules = null!;
        var state = LifecycleFixture.CreateScenarioState(config.Players, seed);
        var rules = _rulesFactory(state);
        var engine = new TurnEngine(state, rules);
        var start = engine.StartRound();
        if (!start.IsSuccess)
            throw new NotSupportedException($"Synthetic lifecycle startup: {string.Join("; ", start.Diagnostics.Select(x => x.Code))}");
        PlayerCount = config.Players;
        Rules = rules;
        _engine = engine;
    }

    public TurnObservation Observe(int playerId) => Engine.Observe(playerId);
    public IReadOnlyList<GameAction> LegalActions(int playerId) => Engine.LegalActions(playerId);
    public void Step(GameAction action)
    {
        var result = Engine.Step(action);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Selected legal action failed: {string.Join("; ", result.Diagnostics.Select(x => x.Code))}");
    }
    public DuneGameResult GetResult() => Engine.GetResult();
}

/// <summary>Deterministic public-observation policy for the invented scenario.</summary>
internal sealed class LifecycleFixtureAgent : IAgent<TurnObservation, GameAction>
{
    public GameAction SelectAction(TurnObservation observation, IReadOnlyList<GameAction> legalActions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return legalActions.OfType<ResolveEffectAction>().FirstOrDefault()
            ?? (GameAction?)legalActions.OfType<ChooseOptionAction>().FirstOrDefault(x => x.Option == 1)
            ?? legalActions.OfType<ChooseOptionAction>().FirstOrDefault()
            ?? legalActions.OfType<SelectTrashCardAction>().FirstOrDefault()
            ?? legalActions.OfType<DeclineTrashAction>().FirstOrDefault()
            ?? legalActions.OfType<DeployAction>().LastOrDefault()
            ?? legalActions.OfType<PlaceAgentAction>().FirstOrDefault(x =>
                observation.Game.Board.Spaces.Any(space => space.Id == x.Space && space.Name == "Arrakeen"))
            ?? legalActions.OfType<PlaceAgentAction>().FirstOrDefault()
            ?? legalActions.OfType<RevealAction>().FirstOrDefault()
            ?? legalActions.OfType<PlayIntrigueAction>().FirstOrDefault()
            ?? (GameAction?)legalActions.OfType<EndRevealAction>().FirstOrDefault()
            ?? legalActions.OfType<PassPriorityAction>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No supported synthetic action in {observation.Game.Phase}.");
    }
}
