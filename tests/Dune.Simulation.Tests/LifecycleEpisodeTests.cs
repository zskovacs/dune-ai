using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;

namespace Dune.Simulation.Tests;

public sealed class LifecycleEpisodeTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void RunnerCompletesOriginalTenConflictRecipeAndReplays(int players)
    {
        LifecycleFixtureGame? played = null;
        var ownersInNonTurnWindows = new HashSet<int>();
        var combatPlaysByOpportunity = new Dictionary<(int Round, int Window, int Owner), int>();
        var bonusByRound = new Dictionary<int, int>();
        var postWinPlays = 0;
        var runner = new EpisodeRunner<LifecycleFixtureConfiguration, TurnObservation, GameAction, DuneGameResult>(
            () => played = new LifecycleFixtureGame(),
            (owner, _) => new RecordingAgent((observation, legal) =>
            {
                Assert.Equal(owner, observation.Game.DecisionOwnerId);
                Assert.Equal(owner, observation.Game.PlayerId);
                Assert.Equal(owner, legal[0].PlayerId);
                bonusByRound.TryAdd(observation.Game.Round,
                    observation.Game.Board.Spaces.Where(x => x.AccumulatesSpice).Sum(x => x.BonusSpice));
                if (observation.Game.Phase is GamePhase.Combat or GamePhase.Endgame)
                {
                    Assert.Null(observation.Game.CurrentPlayerId);
                    ownersInNonTurnWindows.Add(owner);
                }
                var mutable = Assert.IsAssignableFrom<IList<GameAction>>(legal);
                Assert.Throws<NotSupportedException>(() => mutable[0] = new PassPriorityAction(owner));
                var action = new LifecycleFixtureAgent().SelectAction(observation, legal);
                if (observation.Game.Phase == GamePhase.Combat && action is PlayIntrigueAction &&
                    observation.Game.Window is { } window)
                {
                    var key = (observation.Game.Round, window.Id, owner);
                    combatPlaysByOpportunity[key] = combatPlaysByOpportunity.GetValueOrDefault(key) + 1;
                }
                if (observation.Game.Window?.Kind == DecisionWindowKind.PostWinRewards && action is PlayIntrigueAction)
                    postWinPlays++;
                return action;
            }));
        var episode = runner.Run(42, new(players), agentSeed: 17, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(10, episode.Result.Rounds);
        Assert.Equal(DuneTerminalReason.ConflictDeckExhausted, episode.Result.TerminalReason);
        Assert.Equal(Enumerable.Range(0, players), ownersInNonTurnWindows.Order());
        Assert.Contains(combatPlaysByOpportunity.Values, count => count >= 2);
        Assert.True(bonusByRound[2] > bonusByRound[1]); // automatic Makers consequence after round-one Recall
        Assert.True(postWinPlays > 0);
        Assert.Contains(episode.Actions, x => x is PlaceAgentAction);
        Assert.Contains(episode.Actions, x => x is DeployAction { Troops: > 0 });
        Assert.Contains(episode.Actions, x => x is ChooseOptionAction { Option: 1 });
        Assert.Contains(episode.Actions, x => x is PlayIntrigueAction);
        var game = Assert.IsType<LifecycleFixtureGame>(played);
        Assert.True(game.IsTerminal);
        Assert.Null(game.DecisionOwnerId);
        var replay = game.CaptureReplay();
        Assert.IsType<StartRoundAction>(replay.Actions[0]);
        Assert.Equal(episode.Actions, replay.Actions.Skip(1));
        var reconstructed = TurnEngine.Replay(replay, () => LifecycleFixture.CreateScenarioState(players, 42), game.Rules);
        Assert.True(reconstructed.IsSuccess);
        Assert.Equal(episode.Result.ReplayIdentity, reconstructed.Engine!.GetResult().ReplayIdentity);
        Assert.Equal(replay.FinalStateHash, reconstructed.Engine.CaptureTrustedSnapshot().Hash);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void FixtureAgentHandlesRealOwnerTargetAndDeclineOnlyDecisions(int players, bool declineOnly)
    {
        var state = LifecycleFixture.CreateSelectedTrashState(players);
        var source = state.Players[0].Deck.Cards[0];
        var rules = LifecycleFixture.SelectedTrashRules(state);
        if (declineOnly)
            rules = TurnEngineFixture.CardEffects(rules, source.Definition, agent:
                [new ForPlayerEffect(1, [new SelectedTrashEffect([new UnsupportedEffect("Synthetic unavailable branch")])])]);
        var engine = new TurnEngine(state, rules);
        TurnEngineFixture.Apply(engine, new StartRoundAction());
        TurnEngineFixture.Apply(engine, new PlaceAgentAction(0, TurnEngineFixture.Card(source), TurnEngineFixture.Space("selective-breeding")));
        var effect = engine.Observe(0).PendingEffects.Single(x => x.Kind == (declineOnly ? nameof(ForPlayerEffect) : nameof(SelectedTrashEffect)));
        TurnEngineFixture.Apply(engine, new ResolveEffectAction(0, effect.Id));
        if (declineOnly)
        {
            var pending = engine.Observe(1).PendingEffects.Single(x => x.Kind == nameof(SelectedTrashEffect));
            TurnEngineFixture.Apply(engine, new ResolveEffectAction(1, pending.Id));
        }
        var owner = engine.DecisionOwnerId!.Value;
        var observation = engine.Observe(owner);
        var legal = engine.LegalActions(owner);
        Assert.Equal(declineOnly ? 1 : 0, owner);
        Assert.Equal(owner, observation.Game.PlayerId);
        Assert.Equal(owner, observation.Game.DecisionOwnerId);
        Assert.All(legal, action => Assert.Equal(owner, action.PlayerId));
        Assert.Empty(engine.LegalActions((owner + 1) % players));
        var agent = new LifecycleFixtureAgent();
        var action = agent.SelectAction(observation, legal, TestContext.Current.CancellationToken);
        Assert.Equal(action, agent.SelectAction(observation, legal, TestContext.Current.CancellationToken));
        if (declineOnly) Assert.IsType<DeclineTrashAction>(Assert.Single(legal));
        else Assert.IsType<SelectTrashCardAction>(action);
        Assert.Contains(action, legal);
        TurnEngineFixture.Apply(engine, action);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => agent.SelectAction(observation, legal, cancelled.Token));
    }

    [Fact]
    public void SerialAndParallelSchedulesKeepPolicySeedsAndResultsIndependentOfEnvironmentSeeds()
    {
        int[] seeds = [42, 7, -5];
        var runner = CreateRunner();
        var serial = runner.RunBatch(seeds, new(3), 1, agentSeed: 91,
            cancellationToken: TestContext.Current.CancellationToken);
        var parallel = runner.RunBatch(seeds, new(3), 3, agentSeed: 91,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(serial.Select(x => x.AgentSeed), parallel.Select(x => x.AgentSeed));
        for (var i = 0; i < seeds.Length; i++)
        {
            Assert.Equal(serial[i].Actions, parallel[i].Actions);
            Assert.Equal(serial[i].Result.ReplayIdentity, parallel[i].Result.ReplayIdentity);
        }
        var changedEnvironment = runner.RunBatch([1, 2, 3], new(3), 3, agentSeed: 91,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(serial.Select(x => x.AgentSeed), changedEnvironment.Select(x => x.AgentSeed));
    }

    [Fact]
    public void CancellationBoundAndUnsupportedStartupRejectCleanly()
    {
        var runner = CreateRunner();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => runner.Run(42, new(3), cancellationToken: cancelled.Token));
        Assert.Throws<InvalidOperationException>(() => runner.Run(42, new(3), maxSteps: 1,
            cancellationToken: TestContext.Current.CancellationToken));
        var unsupported = new LifecycleFixtureGame(state =>
        {
            var rules = LifecycleFixture.ScenarioRules(state);
            return rules with { Lifecycle = rules.Lifecycle! with { Conflicts = rules.Lifecycle.Conflicts.RemoveAt(0) } };
        });
        Assert.Throws<NotSupportedException>(() => unsupported.Reset(42, new(3)));
    }

    [Fact]
    public void ObservationIsDetachedFromSubsequentEngineTransitions()
    {
        var game = new LifecycleFixtureGame();
        game.Reset(42, new(3));
        var owner = game.DecisionOwnerId!.Value;
        var before = game.Observe(owner);
        var hand = before.Game.Hand.ToArray();
        game.Step(new LifecycleFixtureAgent().SelectAction(before, game.LegalActions(owner),
            TestContext.Current.CancellationToken));
        Assert.Equal(hand, before.Game.Hand);
        Assert.NotSame(before.Game, game.Observe(owner).Game);
        Assert.DoesNotContain(typeof(TurnObservation).GetProperties(), x => x.PropertyType == typeof(GameState));
        Assert.DoesNotContain(typeof(TurnObservation).GetProperties(), x => x.PropertyType == typeof(TurnReplay));
    }

    private static EpisodeRunner<LifecycleFixtureConfiguration, TurnObservation, GameAction, DuneGameResult> CreateRunner() =>
        new(() => new LifecycleFixtureGame(), (_, _) => new LifecycleFixtureAgent());

    private sealed class RecordingAgent(Func<TurnObservation, IReadOnlyList<GameAction>, GameAction> select)
        : IAgent<TurnObservation, GameAction>
    {
        public GameAction SelectAction(TurnObservation observation, IReadOnlyList<GameAction> legalActions,
            CancellationToken cancellationToken = default) => select(observation, legalActions);
    }
}
