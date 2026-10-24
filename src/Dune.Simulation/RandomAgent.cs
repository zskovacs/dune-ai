namespace Dune.Simulation;

/// <summary>A seeded uniform random policy. Own one instance per player per episode.</summary>
/// <remarks>System.Random reproducibility is scoped to the pinned runtime.</remarks>
public sealed class RandomAgent<TObservation, TAction>(int seed) : IAgent<TObservation, TAction>
{
    private readonly Random _random = new(seed);

    public TAction SelectAction(TObservation observation, IReadOnlyList<TAction> legalActions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(legalActions);
        if (legalActions.Count == 0)
        {
            throw new ArgumentException("At least one legal action is required.", nameof(legalActions));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return legalActions[_random.Next(legalActions.Count)];
    }
}
