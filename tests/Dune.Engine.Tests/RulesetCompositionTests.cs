using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;

namespace Dune.Engine.Tests;

public sealed class RulesetCompositionTests
{
    private static GameConfiguration Standard => ConfigurationPresets.All["imperium-standard"];
    private static ContentCatalog Catalog => ContentCatalog.LoadRepresentative();
    private static RulesetComposer Composer => RulesetComposer.CreateDefault();
    private static RuleModule Module(string id, string[]? dependencies = null, RuleContribution[]? rules = null, string[]? incompatible = null) =>
        new(new(id), 1, (dependencies ?? []).Select(x => new ModuleId(x)).ToImmutableArray(),
            (incompatible ?? []).Select(x => new ModuleId(x)).ToImmutableArray(), (rules ?? []).ToImmutableArray());
    private static RuleContribution Rule(string id, string value = "test-metadata", string? replaces = null, string slot = "main") =>
        new(new(new(id), new(DefinitionKind.Setup, slot), value, [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 4, "Graph test metadata")]),
            replaces is null ? null : new DefinitionId(replaces));
    private static ImmutableArray<RuleModule> BaseModules => Composer.Compose(Standard).Definition!.Modules;
    private static ResolutionResult Resolve(params RuleModule[] modules) => new RulesetComposer(Catalog, BaseModules.AddRange(modules)).Compose(Standard);
    private static string Value(ResolutionResult result, DefinitionKind kind, string slot) =>
        result.Definition!.Definitions.Single(x => x.Definition.Key == new DefinitionKey(kind, slot)).Definition.Value;
    private static void Error(ResolutionResult result, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, x => x.Code == code && !string.IsNullOrWhiteSpace(x.Message));
        Assert.Equal(GameplaySupport.NotImplemented, result.Gameplay);
    }

    [Fact]
    public void EveryPresetResolvesMetadataAndExplicitlyRemainsUnplayable()
    {
        Assert.True(ConfigurationPresets.All.Count >= 7);
        foreach (var pair in ConfigurationPresets.All)
        {
            var result = Composer.Compose(pair.Value);
            Assert.True(result.IsSuccess, pair.Key + ": " + string.Join(";", result.Errors));
            Assert.Equal(ConfigurationSupport.Validated, result.Support);
            Assert.Equal(GameplaySupport.NotImplemented, result.Gameplay);
            Assert.Equal(GameplaySupport.NotImplemented, result.Definition!.Gameplay);
            Assert.Equal(CatalogCompleteness.Representative, result.Definition.CatalogCompleteness);
            Assert.All(result.Definition.Content, x => Assert.Equal(ImplementationStatus.MetadataOnly, x.Implementation));
        }
    }

    [Fact]
    public void BaseRulesRecordActualPlayerCountDependentSetup()
    {
        var four = Composer.Compose(Standard);
        var three = Composer.Compose(Standard with { Players = new(PlayerFormat.Competitive, 3), Leaders = new() { PlayerOrder = [0, 1, 2] } });
        Assert.Contains("vp:1", Value(four, DefinitionKind.Resources, "initial"));
        Assert.Contains("vp:0", Value(three, DefinitionKind.Resources, "initial"));
        Assert.Equal("I:1;II:5;III:4", Value(four, DefinitionKind.ConflictDeck, "recipe"));
    }

    [Fact]
    public void IxUsesDifferentOverlayOnEachBaseAndUprisingVirtualPosts()
    {
        var original = Composer.Compose(ConfigurationPresets.All["imperium-ix"]);
        var uprising = Composer.Compose(ConfigurationPresets.All["uprising-ix"]);
        Assert.Contains("upper-right", Value(original, DefinitionKind.BoardLayout, "main"));
        Assert.Contains("folded", Value(uprising, DefinitionKind.BoardLayout, "main"));
        Assert.Contains("Gather Support", Value(uprising, DefinitionKind.BoardLayout, "main"));
        Assert.Equal("Interstellar Shipping/Smuggling; Tech Negotiation/Dreadnought", Value(uprising, DefinitionKind.BoardSpace, "virtual-observation-posts"));
        Assert.Contains("omit all Ix conflicts", Value(uprising, DefinitionKind.CardPool, "ix"));
    }

    [Fact]
    public void ImmortalityReplacesBothStartingCardsAndEpicHasExplicitOverrideHistory()
    {
        var standard = Composer.Compose(ConfigurationPresets.All["imperium-ix-immortality"]);
        Assert.Contains("BOTH", Value(standard, DefinitionKind.StartingDeck, "player"));
        var epic = Composer.Compose(ConfigurationPresets.All["imperium-ix-immortality-epic"]);
        Assert.Contains("11 total", Value(epic, DefinitionKind.StartingDeck, "player"));
        Assert.Contains("starts in discard", Value(epic, DefinitionKind.StartingDeck, "player"));
        Assert.Equal("12", Value(epic, DefinitionKind.VictoryCondition, "threshold"));
        Assert.Equal("I:0;II:5;III:5", Value(epic, DefinitionKind.ConflictDeck, "recipe"));
        Assert.Contains("garrison:5;intrigue:1", Value(epic, DefinitionKind.Resources, "initial"));
        var history = epic.Definition!.Definitions.Single(x => x.Definition.Key.Kind == DefinitionKind.StartingDeck).History;
        Assert.Equal(new[] { "base.imperium", "expansion.immortality", "interaction.immortality-epic" }, history.Select(x => x.Module.Value));
        Assert.Contains(history[^1].Definition.Sources, x => x.Document == "DUNE_IMPERIUM_IMMORTALITY_Rulebook.pdf" && x.Page == 12);
    }

    [Fact]
    public void EpicWithoutImmortalityReplacesOneCard()
    {
        var result = Composer.Compose(ConfigurationPresets.All["imperium-ix-epic"]);
        Assert.Contains("replace one Dune", Value(result, DefinitionKind.StartingDeck, "player"));
    }

    [Theory]
    [InlineData(3, "11")]
    [InlineData(4, "10")]
    public void GoTo11AccountsForFourPlayerException(int count, string target)
    {
        var c = ConfigurationPresets.All["imperium-ix-immortality-go-to-11"] with
        {
            Players = new(PlayerFormat.Competitive, count),
            Leaders = new() { PlayerOrder = Enumerable.Range(0, count).ToImmutableArray() },
        };
        var result = Composer.Compose(c);
        Assert.Equal(target, Value(result, DefinitionKind.VictoryCondition, "threshold"));
        Assert.Contains("vp:0", Value(result, DefinitionKind.Resources, "initial"));
    }

    [Fact]
    public void BloodlinesTechIsOptInAndExcludesChoamTransportsWithoutChoam()
    {
        var blood = Composer.Compose(ConfigurationPresets.All["uprising-bloodlines"]);
        var tech = Composer.Compose(ConfigurationPresets.All["uprising-bloodlines-tech"]);
        Assert.DoesNotContain(blood.Definition!.Modules, x => x.Id.Value == "module.bloodlines-tech");
        Assert.DoesNotContain(blood.Definition.Content, x => x.Category == ContentCategory.TechTile);
        Assert.Contains(tech.Definition!.Content, x => x.Id.Value == "bloodlines:tech:ornithopter-fleet:v1");
        Assert.DoesNotContain(tech.Definition.Content, x => x.Id.Value == "bloodlines:tech:choam-transports:v1");
        Assert.Contains("Ixian Embassy", Value(tech, DefinitionKind.BoardLayout, "tech"));
    }

    [Fact]
    public void ChoamMakesShaddamEligible()
    {
        var standard = Composer.Compose(ConfigurationPresets.All["uprising-standard"]);
        var choam = Composer.Compose(ConfigurationPresets.All["uprising-choam"]);
        var id = new ContentId("uprising:leader:shaddam-corrino-iv:v1");
        Assert.DoesNotContain(id, standard.Definition!.EligibleLeaders);
        Assert.Contains(id, choam.Definition!.EligibleLeaders);
    }

    [Fact]
    public void ConfigurationSerializationRoundTripsAllPolicyParametersAndSeed()
    {
        var c = ConfigurationPresets.All["imperium-ix-immortality"] with
        {
            RandomSeed = -812,
            Leaders = new() { Strategy = LeaderSelectionStrategy.SnakeDraft, Seed = 95, PlayerOrder = [2, 0, 3, 1] },
            Content = new() { Id = "sample-profile", Revision = 3 },
        };
        var json = ConfigurationJson.Serialize(c);
        var copy = ConfigurationJson.Deserialize(json);
        Assert.Equal(-812, copy.RandomSeed);
        Assert.Equal(95, copy.Leaders.Seed);
        Assert.Equal(LeaderSelectionStrategy.SnakeDraft, copy.Leaders.Strategy);
        Assert.Equal(c.Leaders.PlayerOrder, copy.Leaders.PlayerOrder);
        Assert.Equal(c.Expansions, copy.Expansions);
        Assert.Equal("sample-profile", copy.Content.Id);
        Assert.Equal(Composer.Compose(c).Definition!.Fingerprint, Composer.Compose(copy).Definition!.Fingerprint);
    }

    [Theory]
    [InlineData("{\"baseGame\":\"Unknown\"}")]
    [InlineData("{\"baseGame\":99}")]
    [InlineData("{\"typo\":true}")]
    [InlineData("null")]
    public void JsonRejectsUnknownEnumNumbersAndMisspelledFields(string json) => Assert.Throws<JsonException>(() => ConfigurationJson.Deserialize(json));

    [Fact]
    public void InvalidTypedValuesAndMissingRecordsReturnDiagnosticsWithoutDefinition()
    {
        var unknown = Composer.Compose(Standard with { BaseGame = (BaseGame)99 });
        Assert.Equal(ConfigurationSupport.Unrecognized, unknown.Support);
        Error(unknown, "unknown-value");
        Error(Composer.Compose(Standard with { Players = null! }), "required");
        Error(Composer.Compose(Standard with { Leaders = null! }), "required");
        Error(Composer.Compose(Standard with { Content = null! }), "required");
        Error(Composer.Compose(null), "required");
        Error(Composer.Compose(Standard with { Expansions = default }), "invalid-collection");
        Error(Composer.Compose(Standard with { Expansions = [Expansion.RiseOfIx, Expansion.RiseOfIx] }), "duplicate-value");
        Error(Composer.Compose(Standard with { Leaders = new() { FixedAssignments = default } }), "invalid-collection");
        Error(Composer.Compose(Standard with { Content = new() { Included = default } }), "invalid-collection");
    }

    [Theory]
    [InlineData(PlayerFormat.SoloWithRivals, 1)]
    [InlineData(PlayerFormat.TwoPlayerWithRivals, 2)]
    [InlineData(PlayerFormat.SixPlayerTeams, 6)]
    public void KnownUnimplementedFormatsRemainRecognizedAndExplicitlyReject(PlayerFormat format, int count)
    {
        var result = Composer.Compose(Standard with { Players = new(format, count), Leaders = new() { PlayerOrder = Enumerable.Range(0, count).ToImmutableArray() } });
        Assert.Equal(ConfigurationSupport.Recognized, result.Support);
        Error(result, "unsupported-format");
    }

    [Fact]
    public void InvalidFormatCountsAndUnsupportedCombinationsReject()
    {
        Error(Composer.Compose(Standard with { Players = new(PlayerFormat.Competitive, 2) }), "player-format-count");
        Error(Composer.Compose(Standard with { Players = new(PlayerFormat.Competitive, 7) }), "player-count");
        Error(Composer.Compose(Standard with { Modules = [OptionalModule.BloodlinesTech] }), "module-dependency");
        Error(Composer.Compose(Standard with { Modules = [OptionalModule.UprisingChoam] }), "module-incompatible");
        Error(Composer.Compose(Standard with { Mode = GameMode.Epic }), "mode-dependency");
        Error(Composer.Compose(ConfigurationPresets.All["imperium-ix-immortality-epic"] with { Variants = [RuleVariant.GoTo11] }), "unresolved-interaction");
        Error(Composer.Compose(ConfigurationPresets.All["uprising-ix"] with { Expansions = [Expansion.RiseOfIx, Expansion.Bloodlines], Modules = [OptionalModule.BloodlinesTech] }), "unsupported-combination");
    }

    [Fact]
    public void LeaderPolicyRejectsDuplicatesUnknownLeadersBadOrderAndFutureRestrictions()
    {
        Error(Composer.Compose(Standard with { Leaders = new() { PlayerOrder = [0, 0, 2, 3] } }), "leader-order");
        Error(Composer.Compose(Standard with { Leaders = new() { CandidateLeaders = [new("unknown:leader:v1")] } }), "ineligible-leader");
        Error(Composer.Compose(Standard with { Leaders = new() { CandidateLeaders = [new("bad id")] } }), "leader-candidates");
        Error(Composer.Compose(Standard with { Leaders = new() { CandidateLeaders = [new("imperium:leader:paul-atreides:v1"), new("imperium:leader:paul-atreides:v1")] } }), "leader-candidates");
        Error(Composer.Compose(Standard with { Leaders = new() { RestrictionIds = ["future-ban-policy"] } }), "unsupported-restriction");
        Error(Composer.Compose(Standard with { Leaders = new() { Strategy = LeaderSelectionStrategy.Fixed } }), "leader-assignment");
        Error(Composer.Compose(Standard with { Leaders = new() { FixedAssignments = [new(0, new("imperium:leader:paul-atreides:v1"))] } }), "leader-assignment");
    }

    [Fact]
    public void FixedLeadersMustBeUniqueAndEligibleAfterProfileFiltering()
    {
        var pool = Composer.Compose(Standard).Definition!.EligibleLeaders;
        var assignments = pool.Select((id, seat) => new LeaderAssignment(seat, id)).ToImmutableArray();
        var valid = Standard with { Leaders = new() { Strategy = LeaderSelectionStrategy.Fixed, FixedAssignments = assignments } };
        Assert.True(Composer.Compose(valid).IsSuccess);
        Error(Composer.Compose(valid with { Leaders = valid.Leaders with { FixedAssignments = assignments.SetItem(1, assignments[1] with { Leader = assignments[0].Leader }) } }), "leader-assignment");
        Error(Composer.Compose(valid with { Content = new() { Excluded = [pool[0]] } }), "ineligible-leader");
        Error(Composer.Compose(valid with { Leaders = valid.Leaders with { FixedAssignments = assignments.SetItem(0, new(0, new("uprising:leader:lady-jessica:v1"))) } }), "ineligible-leader");
    }

    [Theory]
    [InlineData(LeaderSelectionStrategy.Random)]
    [InlineData(LeaderSelectionStrategy.SequentialDraft)]
    [InlineData(LeaderSelectionStrategy.SnakeDraft)]
    public void FutureLeaderStrategiesValidateWithoutExecutingSelection(LeaderSelectionStrategy strategy)
    {
        var result = Composer.Compose(Standard with { Leaders = new() { Strategy = strategy, Seed = 90, PlayerOrder = [3, 2, 1, 0] } });
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Definition!.EligibleLeaders.Length);
        Assert.DoesNotContain(result.Definition.Definitions, x => x.Definition.Key.Slot == "leader-assignments");
    }

    [Fact]
    public void CatalogRejectsDuplicateAndMalformedIdsAndInvalidOwnership()
    {
        var d = Catalog.Definitions[0];
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d, d]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d with { Id = default }]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d with { Availability = new() }]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d with { Quantity = 0 }]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d with { Sources = [] }]));
        Assert.Throws<ArgumentException>(() => new ContentCatalog([d with { Availability = d.Availability with { BaseGames = default } }]));
    }

    [Fact]
    public void ContentProfilesRejectUnknownIncompatibleAndContradictoryIds()
    {
        var uprisingId = new ContentId("uprising:leader:lady-jessica:v1");
        Error(Composer.Compose(Standard with { Content = new() { Included = [uprisingId] } }), "unavailable-content");
        Error(Composer.Compose(Standard with { Content = new() { Excluded = [new("unknown:card:v1")] } }), "unknown-content");
        Error(Composer.Compose(Standard with { Content = new() { Included = [uprisingId], Excluded = [uprisingId] } }), "content-profile");
        var leader = new ContentId("imperium:leader:paul-atreides:v1");
        Error(Composer.Compose(Standard with { Content = new() { Excluded = [leader] } }), "insufficient-leaders");
    }

    [Fact]
    public void DefinitionsWithOverlappingNamesHaveDistinctProductIdentity()
    {
        var cards = Catalog.Definitions.Where(x => x.Name == "Dune, the Desert Planet").ToArray();
        Assert.Equal(2, cards.Length);
        Assert.NotEqual(cards[0].Id, cards[1].Id);
        Assert.NotEqual(cards[0].Product, cards[1].Product);
        var instance = new CardInstance(new(20), cards[0].Id);
        Assert.Equal(cards[0].Id, instance.Definition);
    }

    [Fact]
    public void ExplicitDependencyAndExpectedPriorDefinitionProduceAHistory()
    {
        var first = Module("base", rules: [Rule("first")]);
        var second = Module("second", ["base"], [Rule("second-def", "replacement", "first")]);
        var result = Resolve(second, first);
        Assert.True(result.IsSuccess);
        Assert.Equal("replacement", Value(result, DefinitionKind.Setup, "main"));
        Assert.Equal(new[] { "base", "second" }, result.Definition!.Definitions.Single(x => x.Definition.Key == new DefinitionKey(DefinitionKind.Setup, "main")).History.Select(x => x.Module.Value));
    }

    [Fact]
    public void ModuleGraphRejectsMissingDependencyIncompatibilityCycleAndDuplicateIds()
    {
        Error(Resolve(Module("a", ["missing"])), "missing-dependency");
        Error(Resolve(Module("a", incompatible: ["b"]), Module("b")), "incompatible-modules");
        Error(Resolve(Module("a", ["b"]), Module("b", ["a"])), "module-cycle");
        Error(Resolve(Module("a"), Module("a")), "duplicate-or-invalid-module");
        Error(Resolve(Module("a") with { Dependencies = default }), "invalid-module");
    }

    [Fact]
    public void WritesRequireVacantKeyOrExactTargetAndDependency()
    {
        var root = Module("root", rules: [Rule("root-def")]);
        Error(Resolve(root, Module("add", ["root"], [Rule("other")])), "conflicting-definition");
        Error(Resolve(root, Module("replace", ["root"], [Rule("other", replaces: "wrong")])), "conflicting-definition");
        Error(Resolve(root, Module("z-replace", rules: [Rule("other", replaces: "root-def")])), "conflicting-definition");
        Error(Resolve(Module("replace", rules: [Rule("other", replaces: "missing")])), "replacement-target");
        Error(Resolve(root, Module("other", ["root"], [Rule("root-def", replaces: "root-def")])), "invalid-definition");
    }

    [Fact]
    public void SiblingReplacementsNeverGainImplicitPrecedenceFromRegistrationOrder()
    {
        var root = Module("root", rules: [Rule("root-def")]);
        var a = Module("a", ["root"], [Rule("a-def", replaces: "root-def")]);
        // Even naming a's target does not establish a dependency on a.
        var b = Module("b", ["root"], [Rule("b-def", replaces: "a-def")]);
        Error(Resolve(root, a, b), "conflicting-definition");
        Error(Resolve(b, a, root), "conflicting-definition");
        b = b with { Dependencies = [new("a")] };
        Assert.True(Resolve(b, root, a).IsSuccess);
    }

    [Fact]
    public void FingerprintIgnoresBothSeedsAndSetOrderButIncludesPolicyOrder()
    {
        var c = ConfigurationPresets.All["imperium-ix-immortality"];
        var first = Composer.Compose(c).Definition!.Fingerprint;
        var second = Composer.Compose(c with { Expansions = [Expansion.Immortality, Expansion.RiseOfIx], RandomSeed = 99, Leaders = c.Leaders with { Seed = -15 } }).Definition!.Fingerprint;
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, Composer.Compose(c with { Leaders = c.Leaders with { PlayerOrder = [3, 2, 1, 0] } }).Definition!.Fingerprint);
        Assert.NotEqual(first, Composer.Compose(c with { Content = c.Content with { Revision = 2 } }).Definition!.Fingerprint);
    }

    [Fact]
    public void FingerprintIncludesContentMetadataAndModuleHistoryAndIsRegistrationIndependent()
    {
        var a = Module("a", rules: [Rule("original")]);
        var b = Module("b", ["a"], [Rule("replacement", "final", "original")]);
        var digest = Resolve(a, b).Definition!.Fingerprint;
        Assert.Equal(digest, Resolve(b, a).Definition!.Fingerprint);
        Assert.NotEqual(digest, Resolve(a with { Revision = 2 }, b).Definition!.Fingerprint);
        Assert.NotEqual(digest, Resolve(a with { Contributions = [Rule("original", "different historical value")] }, b).Definition!.Fingerprint);
        var changed = new ContentCatalog(Catalog.Definitions.Select(x => x.Id.Value == "imperium:leader:paul-atreides:v1" ? x with { Revision = 2 } : x));
        Assert.NotEqual(digest, new RulesetComposer(changed, BaseModules.Add(a).Add(b)).Compose(Standard).Definition!.Fingerprint);
    }

    [Fact]
    public void EmptyOrSparseExplicitModulesCannotValidateAnEmptyRuleset()
    {
        Error(new RulesetComposer(Catalog, []).Compose(Standard), "incomplete-definition");
        Error(new RulesetComposer(Catalog, [Module("sparse", rules: [Rule("single")])]).Compose(Standard), "incomplete-definition");
    }

    [Fact]
    public void FingerprintGoldenDetectsAccidentalCanonicalFormatChanges()
    {
        Assert.Equal("498733ed1bdf6201f841396a8851e7f054c2954ccd59fe9ef2170a41474e82e4", Composer.Compose(Standard).Definition!.Fingerprint);
    }

    [Fact]
    public void IdentifiersRejectTrailingNewlinesAcrossCatalogProfilesAndModules()
    {
        var definition = Catalog.Definitions[0];
        Assert.Throws<ArgumentException>(() => new ContentCatalog([definition with { Id = new("invalid\n") }]));
        Error(Composer.Compose(Standard with { Content = new() { Id = "invalid\n" } }), "content-profile");
        Error(Resolve(Module("invalid\n")), "duplicate-or-invalid-module");
    }

}
