using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dune.Engine.Configuration;

namespace Dune.Engine.Content;

public sealed class ContentCatalog
{
    public ContentCatalog(IEnumerable<ContentDefinition> definitions, CatalogCompleteness completeness = CatalogCompleteness.Representative)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        Definitions = definitions.OrderBy(x => x?.Id.Value, StringComparer.Ordinal).ToImmutableArray()!;
        Completeness = completeness;
        if (!Enum.IsDefined(completeness))
            throw new ArgumentException("Unknown catalog completeness.", nameof(completeness));
        var seen = new HashSet<ContentId>();
        foreach (var definition in Definitions)
        {
            if (definition is null || !ValidId(definition.Id.Value) || !seen.Add(definition.Id))
                throw new ArgumentException("Content IDs must be valid and unique.", nameof(definitions));
            if (string.IsNullOrWhiteSpace(definition.Name) || definition.Quantity <= 0 || definition.Revision <= 0 ||
                !Enum.IsDefined(definition.Product) || !Enum.IsDefined(definition.Category) ||
                !Enum.IsDefined(definition.Implementation) || !Enum.IsDefined(definition.Verification) ||
                definition.Availability is null || !ValidSources(definition.Sources) || !ValidAvailability(definition.Availability))
                throw new ArgumentException($"Invalid content metadata: {definition.Id.Value}.", nameof(definitions));
            if (!HasProductRequirement(definition))
                throw new ArgumentException($"Content ownership must be reflected in availability: {definition.Id.Value}.", nameof(definitions));
        }
    }

    public ImmutableArray<ContentDefinition> Definitions { get; }
    public CatalogCompleteness Completeness { get; }

    public static ContentCatalog LoadRepresentative()
    {
        using var stream = typeof(ContentCatalog).Assembly.GetManifestResourceStream("Dune.Engine.Content.representative.json")
            ?? throw new InvalidOperationException("Representative catalog resource is missing.");
        var definitions = JsonSerializer.Deserialize<ImmutableArray<ContentDefinition>>(stream, ConfigurationJson.Options);
        if (definitions.IsDefault) throw new JsonException("A content array is required.");
        return new ContentCatalog(definitions);
    }

    internal static bool ValidId(string? value) => value is not null && Regex.IsMatch(value, @"\A[a-z0-9]+(?:[.:-][a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    internal static bool ValidSources(ImmutableArray<SourceReference> sources) =>
        !sources.IsDefaultOrEmpty && sources.All(x => x is not null && !string.IsNullOrWhiteSpace(x.Document) && x.Page > 0 && !string.IsNullOrWhiteSpace(x.Section));

    private static bool ValidSet<T>(ImmutableArray<T> values) where T : struct, Enum =>
        !values.IsDefault && values.All(x => Enum.IsDefined(x)) && values.Distinct().Count() == values.Length;

    private static bool ValidAvailability(AvailabilityRequirements a) =>
        ValidSet(a.BaseGames) && ValidSet(a.RequiredExpansions) && ValidSet(a.ExcludedExpansions) &&
        ValidSet(a.RequiredModules) && ValidSet(a.ExcludedModules) && ValidSet(a.Modes) && ValidSet(a.Formats) &&
        !a.RequiredExpansions.Intersect(a.ExcludedExpansions).Any() && !a.RequiredModules.Intersect(a.ExcludedModules).Any() &&
        a.MinimumPlayers >= 1 && a.MaximumPlayers <= 6 && a.MinimumPlayers <= a.MaximumPlayers;

    private static bool HasProductRequirement(ContentDefinition d) => d.Product switch
    {
        Product.Imperium => d.Availability.BaseGames.Contains(BaseGame.Imperium),
        Product.Uprising => d.Availability.BaseGames.Contains(BaseGame.Uprising),
        Product.RiseOfIx => d.Availability.RequiredExpansions.Contains(Expansion.RiseOfIx),
        Product.Immortality => d.Availability.RequiredExpansions.Contains(Expansion.Immortality),
        Product.Bloodlines => d.Availability.RequiredExpansions.Contains(Expansion.Bloodlines),
        _ => false,
    };
}
