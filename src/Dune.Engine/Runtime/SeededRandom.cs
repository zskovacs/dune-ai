namespace Dune.Engine.Runtime;

/// <summary>Trusted RNG checkpoint; replay requires the same pinned runtime implementation.</summary>
public readonly record struct RandomCheckpoint(int Seed, long RawDrawCount);

/// <summary>Episode-local System.Random adapter. No cross-runtime replay guarantee is made.</summary>
internal sealed class SeededRandom
{
    private readonly Random _random;
    private long _rawDrawCount;

    internal SeededRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    internal int Seed { get; }
    internal RandomCheckpoint Checkpoint => new(Seed, _rawDrawCount);

    internal static SeededRandom Restore(RandomCheckpoint checkpoint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(checkpoint.RawDrawCount);
        var result = new SeededRandom(checkpoint.Seed);
        for (long index = 0; index < checkpoint.RawDrawCount; index++)
        {
            result.NextRaw();
        }
        return result;
    }

    /// <summary>Samples [0, upperExclusive), rejecting the incomplete remainder of the raw range.</summary>
    internal int Next(int upperExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(upperExclusive);
        // Random.Next() has int.MaxValue possible results, not int.MaxValue + 1.
        var limit = int.MaxValue - int.MaxValue % upperExclusive;
        int value;
        do
        {
            value = NextRaw();
        } while (value >= limit);
        return value % upperExclusive;
    }

    internal void Shuffle<T>(IList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private int NextRaw()
    {
        if (_rawDrawCount == long.MaxValue)
        {
            throw new InvalidOperationException("Random draw counter is exhausted.");
        }
        var value = _random.Next();
        _rawDrawCount++;
        return value;
    }
}
