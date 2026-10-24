using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

/// <summary>Synthetic priority and ownership fixtures; no official Intrigue text is inferred.</summary>
public sealed class PhaseDecisionTests
{
    [Theory]
    [InlineData(GamePhase.Combat, IntrigueTiming.Combat, DecisionWindowKind.Combat)]
    [InlineData(GamePhase.Endgame, IntrigueTiming.Endgame, DecisionWindowKind.Endgame)]
    public void RealIntrigueSuspendsWindowForForeignChildAndRestoresPriorityWithoutTurnScheduling(
        GamePhase phase, IntrigueTiming timing, DecisionWindowKind kind)
    {
        GameState Create() => WindowState(phase);
        var state = Create();
        var card = state.Players[2].Intrigue.Cards.Single();
        var rules = WindowRules(state, timing,
            [new SequenceEffect([
                new ForPlayerEffect(0, [new OptionalPaymentEffect(new(Solari: 1), [new GainEffect(ResourceKind.Water, 2)])])
                { DecisionOwnerId = 1, PayerId = 2 },
                new GainEffect(ResourceKind.Spice, 3)])]);
        var engine = new TurnEngine(state, rules);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(2, engine.DecisionOwnerId);
        var publicBefore = engine.Observe(0);
        var windowBefore = publicBefore.Game.Window!;
        Assert.Equal(kind, windowBefore.Kind);
        Assert.Equal(new[] { 2, 0, 1 }, windowBefore.EligibleSeats);
        Assert.False(windowBefore.IsSuspended);
        Assert.Empty(publicBefore.Game.Intrigue);
        Assert.Empty(engine.Observe(0).LegalActions);
        Assert.Contains(new PlayIntrigueAction(2, Card(card)), engine.LegalActions(2));
        Assert.Empty(engine.SupportDiagnostics(2));
        RejectOrdinaryActions(engine);
        Replay(engine, Create, rules);
        Apply(engine, new PlayIntrigueAction(2, Card(card)));
        Assert.Single(state.Shared.IntrigueResolving.Cards);
        Assert.Empty(state.Shared.IntrigueDiscard.Cards);
        Assert.Empty(state.Players[2].Intrigue.Cards);
        Assert.Equal(1, engine.Observe(0).Game.Window!.OpportunityPlays);
        Assert.True(engine.Observe(0).Game.Window!.IsSuspended);
        var suspended = engine.CaptureTrustedSnapshot().Data;
        Assert.Equal(suspended.Frames[0].Id, suspended.Window!.SuspendedFrameId);
        Assert.Equal(suspended.Window.Id, suspended.Frames[0].Continuation.WindowId);
        Assert.Equal(2, suspended.Frames[0].Continuation.PlayerId);
        Assert.Equal(card.Id, suspended.Window.ResolvingCardId);
        Assert.DoesNotContain("resolvingCardId", JsonSerializer.Serialize(engine.Observe(0), ConfigurationJson.Options));
        Assert.DoesNotContain("suspendedFrameId", JsonSerializer.Serialize(engine.Observe(0), ConfigurationJson.Options));
        Assert.Equal(0, windowBefore.OpportunityPlays); // Detached prior view.
        Assert.False(windowBefore.IsSuspended);
        Assert.Contains(engine.Observe(0).Game.PublicZones, x => x.Name == "Resolving Intrigue" && x.Count == 1);
        Reject(engine, new PassPriorityAction(2));
        Replay(engine, Create, rules);
        Resolve<SequenceEffect>(engine);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Equal(2, engine.Observe(0).Game.Window!.PriorityOwnerId);
        Reject(engine, new PassPriorityAction(2));
        Assert.Empty(engine.LegalActions(2));
        Assert.Empty(engine.Observe(0).PendingEffects);
        Replay(engine, Create, rules);
        Resolve<OptionalPaymentEffect>(engine);
        var context = engine.CaptureTrustedSnapshot().Data.Decisions.Single().Context;
        Assert.Equal(2, context.InitiatorId);
        Assert.Equal(1, context.DecisionOwnerId);
        Assert.Equal(2, context.PayerId);
        Assert.Equal(0, context.RecipientId);
        Assert.Equal(card.Id, context.SourceCard);
        Assert.Equal(card.Definition, context.SourceDefinition);
        Assert.Null(context.Space);
        Assert.Null(engine.Observe(0).Decision);
        Assert.Null(engine.Observe(2).Decision);
        Assert.NotNull(engine.Observe(1).Decision);
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(1, 1));
        Assert.Equal(0, state.Players[2].Resources.Solari);
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Equal(3, state.Players[2].Resources.Spice);
        Assert.Empty(state.Shared.IntrigueResolving.Cards);
        Assert.Single(state.Shared.IntrigueDiscard.Cards);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Frames);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(phase, engine.Phase);
        Assert.False(engine.Observe(0).Game.Window!.IsSuspended);
        Assert.Equal(windowBefore.Id, engine.Observe(0).Game.Window!.Id);
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(2));
        var afterPlayedOpportunity = engine.CaptureTrustedSnapshot().Data.Window!;
        Assert.Empty(afterPlayedOpportunity.ConsecutivePasses);
        Assert.Equal(0, afterPlayedOpportunity.OpportunityPlays);
        Assert.Equal(0, engine.DecisionOwnerId);
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Equal(new[] { 0, 1 }, engine.CaptureTrustedSnapshot().Data.Window!.ConsecutivePasses);
        Apply(engine, new PassPriorityAction(2));
        Assert.Equal(phase, engine.Phase);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.False(state.IsCompleted);
        Assert.Equal("lifecycle-support", Assert.Single(engine.SupportDiagnostics(0)).Code);
        Replay(engine, Create, rules);
    }

    [Theory]
    [InlineData(GamePhase.Combat, IntrigueTiming.Endgame)]
    [InlineData(GamePhase.Endgame, IntrigueTiming.Plot)]
    [InlineData(GamePhase.Combat, IntrigueTiming.PostWin)]
    [InlineData(GamePhase.Endgame, IntrigueTiming.PostWin)]
    public void UnsupportedAndWrongTimingIntriguesPreserveEveryTrustedField(GamePhase phase, IntrigueTiming timing)
    {
        var state = WindowState(phase);
        var own = state.Players[2].Intrigue.Cards.Single();
        var rules = WindowRules(state, timing, [new UnsupportedEffect("synthetic unsupported box")]);
        var engine = new TurnEngine(state, rules);
        Reject(engine, new PlayIntrigueAction(2, Card(own)));
        var wrongOwner = state.Shared.IntrigueDeck.Cards[0];
        Reject(engine, new PlayIntrigueAction(2, Card(wrongOwner)));
        state = WindowState(phase);
        rules = WindowRules(state, phase == GamePhase.Combat ? IntrigueTiming.Combat : IntrigueTiming.Endgame,
            [new UnsupportedEffect("synthetic unsupported box")]);
        engine = new TurnEngine(state, rules);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, engine.Step(new PlayIntrigueAction(2, Card(own))).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.DoesNotContain(engine.LegalActions(2), x => x is PlayIntrigueAction);
        Assert.Contains(engine.SupportDiagnostics(2), x => x.Status == StepStatus.Unsupported);
    }

    [Fact]
    public void LastRevealAutomaticallyOpensCombatWindowWithNoCurrentPlayer()
    {
        GameState Create()
        {
            var result = WindowState(GamePhase.Combat, lifecycleInventory: true);
            result.Phase = GamePhase.BeforeRoundStart;
            result.Round = 0;
            return result;
        }
        var state = Create();
        var rules = LifecycleFixture.Rules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        FinishPlayerTurns(engine);
        Assert.Equal(GamePhase.Combat, engine.Phase);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.NotNull(engine.Observe(0).Game.Window);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void LastRevealWithNoCombatParticipantsFreezesEmptyOutcomeAndReplays()
    {
        GameState Create()
        {
            var result = WindowState(GamePhase.Combat, lifecycleInventory: true);
            foreach (var player in result.Players)
                player.Units = player.Units with
                {
                    TroopsInSupply = player.Units.TroopsInSupply + player.Units.TroopsInConflict,
                    TroopsInConflict = 0,
                };
            result.Phase = GamePhase.BeforeRoundStart;
            result.Round = 0;
            return result;
        }
        var state = Create();
        var rules = LifecycleFixture.Rules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        FinishPlayerTurns(engine);
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.Window);
        var outcome = engine.CaptureTrustedSnapshot().Data.CombatOutcome!;
        Assert.Empty(outcome.Placements);
        Assert.Equal([0, 1, 2], outcome.NoRewardPlayerIds);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void ZeroParticipantsClosesAutomaticallyAndOneParticipantMustPassExplicitly()
    {
        var state = WindowState(GamePhase.Combat);
        foreach (var player in state.Players)
            player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply + 1, TroopsInConflict = 0 };
        var rules = WindowRules(state, IntrigueTiming.Combat, []);
        var engine = new TurnEngine(state, rules);
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.Window);
        Assert.Equal(1, engine.CaptureTrustedSnapshot().Data.NextWindowId);
        state = WindowState(GamePhase.Combat);
        foreach (var player in state.Players.Where(x => x.Id != 2))
            player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply + 1, TroopsInConflict = 0 };
        engine = new TurnEngine(state, rules);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Contains(new PassPriorityAction(2), engine.LegalActions(2));
        Apply(engine, new PassPriorityAction(2));
        if (engine.Observe(0).Game.Window?.Kind == DecisionWindowKind.PostWinRewards)
            Apply(engine, new PassPriorityAction(engine.DecisionOwnerId!.Value));
        Assert.Null(engine.DecisionOwnerId);
        Assert.Equal(GamePhase.Combat, engine.Phase);
        Assert.False(state.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinishingPlayedOpportunityRequiresFreshNoPlayPassesIncludingTheActor(bool soleParticipant)
    {
        GameState Create()
        {
            var result = WindowState(GamePhase.Combat);
            result.FirstPlayerId = soleParticipant ? 2 : 1;
            if (soleParticipant)
                foreach (var player in result.Players.Where(x => x.Id != 2))
                    player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply + 1, TroopsInConflict = 0 };
            return result;
        }
        var state = Create();
        var rules = WindowRules(state, IntrigueTiming.Combat, []);
        var engine = new TurnEngine(state, rules);
        if (!soleParticipant)
        {
            Apply(engine, new PassPriorityAction(1));
            Assert.Equal(new[] { 1 }, engine.CaptureTrustedSnapshot().Data.Window!.ConsecutivePasses);
        }
        Apply(engine, new PlayIntrigueAction(2, Card(state.Players[2].Intrigue.Cards[0])));
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Window!.ConsecutivePasses);
        Assert.Equal(1, engine.CaptureTrustedSnapshot().Data.Window!.OpportunityPlays);
        Replay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(2));
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Window!.ConsecutivePasses);
        Assert.Equal(0, engine.CaptureTrustedSnapshot().Data.Window!.OpportunityPlays);
        Replay(engine, Create, rules);
        if (!soleParticipant)
        {
            Apply(engine, new PassPriorityAction(0));
            Apply(engine, new PassPriorityAction(1));
        }
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Null(engine.CurrentPlayerId);
        Apply(engine, new PassPriorityAction(2));
        if (engine.Observe(0).Game.Window?.Kind == DecisionWindowKind.PostWinRewards)
            Apply(engine, new PassPriorityAction(engine.DecisionOwnerId!.Value));
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.Equal(GamePhase.Combat, engine.Phase);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void HiddenIntrigueSwapCannotChangeOtherSeatsWindowsSelectorsOrSupportDiagnostics()
    {
        var first = WindowState(GamePhase.Endgame);
        var second = WindowState(GamePhase.Endgame);
        second.Players[1].Intrigue.MoveTo(second.Players[1].Intrigue.Cards[0].Id, second.Shared.IntrigueDeck);
        second.Shared.IntrigueDeck.TryDrawTo(second.Players[1].Intrigue);
        Assert.NotEqual(first.Players[1].Intrigue.Cards[0].Definition, second.Players[1].Intrigue.Cards[0].Definition);
        var rules = WindowRules(first, IntrigueTiming.Endgame, []);
        var left = new TurnEngine(first, rules);
        var right = new TurnEngine(second, rules);
        foreach (var seat in new[] { 0, 2 })
        {
            Assert.Equal(JsonSerializer.Serialize(left.Observe(seat), ConfigurationJson.Options),
                JsonSerializer.Serialize(right.Observe(seat), ConfigurationJson.Options));
            Assert.Equal(left.SupportDiagnostics(seat), right.SupportDiagnostics(seat));
        }
        var before = left.CaptureTrustedSnapshot().Hash;
        _ = left.Observe(2);
        _ = left.SupportDiagnostics(2);
        _ = left.LegalActions(2);
        Assert.Equal(before, left.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void WindowOwnerAndPhaseSynchronizationAreChecked()
    {
        var state = WindowState(GamePhase.Endgame);
        var engine = new TurnEngine(state, WindowRules(state, IntrigueTiming.Endgame, []));
        state.DecisionOwnerId = 0;
        Assert.Throws<InvalidOperationException>(() => engine.LegalActions(0));
        state.DecisionOwnerId = 2;
        state.CurrentPlayerId = 2;
        Assert.Throws<InvalidOperationException>(() => engine.Step(new PassPriorityAction(2)));
        state.CurrentPlayerId = null;
        state.Phase = GamePhase.Combat;
        Assert.Throws<InvalidOperationException>(() => engine.Observe(2));
    }

    [Fact]
    public void WindowJsonRejectsUnknownMembersAndEnums()
    {
        var state = WindowState(GamePhase.Endgame);
        var engine = new TurnEngine(state, WindowRules(state, IntrigueTiming.Endgame, []));
        var json = JsonSerializer.Serialize(engine.CaptureTrustedSnapshot().Data.Window, ConfigurationJson.Options);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DecisionWindowState>(
            json.Replace("\"Endgame\"", "\"FutureWindow\"", StringComparison.Ordinal), ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DecisionWindowState>(
            json[..^1] + ",\"future\":true}", ConfigurationJson.Options));
    }

    [Fact]
    public void ForeignControlRewardCanFundForeignOptionalWithoutOwningRootOrder()
    {
        GameState Create()
        {
            var result = CreateState();
            result.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).ControllerId = 2;
            return result;
        }
        var state = Create();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new OptionalPaymentEffect(new(Solari: 1), [new GainEffect(ResourceKind.Water, 2)], 2)]);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract")
            ? x with { Effects = [], ControlReward = ResourceKind.Solari } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        var root = engine.CaptureTrustedSnapshot().Data.Frames.Single();
        var bonus = root.Remaining.Single(x => x.Effect is GainEffect);
        Assert.Equal(0, root.ChooserId);
        Assert.Equal(0, bonus.Context.InitiatorId);
        Assert.Equal(0, bonus.Context.DecisionOwnerId);
        Assert.Equal(0, bonus.Context.PayerId);
        Assert.Equal(2, bonus.Context.RecipientId);
        Assert.Equal(EffectProvenance.Board, bonus.Context.Provenance);
        Assert.Null(bonus.Context.SourceCard);
        Assert.Equal(Space("secure-contract"), bonus.Context.Space);
        Assert.Contains(new ResolveEffectAction(0, bonus.Id), engine.LegalActions(0));
        Assert.Empty(engine.Observe(2).PendingEffects);
        Reject(engine, new ResolveEffectAction(2, bonus.Id));
        Apply(engine, new ResolveEffectAction(0, bonus.Id));
        Assert.Equal(1, state.Players[2].Resources.Solari);
        Apply(engine, engine.LegalActions(0).OfType<ResolveEffectAction>().Single());
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Null(engine.Observe(0).Decision);
        Assert.Empty(engine.LegalActions(0));
        Assert.Contains(new ChooseOptionAction(2, 1), engine.LegalActions(2));
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(0, state.Players[2].Resources.Solari);
        Assert.Equal(3, state.Players[2].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void PreviewStopsInNonterminalCombatWithExplicitLifecycleDiagnostic()
    {
        var state = CreateState();
        var engine = new TurnEngine(state, Rules(state));
        Apply(engine, new StartRoundAction());
        FinishPlayerTurns(engine);
        Assert.Equal(GamePhase.Combat, engine.Phase);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Null(engine.DecisionOwnerId);
        Assert.False(state.IsCompleted);
        Assert.Null(engine.Observe(0).Game.Window);
        foreach (var seat in Enumerable.Range(0, 3))
        {
            Assert.Empty(engine.LegalActions(seat));
            Assert.Equal("lifecycle-support", Assert.Single(engine.SupportDiagnostics(seat)).Code);
        }
        RejectOrdinaryActions(engine);
    }

    [Fact]
    public void PriorityActionTagsRoundTripAndStrictlyRejectUnknownPayloads()
    {
        ImmutableArray<GameAction> actions = [new PlayIntrigueAction(2, new(new("fixture:intrigue"))), new PassPriorityAction(2)];
        const string expected = "[{\"$action\":\"play-intrigue\",\"card\":{\"definition\":{\"value\":\"fixture:intrigue\"},\"copy\":0},\"playerId\":2},{\"$action\":\"pass-priority\",\"playerId\":2}]";
        Assert.Equal(expected, JsonSerializer.Serialize(actions, ConfigurationJson.Options));
        Assert.Equal(actions, JsonSerializer.Deserialize<ImmutableArray<GameAction>>(expected, ConfigurationJson.Options));
        foreach (var json in new[]
        {
            "{\"$action\":\"play-intrigue\",\"card\":{\"definition\":{\"value\":\"fixture:intrigue\"},\"secret\":true},\"playerId\":2}",
            "{\"$action\":\"pass-priority\",\"playerId\":2,\"plays\":1}",
            "{\"$action\":\"future-priority\",\"playerId\":2}",
        }) Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameAction>(json, ConfigurationJson.Options));
    }

    private static void RejectOrdinaryActions(TurnEngine engine)
    {
        var owner = engine.DecisionOwnerId ?? 0;
        foreach (var action in new GameAction[]
        {
            new PlaceAgentAction(owner, new(new("fixture:missing")), Space("secure-contract")),
            new RevealAction(owner), new AcquireCardAction(owner, new(new("fixture:missing")), new("imperium:row")),
            new EndRevealAction(owner), new DeployAction(owner, 0),
        }) Reject(engine, action);
    }

    private static void Reject(TurnEngine engine, GameAction action)
    {
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    private static void Replay(TurnEngine engine, Func<GameState> create, TurnRuleSet rules)
    {
        var replay = TurnEngine.Replay(engine.CaptureReplay(), create, rules);
        Assert.True(replay.IsSuccess, string.Join("; ", replay.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, replay.Engine!.CaptureTrustedSnapshot().Hash);
        foreach (var seat in Enumerable.Range(0, 3)) Assert.Equal(engine.LegalActions(seat), replay.Engine.LegalActions(seat));
        var snapshot = engine.CaptureTrustedSnapshot();
        var copy = JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options)!;
        Assert.Equal(snapshot.CanonicalJson, JsonSerializer.Serialize(copy, ConfigurationJson.Options));
    }

    private static GameState WindowState(GamePhase phase, bool lifecycleInventory = false)
    {
        var state = lifecycleInventory ? LifecycleFixture.CreateState() : CreateState();
        state.Phase = phase;
        state.Round = 1;
        if (phase == GamePhase.Combat && !lifecycleInventory) state.Conflict.Deck.TryDrawTo(state.Conflict.Revealed);
        state.FirstPlayerId = 2;
        foreach (var player in state.Players)
            player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply - 1, TroopsInConflict = 1 };
        state.Players[2].Resources = state.Players[2].Resources with { Solari = 1 };
        state.Shared.IntrigueDeck.TryDrawTo(state.Players[2].Intrigue);
        // Give another seat a hidden card that must never enter the current player's selectors or diagnostics.
        state.Shared.IntrigueDeck.TryDrawTo(state.Players[2].Intrigue);
        state.Players[2].Intrigue.MoveTo(state.Players[2].Intrigue.Cards[1].Id, state.Players[1].Intrigue);
        return state;
    }

    private static TurnRuleSet WindowRules(GameState state, IntrigueTiming timing, ImmutableArray<Effect> effects) =>
        Rules(state) with
        {
            Lifecycle = new("synthetic-window-tests-v1", [new(state.Players[2].Intrigue.Cards[0].Definition, timing,
                new(), effects, [new("Synthetic window fixture, not official content", 1, "Invented Intrigue effects")])],
                Conflicts: [.. LifecycleFixture.LifecycleRules(state).Conflicts.Select(x => x with
                    { Rewards = [new(1, []), new(2, []), new(3, [])] })]),
        };

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new ResolveEffectAction(owner, engine.Observe(owner).PendingEffects.Single(x => x.Kind == typeof(T).Name).Id));
    }
}
