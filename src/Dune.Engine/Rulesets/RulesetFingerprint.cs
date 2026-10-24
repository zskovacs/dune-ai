using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;

namespace Dune.Engine.Rulesets;

internal static class RulesetFingerprint
{
    // Version this format when canonicalization or semantics change. Never use runtime GetHashCode.
    public static string Compute(GameConfiguration c, ImmutableArray<RuleModule> modules,
        ImmutableArray<ResolvedRule> rules, ImmutableArray<ContentDefinition> content,
        ImmutableArray<ContentId> leaders, CatalogCompleteness completeness)
    {
        static IEnumerable<string> Ids(IEnumerable<ContentId> ids) => ids.Select(x => x.Value).Order(StringComparer.Ordinal);
        static IEnumerable<string> ModuleIds(IEnumerable<ModuleId> ids) => ids.Select(x => x.Value).Order(StringComparer.Ordinal);
        static IEnumerable<T> Enums<T>(IEnumerable<T> values) where T : struct, Enum => values.Order();
        static object Sources(IEnumerable<SourceReference> sources) => sources.OrderBy(x => x.Document, StringComparer.Ordinal)
            .ThenBy(x => x.Page).ThenBy(x => x.Section, StringComparer.Ordinal).ToArray();
        static object Definition(RuleDefinition d) => new { d.Id, d.Key, d.Value, Sources = Sources(d.Sources) };
        var canonical = new
        {
            Format = "dune-ruleset-sha256-v1",
            Configuration = new
            {
                c.SchemaVersion,
                c.BaseGame,
                Expansions = Enums(c.Expansions),
                Modules = Enums(c.Modules),
                c.Mode,
                Variants = Enums(c.Variants),
                c.Players,
                Leaders = new
                {
                    c.Leaders.Strategy,
                    c.Leaders.PlayerOrder,
                    CandidateLeaders = Ids(c.Leaders.CandidateLeaders),
                    FixedAssignments = c.Leaders.FixedAssignments.OrderBy(x => x.PlayerId),
                    RestrictionIds = c.Leaders.RestrictionIds.Order(StringComparer.Ordinal),
                },
                Content = new { c.Content.Id, c.Content.Revision, Included = Ids(c.Content.Included), Excluded = Ids(c.Content.Excluded) },
            },
            Modules = modules.OrderBy(x => x.Id.Value, StringComparer.Ordinal).Select(x => new
            {
                x.Id,
                x.Revision,
                Dependencies = ModuleIds(x.Dependencies),
                IncompatibleWith = ModuleIds(x.IncompatibleWith),
                Contributions = x.Contributions.OrderBy(y => y.Definition.Id.Value, StringComparer.Ordinal)
                    .Select(y => new { Definition = Definition(y.Definition), y.Replaces }),
            }),
            Definitions = rules.Select(x => new
            {
                Definition = Definition(x.Definition),
                History = x.History.Select(y => new { y.Module, Definition = Definition(y.Definition) }),
            }),
            Content = content.Select(x => new
            {
                x.Id,
                x.Name,
                x.Product,
                x.Category,
                x.Quantity,
                Availability = new
                {
                    BaseGames = Enums(x.Availability.BaseGames),
                    RequiredExpansions = Enums(x.Availability.RequiredExpansions),
                    ExcludedExpansions = Enums(x.Availability.ExcludedExpansions),
                    RequiredModules = Enums(x.Availability.RequiredModules),
                    ExcludedModules = Enums(x.Availability.ExcludedModules),
                    Modes = Enums(x.Availability.Modes),
                    Formats = Enums(x.Availability.Formats),
                    x.Availability.MinimumPlayers,
                    x.Availability.MaximumPlayers,
                },
                Sources = Sources(x.Sources),
                x.Implementation,
                x.Verification,
                x.Revision,
            }),
            EligibleLeaders = Ids(leaders),
            CatalogCompleteness = completeness,
        };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical, ConfigurationJson.Options)));
    }
}
