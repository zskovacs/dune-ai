using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Rulesets;

public enum ConfigurationSupport { Unrecognized, Recognized, Validated }
public enum GameplaySupport { NotImplemented, Playable }
public sealed record ConfigurationError(string Code, string Path, string Message);

/// <summary>Validated means composition metadata only. This slice supplies no playable Dune game.</summary>
public sealed record ResolutionResult(ConfigurationSupport Support, GameplaySupport Gameplay,
    ImmutableArray<ConfigurationError> Errors, ResolvedGameDefinition? Definition)
{
    public bool IsSuccess => Errors.IsEmpty && Definition is not null;
}

/// <summary>Reusable immutable metadata. Never hand this privileged setup object or seeds to a policy.</summary>
public sealed record ResolvedGameDefinition(
    string Fingerprint,
    ImmutableArray<RuleModule> Modules,
    ImmutableArray<ResolvedRule> Definitions,
    ImmutableArray<ContentDefinition> Content,
    ImmutableArray<ContentId> EligibleLeaders,
    CatalogCompleteness CatalogCompleteness)
{
    public GameplaySupport Gameplay => GameplaySupport.NotImplemented;
    public GameSetupDefinition? Setup { get; internal init; }
    public string ConfigurationIdentity { get; internal init; } = "";
}
