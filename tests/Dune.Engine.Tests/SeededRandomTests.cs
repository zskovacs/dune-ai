using Dune.Engine.Runtime;

namespace Dune.Engine.Tests;

public sealed class SeededRandomTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-123)]
    public void EqualSeedsReproduceMixedBoundsAndShuffles(int seed)
    {
        var first = new SeededRandom(seed);
        var second = new SeededRandom(seed);
        int[] bounds = [1, 2, 3, 17, 1073741824, int.MaxValue];
        for (var repeat = 0; repeat < 20; repeat++)
        {
            foreach (var bound in bounds)
            {
                var value = first.Next(bound);
                Assert.InRange(value, 0, bound - 1);
                Assert.Equal(value, second.Next(bound));
            }
        }
        var firstOrder = Enumerable.Range(0, 30).ToArray();
        var secondOrder = Enumerable.Range(0, 30).ToArray();
        first.Shuffle(firstOrder);
        second.Shuffle(secondOrder);
        Assert.Equal(firstOrder, secondOrder);
        Assert.Equal(Enumerable.Range(0, 30), firstOrder.Order());
        Assert.Equal(first.Checkpoint, second.Checkpoint);
    }

    [Fact]
    public void RejectionSamplingCountsEveryRawDrawAndRestoresContinuation()
    {
        const int seed = 42;
        const int bound = 1073741824;
        var random = new SeededRandom(seed);
        var raw = new Random(seed);
        long rawDraws = 0;
        for (var sample = 0; sample < 100; sample++)
        {
            int expected;
            do
            {
                expected = raw.Next();
                rawDraws++;
            } while (expected >= bound);
            Assert.Equal(expected, random.Next(bound));
        }
        Assert.True(rawDraws > 100);
        Assert.Equal(new RandomCheckpoint(seed, rawDraws), random.Checkpoint);

        var checkpoint = random.Checkpoint;
        var restored = SeededRandom.Restore(checkpoint);
        Assert.Equal(checkpoint, restored.Checkpoint);
        int[] bounds = [7, int.MaxValue, bound, 1, 99];
        foreach (var nextBound in bounds)
        {
            Assert.Equal(random.Next(nextBound), restored.Next(nextBound));
        }
        var originalOrder = Enumerable.Range(0, 50).ToArray();
        var restoredOrder = Enumerable.Range(0, 50).ToArray();
        random.Shuffle(originalOrder);
        restored.Shuffle(restoredOrder);
        Assert.Equal(originalOrder, restoredOrder);
        Assert.Equal(random.Checkpoint, restored.Checkpoint);
    }

    [Fact]
    public void SeparateInstancesDoNotShareRandomState()
    {
        var first = new SeededRandom(12);
        var untouched = new SeededRandom(12);
        for (var index = 0; index < 100; index++)
        {
            first.Next(20);
        }
        Assert.Equal(new RandomCheckpoint(12, 0), untouched.Checkpoint);
        Assert.Equal(new SeededRandom(12).Next(20), untouched.Next(20));
        Assert.NotEqual(new SeededRandom(13).Next(int.MaxValue), new SeededRandom(12).Next(int.MaxValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidBoundsDoNotAdvanceRandomState(int upperExclusive)
    {
        var random = new SeededRandom(4);
        var before = random.Checkpoint;
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(upperExclusive));
        Assert.Equal(before, random.Checkpoint);
    }

    [Fact]
    public void InvalidCheckpointIsRejectedAndEmptyShuffleDoesNotConsumeRandomness()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeededRandom.Restore(new RandomCheckpoint(1, -1)));
        var random = new SeededRandom(7);
        random.Shuffle(Array.Empty<int>());
        random.Shuffle(new[] { 1 });
        Assert.Equal(new RandomCheckpoint(7, 0), random.Checkpoint);
        Assert.Equal(random.Checkpoint, SeededRandom.Restore(random.Checkpoint).Checkpoint);
    }
}
