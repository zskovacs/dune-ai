using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class TurnEngineTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void RoundStartDrawsFiveRevealsConflictAndSchedulesClockwise(int players)
    {
        var state = CreateState(players);
        state.FirstPlayerId = players - 1;
        var engine = new TurnEngine(state, Rules(state));
        Assert.True(engine.StartRound().IsSuccess);
        Assert.Equal(1, state.Round);
        Assert.Equal(GamePhase.PlayerTurns, state.Phase);
        Assert.Single(state.Conflict.Revealed.Cards);
        Assert.All(state.Players, x => Assert.Equal(5, x.Hand.Count));
        Assert.Equal(players - 1, engine.CurrentPlayerId);
        Apply(engine, new PlaceAgentAction(players - 1, Card(state.Players[^1].Hand.Cards[0]), Space("secure-contract")));
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(3, state.Players[^1].Resources.Solari);
        Assert.Equal(1, state.Players[^1].Units.AvailableAgents);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.StartRound().Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void PlacementChecksAccessCostsOccupancyInfluenceAndOncePerGameBeforeMutation()
    {
        var state = CreateState();
        var rules = Rules(state);
        rules = rules with
        {
            Spaces = rules.Spaces.Add(new(Space("sietch-tabr"), AgentIcon.City, new(), [],
            Faction: Faction.Fremen, RequiredInfluence: 2))
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var player = state.Players[0];
        foreach (var action in new GameAction[]
        {
            new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("high-council")),
            new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("sietch-tabr")),
            new PlaceAgentAction(1, Card(player.Hand.Cards[0]), Space("secure-contract")),
            new PlaceAgentAction(0, new(new("hidden-or-forged")), Space("secure-contract")),
        })
        {
            var before = engine.CaptureTrustedSnapshot().Hash;
            Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);
            Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        }
        player.Resources = player.Resources with { Solari = 10 };
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("high-council")));
        Assert.Equal(5, player.Resources.Solari);
        Assert.True(player.HasHighCouncilSeat);
        Apply(engine, new RevealAction(1)); ResolveAll(engine); Apply(engine, new EndRevealAction(1));
        Apply(engine, new RevealAction(2)); ResolveAll(engine); Apply(engine, new EndRevealAction(2));
        Assert.Equal(0, engine.CurrentPlayerId);
        var hash = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("high-council"))).Status);
        Assert.Equal(hash, engine.CaptureTrustedSnapshot().Hash);
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("secure-contract")));
        Assert.Equal(0, player.Units.AvailableAgents);
        Assert.DoesNotContain(engine.LegalActions(0), x => x is PlaceAgentAction);
        Assert.Contains(engine.LegalActions(0), x => x is RevealAction);
    }

    [Fact]
    public void EffectOrderAllowsBoardGainToFundOptionalCardPayment()
    {
        var state = CreateState();
        var firstCard = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), firstCard.Definition,
            agent: [new OptionalPaymentEffect(new(Solari: 3), [new GainEffect(ResourceKind.Water, 2)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(firstCard), Space("secure-contract")));
        var gain = engine.Observe(0).PendingEffects.Single(x => x.Kind == nameof(GainEffect));
        Apply(engine, new ResolveEffectAction(0, gain.Id));
        Assert.Equal(3, state.Players[0].Resources.Solari);
        Apply(engine, engine.LegalActions(0).OfType<ResolveEffectAction>().Single());
        Assert.Contains(engine.LegalActions(0), x => x is ChooseOptionAction { Option: 1 });
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void NestedOptionalDecisionsPreserveOwnerIndependentOfActivePlayer()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
        [new OptionalPaymentEffect(new(), [new OptionalPaymentEffect(new(),
            [new GainEffect(ResourceKind.Water, 2)], DecisionOwnerId: 2)], DecisionOwnerId: 1)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        Apply(engine, engine.Observe(0).PendingEffects.Where(x => x.Kind == nameof(OptionalPaymentEffect))
            .Select(x => new ResolveEffectAction(0, x.Id)).Single());
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Empty(engine.LegalActions(0));
        Assert.Null(engine.Observe(0).Decision);
        Assert.NotNull(engine.Observe(1).Decision);
        var snapshot = engine.CaptureTrustedSnapshot();
        Assert.Single(snapshot.Data.Decisions);
        Assert.Equal(1, snapshot.Data.Decisions[0].Context.OwnerId);
        Apply(engine, new ChooseOptionAction(1, 1));
        // The foreign child completes before the active player's suspended board gain.
        Assert.Empty(engine.LegalActions(0));
        Apply(engine, engine.LegalActions(1).OfType<ResolveEffectAction>().Single());
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(2, engine.DecisionOwnerId);
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(3, state.Players[2].Resources.Water);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedNestedGraphsFailBeforeCostsCardsRandomnessOrHistoryChange(bool reveal)
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        ImmutableArray<Effect> unsupported = [new GainEffect(ResourceKind.Solari, 99),
            new OptionalPaymentEffect(new(), [new ChoiceEffect([new("Nested", new(), [new UnsupportedEffect("missing")])])])];
        var rules = reveal ? CardEffects(Rules(state), card.Definition, reveal: unsupported) :
            CardEffects(Rules(state), card.Definition, agent: unsupported);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        GameAction action = reveal ? new RevealAction(0) : new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.DoesNotContain(action, engine.LegalActions(0));
        Assert.NotEmpty(engine.SupportDiagnostics(0));
    }

    [Fact]
    public void DeploymentAllowsRecruitsAndOnlyTwoOldGarrisonTroops()
    {
        var state = CreateState();
        var player = state.Players[0];
        var card = player.Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent: [new RecruitEffect(1)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("arrakeen")));
        ResolveAllEffects(engine);
        Assert.Equal(6, player.Units.TroopsInGarrison);
        var legal = engine.LegalActions(0).OfType<DeployAction>().ToArray();
        Assert.Equal([0, 1, 2, 3, 4, 5], legal.Select(x => x.Troops));
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new DeployAction(0, 6)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Apply(engine, new DeployAction(0, 5));
        Assert.Equal(1, player.Units.TroopsInGarrison);
        Assert.Equal(5, player.Units.TroopsInConflict);
        Assert.Equal(1, engine.CurrentPlayerId);
    }

    [Fact]
    public void RevealUsesOnlyHandAndAllowsPurchasesBetweenEffectsWithAcquireRewardsAndRefill()
    {
        var state = CreateState();
        var player = state.Players[0];
        var played = player.Deck.Cards[0];
        var revealed = player.Deck.Cards[1];
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(Rules(state), played.Definition, reveal: [new GainEffect(ResourceKind.Persuasion, 90)]);
        rules = CardEffects(rules, revealed.Definition,
            reveal: [new GainEffect(ResourceKind.Persuasion, 5), new GainEffect(ResourceKind.Swords, 2)]);
        rules = CardEffects(rules, market.Definition, acquire: [new GainEffect(ResourceKind.VictoryPoints, 1)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(played), Space("secure-contract")));
        Apply(engine, new RevealAction(1)); ResolveAll(engine); Apply(engine, new EndRevealAction(1));
        Apply(engine, new RevealAction(2)); ResolveAll(engine); Apply(engine, new EndRevealAction(2));
        Assert.Equal(4, player.Hand.Count);
        Apply(engine, new RevealAction(0));
        Assert.Equal(5, player.InPlay.Count);
        var effects = engine.CaptureTrustedSnapshot().Data.Pending;
        Assert.DoesNotContain(effects, x => x.Effect is GainEffect { Amount: 90 });
        Apply(engine, new ResolveEffectAction(0, effects.Single(x => x.Effect is GainEffect { Resource: ResourceKind.Persuasion, Amount: 5 }).Id));
        var pendingBeforePurchase = engine.CaptureTrustedSnapshot().Data.Pending.Length;
        var deckBefore = state.Shared.ImperiumDeck.Count;
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Assert.Equal(5, state.Shared.ImperiumRow.Count);
        Assert.Equal(deckBefore - 1, state.Shared.ImperiumDeck.Count);
        Assert.Contains(market, player.Discard.Cards);
        Assert.Equal(pendingBeforePurchase, engine.CaptureTrustedSnapshot().Data.Pending.Length);
        Assert.Equal(1, player.Resources.VictoryPoints);
        Apply(engine, new AcquireCardAction(0, new(new("imperium:reserve:arrakis-liaison:v1")),
            state.Shared.Reserves.Single(x => x.Cards[0].Definition.Value.Contains("arrakis-liaison", StringComparison.Ordinal)).Id));
        Assert.Equal(1, player.Temporary.Persuasion);
        ResolveAll(engine);
        Assert.Equal(1, player.Resources.VictoryPoints);
        var expectedStrength = player.Units.TroopsInConflict == 0 ? 0 : player.Units.TroopsInConflict * 2 + player.Temporary.Swords;
        Apply(engine, new EndRevealAction(0));
        Assert.Equal(expectedStrength, player.CombatMarker);
        Assert.Equal(0, player.Temporary.Persuasion);
        Assert.Empty(player.InPlay.Cards);
        Assert.Equal(GamePhase.CombatBoundary, engine.Phase);
    }

    [Fact]
    public void RevealStrengthIgnoresSwordsWithoutTroopsAndKeepsDeployedStrength()
    {
        var state = CreateState();
        var engine = new TurnEngine(state, Rules(state));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("arrakeen")));
        Apply(engine, new DeployAction(0, 3));
        FinishPlayerTurns(engine);
        Assert.Equal(10, state.Players[0].CombatMarker); // 3 troops*2 + 4 remaining hand swords.
        Assert.All(state.Players.Skip(1), x => Assert.Equal(0, x.CombatMarker));
        Assert.Null(engine.CurrentPlayerId);
        Assert.Null(engine.DecisionOwnerId);
        Assert.All(state.Players, x => Assert.True(x.Temporary.HasRevealed));
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new RevealAction(0)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void DeterministicReplayBindsRulesRuntimeConfigSeedsAndPendingDecisions()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent:
            [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Spice, 1)], DecisionOwnerId: 1)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        var optional = engine.Observe(0).PendingEffects.Single(x => x.Kind == nameof(OptionalPaymentEffect));
        Apply(engine, new ResolveEffectAction(0, optional.Id));
        var replay = engine.CaptureReplay();
        var result = TurnEngine.Replay(replay, () => CreateState(), rules);
        Assert.True(result.IsSuccess);
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, result.Engine!.CaptureTrustedSnapshot().Hash);
        Assert.False(TurnEngine.Replay(replay with { SupportVersion = "different" }, () => CreateState(), rules).IsSuccess);
        Assert.False(TurnEngine.Replay(replay with { RuntimeVersion = "different" }, () => CreateState(), rules).IsSuccess);
        Assert.False(TurnEngine.Replay(replay, () => CreateState(seed: 43), rules).IsSuccess);
        Assert.False(TurnEngine.Replay(replay, () => CreateState(), rules with { Version = "different" }).IsSuccess);
        var json = JsonSerializer.Serialize(replay);
        var deserialized = JsonSerializer.Deserialize<TurnReplay>(json)!;
        Assert.True(TurnEngine.Replay(deserialized, () => CreateState(), rules).IsSuccess);
        Apply(engine, new ChooseOptionAction(1, 0));
        Assert.NotEqual(replay.FinalStateHash, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(replay.FinalStateHash, result.Engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void LegalEnumerationAndObservationsAreReadOnlyPrivateAndDoNotExposePhysicalIds()
    {
        var state = CreateState();
        var engine = new TurnEngine(state, Rules(state));
        Apply(engine, new StartRoundAction());
        var before = engine.CaptureTrustedSnapshot().Hash;
        var observation = engine.Observe(0);
        var serialized = JsonSerializer.Serialize(observation);
        Assert.DoesNotContain("CardInstance", serialized);
        Assert.DoesNotContain("SourceCard", serialized);
        Assert.DoesNotContain("Checkpoint", serialized);
        Assert.DoesNotContain("Seed", serialized);
        Assert.DoesNotContain(state.Players[1].Hand.Cards[0].Definition.Value, serialized);
        Assert.DoesNotContain(state.Shared.ImperiumDeck.Cards[0].Definition.Value, serialized);
        Assert.Empty(engine.LegalActions(1));
        Assert.Empty(engine.SupportDiagnostics(1));
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(serialized, JsonSerializer.Serialize(engine.Observe(0)));
            Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        }
        Assert.All(engine.LegalActions(0).OfType<PlaceAgentAction>(), x =>
            Assert.Contains(state.Players[0].Hand.Cards, c => c.Definition == x.Card.Definition));
        var initialLegal = engine.LegalActions(0);
        foreach (var action in initialLegal)
        {
            var candidate = new TurnEngine(CreateState(), Rules(state));
            Apply(candidate, new StartRoundAction());
            Assert.True(candidate.Step(action).IsSuccess);
        }
        Apply(engine, initialLegal.OfType<PlaceAgentAction>().First());
        Assert.Equal(5, observation.Game.Hand.Length);
        Assert.NotEqual(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void DrawRecyclesDiscardDeterministicallyAndEpisodesRemainIsolated()
    {
        static (GameState State, TurnRuleSet Rules) Setup()
        {
            var state = CreateState();
            var player = state.Players[0];
            var card = player.Deck.Cards[0];
            foreach (var other in player.Deck.Cards.Skip(1).ToArray()) player.Deck.MoveTo(other.Id, player.Discard);
            var rules = CardEffects(Rules(state), card.Definition, agent: [new DrawEffect(8)]);
            return (state, rules);
        }
        var a = Setup(); var b = Setup();
        var first = new TurnEngine(a.State, a.Rules);
        var second = new TurnEngine(b.State, b.Rules);
        Apply(first, new StartRoundAction()); Apply(second, new StartRoundAction());
        Assert.Equal(first.CaptureTrustedSnapshot().Hash, second.CaptureTrustedSnapshot().Hash);
        var initialOther = second.CaptureTrustedSnapshot().Hash;
        var action = new PlaceAgentAction(0, Card(a.State.Players[0].Hand.Cards[0]), Space("secure-contract"));
        Apply(first, action); ResolveAll(first);
        Assert.Equal(initialOther, second.CaptureTrustedSnapshot().Hash);
        Apply(second, action); ResolveAll(second);
        Assert.Equal(first.CaptureTrustedSnapshot().Hash, second.CaptureTrustedSnapshot().Hash);
        Assert.Equal(9, a.State.Players[0].Hand.Count);
        Assert.True(TurnEngine.Replay(first.CaptureReplay(), () => Setup().State, a.Rules).IsSuccess);
    }

    [Fact]
    public void InfluenceCrossingTwoAwardsVpAndUnsupportedAllianceThresholdIsAtomic()
    {
        var state = CreateState();
        var player = state.Players[0];
        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Emperor ? x with { Amount = 1 } : x)];
        var engine = new TurnEngine(state, Rules(state));
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space("wealth")));
        Assert.Equal(1, player.Resources.VictoryPoints);
        Assert.Equal(2, player.Influence.Single(x => x.Faction == Faction.Emperor).Amount);
        var highState = CreateState();
        highState.Players[0].Influence = [.. highState.Players[0].Influence.Select(x =>
            x.Faction == Faction.Emperor ? x with { Amount = 3 } : x)];
        var high = new TurnEngine(highState, Rules(highState)); Apply(high, new StartRoundAction());
        var before = high.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, high.Step(new PlaceAgentAction(0,
            Card(highState.Players[0].Hand.Cards[0]), Space("wealth"))).Status);
        Assert.Equal(before, high.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void InvariantCorruptionThrowsInsteadOfMasqueradingAsIllegalInput()
    {
        var state = CreateState();
        var engine = new TurnEngine(state, Rules(state));
        state.Players[0].Resources = state.Players[0].Resources with { Water = -1 };
        Assert.Throws<InvalidOperationException>(() => engine.StartRound());
    }

    [Fact]
    public void MandatoryPlacementChoicePaysBeforeRewardsAndRejectsUnaffordableOptions()
    {
        var state = CreateState();
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = 3 };
        var rules = Rules(state);
        rules = rules with
        {
            Spaces = rules.Spaces.Add(new(Space("sell-melange"), AgentIcon.SpiceTrade,
            new(), [new GainEffect(ResourceKind.Spice, 100)])
            {
                PlacementOptions = [new("small", new(Spice: 2), [new GainEffect(ResourceKind.Solari, 6)]),
                new("large", new(Spice: 5), [new GainEffect(ResourceKind.Solari, 12)])],
            })
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var card = Card(player.Hand.Cards[0]);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new PlaceAgentAction(0, card, Space("sell-melange"), 1)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Apply(engine, new PlaceAgentAction(0, card, Space("sell-melange"), 0));
        Assert.Equal(101, player.Resources.Spice);
        Assert.Equal(6, player.Resources.Solari);
        Assert.DoesNotContain(engine.CaptureReplay().Actions, x => x is ResolveEffectAction);
    }

    [Fact]
    public void UnsupportedAcquireGraphPreservesPersuasionMarketAndHistory()
    {
        var state = CreateState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(Rules(state), market.Definition,
            acquire: [new OptionalPaymentEffect(new(), [new UnsupportedEffect("unverified acquire")])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        ResolveAll(engine);
        var action = new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, engine.Step(action).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.DoesNotContain(action, engine.LegalActions(0));
        FinishPlayerTurns(engine);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void SelfTrashMovesOnlyItsPhysicalSourceAndPreservesConservation()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, agent: [new TrashSelfEffect()]);
        var engine = new TurnEngine(state, rules);
        var originalCards = state.Zones.Sum(x => x.Count);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(card), Space("secure-contract")));
        ResolveAll(engine);
        Assert.Contains(card, state.Shared.Trashed.Cards);
        Assert.Empty(state.Players[0].InPlay.Cards);
        Assert.Equal(originalCards, state.Zones.Sum(x => x.Count));
        FinishPlayerTurns(engine);
        Assert.DoesNotContain(card, state.Players[0].Discard.Cards);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void InvalidEffectContextAndNegativeCostsReportContentErrorsBeforeMutation()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), card.Definition, reveal: [new HarvestSpiceEffect()]);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract")
            ? x with { Cost = new(Solari: -1) } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.ContentError, engine.Step(new RevealAction(0)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(StepStatus.ContentError,
            engine.Step(new PlaceAgentAction(0, Card(card), Space("secure-contract"))).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void GlobalLeaderGateRejectsClaimsOfSupportWithoutAnImplementedOfficialPassive()
    {
        var state = CreateState();
        var rules = Rules(state);
        rules = rules with { Leaders = [.. rules.Leaders.Select((x, i) => i == 2 ? x with { Synthetic = false } : x)] };
        var engine = new TurnEngine(state, rules);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, engine.StartRound().Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void CombinedNestedInfluenceForForeignOwnerIsPreflightedBeforePlacement()
    {
        var state = CreateState();
        var card = state.Players[0].Deck.Cards[0];
        state.Players[1].Influence = [.. state.Players[1].Influence.Select(x =>
            x.Faction == Faction.Emperor ? x with { Amount = 2 } : x)];
        var rules = CardEffects(Rules(state), card.Definition, agent:
        [new ForPlayerEffect(1, [new InfluenceEffect(Faction.Emperor)]),
            new OptionalPaymentEffect(new(), [new InfluenceEffect(Faction.Emperor)], DecisionOwnerId: 1)]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported,
            engine.Step(new PlaceAgentAction(0, Card(card), Space("secure-contract"))).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void OccupancyAndCardAccessFailuresAreImmutableAndDoNotLeakHiddenCards()
    {
        var state = CreateState();
        var card = state.Players[1].Deck.Cards[0];
        var rules = Rules(state);
        rules = rules with
        {
            Cards = [.. rules.Cards.Select(x => x.Id == card.Definition ?
            x with { Icons = [AgentIcon.SpiceTrade] } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new PlaceAgentAction(1, Card(card), Space("wealth"))).Status);
        Assert.Equal(StepStatus.Illegal, engine.Step(new PlaceAgentAction(1, Card(card), Space("secure-contract"))).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        var serialized = JsonSerializer.Serialize(engine.Observe(1));
        Assert.DoesNotContain(state.Players[0].Hand.Cards[0].Definition.Value, serialized);
        Assert.DoesNotContain(state.Players[2].Hand.Cards[0].Definition.Value, serialized);
    }

    [Fact]
    public void ResolvedCapacityAndTypedStrengthContributionsProvideNarrowRuleSeams()
    {
        var state = CreateState();
        var rules = Rules(state);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract")
            ? x with { Capacity = 2 } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Apply(engine, new PlaceAgentAction(1, Card(state.Players[1].Hand.Cards[0]), Space("secure-contract")));
        Assert.Equal(2, state.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).Occupants.Length);
        Assert.Equal(12, CombatStrength.Calculate([new("troop", 2, 2), new("synthetic-unit", 1, 5)], 3));
        Assert.Equal(0, CombatStrength.Calculate([], 4));
        Assert.Equal(4, CombatStrength.Calculate([], 4, requiresDeployedUnits: false));
    }

    [Fact]
    public void ForeignOnlyRevealEffectsHandOffAndReturnToActiveRevealWithReplay()
    {
        var state = CreateState();
        var ownCards = state.Players[0].Deck.Cards.Take(5).Select(x => x.Definition).ToHashSet();
        var rules = Rules(state);
        rules = rules with
        {
            Cards = [.. rules.Cards.Select(x => ownCards.Contains(x.Id)
            ? x with { RevealEffects = [new ForPlayerEffect(1, [new ChoiceEffect([new("gain", new(), [new GainEffect(ResourceKind.Water, 1)])])])] } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        Apply(engine, engine.LegalActions(0).OfType<ResolveEffectAction>().First());
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Empty(engine.LegalActions(0));
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
        for (var i = 0; i < 5; i++)
        {
            Apply(engine, engine.LegalActions(1).OfType<ResolveEffectAction>().Single());
            Apply(engine, new ChooseOptionAction(1, 0));
            if (i < 4) Apply(engine, engine.LegalActions(0).OfType<ResolveEffectAction>().First());
        }
        Assert.Equal(6, state.Players[1].Resources.Water);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.Contains(engine.LegalActions(0), x => x is EndRevealAction);
        FinishPlayerTurns(engine);
        Assert.Equal(GamePhase.CombatBoundary, engine.Phase);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void ForeignOnlyAcquireEffectsHandOffAndReturnToActiveRevealWithReplay()
    {
        var state = CreateState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(Rules(state), market.Definition,
            acquire: [new ForPlayerEffect(1, [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        ResolveAllEffects(engine);
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Apply(engine, engine.LegalActions(0).OfType<ResolveEffectAction>().Single());
        Assert.Equal(0, engine.CurrentPlayerId);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Empty(engine.LegalActions(0));
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
        Apply(engine, engine.LegalActions(1).OfType<ResolveEffectAction>().Single());
        Apply(engine, new ChooseOptionAction(1, 1));
        Assert.Equal(3, state.Players[1].Resources.Water);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.Contains(engine.LegalActions(0), x => x is EndRevealAction);
        FinishPlayerTurns(engine);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompleteGraphRejectsDuplicateMandatoryStockDemandBeforePlacementMutation(int nestedKind)
    {
        var state = CreateState();
        var reserve = state.Shared.Reserves.Single(x => x.Cards[0].Definition.Value.Contains("arrakis-liaison", StringComparison.Ordinal));
        var target = reserve.Cards[0].Definition;
        foreach (var excess in reserve.Cards.Skip(1).ToArray()) reserve.MoveTo(excess.Id, state.Shared.Trashed);
        var card = state.Players[0].Deck.Cards[0];
        Effect nested = nestedKind switch
        {
            1 => new OptionalPaymentEffect(new(), [new ForPlayerEffect(1, [new AcquireEffect(target)])]),
            2 => new ChoiceEffect([new("decline", new(), []), new("grant", new(), [new AcquireEffect(target)])]),
            _ => new AcquireEffect(target),
        };
        var rules = CardEffects(Rules(state), card.Definition,
            agent: [new GainEffect(ResourceKind.Water, 99), new AcquireEffect(target), nested]);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract")
            ? x with { Cost = new(Water: 1) } : x)]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var before = engine.CaptureTrustedSnapshot();
        var action = new PlaceAgentAction(0, Card(card), Space("secure-contract"));
        Assert.Equal(StepStatus.Unsupported, engine.Step(action).Status);
        Assert.Equal(before.Hash, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(before.Data.Game.MainRandom, state.Random.Checkpoint);
        Assert.Equal(before.Data.Actions, engine.CaptureTrustedSnapshot().Data.Actions);
        Assert.DoesNotContain(action, engine.LegalActions(0));
        Assert.Single(reserve.Cards);
    }

    [Fact]
    public void InterleavedPurchasesPreserveStockReservedForPendingMandatoryGrants()
    {
        static GameState InitialState()
        {
            var initial = CreateState();
            var reserve = initial.Shared.Reserves.Single(x => x.Cards[0].Definition.Value.Contains("arrakis-liaison", StringComparison.Ordinal));
            foreach (var excess in reserve.Cards.Skip(1).ToArray()) reserve.MoveTo(excess.Id, initial.Shared.Trashed);
            return initial;
        }
        var state = InitialState();
        var reserve = state.Shared.Reserves.Single(x => x.Cards.Count == 1);
        var target = reserve.Cards[0].Definition;
        var ownCards = state.Players[0].Deck.Cards.Take(5).Select(x => x.Definition).ToHashSet();
        var first = state.Players[0].Deck.Cards[0].Definition;
        var rules = Rules(state);
        rules = rules with
        {
            Cards = [.. rules.Cards.Select(x => !ownCards.Contains(x.Id) ? x :
            x with { RevealEffects = x.Id == first
                ? [new AcquireEffect(target), new GainEffect(ResourceKind.Persuasion, 5)] : [] })]
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        var gain = engine.Observe(0).PendingEffects.Single(x => x.Kind == nameof(GainEffect));
        Apply(engine, new ResolveEffectAction(0, gain.Id));
        var purchaseReserved = new AcquireCardAction(0, new(target), reserve.Id);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Unsupported, engine.Step(purchaseReserved).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.DoesNotContain(purchaseReserved, engine.LegalActions(0));

        // Every supported next action keeps the pending grant and the rest of Player Turns resolvable.
        var prefix = engine.CaptureReplay();
        var nextActions = engine.LegalActions(0);
        Assert.Contains(nextActions, x => x is AcquireCardAction);
        foreach (var next in nextActions)
        {
            var replayed = TurnEngine.Replay(prefix, InitialState, rules);
            Assert.True(replayed.IsSuccess);
            var branch = replayed.Engine!;
            Apply(branch, next);
            ResolveAllEffects(branch);
            FinishPlayerTurns(branch);
            Assert.Equal(GamePhase.CombatBoundary, branch.Phase);
            Assert.True(TurnEngine.Replay(branch.CaptureReplay(), InitialState, rules).IsSuccess);
        }
    }

    [Fact]
    public void AcquireOptionalDecisionBlocksOuterRevealEffectsAndPurchasesUntilResolved()
    {
        var state = CreateState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var first = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), first.Definition,
            reveal: [new GainEffect(ResourceKind.Persuasion, 5)]);
        rules = CardEffects(rules, market.Definition,
            acquire: [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        var gain = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Amount: 5 });
        Apply(engine, new ResolveEffectAction(0, gain.Id));
        var outer = engine.CaptureTrustedSnapshot().Data.Pending.First();
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        var acquisition = engine.CaptureTrustedSnapshot().Data.Frames[^1].Remaining.Single();
        var blockedBuy = new AcquireCardAction(0, Card(state.Shared.ImperiumRow.Cards[0]), state.Shared.ImperiumRow.Id);
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.Equal(StepStatus.Illegal, engine.Step(new ResolveEffectAction(0, outer.Id)).Status);
        Assert.Equal(StepStatus.Illegal, engine.Step(blockedBuy).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal([new ResolveEffectAction(0, acquisition.Id)], engine.LegalActions(0).Cast<ResolveEffectAction>());
        Apply(engine, new ResolveEffectAction(0, acquisition.Id));
        Assert.All(engine.LegalActions(0), action => Assert.IsType<ChooseOptionAction>(action));
        var replay = TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules);
        Assert.True(replay.IsSuccess);
        var replayed = replay.Engine!.CaptureTrustedSnapshot().Data;
        Assert.Equal(replayed.Frames[^1].Id, replayed.Decisions.Single().Context.ParentFrameId);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(3, state.Players[0].Resources.Water);
        Assert.Contains(new ResolveEffectAction(0, outer.Id), engine.LegalActions(0));
        FinishPlayerTurns(engine);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void NestedAcquireGainCompletesBeforeParentAcquireDecisionAndOuterReveal()
    {
        var state = CreateState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var first = state.Players[0].Deck.Cards[0];
        var rules = CardEffects(Rules(state), first.Definition,
            reveal: [new GainEffect(ResourceKind.Persuasion, 5)]);
        rules = CardEffects(rules, market.Definition, acquire:
            [new AcquireEffect(new("imperium:reserve:spice-must-flow:v1")), new OptionalPaymentEffect(new(), [])]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        var gain = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Amount: 5 });
        Apply(engine, new ResolveEffectAction(0, gain.Id));
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        var immediate = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is AcquireEffect);
        Assert.Equal(engine.CaptureTrustedSnapshot().Data.Frames[^1].Id, immediate.Context.ParentFrameId);
        Apply(engine, new ResolveEffectAction(0, immediate.Id));
        Assert.Equal(1, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(2, engine.CaptureTrustedSnapshot().Data.Frames.Length);
        var optional = engine.CaptureTrustedSnapshot().Data.Frames[^1].Remaining.Single();
        Apply(engine, new ResolveEffectAction(0, optional.Id));
        Apply(engine, new ChooseOptionAction(0, 0));
        FinishPlayerTurns(engine);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), () => CreateState(), rules).IsSuccess);
    }

    [Fact]
    public void CommutativeSameOwnerAgentGainsResolveWithoutExtraActions()
    {
        var state = CreateState();
        var rules = Rules(state);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract")
            ? x with { Effects = [new GainEffect(ResourceKind.Solari, 3), new InfluenceEffect(Faction.Emperor),
                new RecruitEffect(1), new HarvestSpiceEffect()] } : x)]
        };
        state.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).BonusSpice = 2;
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("secure-contract")));
        Assert.Equal(1, engine.CurrentPlayerId);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Pending);
        Assert.DoesNotContain(engine.CaptureReplay().Actions, x => x is ResolveEffectAction);
        Assert.Equal(3, state.Players[0].Resources.Solari);
        Assert.Equal(2, state.Players[0].Resources.Spice);
        Assert.Equal(4, state.Players[0].Units.TroopsInGarrison);
        Assert.Equal(1, state.Players[0].Influence.Single(x => x.Faction == Faction.Emperor).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurchasesPreflightCombinedInfluenceWithSuspendedRevealWork(bool foreignOwner)
    {
        var targetOwner = foreignOwner ? 1 : 0;
        GameState InitialState()
        {
            var initial = CreateState();
            var target = initial.Players[targetOwner];
            target.Influence = [.. target.Influence.Select(x => x.Faction == Faction.Emperor ? x with { Amount = 1 } : x)];
            return initial;
        }
        var state = InitialState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var safeMarket = state.Shared.ImperiumRow.Cards[1];
        var ownCards = state.Players[0].Deck.Cards.Take(5).Select(x => x.Definition).ToHashSet();
        var first = state.Players[0].Deck.Cards[0].Definition;
        Effect suspended = foreignOwner
            ? new ForPlayerEffect(targetOwner, [new OptionalPaymentEffect(new(), [new InfluenceEffect(Faction.Emperor, 2)])])
            : new InfluenceEffect(Faction.Emperor, 2);
        Effect immediate = foreignOwner
            ? new ForPlayerEffect(targetOwner, [new ChoiceEffect([
                new("gain", new(), [new InfluenceEffect(Faction.Emperor)]), new("decline", new(), [])])])
            : new InfluenceEffect(Faction.Emperor);
        var rules = Rules(state);
        rules = rules with
        {
            Cards = [.. rules.Cards.Select(x => !ownCards.Contains(x.Id) ? x :
            x with { RevealEffects = x.Id == first ? [new GainEffect(ResourceKind.Persuasion, 5), suspended] : [] })]
        };
        rules = CardEffects(rules, market.Definition, acquire: [immediate]);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        var persuasion = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is GainEffect { Amount: 5 });
        Apply(engine, new ResolveEffectAction(0, persuasion.Id));
        var action = new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id);
        var before = engine.CaptureTrustedSnapshot();
        Assert.Equal(StepStatus.Unsupported, engine.Step(action).Status);
        Assert.Equal(before.Hash, engine.CaptureTrustedSnapshot().Hash);
        Assert.DoesNotContain(action, engine.LegalActions(0));
        Assert.Equal(5, state.Players[0].Temporary.Persuasion);

        // A harmless acquisition remains legal, then the suspended gain can complete below four.
        Apply(engine, new AcquireCardAction(0, Card(safeMarket), state.Shared.ImperiumRow.Id));
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), InitialState, rules).IsSuccess);
        while (engine.CaptureTrustedSnapshot().Data.Pending.Length > 0 || engine.Observe(engine.DecisionOwnerId!.Value).Decision is not null)
        {
            var owner = engine.DecisionOwnerId!.Value;
            var legal = engine.LegalActions(owner);
            GameAction? next = legal.OfType<ChooseOptionAction>().FirstOrDefault(x => x.Option == 1);
            next ??= legal.OfType<ResolveEffectAction>().FirstOrDefault();
            Apply(engine, next ?? throw new InvalidOperationException("Supported suspended influence lost its continuation."));
        }
        Assert.Equal(3, state.Players[targetOwner].Influence.Single(x => x.Faction == Faction.Emperor).Amount);
        Assert.Equal(1, state.Players[targetOwner].Resources.VictoryPoints);
        FinishPlayerTurns(engine);
        Assert.True(TurnEngine.Replay(engine.CaptureReplay(), InitialState, rules).IsSuccess);
    }

    private static void ResolveAllEffects(TurnEngine engine)
    {
        while (engine.DecisionOwnerId is { } owner && engine.LegalActions(owner).OfType<ResolveEffectAction>().FirstOrDefault() is { } action)
            Apply(engine, action);
    }
}
