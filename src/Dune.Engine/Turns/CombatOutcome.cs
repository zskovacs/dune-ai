using System.Collections.Immutable;

namespace Dune.Engine.Turns;

public sealed record CombatParticipant(int PlayerId, int Strength);

public sealed record CombatPlacement(ImmutableArray<int> PlayerIds, int Rank, int? RewardTier, bool IsTie);

/// <summary>Frozen Combat ranking. Player IDs within a tie are in canonical seat order.</summary>
public sealed record CombatOutcome(ImmutableArray<CombatPlacement> Placements, int? WinnerId,
    ImmutableArray<int> NoRewardPlayerIds)
{
    public static CombatOutcome Calculate(IReadOnlyList<CombatParticipant> participants, int playerCount)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (playerCount is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(playerCount));
        if (participants.Count > playerCount || participants.Any(x => x is null || x.PlayerId < 0 ||
            x.PlayerId >= playerCount || x.Strength < 0) ||
            participants.Select(x => x.PlayerId).Distinct().Count() != participants.Count)
            throw new ArgumentException("Combat participants must have unique valid seats and nonnegative strength.", nameof(participants));

        var ranked = participants.Where(x => x.Strength > 0).GroupBy(x => x.Strength)
            .OrderByDescending(x => x.Key).ToArray();
        var placements = ImmutableArray.CreateBuilder<CombatPlacement>(ranked.Length);
        var noReward = participants.Where(x => x.Strength == 0).Select(x => x.PlayerId).ToList();
        var rank = 1;
        foreach (var group in ranked)
        {
            var seats = group.Select(x => x.PlayerId).Order().ToImmutableArray();
            var tier = rank switch
            {
                1 when seats.Length > 1 => 2,
                1 => 1,
                2 when seats.Length > 1 => 3,
                2 => 2,
                3 when seats.Length == 1 && playerCount == 4 => 3,
                _ => (int?)null,
            };
            placements.Add(new(seats, rank, tier, seats.Length > 1));
            if (tier is null) noReward.AddRange(seats);
            rank += seats.Length;
        }
        var winnerId = placements.Count > 0 && placements[0].RewardTier == 1
            ? placements[0].PlayerIds[0] : (int?)null;
        return new(placements.MoveToImmutable(), winnerId, [.. noReward.Order()]);
    }
}

/// <summary>Frozen, trusted reward scheduling. Remaining work is captured with its existing execution frames.</summary>
public sealed record ConflictRewardRecipient(int PlayerId, int Tier, ImmutableArray<Effect> Effects);
public sealed record ConflictRewardProgress(CombatOutcome Outcome, ImmutableArray<ConflictRewardRecipient> Recipients,
    int CurrentRecipient, ImmutableArray<PendingEffect> RemainingRewardWork, ImmutableArray<int> ClosedPostWinOpportunities, int? RootFrameId);
