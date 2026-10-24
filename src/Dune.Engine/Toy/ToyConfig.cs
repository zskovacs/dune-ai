namespace Dune.Engine.Toy;

/// <summary>Immutable inclusive bounds for the invented toy game's initial pile.</summary>
public sealed record ToyConfig
{
    public ToyConfig(int minInitialTokens = 12, int maxInitialTokens = 24)
    {
        if (minInitialTokens is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(minInitialTokens));
        }

        if (maxInitialTokens < minInitialTokens || maxInitialTokens > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInitialTokens));
        }

        MinInitialTokens = minInitialTokens;
        MaxInitialTokens = maxInitialTokens;
    }

    public int MinInitialTokens { get; }
    public int MaxInitialTokens { get; }
}
