using Dune.Engine.Toy;

namespace Dune.Engine.Tests;

public sealed class ToyReplayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-123)]
    public void PartialAndCompleteReplayReproduceEveryObservationAndResult(int seed)
    {
        var game = ToyGameTests.CreateGame(seed);
        AssertReplayMatches(game);
        while (!game.IsTerminal)
        {
            var actions = game.LegalActions(game.CurrentPlayerId!.Value);
            game.Step(actions[^1]);
            AssertReplayMatches(game);
        }
    }

    [Fact]
    public void ReplayHistoryCopiesInputAndCannotMutateGame()
    {
        var game = ToyGameTests.CreateGame(42, new ToyConfig(4, 4));
        game.Step(new ToyAction(0, 1));
        var replay = game.CreateReplay();
        var mutableView = Assert.IsAssignableFrom<IList<ToyAction>>(replay.Actions);
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        var input = replay.Actions.ToArray();
        var copied = new ToyReplay(replay.EngineVersion, replay.RulesVersion, replay.RuntimeVersion,
            replay.Seed, replay.Config, input);
        input[0] = new ToyAction(1, 99);
        Assert.Equal(new ToyAction(0, 1), copied.Actions[0]);
        game.Step(new ToyAction(1, 1));
        Assert.Single(replay.Actions);
        Assert.Equal(2, game.CreateReplay().Actions.Count);
        Assert.Equal(3, ToyGame.Replay(copied).Observe(0).RemainingTokens);
    }

    [Fact]
    public void ResetReplacesOldConfigurationSeedSecretsAndHistory()
    {
        var game = ToyGameTests.CreateGame(42, new ToyConfig(1, 1));
        game.Step(new ToyAction(0, 1));
        var old = game.CreateReplay();
        game.Reset(123, new ToyConfig(7, 7));
        var fresh = ToyGameTests.CreateGame(123, new ToyConfig(7, 7));
        Assert.Equal(fresh.Observe(0), game.Observe(0));
        Assert.Equal(fresh.Observe(1), game.Observe(1));
        Assert.Empty(game.CreateReplay().Actions);
        Assert.Equal(123, game.CreateReplay().Seed);
        Assert.Equal(new ToyConfig(7, 7), game.CreateReplay().Config);
        Assert.False(game.IsTerminal);
        Assert.Throws<InvalidOperationException>(() => game.GetResult());
        Assert.True(ToyGame.Replay(old).IsTerminal);
    }

    [Theory]
    [InlineData("future", ToyGame.RulesVersion)]
    [InlineData(ToyGame.EngineVersion, "future")]
    public void UnsupportedVersionsAreRejected(string engineVersion, string rulesVersion)
    {
        var original = ToyGameTests.CreateGame(42).CreateReplay();
        var unsupported = new ToyReplay(engineVersion, rulesVersion, original.RuntimeVersion,
            original.Seed, original.Config, original.Actions);
        Assert.Throws<NotSupportedException>(() => ToyGame.Replay(unsupported));
    }

    [Fact]
    public void ForeignRuntimeIsRejectedBeforeApplyingHistory()
    {
        var original = ToyGameTests.CreateGame(42).CreateReplay();
        var unsupported = new ToyReplay(original.EngineVersion, original.RulesVersion,
            original.RuntimeVersion + "-different-runtime", original.Seed, original.Config,
            [new ToyAction(1, 99)]);

        var error = Assert.Throws<NotSupportedException>(() => ToyGame.Replay(unsupported));
        Assert.Contains(unsupported.RuntimeVersion, error.Message);
        Assert.Contains(original.RuntimeVersion, error.Message);
    }

    [Fact]
    public void IllegalReplayHistoryIsRejected()
    {
        var original = ToyGameTests.CreateGame(42, new ToyConfig(1, 1)).CreateReplay();
        var wrongTurn = CopyWithActions(original, [new ToyAction(1, 1)]);
        Assert.ThrowsAny<ArgumentException>(() => ToyGame.Replay(wrongTurn));
        var afterTerminal = CopyWithActions(original, [new ToyAction(0, 1), new ToyAction(1, 1)]);
        Assert.Throws<InvalidOperationException>(() => ToyGame.Replay(afterTerminal));
    }

    private static ToyReplay CopyWithActions(ToyReplay replay, IEnumerable<ToyAction> actions) =>
        new(replay.EngineVersion, replay.RulesVersion, replay.RuntimeVersion, replay.Seed, replay.Config, actions);

    private static void AssertReplayMatches(ToyGame game)
    {
        var record = game.CreateReplay();
        var replayed = ToyGame.Replay(record);
        Assert.Equal(ToyGame.EngineVersion, record.EngineVersion);
        Assert.Equal(ToyGame.RulesVersion, record.RulesVersion);
        Assert.NotEmpty(record.RuntimeVersion);
        Assert.Equal(game.Observe(0), replayed.Observe(0));
        Assert.Equal(game.Observe(1), replayed.Observe(1));
        Assert.Equal(game.CurrentPlayerId, replayed.CurrentPlayerId);
        Assert.Equal(game.IsTerminal, replayed.IsTerminal);
        Assert.Equal(record.Actions, replayed.CreateReplay().Actions);
        Assert.Equal(game.LegalActions(0), replayed.LegalActions(0));
        Assert.Equal(game.LegalActions(1), replayed.LegalActions(1));
        if (game.IsTerminal)
        {
            Assert.Equal(game.GetResult(), replayed.GetResult());
        }
    }
}
