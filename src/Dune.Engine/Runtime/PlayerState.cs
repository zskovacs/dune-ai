using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Runtime;

public sealed record PlayerResources(int Solari, int Spice, int Water, int VictoryPoints);
public sealed record PlayerUnits(int AvailableAgents, int UnacquiredSwordmasters, int TroopsInSupply,
    int TroopsInGarrison, int TroopsInConflict, int ControlMarkers)
{
    public int OrdinaryAgents { get; init; }
    public int AcquiredSwordmasters { get; init; }
    public int AvailableSwordmasters { get; init; }
    public int AvailableMentats { get; init; }
}
public sealed record FactionInfluence(Faction Faction, int Amount);
public sealed record TemporaryPlayerState(int Persuasion, int Swords, bool HasRevealed);
public sealed record ActiveEffect(ContentId Source, string EffectId);

public sealed class PlayerState
{
    internal PlayerState(int id, ContentId leader, InitialPlayerSetup setup, CardZone deck,
        ImmutableArray<StartingCardRecipe> composition)
    {
        Id = id;
        Leader = leader;
        Resources = new(setup.Solari, setup.Spice, setup.Water, setup.VictoryPoints);
        Units = new(setup.AvailableAgents, setup.UnacquiredSwordmasters, setup.TroopsInSupply,
            setup.TroopsInGarrison, 0, setup.ControlMarkers)
        { OrdinaryAgents = setup.AvailableAgents };
        Influence = Enum.GetValues<Faction>().Select(x => new FactionInfluence(x, 0)).ToImmutableArray();
        Deck = deck;
        Hand = Zone("hand", ZoneVisibility.OwnerOnly);
        Discard = Zone("discard", ZoneVisibility.Public);
        InPlay = Zone("in-play", ZoneVisibility.Public);
        Intrigue = Zone("intrigue", ZoneVisibility.OwnerOnly);
        StartingComposition = composition;
    }

    public int Id { get; }
    public ContentId Leader { get; }
    public PlayerResources Resources { get; internal set; }
    public PlayerUnits Units { get; internal set; }
    public ImmutableArray<FactionInfluence> Influence { get; internal set; }
    public bool HasHighCouncilSeat { get; internal set; }
    public int CombatMarker { get; internal set; }
    public TemporaryPlayerState Temporary { get; internal set; } = new(0, 0, false);
    public ImmutableArray<ActiveEffect> ActiveEffects { get; internal set; } = [];
    public CardZone Deck { get; }
    public CardZone Hand { get; }
    public CardZone Discard { get; }
    public CardZone InPlay { get; }
    public CardZone Intrigue { get; }
    public ImmutableArray<StartingCardRecipe> StartingComposition { get; }

    private CardZone Zone(string name, ZoneVisibility visibility) =>
        new(new($"player:{Id}:{name}"), name, visibility, Id);
}
