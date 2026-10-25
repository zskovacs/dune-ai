using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class SelectedTrashTests
{
    [Fact]
    public void HandDiscardAndInPlayTargetsUseVisibleZoneSelectors()
    {
        var (state, engine, rules) = Ready([new SelectedTrashEffect([])], discard: true);
        var player = state.Players[0];
        var played = player.Hand.Cards[0];
        Apply(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")));
        Assert.Contains(played, player.InPlay.Cards);
        Resolve<SelectedTrashEffect>(engine);
        var actions = engine.LegalActions(0).OfType<SelectTrashCardAction>().ToArray();
        Assert.Equal(new[] { player.Hand.Id, player.Discard.Id, player.InPlay.Id }, actions.Select(x => x.Source).Distinct());
        Assert.Equal(5, actions.Length);
        Assert.DoesNotContain("instance", JsonSerializer.Serialize(engine.Observe(0), ConfigurationJson.Options), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targets", JsonSerializer.Serialize(engine.Observe(0).Decision, ConfigurationJson.Options), StringComparison.OrdinalIgnoreCase);
        var snapshot = engine.CaptureTrustedSnapshot();
        Assert.Equal(snapshot.CanonicalJson, JsonSerializer.Serialize(
            JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options), ConfigurationJson.Options));
        var prefix = engine.CaptureReplay();
        foreach (var action in actions)
        {
            var replay = TurnEngine.Replay(prefix, () => ReadyState(discard: true), rules);
            Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics));
            Apply(replay.Engine!, action);
        }
    }

    [Fact]
    public void DuplicateCopyChoiceMovesExactlyItsBoundInstance()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([])]);
        var player = state.Players[0];
        var duplicate = AddDuplicates(state);
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var selection = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = selection.Targets.Single(x => x.Instance == duplicate.Id);
        Assert.Equal(1, target.Card.Copy);
        var before = Inventory(state);
        var action = new SelectTrashCardAction(0, selection.Id, target.Source, target.Card);
        Apply(engine, action);
        Assert.Contains(duplicate, state.Shared.Trashed.Cards);
        Assert.DoesNotContain(duplicate, player.Hand.Cards);
        Assert.Contains(player.Hand.Cards, x => x.Definition == duplicate.Definition);
        Assert.Equal(before, Inventory(state));
        RejectUnchanged(engine, action, StepStatus.Illegal);
    }

    [Fact]
    public void OldDecisionAndCompactedDuplicateSelectorsAreRejected()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([]), new SelectedTrashEffect([])]);
        var player = state.Players[0];
        var duplicate = AddDuplicates(state);
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var old = engine.LegalActions(0).OfType<SelectTrashCardAction>().First(x => x.Card.Definition == duplicate.Definition && x.Card.Copy == 0);
        var original = player.Hand.Cards.First(x => x.Definition == duplicate.Definition);
        player.Hand.MoveTo(original.Id, player.Discard);
        RejectUnchanged(engine, old, StepStatus.Illegal);
        Assert.DoesNotContain(old, engine.LegalActions(0));
        Apply(engine, new DeclineTrashAction(0, old.DecisionId));
        Resolve<SelectedTrashEffect>(engine);
        Assert.NotEqual(old.DecisionId, engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!.Id);
        RejectUnchanged(engine, old, StepStatus.Illegal);
        RejectUnchanged(engine, new DeclineTrashAction(0, old.DecisionId), StepStatus.Illegal);
    }

    [Fact]
    public void InvalidTargetMatrixLeavesTrustedStateRngFramesAndHistoryUnchanged()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([], Optional: false)]);
        RejectUnchanged(engine, new SelectTrashCardAction(0, 1, state.Players[0].Hand.Id, Card(state.Players[0].Hand.Cards[0])), StepStatus.Illegal);
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var valid = engine.LegalActions(0).OfType<SelectTrashCardAction>().First();
        foreach (var action in new GameAction[]
        {
            valid with { Source = new("unknown") },
            valid with { Source = state.Players[1].Hand.Id },
            valid with { Source = state.Players[0].Deck.Id },
            valid with { Source = state.Players[0].Intrigue.Id },
            valid with { Source = state.Shared.ImperiumRow.Id },
            valid with { Card = valid.Card with { Copy = -1 } },
            valid with { Card = valid.Card with { Copy = 999 } },
            valid with { Card = new(new("absent")) },
            valid with { PlayerId = 1 },
            valid with { DecisionId = valid.DecisionId - 1 },
            valid with { Card = null! },
            new DeclineTrashAction(0, valid.DecisionId),
            new ChooseOptionAction(0, 0),
            new RevealAction(0),
            new StartRoundAction(),
        }) RejectUnchanged(engine, action, StepStatus.Illegal);
        var binding = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!.Targets
            .Single(x => x.Source == valid.Source && x.Card == valid.Card);
        state.Players[0].Hand.MoveTo(binding.Instance, state.Players[0].Discard);
        RejectUnchanged(engine, valid, StepStatus.Illegal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalDeclineWorksWithoutTargetsOrSupportedSuccess(bool unsupportedSuccess)
    {
        ImmutableArray<Effect> benefit = unsupportedSuccess ? [new UnsupportedEffect("private unsupported detail")] : [];
        var (state, engine, _) = Ready([new ForPlayerEffect(1, [new SelectedTrashEffect(benefit)])]);
        var player = state.Players[1];
        if (!unsupportedSuccess)
            foreach (var card in player.Hand.Cards.ToArray()) player.Hand.MoveTo(card.Id, player.Deck);
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Resolve<ForPlayerEffect>(engine);
        Resolve<SelectedTrashEffect>(engine);
        var legal = engine.LegalActions(1);
        var decline = Assert.IsType<DeclineTrashAction>(Assert.Single(legal));
        if (unsupportedSuccess)
        {
            var decision = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
            var binding = decision.Targets[0];
            RejectUnchanged(engine, new SelectTrashCardAction(1, decision.Id, binding.Source, binding.Card), StepStatus.Unsupported);
            Assert.DoesNotContain(engine.SupportDiagnostics(1), x => x.Message.Contains("private", StringComparison.Ordinal));
        }
        Apply(engine, decline);
        Assert.Empty(state.Shared.Trashed.Cards);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Decisions);
    }

    [Fact]
    public void MandatoryTrashWithoutTargetsFailsBeforeActivation()
    {
        var (state, engine, _) = Ready([new ForPlayerEffect(1, [new SelectedTrashEffect([], Optional: false)])]);
        foreach (var card in state.Players[1].Hand.Cards.ToArray()) state.Players[1].Hand.MoveTo(card.Id, state.Players[1].Deck);
        RejectUnchanged(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void ForeignSelectionPreservesInitiatorPayerRecipientAndSource()
    {
        var (state, engine, rules) = Ready([new ForPlayerEffect(1,
            [new SelectedTrashEffect([new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])])])
        { RecipientId = 2 }, new GainEffect(ResourceKind.Solari, 3)]);
        var played = state.Players[0].Hand.Cards[0];
        Apply(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")));
        Resolve<ForPlayerEffect>(engine);
        Resolve<SelectedTrashEffect>(engine);
        var action = engine.LegalActions(1).OfType<SelectTrashCardAction>().First();
        var binding = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!.Targets
            .Single(x => x.Source == action.Source && x.Card == action.Card);
        var inventory = Inventory(state);
        Apply(engine, action);
        Assert.Contains(state.Shared.Trashed.Cards, x => x.Id == binding.Instance);
        Assert.Equal(inventory, Inventory(state));
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Resolve<OptionalPaymentEffect>(engine);
        var context = engine.CaptureTrustedSnapshot().Data.Decisions.Single().Context;
        Assert.Equal(0, context.InitiatorId);
        Assert.Equal(1, context.DecisionOwnerId);
        Assert.Equal(1, context.PayerId);
        Assert.Equal(2, context.RecipientId);
        Assert.Equal(played.Id, context.SourceCard);
        Assert.Equal(played.Definition, context.SourceDefinition);
        Assert.Equal(Space("secure-contract"), context.Space);
        Assert.Equal(EffectProvenance.Card, context.Provenance);
        Assert.Equal(new(binding.Instance, binding.Card.Definition, binding.Source, 1), context.SelectedTarget);
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Empty(engine.LegalActions(0));
        RejectUnchanged(engine, new ChooseOptionAction(0, 1), StepStatus.Illegal);
        Apply(engine, new ChooseOptionAction(1, 1));
        Assert.Equal(3, state.Players[2].Resources.Water);
        Assert.Equal(3, state.Players[0].Resources.Solari);
        var replay = TurnEngine.Replay(engine.CaptureReplay(), () => ReadyState(), rules);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, replay.Engine!.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void SelectedTrashInsideForeignPaymentKeepsItsChooserAndSeparateRecipient()
    {
        var (state, engine, _) = Ready([new ForPlayerEffect(1, [new OptionalPaymentEffect(new(),
            [new SelectedTrashEffect([new GainEffect(ResourceKind.Water, 1)])]) { RecipientId = 2 }])]);
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Resolve<ForPlayerEffect>(engine);
        Resolve<OptionalPaymentEffect>(engine);
        Apply(engine, new ChooseOptionAction(1, 1));
        Resolve<SelectedTrashEffect>(engine);
        Apply(engine, engine.LegalActions(1).OfType<SelectTrashCardAction>().First());
        Assert.Equal(2, state.Players[2].Resources.Water);
        Assert.Equal(1, state.Players[1].Resources.Water);
    }

    [Fact]
    public void SelectedSuccessReservesGeneratedThresholdChildrenAlongsideSuspendedParentWork()
    {
        var (state, _, rules) = Ready([new SelectedTrashEffect([new InfluenceEffect(Faction.Emperor, 4)]),
            new GainEffect(ResourceKind.Water, 1)]);
        state.Players[0].Resources = state.Players[0].Resources with { Water = int.MaxValue - 1 };
        rules = rules with
        {
            Lifecycle = new("synthetic-trash-threshold-v1", [], Factions:
            [new(Faction.Emperor, 6, [new GainEffect(ResourceKind.Water, 1)],
                [new("Synthetic trash fixture", 1, "Invented threshold reward")])]),
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var decline = Assert.IsType<DeclineTrashAction>(Assert.Single(engine.LegalActions(0)));
        var selection = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = selection.Targets[0];
        RejectUnchanged(engine, new SelectTrashCardAction(0, selection.Id, target.Source, target.Card), StepStatus.ContentError);
        Apply(engine, decline);
        Assert.Equal(int.MaxValue, state.Players[0].Resources.Water);
        Assert.Equal(0, state.Players[0].Influence.Single(x => x.Faction == Faction.Emperor).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MismatchedChooserAndPayerRejectBeforeActivation(bool optional)
    {
        var (state, engine, _) = Ready([new ForPlayerEffect(1, [new SelectedTrashEffect([], optional)]) { PayerId = 0 }]);
        RejectUnchanged(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("null")]
    [InlineData("cycle")]
    [InlineData("context")]
    [InlineData("unsupported-context")]
    [InlineData("unsupported-role")]
    public void MalformedOptionalSuccessFailsBeforeActivation(string malformed)
    {
        Effect child = malformed switch
        {
            "negative" => new DrawEffect(-1),
            "null" => null!,
            "context" => new HarvestSpiceEffect(),
            "unsupported-context" => new SequenceEffect([new UnsupportedEffect("Unavailable"), new HarvestSpiceEffect()]),
            "unsupported-role" => new SequenceEffect([new UnsupportedEffect("Unavailable"), new ForPlayerEffect(99, [])]),
            _ => new DrawEffect(1),
        };
        if (malformed == "cycle")
        {
            var nodes = new Effect[1];
            var cycle = new SelectedTrashEffect(System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(nodes));
            nodes[0] = cycle;
            child = cycle;
        }
        // No board provenance is available in a Reveal box.
        var (state, engine, rules) = Ready([]);
        rules = CardEffects(rules, state.Players[0].Hand.Cards[0].Definition, reveal: [new SelectedTrashEffect([child])]);
        engine = new TurnEngine(state, rules);
        RejectUnchanged(engine, new RevealAction(0), StepStatus.ContentError);
    }

    [Fact]
    public void MandatoryTargetAccountingIncludesChosenRemovalAndGatesCompetingSources()
    {
        var effects = ImmutableArray.Create<Effect>(new SelectedTrashEffect([]), new SelectedTrashEffect([], Optional: false));
        var (state, engine, _) = Ready(effects);
        var player = state.Players[0];
        foreach (var card in player.Hand.Cards.Skip(1).ToArray()) player.Hand.MoveTo(card.Id, player.Deck);
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        Assert.IsType<DeclineTrashAction>(Assert.Single(engine.LegalActions(0)));
        var selected = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = selected.Targets.Single();
        RejectUnchanged(engine, new SelectTrashCardAction(0, selected.Id, target.Source, target.Card), StepStatus.Unsupported);
        Apply(engine, new DeclineTrashAction(0, selected.Id));
        Resolve<SelectedTrashEffect>(engine);
        Apply(engine, Assert.Single(engine.LegalActions(0).OfType<SelectTrashCardAction>()));
        var (other, blocked, _) = Ready([new TrashSelfEffect(), new SelectedTrashEffect([], Optional: false)]);
        RejectUnchanged(blocked, new PlaceAgentAction(0, Card(other.Players[0].Hand.Cards[0]), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MandatorySuccessReservesResourcesStockIntrigueAndPermanentOperations(bool optional)
    {
        foreach (var children in new ImmutableArray<Effect>[]
        {
            [new GainEffect(ResourceKind.Water, int.MaxValue)],
            [new HighCouncilEffect(), new HighCouncilEffect()],
            [new DrawIntrigueEffect(int.MaxValue)],
            [new TakeMentatEffect(true), new TakeMentatEffect(true)],
            [new AcquireEffect(new("imperium:reserve:arrakis-liaison:v1")), new AcquireEffect(new("imperium:reserve:arrakis-liaison:v1"))],
            [new InfluenceEffect(Faction.Emperor, 4)],
        })
        {
            var (state, engine, _) = Ready([new SelectedTrashEffect(children, optional)]);
            foreach (var card in state.Shared.Reserves.SelectMany(x => x.Cards).ToArray())
                state.Shared.Reserves.Single(x => x.Cards.Contains(card)).MoveTo(card.Id, state.Shared.Trashed);
            var place = new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract"));
            if (!optional)
            {
                var before = engine.CaptureTrustedSnapshot().CanonicalJson;
                var result = engine.Step(place);
                Assert.False(result.IsSuccess);
                Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
                Assert.Empty(engine.CaptureTrustedSnapshot().Data.Actions);
                continue;
            }
            Apply(engine, place);
            Resolve<SelectedTrashEffect>(engine);
            var decline = Assert.IsType<DeclineTrashAction>(Assert.Single(engine.LegalActions(0)));
            var selection = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
            var target = selection.Targets[0];
            var snapshot = engine.CaptureTrustedSnapshot().CanonicalJson;
            Assert.False(engine.Step(new SelectTrashCardAction(0, selection.Id, target.Source, target.Card)).IsSuccess);
            Assert.Equal(snapshot, engine.CaptureTrustedSnapshot().CanonicalJson);
            Apply(engine, decline);
        }
    }

    [Fact]
    public void MandatoryTrashRejectsUnaffordableSuccessChoiceBeforeActivation()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([new ChoiceEffect([
            new("Water payment", new(Water: 2), [new GainEffect(ResourceKind.Solari, 1)]),
            new("Spice payment", new(Spice: 2), [new GainEffect(ResourceKind.Water, 1)])])], Optional: false)]);
        RejectUnchanged(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")), StepStatus.Unsupported);
    }

    [Fact]
    public void SelectedSourceCanTrashItselfWithoutRebindingOrMovingTwice()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([new TrashSelfEffect()])]);
        var played = state.Players[0].Hand.Cards[0];
        Apply(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var action = engine.LegalActions(0).OfType<SelectTrashCardAction>().Single(x => x.Source == state.Players[0].InPlay.Id);
        Apply(engine, action);
        Resolve<TrashSelfEffect>(engine);
        Assert.Equal(played.Id, Assert.Single(state.Shared.Trashed.Cards).Id);
    }

    [Fact]
    public void ReserveTargetsReturnToTheirSharedStack()
    {
        var (state, engine, _) = Ready([new SelectedTrashEffect([])]);
        var reserve = state.Shared.Reserves[0];
        var card = reserve.Cards[0];
        reserve.MoveTo(card.Id, state.Players[0].Discard);
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Resolve<SelectedTrashEffect>(engine);
        var selection = engine.CaptureTrustedSnapshot().Data.Decisions.Single().SelectedTrash!;
        var target = selection.Targets.Single(x => x.Instance == card.Id);
        Apply(engine, new SelectTrashCardAction(0, selection.Id, target.Source, target.Card));
        Assert.Contains(card, reserve.Cards);
        Assert.DoesNotContain(card, state.Players[0].Discard.Cards);
        Assert.Empty(state.Shared.Trashed.Cards);
    }

    private static (GameState State, TurnEngine Engine, TurnRuleSet Rules) Ready(ImmutableArray<Effect> effects, bool discard = false)
    {
        var state = ReadyState(discard);
        var rules = ReadyRules(state, effects);
        return (state, new(state, rules), rules);
    }

    private static GameState ReadyState(bool discard = false)
    {
        var state = CreateState();
        state.Phase = GamePhase.PlayerTurns;
        state.Round = 1;
        state.CurrentPlayerId = state.DecisionOwnerId = 0;
        foreach (var player in state.Players)
            for (var i = 0; i < 5; i++) player.Deck.TryDrawTo(player.Hand);
        if (discard) state.Players[0].Hand.MoveTo(state.Players[0].Hand.Cards[^1].Id, state.Players[0].Discard);
        return state;
    }

    private static TurnRuleSet ReadyRules(GameState state, ImmutableArray<Effect> effects)
    {
        var rules = CardEffects(Rules(state), state.Players[0].Hand.Cards[0].Definition, agent: effects);
        return rules with { Spaces = [.. rules.Spaces.Select(x => x with { Effects = [] })] };
    }

    private static CardInstance AddDuplicates(GameState state)
    {
        // Reuse genuine physical starter duplicates as targets without assigning fabricated effects.
        var cards = state.Shared.UnusedStarters.Cards.GroupBy(x => x.Definition).First(x => x.Count() >= 2).Take(2).ToArray();
        foreach (var card in cards) state.Shared.UnusedStarters.MoveTo(card.Id, state.Players[0].Hand);
        return cards[1];
    }

    private static string Inventory(GameState state) => JsonSerializer.Serialize(state.Zones.SelectMany(x => x.Cards)
        .GroupBy(x => x.Definition).OrderBy(x => x.Key.Value, StringComparer.Ordinal)
        .Select(x => new { Definition = x.Key, Instances = x.Select(c => c.Id.Value).Order().ToArray() }), ConfigurationJson.Options);

    private static void RejectUnchanged(TurnEngine engine, GameAction action, StepStatus expected)
    {
        // The canonical snapshot includes every zone ID/card sequence, resources, both RNG checkpoints,
        // all frame payloads/counters and accepted actions. Replay additionally binds the full header/history.
        var before = engine.CaptureTrustedSnapshot();
        var replay = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        Assert.Equal(expected, engine.Step(action).Status);
        Assert.Equal(before.CanonicalJson, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(before.Hash, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(replay, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
    }

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        var work = engine.Observe(owner).PendingEffects.First(x => x.Kind == typeof(T).Name);
        Apply(engine, new ResolveEffectAction(owner, work.Id));
    }
}
