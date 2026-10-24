namespace Dune.Engine;

/// <summary>A sequential game boundary. Agents receive observations and legal actions, never this object.</summary>
/// <remarks>Call Reset before reading state. Player IDs are zero-based; terminal games have no decision owner.</remarks>
public interface IGame<in TConfig, out TObservation, TAction, out TResult>
{
    int PlayerCount { get; }
    int? CurrentPlayerId { get; }
    int? DecisionOwnerId { get; }
    string Phase { get; }
    bool IsTerminal { get; }
    void Reset(int seed, TConfig config);
    TObservation Observe(int playerId);
    IReadOnlyList<TAction> LegalActions(int playerId);
    void Step(TAction action);
    TResult GetResult();
}
