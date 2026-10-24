using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Runtime;

public sealed record ObservedCard(ContentId Definition, string Name);

public sealed record ObservedZone(string Name, int? OwnerId, int Count, ImmutableArray<ObservedCard> Cards);

public sealed record ObservedPlayer(
    int Id,
    ObservedCard Leader,
    PlayerResources Resources,
    PlayerUnits Units,
    ImmutableArray<FactionInfluence> Influence,
    bool HasHighCouncilSeat,
    int CombatMarker,
    TemporaryPlayerState Temporary,
    int DeckCount,
    int HandCount,
    int IntrigueCount,
    ImmutableArray<ObservedCard> Discard,
    ImmutableArray<ObservedCard> InPlay);

public sealed record ObservedBoardSpace(
    BoardSpaceId Id,
    string Name,
    bool AccumulatesSpice,
    bool Controllable,
    ImmutableArray<AgentPlacement> Occupants,
    int BonusSpice,
    int? ControllerId);

public sealed record ObservedBoard(
    ImmutableArray<ObservedBoardSpace> Spaces,
    ImmutableArray<AllianceToken> Alliances,
    MentatLocation Mentat,
    ResourceBank Bank);

/// <summary>Detached policy view. Contains no hidden deck identities, ordering, seed or trusted state reference.</summary>
public sealed record GameObservation(
    int PlayerId,
    int Round,
    GamePhase Phase,
    int FirstPlayerId,
    int? DecisionOwnerId,
    int? CurrentPlayerId,
    bool IsCompleted,
    ImmutableArray<ObservedPlayer> Players,
    ObservedBoard Board,
    ImmutableArray<ObservedZone> PublicZones,
    int ImperiumDeckCount,
    int IntrigueDeckCount,
    int ConflictDeckCount,
    ImmutableArray<ObservedCard> Hand,
    ImmutableArray<ObservedCard> Intrigue,
    ImmutableArray<StartingCardRecipe> StartingComposition)
{
    public DecisionWindowSummary? Window { get; init; }
}

internal static class ObservationProjection
{
    internal static GameObservation Create(GameState state, int playerId)
    {
        var own = state.Players.FirstOrDefault(x => x.Id == playerId)
                  ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        var metadata = state.Definition.Content.ToDictionary(x => x.Id);
        ObservedCard Card(ContentId id) => new(id, metadata[id].Name);

        ImmutableArray<ObservedCard> Cards(CardZone zone) =>
            zone.Cards.Select(x => Card(x.Definition)).ToImmutableArray();

        var players = state.Players.Select(x => new ObservedPlayer(x.Id, Card(x.Leader), x.Resources, x.Units,
            x.Influence, x.HasHighCouncilSeat, x.CombatMarker, x.Temporary,
            x.Deck.Count, x.Hand.Count, x.Intrigue.Count, Cards(x.Discard), Cards(x.InPlay))).ToImmutableArray();
        return new(playerId, state.Round, state.Phase, state.FirstPlayerId, state.DecisionOwnerId,
            state.CurrentPlayerId, state.IsCompleted, players, Board(state.Board),
            state.Zones.Where(x => x.Visibility == ZoneVisibility.Public).Select(x =>
                new ObservedZone(x.Name, x.OwnerId, x.Count, Cards(x))).ToImmutableArray(),
            state.Shared.ImperiumDeck.Count, state.Shared.IntrigueDeck.Count, state.Conflict.Deck.Count,
            Cards(own.Hand), Cards(own.Intrigue), own.StartingComposition)
        {
            Window = state.DecisionWindow is { } window ? new(window.Id, window.Kind, window.EligibleSeats,
                window.ClockwiseCursor, window.PriorityOwnerId, window.OpportunityPlays, window.ConsecutivePasses,
                window.SuspendedFrameId is not null) : null,
        };
    }

    internal static ObservedBoard Board(BoardState board) => new(board.Spaces.Select(x =>
            new ObservedBoardSpace(x.Definition.Id, x.Definition.Name, x.Definition.AccumulatesSpice,
                x.Definition.Controllable, x.Occupants, x.BonusSpice, x.ControllerId)).ToImmutableArray(),
        board.Alliances, board.Mentat, board.Bank);
}
