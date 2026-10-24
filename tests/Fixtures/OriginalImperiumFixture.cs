using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Fixtures;

/// <summary>Synthetic inventory for setup tests/benchmarks only. Never official card metadata.</summary>
internal static class OriginalImperiumFixture
{
    internal static ContentCatalog Catalog { get; } = BuildCatalog();
    internal static RulesetComposer Composer { get; } = new(Catalog);
    internal static GameInitializer Initializer { get; } = new(Composer);

    internal static GameConfiguration Configuration(int players = 4, int seed = 42, int leaderSeed = 17) => new()
    {
        RandomSeed = seed,
        Players = new(Count: players),
        Leaders = new() { Seed = leaderSeed, PlayerOrder = [.. Enumerable.Range(0, players)] },
        Content = new() { Id = "synthetic-fixture" },
    };

    internal static ResolvedGameDefinition Resolve(GameConfiguration configuration) =>
        Composer.Compose(configuration).Definition ??
        throw new InvalidOperationException("Fixture composition failed.");

    internal static GameState Create(int players = 4, int seed = 42, int leaderSeed = 17)
    {
        var c = Configuration(players, seed, leaderSeed);
        return Create(c, Resolve(c));
    }

    internal static GameState Create(GameConfiguration configuration, ResolvedGameDefinition definition) =>
        Initializer.InitializeFixture(configuration, definition).State
        ?? throw new InvalidOperationException("Fixture initialization failed.");

    private static ContentCatalog BuildCatalog()
    {
        var definitions = ContentCatalog.LoadRepresentative().Definitions.Where(x => x.Product == Product.Imperium &&
            (x.Category is ContentCategory.StartingCard or ContentCategory.ReserveCard or ContentCategory.BoardLayout
                or ContentCategory.BoardSpace)).ToList();

        void Add(ContentCategory category, int count, ConflictTier? tier = null)
        {
            for (var i = 0; i < count; i++)
                definitions.Add(new(new($"fixture:{category.ToString().ToLowerInvariant()}:{(int?)tier ?? 0}:{i}:v1"),
                    $"SYNTHETIC FIXTURE {category} {tier} {i}", Product.Imperium, category, 1,
                    new() { BaseGames = [BaseGame.Imperium] },
                    [new("Synthetic fixture, not official content", 1, "Synthetic setup inventory")],
                    ImplementationStatus.MetadataOnly, VerificationStatus.Unverified)
                { ConflictTier = tier });
        }

        Add(ContentCategory.ImperiumCard, 67);
        Add(ContentCategory.IntrigueCard, 40);
        Add(ContentCategory.ConflictCard, 4, ConflictTier.I);
        Add(ContentCategory.ConflictCard, 10, ConflictTier.II);
        Add(ContentCategory.ConflictCard, 4, ConflictTier.III);
        Add(ContentCategory.Leader, 8);
        return new(definitions, CatalogCompleteness.Representative);
    }
}
