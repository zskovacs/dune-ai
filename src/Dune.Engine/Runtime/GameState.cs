using System.Collections.Immutable;
using Dune.Engine.Rulesets;
using Dune.Engine.Turns;

namespace Dune.Engine.Runtime;

public enum GamePhase
{
    BeforeRoundStart,
    RoundStart,
    PlayerTurns,
    Combat,
    Makers,
    Recall,
    Endgame,
    Completed
}

public enum ContentAuthenticity
{
    VerifiedCatalog,
    SyntheticFixture
}

/// <summary>Future modules must supply an explicit snapshot contract before snapshotting is supported.</summary>
public abstract class ModuleExtensionState
{
    internal ModuleExtensionState()
    {
    }
}

public sealed class ConflictState
{
    internal ConflictState(CardZone deck, CardZone unused)
    {
        Deck = deck;
        Unused = unused;
        Revealed = new(new("conflict:revealed"), "Revealed Conflict", ZoneVisibility.Public);
        Discard = new(new("conflict:discard"), "Conflict discard", ZoneVisibility.Public);
    }

    public CardZone Deck { get; }
    public CardZone Unused { get; }
    public CardZone Revealed { get; }
    public CardZone Discard { get; }
}

public sealed class SharedCardState
{
    internal SharedCardState(CardZone imperiumDeck, CardZone row, CardZone intrigueDeck,
        IEnumerable<CardZone> reserves, CardZone unusedStarters)
    {
        ImperiumDeck = imperiumDeck;
        ImperiumRow = row;
        IntrigueDeck = intrigueDeck;
        Reserves = reserves.ToList().AsReadOnly();
        UnusedStarters = unusedStarters;
    }

    public CardZone ImperiumDeck { get; }
    public CardZone ImperiumRow { get; }
    public CardZone IntrigueDeck { get; }
    public IReadOnlyList<CardZone> Reserves { get; }
    public CardZone UnusedStarters { get; }
    public CardZone ImperiumDiscard { get; } = new(new("imperium:discard"), "Imperium discard", ZoneVisibility.Public);
    public CardZone IntrigueDiscard { get; } = new(new("intrigue:discard"), "Intrigue discard", ZoneVisibility.Public);
    public CardZone IntrigueResolving { get; } = new(new("intrigue:resolving"), "Resolving Intrigue", ZoneVisibility.Public);
    public CardZone Trashed { get; } = new(new("shared:trashed"), "Trashed cards", ZoneVisibility.Public);
}

/// <summary>Trusted complete episode state, including hidden cards. Never pass this object to a policy.</summary>
public sealed class GameState
{
    internal GameState(ResolvedGameDefinition definition, string configurationIdentity, string contentIdentity,
        ContentAuthenticity authenticity, IEnumerable<PlayerState> players, BoardState board,
        SharedCardState shared, SeededRandom random, SeededRandom leadersRandom,
        int firstPlayerId)
    {
        Definition = definition;
        ConfigurationIdentity = configurationIdentity;
        ContentIdentity = contentIdentity;
        Authenticity = authenticity;
        Players = players.ToList().AsReadOnly();
        Board = board;
        Shared = shared;
        Random = random;
        LeadersRandom = leadersRandom;
        FirstPlayerId = firstPlayerId;
        Zones = Players.SelectMany(x => new[] { x.Deck, x.Hand, x.Discard, x.InPlay, x.Intrigue })
            .Concat(new[]
            {
                shared.ImperiumDeck, shared.ImperiumRow, shared.IntrigueDeck, shared.UnusedStarters,
                shared.ImperiumDiscard, shared.IntrigueDiscard, shared.IntrigueResolving, shared.Trashed,
                Conflict.Deck, Conflict.Unused, Conflict.Revealed, Conflict.Discard
            })
            .Concat(shared.Reserves).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToList().AsReadOnly();
    }

    public ResolvedGameDefinition Definition { get; }
    public string ConfigurationIdentity { get; }
    public string ContentIdentity { get; }
    public ContentAuthenticity Authenticity { get; }
    public IReadOnlyList<PlayerState> Players { get; }
    public BoardState Board { get; }
    public SharedCardState Shared { get; }
    public ConflictState Conflict => Board.Conflict;
    public IReadOnlyList<CardZone> Zones { get; }
    public int Round { get; internal set; }
    public GamePhase Phase { get; internal set; } = GamePhase.BeforeRoundStart;
    public int FirstPlayerId { get; internal set; }
    public int? DecisionOwnerId { get; internal set; }
    public int? CurrentPlayerId { get; internal set; }
    public bool IsCompleted { get; internal set; }
    internal DecisionWindowState? DecisionWindow { get; set; }
    public ImmutableArray<ActiveEffect> ActiveEffects { get; internal set; } = [];
    public ImmutableArray<ModuleExtensionState> Extensions { get; internal set; } = [];
    internal SeededRandom Random { get; }
    internal SeededRandom LeadersRandom { get; }

    public GameObservation Observe(int playerId) => ObservationProjection.Create(this, playerId);
    public TrustedGameSnapshot CaptureTrustedSnapshot() => TrustedGameSnapshot.Capture(this);
}
