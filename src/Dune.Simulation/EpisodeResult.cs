namespace Dune.Simulation;

/// <summary>A completed episode's detached trace and terminal result.</summary>
/// <remarks>Actions and result values must themselves be immutable or detached engine snapshots.</remarks>
public sealed class EpisodeResult<TAction, TResult>
{
    internal EpisodeResult(int seed, int agentSeed, IEnumerable<TAction> actions, TResult result)
    {
        Seed = seed;
        AgentSeed = agentSeed;
        Actions = Array.AsReadOnly(actions.ToArray());
        Result = result;
    }

    /// <summary>Private environment seed; this result envelope is unsuitable for agent input.</summary>
    public int Seed { get; }
    /// <summary>Actual per-episode policy seed. Supply it to Run to reproduce this episode's policy streams.</summary>
    public int AgentSeed { get; }
    public IReadOnlyList<TAction> Actions { get; }
    public TResult Result { get; }
}
