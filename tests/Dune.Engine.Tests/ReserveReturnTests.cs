using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class ReserveReturnTests
{
    private static readonly ContentId Liaison = new("imperium:reserve:arrakis-liaison:v1");
    private static readonly ContentId SpiceMustFlow = new("imperium:reserve:spice-must-flow:v1");
    private static readonly ContentId SyntheticReserve = new("fixture:reserve:lifecycle:v1");

    [Theory]
    [InlineData("arrakis-liaison", false)]
    [InlineData("arrakis-liaison", true)]
    [InlineData("spice-must-flow", false)]
    [InlineData("spice-must-flow", true)]
    [InlineData("foldspace", false)]
    [InlineData("foldspace", true)]
    public void EachReserveReturnsTheSameInstanceToItsOwnPossiblyEmptyStack(string name, bool empty)
    {
        var state = ReadyState();
        var definition = new ContentId("imperium:reserve:" + name + ":v1");
        var stack = Stack(state, definition);
        var card = stack.Cards[0];
        stack.MoveTo(card.Id, state.Players[0].Discard);
        if (empty) Empty(state, stack);
        var before = Inventory(state);
        var count = stack.Count;
        // Foldspace's unsupported Agent/Reveal boxes do not affect destination eligibility.
        var engine = new TurnEngine(state, ReadyRules(state, [new SelectedTrashEffect([])]));
        Begin(engine, state);
        Apply(engine, Selection(engine, card));
        Assert.Same(card, stack.Cards.Single(x => x.Id == card.Id));
        Assert.Equal(count + 1, stack.Count);
        Assert.DoesNotContain(card, state.Shared.Trashed.Cards);
        Assert.DoesNotContain(card, state.Players[0].Discard.Cards);
        Assert.Equal(before, Inventory(state));
    }

    [Fact]
    public void SelfTrashUsesTheSameReserveDestination()
    {
        var state = ReadyState(syntheticReserve: true);
        var stack = Stack(state, SyntheticReserve);
        var card = stack.Cards[0];
        foreach (var extra in stack.Cards.Skip(1).ToArray()) stack.MoveTo(extra.Id, state.Shared.Trashed);
        var before = Inventory(state);
        // Invented acquisition children exercise the executor's physical-source contract;
        // the lifecycle Reserve is explicitly synthetic, including its setup/category/zone.
        var rules = SyntheticAcquire(ReadyRules(state, [new ForPlayerEffect(1, [new AcquireEffect(SyntheticReserve)])]),
            SyntheticReserve,
            [new ForPlayerEffect(2, [new TrashSelfEffect(), new TrashSelfEffect()])]);
        var engine = new TurnEngine(state, rules);
        Place(engine, state);
        ResolveAll(engine);
        Assert.Same(card, Assert.Single(stack.Cards));
        Assert.DoesNotContain(card, state.Players[0].Discard.Cards);
        Assert.Equal(before, Inventory(state));
    }

    [Fact]
    public void ReturnedReserveCanBeAcquiredWithItsAcquireEffects()
    {
        var state = ReadyState();
        var card = OwnReserve(state, SpiceMustFlow, empty: true);
        var before = Inventory(state);
        var engine = new TurnEngine(state,
            ReadyRules(state, [new SelectedTrashEffect([new AcquireEffect(SpiceMustFlow)])]));
        Begin(engine, state);
        var vp = state.Players[0].Resources.VictoryPoints;
        Apply(engine, Selection(engine, card));
        ResolveAll(engine);
        Assert.Same(card, state.Players[0].Discard.Cards.Single(x => x.Id == card.Id));
        Assert.Empty(Stack(state, SpiceMustFlow).Cards);
        Assert.Equal(vp + 1, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(before, Inventory(state));
    }

    [Fact]
    public void SpiceMustFlowKeepsPreviouslyEarnedVictoryPoint()
    {
        var state = ReadyState();
        var rules = ReadyRules(state,
            [new SequenceEffect([new AcquireEffect(SpiceMustFlow), new SelectedTrashEffect([])])]);
        var engine = new TurnEngine(state, rules);
        var before = state.Players[0].Resources.VictoryPoints;
        Place(engine, state);
        Resolve<SequenceEffect>(engine);
        ResolveAll(engine);
        var acquired = state.Players[0].Discard.Cards.Single(x => x.Definition == SpiceMustFlow);
        Assert.Equal(before + 1, state.Players[0].Resources.VictoryPoints);
        Apply(engine, Selection(engine, acquired));
        Assert.Contains(acquired, Stack(state, SpiceMustFlow).Cards);
        Assert.Equal(before + 1, state.Players[0].Resources.VictoryPoints);
    }

    [Fact]
    public void ReturnedReserveCanBePurchasedNormallyLater()
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state, [new SelectedTrashEffect([])]));
        Begin(engine, state);
        Apply(engine, Selection(engine, card));
        foreach (var owner in new[] { 1, 2 })
        {
            Apply(engine, new RevealAction(owner));
            ResolveAll(engine);
            Apply(engine, new EndRevealAction(owner));
        }
        Apply(engine, new RevealAction(0));
        ResolveAll(engine);
        var acquire = engine.LegalActions(0).OfType<AcquireCardAction>()
            .Single(x => x.Source == Stack(state, Liaison).Id && x.Card.Definition == Liaison);
        Apply(engine, acquire);
        Assert.Same(card, state.Players[0].Discard.Cards.Single(x => x.Id == card.Id));
        Assert.Empty(Stack(state, Liaison).Cards);
    }

    [Theory]
    [InlineData("arrakis-liaison")]
    [InlineData("spice-must-flow")]
    [InlineData("foldspace")]
    public void MandatoryTrashCanUseAnOwnedReserveAsItsOnlyTarget(string name)
    {
        var state = ReadyState();
        foreach (var card in state.Players[1].Hand.Cards.ToArray())
            state.Players[1].Hand.MoveTo(card.Id, state.Players[1].Deck);
        var stack = Stack(state, new("imperium:reserve:" + name + ":v1"));
        var target = stack.Cards[0];
        stack.MoveTo(target.Id, state.Players[1].Discard);
        var engine = new TurnEngine(state, ReadyRules(state,
            [new ForPlayerEffect(1, [new SelectedTrashEffect([], Optional: false)])]));
        Place(engine, state);
        Resolve<ForPlayerEffect>(engine);
        Resolve<SelectedTrashEffect>(engine);
        var select = Assert.Single(engine.LegalActions(1).OfType<SelectTrashCardAction>());
        Apply(engine, select);
        Assert.Contains(target, stack.Cards);
        Assert.Empty(state.Players[1].Discard.Cards);
    }

    [Fact]
    public void SelectedReturnFundsOnlyPostTrashMandatoryChildren()
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state, [new SelectedTrashEffect([new AcquireEffect(Liaison)])]));
        Begin(engine, state);
        var ordinary = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!.Targets
            .First(x => x.Card.Definition != Liaison);
        RejectUnchanged(engine, new SelectTrashCardAction(0, DecisionId(engine), ordinary.Source, ordinary.Card),
            StepStatus.Unsupported);
        Apply(engine, Selection(engine, card));
        ResolveAll(engine);
        Assert.Contains(card, state.Players[0].Discard.Cards);
        Assert.Empty(Stack(state, Liaison).Cards);
    }

    [Fact]
    public void UnselectedOptionalReturnNeverCreatesStock()
    {
        var state = ReadyState();
        OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state,
            [new SelectedTrashEffect([]), new AcquireEffect(Liaison)]));
        RejectUnchanged(engine, Placement(state), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlierAcquireCannotConsumeFutureReturn(bool mandatoryReturn)
    {
        var state = ReadyState();
        OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state,
        [
            new SequenceEffect([new AcquireEffect(Liaison), new SelectedTrashEffect([], Optional: !mandatoryReturn)])
        ]));
        RejectUnchanged(engine, Placement(state), StepStatus.Unsupported);
    }

    [Fact]
    public void MandatoryReturnDoesNotPromiseAnUnprovedOrderedStockCredit()
    {
        var state = ReadyState();
        OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state,
            [new SequenceEffect([new SelectedTrashEffect([], Optional: false), new AcquireEffect(Liaison)])]));
        RejectUnchanged(engine, Placement(state), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedThresholdAcquireAndNestedStockChoicesUseTheSpecificReturn(bool nestedChoice)
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: true);
        ImmutableArray<Effect> reward = [new AcquireEffect(Liaison)];
        if (nestedChoice)
            reward =
            [
                new ChoiceEffect([
                    new("Outer", new(),
                        [new ChoiceEffect([new("Inner", new(), reward)])])
                ])
            ];
        var rules = Threshold(ReadyRules(state, [new SelectedTrashEffect([new InfluenceEffect(Faction.Emperor, 4)])]),
            reward);
        var engine = new TurnEngine(state, rules);
        Begin(engine, state);
        Apply(engine, Selection(engine, card));
        ResolveAll(engine);
        Assert.Contains(card, state.Players[0].Discard.Cards);
        Assert.Empty(Stack(state, Liaison).Cards);
        Assert.Equal(4, state.Players[0].Influence.Single(x => x.Faction == Faction.Emperor).Amount);
    }

    [Theory]
    [InlineData("resources", StepStatus.ContentError)]
    [InlineData("mentat", StepStatus.Unsupported)]
    [InlineData("permanent", StepStatus.Illegal)]
    [InlineData("stock", StepStatus.Unsupported)]
    [InlineData("intrigue", StepStatus.Unsupported)]
    public void GeneratedMandatoryFailureRejectsTheWholeSelectedReturn(string problem, StepStatus status)
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: true);
        ImmutableArray<Effect> reward = problem switch
        {
            "resources" => [new GainEffect(ResourceKind.Water, int.MaxValue)],
            "mentat" => [new TakeMentatEffect(true), new TakeMentatEffect(true)],
            "permanent" => [new HighCouncilEffect(), new HighCouncilEffect()],
            "stock" => [new AcquireEffect(Liaison), new AcquireEffect(Liaison)],
            _ => [new DrawIntrigueEffect(int.MaxValue)],
        };
        var engine = new TurnEngine(state, Threshold(ReadyRules(state,
            [new SelectedTrashEffect([new InfluenceEffect(Faction.Emperor, 4)])]), reward));
        Begin(engine, state);
        RejectUnchanged(engine, Selection(engine, card), status);
        Assert.Contains(card, state.Players[0].Discard.Cards);
        Assert.Empty(Stack(state, Liaison).Cards);
        Apply(engine, new DeclineTrashAction(0, DecisionId(engine)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedReturnReservesSuspendedParentStock(bool insufficient)
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: false);
        var stack = Stack(state, Liaison);
        foreach (var extra in stack.Cards.Skip(1).ToArray()) stack.MoveTo(extra.Id, state.Shared.Trashed);
        ImmutableArray<Effect> success = insufficient
            ? [new AcquireEffect(Liaison), new AcquireEffect(Liaison)]
            : [new AcquireEffect(Liaison)];
        var engine = new TurnEngine(state, ReadyRules(state,
            [new SequenceEffect([new SelectedTrashEffect(success), new AcquireEffect(Liaison)])]));
        Place(engine, state);
        Resolve<SequenceEffect>(engine);
        if (insufficient) RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        else
        {
            Apply(engine, Selection(engine, card));
            ResolveAll(engine);
            Assert.Equal(2, state.Players[0].Discard.Cards.Count(x => x.Definition == Liaison));
            Assert.Empty(stack.Cards);
        }
    }

    [Fact]
    public void SelectedRemovalCannotStrandLaterMandatoryTrash()
    {
        var state = ReadyState();
        foreach (var card in state.Players[0].Hand.Cards.Skip(1).ToArray())
            state.Players[0].Hand.MoveTo(card.Id, state.Players[0].Deck);
        var engine = new TurnEngine(state, ReadyRules(state,
            [new SelectedTrashEffect([]), new SelectedTrashEffect([], Optional: false)]));
        Begin(engine, state);
        var selected = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = Assert.Single(selected.Targets);
        RejectUnchanged(engine, new SelectTrashCardAction(0, selected.Id, target.Source, target.Card),
            StepStatus.Unsupported);
        Apply(engine, new DeclineTrashAction(0, selected.Id));
        Resolve<SelectedTrashEffect>(engine);
        Apply(engine, Assert.Single(engine.LegalActions(0).OfType<SelectTrashCardAction>()));
    }

    [Fact]
    public void InconsistentReserveDestinationRejectsBeforeMovement()
    {
        var state = ReadyState();
        var card = OwnReserve(state, Liaison, empty: true);
        var engine = new TurnEngine(state, ReadyRules(state, [new SelectedTrashEffect([])]));
        Begin(engine, state);
        var other = Stack(state, SpiceMustFlow);
        other.MoveTo(other.Cards[0].Id, Stack(state, Liaison));
        RejectUnchanged(engine, Selection(engine, card), StepStatus.ContentError);
        Assert.Contains(card, state.Players[0].Discard.Cards);
    }

    [Fact]
    public void ForeignRecipientPreservesAcquiredSourceOwnerBinding()
    {
        var state = ReadyState(syntheticReserve: true);
        var rules = SyntheticAcquire(ReadyRules(state,
            [new ForPlayerEffect(1, [new AcquireEffect(SyntheticReserve)])]), SyntheticReserve,
            [new ForPlayerEffect(2, [new OptionalPaymentEffect(new(), [new TrashSelfEffect()])])]);
        var card = Stack(state, SyntheticReserve).Cards[0];
        var engine = new TurnEngine(state, rules);
        Place(engine, state);
        Resolve<ForPlayerEffect>(engine);
        Resolve<AcquireEffect>(engine);
        Resolve<ForPlayerEffect>(engine);
        Resolve<OptionalPaymentEffect>(engine);
        var decision = engine.CaptureTrustedSnapshot().Data.Decisions.Single();
        Assert.Equal(0, decision.Context.InitiatorId);
        Assert.Equal(1, decision.Context.SourceOwnerId);
        Assert.Equal(2, decision.Context.RecipientId);
        Assert.Equal(card.Id, decision.Context.SourceCard);
        Apply(engine, new ChooseOptionAction(2, 1));
        ResolveAll(engine);
        Assert.Contains(card, Stack(state, SyntheticReserve).Cards);
        Assert.DoesNotContain(card, state.Players[1].Discard.Cards);
    }

    [Fact]
    public void MovedOrdinaryExecutingSourceRejectsBeforeMutation()
    {
        var state = ReadyState();
        var source = state.Players[0].Hand.Cards[0];
        var engine = new TurnEngine(state, ReadyRules(state,
            [new TrashSelfEffect(), new GainEffect(ResourceKind.Solari, 1)]));
        Place(engine, state);
        state.Players[0].InPlay.MoveTo(source.Id, state.Players[1].Discard);
        var pending = engine.Observe(0).PendingEffects.Single(x => x.Kind == nameof(TrashSelfEffect));
        RejectUnchanged(engine, new ResolveEffectAction(0, pending.Id), StepStatus.Unsupported);
        Assert.Contains(source, state.Players[1].Discard.Cards);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void SelectedSourceReturnCannotPromiseItsOwnReacquisitionAndLaterSelfTrash(int beneficiary, bool choice)
    {
        var state = ReadyState(syntheticReserve: true);
        var card = Stack(state, SyntheticReserve).Cards[0];
        Stack(state, SyntheticReserve).MoveTo(card.Id, state.Players[0].Hand);
        Empty(state, Stack(state, SyntheticReserve));
        ImmutableArray<Effect> success = [new ForPlayerEffect(beneficiary, [new AcquireEffect(SyntheticReserve)])];
        if (choice) success = [new ChoiceEffect([new("Reacquire", new(), success)])];
        var rules = CardEffects(ReadyRules(state, []), SyntheticReserve, agent:
            [new SequenceEffect([new SelectedTrashEffect(success),
                new TrashSelfEffect()])]);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x.Id == SyntheticReserve ? x with { Icons = [AgentIcon.SpiceTrade] } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<SequenceEffect>(engine);
        RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        Assert.Contains(card, state.Players[0].InPlay.Cards);
    }

    [Fact]
    public void SelectedSourceCannotEnterChoiceThatWouldReacquireAndTrashIt()
    {
        var state = ReadyState(syntheticReserve: true);
        var card = Stack(state, SyntheticReserve).Cards[0];
        Stack(state, SyntheticReserve).MoveTo(card.Id, state.Players[0].Hand);
        Empty(state, Stack(state, SyntheticReserve));
        var rules = CardEffects(ReadyRules(state, []), SyntheticReserve, agent:
            [new SelectedTrashEffect([new ChoiceEffect([new("Reacquire and trash", new(),
                [new SequenceEffect([new ForPlayerEffect(1, [new AcquireEffect(SyntheticReserve)]), new TrashSelfEffect()])])])])]);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x.Id == SyntheticReserve ? x with { Icons = [AgentIcon.SpiceTrade] } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        Assert.Contains(card, state.Players[0].InPlay.Cards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreeEmptyChoiceKeepsReturnAndDeclineAvailableButRejectsUnsafeReacquisition(bool selfInsideChoice)
    {
        var state = ReadyState(syntheticReserve: true);
        var card = Stack(state, SyntheticReserve).Cards[0];
        Stack(state, SyntheticReserve).MoveTo(card.Id, state.Players[0].Hand);
        Empty(state, Stack(state, SyntheticReserve));
        ImmutableArray<Effect> unsafeOption = [new ForPlayerEffect(1, [new AcquireEffect(SyntheticReserve)])];
        if (selfInsideChoice) unsafeOption = [new SequenceEffect([.. unsafeOption, new TrashSelfEffect()])];
        Effect success = new ChoiceEffect([new("Unsafe", new(), unsafeOption), new("Skip", new(), [])]);
        ImmutableArray<Effect> sourceEffects = selfInsideChoice
            ? [new SelectedTrashEffect([success])]
            : [new SequenceEffect([new SelectedTrashEffect([success]), new TrashSelfEffect()])];
        var rules = CardEffects(ReadyRules(state, []), SyntheticReserve, agent: sourceEffects);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x.Id == SyntheticReserve ? x with { Icons = [AgentIcon.SpiceTrade] } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        if (!selfInsideChoice) Resolve<SequenceEffect>(engine);
        else Resolve<SelectedTrashEffect>(engine);
        Assert.Contains(engine.LegalActions(0), x => x is DeclineTrashAction);
        Apply(engine, Selection(engine, card));
        Resolve<ChoiceEffect>(engine);
        RejectUnchanged(engine, new ChooseOptionAction(0, 0), StepStatus.Unsupported);
        Assert.Equal(new ChooseOptionAction(0, 1), Assert.Single(engine.LegalActions(0)));
        Apply(engine, new ChooseOptionAction(0, 1));
        ResolveAll(engine);
        Assert.Contains(card, Stack(state, SyntheticReserve).Cards);
        Assert.DoesNotContain(card, state.Players[1].Discard.Cards);
    }

    [Fact]
    public void ReturnedSourceReacquiredByAnotherOwnerCannotBeTrashedAgain()
    {
        var state = ReadyState(syntheticReserve: true);
        var rules = SyntheticAcquire(ReadyRules(state, [new AcquireEffect(SyntheticReserve)]), SyntheticReserve,
            [new TrashSelfEffect(), new OptionalPaymentEffect(new(), [new TrashSelfEffect()])]);
        var engine = new TurnEngine(state, rules);
        Place(engine, state);
        Resolve<AcquireEffect>(engine);
        var card = state.Players[0].Discard.Cards.Single(x => x.Definition == SyntheticReserve);
        Resolve<TrashSelfEffect>(engine);
        Resolve<OptionalPaymentEffect>(engine);
        Stack(state, SyntheticReserve).MoveTo(card.Id, state.Players[1].Discard);
        RejectUnchanged(engine, new ChooseOptionAction(0, 1), StepStatus.Unsupported);
        Assert.Contains(card, state.Players[1].Discard.Cards);
        Apply(engine, new ChooseOptionAction(0, 0));
    }

    [Fact]
    public void CorrectlyBoundResolvingIntrigueSelfTrashKeepsItsPhysicalSource()
    {
        var state = ConflictRewardTests.CombatState([8, 6, 4]);
        var card = state.Shared.IntrigueDeck.Cards[0];
        state.Shared.IntrigueDeck.MoveTo(card.Id, state.Players[0].Intrigue);
        var rules = LifecycleFixture.Rules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Id == card.Definition ? x with
                { Effects = [new ForPlayerEffect(1, [new TrashSelfEffect()])] } : x)],
            },
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlayIntrigueAction(0, Card(card)));
        ResolveAll(engine);
        Assert.Contains(card, state.Shared.Trashed.Cards);
        Assert.DoesNotContain(card, state.Shared.IntrigueDiscard.Cards);
        Assert.Empty(state.Shared.IntrigueResolving.Cards);
        Assert.Equal(0, engine.DecisionOwnerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedReturnReservesLaterConflictRecipientStock(bool insufficient)
    {
        var state = ConflictRewardTests.CombatState([8, 6, 4]);
        var card = OwnReserve(state, Liaison, empty: false);
        var stack = Stack(state, Liaison);
        foreach (var extra in stack.Cards.Skip(1).ToArray()) stack.MoveTo(extra.Id, state.Shared.Trashed);
        ImmutableArray<Effect> success = insufficient
            ? [new AcquireEffect(Liaison), new AcquireEffect(Liaison)] : [new AcquireEffect(Liaison)];
        var rules = ConflictRewardTests.Rewards(LifecycleFixture.Rules(state), 1, [new SelectedTrashEffect(success)]);
        rules = ConflictRewardTests.Rewards(rules, 2, [new AcquireEffect(Liaison)]);
        rules = ConflictRewardTests.Rewards(rules, 3, []);
        var engine = new TurnEngine(state, rules);
        ConflictRewardTests.CloseCombat(engine);
        Resolve<SelectedTrashEffect>(engine);
        if (insufficient) RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        else
        {
            Apply(engine, Selection(engine, card));
            ConflictRewardTests.FinishRewards(engine);
            Assert.Single(state.Players[0].Discard.Cards, x => x.Definition == Liaison);
            Assert.Single(state.Players[1].Discard.Cards, x => x.Definition == Liaison);
            Assert.Empty(stack.Cards);
        }
    }

    [Fact]
    public void GeneratedReacquisitionCannotStrandTheSelectedExecutingSource()
    {
        var state = ReadyState(syntheticReserve: true);
        var card = Stack(state, SyntheticReserve).Cards[0];
        Stack(state, SyntheticReserve).MoveTo(card.Id, state.Players[0].Hand);
        var rules = CardEffects(ReadyRules(state, []), SyntheticReserve, agent:
            [new SequenceEffect([new SelectedTrashEffect([new InfluenceEffect(Faction.Emperor, 4)]), new TrashSelfEffect()])]);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x.Id == SyntheticReserve ? x with { Icons = [AgentIcon.SpiceTrade] } : x)] };
        rules = Threshold(rules, [new ForPlayerEffect(1, [new AcquireEffect(SyntheticReserve)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<SequenceEffect>(engine);
        RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        Assert.Contains(card, state.Players[0].InPlay.Cards);
    }

    [Fact]
    public void SelectedGeneratedMentatReservesTheLaterConflictRecipient()
    {
        var state = ConflictRewardTests.CombatState([8, 6, 4]);
        var card = OwnReserve(state, Liaison, empty: true);
        var rules = Threshold(LifecycleFixture.Rules(state), [new TakeMentatEffect(true)]);
        rules = ConflictRewardTests.Rewards(rules, 1, [new SelectedTrashEffect([new InfluenceEffect(Faction.Emperor, 4)])]);
        rules = ConflictRewardTests.Rewards(rules, 2, [new TakeMentatEffect(true)]);
        rules = ConflictRewardTests.Rewards(rules, 3, []);
        var engine = new TurnEngine(state, rules);
        ConflictRewardTests.CloseCombat(engine);
        Resolve<SelectedTrashEffect>(engine);
        RejectUnchanged(engine, Selection(engine, card), StepStatus.Unsupported);
        Assert.Contains(card, state.Players[0].Discard.Cards);
    }

    private static GameState ReadyState(bool syntheticReserve = false)
    {
        var state = syntheticReserve ? LifecycleFixture.CreateState() : CreateState();
        state.Phase = GamePhase.PlayerTurns;
        state.Round = 1;
        state.CurrentPlayerId = state.DecisionOwnerId = 0;
        foreach (var player in state.Players)
            for (var i = 0; i < 5; i++)
                player.Deck.TryDrawTo(player.Hand);
        return state;
    }

    private static TurnRuleSet ReadyRules(GameState state, ImmutableArray<Effect> effects)
    {
        var rules = state.Definition.Setup!.Reserves.Any(x => x.Definition == SyntheticReserve)
            ? LifecycleFixture.Rules(state)
            : Rules(state);
        return CardEffects(rules, state.Players[0].Hand.Cards[0].Definition, agent: effects) with
        {
            Spaces = [.. rules.Spaces.Select(x => x with { Effects = [] })]
        };
    }

    private static TurnRuleSet
        SyntheticAcquire(TurnRuleSet rules, ContentId definition, ImmutableArray<Effect> effects) => rules with
        {
            Cards =
        [
            .. rules.Cards.Select(x => x.Id == definition
                ? x with
                {
                    AcquireEffects = effects,
                    Sources =
                    [
                        new("Synthetic Reserve executor fixture, not official card text", 1,
                            "Physical source routing only")
                    ],
                }
                : x)
        ],
        };

    private static TurnRuleSet Threshold(TurnRuleSet rules, ImmutableArray<Effect> reward) => rules with
    {
        Lifecycle = (rules.Lifecycle ?? new LifecycleRuleSet("synthetic-reserve-return-threshold-v1", [])) with
        { Factions = [new(Faction.Emperor, 6, reward, [new("Synthetic return fixture", 1, "Invented threshold reward")])] },
    };

    private static CardZone Stack(GameState state, ContentId definition) =>
        state.Shared.Reserves.Single(x => x.Id.Value == "reserve:" + definition.Value);

    private static void Empty(GameState state, CardZone stack)
    {
        foreach (var card in stack.Cards.ToArray()) stack.MoveTo(card.Id, state.Shared.Trashed);
    }

    private static CardInstance OwnReserve(GameState state, ContentId definition, bool empty)
    {
        var stack = Stack(state, definition);
        var card = stack.Cards[0];
        stack.MoveTo(card.Id, state.Players[0].Discard);
        if (empty) Empty(state, stack);
        return card;
    }

    private static PlaceAgentAction Placement(GameState state) =>
        new(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract"));

    private static void Place(TurnEngine engine, GameState state) => Apply(engine, Placement(state));

    private static void Begin(TurnEngine engine, GameState state)
    {
        Place(engine, state);
        Resolve<SelectedTrashEffect>(engine);
    }

    private static int DecisionId(TurnEngine engine) =>
        engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!.Id;

    private static SelectTrashCardAction Selection(TurnEngine engine, CardInstance card)
    {
        var selection = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = selection.Targets.Single(x => x.Instance == card.Id);
        return new(0, selection.Id, target.Source, target.Card);
    }

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        var work = engine.Observe(owner).PendingEffects.First(x => x.Kind == typeof(T).Name);
        Apply(engine, new ResolveEffectAction(owner, work.Id));
    }

    private static string Inventory(GameState state) => JsonSerializer.Serialize(state.Zones.SelectMany(x => x.Cards)
            .GroupBy(x => x.Definition).OrderBy(x => x.Key.Value, StringComparer.Ordinal)
            .Select(x => new { Definition = x.Key, Instances = x.Select(c => c.Id.Value).Order().ToArray() }),
        ConfigurationJson.Options);

    private static void RejectUnchanged(TurnEngine engine, GameAction action, StepStatus expected)
    {
        var before = engine.CaptureTrustedSnapshot();
        var replay = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        Assert.Equal(expected, engine.Step(action).Status);
        Assert.Equal(before.CanonicalJson, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(before.Hash, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(replay, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
    }
}
