using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;
using Dune.Engine.Setup;

namespace Dune.Engine.Tests;

public sealed class SetupDefinitionTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void OriginalSetupResolvesTypedResourcesAndStableBoard(int players, int victoryPoints)
    {
        var c = Configuration(players);
        var definition = RulesetComposer.CreateDefault().Compose(c).Definition!;
        var setup = Assert.IsType<GameSetupDefinition>(definition.Setup);
        Assert.Equal(victoryPoints, setup.Player.VictoryPoints);
        Assert.Equal(9, setup.Player.TroopsInSupply);
        Assert.Equal(3, setup.Player.TroopsInGarrison);
        Assert.Equal(1, setup.Player.Water);
        Assert.Equal(0, setup.Player.Spice);
        Assert.Equal(0, setup.Player.Solari);
        Assert.Equal(2, setup.Player.AvailableAgents);
        Assert.Equal(1, setup.Player.UnacquiredSwordmasters);
        Assert.Equal(3, setup.Player.ControlMarkers);
        Assert.Equal(22, setup.Board.Spaces.Length);
        Assert.Equal(22, setup.Board.Spaces.Select(x => x.Id).Distinct().Count());
        Assert.Equal(3, setup.Board.Spaces.Count(x => x.AccumulatesSpice));
        Assert.Equal(3, setup.Board.Spaces.Count(x => x.Controllable));
        Assert.Contains(setup.Board.Spaces, x => x.Id == new BoardSpaceId("imperium:space:mentat:v1"));
        Assert.Equal(10, setup.StartingDeck.Sum(x => x.CopiesPerPlayer));
        Assert.Equal(new[] { 8, 10, 6 }, setup.Reserves.Select(x => x.Quantity));
    }

    [Fact]
    public void OfficialStarterAndReserveMetadataIsCompleteButAuthenticSetupStaysBlocked()
    {
        var result = RulesetComposer.CreateDefault().Compose(Configuration(4));
        Assert.True(result.IsSuccess);
        var d = result.Definition!;
        Assert.Equal(CatalogCompleteness.Representative, d.CatalogCompleteness);
        Assert.Equal(40, d.Content.Where(x => x.Category == ContentCategory.StartingCard).Sum(x => x.Quantity));
        Assert.Equal(24, d.Content.Where(x => x.Category == ContentCategory.ReserveCard).Sum(x => x.Quantity));
        var errors = SetupContentValidation.Validate(d);
        Assert.Contains(errors, x => x.Code == "incomplete-catalog");
        Assert.Contains(errors, x => x.Message.Contains("ImperiumCard: expected 67"));
        Assert.Contains(errors, x => x.Message.Contains("IntrigueCard: expected 40"));
        Assert.Contains(errors, x => x.Message.Contains("ConflictCard: expected 18"));
        Assert.Contains(errors, x => x.Message.Contains("Leader: expected 8"));
        Assert.DoesNotContain(errors, x => x.Code == "required-content");
    }

    [Fact]
    public void CompleteLabelCannotMaskMissingInventory()
    {
        var catalog = ContentCatalog.LoadRepresentative();
        var d = new RulesetComposer(new ContentCatalog(catalog.Definitions, CatalogCompleteness.Complete))
            .Compose(Configuration(4)).Definition!;
        var errors = SetupContentValidation.Validate(d);
        Assert.Contains(errors, x => x.Code == "content-quantity");
        Assert.Contains(errors, x => x.Code == "conflict-tier-quantity");
    }

    [Fact]
    public void EqualReserveTotalCannotMaskWrongIdentities()
    {
        var catalog = ContentCatalog.LoadRepresentative();
        var definitions = catalog.Definitions.Select(x => x.Product == Product.Imperium && x.Category == ContentCategory.ReserveCard
            ? x with { Id = new("fixture:reserve:" + x.Quantity + ":v1") } : x);
        var d = new RulesetComposer(new ContentCatalog(definitions)).Compose(Configuration(4)).Definition!;
        Assert.Equal(24, d.Content.Where(x => x.Category == ContentCategory.ReserveCard).Sum(x => x.Quantity));
        Assert.Equal(3, SetupContentValidation.Validate(d).Count(x => x.Code == "required-content"));
    }

    [Fact]
    public void ConfigBindingExcludesSeedsButIncludesLeaderPolicyAndPlayerCount()
    {
        var c = Configuration(4);
        var d = RulesetComposer.CreateDefault().Compose(c).Definition!;
        var reseeded = c with { RandomSeed = 99, Leaders = c.Leaders with { Seed = 72 } };
        Assert.Equal(d.ConfigurationIdentity, ConfigurationIdentity.Compute(reseeded));
        Assert.NotEqual(ConfigurationIdentity.Compute(c, true), ConfigurationIdentity.Compute(reseeded, true));
        Assert.NotEqual(d.ConfigurationIdentity, ConfigurationIdentity.Compute(Configuration(3)));
        Assert.NotEqual(d.ConfigurationIdentity, ConfigurationIdentity.Compute(c with
        {
            Leaders = c.Leaders with { PlayerOrder = [3, 2, 1, 0] },
        }));
    }

    [Fact]
    public void UnsupportedExpansionAndExplicitModuleRecipesCannotExecuteBaseSetup()
    {
        var c = Configuration(4);
        var ordinary = RulesetComposer.CreateDefault().Compose(c).Definition!;
        var custom = new RulesetComposer(ContentCatalog.LoadRepresentative(), ordinary.Modules).Compose(c);
        Assert.True(custom.IsSuccess);
        Assert.Null(custom.Definition!.Setup);
        var expanded = RulesetComposer.CreateDefault().Compose(c with { Expansions = [Expansion.RiseOfIx] });
        Assert.True(expanded.IsSuccess);
        Assert.Null(expanded.Definition!.Setup);
        var draft = RulesetComposer.CreateDefault().Compose(c with
        {
            Leaders = c.Leaders with { Strategy = LeaderSelectionStrategy.SnakeDraft },
        });
        Assert.True(draft.IsSuccess);
        Assert.Null(draft.Definition!.Setup);
    }

    [Theory]
    [InlineData("bogus.pdf", 3)]
    [InlineData("DUNE_IMPERIUM_UPRISING_Main_Rulebook_23-10-12.pdf", 3)]
    [InlineData("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 20)]
    public void CompleteLabelAndVerifiedFlagCannotHideInapplicableSetupSources(string document, int page)
    {
        var catalog = ContentCatalog.LoadRepresentative();
        var sourceChanged = catalog.Definitions.Select(x => x.Category == ContentCategory.StartingCard
            ? x with { Sources = [new(document, page, "Mislabeled setup inventory")] } : x);
        var d = new RulesetComposer(new ContentCatalog(sourceChanged, CatalogCompleteness.Complete))
            .Compose(Configuration(4)).Definition!;
        Assert.Contains(SetupContentValidation.Validate(d), x => x.Code == "unsupported-content-source");
    }

    [Fact]
    public void ConflictTierCannotBeAttachedToAnotherCategoryOrUnknownTier()
    {
        var card = ContentCatalog.LoadRepresentative().Definitions.First();
        Assert.Throws<ArgumentException>(() => new ContentCatalog([card with { ConflictTier = ConflictTier.I }]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([card with
        {
            Category = ContentCategory.ConflictCard, ConflictTier = (ConflictTier)44,
        }]));
    }

    private static GameConfiguration Configuration(int players) => new()
    {
        Players = new(Count: players),
        Leaders = new() { PlayerOrder = [.. Enumerable.Range(0, players)] },
    };
}
