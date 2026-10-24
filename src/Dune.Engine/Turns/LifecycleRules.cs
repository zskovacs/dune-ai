using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public enum IntrigueTiming { Plot, Combat, PostWin, Endgame }
public enum CombatParticipationPolicy { OriginalTroops }
public enum CombatCleanupPolicy { ReturnTroopsToSupply }

public sealed record IntrigueRule(ContentId Id, IntrigueTiming Timing, ResourceCost Cost,
    ImmutableArray<Effect> Effects, ImmutableArray<SourceReference> Sources);
public sealed record ConflictRewardTier(int Tier, ImmutableArray<Effect> Effects);
public sealed record ConflictRule(ContentId Id, ConflictTier Tier, ImmutableArray<ConflictRewardTier> Rewards,
    BoardSpaceId? ControlSpace, ImmutableArray<SourceReference> Sources);
/// <summary>Printed reward on every upward crossing of four; earned rewards survive subsequent Influence loss.</summary>
public sealed record FactionRule(Faction Faction, int MaximumInfluence, ImmutableArray<Effect> ThresholdFourReward,
    ImmutableArray<SourceReference> Sources);
public sealed record LifecycleRuleSet(string Version, ImmutableArray<IntrigueRule> Intrigues,
    ImmutableArray<ConflictRule> Conflicts = default, ImmutableArray<FactionRule> Factions = default)
{
    public CombatParticipationPolicy Participation { get; init; } = CombatParticipationPolicy.OriginalTroops;
    public CombatCleanupPolicy Cleanup { get; init; } = CombatCleanupPolicy.ReturnTroopsToSupply;
}
