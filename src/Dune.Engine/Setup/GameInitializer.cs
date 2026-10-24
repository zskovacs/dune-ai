using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;

namespace Dune.Engine.Setup;

public sealed record InitializationResult(ImmutableArray<ConfigurationError> Errors, GameState? State)
{
    public bool IsSuccess => Errors.IsEmpty && State is not null;
}

/// <summary>Initializes original Imperium before Round Start; does not implement gameplay.</summary>
public sealed class GameInitializer
{
    private readonly RulesetComposer _composer;

    // Resolved definitions are immutable. Validate and hash their static metadata once per initializer.
    private readonly ConditionalWeakTable<ResolvedGameDefinition, ValidatedDefinition> _validated = new();

    public GameInitializer(RulesetComposer? composer = null) => _composer = composer ?? RulesetComposer.CreateDefault();

    public InitializationResult Create(GameConfiguration? configuration)
    {
        var errors = ValidateConfiguration(configuration);
        if (!errors.IsEmpty) return new(errors, null);
        var resolved = _composer.Compose(configuration);
        return resolved.IsSuccess ? Initialize(configuration, resolved.Definition) : new(resolved.Errors, null);
    }

    public InitializationResult Initialize(GameConfiguration? configuration, ResolvedGameDefinition? definition) =>
        InitializeCore(configuration, definition, false);

    internal InitializationResult InitializeFixture(GameConfiguration? configuration,
        ResolvedGameDefinition? definition) =>
        InitializeCore(configuration, definition, true);

    private InitializationResult InitializeCore(GameConfiguration? configuration, ResolvedGameDefinition? definition,
        bool fixture)
    {
        var errors = ValidateConfiguration(configuration);
        if (!errors.IsEmpty) return new(errors, null);
        if (definition is null) return Failure("required", "definition", "A resolved definition is required.");
        var c = configuration!;
        if (definition.ConfigurationIdentity != ConfigurationIdentity.Compute(c))
            return Failure("configuration-mismatch", "definition",
                "The resolved definition belongs to a different configuration.");
        var validation = _validated.GetValue(definition, d => ValidateDefinition(c, d));
        if (!validation.Errors.IsEmpty) return new(validation.Errors, null);
        var contentErrors = fixture ? validation.FixtureErrors : validation.AuthenticErrors;
        if (!contentErrors.IsEmpty) return new(contentErrors, null);

        var setup = definition.Setup!;
        var random = new SeededRandom(c.RandomSeed);
        var leadersRandom = new SeededRandom(c.Leaders.Seed);
        var leaders = definition.EligibleLeaders.ToList();
        if (c.Leaders.Strategy == LeaderSelectionStrategy.Random) leadersRandom.Shuffle(leaders);
        var assignments = c.Leaders.Strategy == LeaderSelectionStrategy.Fixed
            ? c.Leaders.FixedAssignments.ToDictionary(x => x.PlayerId, x => x.Leader)
            : c.Leaders.PlayerOrder.Select((id, index) => (id, leader: leaders[index]))
                .ToDictionary(x => x.id, x => x.leader);

        long nextId = 0;
        var instances = definition.Content.Where(x => IsCard(x.Category))
            .OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToDictionary(x => x.Id,
                x => Enumerable.Range(0, x.Quantity).Select(_ => new CardInstance(new(++nextId), x.Id)).ToList());

        List<CardInstance> Category(ContentCategory category) => definition.Content.Where(x => x.Category == category)
            .OrderBy(x => x.Id.Value, StringComparer.Ordinal).SelectMany(x => instances[x.Id]).ToList();

        CardZone Zone(string id, ZoneVisibility visibility, IEnumerable<CardInstance> cards, int? owner = null) =>
            new(new(id), id, visibility, owner, cards);

        var players = new List<PlayerState>();
        for (var id = 0; id < c.Players.Count; id++)
        {
            var cards = setup.StartingDeck.SelectMany(x => instances[x.Definition]
                .Skip(id * x.CopiesPerPlayer).Take(x.CopiesPerPlayer)).ToList();
            random.Shuffle(cards);
            players.Add(new(id, assignments[id], setup.Player,
                Zone($"player:{id}:deck", ZoneVisibility.Hidden, cards, id), setup.StartingDeck));
        }

        var unusedStarters =
            setup.StartingDeck.SelectMany(x => instances[x.Definition].Skip(c.Players.Count * x.CopiesPerPlayer));
        var imperium = Category(ContentCategory.ImperiumCard);
        random.Shuffle(imperium);
        var intrigue = Category(ContentCategory.IntrigueCard);
        random.Shuffle(intrigue);
        var conflicts = new List<CardInstance>();
        var unusedConflicts = new List<CardInstance>();
        foreach (var tier in setup.Conflicts.OrderBy(x => x.Tier))
        {
            var cards = definition.Content
                .Where(x => x.Category == ContentCategory.ConflictCard && x.ConflictTier == tier.Tier)
                .OrderBy(x => x.Id.Value, StringComparer.Ordinal).SelectMany(x => instances[x.Id]).ToList();
            random.Shuffle(cards);
            conflicts.AddRange(cards.Take(tier.Active));
            unusedConflicts.AddRange(cards.Skip(tier.Active));
        }

        var shared = new SharedCardState(
            Zone("imperium:deck", ZoneVisibility.Hidden, imperium.Skip(setup.SharedCards.ImperiumRowSize)),
            Zone("imperium:row", ZoneVisibility.Public, imperium.Take(setup.SharedCards.ImperiumRowSize)),
            Zone("intrigue:deck", ZoneVisibility.Hidden, intrigue),
            setup.Reserves.Select(x =>
                Zone("reserve:" + x.Definition.Value, ZoneVisibility.Public, instances[x.Definition])),
            Zone("outside:unused-starters", ZoneVisibility.Hidden, unusedStarters));
        var conflict = new ConflictState(Zone("conflict:deck", ZoneVisibility.Hidden, conflicts),
            Zone("outside:unused-conflicts", ZoneVisibility.Hidden, unusedConflicts));
        var firstPlayer = random.Next(c.Players.Count);
        return new([], new(definition, ConfigurationIdentity.Compute(c, true), validation.ContentIdentity,
            fixture ? ContentAuthenticity.SyntheticFixture : ContentAuthenticity.VerifiedCatalog,
            players, new(setup.Board, conflict), shared, random, leadersRandom, firstPlayer));
    }

    private static bool IsCard(ContentCategory category) => category is ContentCategory.StartingCard or
        ContentCategory.ImperiumCard or ContentCategory.IntrigueCard or ContentCategory.ReserveCard
        or ContentCategory.ConflictCard;

    private static ValidatedDefinition ValidateDefinition(GameConfiguration c, ResolvedGameDefinition d)
    {
        var errors = ImmutableArray.CreateBuilder<ConfigurationError>();
        void Error(string code, string message) => errors.Add(new(code, "definition", message));
        if (d.Content.IsDefault || d.Modules.IsDefault || d.Definitions.IsDefault || d.EligibleLeaders.IsDefault)
            return new([new("invalid-definition", "definition", "Resolved definition arrays must be initialized.")], [],
                [], "");
        if (d.Setup is null)
            Error("unsupported-setup", "The resolved ruleset has no supported base initialization recipe.");
        try
        {
            _ = new ContentCatalog(d.Content, d.CatalogCompleteness);
            var modules = BuiltinModules.Select(c);
            var rules = modules.SelectMany(m => m.Contributions.Select(x =>
                    new ResolvedRule(x.Definition, [new(m.Id, x.Definition)])))
                .OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Slot, StringComparer.Ordinal)
                .ToImmutableArray();
            static string Json<T>(T value) => JsonSerializer.Serialize(value, ConfigurationJson.Options);
            if (Json(d.Modules) != Json(modules) || Json(d.Definitions) != Json(rules) ||
                Json(d.Setup) != Json(GameSetupDefinition.Resolve(c)))
            {
                Error("unsupported-setup",
                    "Only unchanged original Imperium base definitions and recipes can initialize.");
                return new(errors.ToImmutable(), [], [], "");
            }

            if (d.Content.Any(x => !x.Availability.Allows(c) || c.Content.Excluded.Contains(x.Id) ||
                                   (!c.Content.Included.IsEmpty && !c.Content.Included.Contains(x.Id))))
                Error("unavailable-content", "The resolved content does not match configuration eligibility.");
            var eligible = d.Content.Where(x => x.Category == ContentCategory.Leader).Select(x => x.Id)
                .Where(x => c.Leaders.CandidateLeaders.IsEmpty || c.Leaders.CandidateLeaders.Contains(x))
                .OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
            if (!eligible.SequenceEqual(d.EligibleLeaders) || eligible.Length < c.Players.Count ||
                c.Leaders.CandidateLeaders.Any(x => !eligible.Contains(x)) ||
                c.Leaders.FixedAssignments.Any(x => !eligible.Contains(x.Leader)))
                Error("ineligible-leader",
                    "Resolved leaders or fixed assignments do not match the eligible unique pool.");
            if (d.Fingerprint != RulesetFingerprint.Compute(c, d.Modules, d.Definitions, d.Content,
                    d.EligibleLeaders, d.CatalogCompleteness, d.Setup))
                Error("definition-mismatch", "Resolved metadata no longer matches its ruleset fingerprint.");
        }
        catch (ArgumentException)
        {
            Error("invalid-definition", "Resolved content metadata is invalid.");
        }
        catch (InvalidOperationException)
        {
            Error("invalid-definition", "Resolved metadata contains an uninitialized collection.");
        }

        if (errors.Count != 0) return new(errors.ToImmutable(), [], [], "");
        var identity = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            d.Content.OrderBy(x => x.Id.Value, StringComparer.Ordinal), ConfigurationJson.Options)));
        return new([], SetupContentValidation.Validate(d, true), SetupContentValidation.Validate(d), identity);
    }

    private static ImmutableArray<ConfigurationError> ValidateConfiguration(GameConfiguration? c)
    {
        if (c is null) return [new("required", "configuration", "A configuration is required.")];
        var errors = ConfigurationValidation.Validate(c).ToBuilder();

        void Unsupported(bool condition, string path, string message)
        {
            if (condition) errors.Add(new("unsupported-setup", path, message));
        }

        Unsupported(c.BaseGame != BaseGame.Imperium, "baseGame", "Initialization supports only original Imperium.");
        Unsupported(c.Mode != GameMode.Standard, "mode", "Initialization supports only Standard mode.");
        Unsupported(!c.Expansions.IsDefaultOrEmpty || !c.Modules.IsDefaultOrEmpty, "expansions",
            "Expansion and optional module initialization is not implemented.");
        Unsupported(!c.Variants.IsDefaultOrEmpty, "variants", "Variant initialization is not implemented.");
        if (c.Players is { } p)
            Unsupported(p.Format != PlayerFormat.Competitive || p.Count is not (3 or 4),
                "players", "Initialization requires three or four competitive seats.");
        if (c.Leaders is { } l)
            Unsupported(l.Strategy is not (LeaderSelectionStrategy.Fixed or LeaderSelectionStrategy.Random),
                "leaders.strategy", "Leader drafts are not implemented; use fixed or random assignment.");
        return errors.ToImmutable();
    }

    private static InitializationResult Failure(string code, string path, string message) =>
        new([new(code, path, message)], null);

    private sealed record ValidatedDefinition(
        ImmutableArray<ConfigurationError> Errors,
        ImmutableArray<ConfigurationError> FixtureErrors,
        ImmutableArray<ConfigurationError> AuthenticErrors,
        string ContentIdentity);
}
