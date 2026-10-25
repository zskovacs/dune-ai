using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class SelectiveBreedingTests
{
    private static readonly BoardSpaceId Breeding = Space("selective-breeding");

    [Fact]
    public void PlacementRequiresMatchingIconAvailableAgentVacancyAndTwoSpice()
    {
        var (state, engine) = Ready([new GainEffect(ResourceKind.Spice, 2)]);
        var player = state.Players[0];
        var card = player.Hand.Cards[0];
        var action = new PlaceAgentAction(0, Card(card), Breeding);
        RejectUnchanged(engine, action);
        // A synthetic card's Spice gain cannot pay the space cost that precedes all effects.
        Assert.Equal(0, player.Resources.Spice);
        player.Resources = player.Resources with { Spice = 2 };
        Assert.Contains(action, engine.LegalActions(0));
        Apply(engine, action);
        Assert.Equal(0, player.Resources.Spice);
        Assert.Single(state.Board.Spaces.Single(x => x.Definition.Id == Breeding).Occupants);

        var (blockedState, blocked) = Ready();
        blockedState.Players[0].Resources = blockedState.Players[0].Resources with { Spice = 2 };
        var wrong = blockedState.Players[0].Hand.Cards[0];
        var wrongRules = Rules(blockedState);
        wrongRules = CardEffects(wrongRules, wrong.Definition, agent: [new GainEffect(ResourceKind.Spice, 2)]);
        wrongRules = wrongRules with { Cards = [.. wrongRules.Cards.Select(x => x.Id == wrong.Definition ? x with { Icons = [AgentIcon.City] } : x)] };
        var wrongEngine = new TurnEngine(blockedState, wrongRules);
        RejectUnchanged(wrongEngine, new PlaceAgentAction(0, Card(wrong), Breeding));
        var (vacantState, vacantEngine) = Ready();
        vacantState.Players[0].Resources = vacantState.Players[0].Resources with { Spice = 2 };
        vacantState.Players[0].Units = vacantState.Players[0].Units with { AvailableAgents = 0, OrdinaryAgents = 0 };
        RejectUnchanged(vacantEngine, new PlaceAgentAction(0, Card(vacantState.Players[0].Hand.Cards[0]), Breeding));
        var (occupiedState, occupiedEngine) = Ready();
        occupiedState.Players[0].Resources = occupiedState.Players[0].Resources with { Spice = 2 };
        var occupant = occupiedState.Players[1];
        occupant.Units = occupant.Units with { AvailableAgents = occupant.Units.AvailableAgents - 1 };
        occupiedState.Board.Spaces.Single(x => x.Definition.Id == Breeding).Occupants = [new(1, AgentKind.Agent)];
        RejectUnchanged(occupiedEngine, new PlaceAgentAction(0, Card(occupiedState.Players[0].Hand.Cards[0]), Breeding));
    }

    [Fact]
    public void DecliningExchangeStillPaysPlacementAndGrantsInfluence()
    {
        var (state, engine) = Ready();
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        var hand = player.Hand.Count;
        Place(engine, player.Hand.Cards[0]);
        Resolve<InfluenceEffect>(engine);
        Resolve<SelectedTrashEffect>(engine);
        var decline = Assert.Single(engine.LegalActions(0).OfType<DeclineTrashAction>());
        Apply(engine, decline);
        Assert.Equal(0, player.Resources.Spice);
        Assert.Equal(1, Track(player));
        Assert.Equal(hand - 1, player.Hand.Count);
        Assert.Empty(state.Shared.Trashed.Cards);
        RejectUnchanged(engine, decline);
    }

    [Theory]
    [InlineData("hand")]
    [InlineData("discard")]
    [InlineData("inplay")]
    public void TakingExchangeTrashesExactlyOneThenDrawsTwo(string zone)
    {
        var (state, engine) = Ready();
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        var played = player.Hand.Cards[0];
        var target = zone switch
        {
            "discard" => player.Hand.Cards[1],
            "inplay" => played,
            _ => player.Hand.Cards[2]
        };
        if (zone == "discard") player.Hand.MoveTo(target.Id, player.Discard);
        var total = state.Zones.Sum(x => x.Count);
        var handBefore = player.Hand.Count;
        Place(engine, played);
        Resolve<SelectedTrashEffect>(engine);
        var source = zone switch { "discard" => player.Discard, "inplay" => player.InPlay, _ => player.Hand };
        var select = engine.LegalActions(0).OfType<SelectTrashCardAction>().Single(x => x.Source == source.Id && x.Card.Definition == target.Definition);
        Apply(engine, select);
        Assert.Contains(target, state.Shared.Trashed.Cards);
        Assert.Equal(handBefore - 1 - (zone == "hand" ? 1 : 0) + 2, player.Hand.Count);
        Assert.Equal(total, state.Zones.Sum(x => x.Count));
        RejectUnchanged(engine, select);
        Assert.Equal(1, Track(player));
    }

    [Fact]
    public void ExchangeCannotBeRepeatedOrDrawWithoutPayment()
    {
        var (state, engine) = Ready();
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        Place(engine, player.Hand.Cards[0]);
        Assert.Equal(0, player.Resources.Spice);
        Resolve<SelectedTrashEffect>(engine);
        var choice = engine.LegalActions(0).OfType<SelectTrashCardAction>().First();
        var handBefore = player.Hand.Count;
        Apply(engine, choice);
        RejectUnchanged(engine, choice);
        Assert.Equal(handBefore + 1, player.Hand.Count);
    }

    [Fact]
    public void CardInfluenceAndBoardEffectsRemainPlayerOrdered()
    {
        var (state, engine) = Ready([new InfluenceEffect(Faction.BeneGesserit)]);
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.BeneGesserit ? x with { Amount = 1 } : x)];
        Place(engine, player.Hand.Cards[0]);
        Assert.Equal(2, engine.Observe(0).PendingEffects.Count(x => x.Kind == nameof(InfluenceEffect)));
        Resolve<SelectedTrashEffect>(engine);
        Apply(engine, Assert.Single(engine.LegalActions(0).OfType<DeclineTrashAction>()));
        Assert.Equal(3, Track(player));
        Assert.Equal(1, player.Resources.VictoryPoints); // Crossing 1 -> 2 grants the sourced faction point.
    }

    [Fact]
    public void TrashFromDiscardPrecedesSeededEmptyDeckReshuffle()
    {
        var (state, engine) = Ready();
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        var played = player.Hand.Cards[0];
        var target = player.Hand.Cards[1];
        var survivorA = player.Hand.Cards[2];
        var survivorB = player.Hand.Cards[3];
        player.Hand.MoveTo(target.Id, player.Discard);
        player.Hand.MoveTo(survivorA.Id, player.Discard);
        player.Hand.MoveTo(survivorB.Id, player.Discard);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.MoveTo(card.Id, state.Shared.UnusedStarters);
        var before = state.Zones.Sum(x => x.Count);
        var checkpoint = state.Random.Checkpoint;
        var expectedRandom = SeededRandom.Restore(checkpoint);
        var expectedDraws = new[] { survivorA, survivorB };
        expectedRandom.Shuffle(expectedDraws);
        Place(engine, played);
        Resolve<SelectedTrashEffect>(engine);
        var select = engine.LegalActions(0).OfType<SelectTrashCardAction>()
            .Single(x => x.Source == player.Discard.Id && x.Card.Definition == target.Definition);
        Apply(engine, select);
        Assert.Contains(target, state.Shared.Trashed.Cards);
        Assert.DoesNotContain(target, player.Hand.Cards);
        Assert.DoesNotContain(target, player.Deck.Cards);
        Assert.Contains(survivorA, player.Hand.Cards);
        Assert.Contains(survivorB, player.Hand.Cards);
        Assert.Equal(expectedDraws, player.Hand.Cards.TakeLast(2));
        Assert.Equal(expectedRandom.Checkpoint, state.Random.Checkpoint);
        Assert.Equal(before, state.Zones.Sum(x => x.Count));
    }

    [Fact]
    public void ThresholdChildrenAndSelectedDrawFinishBeforeSuspendedParent()
    {
        var (state, engine) = Ready([new ForPlayerEffect(1,
            [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 1)])])]);
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 2 };
        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.BeneGesserit ? x with { Amount = 3 } : x)];
        var intrigue = player.Intrigue.Count;
        var stock = state.Shared.IntrigueDeck.Count;
        Place(engine, player.Hand.Cards[0]);
        Resolve<InfluenceEffect>(engine);
        Assert.Equal(4, Track(player));
        Assert.Equal(intrigue + 1, player.Intrigue.Count);
        Assert.Equal(stock - 1, state.Shared.IntrigueDeck.Count);
        // The board exchange remains a separate root; its draw child completes before a foreign decision resumes.
        Resolve<SelectedTrashEffect>(engine);
        var choice = engine.LegalActions(0).OfType<SelectTrashCardAction>().First();
        var hand = player.Hand.Count;
        Apply(engine, choice);
        Assert.Equal(hand + 1, player.Hand.Count);
        Resolve<ForPlayerEffect>(engine);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Empty(engine.LegalActions(0));
        var pending = engine.Observe(1).PendingEffects.First(x => x.Kind == nameof(OptionalPaymentEffect));
        Apply(engine, new ResolveEffectAction(1, pending.Id));
        Apply(engine, new ChooseOptionAction(1, 0));
    }

    private static (GameState State, TurnEngine Engine) Ready(ImmutableArray<Effect>? agent = null)
    {
        var state = CreateState();
        state.Phase = GamePhase.PlayerTurns;
        state.Round = 1;
        state.CurrentPlayerId = state.DecisionOwnerId = 0;
        foreach (var player in state.Players)
            for (var i = 0; i < 5; i++) player.Deck.TryDrawTo(player.Hand);
        var rules = Rules(state);
        var card = state.Players[0].Hand.Cards[0];
        rules = CardEffects(rules, card.Definition, agent: agent ?? []);
        return (state, new TurnEngine(state, rules));
    }

    private static TurnRuleSet Rules(GameState state)
    {
        var official = OriginalImperiumTurnRules.Create();
        var fixture = TurnEngineFixture.Rules(state);
        return official with
        {
            Cards = [.. official.Cards, .. fixture.Cards.Where(x => x.Id.Value.StartsWith("fixture:imperiumcard:", StringComparison.Ordinal))],
            Leaders = fixture.Leaders,
            Lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules()
        };
    }

    private static void Place(TurnEngine engine, CardInstance card) =>
        Apply(engine, new PlaceAgentAction(0, Card(card), Breeding));

    private static int Track(PlayerState player) =>
        player.Influence.Single(x => x.Faction == Faction.BeneGesserit).Amount;

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var effect = engine.Observe(0).PendingEffects.First(x => x.Kind == typeof(T).Name);
        Apply(engine, new ResolveEffectAction(0, effect.Id));
    }

    private static void RejectUnchanged(TurnEngine engine, GameAction action)
    {
        var before = engine.CaptureTrustedSnapshot();
        Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);
        Assert.Equal(before.CanonicalJson, engine.CaptureTrustedSnapshot().CanonicalJson);
    }
}
