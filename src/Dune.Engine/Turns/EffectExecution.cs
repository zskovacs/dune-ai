using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public enum EffectGroupMode { Sequence, PlayerOrdered }
public enum ExecutionContinuationKind { ParentFrame, AgentTurn, RevealTurn, CombatOpportunity, ConflictRewards, RoundStart, Endgame }
public enum EffectProvenance { Card, Board, Acquisition, Signet }

/// <summary>Typed return destination. Future lifecycle owners bind their window/reward identifiers here.</summary>
public sealed record ExecutionContinuation(ExecutionContinuationKind Kind, int? PlayerId = null,
    int? WindowId = null, int? RewardId = null);

/// <summary>Roles and physical/definition provenance stay bound while child work suspends its parent.</summary>
public sealed record EffectContext(int InitiatorId, int DecisionOwnerId, int PayerId, int RecipientId,
    CardInstanceId? SourceCard, ContentId? SourceDefinition, BoardSpaceId? Space,
    EffectProvenance Provenance, int? ParentFrameId = null)
{
    [JsonIgnore]
    public int OwnerId => DecisionOwnerId;
}

public sealed record PendingEffect(int Id, Effect Effect, EffectContext Context);
public sealed record DecisionFrame(Effect Effect, EffectContext Context);

/// <summary>Episode-local immutable execution data; only the last frame can act.</summary>
public sealed record EffectExecutionFrame(int Id, int? ParentId, EffectGroupMode Mode, int ChooserId,
    ImmutableArray<PendingEffect> Remaining, ExecutionContinuation Continuation);
