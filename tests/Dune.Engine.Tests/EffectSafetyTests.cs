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
