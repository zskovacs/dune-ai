using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

/// <summary>Synthetic episodes and explicitly marked initial-Endgame seams, with the sourced Corner face.</summary>
public sealed class EndgameTests
{
    private static readonly ContentId Corner = new("imperium:intrigue:corner-the-market:v1");
    private static readonly ContentId Nested = new("fixture:intrigue:endgame-nested:v1");

    [Theory]
    [InlineData(DuneTiebreaker.Spice)]
    [InlineData(DuneTiebreaker.Solari)]
    [InlineData(DuneTiebreaker.Water)]
    [InlineData(DuneTiebreaker.GarrisonTroops)]
    public void EachTiebreakerAloneSplitsOnlyTheVictoryPointTiedGroup(DuneTiebreaker criterion)
    {
        GameState Create()
        {
            var state = EndgameState();
            foreach (var player in state.Players) player.Resources = new(3, 3, 3, player.Id == 2 ? 8 : 9);
            var winner = state.Players[1];
            winner.Resources = criterion switch
            {
                DuneTiebreaker.Spice => winner.Resources with { Spice = 4 },
                DuneTiebreaker.Solari => winner.Resources with { Solari = 4 },
                DuneTiebreaker.Water => winner.Resources with { Water = 4 },
                _ => winner.Resources,
            };
            if (criterion == DuneTiebreaker.GarrisonTroops) AddGarrison(winner, 1);
            // A lower-VP seat cannot win with any amount of resources.
            state.Players[2].Resources = new(100, 100, 100, 8);
            return state;
        }
        var state = Create();
        var rules = LifecycleFixture.Rules(state);
        var engine = new TurnEngine(state, rules);
        Complete(engine);
        var result = engine.GetResult();
        Assert.Equal(new[] { 1 }, result.WinnerIds);
        Assert.Equal(new[] { 2, 1, 3 }, result.Standings.Select(x => x.Rank));
        Assert.All(result.Standings.Take(2), x => Assert.Equal(new[] { criterion }, x.AppliedTiebreakers));
        Assert.Empty(result.Standings[2].AppliedTiebreakers);
        ConflictRewardTests.CheckReplay(engine, Create, rules);
    }

    [Fact]
    public void SuccessiveGroupSplitsRecordOnlyEffectiveCriteriaAndFullTiesShareCompetitionRank()
    {
        var state = EndgameState(4);
        state.Players[0].Resources = new(2, 5, 2, 9);
        state.Players[1].Resources = new(2, 5, 1, 9);
        state.Players[2].Resources = new(2, 4, 2, 9);
        state.Players[3].Resources = new(2, 4, 2, 9);
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state));
        Complete(engine);
        var result = engine.GetResult();
        Assert.Equal(new[] { 1, 2, 3, 3 }, result.Standings.Select(x => x.Rank));
        Assert.All(result.Standings.Take(2), x => Assert.Equal(new[] { DuneTiebreaker.Spice, DuneTiebreaker.Water }, x.AppliedTiebreakers));
        Assert.All(result.Standings.Skip(2), x => Assert.Equal(new[] { DuneTiebreaker.Spice }, x.AppliedTiebreakers));
        Assert.Equal(new[] { 0 }, result.WinnerIds);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EqualFullTuplesKeepEveryWinnerAndSeatOrderOnlyCanonicalizesOutput(int players)
    {
        var state = EndgameState(players);
        state.FirstPlayerId = players - 1;
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state));
        Complete(engine);
        Assert.Equal(Enumerable.Range(0, players), engine.GetResult().WinnerIds);
        Assert.Equal(Enumerable.Range(0, players), engine.GetResult().Standings.Select(x => x.PlayerId));
        Assert.All(engine.GetResult().Standings, x => { Assert.Equal(1, x.Rank); Assert.Empty(x.AppliedTiebreakers); });
    }

    [Fact]
    public void TiedWinnersShareFirstPlaceAndNextSeatHasRankThree()
    {
        var state = EndgameState();
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 9 };
        state.Players[1].Resources = state.Players[1].Resources with { VictoryPoints = 9 };
        state.Players[2].Resources = state.Players[2].Resources with { VictoryPoints = 0 };
        var engine = new TurnEngine(state, LifecycleFixture.Rules(state));
        Complete(engine);
        Assert.Equal(new[] { 0, 1 }, engine.GetResult().WinnerIds);
        Assert.Equal(new[] { 1, 1, 3 }, engine.GetResult().Standings.Select(x => x.Rank));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)] // More than each rival, independently of the at-least-two condition.
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 1)] // At least two, independently of the most condition.
    [InlineData(2, 1, 2)]
    [InlineData(4, 3, 2)] // Deck, hand, discard and in-play are all owned.
    public void CornerPrintedConditionsEvaluateIndependentlyAcrossOwnedZones(int own, int rival, int gain)
    {
        GameState Create()
        {
            var state = CornerState();
            OwnSpiceMustFlow(state, 0, own);
            OwnSpiceMustFlow(state, 1, rival);
            // A public trashed copy and the remaining Reserve copies belong to nobody.
            var reserve = SpiceMustFlowReserve(state);
            reserve.MoveTo(reserve.Cards[0].Id, state.Shared.Trashed);
            return state;
        }
        var state = Create();
        var rules = CornerRules(state);
        var engine = new TurnEngine(state, rules);
        var before = state.Players[0].Resources.VictoryPoints;
        Apply(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single())));
        ResolveAll(engine);
        Assert.Equal(before + gain, state.Players[0].Resources.VictoryPoints);
        Assert.Contains(state.Shared.IntrigueDiscard.Cards, x => x.Definition == Corner);
        Complete(engine);
        Assert.Equal(before + gain, engine.GetResult().Standings[0].VictoryPoints);
        ConflictRewardTests.CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenRivalCopiesCannotChangeCornerLegalityDiagnosticsOrMetadataEvenOnFailure(bool overflow)
    {
        (GameState State, TurnEngine Engine) Create(bool rivalOwns)
        {
            var state = CornerState();
            OwnSpiceMustFlow(state, 0, 1);
            var reserve = SpiceMustFlowReserve(state);
            var hidden = reserve.Cards[0];
            reserve.MoveTo(hidden.Id, state.Shared.ImperiumDeck);
            if (rivalOwns)
            {
                state.Players[1].Deck.MoveTo(state.Players[1].Deck.Cards[0].Id, state.Shared.ImperiumDeck);
                state.Shared.ImperiumDeck.MoveTo(hidden.Id, state.Players[1].Deck);
            }
            if (overflow) state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue - 1 };
            return (state, new TurnEngine(state, CornerRules(state)));
        }
        var left = Create(false);
        var right = Create(true);
        Assert.Equal(Json(left.Engine.Observe(0)), Json(right.Engine.Observe(0)));
        Assert.Equal(left.Engine.SupportDiagnostics(0), right.Engine.SupportDiagnostics(0));
        Assert.Equal(left.Engine.LegalActions(0), right.Engine.LegalActions(0));
        foreach (var (state, engine) in new[] { left, right })
        {
            var action = new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single()));
            if (overflow)
            {
                var snapshot = engine.CaptureTrustedSnapshot().CanonicalJson;
                var replay = Json(engine.CaptureReplay());
                Assert.Equal(StepStatus.ContentError, engine.Step(action).Status);
                Assert.Equal(snapshot, engine.CaptureTrustedSnapshot().CanonicalJson);
                Assert.Equal(replay, Json(engine.CaptureReplay()));
            }
            else Apply(engine, action);
        }
        if (overflow) return;
        // Pending typed metadata is identical until the conditional actually resolves.
        Assert.Equal(Json(left.Engine.Observe(0)), Json(right.Engine.Observe(0)));
        ResolveAll(left.Engine);
        ResolveAll(right.Engine);
        Assert.Equal(1, left.State.Players[0].Resources.VictoryPoints - right.State.Players[0].Resources.VictoryPoints);
        Complete(left.Engine);
        Complete(right.Engine);
        foreach (var item in new[] { left, right })
        {
            var view = item.Engine.Observe(0);
            Assert.Empty(view.Game.Players[1].Discard);
            Assert.Empty(view.Game.Players[1].InPlay);
            Assert.DoesNotContain("spice-must-flow", Json(view.Game.Players[1]));
            Assert.DoesNotContain("environmentSeed", Json(view));
            Assert.DoesNotContain("replayIdentity", Json(view));
        }
        var firstView = left.Engine.Observe(0);
        var secondView = right.Engine.Observe(0);
        var normalizedPlayers = firstView.Game.Players.Select(player => player.Id == 0 ? player with
        { Resources = player.Resources with { VictoryPoints = secondView.Game.Players[0].Resources.VictoryPoints } } : player).ToImmutableArray();
        Assert.Equal(Json(firstView with { Game = firstView.Game with { Players = normalizedPlayers } }), Json(secondView));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedForeignOptionalReturnsToSameEndgameOpportunityAndCanDecline(bool pay)
    {
        GameState Create() => CornerState();
        var state = Create();
        var rules = CornerRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new PassPriorityAction(0));
        var window = engine.CaptureTrustedSnapshot().Data.Window!;
        var card = state.Players[1].Intrigue.Cards.Single();
        Apply(engine, new PlayIntrigueAction(1, Card(card)));
        ConflictRewardTests.Resolve<SequenceEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Equal(1, engine.Observe(2).Game.Window!.PriorityOwnerId);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Empty(engine.LegalActions(1));
        ConflictRewardTests.CheckReplay(engine, Create, rules);
        ConflictRewardTests.Resolve<OptionalPaymentEffect>(engine);
        Assert.NotNull(engine.Observe(2).Decision);
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        Assert.Equal(StepStatus.Illegal, engine.Step(new PassPriorityAction(1)).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Apply(engine, new ChooseOptionAction(2, pay ? 1 : 0));
        ResolveAll(engine);
        Assert.Equal(1, engine.DecisionOwnerId);
        Assert.Equal(window.Id, engine.Observe(0).Game.Window!.Id);
        Assert.False(engine.Observe(0).Game.Window!.IsSuspended);
        Assert.Equal(pay ? 3 : 1, state.Players[0].Resources.Water);
        Assert.Equal(pay ? 0 : 1, state.Players[1].Resources.Solari);
        Assert.Empty(engine.Observe(0).Game.Window!.ConsecutivePasses);
        Apply(engine, new PassPriorityAction(1));
        Apply(engine, new PassPriorityAction(2));
        Assert.Equal(0, engine.DecisionOwnerId); // Prior passer must be revisited after the play.
        Apply(engine, new PassPriorityAction(0));
        Assert.False(engine.IsTerminal); // The player who played also gets a fresh no-play opportunity.
        Apply(engine, new PassPriorityAction(1));
        Assert.True(engine.IsTerminal);
        ConflictRewardTests.CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleOrderIndependentCardsCanPlayInEitherOrderInOneOpportunity(bool cornerFirst)
    {
        GameState Create()
        {
            var state = CornerState();
            var nested = state.Players[1].Intrigue.Cards.Single();
            state.Players[1].Intrigue.MoveTo(nested.Id, state.Players[0].Intrigue);
            OwnSpiceMustFlow(state, 0, 2);
            return state;
        }
        var state = Create();
        var rules = CornerRules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues
            .Select(x => x.Id == Nested ? x with { Effects = [new GainEffect(ResourceKind.VictoryPoints, 1)] } : x)]
            }
        };
        var engine = new TurnEngine(state, rules);
        foreach (var id in cornerFirst ? new[] { Corner, Nested } : new[] { Nested, Corner })
        {
            Apply(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single(x => x.Definition == id))));
            ResolveAll(engine);
            Assert.Equal(0, engine.DecisionOwnerId);
            Assert.Null(engine.CurrentPlayerId);
            ConflictRewardTests.CheckReplay(engine, Create, rules);
        }
        Assert.Equal(2, engine.Observe(0).Game.Window!.OpportunityPlays);
        Complete(engine);
        Assert.Equal(3 + Create().Players[0].Resources.VictoryPoints, engine.GetResult().Standings[0].VictoryPoints);
        Assert.Equal(2, engine.CaptureReplay().Actions.OfType<PlayIntrigueAction>().Count());
        ConflictRewardTests.CheckReplay(engine, Create, rules);
    }

    [Theory]
    [InlineData("timing")]
    [InlineData("unsupported")]
    [InlineData("unknown")]
    public void WrongTimingOrUnsupportedEndgamePlayPreservesStateHistoryAndRandom(string problem)
    {
        var state = CornerState();
        var rules = CornerRules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues
            .Where(x => problem != "unknown" || x.Id != Corner).Select(x => x.Id != Corner ? x : x with
            { Timing = problem == "timing" ? IntrigueTiming.Combat : x.Timing,
              Effects = problem == "unsupported" ? [new UnsupportedEffect("Synthetic unsupported Endgame face")] : x.Effects })]
            }
        };
        var engine = new TurnEngine(state, rules);
        var before = engine.CaptureTrustedSnapshot().CanonicalJson;
        var replay = Json(engine.CaptureReplay());
        Assert.Equal(problem == "timing" ? StepStatus.Illegal : StepStatus.Unsupported,
            engine.Step(new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single()))).Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(replay, Json(engine.CaptureReplay()));
        Assert.False(engine.IsTerminal);
        Assert.Throws<InvalidOperationException>(() => engine.GetResult());
    }

    [Fact]
    public void FullTenConflictEpisodeCompletesWithDetachedResultAndEveryAcceptedActionBound()
    {
        GameState Create() => LifecycleFixture.CreateState();
        var state = Create();
        var rules = LifecycleFixture.Rules(state);
        var engine = new TurnEngine(state, rules);
        Assert.Throws<InvalidOperationException>(() => engine.GetResult());
        Apply(engine, new StartRoundAction());
        for (var round = 1; round <= 10; round++) RoundLifecycleTests.AdvanceRound(engine);
        Assert.Equal(GamePhase.Endgame, engine.Phase);
        Assert.Throws<InvalidOperationException>(() => engine.GetResult());
        var zonesBefore = state.Zones.Select(x => (x.Id, Cards: x.Cards.ToArray())).ToArray();
        Complete(engine);
        Assert.Equal(10, engine.GetResult().Rounds);
        Assert.Equal(DuneTerminalReason.ConflictDeckExhausted, engine.GetResult().TerminalReason);
        Assert.True(engine.Observe(0).Game.IsCompleted);
        Assert.Null(engine.CurrentPlayerId);
        Assert.Null(engine.DecisionOwnerId);
        Assert.Null(engine.CaptureTrustedSnapshot().Data.Window);
        Assert.Empty(engine.CaptureTrustedSnapshot().Data.Frames);
        foreach (var (id, cards) in zonesBefore) Assert.Equal(cards, state.Zones.Single(x => x.Id == id).Cards);
        AssertResultMetadata(engine, state, rules);
        AssertEveryActionRejected(engine, state);
        ConflictRewardTests.CheckReplay(engine, Create, rules);

        var replay = engine.CaptureReplay();
        var result = engine.GetResult();
        Assert.IsType<PassPriorityAction>(replay.Actions[^1]);
        Assert.Equal(Identity(replay), result.ReplayIdentity);
        Assert.NotEqual(Identity(replay with { Actions = replay.Actions.RemoveAt(replay.Actions.Length - 1) }), result.ReplayIdentity);
        Assert.Equal(Identity(replay with { FinalStateHash = "ignored-by-identity" }), result.ReplayIdentity);
        Assert.NotEqual(Identity(replay with { EnvironmentSeed = replay.EnvironmentSeed + 1 }), result.ReplayIdentity);
        Assert.NotEqual(Identity(replay with { LeaderSeed = replay.LeaderSeed + 1 }), result.ReplayIdentity);
        var withoutClosingAction = TurnEngine.Replay(replay with { Actions = replay.Actions.RemoveAt(replay.Actions.Length - 1) }, Create, rules);
        Assert.False(withoutClosingAction.IsSuccess);

        var json = Json(result);
        Assert.Equal(json, Json(JsonSerializer.Deserialize<DuneGameResult>(json, ConfigurationJson.Options)!));
        Assert.Equal(json, Json(engine.CaptureTrustedSnapshot().Data.Result!));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DuneGameResult>(json[..^1] + ",\"unexpected\":true}", ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DuneGameResult>(json.Replace("ConflictDeckExhausted", "FutureReason", StringComparison.Ordinal), ConfigurationJson.Options));
        // Even a trusted test mutation cannot change the captured result's immutable values.
        state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 100 };
        Assert.Equal(json, Json(result));
        Assert.Equal(json, Json(engine.GetResult()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecallVictoryPointTriggerStillAllowsOfficialCornerToChangeTheWinner(bool alsoExhausted)
    {
        GameState Create()
        {
            var state = CornerState(initialEndgame: false);
            state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 9 };
            state.Players[1].Resources = state.Players[1].Resources with { VictoryPoints = 10 };
            OwnSpiceMustFlow(state, 0, 2);
            if (alsoExhausted)
                while (state.Conflict.Deck.Count > 1)
                    state.Conflict.Deck.MoveTo(state.Conflict.Deck.Cards[0].Id, state.Conflict.Unused);
            return state;
        }
        var state = Create();
        var rules = CornerRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        RoundLifecycleTests.AdvanceRound(engine);
        Assert.Equal(GamePhase.Endgame, engine.Phase);
        Assert.Equal(10, state.Players[1].Resources.VictoryPoints);
        Assert.Throws<InvalidOperationException>(() => engine.GetResult());
        Apply(engine, new PlayIntrigueAction(0, Card(state.Players[0].Intrigue.Cards.Single())));
        ResolveAll(engine);
        Complete(engine);
        Assert.Equal(new[] { 0 }, engine.GetResult().WinnerIds);
        Assert.Equal(11, engine.GetResult().Standings[0].VictoryPoints);
        Assert.Equal(DuneTerminalReason.VictoryPointThreshold | (alsoExhausted ? DuneTerminalReason.ConflictDeckExhausted : DuneTerminalReason.None), engine.GetResult().TerminalReason);
        Assert.Equal(1, engine.GetResult().Rounds);
        AssertEveryActionRejected(engine, state);
        ConflictRewardTests.CheckReplay(engine, Create, rules);
    }

    private static void AssertResultMetadata(TurnEngine engine, GameState state, TurnRuleSet rules)
    {
        var result = engine.GetResult();
        var replay = engine.CaptureReplay();
        Assert.Equal("dune-game-result-v1", result.Schema);
        Assert.Equal(replay.EngineVersion, result.EngineVersion);
        Assert.Equal(replay.RuntimeVersion, result.RuntimeVersion);
        Assert.Equal(state.Definition.Setup!.Id, result.SetupIdentity);
        Assert.Equal(state.ConfigurationIdentity, result.ConfigurationIdentity);
        Assert.Equal("dune-configuration-sha256-v1", result.ConfigurationIdentityFormat);
        Assert.Equal(state.Definition.Fingerprint, result.RulesetIdentity);
        Assert.Equal(state.ContentIdentity, result.ContentIdentity);
        Assert.Equal(rules.Version, result.RulesVersion);
        Assert.Equal(replay.RulesIdentity, result.RulesIdentity);
        Assert.Equal(rules.SupportVersion, result.SupportVersion);
        Assert.Equal(rules.Lifecycle!.Version, result.LifecycleVersion);
        Assert.Equal(ContentAuthenticity.SyntheticFixture, result.Authenticity);
        Assert.Equal(42, result.EnvironmentSeed);
        Assert.Equal(17, result.LeaderSeed);
        Assert.Equal(64, result.ReplayIdentity.Length);
    }

    private static void AssertEveryActionRejected(TurnEngine engine, GameState state)
    {
        var card = Card(state.Players[0].Deck.Cards.Concat(state.Players[0].Discard.Cards).First());
        GameAction[] actions = [new StartRoundAction(), new PlaceAgentAction(0, card, Space("arrakeen")),
            new RevealAction(0), new ResolveEffectAction(0, 0), new ChooseOptionAction(0, 0), new DeployAction(0, 0),
            new AcquireCardAction(0, card, state.Shared.ImperiumRow.Id), new EndRevealAction(0),
            new PlayIntrigueAction(0, card), new PassPriorityAction(0), new PassPriorityAction(-1)];
        var snapshot = engine.CaptureTrustedSnapshot().CanonicalJson;
        var replay = Json(engine.CaptureReplay());
        var result = Json(engine.GetResult());
        foreach (var action in actions)
        {
            Assert.Equal("completed", Assert.Single(engine.Step(action).Diagnostics).Code);
            Assert.Equal(snapshot, engine.CaptureTrustedSnapshot().CanonicalJson);
            Assert.Equal(replay, Json(engine.CaptureReplay()));
            Assert.Equal(result, Json(engine.GetResult()));
        }
        foreach (var player in state.Players)
        {
            Assert.Empty(engine.LegalActions(player.Id));
            Assert.Empty(engine.SupportDiagnostics(player.Id));
            Assert.Empty(engine.Observe(player.Id).LegalActions);
        }
    }

    private static GameState EndgameState(int players = 3)
    {
        var state = LifecycleFixture.CreateState(players);
        // Honest isolated scoring seam; full-episode tests never jump phases.
        state.Phase = GamePhase.Endgame;
        state.Round = 10;
        state.TerminalReason = DuneTerminalReason.ConflictDeckExhausted;
        return state;
    }

    private static GameState CornerState(bool initialEndgame = true)
    {
        var original = LifecycleFixture.CreateState();
        var replacements = original.Shared.IntrigueDeck.Cards.Take(2).Select((card, i) =>
            (card.Definition, Replacement: i == 0 ? Corner : Nested)).ToDictionary(x => x.Definition, x => x.Replacement);
        var configuration = OriginalImperiumFixture.Configuration(3) with
        { Content = new() { Id = "synthetic-endgame-fixture", Revision = 1 } };
        var content = original.Definition.Content.Select(x => replacements.TryGetValue(x.Id, out var id)
            ? x with
            {
                Id = id,
                Name = id == Corner ? "Corner the Market" : "SYNTHETIC nested Endgame card",
                Sources = id == Corner ? [new("DUNE_IMPERIUM_Rules_2020_10_26.pdf", 7, "Complete Corner the Market face")]
                    : [new("Synthetic Endgame fixture, not official content", 1, "Invented nested effect")]
            }
            : x).OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToImmutableArray();
        var definition = original.Definition with
        {
            Content = content,
            ConfigurationIdentity = ConfigurationIdentity.Compute(configuration),
            Fingerprint = RulesetFingerprint.Compute(configuration, original.Definition.Modules, original.Definition.Definitions,
                content, original.Definition.EligibleLeaders, original.Definition.CatalogCompleteness, original.Definition.Setup),
        };
        var shared = original.Shared;
        var deck = shared.IntrigueDeck;
        var replacement = new SharedCardState(shared.ImperiumDeck, shared.ImperiumRow,
            new(deck.Id, deck.Name, deck.Visibility, cards: deck.Cards.Select(x => replacements.TryGetValue(x.Definition, out var id)
                ? x with { Definition = id } : x)), shared.Reserves, shared.UnusedStarters);
        var identity = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content, ConfigurationJson.Options)));
        var state = new GameState(definition, ConfigurationIdentity.Compute(configuration, true), identity,
            ContentAuthenticity.SyntheticFixture, original.Players, original.Board, replacement, original.Random, original.LeadersRandom, 0);
        state.Shared.IntrigueDeck.TryDrawTo(state.Players[0].Intrigue);
        state.Shared.IntrigueDeck.TryDrawTo(state.Players[1].Intrigue);
        state.Players[1].Resources = state.Players[1].Resources with { Solari = 1 };
        if (initialEndgame)
        {
            state.Phase = GamePhase.Endgame;
            state.Round = 10;
            state.TerminalReason = DuneTerminalReason.ConflictDeckExhausted;
        }
        return state;
    }

    private static TurnRuleSet CornerRules(GameState state)
    {
        var rules = LifecycleFixture.Rules(state);
        var corner = OriginalImperiumTurnRules.CreateLifecycleRules().Intrigues.Single(x => x.Id == Corner);
        return rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Id == Corner ? corner : x.Id == Nested ? x with
            { Timing = IntrigueTiming.Endgame, Effects = [new SequenceEffect([new ForPlayerEffect(0,
                [new OptionalPaymentEffect(new(Solari: 1), [new GainEffect(ResourceKind.Water, 2)])])
                { DecisionOwnerId = 2, PayerId = 1 }])] } : x)],
            }
        };
    }

    private static void OwnSpiceMustFlow(GameState state, int seat, int count)
    {
        var player = state.Players[seat];
        var zones = new[] { player.Deck, player.Hand, player.Discard, player.InPlay };
        var reserve = SpiceMustFlowReserve(state);
        for (var i = 0; i < count; i++) reserve.MoveTo(reserve.Cards[0].Id, zones[i % zones.Length]);
    }

    private static CardZone SpiceMustFlowReserve(GameState state) => state.Shared.Reserves.Single(x =>
        x.Id.Value == "reserve:imperium:reserve:spice-must-flow:v1");

    private static void AddGarrison(PlayerState player, int amount) => player.Units = player.Units with
    { TroopsInSupply = player.Units.TroopsInSupply - amount, TroopsInGarrison = player.Units.TroopsInGarrison + amount };

    private static void Complete(TurnEngine engine)
    {
        var guard = 0;
        while (!engine.IsTerminal)
        {
            Assert.True(++guard < 20);
            Apply(engine, new PassPriorityAction(engine.DecisionOwnerId!.Value));
        }
    }

    private static string Identity(TurnReplay replay) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(TurnReplayIdentityData.From(replay), ConfigurationJson.Options)));
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ConfigurationJson.Options);
}
