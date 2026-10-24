namespace Dune.Simulation;

/// <summary>A policy receives only a player's observation and permitted legal actions.</summary>
public interface IAgent<in TObservation, TAction>
{
    TAction SelectAction(TObservation observation, IReadOnlyList<TAction> legalActions,
        CancellationToken cancellationToken = default);
}
