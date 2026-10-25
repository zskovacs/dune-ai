using System.Text.Json;
using Dune.Engine.Content;
using Dune.Engine.Turns;

namespace Dune.Engine.Tests;

public class ContentCoverageRegistryTests
{
    private static JsonDocument LoadInventory() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content-coverage", "inventory.json")));

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;

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
}
