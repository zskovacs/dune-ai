using Dune.Engine.Turns;

namespace Dune.Engine.Tests;

public sealed class CombatOutcomeTests
{
    public static TheoryData<int[], int?[], int?> RewardCases => new()
    {
        { [8, 6, 4], [1, 2, null], 0 },
        { [8, 8, 4], [2, 2, null], null },
        { [8, 6, 6], [1, 3, 3], 0 },
        { [8, 8, 8], [2, 2, 2], null },
        { [8, 6, 4, 2], [1, 2, 3, null], 0 },
        { [8, 8, 4, 2], [2, 2, 3, null], null },
        { [8, 8, 4, 4], [2, 2, null, null], null },
        { [8, 6, 6, 2], [1, 3, 3, null], 0 },
        { [8, 6, 4, 4], [1, 2, null, null], 0 },
        { [8, 6, 6, 6], [1, 3, 3, 3], 0 },
        { [8, 8, 8, 8], [2, 2, 2, 2], null },
        { [0, 0, 0, 0], [null, null, null, null], null },
        { [8, 0, 0], [1, null, null], 0 },
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public void OfficialPlacementTiersMatchExactTieTable(int[] strengths, int?[] tiers, int? winner)
    {
        var outcome = CombatOutcome.Calculate(strengths.Select((strength, seat) => new CombatParticipant(seat, strength)).ToArray(), strengths.Length);
        Assert.Equal(winner, outcome.WinnerId);
        var actual = new int?[strengths.Length];
        foreach (var placement in outcome.Placements)
        {
            Assert.Equal(placement.PlayerIds.Length > 1, placement.IsTie);
            Assert.Equal(placement.PlayerIds.Order(), placement.PlayerIds);
            foreach (var seat in placement.PlayerIds) actual[seat] = placement.RewardTier;
        }
        Assert.Equal(tiers, actual);
        Assert.Equal(Enumerable.Range(0, strengths.Length).Where(seat => tiers[seat] is null), outcome.NoRewardPlayerIds);
    }

    [Fact]
    public void PermutationsKeepCanonicalTieSeatsAndCompetitiveRanks()
    {
        var participants = new[] { new CombatParticipant(3, 4), new CombatParticipant(2, 8),
            new CombatParticipant(0, 8), new CombatParticipant(1, 2) };
        var expected = CombatOutcome.Calculate(participants, 4);
        Assert.Equal([0, 2], expected.Placements[0].PlayerIds);
        Assert.Equal(3, expected.Placements[1].Rank);
        foreach (var offset in Enumerable.Range(0, participants.Length))
        {
            var actual = CombatOutcome.Calculate([.. participants.Skip(offset), .. participants.Take(offset)], 4);
            Assert.Equal(expected.WinnerId, actual.WinnerId);
            Assert.Equal(expected.NoRewardPlayerIds, actual.NoRewardPlayerIds);
            Assert.Equal(expected.Placements.Select(x => (x.Rank, x.RewardTier, x.IsTie, Seats: string.Join(',', x.PlayerIds))),
                actual.Placements.Select(x => (x.Rank, x.RewardTier, x.IsTie, Seats: string.Join(',', x.PlayerIds))));
        }
    }

    [Fact]
    public void ASinglePositiveParticipantWinsAndUnlistedSeatsAreNotInvented()
    {
        var outcome = CombatOutcome.Calculate([new(2, 5)], 4);
        Assert.Equal(2, outcome.WinnerId);
        Assert.Equal(1, Assert.Single(outcome.Placements).RewardTier);
        Assert.Empty(outcome.NoRewardPlayerIds);
        Assert.Empty(CombatOutcome.Calculate([], 3).Placements);
    }

    [Fact]
    public void InvalidPlayerCountsSeatsDuplicatesAndStrengthsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatOutcome.Calculate([], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatOutcome.Calculate([], 5));
        Assert.Throws<ArgumentException>(() => CombatOutcome.Calculate([new(-1, 1)], 3));
        Assert.Throws<ArgumentException>(() => CombatOutcome.Calculate([new(3, 1)], 3));
        Assert.Throws<ArgumentException>(() => CombatOutcome.Calculate([new(0, -1)], 3));
        Assert.Throws<ArgumentException>(() => CombatOutcome.Calculate([new(0, 1), new(0, 2)], 3));
    }
}
