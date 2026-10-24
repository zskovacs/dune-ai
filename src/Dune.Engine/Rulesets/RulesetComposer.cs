using System.Collections.Immutable;
using Dune.Engine.Configuration;
using Dune.Engine.Content;

namespace Dune.Engine.Rulesets;

/// <summary>Resolves immutable metadata once before gameplay. No discovery or per-action hooks.</summary>
public sealed class RulesetComposer
{
    private readonly ContentCatalog catalog;
    private readonly ImmutableArray<RuleModule>? explicitModules;

    private RulesetComposer(ContentCatalog catalog) => this.catalog = catalog;

    /// <summary>Explicit modules are the complete composition input, useful for authored catalogs and graph validation.</summary>
    public RulesetComposer(ContentCatalog catalog, IEnumerable<RuleModule> modules)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(modules);
        this.catalog = catalog;
        explicitModules = modules.ToImmutableArray();
    }

    public static RulesetComposer CreateDefault() => new(ContentCatalog.LoadRepresentative());

    public ResolutionResult Compose(GameConfiguration? configuration)
    {
        if (configuration is null) return Failure(ConfigurationSupport.Unrecognized, [new("required", "configuration", "A configuration is required.")]);
        var configErrors = ConfigurationValidation.Validate(configuration);
        if (!configErrors.IsEmpty)
            return Failure(configErrors.Any(x => x.Code is "unknown-value" or "schema-version") ? ConfigurationSupport.Unrecognized : ConfigurationSupport.Recognized, configErrors);
        var errors = ImmutableArray.CreateBuilder<ConfigurationError>();
        var content = SelectContent(configuration, errors);
        var leaders = ValidateLeaders(configuration, content, errors);
        if (errors.Count != 0) return Failure(ConfigurationSupport.Recognized, errors.ToImmutable());
        var modules = explicitModules ?? BuiltinModules.Select(configuration);
        var orderedModules = ValidateModules(modules, errors);
        if (errors.Count != 0) return Failure(ConfigurationSupport.Recognized, errors.ToImmutable());
        var rules = Merge(orderedModules, errors);
        if (errors.Count != 0) return Failure(ConfigurationSupport.Recognized, errors.ToImmutable());
        var requiredKeys = new DefinitionKey[]
        {
            new(DefinitionKind.BoardLayout, "main"),
            new(DefinitionKind.StartingDeck, "player"),
            new(DefinitionKind.Resources, "initial"),
            new(DefinitionKind.ConflictDeck, "recipe"),
            new(DefinitionKind.VictoryCondition, "threshold"),
        };
        foreach (var key in requiredKeys)
            if (!rules.Any(x => x.Definition.Key == key))
                errors.Add(new("incomplete-definition", "definitions", $"A resolved recipe is required for {key}."));
        if (errors.Count != 0) return Failure(ConfigurationSupport.Recognized, errors.ToImmutable());
        var fingerprint = RulesetFingerprint.Compute(configuration, orderedModules, rules, content, leaders, catalog.Completeness);
        return new(ConfigurationSupport.Validated, GameplaySupport.NotImplemented, [],
            new(fingerprint, orderedModules, rules, content, leaders, catalog.Completeness));
    }

    private static ResolutionResult Failure(ConfigurationSupport support, ImmutableArray<ConfigurationError> errors) =>
        new(support, GameplaySupport.NotImplemented, errors.OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Message, StringComparer.Ordinal).ToImmutableArray(), null);

    private ImmutableArray<ContentDefinition> SelectContent(GameConfiguration c, ImmutableArray<ConfigurationError>.Builder errors)
    {
        var byId = catalog.Definitions.ToDictionary(x => x.Id);
        foreach (var id in c.Content.Included.Concat(c.Content.Excluded))
        {
            if (!byId.TryGetValue(id, out var definition))
                errors.Add(new("unknown-content", "content", $"Unknown catalog ID: {id.Value}."));
            else if (c.Content.Included.Contains(id) && !definition.Availability.Allows(c))
                errors.Add(new("unavailable-content", "content.included", $"Content is incompatible with this configuration: {id.Value}."));
        }
        return catalog.Definitions.Where(x => x.Availability.Allows(c) &&
            (c.Content.Included.IsEmpty || c.Content.Included.Contains(x.Id)) && !c.Content.Excluded.Contains(x.Id)).ToImmutableArray();
    }

    private static ImmutableArray<ContentId> ValidateLeaders(GameConfiguration c, ImmutableArray<ContentDefinition> content, ImmutableArray<ConfigurationError>.Builder errors)
    {
        var available = content.Where(x => x.Category == ContentCategory.Leader).Select(x => x.Id).ToImmutableArray();
        foreach (var id in c.Leaders.CandidateLeaders)
            if (!available.Contains(id)) errors.Add(new("ineligible-leader", "leaders.candidateLeaders", $"Leader is unknown, excluded or unavailable: {id.Value}."));
        var pool = (c.Leaders.CandidateLeaders.IsEmpty ? available : c.Leaders.CandidateLeaders).OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
        if (pool.Length < c.Players.Count)
            errors.Add(new("insufficient-leaders", "leaders", "The representative eligible pool has fewer unique leaders than seats."));
        foreach (var assignment in c.Leaders.FixedAssignments)
            if (!pool.Contains(assignment.Leader)) errors.Add(new("ineligible-leader", "leaders.fixedAssignments", $"Assigned leader is outside the eligible pool: {assignment.Leader.Value}."));
        return pool;
    }

    private static ImmutableArray<RuleModule> ValidateModules(ImmutableArray<RuleModule> modules, ImmutableArray<ConfigurationError>.Builder errors)
    {
        var byId = new Dictionary<ModuleId, RuleModule>();
        var definitionIds = new HashSet<DefinitionId>();
        foreach (var module in modules)
        {
            if (module is null || !ContentCatalog.ValidId(module.Id.Value) || !byId.TryAdd(module.Id, module))
            {
                errors.Add(new("duplicate-or-invalid-module", "modules", "Module IDs must be valid and unique."));
                continue;
            }
            if (module.Revision <= 0 || module.Dependencies.IsDefault || module.IncompatibleWith.IsDefault || module.Contributions.IsDefault ||
                module.Dependencies.Any(x => !ContentCatalog.ValidId(x.Value)) || module.IncompatibleWith.Any(x => !ContentCatalog.ValidId(x.Value)) ||
                module.Dependencies.Distinct().Count() != module.Dependencies.Length || module.IncompatibleWith.Distinct().Count() != module.IncompatibleWith.Length)
            {
                errors.Add(new("invalid-module", module.Id.Value, "Modules need a positive revision and initialized, unique dependency arrays."));
                continue;
            }
            var keys = new HashSet<DefinitionKey>();
            foreach (var contribution in module.Contributions)
            {
                var d = contribution?.Definition;
                if (d is null || !ContentCatalog.ValidId(d.Id.Value) || !definitionIds.Add(d.Id) || !keys.Add(d.Key) ||
                    !Enum.IsDefined(d.Key.Kind) || !ContentCatalog.ValidId(d.Key.Slot) || string.IsNullOrWhiteSpace(d.Value) ||
                    !ContentCatalog.ValidSources(d.Sources) || (contribution!.Replaces is { } expected && !ContentCatalog.ValidId(expected.Value)))
                    errors.Add(new("invalid-definition", module.Id.Value, "Definitions require globally unique IDs, one contribution per key per module, values and sources."));
            }
        }
        if (errors.Count != 0) return [];
        foreach (var module in byId.Values.OrderBy(x => x.Id.Value, StringComparer.Ordinal))
        {
            foreach (var dependency in module.Dependencies)
                if (!byId.ContainsKey(dependency)) errors.Add(new("missing-dependency", module.Id.Value, $"Missing module: {dependency.Value}."));
            foreach (var incompatible in module.IncompatibleWith)
                if (byId.ContainsKey(incompatible)) errors.Add(new("incompatible-modules", module.Id.Value, $"Incompatible active module: {incompatible.Value}."));
        }
        if (errors.Count != 0) return [];
        var ordered = ImmutableArray.CreateBuilder<RuleModule>();
        var remaining = byId.Values.ToList();
        var done = new HashSet<ModuleId>();
        while (remaining.Count != 0)
        {
            var ready = remaining.Where(x => x.Dependencies.All(done.Contains)).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToArray();
            if (ready.Length == 0)
            {
                errors.Add(new("module-cycle", "modules", "Active module dependency graph contains a cycle."));
                return [];
            }
            foreach (var module in ready)
            {
                ordered.Add(module);
                done.Add(module.Id);
                remaining.Remove(module);
            }
        }
        return ordered.ToImmutable();
    }

    private static ImmutableArray<ResolvedRule> Merge(ImmutableArray<RuleModule> modules, ImmutableArray<ConfigurationError>.Builder errors)
    {
        var ancestors = new Dictionary<ModuleId, HashSet<ModuleId>>();
        var resolved = new Dictionary<DefinitionKey, ResolvedRule>();
        foreach (var module in modules)
        {
            var prior = new HashSet<ModuleId>(module.Dependencies);
            foreach (var dependency in module.Dependencies) prior.UnionWith(ancestors[dependency]);
            ancestors.Add(module.Id, prior);
            foreach (var contribution in module.Contributions.OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Slot, StringComparer.Ordinal))
            {
                var definition = contribution.Definition;
                if (!resolved.TryGetValue(definition.Key, out var current))
                {
                    if (contribution.Replaces is not null)
                    {
                        errors.Add(new("replacement-target", module.Id.Value, $"Cannot replace missing key {definition.Key}."));
                        continue;
                    }
                    resolved.Add(definition.Key, new(definition, [new(module.Id, definition)]));
                }
                else if (contribution.Replaces != current.Definition.Id || !prior.Contains(current.History[^1].Module))
                    errors.Add(new("conflicting-definition", module.Id.Value, $"Key {definition.Key} already has {current.Definition.Id.Value}; replacement requires that exact ID and a dependency on its producer."));
                else resolved[definition.Key] = new(definition, current.History.Add(new(module.Id, definition)));
            }
        }
        return resolved.Values.OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Slot, StringComparer.Ordinal).ToImmutableArray();
    }
}
