using Dune.Engine.Toy;

namespace Dune.Engine.Tests;

public sealed class ToyGameTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-123)]
    public void ResetReproducesSeededSetupAndTurns(int seed)
    {
        var game = CreateGame(seed);
        var first = game.Observe(0);
        var second = game.Observe(1);
        Assert.InRange(first.RemainingTokens, 12, 24);
        Assert.Equal(0, game.CurrentPlayerId);
        Assert.Equal(0, game.DecisionOwnerId);
        Assert.Equal(ToyPhase.TakingTokens, game.CurrentPhase);
        Assert.Equal(2, game.PlayerCount);

        game.Step(new ToyAction(0, 1));
        Assert.Equal(1, game.CurrentPlayerId);
        game.Reset(seed, new ToyConfig());

        Assert.Equal(first, game.Observe(0));
        Assert.Equal(second, game.Observe(1));
        Assert.Empty(game.CreateReplay().Actions);
    }

    [Fact]
    public void LastTakerWinsAndEndsDecision()
    {
        var game = CreateGame(42, new ToyConfig(1, 1));
        Assert.Throws<InvalidOperationException>(() => game.GetResult());
        game.Step(new ToyAction(0, 1));

        Assert.True(game.IsTerminal);
        Assert.Null(game.CurrentPlayerId);
        Assert.Null(game.DecisionOwnerId);
        Assert.Equal(ToyPhase.Finished, game.CurrentPhase);
        Assert.Equal("Finished", game.Phase);
        Assert.Equal(0, game.Observe(1).RemainingTokens);
        Assert.Equal(new ToyResult(0, 1), game.GetResult());
        Assert.Empty(game.LegalActions(0));
        Assert.Empty(game.LegalActions(1));
        var before = game.CreateReplay();
        Assert.Throws<InvalidOperationException>(() => game.Step(new ToyAction(0, 1)));
        Assert.Equal(before.Actions, game.CreateReplay().Actions);
    }

    [Fact]
    public void LegalActionsRespectPileAndActivePlayer()
    {
        var game = CreateGame(42, new ToyConfig(4, 4));
        Assert.Equal([new ToyAction(0, 1), new ToyAction(0, 2), new ToyAction(0, 3)], game.LegalActions(0));
        Assert.Empty(game.LegalActions(1));
        game.Step(new ToyAction(0, 3));
        Assert.Empty(game.LegalActions(0));
        Assert.Equal([new ToyAction(1, 1)], game.LegalActions(1));
        game.Step(new ToyAction(1, 1));
        Assert.Equal(new ToyResult(1, 2), game.GetResult());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 4)]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public void InvalidActionsDoNotMutateGame(int playerId, int amount)
    {
        var game = CreateGame(42, new ToyConfig(2, 2));
        var first = game.Observe(0);
        var second = game.Observe(1);
        Assert.ThrowsAny<ArgumentException>(() => game.Step(new ToyAction(playerId, amount)));
        Assert.Equal(first, game.Observe(0));
        Assert.Equal(second, game.Observe(1));
        Assert.Empty(game.CreateReplay().Actions);
        game.Step(new ToyAction(0, 2));
        Assert.Equal(new ToyResult(0, 1), game.GetResult());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void InvalidPlayerQueriesFailWithoutMutation(int playerId)
    {
        var game = CreateGame(42);
        var before = game.Observe(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Observe(playerId));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.LegalActions(playerId));
        Assert.Equal(before, game.Observe(0));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 1000001)]
    public void InvalidConfigurationIsRejected(int minimum, int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToyConfig(minimum, maximum));
    }

    [Fact]
    public void NullResetConfigurationLeavesPriorGameUntouched()
    {
        var game = CreateGame(42);
        var before = game.Observe(0);
        Assert.Throws<ArgumentNullException>(() => game.Reset(3, null!));
        Assert.Equal(before, game.Observe(0));
    }

    [Fact]
    public void UninitializedStateFailsExplicitly()
    {
        var game = new ToyGame();
        Assert.Equal(2, game.PlayerCount);
        Assert.Throws<InvalidOperationException>(() => game.Observe(0));
        Assert.Throws<InvalidOperationException>(() => game.LegalActions(0));
        Assert.Throws<InvalidOperationException>(() => game.Step(new ToyAction(0, 1)));
        Assert.Throws<InvalidOperationException>(() => game.GetResult());
        Assert.Throws<InvalidOperationException>(() => game.CreateReplay());
        Assert.Throws<InvalidOperationException>(() => game.IsTerminal);
        Assert.Throws<InvalidOperationException>(() => game.CurrentPlayerId);
        Assert.Throws<InvalidOperationException>(() => game.DecisionOwnerId);
        Assert.Throws<InvalidOperationException>(() => game.CurrentPhase);
        Assert.Throws<InvalidOperationException>(() => game.Phase);
    }

    [Fact]
    public void ObservationContainsOnlyOwnMarkerAndPublicValues()
    {
        var game = CreateGame(42);
        var first = game.Observe(0);
        var second = game.Observe(1);
        Assert.Equal(0, first.PlayerId);
        Assert.Equal(1, second.PlayerId);
        Assert.NotEqual(first.PrivateMarker, second.PrivateMarker);
        Assert.Equal(first with { PlayerId = second.PlayerId, PrivateMarker = second.PrivateMarker }, second);

        // An explicit public schema whitelist catches accidental state/history/opponent references.
        Assert.Equal(
            ["CurrentPlayerId", "DecisionOwnerId", "IsTerminal", "Phase", "PlayerId", "PrivateMarker", "RemainingTokens", "TurnNumber"],
            typeof(ToyObservation).GetProperties().Select(property => property.Name).Order());
        Assert.All(typeof(ToyObservation).GetProperties(), property =>
            Assert.True(property.PropertyType.IsValueType));

        var changed = first with { PrivateMarker = -1, RemainingTokens = -1 };
        Assert.NotEqual(changed, game.Observe(0));
        game.Step(new ToyAction(0, 1));
        Assert.Equal(first.PrivateMarker, game.Observe(0).PrivateMarker);
        Assert.Equal(second.PrivateMarker, game.Observe(1).PrivateMarker);
        Assert.Equal(first.RemainingTokens - 1, game.Observe(0).RemainingTokens);
    }

    [Fact]
    public void ReturnedLegalActionsAreReadOnlyAndDetached()
    {
        var game = CreateGame(42, new ToyConfig(4, 4));
        var actions = game.LegalActions(0);
        var mutableView = Assert.IsAssignableFrom<IList<ToyAction>>(actions);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = new ToyAction(1, 99));
        game.Step(actions[0]);
        Assert.Equal(new ToyAction(0, 1), actions[0]);
        Assert.Equal(3, actions.Count);
    }

    internal static ToyGame CreateGame(int seed, ToyConfig? config = null)
    {
        var game = new ToyGame();
        game.Reset(seed, config ?? new ToyConfig());
        return game;
    }
}
