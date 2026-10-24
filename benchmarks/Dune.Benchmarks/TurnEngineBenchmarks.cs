using BenchmarkDotNet.Attributes;
using Dune.Engine.Fixtures;
using Dune.Engine.Turns;

namespace Dune.Benchmarks;

/// <summary>
/// Synthetic inventory/effects, not a complete official game. Mutable workloads use independent
/// batched states prepared outside measurement; legal enumeration reuses one unchanged state.
/// </summary>
[MemoryDiagnoser]
public class TurnEngineBenchmarks
{
    private const int BatchSize = 4096;
    private TurnEngine _legal = null!;
    private TurnEngine[] _batch = null!;
    private GameAction[] _actions = null!;

    [GlobalSetup]
    public void Setup()
    {
        var state = TurnEngineFixture.CreateState();
        _legal = new(state, TurnEngineFixture.Rules(state));
        Apply(_legal, new StartRoundAction());
    }

    [IterationSetup(Target = nameof(RoundStart))]
    public void PrepareRoundStart() => PrepareBatch(start: false, optional: false);

    [IterationSetup(Target = nameof(AgentPlacement))]
    public void PreparePlacement() => PrepareBatch(start: true, optional: false);

    [IterationSetup(Target = nameof(EffectResolution))]
    public void PrepareEffect() => PrepareBatch(start: true, optional: true);

    [Benchmark]
    public int LegalActionEnumeration() => _legal.LegalActions(0).Length;

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int RoundStart() => RunBatch();

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int AgentPlacement() => RunBatch();

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int EffectResolution() => RunBatch();

    private void PrepareBatch(bool start, bool optional)
    {
        _batch = new TurnEngine[BatchSize];
        _actions = new GameAction[BatchSize];
        for (var i = 0; i < BatchSize; i++)
        {
            var state = TurnEngineFixture.CreateState();
            var rules = TurnEngineFixture.Rules(state);
            if (optional)
                rules = rules with
                {
                    // Invented fixture cards only: exercise a real optional-payment primitive.
                    Cards = [.. rules.Cards.Select(card => card.Id.Value.StartsWith("fixture:", StringComparison.Ordinal)
                        ? card with { AgentEffects = [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Solari, 1)])] }
                        : card)],
                };
            var engine = new TurnEngine(state, rules);
            _batch[i] = engine;
            if (!start)
            {
                _actions[i] = new StartRoundAction();
                continue;
            }
            Apply(engine, new StartRoundAction());
            var placement = engine.LegalActions(0).OfType<PlaceAgentAction>()
                .First(x => x.Space == TurnEngineFixture.Space("secure-contract"));
            if (!optional)
            {
                _actions[i] = placement;
                continue;
            }
            Apply(engine, placement);
            // Resolve the outer choice of effect order outside measurement, leaving the nested payment.
            for (var guard = 0; guard < 20; guard++)
            {
                var legal = engine.LegalActions(engine.DecisionOwnerId!.Value);
                var payment = legal.OfType<ChooseOptionAction>().FirstOrDefault(x => x.Option == 1);
                if (payment is not null)
                {
                    _actions[i] = payment;
                    break;
                }
                var resolve = legal.OfType<ResolveEffectAction>().FirstOrDefault()
                    ?? throw new InvalidOperationException("Expected a pending optional effect in benchmark fixture.");
                Apply(engine, resolve);
            }
            if (_actions[i] is null) throw new InvalidOperationException("Optional-payment fixture did not suspend.");
        }
    }

    private int RunBatch()
    {
        var applied = 0;
        for (var i = 0; i < BatchSize; i++)
        {
            var result = _batch[i].Step(_actions[i]);
            if (!result.IsSuccess) throw new InvalidOperationException("Benchmark transition failed: " + result.Status);
            applied++;
        }
        return applied;
    }

    private static void Apply(TurnEngine engine, GameAction action) => TurnEngineFixture.Apply(engine, action);
}
