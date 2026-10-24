using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

/// <summary>Invented graphs exercise expected validation failures before any trusted state changes.</summary>
public sealed class EffectSafetyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PermanentMultiplicityAndOwnershipRejectBeforePlacement(bool council, bool alreadyOwned)
    {
        var state = CreateState();
        var player = state.Players[0];
        player.HasHighCouncilSeat = council && alreadyOwned;
        if (!council && alreadyOwned) player.Units = player.Units with { UnacquiredSwordmasters = 0 };
        var card = player.Deck.Cards[0];
        Effect upgrade = council ? new HighCouncilEffect() : new SwordmasterEffect();
        var effects = alreadyOwned ? ImmutableArray.Create(upgrade) : [upgrade, new SequenceEffect([upgrade])];
        var rules = CardEffects(Rules(state), card.Definition, agent: effects);
        rules = rules with { Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract") ? x with { Cost = new(Water: 1) } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Illegal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedDuplicatePermanentBranchRejectsBeforePaymentOrDecisionMutation(bool choice)
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        ImmutableArray<Effect> invalid = [new SwordmasterEffect(), new SwordmasterEffect()];
        Effect branch = choice ? new ChoiceEffect([new("upgrade", new(Water: 1), invalid), new("decline", new(), [])])
            : new OptionalPaymentEffect(new(Water: 1), invalid);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [branch]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, branch.GetType());
        Reject(engine, new ChooseOptionAction(0, choice ? 0 : 1), StepStatus.Illegal);
        Apply(engine, new ChooseOptionAction(0, choice ? 1 : 0));
        Assert.Equal(1, state.Players[0].Resources.Water);
    }

    [Fact]
    public void MissingOptionalAcquisitionCanBeDeclinedWhileMandatoryAcquisitionRejects()
    {
        var state = CreateState();
        var reserve = state.Shared.Reserves[0];
        var target = reserve.Cards[0].Definition;
        foreach (var card in reserve.Cards.ToArray()) reserve.MoveTo(card.Id, state.Shared.Trashed);
        var played = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), played.Definition,
            agent: [new OptionalPaymentEffect(new(Water: 1), [new AcquireEffect(target)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")));
        Resolve(engine, typeof(OptionalPaymentEffect));
        Reject(engine, new ChooseOptionAction(0, 1), StepStatus.Unsupported);
        Assert.Equal([new ChooseOptionAction(0, 0)], engine.LegalActions(0));
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(1, state.Players[0].Resources.Water);

        var other = CreateState();
        var stock = other.Shared.Reserves[0];
        foreach (var card in stock.Cards.ToArray()) stock.MoveTo(card.Id, other.Shared.Trashed);
        var mandatory = new TurnEngine(other, CardEffects(Rules(other), played.Definition, agent: [new AcquireEffect(target)]));
        Apply(mandatory, new StartRoundAction());
        Reject(mandatory, new PlaceAgentAction(0, Card(played), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void UnsupportedOptionalChildCannotBeLaunderedByDeclining()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new OptionalPaymentEffect(new(), [new UnsupportedEffect("unimplemented plot")])]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is PlaceAgentAction p && p.Card == Card(card));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcquireAndSignetCyclesRemainLocalSupportFailures(bool signet)
    {
        var state = CreateState();
        var played = state.Players[0].Deck.Cards[0];
        var target = state.Shared.Reserves[0].Cards[0].Definition;
        var rules = CardEffects(Rules(state), played.Definition, agent: signet ? [new SignetEffect()] : [new AcquireEffect(target)]);
        rules = signet
            ? rules with { Leaders = [.. rules.Leaders.Select(x => x.Id == state.Players[0].Leader ? x with { SignetEffects = [new SignetEffect()] } : x)] }
            : CardEffects(rules, target, acquire: [new AcquireEffect(target)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")), StepStatus.ContentError);
        Assert.Contains(engine.LegalActions(0), x => x is PlaceAgentAction p && p.Card.Definition != played.Definition);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MalformedGraphsAreDiagnosedWithoutConstructorOrCanonicalSerializationFailure(int kind)
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        Effect graph = kind switch
        {
            0 => new GainEffect((ResourceKind)999, 1),
            1 => new OptionalPaymentEffect(new(Water: -1), []),
            _ => new GainEffect(ResourceKind.Water, 1),
        };
        if (kind == 2) for (var i = 0; i < 40; i++) graph = new SequenceEffect([graph]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [graph]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.ContentError);
        Assert.Contains(engine.LegalActions(0), x => x is PlaceAgentAction p && p.Card.Definition != card.Definition);
    }

    [Fact]
    public void ValidatorCachesImmutableShapeDiagnosticsAndAllowsSharedAcyclicNodes()
    {
        var validator = new EffectGraphValidator();
        Effect shared = new GainEffect(ResourceKind.Water, 1);
        Assert.Empty(validator.Validate([new SequenceEffect([shared, shared])]));
        ImmutableArray<Effect> invalid = [new OptionalPaymentEffect(new(Spice: -1), [new DrawEffect(-1)])];
        var first = validator.Validate(invalid);
        Assert.Equal(2, first.Length);
        Assert.True(first == validator.Validate(invalid));
    }

    [Fact]
    public void InvalidBoxMarkersBindNormalizedIdentityAndRetainOriginalDiagnostics()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = Rules(state);
        var first = new TurnEngine(state, CardEffects(rules, card.Definition, agent: [new DrawEffect(-1)]));
        var second = new TurnEngine(CreateState(), CardEffects(rules, card.Definition, agent: [new DrawEffect(-2)]));
        // Invalid payloads sharing a diagnostic intentionally share a nonexecutable marker identity.
        Assert.Equal(first.CaptureReplay().RulesIdentity, second.CaptureReplay().RulesIdentity);
        Apply(first, new StartRoundAction());
        Apply(second, new StartRoundAction());
        var action = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        Assert.Equal("effect", Assert.Single(first.Step(action).Diagnostics).Code);
        Reject(first, action, StepStatus.ContentError);
        Reject(second, action, StepStatus.ContentError);
        Assert.Contains(first.LegalActions(0), x => x is PlaceAgentAction p && p.Card.Definition != card.Definition);
        var validA = new TurnEngine(CreateState(), CardEffects(rules, card.Definition, agent: [new DrawEffect(1)]));
        var validB = new TurnEngine(CreateState(), CardEffects(rules, card.Definition, agent: [new DrawEffect(2)]));
        Assert.NotEqual(validA.CaptureReplay().RulesIdentity, validB.CaptureReplay().RulesIdentity);
        Assert.NotEqual(first.CaptureReplay().RulesIdentity, validA.CaptureReplay().RulesIdentity);
    }

    [Fact]
    public void ActualReferenceCycleIsDiagnosedBeforeNormalizationOrSerialization()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var storage = new Effect[1];
        var cyclic = System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(storage);
        storage[0] = new SequenceEffect(cyclic);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: cyclic));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.ContentError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceBoundsRejectBeforeAutomaticGainsOrRevealMutation(bool reveal)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = int.MaxValue - 1 };
        var card = state.Players[0].Deck.Cards[0];
        ImmutableArray<Effect> gains = [new SequenceEffect([new GainEffect(ResourceKind.Water, 1), new GainEffect(ResourceKind.Water, 1)])];
        var rules = CardEffects(Rules(state), card.Definition, agent: reveal ? [] : gains, reveal: reveal ? gains : []);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, reveal ? new RevealAction(0) : new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.ContentError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidDefinitionEnumsStayLocalToTheirExecutableDefinitions(bool board)
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = Rules(state);
        rules = board ? rules with { Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract") ? x with { Icon = (AgentIcon)999 } : x)] }
            : rules with { Cards = [.. rules.Cards.Select(x => x.Id == card.Definition ? x with { Icons = [(AgentIcon)999] } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.ContentError);
        Assert.Contains(engine.LegalActions(0), x => x is PlaceAgentAction p && p.Space != Space("secure-contract") && p.Card.Definition != card.Definition);
    }

    [Fact]
    public void ClosingOptionalSequenceCannotExposeUnsupportedMandatoryTailAfterMutation()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([new OptionalPaymentEffect(new(), []), new InfluenceEffect(Dune.Engine.Setup.Faction.Emperor, 4)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(GamePhase.PlayerTurns, engine.Phase);
    }

    [Fact]
    public void MissingAcquireChoiceBranchDoesNotBlockViableSelection()
    {
        var state = CreateState();
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var excess in stock.Cards.ToArray()) stock.MoveTo(excess.Id, state.Shared.Trashed);
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new ChoiceEffect([new("missing", new(), [new AcquireEffect(target)]),
                new("viable", new(), [new GainEffect(ResourceKind.Spice, 1)])])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, typeof(ChoiceEffect));
        Reject(engine, new ChooseOptionAction(0, 0), StepStatus.Unsupported);
        Assert.Contains(new ChooseOptionAction(0, 1), engine.LegalActions(0));
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(1, state.Players[0].Resources.Spice);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnselectedCappedResourceBranchCanBeDeclinedOrReplaced(bool choice)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = int.MaxValue };
        var card = state.Players[0].Deck.Cards[0];
        ImmutableArray<Effect> capped = [new GainEffect(ResourceKind.Water, 1)];
        Effect branch = choice ? new ChoiceEffect([new("overflow", new(), capped), new("viable", new(), [new GainEffect(ResourceKind.Spice, 1)])])
            : new OptionalPaymentEffect(new(), capped);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [branch]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, branch.GetType());
        Reject(engine, new ChooseOptionAction(0, choice ? 0 : 1), StepStatus.ContentError);
        Apply(engine, new ChooseOptionAction(0, choice ? 1 : 0));
        Assert.Equal(int.MaxValue, state.Players[0].Resources.Water);
        Assert.Equal(choice ? 1 : 0, state.Players[0].Resources.Spice);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnselectedAcquireBranchDoesNotReserveLastStockButSelectedBranchProtectsMandatoryTail(bool choice)
    {
        var state = CreateState();
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var excess in stock.Cards.Skip(1).ToArray()) stock.MoveTo(excess.Id, state.Shared.Trashed);
        var card = state.Players[0].Deck.Cards[0];
        ImmutableArray<Effect> competing = [new AcquireEffect(target)];
        Effect branch = choice ? new ChoiceEffect([new("compete", new(Water: 1), competing), new("viable", new(), [])])
            : new OptionalPaymentEffect(new(Water: 1), competing);
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([branch, new AcquireEffect(target)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, typeof(SequenceEffect));
        Assert.Single(stock.Cards);
        Reject(engine, new ChooseOptionAction(0, choice ? 0 : 1), StepStatus.Unsupported);
        Assert.Contains(new ChooseOptionAction(0, choice ? 1 : 0), engine.LegalActions(0));
        Apply(engine, new ChooseOptionAction(0, choice ? 1 : 0));
        Assert.Empty(stock.Cards);
        Assert.Contains(state.Players[0].Discard.Cards, x => x.Definition == target);
        Assert.Equal(1, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SelectedPaymentFreesOnlyTheBoundPayersResourceCapacity(bool choice, bool foreignPayer)
    {
        var state = CreateState();
        state.Players[1].Resources = state.Players[1].Resources with { Water = int.MaxValue };
        var card = state.Players[0].Deck.Cards[0];
        var payer = foreignPayer ? 0 : 1;
        ImmutableArray<Effect> refund = [new GainEffect(ResourceKind.Water, 1)];
        Effect branch = choice ? new ForPlayerEffect(1,
            [new ChoiceEffect([new("refund", new(Water: 1), refund), new("decline", new(), [])])])
        { DecisionOwnerId = 2, PayerId = payer }
            : new OptionalPaymentEffect(new(Water: 1), refund, 2) { PayerId = payer, RecipientId = 1 };
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [branch]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, branch.GetType());
        if (choice) Resolve(engine, typeof(ChoiceEffect));
        var selected = new ChooseOptionAction(2, choice ? 0 : 1);
        if (foreignPayer)
        {
            Reject(engine, selected, StepStatus.ContentError);
            Apply(engine, new ChooseOptionAction(2, choice ? 1 : 0));
        }
        else
        {
            Assert.Contains(selected, engine.LegalActions(2));
            Apply(engine, selected);
        }
        Assert.Equal(int.MaxValue, state.Players[1].Resources.Water);
        Assert.Equal(1, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllUnavailableStockChoiceRejectsBeforeItsDecisionOrEnclosingPayment(bool optional)
    {
        var state = CreateState();
        var stocks = state.Shared.Reserves.Take(2).ToArray();
        var targets = stocks.Select(x => x.Cards[0].Definition).ToArray();
        foreach (var stock in stocks)
            foreach (var card in stock.Cards.ToArray()) stock.MoveTo(card.Id, state.Shared.Trashed);
        var played = state.Players[0].Deck.Cards[0];
        Effect choice = new ChoiceEffect([new("missing1", new(), [new AcquireEffect(targets[0])]),
            new("missing2", new(), [new AcquireEffect(targets[1])])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), played.Definition, agent:
            optional ? [new OptionalPaymentEffect(new(Water: 1), [choice])] : [new SequenceEffect([choice])]));
        Apply(engine, new StartRoundAction());
        var place = new PlaceAgentAction(0, Card(played), Space("secure-contract"));
        if (optional)
        {
            Apply(engine, place);
            Resolve(engine, typeof(OptionalPaymentEffect));
            Reject(engine, new ChooseOptionAction(0, 1), StepStatus.Unsupported);
            Apply(engine, new ChooseOptionAction(0, 0));
            Assert.Equal(1, state.Players[0].Resources.Water);
        }
        else Reject(engine, place, StepStatus.Unsupported);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Decisions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedPlacementPaymentsFreeCapacityBeforeMandatoryRefund(bool option)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = int.MaxValue };
        var card = state.Players[0].Deck.Cards[0];
        var rules = Rules(state);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract") ? x with
        {
            Cost = new(Water: 1), Effects = [new GainEffect(ResourceKind.Water, option ? 2 : 1)],
            PlacementOptions = option ? [new("pay", new(Water: 1), [])] : [],
        } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Assert.Equal(int.MaxValue, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void SelectedPurchasePaymentFreesPersuasionCapacityBeforeAcquireRefund()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = Rules(state);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x with { RevealEffects = [] })] };
        rules = CardEffects(rules, card.Definition, reveal: [new GainEffect(ResourceKind.Persuasion, int.MaxValue)]);
        rules = CardEffects(rules, market.Definition, acquire: [new GainEffect(ResourceKind.Persuasion, 2)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        ResolveAll(engine);
        Assert.Equal(int.MaxValue, state.Players[0].Temporary.Persuasion);
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Assert.Equal(int.MaxValue, state.Players[0].Temporary.Persuasion);
        Assert.Contains(market, state.Players[0].Discard.Cards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllCappedMandatoryChoiceOptionsRejectBeforeDecisionOrAutomaticPrefix(bool prefix)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = prefix ? int.MaxValue - 1 : int.MaxValue };
        var card = state.Players[0].Deck.Cards[0];
        Effect choice = new ChoiceEffect([new("one", new(), [new GainEffect(ResourceKind.VictoryPoints, 1)]),
            new("two", new(), [new GainEffect(ResourceKind.VictoryPoints, 2)])]);
        var rules = CardEffects(Rules(state), card.Definition, agent:
            prefix ? [new SequenceEffect([new GainEffect(ResourceKind.VictoryPoints, 1), choice])] : [choice]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Decisions);
        Assert.Equal(prefix ? int.MaxValue - 1 : int.MaxValue, state.Players[0].Resources.VictoryPoints);
    }

    [Fact]
    public void OptionalImpossibleCapChoiceCanBeDeclinedBeforePayment()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new OptionalPaymentEffect(new(Water: 1), [new ChoiceEffect([
                new("one", new(), [new GainEffect(ResourceKind.VictoryPoints, 1)]),
                new("two", new(), [new GainEffect(ResourceKind.VictoryPoints, 2)])])])]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, typeof(OptionalPaymentEffect));
        Reject(engine, new ChooseOptionAction(0, 1), StepStatus.Unsupported);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(1, state.Players[0].Resources.Water);
    }

    [Fact]
    public void ChoiceViabilityRequiresTheSameOptionToSatisfyStockAndCapacity()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue };
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var card in stock.Cards.ToArray()) stock.MoveTo(card.Id, state.Shared.Trashed);
        var played = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), played.Definition, agent:
            [new ChoiceEffect([new("stock", new(), [new AcquireEffect(target)]),
                new("cap", new(), [new GainEffect(ResourceKind.VictoryPoints, 1)])])]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void UnprovenJointMandatoryChoiceStockIsExplicitlyUnsupportedBeforeActivation()
    {
        var state = CreateState();
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var card in stock.Cards.Skip(1).ToArray()) stock.MoveTo(card.Id, state.Shared.Trashed);
        var played = state.Players[0].Deck.Cards[0];
        var first = new ChoiceEffect([new("take", new(), [new AcquireEffect(target)])]);
        var second = new ChoiceEffect([new("take", new(), [new AcquireEffect(target)])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), played.Definition, agent: [first, second]));
        Apply(engine, new StartRoundAction());
        var action = new PlaceAgentAction(0, Card(played), Space("secure-contract"));
        Assert.Equal("choice-stock-ambiguous", Assert.Single(engine.Step(action).Diagnostics).Code);
        Reject(engine, action, StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void JointChoiceCapacityAndPermanentCompetitionNeedsAProvenSafeAlternative(bool permanent, bool emptyAlternative)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue - 1 };
        var card = state.Players[0].Deck.Cards[0];
        Effect reward = permanent ? new SwordmasterEffect() : new GainEffect(ResourceKind.VictoryPoints, 1);
        ChoiceEffect Choice() => new(emptyAlternative ? [new("reward", new(), [reward]), new("leave", new(), [])]
            : [new("reward", new(), [reward])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [Choice(), Choice()]));
        Apply(engine, new StartRoundAction());
        var place = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        if (!emptyAlternative)
        {
            Assert.Equal("choice-joint-ambiguous", Assert.Single(engine.Step(place).Diagnostics).Code);
            Reject(engine, place, StepStatus.Unsupported);
            return;
        }
        Apply(engine, place);
        Apply(engine, new ResolveEffectAction(0, engine.Observe(0).PendingEffects.First(x => x.Kind == nameof(ChoiceEffect)).Id));
        Apply(engine, new ChooseOptionAction(0, 0));
        Resolve(engine, typeof(ChoiceEffect));
        Reject(engine, new ChooseOptionAction(0, 0), permanent ? StepStatus.Illegal : StepStatus.ContentError);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void JointStockChoicesWithFreeEmptyAlternativesRemainResolvable()
    {
        var state = CreateState();
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var excess in stock.Cards.Skip(1).ToArray()) stock.MoveTo(excess.Id, state.Shared.Trashed);
        var card = state.Players[0].Deck.Cards[0];
        ChoiceEffect Choice() => new([new("take", new(), [new AcquireEffect(target)]), new("leave", new(), [])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [Choice(), Choice()]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Apply(engine, new ResolveEffectAction(0, engine.Observe(0).PendingEffects.First(x => x.Kind == nameof(ChoiceEffect)).Id));
        Apply(engine, new ChooseOptionAction(0, 0));
        Resolve(engine, typeof(AcquireEffect));
        Resolve(engine, typeof(ChoiceEffect));
        Reject(engine, new ChooseOptionAction(0, 0), StepStatus.Unsupported);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Empty(stock.Cards);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NestedCompulsoryChoicesDoNotHideJointCompetition(int kind)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue - 1 };
        var stock = state.Shared.Reserves[0];
        var target = stock.Cards[0].Definition;
        foreach (var excess in stock.Cards.Skip(1).ToArray()) stock.MoveTo(excess.Id, state.Shared.Trashed);
        var card = state.Players[0].Deck.Cards[0];
        Effect reward = kind switch { 0 => new GainEffect(ResourceKind.VictoryPoints, 1), 1 => new SwordmasterEffect(), _ => new AcquireEffect(target) };
        var inner = new ChoiceEffect([new("reward", new(), [reward])]);
        var outer = new ChoiceEffect([new("inner", new(), [inner])]);
        var sibling = new ChoiceEffect([new("reward", new(), [reward])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [outer, sibling]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SoleUnaffordableChoiceRejectsBeforeEnclosingPlacementOrItsPayment(bool placementCost)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = placementCost ? 1 : 0 };
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new ChoiceEffect([new("pay", new(Water: 1), [])])]);
        if (placementCost) rules = rules with { Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract") ? x with { Cost = new(Water: 1) } : x)] };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void MixedAffordableAndUnaffordableChoiceOptionsKeepTheAffordableBranchLegal()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 0 };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new ChoiceEffect([new("unaffordable", new(Water: 1), []),
                new("free", new(), [new GainEffect(ResourceKind.Spice, 1)])])]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, typeof(ChoiceEffect));
        Reject(engine, new ChooseOptionAction(0, 0), StepStatus.Illegal);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(1, state.Players[0].Resources.Spice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChoiceAffordabilityUsesTheBoundForeignPayer(bool affordable)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 5 };
        state.Players[1].Resources = state.Players[1].Resources with { Water = affordable ? 1 : 0 };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new ForPlayerEffect(0, [new ChoiceEffect([new("pay", new(Water: 1), [new GainEffect(ResourceKind.Spice, 1)])])])
            { DecisionOwnerId = 2, PayerId = 1 }]));
        Apply(engine, new StartRoundAction());
        var place = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        if (!affordable) { Reject(engine, place, StepStatus.Unsupported); return; }
        Apply(engine, place);
        Resolve(engine, typeof(ForPlayerEffect));
        Resolve(engine, typeof(ChoiceEffect));
        Apply(engine, new ChooseOptionAction(2, 0));
        Assert.Equal(0, state.Players[1].Resources.Water);
        Assert.Equal(5, state.Players[0].Resources.Water);
        Assert.Equal(1, state.Players[0].Resources.Spice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyAnEarlierAutomaticSequenceGainCanFundTheFollowingChoice(bool gainFirst)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 0 };
        var card = state.Players[0].Deck.Cards[0];
        Effect gain = new GainEffect(ResourceKind.Water, 1);
        Effect choice = new ChoiceEffect([new("pay", new(Water: 1), [])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect(gainFirst ? [gain, choice] : [choice, gain])]));
        Apply(engine, new StartRoundAction());
        var place = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        if (!gainFirst) { Reject(engine, place, StepStatus.Unsupported); return; }
        Apply(engine, place);
        Resolve(engine, typeof(SequenceEffect));
        Assert.Equal(1, state.Players[0].Resources.Water);
        Assert.Contains(new ChooseOptionAction(0, 0), engine.LegalActions(0));
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(0, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void PlayerOrderedPeerFundingIsExplicitlyUnsupportedBeforePlacement()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 0 };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new GainEffect(ResourceKind.Water, 1), new ChoiceEffect([new("pay", new(Water: 1), [])])]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void EnclosingOptionalPaymentCannotAlsoFundItsMandatoryChildChoice()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new OptionalPaymentEffect(new(Water: 1), [new ChoiceEffect([new("pay again", new(Water: 1), [])])])]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve(engine, typeof(OptionalPaymentEffect));
        Reject(engine, new ChooseOptionAction(0, 1), StepStatus.Unsupported);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(1, state.Players[0].Resources.Water);
    }

    [Fact]
    public void AChoiceRewardCannotFundItsOwnPayment()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 0 };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new ChoiceEffect([new("refund", new(Water: 1), [new GainEffect(ResourceKind.Water, 1)])])]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CompulsoryPaidChoicesMustNotCompeteForTheSameFunds(int water)
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = water };
        var card = state.Players[0].Deck.Cards[0];
        ChoiceEffect Choice() => new([new("pay", new(Water: 1), [])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [Choice(), Choice()]));
        Apply(engine, new StartRoundAction());
        var place = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        if (water == 1) { Reject(engine, place, StepStatus.Unsupported); return; }
        Apply(engine, place);
        Apply(engine, new ResolveEffectAction(0, engine.Observe(0).PendingEffects.First(x => x.Kind == nameof(ChoiceEffect)).Id));
        Apply(engine, new ChooseOptionAction(0, 0));
        Resolve(engine, typeof(ChoiceEffect));
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(0, state.Players[0].Resources.Water);
    }

    [Fact]
    public void PaidChoicesWithFreeEmptyAlternativesCanSpendThenDecline()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        ChoiceEffect Choice() => new([new("pay", new(Water: 1), []), new("leave", new(), [])]);
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [Choice(), Choice()]));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Apply(engine, new ResolveEffectAction(0, engine.Observe(0).PendingEffects.First(x => x.Kind == nameof(ChoiceEffect)).Id));
        Apply(engine, new ChooseOptionAction(0, 0));
        Resolve(engine, typeof(ChoiceEffect));
        Reject(engine, new ChooseOptionAction(0, 0), StepStatus.Illegal);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(0, state.Players[0].Resources.Water);
    }

    [Fact]
    public void FundingAfterAnEarlierDecisionBoundaryIsExplicitlyUnsupported()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Water = 0 };
        var card = state.Players[0].Deck.Cards[0];
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([new OptionalPaymentEffect(new(), []), new GainEffect(ResourceKind.Water, 1),
                new ChoiceEffect([new("pay", new(Water: 1), [])])])]));
        Apply(engine, new StartRoundAction());
        Reject(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")), StepStatus.Unsupported);
    }

    private static void Resolve(TurnEngine engine, Type effect)
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new ResolveEffectAction(owner, engine.Observe(owner).PendingEffects.Single(x => x.Kind == effect.Name).Id));
    }

    private static void Reject(TurnEngine engine, GameAction action, StepStatus status)
    {
        var before = engine.CaptureTrustedSnapshot();
        Assert.Equal(status, engine.Step(action).Status);
        var after = engine.CaptureTrustedSnapshot();
        Assert.Equal(before.CanonicalJson, after.CanonicalJson);
        Assert.Equal(before.Hash, after.Hash);
        Assert.Equal(before.Data.Game.MainRandom, after.Data.Game.MainRandom);
        Assert.Equal(before.Data.Actions, after.Data.Actions);
        Assert.DoesNotContain(action, engine.LegalActions(action.PlayerId));
    }
}
