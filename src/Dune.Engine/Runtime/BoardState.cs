using System.Collections.Immutable;
using Dune.Engine.Setup;

namespace Dune.Engine.Runtime;

public enum AgentKind
{
    Agent,
    Swordmaster,
    Mentat
}

public sealed record AgentPlacement(int PlayerId, AgentKind Kind);

public sealed record AllianceToken(Faction Faction, int? OwnerId);

public sealed record MentatLocation(BoardSpaceId? Space, int? OwnerId);

public sealed record ResourceBank(bool IsUnlimited);

public sealed class BoardSpaceState
{
    internal BoardSpaceState(BoardSpaceDefinition definition) => Definition = definition;
    public BoardSpaceDefinition Definition { get; }
    public ImmutableArray<AgentPlacement> Occupants { get; internal set; } = [];
    public int BonusSpice { get; internal set; }
    public int? ControllerId { get; internal set; }
}

public sealed class BoardState
{
    internal BoardState(BoardDefinition definition, ConflictState conflict)
    {
        Definition = definition;
        Conflict = conflict;
        Spaces = definition.Spaces.Select(x => new BoardSpaceState(x)).ToList().AsReadOnly();
        Alliances = Enum.GetValues<Faction>().Select(x => new AllianceToken(x, null)).ToImmutableArray();
        Mentat = new(definition.InitialMentatSpace, null);
    }

    public BoardDefinition Definition { get; }
    public ConflictState Conflict { get; }
    public IReadOnlyList<BoardSpaceState> Spaces { get; }
    public ImmutableArray<AllianceToken> Alliances { get; internal set; }
    public MentatLocation Mentat { get; internal set; }
    public ResourceBank Bank { get; } = new(true);
}
