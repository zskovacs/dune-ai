using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class RoundLifecycleTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ConsecutiveRoundsAccumulateCollectRecallRotateAndReplayWithExactConservation(int players)
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState(players);
            result.Players[0].Influence = [.. result.Players[0].Influence.Select(x => x with { Amount = 1 })];
            result.Board.Spaces.Single(x => x.Definition.Id == Space("carthag")).ControllerId = players - 1;
            result.Players[players - 1].Units = result.Players[players - 1].Units with { ControlMarkers = 2 };
            return result;
        }
        var state = Create();
        var rules = RoundRules(state);
        var engine = new TurnEngine(state, rules);
        var cardIds = Ids(state);
        var troops = Totals(state);
        Apply(engine, new StartRoundAction());
        var basin = state.Board.Spaces.Single(x => x.Definition.Id == Space("imperial-basin"));
        var flat = state.Board.Spaces.Single(x => x.Definition.Id == Space("great-flat"));
        for (var round = 1; round <= players + 1; round++)
        {
            Assert.Equal(round, state.Round);
            Assert.Equal((round - 1) % players, state.FirstPlayerId);
            Assert.All(state.Players, x => Assert.Equal(5, x.Hand.Count));
            Assert.Equal(round - 1, state.Conflict.Discard.Count);
            Assert.Single(state.Conflict.Revealed.Cards);
            if (round is 1 or 3)
            {
                var owner = engine.DecisionOwnerId!.Value;
                var spice = state.Players[owner].Resources.Spice;
                Place(engine, state, "imperial-basin");
                Assert.Equal(spice + (round == 3 ? 2 : 1), state.Players[owner].Resources.Spice);
                Assert.Equal(0, basin.BonusSpice);
            }
            Replay(engine, Create, rules);
            AdvanceRound(engine);
            Assert.Equal(round is 1 or 3 ? 0 : round == 2 ? 1 : round - 3, basin.BonusSpice);
            Assert.Equal(round, flat.BonusSpice);
            Assert.All(state.Board.Spaces, x => Assert.Empty(x.Occupants));
            Assert.All(state.Players, x =>
            {
                Assert.Equal(2, x.Units.AvailableAgents);
                Assert.Equal(0, x.Temporary.Swords);
                Assert.Equal(0, x.CombatMarker);
            });
            Assert.All(state.Players[0].Influence, x => Assert.Equal(1, x.Amount));
            Assert.Equal(players - 1, state.Board.Spaces.Single(x => x.Definition.Id == Space("carthag")).ControllerId);
            Assert.Equal(2, state.Players[players - 1].Units.ControlMarkers);
            Assert.Equal(cardIds, Ids(state));
            Assert.Equal(troops, Totals(state));
            Assert.Null(engine.CaptureTrustedSnapshot().Data.CombatOutcome);
            Assert.Null(engine.CaptureTrustedSnapshot().Data.ConflictRewards);
            Replay(engine, Create, rules);
        }
        Assert.Single(engine.CaptureReplay().Actions.OfType<StartRoundAction>());
        Assert.Equal(players + 2, state.Round);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryMentatReturnsWhetherUnusedOrPlacedWithoutEarlyRevealLosingIt(bool spend)
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState();
            result.Players[0].Resources = result.Players[0].Resources with { Solari = 20 };
            return result;
        }
        var state = Create();
        var rules = RoundRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Place(engine, state, "mentat");
        Assert.Equal(0, state.Board.Mentat.OwnerId);
        Assert.Equal(1, state.Players[0].Units.AvailableMentats);
        if (spend)
        {
            RevealSeat(engine, 1);
            RevealSeat(engine, 2);
            Place(engine, state, "secure-contract");
            Assert.Equal(AgentKind.Mentat, state.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).Occupants.Single().Kind);
            Assert.Equal(0, state.Players[0].Units.AvailableMentats);
        }
        Replay(engine, Create, rules);
        AdvanceRound(engine);
        Assert.Null(state.Board.Mentat.OwnerId);
        Assert.Equal(state.Board.Definition.InitialMentatSpace, state.Board.Mentat.Space);
        Assert.Equal(2, state.Players[0].Units.AvailableAgents);
        Assert.Equal(0, state.Players[0].Units.AvailableMentats);
        Replay(engine, Create, rules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictMentatSurvivesAwardRecallAndReturnsAtFollowingRecall(bool spend)
    {
        GameState Create() => ConflictRewardTests.CombatState([8, 0, 0]);
        var state = Create();
        var current = state.Conflict.Revealed.Cards.Single().Definition;
        var rules = RoundRules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x.Id == current
            ? x with { Rewards = [new(1, [new TakeMentatEffect(true)]), new(2, []), new(3, [])] } : x)]
            }
        };
        var engine = new TurnEngine(state, rules);
        ConflictRewardTests.CloseCombat(engine);
        ConflictRewardTests.FinishRewards(engine);
        Assert.Equal(2, state.Round);
        Assert.Equal(2, state.Board.Mentat.ProtectedThroughRound);
        Assert.Equal(3, state.Players[0].Units.AvailableAgents);
        Assert.Equal(1, state.Players[0].Units.AvailableMentats);
        Replay(engine, Create, rules);
        RevealSeat(engine, 1);
        RevealSeat(engine, 2);
        if (spend)
        {
            Place(engine, state, "secure-contract");
            Assert.Equal(AgentKind.Mentat, state.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).Occupants.Single().Kind);
            Assert.Equal(0, state.Players[0].Units.AvailableMentats);
        }
        AdvanceRound(engine);
        Assert.Equal(3, state.Round);
        Assert.Null(state.Board.Mentat.OwnerId);
        Assert.Equal(2, state.Players[0].Units.AvailableAgents);
        Assert.Equal(0, state.Players[0].Units.AvailableMentats);
        Replay(engine, Create, rules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwordmasterAndHighCouncilPersistAfterUsedOrUnusedAgentAndOnceGameSpacesRemainClosed(bool spend)
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState();
            result.Players[0].Resources = result.Players[0].Resources with { Solari = 20 };
            return result;
        }
        var state = Create();
        var rules = RoundRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Place(engine, state, "swordmaster");
        RevealSeat(engine, 1);
        RevealSeat(engine, 2);
        Place(engine, state, "high-council");
        Assert.Equal(1, state.Players[0].Units.AvailableAgents);
        Assert.Equal(1, state.Players[0].Units.AvailableSwordmasters);
        if (spend)
        {
            Place(engine, state, "secure-contract");
            Assert.Equal(AgentKind.Swordmaster, state.Board.Spaces.Single(x => x.Definition.Id == Space("secure-contract")).Occupants.Single().Kind);
        }
        Replay(engine, Create, rules);
        for (var round = 2; round <= 4; round++)
        {
            AdvanceRound(engine);
            Assert.Equal(round, state.Round);
            Assert.Equal(3, state.Players[0].Units.AvailableAgents);
            Assert.Equal(1, state.Players[0].Units.AvailableSwordmasters);
            Assert.Equal(1, state.Players[0].Units.AcquiredSwordmasters);
            Assert.True(state.Players[0].HasHighCouncilSeat);
            Assert.Equal(spend ? 10 : 7, state.Players[0].Resources.Solari);
            var used = engine.CaptureTrustedSnapshot().Data.UsedSpaces;
            Assert.Contains(new(0, Space("swordmaster")), used);
            Assert.Contains(new(0, Space("high-council")), used);
            while (engine.DecisionOwnerId != 0) RevealSeat(engine, engine.DecisionOwnerId!.Value);
            Assert.DoesNotContain(engine.LegalActions(0), x => x is PlaceAgentAction place &&
                (place.Space == Space("high-council") || place.Space == Space("swordmaster")));
            Replay(engine, Create, rules);
        }
    }

    [Fact]
    public void MentatSpaceAlwaysDrawsForPaidPlacementWhenTokenIsAbsent()
    {
        var state = LifecycleFixture.CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Solari = 2 };
        state.Board.Mentat = new(null, 1) { ProtectedThroughRound = 2 };
        state.Players[1].Units = state.Players[1].Units with { AvailableAgents = 3, AvailableMentats = 1 };
        var engine = new TurnEngine(state, RoundRules(state));
        Apply(engine, new StartRoundAction());
        Place(engine, state, "mentat");
        Assert.Equal(5, state.Players[0].Hand.Count); // Played one, drew one.
        Assert.Equal(0, state.Players[0].Resources.Solari);
        Assert.Equal(1, state.Players[0].Units.AvailableAgents);
        Assert.Equal(0, state.Players[0].Units.AvailableMentats);
        Assert.Equal(1, state.Board.Mentat.OwnerId);
        Assert.Equal(1, state.Players[1].Units.AvailableMentats);
    }

    [Fact]
    public void NextConflictDefenseInterruptsBeforeDrawFiveAndPreservesControlAndSupplyAccounting()
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState();
            result.Board.Spaces.Single(x => x.Definition.Id == Space("arrakeen")).ControllerId = 0;
            result.Players[0].Units = result.Players[0].Units with { ControlMarkers = 2 };
            return result;
        }
        var state = Create();
        var rules = RoundRules(state);
        rules = rules with { Lifecycle = rules.Lifecycle! with { Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with { ControlSpace = Space("arrakeen") })] } };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new ChooseOptionAction(0, 0));
        AdvanceRound(engine);
        Assert.Equal(GamePhase.RoundStart, state.Phase);
        Assert.Equal(2, state.Round);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(0, engine.DecisionOwnerId);
        Assert.All(state.Players, x => Assert.Empty(x.Hand.Cards));
        var supply = state.Players[0].Units.TroopsInSupply;
        var garrison = state.Players[0].Units.TroopsInGarrison;
        Replay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(0, 1));
        Replay(engine, Create, rules);
        ConflictRewardTests.Resolve<DeployFromSupplyEffect>(engine);
        Assert.Equal(GamePhase.PlayerTurns, state.Phase);
        Assert.Equal(1, engine.CurrentPlayerId);
        Assert.Equal(supply - 1, state.Players[0].Units.TroopsInSupply);
        Assert.Equal(garrison, state.Players[0].Units.TroopsInGarrison);
        Assert.Equal(1, state.Players[0].Units.TroopsInConflict);
        Assert.All(state.Players, x => Assert.Equal(5, x.Hand.Count));
        Assert.Equal(0, state.Board.Spaces.Single(x => x.Definition.Id == Space("arrakeen")).ControllerId);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void TenVictoryPointsDuringTurnsStillResolvesCombatCleanupAndMakersBeforeEndgame()
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState();
            result.Players[0].Resources = result.Players[0].Resources with { VictoryPoints = 9 };
            return result;
        }
        var state = Create();
        var rules = RoundRules(state);
        rules = rules with
        {
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("arrakeen")
            ? x with { Effects = [new GainEffect(ResourceKind.VictoryPoints, 1)] } : x)]
        };
        var engine = new TurnEngine(state, rules);
        var supply = state.Players[0].Units.TroopsInSupply;
        Apply(engine, new StartRoundAction());
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new PlaceAgentAction(owner, Card(state.Players[owner].Hand.Cards[0]), Space("arrakeen")));
        Apply(engine, new DeployAction(0, 1));
        Assert.Equal(10, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(GamePhase.PlayerTurns, state.Phase);
        FinishPlayerTurns(engine);
        Assert.Equal(GamePhase.Combat, state.Phase);
        Assert.Equal(DuneTerminalReason.None, state.TerminalReason);
        ConflictRewardTests.CloseCombat(engine);
        ConflictRewardTests.FinishRewards(engine);
        Assert.Equal(GamePhase.Endgame, state.Phase);
        Assert.Equal(DuneTerminalReason.VictoryPointThreshold, state.TerminalReason);
        Assert.Equal(1, state.Round);
        Assert.Equal(0, state.FirstPlayerId);
        Assert.Equal(supply + 1, state.Players[0].Units.TroopsInSupply); // Original garrison troop returned to supply.
        Assert.Equal(2, state.Players[0].Units.TroopsInGarrison);
        Assert.Equal(0, state.Players[0].Units.TroopsInConflict);
        Assert.All(state.Players, x => Assert.Empty(x.Hand.Cards));
        Assert.Equal(1, state.Board.Spaces.Single(x => x.Definition.Id == Space("great-flat")).BonusSpice);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Equal(DecisionWindowKind.Endgame, engine.Observe(0).Game.Window!.Kind);
        Assert.Equal(state.TerminalReason, engine.Observe(2).Game.TerminalReason);
        Replay(engine, Create, rules);
    }

    [Fact]
    public void EntireSyntheticConflictDeckExhaustsAtRecallWithoutExtraDrawOrRoundAndReshufflesDeterministically()
    {
        GameState Create() => LifecycleFixture.CreateState();
        var state = Create();
        var rules = RoundRules(state);
        var engine = new TurnEngine(state, rules);
        var ids = Ids(state);
        Apply(engine, new StartRoundAction());
        var randomAtStart = state.CaptureTrustedSnapshot().Data.MainRandom;
        for (var round = 1; round <= 10; round++)
        {
            Assert.Equal(round, state.Round);
            if (round == 10) Assert.Equal(0, state.Conflict.Deck.Count);
            Assert.Equal(GamePhase.PlayerTurns, state.Phase);
            AdvanceRound(engine);
            Replay(engine, Create, rules);
        }
        Assert.NotEqual(randomAtStart, state.CaptureTrustedSnapshot().Data.MainRandom);
        Assert.Equal(GamePhase.Endgame, engine.Phase);
        Assert.Equal(DuneTerminalReason.ConflictDeckExhausted, state.TerminalReason);
        Assert.Equal(10, state.Round);
        Assert.Equal(9, state.Conflict.Discard.Count);
        Assert.Single(state.Conflict.Revealed.Cards);
        Assert.All(state.Players, x =>
        {
            Assert.Empty(x.Hand.Cards);
            Assert.Equal(10, x.Deck.Count + x.Discard.Count);
        });
        Assert.Equal(ids, Ids(state));
        Assert.Single(engine.CaptureReplay().Actions.OfType<StartRoundAction>());
    }

    [Fact]
    public void BothTerminalReasonsAreStoredWithoutPreparingAnotherRound()
    {
        var state = LifecycleFixture.CreateState();
        // Explicit synthetic one-round terminal seam, preserving every physical card in public/trusted zones.
        while (state.Conflict.Deck.Count > 1) state.Conflict.Deck.MoveTo(state.Conflict.Deck.Cards[0].Id, state.Conflict.Unused);
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 10 };
        var engine = new TurnEngine(state, RoundRules(state));
        Apply(engine, new StartRoundAction());
        AdvanceRound(engine);
        Assert.Equal(GamePhase.Endgame, state.Phase);
        Assert.Equal(DuneTerminalReason.VictoryPointThreshold | DuneTerminalReason.ConflictDeckExhausted, state.TerminalReason);
        Assert.Equal(state.TerminalReason, engine.CaptureTrustedSnapshot().Data.Game.TerminalReason);
        Assert.Equal(1, state.Round);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingCombatRejectsUnsafeAutomaticContinuationBeforeAnyMutation(bool makerOverflow)
    {
        var state = ConflictRewardTests.CombatState([8, 6, 4]);
        var rules = RoundRules(state);
        if (makerOverflow)
            state.Board.Spaces.First(x => x.Definition.AccumulatesSpice).BonusSpice = int.MaxValue;
        else rules = rules with { Lifecycle = rules.Lifecycle! with { Conflicts = [.. rules.Lifecycle.Conflicts.Where(x => x.Id == state.Conflict.Revealed.Cards[0].Definition)] } };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        var replay = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        var rejected = engine.Step(new PassPriorityAction(2));
        Assert.Equal(makerOverflow ? StepStatus.ContentError : StepStatus.Unsupported, rejected.Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(replay, JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options));
    }

    [Fact]
    public void EndgameLeavesUnspentMentatAndPlacedAgentsUntouchedAndPublishesDetachedKindState()
    {
        var state = LifecycleFixture.CreateState();
        state.Players[0].Resources = state.Players[0].Resources with { Solari = 2, VictoryPoints = 10 };
        var engine = new TurnEngine(state, RoundRules(state));
        Apply(engine, new StartRoundAction());
        var before = engine.Observe(1).Game;
        Place(engine, state, "mentat");
        var held = engine.Observe(2).Game;
        Assert.Equal(1, held.Players[0].Units.AvailableMentats);
        Assert.Equal(2, held.Players[0].Units.OrdinaryAgents);
        Assert.Equal(0, before.Players[0].Units.AvailableMentats);
        AdvanceRound(engine);
        Assert.Equal(GamePhase.Endgame, state.Phase);
        Assert.Equal(0, state.Board.Mentat.OwnerId);
        Assert.Equal(1, state.Players[0].Units.AvailableMentats);
        Assert.Single(state.Board.Spaces.Single(x => x.Definition.Id == Space("mentat")).Occupants);
        Assert.Equal(1, state.Round);
        Assert.Equal(GamePhase.PlayerTurns, held.Phase);
        Assert.Equal(DuneTerminalReason.None, held.TerminalReason);
        var serialized = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.Equal(serialized, JsonSerializer.Serialize(JsonSerializer.Deserialize<TurnSnapshotData>(serialized, ConfigurationJson.Options), ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TurnSnapshotData>(
            serialized.Replace("VictoryPointThreshold", "UnverifiedTerminalReason", StringComparison.Ordinal), ConfigurationJson.Options));
        Assert.DoesNotContain("random", JsonSerializer.Serialize(engine.Observe(1), ConfigurationJson.Options), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinalRevealRejectsMakerOverflowWithoutDiscardingCardsOrAcceptingAction()
    {
        var state = LifecycleFixture.CreateState();
        state.Board.Spaces.First(x => x.Definition.AccumulatesSpice).BonusSpice = int.MaxValue;
        var engine = new TurnEngine(state, RoundRules(state));
        Apply(engine, new StartRoundAction());
        RevealSeat(engine, 0);
        RevealSeat(engine, 1);
        Apply(engine, new RevealAction(2));
        ResolveAll(engine);
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.Equal(StepStatus.ContentError, engine.Step(new EndRevealAction(2)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.NotEmpty(state.Players[2].InPlay.Cards);
    }

    [Fact]
    public void PendingInfluenceLossCannotBypassNextRoundSupportBecauseCurrentVpIsTen()
    {
        var state = ConflictRewardTests.CombatState([8, 6, 4]);
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 10 };
        state.Players[0].Influence = [.. state.Players[0].Influence.Select(x => x.Faction == Dune.Engine.Setup.Faction.Emperor ? x with { Amount = 2 } : x)];
        var rules = RoundRules(state);
        var conflict = rules.Lifecycle!.Conflicts.Single(x => x.Id == state.Conflict.Revealed.Cards[0].Definition);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle with
            {
                Conflicts = [conflict with
            { Rewards = [new(1, [new InfluenceEffect(Dune.Engine.Setup.Faction.Emperor, -1)]), new(2, []), new(3, [])] }]
            }
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.Equal(StepStatus.Unsupported, engine.Step(new PassPriorityAction(2)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
    }

    [Fact]
    public void NextRoundSupportDependsOnConfiguredPoolInsteadOfHiddenSelectedConflictIdentities()
    {
        var first = ConflictRewardTests.CombatState([8, 6, 4]);
        var second = ConflictRewardTests.CombatState([8, 6, 4]);
        var missing = first.Conflict.Unused.Cards.First().Definition;
        var substitute = second.Conflict.Unused.Cards.First(x => x.Definition == missing);
        second.Conflict.Deck.MoveTo(second.Conflict.Deck.Cards[0].Id, second.Conflict.Unused);
        second.Conflict.Unused.MoveTo(substitute.Id, second.Conflict.Deck);
        var rules = RoundRules(first);
        rules = rules with { Lifecycle = rules.Lifecycle! with { Conflicts = [.. rules.Lifecycle.Conflicts.Where(x => x.Id != missing)] } };
        var a = new TurnEngine(first, rules);
        var b = new TurnEngine(second, rules);
        Assert.Equal(JsonSerializer.Serialize(a.Observe(0), ConfigurationJson.Options), JsonSerializer.Serialize(b.Observe(0), ConfigurationJson.Options));
        foreach (var engine in new[] { a, b })
        {
            Apply(engine, new PassPriorityAction(0));
            Apply(engine, new PassPriorityAction(1));
            var before = engine.CaptureTrustedSnapshot().CanonicalJson;
            var result = engine.Step(new PassPriorityAction(2));
            Assert.Equal(StepStatus.Unsupported, result.Status);
            Assert.Equal("lifecycle-deck-support", Assert.Single(result.Diagnostics).Code);
            Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        }
    }

    private static TurnRuleSet RoundRules(GameState state) => LifecycleFixture.RulesWithOriginalBoard(state);

    private static void Place(TurnEngine engine, GameState state, string space)
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new PlaceAgentAction(owner, Card(state.Players[owner].Hand.Cards[0]), Space(space)));
        ResolveAll(engine);
    }

    private static void RevealSeat(TurnEngine engine, int owner)
    {
        Apply(engine, new RevealAction(owner));
        ResolveAll(engine);
        Apply(engine, new EndRevealAction(owner));
    }

    internal static void AdvanceRound(TurnEngine engine)
    {
        var round = engine.Observe(0).Game.Round;
        var guard = 0;
        while (engine.Observe(0).Game.Round == round && engine.Phase != GamePhase.Endgame)
        {
            Assert.True(++guard < 500);
            var owner = engine.DecisionOwnerId!.Value;
            var legal = engine.LegalActions(owner);
            GameAction? action = legal.OfType<ResolveEffectAction>().FirstOrDefault();
            action ??= legal.OfType<ChooseOptionAction>().FirstOrDefault();
            action ??= legal.OfType<DeployAction>().FirstOrDefault();
            action ??= legal.OfType<EndRevealAction>().FirstOrDefault();
            action ??= legal.OfType<RevealAction>().FirstOrDefault();
            action ??= legal.OfType<PassPriorityAction>().FirstOrDefault();
            Apply(engine, action ?? throw new InvalidOperationException("Lifecycle stopped: " + string.Join(';', engine.SupportDiagnostics(owner))));
        }
    }

    private static long[] Ids(GameState state) => [.. state.Zones.SelectMany(x => x.Cards).Select(x => x.Id.Value).Order()];
    private static int[] Totals(GameState state) => [.. state.Players.Select(x => x.Units.TroopsInSupply + x.Units.TroopsInGarrison + x.Units.TroopsInConflict)];
    private static void Replay(TurnEngine engine, Func<GameState> create, TurnRuleSet rules) => ConflictRewardTests.CheckReplay(engine, create, rules);
}
