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

    /// <summary>Synthetic cards/rewards with the sourced original board; still not an authentic inventory.</summary>
    internal static TurnRuleSet RulesWithOriginalBoard(GameState state) => Rules(state) with
    { Spaces = OriginalImperiumTurnRules.Create().Spaces };

    /// <summary>A complete invented ten-Conflict recipe for the CLI and runner demonstration.</summary>
    internal static GameState CreateScenarioState(int players, int seed)
    {
        var state = CreateState(players, seed);
        var cards = state.Shared.IntrigueDeck.Cards.OrderBy(x => x.Definition.Value, StringComparer.Ordinal)
            .Take(players * 3).ToArray();
        for (var player = 0; player < players; player++)
            foreach (var card in cards.Skip(player * 3).Take(3))
                state.Shared.IntrigueDeck.MoveTo(card.Id, state.Players[player].Intrigue);
        return state;
    }

    internal static TurnRuleSet ScenarioRules(GameState state)
    {
        var rules = Rules(state);
        var postWin = state.Definition.Content.Where(x => x.Category == ContentCategory.IntrigueCard)
            .OrderBy(x => x.Id.Value, StringComparer.Ordinal).Take(12)
            .Where((_, index) => index % 3 == 2).Select(x => x.Id).ToHashSet();
        return rules with
        {
            // Invented card effects; board-space effects use the existing source-verified definitions.
            Version = "synthetic-lifecycle-scenario-v1",
            Cards = [.. rules.Cards.Select(x => x.Id.Value.StartsWith("fixture:imperiumcard:", StringComparison.Ordinal)
                ? x with { AgentEffects = [], RevealEffects = [new GainEffect(ResourceKind.Persuasion, 1),
                    new GainEffect(ResourceKind.Swords, 1)] } : x)],
            Spaces = OriginalImperiumTurnRules.Create().Spaces,
            Lifecycle = rules.Lifecycle! with
            {
                Version = "synthetic-lifecycle-scenario-v1",
                Intrigues = [.. rules.Lifecycle!.Intrigues.Select(x => x with
                {
                    Timing = postWin.Contains(x.Id) ? IntrigueTiming.PostWin : IntrigueTiming.Combat,
                    Effects = postWin.Contains(x.Id) ? [new GainEffect(ResourceKind.Spice, 1)]
                        : [new GainEffect(ResourceKind.Swords, 1)]
                })],
                Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with
                {
                    Rewards = [new(1, [new GainEffect(ResourceKind.Solari, 3),
                        new OptionalPaymentEffect(new(Solari: 1), [new GainEffect(ResourceKind.Water, 1)])]),
                        new(2, [new GainEffect(ResourceKind.Solari, 2)]),
                        new(3, [new GainEffect(ResourceKind.Solari, 1)])]
                })]
            }
        };
    }

    /// <summary>Invented inventory for sourced Selective Breeding replay tests; the CLI recipe is unchanged.</summary>
    internal static GameState CreateSelectedTrashState(int players = 3)
    {
        var state = CreateScenarioState(players, 42);
        var owner = state.Players[0];
        owner.Resources = owner.Resources with { Spice = 2 };
        // The real StartRound draws five, leaving an empty deck and a nonempty discard for Draw2 refill.
        foreach (var card in owner.Deck.Cards.Skip(5).ToArray()) owner.Deck.MoveTo(card.Id, owner.Discard);
        var reserve =
            state.Shared.Reserves.Single(x => x.Cards[0].Definition.Value == "imperium:reserve:arrakis-liaison:v1");
        foreach (var card in reserve.Cards.Take(2).ToArray()) reserve.MoveTo(card.Id, owner.Discard);
        return state;
    }

    internal static TurnRuleSet SelectedTrashRules(GameState state)
    {
        var rules = ScenarioRules(state);
        var source = state.Players[0].Deck.Cards[0].Definition;
        rules = TurnEngineFixture.CardEffects(rules, source, agent:
        [
            new ForPlayerEffect(1, [
                new SelectedTrashEffect([
                    new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])
                ])
            ])
        ]);
        var endgame = state.Players[2].Intrigue.Cards[^1].Definition;
        return rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues =
                [
                    .. rules.Lifecycle.Intrigues.Select(x => x.Id == endgame
                        ? x with
                        {
                            Timing = IntrigueTiming.Endgame,
                            Effects = [new ForPlayerEffect(1, [new DrawEffect(2), new DrawIntrigueEffect(2)])]
                        }
                        : x)
                ]
            }
        };
    }

    internal static LifecycleRuleSet LifecycleRules(GameState state) => new("synthetic-lifecycle-tests-v1",
        [.. state.Definition.Content.Where(x => x.Category == ContentCategory.IntrigueCard).Select(x =>
            new IntrigueRule(x.Id, IntrigueTiming.Combat, new(), [], Sources))],
        [.. state.Definition.Content.Where(x => x.Category == ContentCategory.ConflictCard).Select(x =>
            new ConflictRule(x.Id, x.ConflictTier!.Value,
                [new(1, [new GainEffect(ResourceKind.Solari, 3)]),
                 new(2, [new GainEffect(ResourceKind.Solari, 2)]),
                 new(3, [new GainEffect(ResourceKind.Solari, 1)])], null, Sources))]);
}
