using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class LifecycleDefinitionTests
{
    [Fact]
    public void OfficialAmbushRetainsSourceAndExecutesFourSwordsThroughRealWindow()
    {
        var state = OfficialIntrigueState("ambush");
        var lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules();
        var rule = lifecycle.Intrigues.Single(x => x.Id.Value == "imperium:intrigue:ambush:v1");
        Assert.Equal(IntrigueTiming.Combat, rule.Timing);
        Assert.Equal(new GainEffect(ResourceKind.Swords, 4), Assert.Single(rule.Effects));
        Assert.Contains(rule.Sources, x => x.Document == "DUNE_IMPERIUM_Rules_2020_10_26.pdf" && x.Page == 7);
        Assert.Contains(rule.Sources, x => x.Page == 12);
        var engine = new TurnEngine(state, Rules(state) with { Lifecycle = lifecycle with { Factions = [] } });
        Apply(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single())));
        Assert.Equal(4, state.Players[0].Temporary.Swords);
        Assert.Equal(6, CombatStrength.Calculate([new("troop", state.Players[0].Units.TroopsInConflict, 2)], state.Players[0].Temporary.Swords));
        Assert.Single(state.Shared.IntrigueDiscard.Cards);
    }

    [Fact]
    public void CompleteMasterTacticianIsSupportedWhileIndependentCornerConditionsStayUnsupported()
    {
        var rules = OriginalImperiumTurnRules.CreateLifecycleRules();
        var tactician = rules.Intrigues.Single(x => x.Id.Value.Contains("master-tactician", StringComparison.Ordinal));
        Assert.Equal(IntrigueTiming.Combat, tactician.Timing);
        var choice = Assert.IsType<ChoiceEffect>(Assert.Single(tactician.Effects));
        Assert.Equal(2, choice.Options.Length);
        Assert.Equal(new GainEffect(ResourceKind.Swords, 3), Assert.Single(choice.Options[0].Effects));
        Assert.Equal(new RetreatEffect(3), Assert.Single(choice.Options[1].Effects));
        Assert.Contains(tactician.Sources, x => x.Page == 12);
        var corner = rules.Intrigues.Single(x => x.Id.Value.Contains("corner-the-market", StringComparison.Ordinal));
        Assert.Equal(IntrigueTiming.Endgame, corner.Timing);
        Assert.Equal(new[] { EndgameCondition.OwnAtLeastTwoSpiceMustFlow, EndgameCondition.OwnMoreSpiceMustFlowThanEachOpponent },
            corner.Effects.Cast<ConditionalEffect>().Select(x => x.Condition));
        Assert.All(corner.Effects.Cast<ConditionalEffect>(), x => Assert.Equal(new GainEffect(ResourceKind.VictoryPoints, 1), Assert.Single(x.Effects)));
        var graph = new EffectGraphValidator();
        Assert.Empty(graph.Validate(tactician.Effects));
        Assert.Contains(graph.Validate(corner.Effects), x => x.Status == StepStatus.Unsupported);
        foreach (var face in new[] { tactician, corner })
        {
            var json = JsonSerializer.Serialize(face, ConfigurationJson.Options);
            Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<IntrigueRule>(json, ConfigurationJson.Options), ConfigurationJson.Options));
        }
    }

    [Fact]
    public void PartialSourceRegistryDoesNotInventConflictTiersQuantitiesOrPostWinValues()
    {
        var rules = OriginalImperiumTurnRules.CreateLifecycleRules();
        Assert.Empty(rules.Conflicts); // p.12 reward rows do not establish I/II/III deck classes.
        Assert.Equal(4, rules.Factions.Length);
        Assert.Equal(new RecruitEffect(2), Assert.Single(rules.Factions.Single(x => x.Faction == Faction.Emperor).ThresholdFourReward));
        Assert.Equal(new GainEffect(ResourceKind.Solari, 3), Assert.Single(rules.Factions.Single(x => x.Faction == Faction.SpacingGuild).ThresholdFourReward));
        Assert.Equal(new DrawIntrigueEffect(1), Assert.Single(rules.Factions.Single(x => x.Faction == Faction.BeneGesserit).ThresholdFourReward));
        Assert.Equal(new GainEffect(ResourceKind.Water, 1), Assert.Single(rules.Factions.Single(x => x.Faction == Faction.Fremen).ThresholdFourReward));
        Assert.All(rules.Factions, x =>
        {
            Assert.Equal(6, x.MaximumInfluence);
            Assert.Contains(x.Sources, source => source.Section.Contains("VISUAL INFERENCE", StringComparison.Ordinal));
        });
        Assert.DoesNotContain(rules.Intrigues, x => x.Timing == IntrigueTiming.PostWin);
        Assert.DoesNotContain(rules.Intrigues, x => x.Id.Value is "imperium:intrigue:demand-respect:v1" or "imperium:intrigue:to-the-victor:v1");
        Assert.DoesNotContain(rules.Intrigues.SelectMany(x => x.Sources),
            x => x.Section.Contains("before rewards", StringComparison.OrdinalIgnoreCase));
        var plot = Assert.Single(rules.Intrigues.Where(x => x.Timing == IntrigueTiming.Plot));
        Assert.Equal(new ResourceCost(Spice: 1), plot.Cost);
        Assert.IsType<UnsupportedEffect>(Assert.Single(plot.Effects));
        Assert.Contains(plot.Sources, x => x.Page == 7 && x.Section.Contains("one-spice cost", StringComparison.Ordinal));
        Assert.Equal("imperium-2020-lifecycle-authoring-v5", rules.Version);
        Assert.Null(OriginalImperiumTurnRules.Create().Lifecycle);
        var result = new GameInitializer(RulesetComposer.CreateDefault()).Create(new GameConfiguration());
        Assert.False(result.IsSuccess);
        Assert.Null(result.State);
        Assert.Contains(result.Errors, x => x.Code == "incomplete-catalog");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownConflictAndMandatoryUnsupportedRewardRejectBeforeAnyStartupMutation(bool knownConflict)
    {
        var state = LifecycleFixture.CreateState();
        var lifecycle = LifecycleFixture.LifecycleRules(state);
        var rules = LifecycleFixture.Rules(state) with
        {
            Lifecycle = lifecycle with
            {
                Conflicts = knownConflict
                ? [.. lifecycle.Conflicts.Select(x => x with { Rewards = [new(1, [new UnsupportedEffect("Synthetic mandatory reward unavailable")]), new(2, []), new(3, [])] })]
                : [],
            }
        };
        var engine = new TurnEngine(state, rules);
        AssertRejectedUnchanged(engine, new StartRoundAction(), StepStatus.Unsupported);
        Assert.All(Enumerable.Range(0, 3), seat => Assert.Equal("lifecycle-deck-support", Assert.Single(engine.SupportDiagnostics(seat)).Code));
    }

    [Fact]
    public void MandatoryControlRewardStaysUnsupportedUntilItsRuntimeTask()
    {
        var state = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with { ControlSpace = Space("arrakeen") })],
            }
        };
        AssertRejectedUnchanged(new TurnEngine(state, rules), new StartRoundAction(), StepStatus.Unsupported);
    }

    [Fact]
    public void PlotAnywhereInEnabledInventoryBlocksStartupWithoutRevealingItsIdentity()
    {
        var state = LifecycleFixture.CreateState();
        var lifecycle = LifecycleFixture.LifecycleRules(state);
        var hidden = state.Shared.IntrigueDeck.Cards[^1].Definition;
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state) with
        {
            Lifecycle = lifecycle with
            {
                Intrigues = [.. lifecycle.Intrigues.Select(x => x.Id == hidden ? x with { Timing = IntrigueTiming.Plot } : x)],
            }
        });
        AssertRejectedUnchanged(engine, new StartRoundAction(), StepStatus.Unsupported);
        Assert.Equal(new TurnDiagnostic(StepStatus.Unsupported, "lifecycle-deck-support", "An enabled deck definition is unsupported."),
            Assert.Single(engine.SupportDiagnostics(0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlotPlayDuringAgentAndRevealTurnsPreservesRngStateFramesAndHistory(bool reveal)
    {
        var state = LifecycleFixture.CreateState();
        state.Phase = GamePhase.PlayerTurns;
        state.Round = 1;
        state.CurrentPlayerId = 0;
        state.DecisionOwnerId = 0;
        for (var i = 0; i < 5; i++) state.Players[0].Deck.TryDrawTo(state.Players[0].Hand);
        state.Shared.IntrigueDeck.TryDrawTo(state.Players[0].Intrigue);
        // Explicit pre-turn fixture seam bypasses startup, then enters Reveal through the real action.
        var card = state.Players[0].Intrigue.Cards.Single();
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state) with
        {
            Lifecycle = new("synthetic-plot-test-v1",
            [new(card.Definition, IntrigueTiming.Plot, new(), [], SyntheticSource)])
        });
        if (reveal)
        {
            Apply(engine, new RevealAction(0));
            ResolveAll(engine);
        }
        AssertRejectedUnchanged(engine, new PlayIntrigueAction(0, Card(card)), StepStatus.Unsupported);
    }

    [Fact]
    public void HiddenUnsupportedIdentityAndOrderNeverChangePublicDiagnostics()
    {
        var first = LifecycleFixture.CreateState(seed: 42);
        var second = LifecycleFixture.CreateState(seed: 43);
        var firstRules = LifecycleFixture.LifecycleRules(first);
        var secondRules = LifecycleFixture.LifecycleRules(second);
        var firstId = first.Shared.IntrigueDeck.Cards[0].Definition;
        var secondId = second.Shared.IntrigueDeck.Cards[^1].Definition;
        Assert.NotEqual(firstId, secondId);
        var firstEngine = new TurnEngine(first, LifecycleFixture.Rules(first) with { Lifecycle = firstRules with { Intrigues = [.. firstRules.Intrigues.Where(x => x.Id != firstId)] } });
        var secondEngine = new TurnEngine(second, LifecycleFixture.Rules(second) with { Lifecycle = secondRules with { Intrigues = [.. secondRules.Intrigues.Where(x => x.Id != secondId)] } });
        Assert.Equal(firstEngine.SupportDiagnostics(0), secondEngine.SupportDiagnostics(0));
        Assert.Equal(firstEngine.Step(new StartRoundAction()).Diagnostics, secondEngine.Step(new StartRoundAction()).Diagnostics);
    }

    [Theory]
    [InlineData("timing")]
    [InlineData("cost")]
    [InlineData("id")]
    [InlineData("source")]
    [InlineData("inapplicable-source")]
    [InlineData("id-category")]
    [InlineData("synthetic-provenance")]
    [InlineData("tier")]
    [InlineData("reward-position")]
    [InlineData("duplicate-reward")]
    [InlineData("participation")]
    [InlineData("faction")]
    public void MalformedLifecycleDefinitionsRejectWithoutMutation(string malformed)
    {
        var state = LifecycleFixture.CreateState();
        var lifecycle = LifecycleFixture.LifecycleRules(state);
        var intrigue = lifecycle.Intrigues[0];
        var conflict = lifecycle.Conflicts[0];
        lifecycle = malformed switch
        {
            "timing" => lifecycle with { Intrigues = [intrigue with { Timing = (IntrigueTiming)99 }] },
            "cost" => lifecycle with { Intrigues = [intrigue with { Cost = new(Solari: -1) }] },
            "id" => lifecycle with { Intrigues = [intrigue with { Id = new("invalid id") }] },
            "source" => lifecycle with { Intrigues = [intrigue with { Sources = [] }] },
            "inapplicable-source" => lifecycle with
            {
                Intrigues = [intrigue with { Id = new("imperium:intrigue:ambush:v1"),
                Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 4, "Setup is not Intrigue face/timing evidence")] }]
            },
            "id-category" => lifecycle with
            {
                Intrigues = [intrigue with { Id = new("imperium:reserve:ambush:v1"),
                Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 7, "Incorrect component category")] }]
            },
            "synthetic-provenance" => lifecycle with { Intrigues = [intrigue with { Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 7, "Official citation cannot authorize fixture effects")] }] },
            "tier" => lifecycle with { Conflicts = [conflict with { Tier = (ConflictTier)99 }] },
            "reward-position" => lifecycle with { Conflicts = [conflict with { Rewards = [new(0, []), new(2, []), new(3, [])] }] },
            "duplicate-reward" => lifecycle with { Conflicts = [conflict with { Rewards = [new(1, []), new(1, []), new(3, [])] }] },
            "participation" => lifecycle with { Participation = (CombatParticipationPolicy)99 },
            "faction" => lifecycle with { Factions = [new((Faction)99, 6, [], SyntheticSource)] },
            _ => throw new InvalidOperationException(),
        };
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state) with { Lifecycle = lifecycle });
        AssertRejectedUnchanged(engine, new StartRoundAction(), StepStatus.ContentError);
    }

    [Fact]
    public void RejectedLifecycleShapeCannotShareIdentityWithSupportedNormalizedPolicies()
    {
        var first = LifecycleFixture.CreateState();
        var second = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(first);
        var valid = new TurnEngine(first, rules);
        var invalid = new TurnEngine(second, rules with { Lifecycle = rules.Lifecycle! with { Participation = (CombatParticipationPolicy)99 } });
        Assert.NotEqual(valid.CaptureReplay().RulesIdentity, invalid.CaptureReplay().RulesIdentity);
        AssertRejectedUnchanged(invalid, new StartRoundAction(), StepStatus.ContentError);
        Apply(valid, new StartRoundAction());
    }

    [Theory]
    [InlineData("calculated-hire", GamePhase.Combat, StepStatus.Illegal)]
    [InlineData("demand-respect", GamePhase.Combat, StepStatus.Unsupported)]
    [InlineData("to-the-victor", GamePhase.Endgame, StepStatus.Unsupported)]
    public void PlotAndUnknownPostWinFacesAreExcludedFromCombatAndEndgameWindows(string name, GamePhase phase, StepStatus expected)
    {
        var state = OfficialIntrigueState(name);
        state.Phase = phase;
        var lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules();
        if (expected == StepStatus.Unsupported)
            Assert.DoesNotContain(lifecycle.Intrigues, x => x.Id.Value == "imperium:intrigue:" + name + ":v1");
        var engine = new TurnEngine(state, Rules(state) with { Lifecycle = lifecycle with { Factions = [] } });
        AssertRejectedUnchanged(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single())), expected);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is PlayIntrigueAction);
    }

    [Theory]
    [InlineData("corner-the-market", GamePhase.Endgame)]
    public void UnavailableSourcedPrimitivesRejectEntireFaceBeforePlayingOrPaying(string name, GamePhase phase)
    {
        var state = OfficialIntrigueState(name);
        state.Phase = phase;
        var engine = new TurnEngine(state, Rules(state) with { Lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules() with { Factions = [] } });
        AssertRejectedUnchanged(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single())), StepStatus.Unsupported);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is PlayIntrigueAction);
    }

    [Fact]
    public void FixtureDefinitionsCannotAuthorizeAuthenticState()
    {
        var fixture = LifecycleFixture.CreateState();
        var state = new GameState(fixture.Definition, fixture.ConfigurationIdentity, fixture.ContentIdentity,
            ContentAuthenticity.VerifiedCatalog, fixture.Players, fixture.Board, fixture.Shared,
            fixture.Random, fixture.LeadersRandom, fixture.FirstPlayerId);
        AssertRejectedUnchanged(new TurnEngine(state, LifecycleFixture.Rules(state)), new StartRoundAction(), StepStatus.ContentError);
    }

    [Fact]
    public void UnsupportedUnusedConflictCandidateBlocksStartupIndependentlyOfRandomSelection()
    {
        var first = LifecycleFixture.CreateState(seed: 42);
        var second = LifecycleFixture.CreateState(seed: 43);
        var missing = first.Conflict.Unused.Cards[0].Definition;
        TurnEngine Engine(GameState state)
        {
            var rules = LifecycleFixture.Rules(state);
            return new(state, rules with { Lifecycle = rules.Lifecycle! with { Conflicts = [.. rules.Lifecycle.Conflicts.Where(x => x.Id != missing)] } });
        }
        var a = Engine(first);
        var b = Engine(second);
        AssertRejectedUnchanged(a, new StartRoundAction(), StepStatus.Unsupported);
        AssertRejectedUnchanged(b, new StartRoundAction(), StepStatus.Unsupported);
        Assert.Equal(a.SupportDiagnostics(0), b.SupportDiagnostics(0));
    }

    [Fact]
    public void CompleteSyntheticInventoryStartsAndPreservesPhysicalCopiesWithDistinctHonestIdentity()
    {
        var original = CreateState();
        var state = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(state);
        Assert.Equal(original.Zones.Sum(x => x.Count), state.Zones.Sum(x => x.Count));
        Assert.Equal(original.Zones.SelectMany(x => x.Cards).Select(x => x.Id).OrderBy(x => x.Value),
            state.Zones.SelectMany(x => x.Cards).Select(x => x.Id).OrderBy(x => x.Value));
        Assert.NotEqual(original.ContentIdentity, state.ContentIdentity);
        Assert.NotEqual(original.ConfigurationIdentity, state.ConfigurationIdentity);
        Assert.NotEqual(original.Definition.Fingerprint, state.Definition.Fingerprint);
        Assert.DoesNotContain(state.Zones.SelectMany(x => x.Cards), x => x.Definition.Value == "imperium:reserve:foldspace:v1");
        Assert.Contains(original.Zones.SelectMany(x => x.Cards), x => x.Definition.Value == "imperium:reserve:foldspace:v1");
        var engine = new TurnEngine(state, rules);
        Assert.Empty(engine.SupportDiagnostics(0));
        Apply(engine, new StartRoundAction());
        var replay = TurnEngine.Replay(engine.CaptureReplay(), () => LifecycleFixture.CreateState(), rules);
        Assert.True(replay.IsSuccess);
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, replay.Engine!.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void ActiveReserveAndHiddenMainDeckAreIncludedInStartupSupportGate()
    {
        foreach (var category in new[] { ContentCategory.ReserveCard, ContentCategory.ImperiumCard })
        {
            var state = LifecycleFixture.CreateState();
            var rules = LifecycleFixture.Rules(state);
            var unsupported = state.Definition.Content.First(x => x.Category == category && x.Id.Value.StartsWith("fixture:", StringComparison.Ordinal)).Id;
            rules = rules with { Cards = [.. rules.Cards.Where(x => x.Id != unsupported)] };
            AssertRejectedUnchanged(new TurnEngine(state, rules), new StartRoundAction(), StepStatus.Unsupported);
        }
        // The old fixture's real Foldspace is still unavailable, even with complete synthetic Intrigue/Conflict definitions.
        var oldState = CreateState();
        AssertRejectedUnchanged(new TurnEngine(oldState, Rules(oldState) with { Lifecycle = LifecycleFixture.LifecycleRules(oldState) }),
            new StartRoundAction(), StepStatus.Unsupported);
    }

    [Fact]
    public void ParticipationUsesOriginalTroopsIndependentlyOfStrength()
    {
        CombatUnitContribution[] futureOnly = [new("synthetic-unit", 1, 5)];
        Assert.False(CombatParticipation.IsEligible(CombatParticipationPolicy.OriginalTroops, futureOnly));
        Assert.Equal(9, CombatStrength.Calculate(futureOnly, 4));
        Assert.True(CombatParticipation.IsEligible(CombatParticipationPolicy.OriginalTroops, [new("troop", 1, 0)]));
        Assert.Equal(0, CombatStrength.Calculate([new("troop", 1, 0)], 0));
        Assert.False(CombatParticipation.IsEligible(CombatParticipationPolicy.OriginalTroops, [new("troop", 0, 2)]));
    }

    private static ImmutableArray<SourceReference> SyntheticSource => [new("Synthetic lifecycle fixture", 1, "Invented definition")];

    private static void AssertRejectedUnchanged(TurnEngine engine, GameAction action, StepStatus expected)
    {
        var before = engine.CaptureTrustedSnapshot().Hash;
        var replay = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        Assert.Equal(expected, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(replay, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
    }

    private static GameState OfficialIntrigueState(string name)
    {
        var replacementId = OriginalImperiumFixture.Catalog.Definitions.First(x => x.Category == ContentCategory.IntrigueCard).Id;
        var timingOnly = name is "demand-respect" or "to-the-victor";
        var sourcePage = timingOnly ? 13 : name == "master-tactician" ? 12 : 7;
        var sourceSection = timingOnly
            ? "Timing-only identity; synthetic one-copy rejection inventory, not a complete numeric face"
            : "Illustrated Intrigue face; synthetic one-copy test inventory, not official multiplicity";
        var catalog = new ContentCatalog(OriginalImperiumFixture.Catalog.Definitions.Select(x => x.Id == replacementId
            ? x with
            {
                Id = new("imperium:intrigue:" + name + ":v1"),
                Name = name,
                Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", sourcePage, sourceSection)]
            } : x));
        var configuration = OriginalImperiumFixture.Configuration(3);
        var composer = new RulesetComposer(catalog);
        var state = new GameInitializer(composer).InitializeFixture(configuration, composer.Compose(configuration).Definition).State!;
        var card = state.Shared.IntrigueDeck.Cards.Single(x => x.Definition.Value == "imperium:intrigue:" + name + ":v1");
        state.Shared.IntrigueDeck.MoveTo(card.Id, state.Players[0].Intrigue);
        state.Players[0].Units = state.Players[0].Units with { TroopsInSupply = state.Players[0].Units.TroopsInSupply - 1, TroopsInConflict = 1 };
        state.Phase = GamePhase.Combat;
        state.FirstPlayerId = 0;
        return state;
    }
}
