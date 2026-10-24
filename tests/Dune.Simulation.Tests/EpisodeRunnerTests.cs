using System.Collections.Concurrent;
using Dune.Engine;
using Dune.Engine.Toy;

namespace Dune.Simulation.Tests;

public sealed class EpisodeRunnerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(42, 17)]
    [InlineData(-123, int.MinValue)]
    public void AutonomousEpisodeIsDeterministicAndReplays(int seed, int agentSeed)
    {
        var runner = CreateRunner();
        var first = runner.Run(seed, new ToyConfig(), agentSeed, cancellationToken: TestContext.Current.CancellationToken);
        var second = runner.Run(seed, new ToyConfig(), agentSeed, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(seed, first.Seed);
        Assert.Equal(agentSeed, first.AgentSeed);
        Assert.NotEmpty(first.Actions);
        Assert.Equal(first.Actions, second.Actions);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.Actions.Count, first.Result.TurnsPlayed);
        Assert.InRange(first.Result.WinnerPlayerId, 0, 1);
        Assert.Contains(first.Actions, action => action.PlayerId == 0);
        Assert.Contains(first.Actions, action => action.PlayerId == 1);

        var game = new ToyGame();
        game.Reset(seed, new ToyConfig());
        foreach (var action in first.Actions)
        {
            game.Step(action);
        }
        var replayed = ToyGame.Replay(game.CreateReplay());
        Assert.True(replayed.IsTerminal);
        Assert.Equal(first.Result, replayed.GetResult());
        Assert.Equal(first.Actions, replayed.CreateReplay().Actions);
    }

    [Fact]
    public void AgentsReceiveOnlyObservationAndDetachedReadOnlyLegalActions()
    {
        var observations = new List<ToyObservation>();
        var legalSnapshots = new List<IReadOnlyList<ToyAction>>();
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => new ToyGame(),
            (playerId, _) => new ChoosingAgent<ToyObservation, ToyAction>((observation, legal) =>
            {
                Assert.Equal(playerId, observation.PlayerId);
                Assert.Equal(playerId, observation.DecisionOwnerId);
                Assert.All(legal, action => Assert.Equal(playerId, action.PlayerId));
                var mutable = Assert.IsAssignableFrom<IList<ToyAction>>(legal);
                Assert.Throws<NotSupportedException>(() => mutable[0] = new ToyAction(99, 99));
                observations.Add(observation);
                legalSnapshots.Add(legal);
                return legal[0];
            }));
        var result = runner.Run(42, new ToyConfig(4, 4), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([0, 1, 0, 1], observations.Select(observation => observation.PlayerId));
        Assert.Equal(4, observations[0].RemainingTokens);
        Assert.Equal(3, legalSnapshots[0].Count);
        Assert.Equal(new ToyResult(1, 4), result.Result);
        Assert.Equal(3, typeof(IAgent<ToyObservation, ToyAction>).GetMethod("SelectAction")!.GetParameters().Length);
        Assert.Equal(
            [typeof(ToyObservation), typeof(IReadOnlyList<ToyAction>), typeof(CancellationToken)],
            typeof(IAgent<ToyObservation, ToyAction>).GetMethod("SelectAction")!.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void BatchMaintainsInputOrderAndRecordedPolicySeedsReproduceEachEpisodeAcrossDegrees()
    {
        int[] seeds = [42, -5, 7, 42, int.MinValue, 0, int.MaxValue, 123];
        var runner = CreateRunner();
        var serial = runner.RunBatch(seeds, new ToyConfig(), 1, agentSeed: 123,
            cancellationToken: TestContext.Current.CancellationToken);
        foreach (var degree in new[] { 1, 4 })
        {
            var batch = runner.RunBatch(seeds, new ToyConfig(), degree, agentSeed: 123,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(seeds, batch.Select(episode => episode.Seed));
            for (var index = 0; index < seeds.Length; index++)
            {
                Assert.Equal(serial[index].Actions, batch[index].Actions);
                Assert.Equal(serial[index].Result, batch[index].Result);
                Assert.Equal(serial[index].AgentSeed, batch[index].AgentSeed);
                var reproduced = runner.Run(batch[index].Seed, new ToyConfig(), batch[index].AgentSeed,
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(batch[index].Actions, reproduced.Actions);
                Assert.Equal(batch[index].Result, reproduced.Result);
            }
        }
    }

    [Fact]
    public void BatchPolicySeedsAreIndependentOfPrivateEnvironmentSeedsAndScheduling()
    {
        int[][] environmentSeeds = [[0, 42, -123, int.MinValue, int.MaxValue], [99, 8, 7, 6, 5]];
        var runner = CreateRunner();
        var baseline = runner.RunBatch(environmentSeeds[0], new ToyConfig(), 1, agentSeed: 123,
            cancellationToken: TestContext.Current.CancellationToken);
        foreach (var degree in new[] { 1, 4 })
        {
            foreach (var seeds in environmentSeeds)
            {
                var batch = runner.RunBatch(seeds, new ToyConfig(), degree, agentSeed: 123,
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(seeds, batch.Select(episode => episode.Seed));
                Assert.Equal(baseline.Select(episode => episode.AgentSeed), batch.Select(episode => episode.AgentSeed));
            }
        }
        var changedPolicy = runner.RunBatch(environmentSeeds[0], new ToyConfig(), 4, agentSeed: 124,
            cancellationToken: TestContext.Current.CancellationToken);
        for (var index = 0; index < baseline.Count; index++)
        {
            Assert.NotEqual(baseline[index].AgentSeed, changedPolicy[index].AgentSeed);
        }
    }

    [Fact]
    public void EveryEpisodeOwnsFreshGameAndAgents()
    {
        var games = new ConcurrentBag<ToyGame>();
        var agents = new ConcurrentBag<RandomAgent<ToyObservation, ToyAction>>();
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () =>
            {
                var game = new ToyGame();
                games.Add(game);
                return game;
            },
            (_, seed) =>
            {
                var agent = new RandomAgent<ToyObservation, ToyAction>(seed);
                agents.Add(agent);
                return agent;
            });
        var episodes = runner.RunBatch([1, 2, 3, 4], new ToyConfig(), 4, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(4, games.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(8, agents.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(games, game => Assert.True(game.IsTerminal));
        var snapshot = episodes[0].Actions.ToArray();
        foreach (var game in games)
        {
            game.Reset(99, new ToyConfig());
        }
        Assert.Equal(snapshot, episodes[0].Actions);
        var mutable = Assert.IsAssignableFrom<IList<ToyAction>>(episodes[0].Actions);
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<EpisodeResult<ToyAction, ToyResult>>>(episodes).Clear());
    }

    [Fact]
    public void AgentFactoryReceivesOnlyPolicyDerivedSeedsIndependentOfPrivateEnvironmentSeed()
    {
        var streams = new List<int[]>();
        foreach (var policySeed in new[] { 0, 123 })
        {
            (int PlayerId, int Seed)[]? expected = null;
            foreach (var environmentSeed in new[] { 0, 42, -123, int.MinValue, int.MaxValue })
            {
                var factoryInputs = new List<(int PlayerId, int Seed)>();
                var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
                    () => new ToyGame(), (playerId, seed) =>
                    {
                        factoryInputs.Add((playerId, seed));
                        return new RandomAgent<ToyObservation, ToyAction>(seed);
                    });
                runner.Run(environmentSeed, new ToyConfig(), policySeed,
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal([0, 1], factoryInputs.Select(input => input.PlayerId));
                expected ??= factoryInputs.ToArray();
                Assert.Equal(expected, factoryInputs);
            }
            Assert.NotNull(expected);
            Assert.NotEqual(expected[0].Seed, expected[1].Seed);
            streams.Add(expected.Select(input => input.Seed).ToArray());
        }
        Assert.NotEqual(streams[0][0], streams[1][0]);
        Assert.NotEqual(streams[0][1], streams[1][1]);
    }

    [Fact]
    public void RandomAgentIsSeededAndRejectsEmptyOrNullLegalActions()
    {
        var first = new RandomAgent<int, int>(42);
        var second = new RandomAgent<int, int>(42);
        int[] legal = [1, 2, 3];
        Assert.Equal(Enumerable.Range(0, 100).Select(_ => first.SelectAction(0, legal, cancellationToken: TestContext.Current.CancellationToken)),
            Enumerable.Range(0, 100).Select(_ => second.SelectAction(0, legal, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Throws<ArgumentException>(() => first.SelectAction(0, [], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => first.SelectAction(0, null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InvalidRunnerArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(null!, (_, _) => null!));
        Assert.Throws<ArgumentNullException>(() => new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(() => new ToyGame(), null!));
        var runner = CreateRunner();
        Assert.Throws<ArgumentNullException>(() => runner.RunBatch(null!, new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => runner.Run(42, null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => runner.RunBatch([], null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(42, new ToyConfig(), maxSteps: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(42, new ToyConfig(), maxSteps: -1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.RunBatch([], new ToyConfig(), maxSteps: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.RunBatch([], new ToyConfig(), degreeOfParallelism: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.RunBatch([], new ToyConfig(), degreeOfParallelism: -1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(runner.RunBatch([], new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullGamesAndAgentsAndReusedPlayerAgentAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => null!, (_, _) => new RandomAgent<ToyObservation, ToyAction>(1)).Run(42, new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => new ToyGame(), (_, _) => null!).Run(42, new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
        var shared = new RandomAgent<ToyObservation, ToyAction>(1);
        Assert.Throws<InvalidOperationException>(() => new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => new ToyGame(), (_, _) => shared).Run(42, new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void IllegalPolicyChoiceIsRejectedBeforeGameMutation()
    {
        var game = new ToyGame();
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(() => game,
            (_, _) => new ChoosingAgent<ToyObservation, ToyAction>((_, _) => new ToyAction(0, 99)));
        Assert.Throws<InvalidOperationException>(() => runner.Run(42, new ToyConfig(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(game.CreateReplay().Actions);
    }

    [Fact]
    public void CancellationPropagatesBeforeFactoriesAndAfterPolicyDecision()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => throw new Exception("Factory must not run"), (_, _) => throw new Exception("Factory must not run"));
        Assert.Throws<OperationCanceledException>(() => runner.Run(42, new ToyConfig(), cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => runner.RunBatch([42, 43], new ToyConfig(), 4, cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => new RandomAgent<int, int>(1).SelectAction(0, [1], cancelled.Token));

        using var duringDecision = new CancellationTokenSource();
        var game = new ToyGame();
        var cancelling = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(() => game,
            (_, _) => new ChoosingAgent<ToyObservation, ToyAction>((_, legal) =>
            {
                duringDecision.Cancel();
                return legal[0];
            }));
        Assert.Throws<OperationCanceledException>(() => cancelling.Run(42, new ToyConfig(), cancellationToken: duringDecision.Token));
        Assert.Empty(game.CreateReplay().Actions);
    }

    [Fact]
    public void BatchPropagatesIllegalPolicyChoice()
    {
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => new ToyGame(),
            (_, _) => new ChoosingAgent<ToyObservation, ToyAction>((_, _) => new ToyAction(0, 99)));
        var error = Assert.Throws<AggregateException>(() => runner.RunBatch([1, 2, 3, 4], new ToyConfig(), 4,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.All(error.Flatten().InnerExceptions, exception => Assert.IsType<InvalidOperationException>(exception));
    }

    [Fact]
    public void BatchPropagatesCancellationDuringDecision()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runner = new EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult>(
            () => new ToyGame(),
            (_, _) => new ChoosingAgent<ToyObservation, ToyAction>((_, legal) =>
            {
                cancellation.Cancel();
                return legal[0];
            }));
        Assert.Throws<OperationCanceledException>(() => runner.RunBatch([1, 2, 3, 4], new ToyConfig(), 4,
            cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, null, true)]
    [InlineData(1, -1, true)]
    [InlineData(1, 1, true)]
    [InlineData(1, 0, false)]
    public void InvalidGameDecisionBoundariesFailExplicitly(int players, int? owner, bool hasLegalAction)
    {
        var game = new RunawayGame(players, owner, hasLegalAction);
        var runner = CreateRunawayRunner(game);
        Assert.Throws<InvalidOperationException>(() => runner.Run(0, 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, game.Steps);
    }

    [Fact]
    public void StepBoundStopsRunawayGameAfterExactlyAllowedTransitions()
    {
        var game = new RunawayGame(1, 0, true);
        Assert.Throws<InvalidOperationException>(() => CreateRunawayRunner(game).Run(0, 0, maxSteps: 3, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, game.Steps);
    }

    [Fact]
    public void GameMayFinishExactlyAtStepBound()
    {
        var result = CreateRunner().Run(0, new ToyConfig(1, 1), maxSteps: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new ToyResult(0, 1), result.Result);
    }

    private static EpisodeRunner<ToyConfig, ToyObservation, ToyAction, ToyResult> CreateRunner() => new(
        () => new ToyGame(), (_, seed) => new RandomAgent<ToyObservation, ToyAction>(seed));

    private static EpisodeRunner<int, int, int, int> CreateRunawayRunner(RunawayGame game) => new(
        () => game, (_, _) => new ChoosingAgent<int, int>((_, legal) => legal[0]));

    private sealed class ChoosingAgent<TObservation, TAction>(Func<TObservation, IReadOnlyList<TAction>, TAction> choose)
        : IAgent<TObservation, TAction>
    {
        public TAction SelectAction(TObservation observation, IReadOnlyList<TAction> legalActions, CancellationToken cancellationToken = default)
            => choose(observation, legalActions);
    }

    private sealed class RunawayGame(int players, int? owner, bool hasLegalAction) : IGame<int, int, int, int>
    {
        public int Steps { get; private set; }
        public int PlayerCount => players;
        public int? CurrentPlayerId => owner;
        public int? DecisionOwnerId => owner;
        public string Phase => "Runaway";
        public bool IsTerminal => false;
        public void Reset(int seed, int config) => Steps = 0;
        public int Observe(int playerId) => Steps;
        public IReadOnlyList<int> LegalActions(int playerId) => hasLegalAction ? [1] : [];
        public void Step(int action) => Steps++;
        public int GetResult() => throw new InvalidOperationException();
    }
}
