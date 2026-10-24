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

/// <summary>Official pictured Combat faces in an explicit synthetic one-copy inventory, not a complete game.</summary>
public sealed class CombatPriorityTests
{
    [Theory]
    [InlineData(3, 0, 2)]
    [InlineData(3, 1, 2)]
    [InlineData(3, 3, 2)]
    [InlineData(4, 4, 3)]
    public void PriorityStartsAtFirstPlayerAndWrapsAcrossEligibleSeats(int players, int participants, int first)
    {
        GameState Create() => CombatState(players, first, [.. Enumerable.Range(0, players).Select(x => x < participants ? 1 : 0)]);
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        var expected = Enumerable.Range(0, players).Select(x => (first + x) % players).Where(x => x < participants).ToArray();
        Assert.Equal(expected, engine.Observe(0).Game.Window?.EligibleSeats ?? []);
        foreach (var seat in expected)
        {
            Assert.Null(engine.CurrentPlayerId);
            Assert.Equal(seat, engine.DecisionOwnerId);
            Assert.Equal(new PassPriorityAction(seat), Assert.Single(engine.LegalActions(seat)));
            Apply(engine, new PassPriorityAction(seat));
            Replay(engine, Create, rules);
        }
        if (participants == 1) Apply(engine, new PassPriorityAction(0)); // Close the unique winner's empty PostWin opportunity.
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.False(state.IsCompleted);
        Assert.Equal(GamePhase.Combat, engine.Phase);
        var outcome = engine.CaptureTrustedSnapshot().Data.CombatOutcome!;
        Assert.NotNull(outcome);
        Assert.Equal(participants == 1 ? 0 : null, outcome.WinnerId);
        Assert.Equal(Enumerable.Range(0, players).Where(x => x >= participants), outcome.NoRewardPlayerIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbushAndMasterTacticianCanBothPlayBeforeFinishingOneOpportunity(bool soleParticipant)
    {
        GameState Create()
        {
            var result = CombatState(deployed: soleParticipant ? [1, 0, 0] : [1, 1, 1]);
            Give(result, "ambush", 0);
            Give(result, "master-tactician", 0);
            return result;
        }
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, Play(state, "ambush", 0));
        Assert.Equal(6, state.Players[0].CombatMarker);
        Assert.Equal(0, engine.DecisionOwnerId);
        Replay(engine, Create, rules);
        Apply(engine, Play(state, "master-tactician", 0));
        Replay(engine, Create, rules);
        Resolve<ChoiceEffect>(engine);
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(7, state.Players[0].Temporary.Swords);
        Assert.Equal(9, state.Players[0].CombatMarker);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.Equal(2, engine.Observe(1).Game.Window!.OpportunityPlays);
        Assert.Equal(2, state.Shared.IntrigueDiscard.Count);
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(0));
        Assert.Equal(soleParticipant ? 0 : 1, engine.DecisionOwnerId);
        Assert.Empty(engine.Observe(1).Game.Window!.ConsecutivePasses);
        Replay(engine, Create, rules);
        if (soleParticipant)
        {
            Apply(engine, new PassPriorityAction(0));
            Assert.Equal(DecisionWindowKind.PostWinRewards, engine.Observe(0).Game.Window!.Kind);
            Apply(engine, new PassPriorityAction(0));
            Assert.Null(engine.DecisionOwnerId);
            Replay(engine, Create, rules);
        }
    }

    [Fact]
    public void EarlierPasserCanPlayAgainAfterOtherPlayersPlayAndPass()
    {
        GameState Create()
        {
            var result = CombatState();
            Give(result, "ambush", 1);
            Give(result, "master-tactician", 0);
            return result;
        }
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        Assert.Equal(new[] { 0 }, engine.Observe(0).Game.Window!.ConsecutivePasses);
        Apply(engine, Play(state, "ambush", 1));
        Assert.Empty(engine.Observe(0).Game.Window!.ConsecutivePasses);
        Assert.Equal(1, engine.DecisionOwnerId);
        Apply(engine, new PassPriorityAction(1));
        Apply(engine, new PassPriorityAction(2));
        Assert.Equal(new[] { 2 }, engine.Observe(0).Game.Window!.ConsecutivePasses);
        Apply(engine, Play(state, "master-tactician", 0));
        Assert.Empty(engine.Observe(0).Game.Window!.ConsecutivePasses);
        Resolve<ChoiceEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 0));
        Apply(engine, new PassPriorityAction(0));
        Assert.Empty(engine.Observe(0).Game.Window!.ConsecutivePasses);
        Apply(engine, new PassPriorityAction(1));
        Apply(engine, new PassPriorityAction(2));
        Assert.Equal(0, engine.DecisionOwnerId);
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(0));
        Assert.Equal(DecisionWindowKind.PostWinRewards, engine.Observe(0).Game.Window!.Kind);
        Apply(engine, new PassPriorityAction(1));
        Assert.Null(engine.DecisionOwnerId);
        Replay(engine, Create, rules);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    public void MasterTacticianRetreatOffersBoundedCountAndReturnsOnlyOwnTroopsToGarrison(int troops, int count)
    {
        GameState Create()
        {
            var result = CombatState(deployed: [1, troops, 1]);
            result.Players[1].Temporary = new(0, 5, false);
            Give(result, "master-tactician", 1);
            return result;
        }
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        var before = state.Players.Select(x => x.Units).ToArray();
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, Play(state, "master-tactician", 1));
        Replay(engine, Create, rules);
        Resolve<ChoiceEffect>(engine);
        Assert.Equal(new[] { 0, 1 }, engine.LegalActions(1).OfType<ChooseOptionAction>().Select(x => x.Option));
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(1, 1));
        Replay(engine, Create, rules);
        Resolve<RetreatEffect>(engine);
        Assert.Equal(Enumerable.Range(0, Math.Min(3, troops) + 1), engine.LegalActions(1).OfType<ChooseOptionAction>().Select(x => x.Option));
        Assert.Equal(Math.Min(3, troops) + 1, engine.Observe(1).Decision!.Options.Length);
        Reject(engine, new ChooseOptionAction(1, -1), StepStatus.Illegal);
        Reject(engine, new ChooseOptionAction(1, Math.Min(3, troops) + 1), StepStatus.Illegal);
        Reject(engine, new PassPriorityAction(1), StepStatus.Illegal);
        Assert.Single(state.Shared.IntrigueResolving.Cards);
        Assert.Empty(state.Shared.IntrigueDiscard.Cards);
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(1, count));
        Assert.Equal(before[1].TroopsInSupply, state.Players[1].Units.TroopsInSupply);
        Assert.Equal(before[1].TroopsInGarrison + count, state.Players[1].Units.TroopsInGarrison);
        Assert.Equal(troops - count, state.Players[1].Units.TroopsInConflict);
        Assert.Equal(before[0], state.Players[0].Units);
        Assert.Equal(before[2], state.Players[2].Units);
        Assert.Equal(troops == count ? 0 : (troops - count) * 2 + 5, state.Players[1].CombatMarker);
        Assert.Equal(troops == count ? 2 : 1, engine.DecisionOwnerId);
        Assert.Equal(troops == count ? new[] { 0, 2 } : new[] { 0, 1, 2 }, engine.Observe(0).Game.Window!.EligibleSeats);
        Assert.Empty(engine.Observe(0).Game.Window!.ConsecutivePasses);
        Assert.Empty(state.Shared.IntrigueResolving.Cards);
        Assert.Single(state.Shared.IntrigueDiscard.Cards);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void SoleParticipantRetreatClosesAtUnsupportedOutcomeBoundary()
    {
        GameState Create()
        {
            var result = CombatState(deployed: [0, 1, 0]);
            Give(result, "master-tactician", 1);
            return result;
        }
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, Play(state, "master-tactician", 1));
        Resolve<ChoiceEffect>(engine);
        Apply(engine, new ChooseOptionAction(1, 1));
        Resolve<RetreatEffect>(engine);
        Apply(engine, new ChooseOptionAction(1, 1));
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.Equal(0, state.Players[1].CombatMarker);
        var outcome = engine.CaptureTrustedSnapshot().Data.CombatOutcome!;
        Assert.Null(outcome.WinnerId);
        Assert.Empty(outcome.Placements);
        Assert.Equal([0, 1, 2], outcome.NoRewardPlayerIds);
        Assert.Single(state.Shared.IntrigueDiscard.Cards);
        Assert.Equal("lifecycle-support", Assert.Single(engine.SupportDiagnostics(0)).Code);
        Assert.False(state.IsCompleted);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void ClosedCombatFreezesCalculatedStrengthAfterLastIntrigueAndReplaysBeforeRewards()
    {
        GameState Create()
        {
            var result = CombatState(deployed: [1, 1, 0]);
            Give(result, "ambush", 1);
            return result;
        }
        var state = Create();
        var rules = CombatRules(state);
        var engine = new TurnEngine(state, rules);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
        Apply(engine, new PassPriorityAction(0));
        Replay(engine, Create, rules);
        Apply(engine, Play(state, "ambush", 1));
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(1));
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Assert.Equal(DecisionWindowKind.PostWinRewards, engine.Observe(0).Game.Window!.Kind);
        Apply(engine, new PassPriorityAction(1));
        var snapshot = engine.CaptureTrustedSnapshot();
        var outcome = snapshot.Data.CombatOutcome!;
        Assert.Equal(1, outcome.WinnerId);
        Assert.Equal([1], outcome.Placements[0].PlayerIds);
        Assert.Equal([0], outcome.Placements[1].PlayerIds);
        Assert.Equal([2], outcome.NoRewardPlayerIds);
        Assert.Null(engine.DecisionOwnerId);
        Assert.Equal("lifecycle-support", Assert.Single(engine.SupportDiagnostics(0)).Code);
        Replay(engine, Create, rules);
        var restored = JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options)!;
        Assert.Equal(1, restored.CombatOutcome!.WinnerId);
    }

    [Fact]
    public void ZeroStrengthTroopsRemainEligibleAndSwordChoiceFiltersAtDerivedStrengthBound()
    {
        var state = CombatState();
        Give(state, "master-tactician", 0);
        state.Players[0].Temporary = new(0, int.MaxValue - 3, false);
        var engine = new TurnEngine(state, CombatRules(state));
        Assert.Equal(int.MaxValue - 1, state.Players[0].CombatMarker);
        Apply(engine, Play(state, "master-tactician", 0));
        Resolve<ChoiceEffect>(engine);
        Assert.Equal(new ChooseOptionAction(0, 1), Assert.Single(engine.LegalActions(0)));
        Reject(engine, new ChooseOptionAction(0, 0), StepStatus.ContentError);
        Apply(engine, new ChooseOptionAction(0, 1));
        Resolve<RetreatEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(0, engine.DecisionOwnerId);
        state = CombatState();
        engine = new TurnEngine(state, CombatRules(state) with { Strength = new(TroopStrength: 0, SwordStrength: 0) });
        Assert.All(state.Players, x => Assert.Equal(0, x.CombatMarker));
        Assert.Equal(new[] { 0, 1, 2 }, engine.Observe(0).Game.Window!.EligibleSeats);
    }

    [Theory]
    [InlineData(1, 0, 7)]
    [InlineData(2, 1, 0)]
    public void SyntheticForeignDecisionUsesRecipientTroopsAndRestoresPriorityWithFreshStrength(int count, int nextOwner, int strength)
    {
        GameState Create()
        {
            var result = CombatState(deployed: [2, 1, 1]);
            GiveSynthetic(result, 0);
            return result;
        }
        var state = Create();
        var card = state.Players[0].Intrigue.Cards.Single();
        var rules = SyntheticRules(state, card.Definition,
            [new SequenceEffect([new GainEffect(ResourceKind.Swords, 2),
                new ForPlayerEffect(0, [new RetreatEffect(3)]) { DecisionOwnerId = 2 },
                new OptionalPaymentEffect(new(), []),
                new GainEffect(ResourceKind.Swords, 3)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlayIntrigueAction(0, Card(card)));
        Resolve<SequenceEffect>(engine);
        Assert.Equal(6, state.Players[0].CombatMarker);
        Assert.Equal(2, engine.DecisionOwnerId);
        Replay(engine, Create, rules);
        Resolve<RetreatEffect>(engine);
        var context = engine.CaptureTrustedSnapshot().Data.Decisions.Single().Context;
        Assert.Equal(0, context.InitiatorId);
        Assert.Equal(2, context.DecisionOwnerId);
        Assert.Equal(0, context.RecipientId);
        Assert.Equal(new[] { 0, 1, 2 }, engine.LegalActions(2).OfType<ChooseOptionAction>().Select(x => x.Option));
        Assert.Null(engine.Observe(0).Decision);
        Reject(engine, new PassPriorityAction(0), StepStatus.Illegal);
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(2, count));
        Assert.Equal(count == 2 ? 0 : 4, state.Players[0].CombatMarker);
        Assert.Equal(2 - count, state.Players[0].Units.TroopsInConflict);
        Assert.Equal(1, state.Players[2].Units.TroopsInConflict);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.True(engine.Observe(1).Game.Window!.IsSuspended);
        Assert.Equal(new[] { 0, 1, 2 }, engine.Observe(1).Game.Window!.EligibleSeats);
        Assert.Single(state.Shared.IntrigueResolving.Cards);
        Reject(engine, new PassPriorityAction(0), StepStatus.Illegal);
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(strength, state.Players[0].CombatMarker);
        Assert.Equal(nextOwner, engine.DecisionOwnerId);
        Assert.False(engine.Observe(1).Game.Window!.IsSuspended);
        Assert.Single(state.Shared.IntrigueDiscard.Cards);
        Replay(engine, Create, rules);
    }

    [Theory]
    [InlineData("unsupported", StepStatus.Unsupported)]
    [InlineData("overflow", StepStatus.ContentError)]
    [InlineData("timing", StepStatus.Illegal)]
    [InlineData("unaffordable", StepStatus.Illegal)]
    [InlineData("postwin", StepStatus.Illegal)]
    public void UnsupportedOrUnsafePlayPreservesResourcesZonesRandomFramesWindowAndHistory(string problem, StepStatus status)
    {
        var state = CombatState();
        GiveSynthetic(state, 0);
        var card = state.Players[0].Intrigue.Cards.Single();
        var effects = problem switch
        {
            "unsupported" => ImmutableArray.Create<Effect>(new GainEffect(ResourceKind.Swords, 1), new UnsupportedEffect("Combat deployment/re-entry is unsupported")),
            "overflow" => [new GainEffect(ResourceKind.Swords, int.MaxValue)],
            _ => [],
        };
        var rules = SyntheticRules(state, card.Definition, effects);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x with
        {
            Timing = problem == "timing" ? IntrigueTiming.Endgame : problem == "postwin" ? IntrigueTiming.PostWin : IntrigueTiming.Combat,
            Cost = problem == "unaffordable" ? new(Water: 2) : new(Water: 1),
        })]
            }
        };
        var engine = new TurnEngine(state, rules);
        Reject(engine, new PlayIntrigueAction(0, Card(card)), status);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is PlayIntrigueAction);
    }

    [Fact]
    public void RetreatIsNotEnabledForOtherTimingOrOrdinaryPlayerTurnBoxes()
    {
        var state = CombatState();
        state.Phase = GamePhase.Endgame;
        GiveSynthetic(state, 0);
        var card = state.Players[0].Intrigue.Cards.Single();
        var rules = SyntheticRules(state, card.Definition, [new RetreatEffect(3)]);
        rules = rules with { Lifecycle = rules.Lifecycle! with { Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x with { Timing = IntrigueTiming.Endgame })] } };
        Reject(new TurnEngine(state, rules), new PlayIntrigueAction(0, Card(card)), StepStatus.Unsupported);
        state = LifecycleFixture.CreateState();
        rules = LifecycleFixture.Rules(state);
        rules = CardEffects(rules, state.Players[0].Deck.Cards[0].Definition, agent: [new RetreatEffect(3)]);
        Reject(new TurnEngine(state, rules), new StartRoundAction(), StepStatus.Unsupported);
    }

    private static TurnRuleSet CombatRules(GameState state) => Rules(state) with
    { Lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules() with { Factions = [], Conflicts = EmptyConflictRewards(state) } };

    private static TurnRuleSet SyntheticRules(GameState state, ContentId id, ImmutableArray<Effect> effects) =>
        Rules(state) with
        {
            Lifecycle = new("synthetic-combat-tests-v1", [new(id, IntrigueTiming.Combat, new(), effects,
            [new("Synthetic Combat fixture, not official content", 1, "Invented effects")])], Conflicts: EmptyConflictRewards(state))
        };

    private static GameState CombatState(int players = 3, int first = 0, int[]? deployed = null)
    {
        var replaced = OriginalImperiumFixture.Catalog.Definitions.Where(x => x.Category == ContentCategory.IntrigueCard).Take(2).ToArray();
        var catalog = new ContentCatalog(OriginalImperiumFixture.Catalog.Definitions.Select(x =>
            x.Id == replaced[0].Id ? x with { Id = Face("ambush"), Name = "Ambush", Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 7, "Illustrated face; one synthetic copy, not official multiplicity")] } :
            x.Id == replaced[1].Id ? x with { Id = Face("master-tactician"), Name = "Master Tactician", Sources = [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 12, "Illustrated face; one synthetic copy, not official multiplicity")] } : x));
        var configuration = OriginalImperiumFixture.Configuration(players);
        var composer = new RulesetComposer(catalog);
        var state = new GameInitializer(composer).InitializeFixture(configuration, composer.Compose(configuration).Definition).State!;
        state.Phase = GamePhase.Combat;
        state.Round = 1;
        state.Conflict.Deck.TryDrawTo(state.Conflict.Revealed);
        state.FirstPlayerId = first;
        foreach (var player in state.Players)
        {
            var troops = deployed?[player.Id] ?? 1;
            player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply - troops, TroopsInConflict = troops };
        }
        return state;
    }

    private static ImmutableArray<ConflictRule> EmptyConflictRewards(GameState state) =>
        [.. LifecycleFixture.LifecycleRules(state).Conflicts.Select(x => x with
        { Rewards = [new(1, []), new(2, []), new(3, [])] })];

    private static ContentId Face(string name) => new("imperium:intrigue:" + name + ":v1");
    private static void GiveSynthetic(GameState state, int player) =>
        state.Shared.IntrigueDeck.MoveTo(state.Shared.IntrigueDeck.Cards.First(x => x.Definition.Value.StartsWith("fixture:", StringComparison.Ordinal)).Id,
            state.Players[player].Intrigue);
    private static void Give(GameState state, string name, int player) =>
        state.Shared.IntrigueDeck.MoveTo(state.Shared.IntrigueDeck.Cards.Single(x => x.Definition == Face(name)).Id, state.Players[player].Intrigue);
    private static PlayIntrigueAction Play(GameState state, string name, int player) => new(player,
        Card(state.Players[player].Intrigue.Cards.Single(x => x.Definition == Face(name))));

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new ResolveEffectAction(owner, engine.Observe(owner).PendingEffects.Single(x => x.Kind == typeof(T).Name).Id));
    }

    private static void Reject(TurnEngine engine, GameAction action, StepStatus expected)
    {
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        var replay = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        Assert.Equal(expected, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(replay, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
    }

    private static void Replay(TurnEngine engine, Func<GameState> factory, TurnRuleSet rules)
    {
        var json = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        var result = TurnEngine.Replay(JsonSerializer.Deserialize<TurnReplay>(json, ConfigurationJson.Options)!, factory, rules);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().CanonicalJson, result.Engine!.CaptureTrustedSnapshot().CanonicalJson);
        for (var seat = 0; seat < engine.CaptureTrustedSnapshot().Data.Game.Players.Length; seat++)
            Assert.Equal(engine.LegalActions(seat), result.Engine.LegalActions(seat));
    }
}
