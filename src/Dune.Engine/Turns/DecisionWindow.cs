using System.Collections.Immutable;
using Dune.Engine.Content;

namespace Dune.Engine.Turns;

public enum DecisionWindowKind { Combat, PostWinRewards, Endgame }

/// <summary>Trusted episode state. A played card suspends priority until its frame returns.</summary>
public sealed record DecisionWindowState(int Id, DecisionWindowKind Kind, ImmutableArray<int> EligibleSeats,
    int ClockwiseCursor, int PriorityOwnerId, int OpportunityPlays, ImmutableArray<int> ConsecutivePasses,
    ExecutionContinuation Continuation, int? SuspendedFrameId = null, CardInstanceId? ResolvingCardId = null)
{
    public int? RewardFrameId { get; init; }
}

/// <summary>Detached public priority evidence; never includes eligible private card identities.</summary>
public sealed record DecisionWindowSummary(int Id, DecisionWindowKind Kind, ImmutableArray<int> EligibleSeats,
    int ClockwiseCursor, int PriorityOwnerId, int OpportunityPlays, ImmutableArray<int> ConsecutivePasses,
    bool IsSuspended);
