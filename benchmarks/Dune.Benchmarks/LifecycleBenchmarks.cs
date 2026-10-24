using BenchmarkDotNet.Attributes;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;

namespace Dune.Benchmarks;

/// <summary>Synthetic micro workloads and a separately named full invented game, never authentic throughput.</summary>
[MemoryDiagnoser]
public class LifecycleBenchmarks
{
    private const int BatchSize = 256;
    private TurnEngine[] _batch = null!;
    private GameAction[] _actions = null!;
    private TurnEngine _legal = null!;
    private readonly LifecycleFixtureAgent _policy = new();
    private CombatParticipant[] _participants = null!;

    [Params(3, 4)]
    public int Players { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var state = LifecycleFixture.CreateScenarioState(Players, 42);
        _legal = new(state, LifecycleFixture.ScenarioRules(state));
        Apply(_legal, new StartRoundAction());
        _participants = [.. Enumerable.Range(0, Players).Select(x => new CombatParticipant(x, x < 2 ? 8 : 4))];
    }

    [IterationSetup(Target = nameof(DepthThreeForeignChild))]
    public void PrepareForeign() => Prepare("foreign");
    [IterationSetup(Target = nameof(CombatPlay))]
    public void PreparePlay() => Prepare("play");
    [IterationSetup(Target = nameof(CombatPass))]
    public void PreparePass() => Prepare("pass");
    [IterationSetup(Target = nameof(CombatChildResume))]
    public void PrepareResume() => Prepare("resume");
    [IterationSetup(Target = nameof(RewardRoundTransition))]
    public void PrepareTransition() => Prepare("transition");

    [IterationCleanup(Target = nameof(RewardRoundTransition))]
    public void VerifyTransition()
    {
        if (_batch.Any(engine => engine.Observe(0).Game.Round != 2))
            throw new InvalidOperationException("Reward checkpoint did not include the next round transition.");
    }

    [Benchmark]
    public int UnchangedLegalEnumeration() => _legal.LegalActions(0).Length;
    [Benchmark]
    public CombatOutcome RankingTieDistribution() => CombatOutcome.Calculate(_participants, Players);
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int DepthThreeForeignChild() => RunBatch();
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int CombatPlay() => RunBatch();
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int CombatPass() => RunBatch();
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int CombatChildResume() => RunBatch();
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int RewardRoundTransition() => RunBatch();

    // Includes state creation, synthetic rules composition, engine initialization, observation/legal
    // policy selection, accepted history, and final result/replay hashing. No per-action snapshots/JSON.
    [Benchmark]
    public DuneGameResult FullSyntheticLifecycleIncludingInitialization()
    {
        var state = LifecycleFixture.CreateScenarioState(Players, 42);
        var engine = new TurnEngine(state, LifecycleFixture.ScenarioRules(state));
        Apply(engine, new StartRoundAction());
        for (var i = 0; !engine.IsTerminal; i++)
        {
            if (i >= 2000) throw new InvalidOperationException("Synthetic benchmark did not finish.");
            Apply(engine, Select(engine));
        }
        return engine.GetResult();
    }

    private void Prepare(string kind)
    {
        _batch = new TurnEngine[BatchSize];
        _actions = new GameAction[BatchSize];
        for (var i = 0; i < BatchSize; i++)
        {
            var state = LifecycleFixture.CreateScenarioState(Players, 42);
            var rules = LifecycleFixture.ScenarioRules(state);
            if (kind is "foreign" or "resume")
            {
                rules = rules with
                {
                    Lifecycle = rules.Lifecycle! with
                    {
                        Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Timing == IntrigueTiming.Combat ? x with
                        {
                            Effects = kind == "foreign"
                                ? [new SequenceEffect([new ForPlayerEffect(1,
                                    [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 1)])])
                                    { DecisionOwnerId = 2 }])]
                                : [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Swords, 1)])]
                        } : x)]
                    }
                };
            }
            var engine = new TurnEngine(state, rules);
            Apply(engine, new StartRoundAction());
            for (var step = 0; ; step++)
            {
                if (step >= 2000 || engine.IsTerminal) throw new InvalidOperationException("Missing benchmark checkpoint: " + kind);
                var owner = engine.DecisionOwnerId!.Value;
                var observation = engine.Observe(owner);
                var action = Select(engine);
                var match = kind switch
                {
                    "play" => engine.Phase == GamePhase.Combat && action is PlayIntrigueAction,
                    "pass" => engine.Phase == GamePhase.Combat && action is PassPriorityAction,
                    "foreign" => engine.Phase == GamePhase.Combat && action is ChooseOptionAction { PlayerId: 2, Option: 1 },
                    "resume" => engine.Phase == GamePhase.Combat && action is ChooseOptionAction { Option: 1 },
                    "transition" => observation.Game.Round == 1 && observation.Game.Window?.Kind == DecisionWindowKind.PostWinRewards
                        && action is PassPriorityAction,
                    _ => false,
                };
                if (match)
                {
                    if (kind == "foreign" && engine.CaptureTrustedSnapshot().Data.Frames.Length < 3)
                        throw new InvalidOperationException("Foreign child did not reach depth three.");
                    _batch[i] = engine;
                    _actions[i] = action;
                    break;
                }
                Apply(engine, action);
            }
        }
    }

    private GameAction Select(TurnEngine engine)
    {
        var owner = engine.DecisionOwnerId!.Value;
        return _policy.SelectAction(engine.Observe(owner), engine.LegalActions(owner));
    }

    private int RunBatch()
    {
        for (var i = 0; i < BatchSize; i++) Apply(_batch[i], _actions[i]);
        return BatchSize;
    }

    private static void Apply(TurnEngine engine, GameAction action) => TurnEngineFixture.Apply(engine, action);
}
