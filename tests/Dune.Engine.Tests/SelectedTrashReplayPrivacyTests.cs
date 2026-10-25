using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

/// <summary>Sourced board operation with explicitly invented cards/leaders; no authentic-game claim.</summary>
public sealed class SelectedTrashReplayPrivacyTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryAcceptedPrefixReplaysSelectionReturnDrawAndTurnBoundary(int players)
    {
        foreach (var branch in new[] { "decline", "ordinary", "reserve", "duplicate", "source" })
        {
            var (state, engine, rules) = Create(players);
            var source = state.Players[0].Deck.Cards[0];
            void Step(GameAction action)
            {
                Apply(engine, action);
                CheckReplay(engine, players, rules);
            }
            CheckReplay(engine, players, rules);
            Step(new StartRoundAction());
            Step(new PlaceAgentAction(0, Card(source), Space("selective-breeding")));
            Step(Resolve<SelectedTrashEffect>(engine));
            var pending = engine.CaptureTrustedSnapshot().Data.Decisions.Single();
            var selection = pending.SelectedTrash!;
            Assert.Null(pending.Context.SourceCard); // The board source is separate from the played card source.
            Assert.Null(pending.Context.SourceOwnerId);
            Assert.Equal(Space("selective-breeding"), pending.Context.Space);
            Assert.Null(pending.Context.SelectedTarget);
            var owner = state.Players[0];
            Assert.Empty(owner.Deck.Cards);
            var random = state.Random.Checkpoint;
            var hand = owner.Hand.Count;
            if (branch == "decline")
            {
                Step(new DeclineTrashAction(0, selection.Id));
                Assert.Equal(hand, owner.Hand.Count);
                Assert.Equal(random, state.Random.Checkpoint);
            }
            else
            {
                var target = branch switch
                {
                    "source" => selection.Targets.Single(x => x.Instance == source.Id),
                    "reserve" => selection.Targets.First(x => x.Card.Definition.Value == "imperium:reserve:arrakis-liaison:v1"),
                    "duplicate" => selection.Targets.Single(x => x.Card.Definition.Value == "imperium:reserve:arrakis-liaison:v1" && x.Card.Copy == 1),
                    _ => selection.Targets.First(x => x.Source == owner.Discard.Id && x.Card.Definition.Value.StartsWith("fixture:", StringComparison.Ordinal))
                };
                var discard = owner.Discard.Cards.Where(x => x.Id != target.Instance).ToArray();
                var expectedRandom = SeededRandom.Restore(random);
                expectedRandom.Shuffle(discard);
                Step(new SelectTrashCardAction(0, selection.Id, target.Source, target.Card));
                Assert.Equal(expectedRandom.Checkpoint, state.Random.Checkpoint);
                Assert.Equal(discard.Take(2), owner.Hand.Cards.TakeLast(2));
                Assert.Equal(hand + 2, owner.Hand.Count);
                var destination = branch is "reserve" or "duplicate"
                    ? state.Shared.Reserves.Single(x => x.Id.Value == "reserve:" + target.Card.Definition.Value)
                    : state.Shared.Trashed;
                Assert.Contains(destination.Cards, x => x.Id == target.Instance);
                Assert.DoesNotContain(owner.Deck.Cards, x => x.Id == target.Instance);
                if (branch == "duplicate") Assert.Single(owner.Hand.Cards.Concat(owner.Deck.Cards).Concat(owner.Discard.Cards),
                    x => x.Definition == target.Card.Definition);
                if (branch == "source") Assert.Contains(state.Shared.Trashed.Cards, x => x.Id == source.Id);
            }
            // The pending nontrivial Agent work survives even when the just-played source moved to trash.
            var agent = engine.CaptureTrustedSnapshot().Data.Pending.Single(x => x.Effect is ForPlayerEffect);
            Assert.Equal(source.Id, agent.Context.SourceCard);
            Assert.Equal(0, agent.Context.SourceOwnerId);
            Assert.Null(agent.Context.SelectedTarget);
            Step(Resolve<ForPlayerEffect>(engine));
            Step(Resolve<SelectedTrashEffect>(engine));
            Assert.Equal(0, engine.CurrentPlayerId);
            Assert.Equal(1, engine.DecisionOwnerId);
            var foreign = engine.CaptureTrustedSnapshot().Data.Decisions.Single();
            var foreignTarget = foreign.SelectedTrash!.Targets.First();
            var water = state.Players[1].Resources.Water;
            Step(new SelectTrashCardAction(1, foreign.SelectedTrash.Id, foreignTarget.Source, foreignTarget.Card));
            Step(Resolve<OptionalPaymentEffect>(engine));
            var child = engine.CaptureTrustedSnapshot().Data.Decisions.Single();
            Assert.IsType<OptionalPaymentEffect>(child.Effect);
            Assert.Equal(source.Id, child.Context.SourceCard);
            Assert.Equal(source.Definition, child.Context.SourceDefinition);
            Assert.Equal(0, child.Context.SourceOwnerId);
            Assert.Equal(new SelectedCardTarget(foreignTarget.Instance, foreignTarget.Card.Definition, foreignTarget.Source, 1), child.Context.SelectedTarget);
            Step(new ChooseOptionAction(1, 1));
            Assert.Equal(water + 2, state.Players[1].Resources.Water);
            Assert.Equal(1, engine.DecisionOwnerId);
            Assert.Equal(1, engine.CurrentPlayerId);
            Assert.Equal(1, state.Players[0].Influence.Single(x => x.Faction == Setup.Faction.BeneGesserit).Amount);
            var turns = new HashSet<int>();
            for (var i = 0; !engine.IsTerminal; i++)
            {
                Assert.True(i < 2000);
                if (engine.CurrentPlayerId is { } turn) turns.Add(turn);
                Step(LifecycleIntegrationTests.Select(engine));
            }
            Assert.Equal(Enumerable.Range(0, players), turns.Order());
            Assert.Equal(10, engine.GetResult().Rounds);
            Assert.Equal(DuneTerminalReason.ConflictDeckExhausted, engine.GetResult().TerminalReason);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryExposedSelectionExecutesFromTheSameAcceptedPrefix(int players)
    {
        var (_, engine, rules) = Pending(players);
        CheckAll();
        Apply(engine, engine.LegalActions(0).OfType<DeclineTrashAction>().Single());
        Apply(engine, Resolve<ForPlayerEffect>(engine));
        Apply(engine, Resolve<SelectedTrashEffect>(engine));
        CheckAll();

        void CheckAll()
        {
            var prefix = engine.CaptureReplay();
            var owner = engine.DecisionOwnerId!.Value;
            var actions = engine.LegalActions(owner).ToArray();
            Assert.Contains(actions, x => x is SelectTrashCardAction);
            Assert.Contains(actions, x => x is DeclineTrashAction);
            foreach (var action in actions)
            {
                var reconstructed = TurnEngine.Replay(prefix, () => LifecycleFixture.CreateSelectedTrashState(players), rules);
                Assert.True(reconstructed.IsSuccess, string.Join(";", reconstructed.Diagnostics));
                Assert.Equal(engine.CaptureTrustedSnapshot().CanonicalJson, reconstructed.Engine!.CaptureTrustedSnapshot().CanonicalJson);
                Apply(reconstructed.Engine, action);
                CheckReplay(reconstructed.Engine, players, rules);
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void PendingTargetsAreVisibleOnlyToTheirOwner(int players)
    {
        var (state, engine, _) = Pending(players);
        AssertProjection(engine, state);
        Apply(engine, engine.LegalActions(0).OfType<SelectTrashCardAction>().First());
        AssertProjection(engine, state);
        Apply(engine, Resolve<ForPlayerEffect>(engine));
        Apply(engine, Resolve<SelectedTrashEffect>(engine));
        Assert.Equal(1, engine.DecisionOwnerId);
        AssertProjection(engine, state);
        Apply(engine, engine.LegalActions(1).OfType<SelectTrashCardAction>().First());
        AssertProjection(engine, state);
        Assert.NotEmpty(state.Shared.Trashed.Cards);
        foreach (var observer in Enumerable.Range(0, players))
            Assert.Contains(state.Shared.Trashed.Cards[0].Definition.Value, Json(engine.Observe(observer)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void NonemptyOpponentHiddenChangesLeaveObservationMaskAndDiagnosticsUnchanged(int players)
    {
        var (_, engine, rules) = Pending(players);
        Counterfactual(0, 1);
        Apply(engine, engine.LegalActions(0).OfType<DeclineTrashAction>().Single());
        Counterfactual(0, 1); // completed board exchange, before foreign work
        Apply(engine, Resolve<ForPlayerEffect>(engine));
        Apply(engine, Resolve<SelectedTrashEffect>(engine));
        Counterfactual(1, 2); // foreign selection
        Apply(engine, engine.LegalActions(1).OfType<SelectTrashCardAction>().First());
        Apply(engine, Resolve<OptionalPaymentEffect>(engine));
        Counterfactual(1, 2); // selected target child
        Apply(engine, new ChooseOptionAction(1, 1));
        Counterfactual(0, 1); // child completed, normal turn resumed
        for (var i = 0; !engine.IsTerminal; i++)
        {
            Assert.True(i < 2000);
            Apply(engine, LifecycleIntegrationTests.Select(engine));
        }
        Counterfactual(0, 1); // endgame draw guarantees nonempty Hand/Intrigue/Deck

        void Counterfactual(int observer, int opponent)
        {
            GameState? state = null;
            var replay = TurnEngine.Replay(engine.CaptureReplay(), () => state = LifecycleFixture.CreateSelectedTrashState(players), rules);
            Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics));
            var paired = replay.Engine!;
            var before = Json(paired.Observe(observer));
            var legal = Json(paired.LegalActions(observer));
            var diagnostics = Json(paired.SupportDiagnostics(observer));
            var publicState = Json(paired.Observe(observer).Game with { Hand = [], Intrigue = [] });
            var counts = state!.Zones.Select(x => (x.Id, x.Count)).ToArray();
            var random = state.Random.Checkpoint;
            var leaderRandom = state.LeadersRandom.Checkpoint;
            var hidden = state.Players[opponent];
            SwapDistinct(hidden.Hand, state.Shared.ImperiumDeck);
            SwapDistinct(hidden.Deck, state.Shared.ImperiumDeck);
            SwapDistinct(hidden.Intrigue, state.Shared.IntrigueDeck);
            hidden.Deck.Shuffle(state.Random);
            state.Random.Next(100);
            state.LeadersRandom.Next(100);
            Assert.NotEqual(random, state.Random.Checkpoint);
            Assert.NotEqual(leaderRandom, state.LeadersRandom.Checkpoint);
            Assert.Equal(counts, state.Zones.Select(x => (x.Id, x.Count)));
            Assert.Equal(publicState, Json(paired.Observe(observer).Game with { Hand = [], Intrigue = [] }));
            Assert.Equal(before, Json(paired.Observe(observer)));
            Assert.Equal(legal, Json(paired.LegalActions(observer)));
            Assert.Equal(diagnostics, Json(paired.SupportDiagnostics(observer)));
            AssertProjection(paired, state);
        }
    }

    [Theory]
    [InlineData("{\"$action\":\"future-trash\",\"playerId\":0}")]
    [InlineData("{\"$action\":\"decline-trash\",\"decisionId\":1,\"playerId\":0,\"targets\":[]}")]
    [InlineData("{\"$action\":\"select-trash-card\",\"decisionId\":1,\"source\":{\"value\":\"own:hand\"},\"card\":{\"definition\":{\"value\":\"fixture:x\"},\"copy\":0,\"instance\":1},\"playerId\":0}")]
    public void StrictSelectionActionJsonRejectsUnknownTagsAndMembers(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameAction>(json, ConfigurationJson.Options));

    [Theory]
    [InlineData("{\"$effect\":\"future-trash\"}")]
    [InlineData("{\"$effect\":\"selected-trash\",\"onTrashed\":[],\"optional\":true,\"targets\":[]}")]
    [InlineData("{\"$effect\":\"selected-trash\",\"onTrashed\":[{\"$effect\":\"gain\",\"resource\":\"Future\",\"amount\":1}],\"optional\":true}")]
    [InlineData("{\"$effect\":\"selected-trash\",\"onTrashed\":[{\"$effect\":\"gain\",\"resource\":0,\"amount\":1}],\"optional\":true}")]
    public void StrictSelectionEffectJsonRejectsUnknownTagsMembersAndEnums(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Effect>(json, ConfigurationJson.Options));

    [Fact]
    public void MalformedSelectionTargetsRoundTripButRejectWithoutMutation()
    {
        var (state, engine, _) = Pending(3);
        var valid = engine.LegalActions(0).OfType<SelectTrashCardAction>().First();
        foreach (var malformed in new[]
        {
            valid with { DecisionId = -1 }, valid with { Source = state.Players[1].Hand.Id },
            valid with { Source = state.Players[0].Deck.Id }, valid with { Card = valid.Card with { Copy = -1 } },
            valid with { Card = valid.Card with { Copy = int.MaxValue } },
            valid with { Card = new(new("fixture:missing")) }, valid with { PlayerId = 1 }
        })
        {
            var action = JsonSerializer.Deserialize<GameAction>(Json<GameAction>(malformed), ConfigurationJson.Options)!;
            Assert.Equal(malformed, action);
            var before = engine.CaptureTrustedSnapshot();
            var replay = Json(engine.CaptureReplay());
            Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);
            Assert.Equal(before.CanonicalJson, engine.CaptureTrustedSnapshot().CanonicalJson);
            Assert.Equal(before.Hash, engine.CaptureTrustedSnapshot().Hash);
            Assert.Equal(replay, Json(engine.CaptureReplay()));
        }
    }

    private static (GameState State, TurnEngine Engine, TurnRuleSet Rules) Create(int players)
    {
        var state = LifecycleFixture.CreateSelectedTrashState(players);
        var rules = LifecycleFixture.SelectedTrashRules(state);
        return (state, new(state, rules), rules);
    }

    private static (GameState State, TurnEngine Engine, TurnRuleSet Rules) Pending(int players)
    {
        var result = Create(players);
        var source = result.State.Players[0].Deck.Cards[0];
        Apply(result.Engine, new StartRoundAction());
        Apply(result.Engine, new PlaceAgentAction(0, Card(source), Space("selective-breeding")));
        Apply(result.Engine, Resolve<SelectedTrashEffect>(result.Engine));
        return result;
    }

    private static ResolveEffectAction Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        return new(owner, engine.Observe(owner).PendingEffects.First(x => x.Kind == typeof(T).Name).Id);
    }

    private static void CheckReplay(TurnEngine engine, int players, TurnRuleSet rules)
    {
        var expected = engine.CaptureTrustedSnapshot();
        var replay = JsonSerializer.Deserialize<TurnReplay>(Json(engine.CaptureReplay()), ConfigurationJson.Options)!;
        GameState? state = null;
        var result = TurnEngine.Replay(replay, () => state = LifecycleFixture.CreateSelectedTrashState(players), rules);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics));
        var actual = result.Engine!.CaptureTrustedSnapshot();
        Assert.Equal(expected.CanonicalJson, actual.CanonicalJson);
        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(Json(expected.Data.Frames), Json(actual.Data.Frames)); // Includes source/target bindings and suspended children.
        Assert.Equal(replay.Actions, result.Engine.CaptureReplay().Actions);
        Assert.Equal(Json(replay), Json(result.Engine.CaptureReplay()));
        Assert.Equal(engine.CurrentPlayerId, result.Engine.CurrentPlayerId);
        Assert.Equal(engine.DecisionOwnerId, result.Engine.DecisionOwnerId);
        Assert.Equal(engine.Phase, result.Engine.Phase);
        Assert.Equal(expected.Data.Game.MainRandom, state!.Random.Checkpoint);
        Assert.Equal(expected.Data.Game.LeaderRandom, state.LeadersRandom.Checkpoint);
        Assert.Equal(expected.CanonicalJson, Json(JsonSerializer.Deserialize<TurnSnapshotData>(expected.CanonicalJson, ConfigurationJson.Options)));
        foreach (var player in Enumerable.Range(0, players))
        {
            Assert.Equal(Json(engine.LegalActions(player)), Json(result.Engine.LegalActions(player)));
            Assert.Equal(Json(engine.Observe(player)), Json(result.Engine.Observe(player)));
        }
        if (engine.IsTerminal) Assert.Equal(engine.GetResult().ReplayIdentity, result.Engine.GetResult().ReplayIdentity);
    }

    private static void AssertProjection(TurnEngine engine, GameState state)
    {
        foreach (var player in state.Players)
        {
            var observation = engine.Observe(player.Id);
            var json = Json(observation) + Json(engine.LegalActions(player.Id)) + Json(engine.SupportDiagnostics(player.Id));
            foreach (var name in new[] { "instance", "sourceCard", "sourceDefinition", "sourceOwnerId", "selectedTarget", "targets", "bindings", "frames", "actions", "history", "snapshot", "seed", "initialStateHash",
                "mainRandom", "leaderRandom", "rawDrawCount", "environmentSeed", "leaderSeed", "rulesIdentity", "replayIdentity", "parentFrameId", "continuation", "initiatorId", "payerId", "recipientId" })
                Assert.DoesNotContain("\"" + name + "\"", json, StringComparison.OrdinalIgnoreCase);
            foreach (var card in state.Zones.SelectMany(x => x.Cards)) Assert.DoesNotContain(Json(card.Id), json);
            if (player.Id != engine.DecisionOwnerId)
            {
                Assert.Null(observation.Decision);
                Assert.Empty(observation.PendingEffects);
                Assert.Empty(observation.LegalActions);
                Assert.Empty(engine.LegalActions(player.Id));
            }
            else if (engine.CaptureTrustedSnapshot().Data.Decisions.LastOrDefault()?.SelectedTrash is { } selection)
            {
                Assert.NotNull(observation.Decision);
                Assert.Contains(engine.LegalActions(player.Id), x => x is DeclineTrashAction);
                Assert.Equal(selection.Targets.Length, engine.LegalActions(player.Id).OfType<SelectTrashCardAction>().Count());
            }
        }
    }

    private static void SwapDistinct(CardZone hidden, CardZone stock)
    {
        Assert.NotEmpty(hidden.Cards); // Distinct faces change the ordered sequence; never count an empty case.
        var before = hidden.Cards.Select(x => x.Definition).ToArray();
        var a = hidden.Cards[0];
        var b = stock.Cards.First(x => x.Definition != a.Definition);
        hidden.MoveTo(a.Id, stock);
        stock.MoveTo(b.Id, hidden);
        Assert.NotEqual(a.Definition, b.Definition);
        Assert.False(before.SequenceEqual(hidden.Cards.Select(x => x.Definition)));
        Assert.Equal(b.Definition, hidden.Cards[^1].Definition);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ConfigurationJson.Options);
}
