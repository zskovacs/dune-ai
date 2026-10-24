namespace Dune.Engine.Toy;

/// <summary>Public turn data and only the requesting player's private marker.</summary>
public sealed record ToyObservation(
    int PlayerId,
    int PrivateMarker,
    int RemainingTokens,
    int TurnNumber,
    int? CurrentPlayerId,
    int? DecisionOwnerId,
    ToyPhase Phase,
    bool IsTerminal);
