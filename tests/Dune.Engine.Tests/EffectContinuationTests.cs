using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

/// <summary>Synthetic effect graphs that exercise causal execution, not official card text.</summary>
public sealed class EffectContinuationTests
{
    [Fact]
    public void SequenceChildCompletesBeforeOuterSibling()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
        [new SequenceEffect([new GainEffect(ResourceKind.Water, 2),
            new ForPlayerEffect(2, [new ChoiceEffect([new("reward", new(), [new GainEffect(ResourceKind.Spice, 1)])])]),
            new GainEffect(ResourceKind.Water, 3)]), new GainEffect(ResourceKind.Solari, 7)]);
        rules = WithoutBoardEffects(rules);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        var outer = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Amount: 7 });
        Resolve<SequenceEffect>(engine);
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Empty(engine.LegalActions(0));
        AssertRejectedWithoutMutation(engine, new ResolveEffectAction(0, outer.Id));
        Resolve<ChoiceEffect>(engine);
        Assert.Equal(3, state.Players[0].Resources.Water);
        Apply(engine, new ChooseOptionAction(2, 0));
        Assert.Equal(1, state.Players[2].Resources.Spice);
        Assert.Equal(6, state.Players[0].Resources.Water);
        Assert.Equal(7, state.Players[0].Resources.Solari);
        Assert.Equal(1, engine.CurrentPlayerId);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Frames);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void PlayerOrderedSiblingsCanFundOptionalPayment()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
        [new PlayerOrderedEffect([new OptionalPaymentEffect(new(Solari: 3), [new GainEffect(ResourceKind.Water, 2)]),
            new GainEffect(ResourceKind.Solari, 3)])]));
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<PlayerOrderedEffect>(engine);
        Resolve<GainEffect>(engine);
        Assert.Equal(3, state.Players[0].Resources.Solari);
        Resolve<OptionalPaymentEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void ForeignChooserCanPayFromInitiatorAndRewardRecipient()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Solari = 3 };
        var card = state.Players[0].Deck.Cards[0];
        var rules = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
        [new OptionalPaymentEffect(new(Solari: 3), [new GainEffect(ResourceKind.Water, 2)], DecisionOwnerId: 2)
        { PayerId = 0, RecipientId = 1 }]));
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<OptionalPaymentEffect>(engine);
        var context = engine.CaptureTrustedSnapshot().Data.Decisions.Single().Context;
        Assert.Equal(0, context.InitiatorId);
        Assert.Equal(2, context.DecisionOwnerId);
        Assert.Equal(0, context.PayerId);
        Assert.Equal(1, context.RecipientId);
        Assert.Empty(engine.LegalActions(0));
        Assert.Null(engine.Observe(1).Decision);
        Assert.Contains(new ChooseOptionAction(2, 1), engine.LegalActions(2));
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(3, state.Players[1].Resources.Water);
        Assert.Equal(1, state.Players[2].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void NestedAcquisitionBlocksOuterResolveAndPurchasesUntilChildrenFinish()
    {
        var state = CreateState();
        var first = state.Players[0].Deck.Cards[0];
        var market = state.Shared.ImperiumRow.Cards[0];
        var reserve = new ContentId("imperium:reserve:spice-must-flow:v1");
        var rules = Rules(state);
        rules = rules with { Cards = [.. rules.Cards.Select(x => x with { RevealEffects = [] })] };
        rules = CardEffects(rules, first.Definition, reveal:
            [new GainEffect(ResourceKind.Persuasion, 5), new GainEffect(ResourceKind.Solari, 7)]);
        rules = CardEffects(rules, market.Definition, acquire:
            [new SequenceEffect([new AcquireEffect(reserve), new GainEffect(ResourceKind.Water, 3)])]);
        rules = CardEffects(rules, reserve, acquire:
            [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Spice, 2)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        var outer = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Amount: 7 });
        var persuasion = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Resource: ResourceKind.Persuasion });
        Apply(engine, new ResolveEffectAction(0, persuasion.Id));
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Resolve<SequenceEffect>(engine);
        Assert.Equal(0, state.Players[0].Resources.Spice);
        Assert.Equal(1, state.Players[0].Resources.Water);
        var buy = new AcquireCardAction(0, Card(state.Shared.ImperiumRow.Cards[0]), state.Shared.ImperiumRow.Id);
        AssertRejectedWithoutMutation(engine, new ResolveEffectAction(0, outer.Id));
        AssertRejectedWithoutMutation(engine, buy);
        AssertRejectedWithoutMutation(engine, new EndRevealAction(0));
        Resolve<OptionalPaymentEffect>(engine);
        var snapshot = engine.CaptureTrustedSnapshot();
        Assert.True(snapshot.Data.Frames.Length >= 4);
        Assert.Equal(snapshot.Data.Frames[^2].Id, snapshot.Data.Frames[^1].ParentId);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(2, state.Players[0].Resources.Spice);
        Assert.Equal(4, state.Players[0].Resources.Water);
        Assert.Contains(new ResolveEffectAction(0, outer.Id), engine.LegalActions(0));
        Assert.Contains(engine.LegalActions(0), x => x is AcquireCardAction);
    }

    [Fact]
    public void GroupDefinitionsAndTrustedFramesRoundTripWithExplicitAllowlist()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([new PlayerOrderedEffect([new OptionalPaymentEffect(new(), [])])])]));
        var effects = rules.Cards.Single(x => x.Id == card.Definition).AgentEffects;
        var json = JsonSerializer.Serialize(effects, ConfigurationJson.Options);
        var restored = JsonSerializer.Deserialize<ImmutableArray<Effect>>(json, ConfigurationJson.Options);
        var engine = new TurnEngine(state, CardEffects(rules, card.Definition, agent: restored));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<SequenceEffect>(engine);
        Resolve<OptionalPaymentEffect>(engine);
        var snapshot = engine.CaptureTrustedSnapshot();
        var copy = JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options)!;
        Assert.Equal(snapshot.CanonicalJson, JsonSerializer.Serialize(copy, ConfigurationJson.Options));
        Assert.Equal("dune-turn-snapshot-v7", copy.Schema);
        Assert.Equal("dune-turn-replay-v7", engine.CaptureReplay().Schema);
    }

    [Fact]
    public void ForPlayerOverridesBindChoicePaymentAndRewardSeparately()
    {
        var state = CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Solari = 3 };
        var card = state.Players[0].Deck.Cards[0];
        var rules = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
        [new ForPlayerEffect(1, [new ChoiceEffect([new("pay", new(Solari: 3), [new GainEffect(ResourceKind.Water, 2)])])])
        { DecisionOwnerId = 2, PayerId = 0 }]));
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<ForPlayerEffect>(engine);
        Resolve<ChoiceEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        Apply(engine, new ChooseOptionAction(2, 0));
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(3, state.Players[1].Resources.Water);
        Assert.Equal(1, state.Players[2].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void ForeignChildReturnsToItsAgentDeploymentContinuation()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
        [new ForPlayerEffect(0, [new ChoiceEffect([new("recruit", new(), [new RecruitEffect(2)])])])
        { DecisionOwnerId = 2 }]));
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("arrakeen")));
        Resolve<ForPlayerEffect>(engine);
        AssertRejectedWithoutMutation(engine, new DeployAction(0, 0));
        Resolve<ChoiceEffect>(engine);
        Apply(engine, new ChooseOptionAction(2, 0));
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.Single(engine.CaptureTrustedSnapshot().Data.Frames);
        Assert.Equal(ExecutionContinuationKind.AgentTurn, engine.CaptureTrustedSnapshot().Data.Frames[0].Continuation.Kind);
        Assert.Contains(new DeployAction(0, 4), engine.LegalActions(0));
        Apply(engine, new DeployAction(0, 4));
        Assert.Equal(4, state.Players[0].Units.TroopsInConflict);
        Assert.Equal(1, engine.CurrentPlayerId);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Frames);
    }

    [Fact]
    public void RootSourcesRemainDistinctAndNestedSelfTrashUsesItsPhysicalCard()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([new OptionalPaymentEffect(new(), [new TrashSelfEffect()])])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        var root = engine.CaptureTrustedSnapshot().Data.Frames.Single();
        var cardWork = root.Remaining.Single(x => x.Effect is SequenceEffect);
        var boardWork = root.Remaining.Single(x => x.Effect is GainEffect);
        Assert.Equal(card.Id, cardWork.Context.SourceCard);
        Assert.Equal(card.Definition, cardWork.Context.SourceDefinition);
        Assert.Equal(EffectProvenance.Card, cardWork.Context.Provenance);
        Assert.Null(boardWork.Context.SourceCard);
        Assert.Equal(Space("secure-contract"), boardWork.Context.Space);
        Assert.Equal(EffectProvenance.Board, boardWork.Context.Provenance);
        Resolve<SequenceEffect>(engine);
        var decision = engine.CaptureTrustedSnapshot().Data.Decisions.Single();
        Assert.Equal(card.Id, decision.Context.SourceCard);
        Apply(engine, new ChooseOptionAction(0, 1));
        Resolve<TrashSelfEffect>(engine);
        Assert.Contains(card, state.Shared.Trashed.Cards);
        Assert.Equal(3, state.Players[0].Resources.Solari);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void InvalidRoleOverridesRejectBeforeMutation(bool forPlayer, int role)
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        int? chooser = role == 0 ? -1 : null;
        int? payer = role == 1 ? -1 : null;
        int? recipient = role == 2 ? -1 : null;
        Effect effect = forPlayer
            ? new ForPlayerEffect(0, [new SignetEffect()]) { DecisionOwnerId = chooser, PayerId = payer, RecipientId = recipient }
            : new OptionalPaymentEffect(new(), [new SignetEffect()], chooser) { PayerId = payer, RecipientId = recipient };
        var engine = new TurnEngine(state, CardEffects(Rules(state), card.Definition, agent: [effect]));
        Apply(engine, new StartRoundAction());
        var before = engine.CaptureTrustedSnapshot().Hash;
        var result = engine.Step(new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Assert.Equal(StepStatus.ContentError, result.Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void DefaultOptionalArraysNormalizeOnceAndMatchExplicitEmptyIdentity()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var defaults = WithoutBoardEffects(CardEffects(Rules(state), card.Definition, agent:
            [new SequenceEffect([new PlayerOrderedEffect(default), new ForPlayerEffect(1, default),
                new OptionalPaymentEffect(new(), default)])]));
        var explicitEmpty = CardEffects(defaults, card.Definition, agent:
            [new SequenceEffect([new PlayerOrderedEffect([]), new ForPlayerEffect(1, []),
                new OptionalPaymentEffect(new(), [])])]);
        var engine = new TurnEngine(state, defaults);
        var other = new TurnEngine(CreateState(), explicitEmpty);
        Assert.Equal(other.CaptureReplay().RulesIdentity, engine.CaptureReplay().RulesIdentity);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Resolve<SequenceEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(1, engine.CurrentPlayerId);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), explicitEmpty).IsSuccess);
    }

    private static TurnRuleSet WithoutBoardEffects(TurnRuleSet rules) => rules with
    { Spaces = [.. rules.Spaces.Select(x => x with { Effects = [] })] };

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        var effect = engine.Observe(owner).PendingEffects.Single(x => x.Kind == typeof(T).Name);
        Apply(engine, new ResolveEffectAction(owner, effect.Id));
    }

    private static void AssertRejectedWithoutMutation(TurnEngine engine, GameAction action)
    {
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }
}
