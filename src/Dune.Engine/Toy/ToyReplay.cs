namespace Dune.Engine.Toy;

/// <summary>Privileged replay input: the seed can reconstruct both private markers. Never give this to agents.</summary>
/// <remarks>Reproducibility is scoped to the recorded runtime and engine/rules versions.</remarks>
public sealed class ToyReplay
{
    public ToyReplay(string engineVersion, string rulesVersion, string runtimeVersion,
        int seed, ToyConfig config, IEnumerable<ToyAction> actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeVersion);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(actions);
        EngineVersion = engineVersion;
        RulesVersion = rulesVersion;
        RuntimeVersion = runtimeVersion;
        Seed = seed;
        Config = config;
        Actions = Array.AsReadOnly(actions.ToArray());
    }

    public string EngineVersion { get; }
    public string RulesVersion { get; }
    public string RuntimeVersion { get; }
    public int Seed { get; }
    public ToyConfig Config { get; }
    public IReadOnlyList<ToyAction> Actions { get; }
}
