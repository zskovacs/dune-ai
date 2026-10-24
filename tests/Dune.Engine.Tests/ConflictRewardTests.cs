using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Rulesets;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class ConflictRewardTests
{
    [Theory]
    [MemberData(nameof(CombatOutcomeTests.RewardCases), MemberType = typeof(CombatOutcomeTests))]
    public void EveryRankingTableAwardsItsActualTierExactlyOnce(int[] strengths, int?[] tiers, int? winner)
    {
        var state = CombatState(strengths);
        var rules = LifecycleFixture.Rules(state);
        var engine = new TurnEngine(state, rules);
        var before = state.Players.Select(x => x.Resources.Solari).ToArray();
        CloseCombat(engine);
        Assert.Equal(winner, engine.CaptureTrustedSnapshot().Data.CombatOutcome!.WinnerId);
        FinishRewards(engine);
        foreach (var player in state.Players)
            Assert.Equal(before[player.Id] + (tiers[player.Id] is { } tier ? 4 - tier : 0), player.Resources.Solari);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Frames);
        Assert.Null(engine.DecisionOwnerId);
        Assert.False(state.IsCompleted);
        Assert.Equal(GamePhase.Combat, state.Phase); // Cleanup belongs to Task 9.
        var after = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.Equal(StepStatus.Illegal, engine.Step(new PassPriorityAction(0)).Status);
        Assert.Equal(after, engine.CaptureTrustedSnapshot().CanonicalJson);
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("overflow")]
    [InlineData("agent-overflow")]
    [InlineData("stock")]
    [InlineData("intrigue")]
    [InlineData("tie-influence")]
    [InlineData("tie-mentat")]
    public void FinalPassPreflightsAllRecipientsBeforeAnyWindowOrGrantMutation(string problem)
    {
        var state = CombatState(problem.StartsWith("tie-", StringComparison.Ordinal) ? [8, 8, 4] : [8, 6, 4]);
        if (problem == "overflow") state.Players[1].Resources = state.Players[1].Resources with { Solari = 1 };
        if (problem == "agent-overflow") state.Players[1].Units = state.Players[1].Units with { AvailableAgents = int.MaxValue - 1 };
        var effect = problem switch
        {
            "unsupported" => (Effect)new UnsupportedEffect("Unknown synthetic reward"),
            "overflow" => new GainEffect(ResourceKind.Solari, int.MaxValue),
            "agent-overflow" => new SequenceEffect([new TakeMentatEffect(true), new SwordmasterEffect()]),
            "stock" => new AcquireEffect(new("imperium:reserve:arrakis-liaison:v1")),
            "intrigue" => new DrawIntrigueEffect(int.MaxValue),
            "tie-influence" => new InfluenceEffect(Faction.Emperor, 4),
            _ => new TakeMentatEffect(true),
        };
        if (problem == "stock")
            foreach (var zone in state.Shared.Reserves)
                foreach (var card in zone.Cards.ToArray()) zone.MoveTo(card.Id, state.Shared.Trashed);
        var rules = Rewards(LifecycleFixture.Rules(state), 2, [effect]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Reject(engine, new PassPriorityAction(2));
        Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.ConflictRewards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WinnerCanFundPostWinPaymentWithRewardOrEarlierCardAndResumeExactRoot(bool cardFunds)
    {
        GameState Create()
        {
            var result = PrepareCombat(PostWinTimingState(), [8, 6, 4]);
            result.Players[0].Resources = result.Players[0].Resources with { Spice = 0 };
            GiveCards(result, 0, 2);
            return result;
        }
        var state = Create();
        var cards = state.Players[0].Intrigue.Cards.ToArray();
        Assert.Equal("fixture:post-win-payment", cards[0].Definition.Value);
        Assert.Equal("fixture:post-win-spice", cards[1].Definition.Value);
        var rules = Rewards(LifecycleFixture.Rules(state), 1,
            cardFunds ? [new GainEffect(ResourceKind.Water, 1)] : [new GainEffect(ResourceKind.Spice, 2), new GainEffect(ResourceKind.Water, 1)]);
        rules = Intrigue(rules, cards[0].Definition, "fixture:post-win-payment", new(Spice: 2), [new GainEffect(ResourceKind.Solari, 4)]);
        rules = Intrigue(rules, cards[1].Definition, "fixture:post-win-spice", new(), [new GainEffect(ResourceKind.Spice, 2)]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Assert.Equal(DecisionWindowKind.PostWinRewards, engine.Observe(0).Game.Window!.Kind);
        Reject(engine, new PlayIntrigueAction(0, Card(cards[0])));
        Reject(engine, new PassPriorityAction(0));
        var rootId = engine.CaptureTrustedSnapshot().Data.Frames.Single().Id;
        if (cardFunds) Apply(engine, new PlayIntrigueAction(0, Card(cards[1])));
        else Resolve<GainEffect>(engine, gain => gain.Resource == ResourceKind.Spice);
        CheckReplay(engine, Create, rules);
        Assert.Equal(2, state.Players[0].Resources.Spice);
        Apply(engine, new PlayIntrigueAction(0, Card(cards[0])));
        Assert.Equal(0, state.Players[0].Resources.Spice);
        Assert.Equal(rootId, engine.CaptureTrustedSnapshot().Data.Frames.Single().Id);
        Assert.False(engine.Observe(0).Game.Window!.IsSuspended);
        Assert.NotEmpty(engine.LegalActions(0).OfType<ResolveEffectAction>());
        CheckReplay(engine, Create, rules);
        FinishRewards(engine);
        Assert.Equal(new[] { 0 }, engine.CaptureTrustedSnapshot().Data.ConflictRewards!.ClosedPostWinOpportunities);
        Assert.Null(engine.Observe(0).Game.Window);
        CheckReplay(engine, Create, rules);
    }

    [Fact]
    public void UnfinishedPostWinChildBlocksRewardsCardsAndPassAndReservesLowerRecipients()
    {
        var state = CombatState([8, 6, 4]);
        GiveCards(state, 0, 1);
        var card = state.Players[0].Intrigue.Cards[0];
        var rules = Rewards(LifecycleFixture.Rules(state), 1, [new GainEffect(ResourceKind.Water, 1)]);
        rules = Rewards(rules, 2, [new DrawIntrigueEffect(state.Shared.IntrigueDeck.Count)]);
        rules = Intrigue(rules, card.Definition, "fixture:post-win-child", new(),
            [new OptionalPaymentEffect(new(), [new DrawIntrigueEffect(1)])]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        var root = engine.CaptureTrustedSnapshot().Data.Frames.Single().Remaining.Single();
        Apply(engine, new PlayIntrigueAction(0, Card(card)));
        Reject(engine, new ResolveEffectAction(0, root.Id));
        Reject(engine, new PassPriorityAction(0));
        Resolve<OptionalPaymentEffect>(engine);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is ChooseOptionAction { Option: 1 });
        Reject(engine, new ChooseOptionAction(0, 1));
        Apply(engine, new ChooseOptionAction(0, 0));
        FinishRewards(engine);
        Assert.Equal(0, state.Shared.IntrigueDeck.Count);
    }

    [Fact]
    public void GenericRewardsReuseNestedAcquireChoicePaymentForeignRolesAndConditionalRuntime()
    {
        GameState Create()
        {
            var result = CombatState([8, 6, 4]);
            var reserve = result.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition.Value == "imperium:reserve:spice-must-flow:v1"));
            reserve.MoveTo(reserve.Cards[0].Id, result.Players[0].Deck);
            reserve.MoveTo(reserve.Cards[0].Id, result.Players[0].Discard);
            return result;
        }
        var state = Create();
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(LifecycleFixture.Rules(state), market.Definition,
            acquire: [new ForPlayerEffect(1, [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])]) { DecisionOwnerId = 2 }]);
        rules = Rewards(rules, 1, [new SequenceEffect([
            new GainEffect(ResourceKind.Spice, 2), new DrawEffect(1), new DrawIntrigueEffect(1), new RecruitEffect(2),
            new InfluenceEffect(Faction.Emperor, 2), new TakeMentatEffect(true),
            new OptionalPaymentEffect(new(Spice: 2), [new GainEffect(ResourceKind.Solari, 3)]),
            new ChoiceEffect([new("Water", new(), [new GainEffect(ResourceKind.Water, 1)])]),
            new AcquireEffect(market.Definition),
            new ConditionalEffect(EndgameCondition.OwnAtLeastTwoSpiceMustFlow, [new GainEffect(ResourceKind.VictoryPoints, 1)])])]);
        var engine = new TurnEngine(state, rules);
        var supply = state.Players[0].Units.TroopsInSupply;
        var vp = state.Players[0].Resources.VictoryPoints;
        CloseCombat(engine);
        Resolve<SequenceEffect>(engine);
        CheckReplay(engine, Create, rules);
        Assert.Equal(0, state.Board.Mentat.OwnerId);
        Assert.Equal(2, state.Board.Mentat.ProtectedThroughRound);
        Apply(engine, new ChooseOptionAction(0, 1));
        Apply(engine, new ChooseOptionAction(0, 0));
        Resolve<ForPlayerEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        CheckReplay(engine, Create, rules);
        Resolve<OptionalPaymentEffect>(engine);
        Apply(engine, new ChooseOptionAction(2, 1));
        FinishRewards(engine);
        Assert.Equal(supply - 2, state.Players[0].Units.TroopsInSupply);
        Assert.Equal(5, state.Players[0].Units.TroopsInGarrison);
        Assert.Equal(vp + 2, state.Players[0].Resources.VictoryPoints);
        Assert.Single(state.Players[0].Hand.Cards);
        Assert.Single(state.Players[0].Intrigue.Cards);
        Assert.Contains(market.Id, state.Players[0].Discard.Cards.Select(x => x.Id));
        Assert.Equal(3, state.Players[1].Resources.Water);
        CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData("unrevealed")]
    [InlineData("undefined")]
    [InlineData("missing-tier")]
    public void MissingConflictOrRewardDefinitionCannotDisappearAtFinalPass(string problem)
    {
        var state = CombatState([8, 6, 4]);
        var rules = LifecycleFixture.Rules(state);
        if (problem == "unrevealed") state.Conflict.Revealed.MoveTo(state.Conflict.Revealed.Cards[0].Id, state.Conflict.Unused);
        if (problem == "undefined") rules = rules with { Lifecycle = rules.Lifecycle! with { Conflicts = [] } };
        if (problem == "missing-tier") rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            { Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with { Rewards = [new(1, []), new(3, [])] })] }
        };
        var engine = new TurnEngine(state, rules);
        if (problem == "missing-tier")
        {
            Reject(engine, new PassPriorityAction(0)); // Malformed tier shape is gated before a window can open.
            Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
            return;
        }
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Reject(engine, new PassPriorityAction(2));
        Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConditionalRewardLegalityDoesNotDependOnOpponentHiddenOwnedCounts(bool overflow)
    {
        (GameState State, TurnEngine Engine) Create(bool rivalOwns)
        {
            var state = CombatState([8, 6, 4]);
            var reserve = state.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition.Value == "imperium:reserve:spice-must-flow:v1"));
            reserve.MoveTo(reserve.Cards[0].Id, state.Players[0].Deck);
            // Swap hidden identities while keeping the owner's cards and every public zone count identical.
            var hidden = reserve.Cards[0];
            reserve.MoveTo(hidden.Id, state.Shared.ImperiumDeck);
            if (rivalOwns)
            {
                state.Players[1].Deck.MoveTo(state.Players[1].Deck.Cards[0].Id, state.Shared.ImperiumDeck);
                state.Shared.ImperiumDeck.MoveTo(hidden.Id, state.Players[1].Deck);
            }
            if (overflow) state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue };
            var rules = Rewards(LifecycleFixture.Rules(state), 1,
                [new ConditionalEffect(EndgameCondition.OwnMoreSpiceMustFlowThanEachOpponent, [new GainEffect(ResourceKind.VictoryPoints, 1)])]);
            var engine = new TurnEngine(state, rules);
            Apply(engine, new PassPriorityAction(0));
            Apply(engine, new PassPriorityAction(1));
            return (state, engine);
        }
        var first = Create(false);
        var second = Create(true);
        Assert.Equal(first.Engine.LegalActions(2), second.Engine.LegalActions(2));
        Assert.Equal(first.Engine.SupportDiagnostics(2), second.Engine.SupportDiagnostics(2));
        if (overflow)
        {
            Reject(first.Engine, new PassPriorityAction(2));
            Reject(second.Engine, new PassPriorityAction(2));
            return;
        }
        Apply(first.Engine, new PassPriorityAction(2));
        Apply(second.Engine, new PassPriorityAction(2));
        Assert.Equal(first.Engine.LegalActions(0), second.Engine.LegalActions(0));
        var before = first.State.Players[0].Resources.VictoryPoints;
        FinishRewards(first.Engine);
        FinishRewards(second.Engine);
        Assert.Equal(before + 1, first.State.Players[0].Resources.VictoryPoints);
        Assert.Equal(before, second.State.Players[0].Resources.VictoryPoints);
    }

    [Fact]
    public void PostWinCardCannotConsumeStockOrCapacityNeededByUnfinishedMandatoryReward()
    {
        foreach (var conflict in new[] { "resource", "acquire", "mentat", "control" })
        {
            var state = CombatState([8, 6, 4]);
            GiveCards(state, 0, 1);
            var card = state.Players[0].Intrigue.Cards[0];
            var market = state.Shared.ImperiumRow.Cards[0].Definition;
            var effect = conflict switch
            {
                "resource" => (Effect)new GainEffect(ResourceKind.Water, int.MaxValue - state.Players[0].Resources.Water),
                "acquire" => new AcquireEffect(market),
                "mentat" => new TakeMentatEffect(true),
                _ => new ControlSpaceEffect(Space("carthag")),
            };
            var rules = Rewards(LifecycleFixture.Rules(state), 1, [effect]);
            rules = Intrigue(rules, card.Definition, "fixture:post-win-reservation", new(),
                [conflict == "resource" ? new GainEffect(ResourceKind.Water, 1) :
                 conflict == "control" ? new ForPlayerEffect(1, [effect]) : effect]);
            var engine = new TurnEngine(state, rules);
            CloseCombat(engine);
            Reject(engine, new PlayIntrigueAction(0, Card(card)));
            Assert.Contains(engine.LegalActions(0), x => x is ResolveEffectAction);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalPostWinPaymentUsesResolvedFundingAndBlocksInterleavingWhileSelected(bool grantCardFirst)
    {
        GameState Create()
        {
            var result = PrepareCombat(PostWinTimingState(), [8, 6, 4]);
            result.Players[0].Resources = result.Players[0].Resources with { Spice = 0 };
            GiveCards(result, 0, 2);
            return result;
        }
        var state = Create();
        var cards = state.Players[0].Intrigue.Cards.ToArray();
        var rules = Rewards(LifecycleFixture.Rules(state), 1, [new GainEffect(ResourceKind.Spice, 2)]);
        rules = Intrigue(rules, cards[0].Definition, "fixture:post-win-payment", new(),
            [new OptionalPaymentEffect(new(Spice: 2), [new GainEffect(ResourceKind.Solari, 4)])]);
        rules = Intrigue(rules, cards[1].Definition, "fixture:post-win-spice", new(), [new GainEffect(ResourceKind.Spice, 2)]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        if (grantCardFirst) Apply(engine, new PlayIntrigueAction(0, Card(cards[1])));
        else Resolve<GainEffect>(engine);
        Apply(engine, new PlayIntrigueAction(0, Card(cards[0])));
        Resolve<OptionalPaymentEffect>(engine);
        Reject(engine, new PassPriorityAction(0));
        CheckReplay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(0, state.Players[0].Resources.Spice);
        Assert.Equal(4, state.Players[0].Resources.Solari);
        FinishRewards(engine);
        CheckReplay(engine, Create, rules);
    }

    [Fact]
    public void UnfundedOptionalPostWinPaymentRejectsBeforeResourceOrChildMutation()
    {
        var state = PrepareCombat(PostWinTimingState(), [8, 6, 4]);
        state.Players[0].Resources = state.Players[0].Resources with { Spice = 0 };
        GiveCards(state, 0, 1);
        var card = state.Players[0].Intrigue.Cards[0];
        var rules = Rewards(LifecycleFixture.Rules(state), 1, [new GainEffect(ResourceKind.Spice, 2)]);
        rules = Intrigue(rules, card.Definition, "fixture:post-win-payment", new(),
            [new OptionalPaymentEffect(new(Spice: 2), [new GainEffect(ResourceKind.Solari, 4)])]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Apply(engine, new PlayIntrigueAction(0, Card(card)));
        Resolve<OptionalPaymentEffect>(engine);
        Reject(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(0, state.Players[0].Resources.Spice);
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.NotEmpty(engine.CaptureTrustedSnapshot().Data.Frames[0].Remaining);
        Apply(engine, new ChooseOptionAction(0, 0));
        FinishRewards(engine);
        Assert.Equal(2, state.Players[0].Resources.Spice);
    }

    [Fact]
    public void LowerRecipientsRemainInteractiveAndReplayWithoutAPriorityWindow()
    {
        GameState Create() => CombatState([8, 6, 4]);
        var state = Create();
        var rules = Rewards(LifecycleFixture.Rules(state), 1, []);
        rules = Rewards(rules, 2, [new OptionalPaymentEffect(new(),
            [new GainEffect(ResourceKind.Water, 2)], 2) { PayerId = 1, RecipientId = 0 }]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Apply(engine, new PassPriorityAction(0));
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.Equal(1, engine.DecisionOwnerId);
        Resolve<OptionalPaymentEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        CheckReplay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Null(engine.DecisionOwnerId);
        CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void IndependentTiedInfluenceBelowFourGrantsEachTrackAndThresholdVpInCanonicalSeatOrder(int players, bool wrapped)
    {
        GameState Create()
        {
            var result = CombatState(players == 3 ? [8, 8, 4] : [8, 8, 8, 8]);
            result.FirstPlayerId = players - 1; // Award scheduling is seat-canonical, independently of Combat priority.
            foreach (var player in result.Players)
                player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Emperor ? x with { Amount = 1 } : x)];
            return result;
        }
        var state = Create();
        Effect effect = wrapped
            ? new PlayerOrderedEffect([new InfluenceEffect(Faction.Emperor), new InfluenceEffect(Faction.Emperor)])
            : new InfluenceEffect(Faction.Emperor);
        var rules = Rewards(LifecycleFixture.Rules(state), 2, [effect]);
        var before = state.Players.Select(x => x.Resources.VictoryPoints).ToArray();
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Assert.Null(engine.Observe(0).Game.Window); // Tied first never opens PostWin.
        var progress = engine.CaptureTrustedSnapshot().Data.ConflictRewards!;
        Assert.Equal(players == 3 ? new[] { 0, 1 } : new[] { 0, 1, 2, 3 }, progress.Recipients.Select(x => x.PlayerId));
        CheckReplay(engine, Create, rules);
        FinishRewards(engine);
        foreach (var player in state.Players)
        {
            var awarded = players == 4 || player.Id < 2;
            Assert.Equal(awarded ? wrapped ? 3 : 2 : 1, player.Influence.Single(x => x.Faction == Faction.Emperor).Amount);
            Assert.Equal(before[player.Id] + (awarded ? 1 : 0), player.Resources.VictoryPoints);
        }
        Assert.Null(state.Board.Alliances.Single(x => x.Faction == Faction.Emperor).OwnerId);
        CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData("cross-four")]
    [InlineData("signed")]
    [InlineData("choice")]
    [InlineData("incumbent")]
    public void SharedTiedFactionAwardsOutsideIndependentBoundRejectBeforeClosure(string boundary)
    {
        var state = CombatState([8, 8, 4]);
        foreach (var player in state.Players)
            player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Emperor
                ? x with { Amount = boundary == "cross-four" ? 3 : 2 } : x)];
        if (boundary == "incumbent")
            state.Board.Alliances = [.. state.Board.Alliances.Select(x => x.Faction == Faction.Emperor ? x with { OwnerId = 2 } : x)];
        Effect effect = boundary switch
        {
            "signed" => new InfluenceEffect(Faction.Emperor, -1),
            "choice" => new ChoiceEffect([new("Influence", new(), [new InfluenceEffect(Faction.Emperor)])]),
            _ => new InfluenceEffect(Faction.Emperor),
        };
        var engine = new TurnEngine(state, Rewards(LifecycleFixture.Rules(state), 2, [effect]));
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        var result = engine.Step(new PassPriorityAction(2));
        Assert.Equal("conflict-reward-order", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
    }

    [Fact]
    public void RevealPurchaseCannotConsumeMentatReservedByUnfinishedRevealWork()
    {
        var state = LifecycleFixture.CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(LifecycleFixture.Rules(state), card.Definition,
            reveal: [new GainEffect(ResourceKind.Persuasion, 3), new TakeMentatEffect(false)]);
        rules = CardEffects(rules, market.Definition, acquire: [new TakeMentatEffect(false)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        Resolve<GainEffect>(engine, x => x.Resource == ResourceKind.Persuasion && x.Amount == 3);
        Reject(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Assert.Null(state.Board.Mentat.OwnerId);
        Assert.Contains(market.Id, state.Shared.ImperiumRow.Cards.Select(x => x.Id));
    }

    [Fact]
    public void ConditionalVpCannotOverflowBeforeSignedInfluenceLossEvenWhenNetAwardFits()
    {
        var state = CombatState([8, 6, 4]);
        var player = state.Players[0];
        player.Resources = player.Resources with { VictoryPoints = int.MaxValue };
        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Emperor ? x with { Amount = 2 } : x)];
        var reserve = state.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition.Value == "imperium:reserve:spice-must-flow:v1"));
        reserve.MoveTo(reserve.Cards[0].Id, player.Deck);
        reserve.MoveTo(reserve.Cards[0].Id, player.Deck);
        var rules = Rewards(LifecycleFixture.Rules(state), 1, [new SequenceEffect([
            new ConditionalEffect(EndgameCondition.OwnAtLeastTwoSpiceMustFlow, [new GainEffect(ResourceKind.VictoryPoints, 1)]),
            new InfluenceEffect(Faction.Emperor, -1)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Reject(engine, new PassPriorityAction(2));
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("sequence-ordered")]
    [InlineData("optional")]
    [InlineData("choice")]
    [InlineData("acquire")]
    [InlineData("signet")]
    [InlineData("faction-child")]
    public void TiedPositiveSharedIntrigueDrawsRejectBeforeAnyStateRandomHistoryOrReplayMutation(string path)
    {
        var state = CombatState([8, 8, 4]);
        var rules = LifecycleFixture.Rules(state);
        var market = state.Shared.ImperiumRow.Cards[0];
        Effect effect = path switch
        {
            "sequence-ordered" => new SequenceEffect([new PlayerOrderedEffect([new DrawIntrigueEffect(1)])]),
            "optional" => new OptionalPaymentEffect(new(), [new DrawIntrigueEffect(1)]),
            "choice" => new ChoiceEffect([new("Draw", new(), [new DrawIntrigueEffect(1)])]),
            "acquire" => new AcquireEffect(market.Definition),
            "signet" => new SignetEffect(),
            "faction-child" => new InfluenceEffect(Faction.BeneGesserit),
            _ => new DrawIntrigueEffect(1),
        };
        if (path == "acquire") rules = CardEffects(rules, market.Definition, acquire: [new DrawIntrigueEffect(1)]);
        if (path == "signet") rules = rules with
        { Leaders = [.. rules.Leaders.Select(x => x with { SignetEffects = [new DrawIntrigueEffect(1)] })] };
        if (path == "faction-child")
        {
            rules = rules with { Lifecycle = rules.Lifecycle! with { Factions = OriginalImperiumTurnRules.CreateLifecycleRules().Factions } };
            foreach (var player in state.Players)
                player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.BeneGesserit ? x with { Amount = 3 } : x)];
        }
        var engine = new TurnEngine(state, Rewards(rules, 2, [effect]));
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        var replayBefore = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        var randomBefore = state.Random.Checkpoint;
        var leadersRandomBefore = state.LeadersRandom.Checkpoint;
        Assert.DoesNotContain(engine.LegalActions(2), x => x is PassPriorityAction);
        Assert.Equal("conflict-reward-order", Assert.Single(engine.SupportDiagnostics(2)).Code);
        var result = engine.Step(new PassPriorityAction(2));
        Assert.Equal(StepStatus.Unsupported, result.Status);
        Assert.Equal("conflict-reward-order", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(replayBefore, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
        Assert.Equal(randomBefore, state.Random.Checkpoint);
        Assert.Equal(leadersRandomBefore, state.LeadersRandom.Checkpoint);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TiedZeroIntrigueDrawsRemainNoOpsIncludingWrappedEffects(bool wrapped)
    {
        var state = CombatState([8, 8, 4]);
        Effect effect = wrapped ? new SequenceEffect([new PlayerOrderedEffect([new DrawIntrigueEffect(0)])]) : new DrawIntrigueEffect(0);
        var engine = new TurnEngine(state, Rewards(LifecycleFixture.Rules(state), 2, [effect]));
        var deck = state.Shared.IntrigueDeck.Cards.ToArray();
        CloseCombat(engine);
        FinishRewards(engine);
        Assert.Equal(deck, state.Shared.IntrigueDeck.Cards);
        Assert.All(state.Players, x => Assert.Empty(x.Intrigue.Cards));
    }

    [Fact]
    public void IndependentTiedFactionTracksDoNotTriggerOrReserveTheirUnreachedIntrigueBonus()
    {
        var state = CombatState([8, 8, 4]);
        foreach (var player in state.Players)
            player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.BeneGesserit ? x with { Amount = 1 } : x)];
        var rules = LifecycleFixture.Rules(state);
        rules = rules with { Lifecycle = rules.Lifecycle! with { Factions = OriginalImperiumTurnRules.CreateLifecycleRules().Factions } };
        var engine = new TurnEngine(state, Rewards(rules, 2, [new InfluenceEffect(Faction.BeneGesserit)]));
        var deck = state.Shared.IntrigueDeck.Cards.ToArray();
        CloseCombat(engine);
        FinishRewards(engine);
        Assert.Equal(2, state.Players[0].Influence.Single(x => x.Faction == Faction.BeneGesserit).Amount);
        Assert.Equal(2, state.Players[1].Influence.Single(x => x.Faction == Faction.BeneGesserit).Amount);
        Assert.Equal(deck, state.Shared.IntrigueDeck.Cards);
    }

    [Fact]
    public void UniqueWinnerAndUntiedLowerRecipientStillReceiveTheirIntrigueDrawsInRewardOrder()
    {
        GameState Create() => CombatState([8, 6, 4]);
        var state = Create();
        var rules = Rewards(Rewards(LifecycleFixture.Rules(state), 1, [new DrawIntrigueEffect(1)]), 2, [new DrawIntrigueEffect(1)]);
        var expected = state.Shared.IntrigueDeck.Cards.Take(2).Select(x => x.Id).ToArray();
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Resolve<DrawIntrigueEffect>(engine);
        CheckReplay(engine, Create, rules);
        Apply(engine, new PassPriorityAction(0));
        Assert.Equal(expected[0], Assert.Single(state.Players[0].Intrigue.Cards).Id);
        Assert.Equal(expected[1], Assert.Single(state.Players[1].Intrigue.Cards).Id);
        Assert.Null(engine.DecisionOwnerId);
        CheckReplay(engine, Create, rules);
    }

    [Fact]
    public void LowerRecipientSupportDiagnosticsReflectPendingChoicesInsteadOfGenericLifecycleStop()
    {
        var state = CombatState([8, 6, 4]);
        var rules = Rewards(LifecycleFixture.Rules(state), 1, []);
        rules = Rewards(rules, 2, [new OptionalPaymentEffect(new(), [new DrawIntrigueEffect(state.Shared.IntrigueDeck.Count + 1)])]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Apply(engine, new PassPriorityAction(0));
        Assert.Null(engine.Observe(0).Game.Window);
        Assert.Contains(engine.LegalActions(1), x => x is ResolveEffectAction);
        Assert.Empty(engine.SupportDiagnostics(1));
        Assert.Empty(engine.SupportDiagnostics(0));
        Resolve<OptionalPaymentEffect>(engine);
        Assert.Equal("intrigue-empty", Assert.Single(engine.SupportDiagnostics(1)).Code);
        Assert.Empty(engine.SupportDiagnostics(0));
        Apply(engine, new ChooseOptionAction(1, 0));
        Assert.Null(engine.DecisionOwnerId);
        Assert.Equal("lifecycle-support", Assert.Single(engine.SupportDiagnostics(0)).Code);
    }

    internal static GameState CombatState(int[] strengths)
    {
        return PrepareCombat(LifecycleFixture.CreateState(strengths.Length), strengths);
    }

    private static GameState PrepareCombat(GameState state, int[] strengths)
    {
        state.Phase = GamePhase.Combat;
        state.Round = 1;
        state.Conflict.Deck.TryDrawTo(state.Conflict.Revealed);
        foreach (var player in state.Players)
        {
            var troops = strengths[player.Id] / 2;
            player.Units = player.Units with { TroopsInSupply = player.Units.TroopsInSupply - troops, TroopsInConflict = troops };
        }
        return state;
    }
    internal static TurnRuleSet Rewards(TurnRuleSet rules, int tier, ImmutableArray<Effect> effects) => rules with
    {
        Lifecycle = rules.Lifecycle! with
        {
            Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with
        { Rewards = [.. x.Rewards.Select(r => r.Tier == tier ? r with { Effects = effects } : r)] })]
        }
    };
    private static GameState PostWinTimingState()
    {
        var original = LifecycleFixture.CreateState();
        var names = new[] { "fixture:post-win-payment", "fixture:post-win-spice" };
        var substitutions = original.Shared.IntrigueDeck.Cards.Take(2).Select((card, i) => (card.Definition, Id: new ContentId(names[i])))
            .ToDictionary(x => x.Definition, x => x.Id);
        var configuration = OriginalImperiumFixture.Configuration(3) with
        { Content = new() { Id = "synthetic-post-win-timing-fixture", Revision = 1 } };
        var content = original.Definition.Content.Select(x => substitutions.TryGetValue(x.Id, out var id)
            ? x with
            {
                Id = id,
                Name = "SYNTHETIC " + id.Value,
                Sources = [new("Synthetic post-win timing fixture; invented amounts", 1, "Not official card text")]
            } : x)
            .OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToImmutableArray();
        var definition = original.Definition with
        {
            Content = content,
            ConfigurationIdentity = ConfigurationIdentity.Compute(configuration),
            Fingerprint = RulesetFingerprint.Compute(configuration, original.Definition.Modules, original.Definition.Definitions,
                content, original.Definition.EligibleLeaders, original.Definition.CatalogCompleteness, original.Definition.Setup),
        };
        var shared = original.Shared;
        var intrigue = shared.IntrigueDeck;
        var replacement = new SharedCardState(shared.ImperiumDeck, shared.ImperiumRow,
            new CardZone(intrigue.Id, intrigue.Name, intrigue.Visibility,
                cards: intrigue.Cards.Select(x => substitutions.TryGetValue(x.Definition, out var id) ? x with { Definition = id } : x)),
            shared.Reserves, shared.UnusedStarters);
        var identity = System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content, ConfigurationJson.Options));
        return new(definition, ConfigurationIdentity.Compute(configuration, true), Convert.ToHexStringLower(identity),
            ContentAuthenticity.SyntheticFixture, original.Players, original.Board, replacement, original.Random, original.LeadersRandom,
            original.FirstPlayerId);
    }

    private static TurnRuleSet Intrigue(TurnRuleSet rules, ContentId id, string fixtureName, ResourceCost cost, ImmutableArray<Effect> effects) => rules with
    {
        Lifecycle = rules.Lifecycle! with
        {
            Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Id == id
        ? x with { Timing = IntrigueTiming.PostWin, Cost = cost, Effects = effects,
            Sources = [new("Synthetic " + fixtureName + "; invented amounts, not official card text", 1, "Timing only")] } : x)]
        }
    };
    private static void GiveCards(GameState state, int owner, int count)
    {
        for (var i = 0; i < count; i++) state.Shared.IntrigueDeck.TryDrawTo(state.Players[owner].Intrigue);
    }
    internal static void CloseCombat(TurnEngine engine)
    {
        var guard = 0;
        while (engine.Observe(0).Game.Window?.Kind == DecisionWindowKind.Combat)
        {
            Assert.True(++guard < 20);
            Apply(engine, new PassPriorityAction(engine.DecisionOwnerId!.Value));
        }
    }
    internal static void FinishRewards(TurnEngine engine)
    {
        var guard = 0;
        while (engine.DecisionOwnerId is { } owner)
        {
            Assert.True(++guard < 100);
            var actions = engine.LegalActions(owner);
            GameAction? action = actions.OfType<ResolveEffectAction>().FirstOrDefault();
            action ??= actions.OfType<ChooseOptionAction>().FirstOrDefault();
            action ??= actions.OfType<PassPriorityAction>().FirstOrDefault();
            Apply(engine, action ?? throw new InvalidOperationException("Reward work is stuck: " + string.Join(';', engine.SupportDiagnostics(owner))));
        }
    }
    internal static void Resolve<T>(TurnEngine engine, Func<T, bool>? match = null) where T : Effect
    {
        var frame = engine.CaptureTrustedSnapshot().Data.Frames[^1];
        var effect = frame.Remaining.First(x => x.Effect is T value && (match is null || match(value)));
        Apply(engine, new ResolveEffectAction(engine.DecisionOwnerId!.Value, effect.Id));
    }
    internal static void Reject(TurnEngine engine, GameAction action)
    {
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.NotEqual(StepStatus.Applied, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
    }
    internal static void CheckReplay(TurnEngine engine, Func<GameState> create, TurnRuleSet rules)
    {
        var snapshot = engine.CaptureTrustedSnapshot();
        var restored = JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options)!;
        Assert.Equal(snapshot.CanonicalJson, JsonSerializer.Serialize(restored, ConfigurationJson.Options));
        var replay = JsonSerializer.Deserialize<TurnReplay>(JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options), ConfigurationJson.Options)!;
        var result = TurnEngine.Replay(replay, create, rules);
        Assert.True(result.IsSuccess, string.Join(';', result.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().CanonicalJson, result.Engine!.CaptureTrustedSnapshot().CanonicalJson);
        foreach (var player in engine.CaptureTrustedSnapshot().Data.Game.Players)
            Assert.Equal(engine.LegalActions(player.Id), result.Engine.LegalActions(player.Id));
    }
}
