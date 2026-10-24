using System.Collections.Immutable;
using Dune.Engine.Configuration;

namespace Dune.Engine.Content;

public readonly record struct ContentId(string Value);
public readonly record struct CardInstanceId(long Value);
public enum Product { Imperium, Uprising, RiseOfIx, Immortality, Bloodlines }
public enum ContentCategory
{
    ImperiumCard, StartingCard, ReserveCard, IntrigueCard, ConflictCard, Leader, TechTile,
    TleilaxuCard, Objective, Contract, SardaukarCommanderSkill, HouseHagalCard, Rival,
    BoardSpace, BoardLayout,
}
public enum ImplementationStatus { MetadataOnly, Implemented }
public enum VerificationStatus { Unverified, RulebookVerified }
public enum CatalogCompleteness { Representative, Complete }
public enum ConflictTier { I = 1, II = 2, III = 3 }
public sealed record SourceReference(string Document, int Page, string Section);

public sealed record AvailabilityRequirements
{
    public ImmutableArray<BaseGame> BaseGames { get; init; } = [];
    public ImmutableArray<Expansion> RequiredExpansions { get; init; } = [];
    public ImmutableArray<Expansion> ExcludedExpansions { get; init; } = [];
    public ImmutableArray<OptionalModule> RequiredModules { get; init; } = [];
    public ImmutableArray<OptionalModule> ExcludedModules { get; init; } = [];
    public ImmutableArray<GameMode> Modes { get; init; } = [];
    public ImmutableArray<PlayerFormat> Formats { get; init; } = [];
    public int MinimumPlayers { get; init; } = 1;
    public int MaximumPlayers { get; init; } = 6;

    public bool Allows(GameConfiguration configuration) =>
        (BaseGames.IsEmpty || BaseGames.Contains(configuration.BaseGame)) &&
        RequiredExpansions.All(configuration.Expansions.Contains) &&
        !ExcludedExpansions.Any(configuration.Expansions.Contains) &&
        RequiredModules.All(configuration.Modules.Contains) &&
        !ExcludedModules.Any(configuration.Modules.Contains) &&
        (Modes.IsEmpty || Modes.Contains(configuration.Mode)) &&
        (Formats.IsEmpty || Formats.Contains(configuration.Players.Format)) &&
        configuration.Players.Count >= MinimumPlayers && configuration.Players.Count <= MaximumPlayers;
}

/// <summary>Static metadata. Quantity is boxed-product multiplicity, not runtime card identity.</summary>
public sealed record ContentDefinition(
    ContentId Id, string Name, Product Product, ContentCategory Category, int Quantity,
    AvailabilityRequirements Availability, ImmutableArray<SourceReference> Sources,
    ImplementationStatus Implementation, VerificationStatus Verification, int Revision = 1)
{
    public ConflictTier? ConflictTier { get; init; }
}

/// <summary>An episode-local physical copy refers to its definition; ownership/visibility belong to its current zone.</summary>
public sealed record CardInstance(CardInstanceId Id, ContentId Definition);
