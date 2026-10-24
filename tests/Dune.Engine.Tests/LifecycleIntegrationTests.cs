using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class LifecycleIntegrationTests
{
    [Fact]
    public void OfficialRegistryDefinitionCountsRemainDistinctFromCompleteInventorySupport()
    {
        var rules = OriginalImperiumTurnRules.Create();
        var lifecycle = OriginalImperiumTurnRules.CreateLifecycleRules();
        Assert.Equal(12, rules.Cards.Length);
        Assert.Equal(7, rules.Cards.Count(x => x.Id.Value.Contains(":starter:", StringComparison.Ordinal)));
        Assert.Equal(3, rules.Cards.Count(x => x.Id.Value.Contains(":reserve:", StringComparison.Ordinal)));
        Assert.Equal(2, rules.Cards.Count(x => x.Id.Value.Contains(":imperium-card:", StringComparison.Ordinal)));
        Assert.Equal(19, rules.Spaces.Length);
        Assert.Empty(rules.Leaders);
        Assert.Equal(4, lifecycle.Intrigues.Length);
        Assert.Equal(3, lifecycle.Intrigues.Count(x => !x.Effects.Any(e => e is UnsupportedEffect)));
        Assert.Empty(lifecycle.Conflicts);
        Assert.Equal(4, lifecycle.Factions.Length);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryAcceptedPrefixReplaysNestedForeignRewardsThroughNaturalEndgame(int players)
    {
        var state = LifecycleFixture.CreateScenarioState(players, 42);
        var rules = IntegrationRules(state);
        var engine = new TurnEngine(state, rules);
        var phases = new HashSet<GamePhase>();
        var windows = new HashSet<DecisionWindowKind>();
        var foreign = false;
        var acquire = false;
        var depth = 0;
        for (var step = 0; ; step++)
        {
            Assert.True(step < 2000);
            var snapshot = engine.CaptureTrustedSnapshot();
            var replay = engine.CaptureReplay();
            GameState? reproducedState = null;
            var reproduced = TurnEngine.Replay(replay, () => reproducedState = LifecycleFixture.CreateScenarioState(players, 42), rules);
            Assert.True(reproduced.IsSuccess, string.Join(";", reproduced.Diagnostics));
            var copy = reproduced.Engine!;
            Assert.Equal(snapshot.CanonicalJson, copy.CaptureTrustedSnapshot().CanonicalJson);
            Assert.Equal(snapshot.Hash, copy.CaptureTrustedSnapshot().Hash);
            Assert.Equal(state.Random.Checkpoint, reproducedState!.Random.Checkpoint);
            Assert.Equal(state.LeadersRandom.Checkpoint, reproducedState.LeadersRandom.Checkpoint);
            Assert.Equal(engine.DecisionOwnerId, copy.DecisionOwnerId);
            Assert.Equal(engine.CurrentPlayerId, copy.CurrentPlayerId);
            Assert.Equal(Json(snapshot.Data.Result), Json(copy.CaptureTrustedSnapshot().Data.Result));
            phases.Add(engine.Phase);
            if (snapshot.Data.Window is { } window) windows.Add(window.Kind);
            depth = Math.Max(depth, snapshot.Data.Frames.Length);
            foreign |= snapshot.Data.Frames.Any(x => x.Decision?.Context is { } c && c.DecisionOwnerId != c.InitiatorId);
            acquire |= snapshot.Data.Frames.Any(x => x.Remaining.Any(e => e.Context.Provenance == EffectProvenance.Acquisition)
                || x.Decision?.Context.Provenance == EffectProvenance.Acquisition);
            for (var player = 0; player < players; player++)
                Assert.Equal(engine.LegalActions(player), copy.LegalActions(player));
            AssertInvalidMatrix(engine, players);
            AssertPrivateProjection(engine, state);
            AssertHiddenCounterfactual(copy, reproducedState);
            if (engine.IsTerminal) break;
            Apply(engine, Select(engine));
        }
        Assert.Equal(10, engine.GetResult().Rounds);
        Assert.Equal(DuneTerminalReason.ConflictDeckExhausted, engine.GetResult().TerminalReason);
        Assert.Contains(GamePhase.Combat, phases);
        Assert.Contains(GamePhase.Endgame, phases);
        Assert.Contains(DecisionWindowKind.PostWinRewards, windows);
        Assert.Contains(DecisionWindowKind.Endgame, windows);
        Assert.True(foreign);
        Assert.True(acquire);
        Assert.True(depth >= 3);
        var final = engine.CaptureReplay();
        var preceding = TurnEngine.Replay(final with { Actions = final.Actions.RemoveAt(final.Actions.Length - 1) },
            () => LifecycleFixture.CreateScenarioState(players, 42), rules);
        Assert.False(preceding.IsSuccess); // The late closing action participates in final hash/result identity.
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("engine")]
    [InlineData("content")]
    [InlineData("configuration")]
    [InlineData("ruleset")]
    [InlineData("rules")]
    [InlineData("seed")]
    [InlineData("leader-seed")]
    public void MismatchedReplayHeadersRejectBeforeFirstAction(string field)
    {
        var state = LifecycleFixture.CreateScenarioState(3, 42);
        var rules = IntegrationRules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var replay = engine.CaptureReplay();
        replay = field switch
        {
            "runtime" => replay with { RuntimeVersion = "old-runtime" },
            "engine" => replay with { EngineVersion = "old-engine" },
            "content" => replay with { ContentIdentity = "old-content" },
            "configuration" => replay with { ConfigurationIdentity = "old-configuration" },
            "ruleset" => replay with { RulesetIdentity = "old-ruleset" },
            "rules" => replay with { RulesIdentity = "old-rules" },
            "seed" => replay with { EnvironmentSeed = 43 },
            _ => replay with { LeaderSeed = replay.LeaderSeed + 1 },
        };
        var fresh = LifecycleFixture.CreateScenarioState(3, 42);
        var before = fresh.CaptureTrustedSnapshot().Hash;
        var result = TurnEngine.Replay(replay, () => fresh, rules);
        Assert.False(result.IsSuccess);
        Assert.Equal("replay-version", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(before, fresh.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public async Task ConcurrentEpisodesShareOnlyImmutableDefinitionsAndRemainScheduleIndependent()
    {
        var definition = LifecycleFixture.CreateScenarioState(3, 42).Definition;
        var rules = LifecycleFixture.ScenarioRules(LifecycleFixture.CreateScenarioState(3, 42));
        var definitionBefore = Json(definition);
        var rulesBefore = new TurnEngine(LifecycleFixture.CreateScenarioState(3, 42), rules).CaptureReplay().RulesIdentity;
        string Run(int seed)
        {
            var own = LifecycleFixture.CreateScenarioState(3, seed);
            var shared = new GameState(definition, own.ConfigurationIdentity, own.ContentIdentity, own.Authenticity,
                own.Players, own.Board, own.Shared, own.Random, own.LeadersRandom, own.FirstPlayerId);
            var engine = new TurnEngine(shared, rules);
            for (var i = 0; !engine.IsTerminal; i++)
            {
                Assert.True(i < 2000);
                Apply(engine, Select(engine));
            }
            return engine.CaptureTrustedSnapshot().Hash;
        }
        int[] seeds = [42, 42, 7, 7];
        var expected = seeds.Select(Run).ToArray();
        var actual = await Task.WhenAll(seeds.Select(seed => Task.Run(() => Run(seed), TestContext.Current.CancellationToken)));
        Assert.Equal(expected, actual);
        Assert.Equal(actual[0], actual[1]);
        Assert.Equal(actual[2], actual[3]);
        Assert.NotEqual(actual[0], actual[2]);
        Assert.Equal(definitionBefore, Json(definition));
        Assert.Equal(rulesBefore, new TurnEngine(LifecycleFixture.CreateScenarioState(3, 42), rules).CaptureReplay().RulesIdentity);
    }

    private static TurnRuleSet IntegrationRules(GameState state)
    {
        var rules = LifecycleFixture.ScenarioRules(state);
        var market = state.Shared.ImperiumRow.Cards[0].Definition;
        rules = CardEffects(rules, market, acquire:
            [new ForPlayerEffect(1, [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 1)])])
            { DecisionOwnerId = 2, PayerId = 0 }]);
        var first = state.Conflict.Deck.Cards[0].Definition;
        var endgame = state.Players[^1].Intrigue.Cards[^1].Definition;
        return rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x.Id == first ? x with
                { Rewards = [.. x.Rewards.Select(r => r.Tier == 1 ? r with
                { Effects = [new SequenceEffect([new AcquireEffect(market), new GainEffect(ResourceKind.Solari, 3)])] } : r)] } : x)],
                Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Id == endgame ? x with
                { Timing = IntrigueTiming.Endgame, Effects = [new GainEffect(ResourceKind.Water, 1)] } : x)]
            }
        };
    }

    internal static GameAction Select(TurnEngine engine)
    {
        if (engine.Phase == GamePhase.BeforeRoundStart) return new StartRoundAction();
        var owner = engine.DecisionOwnerId!.Value;
        var legal = engine.LegalActions(owner);
        return legal.OfType<ResolveEffectAction>().FirstOrDefault()
            ?? (GameAction?)legal.OfType<ChooseOptionAction>().FirstOrDefault(x => x.Option == 1)
            ?? legal.OfType<ChooseOptionAction>().FirstOrDefault()
            ?? legal.OfType<DeployAction>().LastOrDefault()
            ?? legal.OfType<PlaceAgentAction>().FirstOrDefault(x => x.Space == Space("arrakeen"))
            ?? legal.OfType<PlaceAgentAction>().FirstOrDefault()
            ?? legal.OfType<RevealAction>().FirstOrDefault()
            ?? legal.OfType<PlayIntrigueAction>().FirstOrDefault()
            ?? (GameAction?)legal.OfType<EndRevealAction>().FirstOrDefault()
            ?? legal.OfType<PassPriorityAction>().FirstOrDefault()
            ?? throw new InvalidOperationException("No fixture continuation.");
    }

    private static void AssertInvalidMatrix(TurnEngine engine, int players)
    {
        var owner = engine.DecisionOwnerId ?? 0;
        GameAction[] invalid = [new RevealAction(players), new ChooseOptionAction(owner, int.MaxValue),
            new ResolveEffectAction(owner, int.MaxValue), new DeployAction(owner, -1),
            new PlayIntrigueAction(owner, new(new("fixture:missing"))),
            new AcquireCardAction(owner, new(new("fixture:missing")), new("fixture:missing")),
            new PlaceAgentAction(owner, new(new("fixture:missing")), Space("missing"), int.MaxValue)];
        if (engine.DecisionOwnerId is { } current)
        {
            var legal = engine.LegalActions(current);
            invalid = [.. invalid, .. legal.Take(1).Select(action => action with { PlayerId = (current + 1) % players })];
            if (legal.OfType<PlaceAgentAction>().FirstOrDefault() is { } place)
                invalid = [.. invalid, place with { PaymentOption = int.MaxValue }, place with { Space = Space("missing") }];
        }
        var snapshot = engine.CaptureTrustedSnapshot().CanonicalJson;
        var history = Json(engine.CaptureReplay());
        foreach (var action in invalid)
        {
            Assert.False(engine.Step(action).IsSuccess);
            Assert.Equal(snapshot, engine.CaptureTrustedSnapshot().CanonicalJson);
            Assert.Equal(history, Json(engine.CaptureReplay()));
        }
        if (engine.Phase != GamePhase.BeforeRoundStart)
        {
            Assert.False(engine.Step(new StartRoundAction()).IsSuccess);
            Assert.Equal(snapshot, engine.CaptureTrustedSnapshot().CanonicalJson);
            Assert.Equal(history, Json(engine.CaptureReplay()));
        }
    }

    private static void AssertPrivateProjection(TurnEngine engine, GameState state)
    {
        for (var player = 0; player < state.Players.Count; player++)
        {
            var observation = engine.Observe(player);
            var json = Json(observation) + Json(engine.SupportDiagnostics(player)) + Json(engine.LegalActions(player));
            foreach (var forbidden in new[] { "rawDrawCount", "mainRandom", "leaderRandom", "environmentSeed", "sourceCard",
                "parentFrameId", "continuation", "rulesIdentity", "replayIdentity", "initiatorId", "payerId", "recipientId" })
                Assert.DoesNotContain("\"" + forbidden + "\"", json, StringComparison.OrdinalIgnoreCase);
            foreach (var card in state.Zones.SelectMany(x => x.Cards))
                Assert.DoesNotContain(Json(card.Id), json);
            if (engine.DecisionOwnerId != player)
            {
                Assert.Empty(observation.PendingEffects);
                Assert.Null(observation.Decision);
                Assert.Empty(observation.LegalActions);
            }
        }
    }

    private static void AssertHiddenCounterfactual(TurnEngine engine, GameState state)
    {
        const int observer = 0;
        var before = Json(engine.Observe(observer));
        var legal = engine.LegalActions(observer);
        var diagnostics = engine.SupportDiagnostics(observer);
        foreach (var player in state.Players.Skip(1))
        {
            Swap(player.Hand, state.Shared.ImperiumDeck);
            Swap(player.Deck, state.Shared.ImperiumDeck);
            Swap(player.Intrigue, state.Shared.IntrigueDeck);
            player.Deck.Shuffle(state.Random);
        }
        state.Shared.ImperiumDeck.Shuffle(state.Random);
        state.Shared.IntrigueDeck.Shuffle(state.Random);
        Assert.Equal(before, Json(engine.Observe(observer)));
        Assert.Equal(legal, engine.LegalActions(observer));
        Assert.Equal(diagnostics, engine.SupportDiagnostics(observer));
    }

    private static void Swap(CardZone first, CardZone second)
    {
        if (first.Count == 0 || second.Count == 0) return;
        var a = first.Cards[0].Id;
        var b = second.Cards[^1].Id;
        first.MoveTo(a, second);
        second.MoveTo(b, first);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ConfigurationJson.Options);
}
