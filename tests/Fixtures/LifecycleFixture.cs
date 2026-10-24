using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Fixtures;

/// <summary>
/// Engine-only lifecycle seam. Replaces the six unsupported original Foldspace reserve copies with
/// six explicitly synthetic copies, preserving their physical IDs/count. Rebuilds metadata, recipe,
/// configuration/content identities and fingerprint for that synthetic substitution. Authentic
/// initialization and the original reserve definition/effects are unchanged.
/// </summary>
internal static class LifecycleFixture
{
    private static readonly ContentId SyntheticReserve = new("fixture:reserve:lifecycle:v1");
    private static readonly ImmutableArray<SourceReference> Sources =
        [new("Synthetic lifecycle fixture, not official content", 1, "Invented lifecycle reserve and deck effects")];

    internal static GameState CreateState(int players = 3, int seed = 42)
    {
        var original = TurnEngineFixture.CreateState(players, seed);
        var configuration = OriginalImperiumFixture.Configuration(players, seed) with
        {
            Content = new() { Id = "synthetic-lifecycle-fixture", Revision = 1 },
        };
        var oldReserve = new ContentId("imperium:reserve:foldspace:v1");
        var content = original.Definition.Content.Select(x => x.Id == oldReserve
            ? x with
            {
                Id = SyntheticReserve,
                Name = "SYNTHETIC lifecycle reserve",
                Sources = Sources,
                Verification = VerificationStatus.Unverified,
                Implementation = ImplementationStatus.MetadataOnly
            }
            : x).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToImmutableArray();
        var setup = original.Definition.Setup! with
        {
            Reserves = [.. original.Definition.Setup!.Reserves.Select(x => x.Definition == oldReserve
                ? x with { Definition = SyntheticReserve } : x)],
        };
        var definition = original.Definition with
        {
            Content = content,
            Setup = setup,
            ConfigurationIdentity = ConfigurationIdentity.Compute(configuration),
            Fingerprint = RulesetFingerprint.Compute(configuration, original.Definition.Modules,
                original.Definition.Definitions, content, original.Definition.EligibleLeaders,
                original.Definition.CatalogCompleteness, setup),
        };
        var shared = original.Shared;
        var reserves = shared.Reserves.Select(zone => zone.Cards.Any(x => x.Definition == oldReserve)
            ? new CardZone(new("reserve:" + SyntheticReserve.Value), "SYNTHETIC lifecycle reserve", ZoneVisibility.Public,
                cards: zone.Cards.Select(x => x with { Definition = SyntheticReserve }))
            : zone);
        var replacement = new SharedCardState(shared.ImperiumDeck, shared.ImperiumRow, shared.IntrigueDeck,
            reserves, shared.UnusedStarters);
        var contentIdentity = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content, ConfigurationJson.Options)));
        return new(definition, ConfigurationIdentity.Compute(configuration, true), contentIdentity,
            ContentAuthenticity.SyntheticFixture, original.Players, original.Board, replacement,
            original.Random, original.LeadersRandom, original.FirstPlayerId);
    }

    internal static TurnRuleSet Rules(GameState state) => TurnEngineFixture.Rules(state) with
    {
        Cards = [.. TurnEngineFixture.Rules(state).Cards,
            new(SyntheticReserve, [], [], [new GainEffect(ResourceKind.Solari, 1)], 1, []) { Sources = Sources }],
        Lifecycle = LifecycleRules(state),
    };

    internal static LifecycleRuleSet LifecycleRules(GameState state) => new("synthetic-lifecycle-tests-v1",
        [.. state.Definition.Content.Where(x => x.Category == ContentCategory.IntrigueCard).Select(x =>
            new IntrigueRule(x.Id, IntrigueTiming.Combat, new(), [], Sources))],
        [.. state.Definition.Content.Where(x => x.Category == ContentCategory.ConflictCard).Select(x =>
            new ConflictRule(x.Id, x.ConflictTier!.Value,
                [new(1, [new GainEffect(ResourceKind.Solari, 3)]),
                 new(2, [new GainEffect(ResourceKind.Solari, 2)]),
                 new(3, [new GainEffect(ResourceKind.Solari, 1)])], null, Sources))]);
}
