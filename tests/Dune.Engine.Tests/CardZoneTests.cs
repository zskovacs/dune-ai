using Dune.Engine.Content;
using Dune.Engine.Runtime;

namespace Dune.Engine.Tests;

public sealed class CardZoneTests
{
    [Fact]
    public void TransfersPreservePhysicalIdentityAndOrderWhileZoneDeterminesOwner()
    {
        var cards = Cards(1, 2, 3);
        var market = Zone("market", cards: cards);
        var discard = Zone("discard", owner: 0, cards: Cards(4));
        market.MoveTo(cards[1].Id, discard);

        Assert.Equal(Cards(1, 3), market.Cards);
        Assert.Equal(Cards(4, 2), discard.Cards);
        Assert.Same(cards[1], discard.Cards[1]);
        Assert.Null(market.OwnerId);
        Assert.Equal(0, discard.OwnerId);
        Assert.Equal(4, market.Count + discard.Count);

        var otherPlayer = Zone("other", owner: 1);
        discard.MoveTo(cards[1].Id, otherPlayer);
        Assert.Same(cards[1], Assert.Single(otherPlayer.Cards));
        Assert.Equal(1, otherPlayer.OwnerId);
    }

    [Fact]
    public void CardsAreReadOnlyAndDetachedFromInputAndOtherInstances()
    {
        var input = Cards(1, 2).ToList();
        var zone = Zone("deck", cards: input);
        var independent = Zone("deck", cards: input);
        input.Clear();
        Assert.Equal(Cards(1, 2), zone.Cards);
        Assert.IsNotType<List<CardInstance>>(zone.Cards);
        var mutableView = Assert.IsAssignableFrom<IList<CardInstance>>(zone.Cards);
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.Throws<NotSupportedException>(() => mutableView[0] = Cards(9)[0]);

        zone.TryDrawTo(Zone("hand"));
        Assert.Single(zone.Cards);
        Assert.Equal(Cards(1, 2), independent.Cards);
    }

    [Fact]
    public void DrawUsesIndexZeroAndEmptyDrawIsExplicit()
    {
        var deck = Zone("deck", cards: Cards(3, 1, 2));
        var hand = Zone("hand", owner: 0, visibility: ZoneVisibility.OwnerOnly);
        Assert.Equal(Cards(3)[0], deck.TryDrawTo(hand));
        Assert.Equal(Cards(1, 2), deck.Cards);
        Assert.Equal(Cards(1)[0], deck.TryDrawTo(hand));
        Assert.Equal(Cards(2)[0], deck.TryDrawTo(hand));
        Assert.Null(deck.TryDrawTo(hand));
        Assert.Empty(deck.Cards);
        Assert.Equal(Cards(3, 1, 2), hand.Cards);
        Assert.Equal(ZoneVisibility.OwnerOnly, hand.Visibility);
    }

    [Fact]
    public void EmptyDeckRecyclesExplicitDiscardReproduciblyAndConservesCards()
    {
        var firstDeck = Zone("deck", owner: 0);
        var secondDeck = Zone("deck", owner: 0);
        var firstDiscard = Zone("discard", owner: 0, cards: Cards(1, 2, 3, 4, 5, 6));
        var secondDiscard = Zone("discard", owner: 0, cards: firstDiscard.Cards);
        var firstHand = Zone("hand", owner: 0);
        var secondHand = Zone("hand", owner: 0);
        var firstRandom = new SeededRandom(42);
        var secondRandom = new SeededRandom(42);

        for (var draw = 0; draw < 6; draw++)
        {
            Assert.Equal(firstDeck.TryDrawTo(firstHand, firstDiscard, firstRandom),
                secondDeck.TryDrawTo(secondHand, secondDiscard, secondRandom));
            Assert.Empty(firstDiscard.Cards);
            Assert.Equal(6 - draw - 1, firstDeck.Count);
            Assert.Equal(draw + 1, firstHand.Count);
            Assert.Equal(Enumerable.Range(1, 6).Select(id => new CardInstanceId(id)),
                firstDeck.Cards.Concat(firstHand.Cards).Select(card => card.Id).OrderBy(id => id.Value));
        }
        Assert.Null(firstDeck.TryDrawTo(firstHand, firstDiscard, firstRandom));
        Assert.Equal(firstRandom.Checkpoint, secondRandom.Checkpoint);
        Assert.Equal(firstHand.Cards, secondHand.Cards);
    }

    [Fact]
    public void NonemptyDeckDoesNotRecycleOrShuffleDiscard()
    {
        var deck = Zone("deck", cards: Cards(1, 2));
        var discard = Zone("discard", cards: Cards(3, 4));
        var hand = Zone("hand");
        var random = new SeededRandom(1);
        Assert.Equal(Cards(1)[0], deck.TryDrawTo(hand, discard, random));
        Assert.Equal(Cards(2), deck.Cards);
        Assert.Equal(Cards(3, 4), discard.Cards);
        Assert.Equal(new RandomCheckpoint(1, 0), random.Checkpoint);
    }

    [Fact]
    public void InvalidMovesLeaveBothZonesUntouched()
    {
        var source = Zone("source", cards: Cards(1, 2));
        var destination = Zone("destination", cards: Cards(3));
        Assert.Throws<ArgumentException>(() => source.MoveTo(new CardInstanceId(99), destination));
        Assert.Throws<ArgumentException>(() => source.MoveTo(new CardInstanceId(1), source));
        Assert.Throws<ArgumentException>(() => source.TryDrawTo(source));
        Assert.Throws<ArgumentException>(() => source.MoveTo(new CardInstanceId(1), Zone("source")));
        Assert.Throws<ArgumentNullException>(() => source.MoveTo(new CardInstanceId(1), null!));
        Assert.Equal(Cards(1, 2), source.Cards);
        Assert.Equal(Cards(3), destination.Cards);

        var duplicated = Zone("duplicated", cards: [new CardInstance(new CardInstanceId(1), new ContentId("other"))]);
        Assert.Throws<ArgumentException>(() => source.MoveTo(new CardInstanceId(1), duplicated));
        Assert.Throws<ArgumentException>(() => source.TryDrawTo(duplicated));
        Assert.Equal(Cards(1, 2), source.Cards);
        Assert.Single(duplicated.Cards);
    }

    [Fact]
    public void InvalidRefillValidatesBeforeMovingCardsOrConsumingRandomness()
    {
        var deck = Zone("deck", owner: 0);
        var discard = Zone("discard", owner: 0, cards: Cards(1, 2, 3));
        var destination = Zone("hand", owner: 0, cards: Cards(3));
        var random = new SeededRandom(12);
        Assert.Throws<ArgumentException>(() => deck.TryDrawTo(destination, discard, random));
        Assert.Throws<ArgumentException>(() => deck.TryDrawTo(discard, discard, random));
        Assert.Throws<ArgumentException>(() => deck.TryDrawTo(destination, deck, random));
        Assert.Throws<ArgumentException>(() => deck.TryDrawTo(destination,
            Zone("otherDiscard", owner: 1, cards: Cards(7)), random));
        Assert.Throws<ArgumentNullException>(() => deck.TryDrawTo(destination, discard, null!));
        Assert.Empty(deck.Cards);
        Assert.Equal(Cards(1, 2, 3), discard.Cards);
        Assert.Equal(Cards(3), destination.Cards);
        Assert.Equal(new RandomCheckpoint(12, 0), random.Checkpoint);
    }

    [Fact]
    public void ConstructorRejectsDuplicatePhysicalIdsAndInvalidZoneMetadata()
    {
        Assert.Throws<ArgumentException>(() => Zone("deck", cards: Cards(1, 1)));
        Assert.Throws<ArgumentException>(() => Zone("deck", cards:
            [Cards(1)[0], new CardInstance(new CardInstanceId(1), new ContentId("other"))]));
        Assert.Throws<ArgumentNullException>(() => Zone("deck", cards: [null!]));
        Assert.Throws<ArgumentException>(() => Zone("deck", visibility: ZoneVisibility.OwnerOnly));
        Assert.Throws<ArgumentException>(() => Zone(" "));
        Assert.Throws<ArgumentException>(() => new CardZone(new CardZoneId("id"), " ", ZoneVisibility.Public));
        Assert.Throws<ArgumentOutOfRangeException>(() => Zone("deck", owner: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Zone("deck", visibility: (ZoneVisibility)999));
    }

    [Fact]
    public void ExplicitShufflePreservesInstancesAndUsesSuppliedRandomState()
    {
        var cards = Cards(1, 2, 3, 4, 5, 6);
        var first = Zone("deck", cards: cards, visibility: ZoneVisibility.Hidden);
        var second = Zone("deck", cards: cards);
        var random = new SeededRandom(77);
        var restored = SeededRandom.Restore(random.Checkpoint);
        first.Shuffle(random);
        second.Shuffle(restored);
        Assert.Equal(first.Cards, second.Cards);
        Assert.Equal(cards.OrderBy(card => card.Id.Value), first.Cards.OrderBy(card => card.Id.Value));
        Assert.All(first.Cards, card => Assert.Same(cards.Single(original => original.Id == card.Id), card));
        Assert.Equal(ZoneVisibility.Hidden, first.Visibility);
        Assert.Equal(random.Checkpoint, restored.Checkpoint);
    }

    private static CardInstance[] Cards(params long[] ids) => ids
        .Select(id => new CardInstance(new CardInstanceId(id), new ContentId("synthetic.card"))).ToArray();

    private static CardZone Zone(string id, int? owner = null, IEnumerable<CardInstance>? cards = null,
        ZoneVisibility visibility = ZoneVisibility.Public) => new(new CardZoneId(id), id, visibility, owner, cards);
}
