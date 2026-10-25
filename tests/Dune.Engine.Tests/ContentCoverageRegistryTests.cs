using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Tests;

public class ContentCoverageRegistryTests
{
    private static JsonDocument LoadInventory() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content-coverage", "inventory.json")));

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;

    private static bool HasAuditedAbility(JsonElement item, string slot, string? key = null) =>
        item.GetProperty("abilities").EnumerateArray().Any(x =>
            x.GetProperty("slot").GetString() == slot &&
            (key is null || Id(x).EndsWith($"#slot:{key}", StringComparison.Ordinal)));

    private static bool BoardEffectsMatchAudit(JsonElement item, BoardRule rule) =>
        HasAuditedAbility(item, "Reveal") == !rule.RevealEffects.IsDefaultOrEmpty &&
        HasAuditedAbility(item, "Triggered", "controller-payout") == (rule.ControlReward is not null);

    [Fact]
    public void AuditedConfigurationGatesAgreeWithActualPresetsAndInitializer()
    {
        using var document = LoadInventory();
        var configurations = document.RootElement.GetProperty("configurations").EnumerateArray().ToArray();
        Assert.Equal(6, configurations.Length);
        foreach (var item in configurations)
        {
            var preset = ConfigurationPresets.All[item.GetProperty("preset").GetString()!];
            var seats = item.GetProperty("seats").GetInt32();
            var configuration = preset with
            {
                Players = preset.Players with { Count = seats },
                Leaders = preset.Leaders with { PlayerOrder = [.. Enumerable.Range(0, seats)] },
            };
            Assert.Equal(configuration.BaseGame.ToString(), item.GetProperty("base").GetString());
            Assert.Equal(configuration.Mode.ToString(), item.GetProperty("mode").GetString());
            Assert.Equal(configuration.Expansions.Select(x => x.ToString()).Order(),
                item.GetProperty("expansions").EnumerateArray().Select(x => x.GetString()).Order());
            Assert.Empty(configuration.Modules);
            Assert.Empty(item.GetProperty("modules").EnumerateArray());
            Assert.Equal(LeaderSelectionStrategy.Random, configuration.Leaders.Strategy);
            Assert.Equal("all-official", item.GetProperty("leaderPolicy").GetString());
            Assert.Empty(item.GetProperty("leaderContentIds").EnumerateArray());

            var composed = RulesetComposer.CreateDefault().Compose(configuration);
            Assert.True(composed.IsSuccess);
            Assert.Equal(ConfigurationSupport.Validated, composed.Support);
            Assert.Equal(GameplaySupport.NotImplemented, composed.Gameplay);
            var definition = composed.Definition!;
            Assert.Equal(CatalogCompleteness.Representative, definition.CatalogCompleteness);
            Assert.Equal(configuration.Expansions.IsEmpty, definition.Setup is not null);
            var initialized = new GameInitializer().Create(configuration);
            Assert.False(initialized.IsSuccess);
            Assert.Null(initialized.State);
            Assert.Contains(initialized.Errors, x => x.Code ==
                (configuration.Expansions.IsEmpty ? "incomplete-catalog" : "unsupported-setup"));
            Assert.Empty(item.GetProperty("initializationTests").EnumerateArray());
            Assert.Empty(item.GetProperty("fullGameTests").EnumerateArray());
            Assert.Empty(item.GetProperty("rlTests").EnumerateArray());
        }
    }

    [Fact]
    public void OriginalCatalogDefinitionsHaveInventoryIdentityAndQuantity()
    {
        using var document = LoadInventory();
        var inventory = document.RootElement.GetProperty("content").EnumerateArray()
            .Where(x => x.GetProperty("product").GetString() == "Imperium")
            .ToDictionary(Id);
        var catalog = ContentCatalog.LoadRepresentative().Definitions
            .Where(x => x.Product == Product.Imperium).ToArray();

        foreach (var definition in catalog)
        {
            Assert.True(inventory.TryGetValue(definition.Id.Value, out var item),
                $"Catalog identity absent from audit: {definition.Id.Value}");
            Assert.Equal(definition.Category.ToString(), item.GetProperty("category").GetString());
            if (item.GetProperty("boxedQuantity").ValueKind == JsonValueKind.Number)
                Assert.Equal(definition.Quantity, item.GetProperty("boxedQuantity").GetInt32());
            Assert.Contains(item.GetProperty("implementationReferences").EnumerateArray(),
                x => x.GetProperty("registryKind").GetString() == "Catalog" &&
                     x.GetProperty("key").GetString() == definition.Id.Value);
        }

        var starters = inventory.Values.Where(x => x.GetProperty("category").GetString() == "StartingCard").ToArray();
        Assert.Equal(7, starters.Length);
        Assert.Equal(40, starters.Sum(x => x.GetProperty("boxedQuantity").GetInt32()));
        Assert.Equal(10, starters.Sum(x => x.GetProperty("perPlayerQuantity").GetInt32()));
        var reserves = inventory.Values.Where(x => x.GetProperty("category").GetString() == "ReserveCard").ToArray();
        Assert.Equal(3, reserves.Length);
        Assert.Equal(24, reserves.Sum(x => x.GetProperty("boxedQuantity").GetInt32()));
        Assert.Equal(4, catalog.Count(x => x.Category == ContentCategory.Leader));
        Assert.Equal(8, inventory.Values.Count(x => x.GetProperty("category").GetString() == "Leader"));
    }

    [Fact]
    public void OriginalAuthoredRuleIdsHaveInventoryReferences()
    {
        using var document = LoadInventory();
        var items = document.RootElement.GetProperty("content").EnumerateArray()
            .Where(x => x.GetProperty("product").GetString() == "Imperium")
            .ToDictionary(Id);
        var turns = OriginalImperiumTurnRules.Create();
        var lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules();
        var rules = turns.Cards.Select(x => (x.Id.Value, Kind: "CardRule"))
            .Concat(turns.Spaces.Select(x => (x.Id.Value, Kind: "BoardRule")))
            .Concat(lifecycle.Intrigues.Select(x => (x.Id.Value, Kind: "IntrigueRule")))
            .ToArray();

        foreach (var (id, kind) in rules)
        {
            Assert.True(items.TryGetValue(id, out var item), $"Rule-only identity absent from audit: {id}");
            Assert.Contains(item.GetProperty("implementationReferences").EnumerateArray(),
                x => x.GetProperty("registryKind").GetString() == kind &&
                     x.GetProperty("key").GetString() == id);
        }

        Assert.Equal(19, rules.Count(x => x.Kind == "BoardRule"));
        Assert.Equal(22, items.Values.Count(x => x.GetProperty("category").GetString() == "BoardSpace"));
        foreach (var item in items.Values)
        {
            if (!item.TryGetProperty("implementationReferences", out var references)) continue;
            foreach (var reference in references.EnumerateArray())
            {
                var kind = reference.GetProperty("registryKind").GetString();
                if (kind is "CardRule" or "BoardRule" or "IntrigueRule")
                    Assert.Contains(rules, rule => rule.Value == Id(item) && rule.Kind == kind);
            }
        }
        Assert.DoesNotContain(items.Values, x => x.GetProperty("category").GetString() == "GameplayComponent" &&
            x.GetProperty("runtimeDefinition").GetBoolean());
    }

    [Fact]
    public void AuthoredBoxesAgreeWithAuditedSlotsAndExplicitUnsupportedEffects()
    {
        using var document = LoadInventory();
        var items = document.RootElement.GetProperty("content").EnumerateArray()
            .Where(x => x.GetProperty("product").GetString() == "Imperium")
            .ToDictionary(Id);
        var turns = OriginalImperiumTurnRules.Create();
        var lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules();

        static JsonElement Slot(JsonElement item, string name) => item.GetProperty("slots")
            .EnumerateArray().Single(x => x.GetProperty("slot").GetString() == name);
        static void AssertBox(JsonElement item, string name, bool hasEffect, bool unsupported)
        {
            var slot = Slot(item, name);
            var discovery = slot.GetProperty("discovery").GetString();
            if (unsupported)
            {
                Assert.True(discovery is "discovered" or "unreadable");
                if (discovery == "unreadable")
                    Assert.False(string.IsNullOrWhiteSpace(slot.GetProperty("reason").GetString()));
                Assert.NotEmpty(slot.GetProperty("abilityIds").EnumerateArray());
                foreach (var abilityId in slot.GetProperty("abilityIds").EnumerateArray())
                    Assert.Contains(item.GetProperty("abilities").EnumerateArray(), ability =>
                        Id(ability) == abilityId.GetString() &&
                        ability.GetProperty("implementation").GetString() == "unsupported");
            }
            else if (hasEffect)
            {
                Assert.Equal("discovered", discovery);
                Assert.NotEmpty(slot.GetProperty("abilityIds").EnumerateArray());
            }
            else if (item.GetProperty("faceComplete").GetBoolean())
                Assert.Equal("verified-absent", discovery);
        }

        foreach (var card in turns.Cards)
        {
            var item = items[card.Id.Value];
            AssertBox(item, "Agent", card.AgentEffects.Length > 0,
                card.AgentEffects.Any(x => x is UnsupportedEffect));
            AssertBox(item, "Reveal", card.RevealEffects.Length > 0,
                card.RevealEffects.Any(x => x is UnsupportedEffect));
            AssertBox(item, "Acquire", !card.AcquireEffects.IsDefaultOrEmpty,
                !card.AcquireEffects.IsDefaultOrEmpty && card.AcquireEffects.Any(x => x is UnsupportedEffect));
        }
        foreach (var space in turns.Spaces)
        {
            var item = items[space.Id.Value];
            Assert.True(BoardEffectsMatchAudit(item, space), $"Board effect drift: {space.Id.Value}");
            AssertBox(item, "Agent", space.Effects.Length > 0 ||
                !space.PlacementOptions.IsDefaultOrEmpty, space.Effects.Any(x => x is UnsupportedEffect));
            if (!space.RevealEffects.IsDefaultOrEmpty)
                AssertBox(item, "Reveal", true, space.RevealEffects.Any(x => x is UnsupportedEffect));
            if (space.ControlReward is not null)
                AssertBox(item, "Triggered", true, false);
            var placement = item.GetProperty("abilities").EnumerateArray()
                .Single(x => x.GetProperty("slot").GetString() == "Agent");
            if (space.Combat)
                Assert.Contains(placement.GetProperty("descriptors").EnumerateArray(), x =>
                    x.GetProperty("value").GetString() == "Combat placement");
            if (space.RequiredInfluence > 0)
                Assert.Contains(placement.GetProperty("descriptors").EnumerateArray(), x =>
                    x.GetProperty("kind").GetString() == "precondition" &&
                    x.GetProperty("value").GetString()!.Contains("two Fremen influence"));
            if (space.OncePerGame)
                Assert.Contains(placement.GetProperty("descriptors").EnumerateArray(), x =>
                    x.GetProperty("kind").GetString() == "usage" &&
                    x.GetProperty("value").GetString() == "once per player per game");
            if (space.Effects.Any(x => x is HighCouncilEffect))
                AssertBox(item, "PersistentModifier", true, false);
        }
        foreach (var key in new[] { "foldspace", "secrets", "selective-breeding" })
        {
            var id = $"imperium:space:{key}:v1";
            Assert.DoesNotContain(turns.Spaces, x => x.Id.Value == id);
            var item = items[id];
            Assert.Equal("discovered", Slot(item, "Agent").GetProperty("discovery").GetString());
            Assert.Contains(item.GetProperty("abilities").EnumerateArray(), x =>
                x.GetProperty("slot").GetString() == "Agent" &&
                x.GetProperty("implementation").GetString() == "unsupported");
        }
        foreach (var intrigue in lifecycle.Intrigues)
        {
            var item = items[intrigue.Id.Value];
            var name = intrigue.Timing switch
            {
                IntrigueTiming.Combat => "Combat",
                IntrigueTiming.Endgame => "Endgame",
                IntrigueTiming.PostWin => "PostWin",
                _ => "Intrigue",
            };
            AssertBox(item, name, intrigue.Effects.Length > 0,
                intrigue.Effects.Any(x => x is UnsupportedEffect));
        }
        Assert.Empty(turns.Leaders);
        Assert.Empty(lifecycle.Conflicts);
        Assert.DoesNotContain(items.Values, x => x.GetProperty("authenticity").GetString() == "synthetic");
    }

    [Fact]
    public void RemovingAuditedBoardRevealOrControllerRewardIsDetected()
    {
        using var document = LoadInventory();
        var items = document.RootElement.GetProperty("content").EnumerateArray().ToDictionary(Id);
        var rules = OriginalImperiumTurnRules.Create().Spaces;
        var hall = rules.Single(x => x.Id.Value == "imperium:space:hall-of-oratory:v1");
        Assert.True(BoardEffectsMatchAudit(items[hall.Id.Value], hall));
        Assert.False(BoardEffectsMatchAudit(items[hall.Id.Value], hall with { RevealEffects = [] }));

        foreach (var name in new[] { "arrakeen", "carthag", "imperial-basin" })
        {
            var rule = rules.Single(x => x.Id.Value == $"imperium:space:{name}:v1");
            Assert.True(BoardEffectsMatchAudit(items[rule.Id.Value], rule));
            Assert.False(BoardEffectsMatchAudit(items[rule.Id.Value], rule with { ControlReward = null }));
        }
    }
}
