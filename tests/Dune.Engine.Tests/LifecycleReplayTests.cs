using System.Collections.Immutable;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Runtime;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class LifecycleReplayTests
{
    [Fact]
    public void LifecycleContractsUseExplicitV7TurnSchemasAndSupportVersions()
    {
        var state = CreateState();
        var engine = new TurnEngine(state, Rules(state));
        Assert.Equal("dune-setup-snapshot-v5", state.CaptureTrustedSnapshot().Data.Schema);
        Assert.Equal("dune-turn-snapshot-v7", engine.CaptureTrustedSnapshot().Data.Schema);
        Assert.Equal("dune-setup-snapshot-v5", engine.CaptureTrustedSnapshot().Data.Game.Schema);
        Assert.Equal("dune-turn-replay-v7", engine.CaptureReplay().Schema);
        Assert.Equal("original-imperium-turn-support-v12", engine.CaptureReplay().SupportVersion);
        Assert.Equal("imperium-2020-turn-preview-v12", OriginalImperiumTurnRules.Version);
        Assert.Equal("imperium-2020-turn-preview-v12", OriginalImperiumTurnRules.Create().Version);
        Assert.Equal("dune-configuration-sha256-v1", state.CaptureTrustedSnapshot().Data.ConfigurationIdentityFormat);
    }

    [Fact]
    public void V3ReplaysBeforeInsideAndAfterForeignAcquireChildPreserveLegalActionsAndHash()
    {
        var state = CreateState();
        var market = state.Shared.ImperiumRow.Cards[0];
        var rules = CardEffects(Rules(state), market.Definition, acquire:
            [new ForPlayerEffect(1, [new OptionalPaymentEffect(new(), [new GainEffect(ResourceKind.Water, 2)])])
            { DecisionOwnerId = 2, PayerId = 0 }]);
        var engine = new TurnEngine(state, rules);
        Check(engine, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new RevealAction(0));
        ResolveAll(engine);
        Check(engine, rules);
        Apply(engine, new AcquireCardAction(0, Card(market), state.Shared.ImperiumRow.Id));
        Check(engine, rules);
        Resolve<ForPlayerEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        Check(engine, rules);
        Resolve<OptionalPaymentEffect>(engine);
        var snapshot = engine.CaptureTrustedSnapshot();
        var frame = snapshot.Data.Frames[^1];
        var context = frame.Decision!.Context;
        Assert.Equal(0, context.InitiatorId);
        Assert.Equal(2, context.DecisionOwnerId);
        Assert.Equal(0, context.PayerId);
        Assert.Equal(1, context.RecipientId);
        Assert.Equal(market.Id, context.SourceCard);
        Assert.Equal(market.Definition, context.SourceDefinition);
        Assert.Equal(EffectProvenance.Acquisition, context.Provenance);
        Assert.Equal(frame.Id, context.ParentFrameId);
        Assert.Equal(snapshot.Data.Frames[^2].Id, frame.ParentId);
        Assert.DoesNotContain("\"pending\":", snapshot.CanonicalJson);
        Assert.DoesNotContain("\"decisions\":", snapshot.CanonicalJson);
        Assert.Equal("dune-setup-snapshot-v5", snapshot.Data.Game.Schema);
        var copy = JsonSerializer.Deserialize<TurnSnapshotData>(snapshot.CanonicalJson, ConfigurationJson.Options)!;
        Assert.Equal(snapshot.CanonicalJson, JsonSerializer.Serialize(copy, ConfigurationJson.Options));
        Check(engine, rules);
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(3, state.Players[1].Resources.Water);
        Assert.Equal(0, engine.DecisionOwnerId);
        Check(engine, rules);
        FinishPlayerTurns(engine);
        Check(engine, rules);
    }

    [Theory]
    [InlineData("dune-turn-replay-v1", "original-imperium-turn-support-v4")]
    [InlineData("dune-turn-replay-v2", "original-imperium-turn-support-v4")]
    [InlineData("dune-turn-replay-v1", "original-imperium-turn-support-v5")]
    [InlineData("dune-turn-replay-v2", "original-imperium-turn-support-v5")]
    [InlineData("dune-turn-replay-v1", "original-imperium-turn-support-v8")]
    [InlineData("dune-turn-replay-v2", "original-imperium-turn-support-v8")]
    [InlineData("dune-turn-replay-v3", "original-imperium-turn-support-v7")]
    [InlineData("dune-turn-replay-v3", "original-imperium-turn-support-v8")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v7")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v2")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v3")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v4")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v5")]
    [InlineData("dune-turn-replay-v1", "original-imperium-turn-support-v6")]
    [InlineData("dune-turn-replay-v2", "original-imperium-turn-support-v6")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v6")]
    [InlineData("dune-turn-replay-v4", "original-imperium-turn-support-v8")]
    [InlineData("dune-turn-replay-v5", "original-imperium-turn-support-v8")]
    [InlineData("dune-turn-replay-v5", "original-imperium-turn-support-v9")]
    [InlineData("dune-turn-replay-v5", "original-imperium-turn-support-v10")]
    [InlineData("dune-turn-replay-v6", "original-imperium-turn-support-v10")]
    [InlineData("dune-turn-replay-v6", "original-imperium-turn-support-v11")]
    [InlineData("dune-turn-replay-v7", "original-imperium-turn-support-v11")]
    public void OlderReplayMetadataFailsValidationBeforeAnyActionsAreApplied(string schema, string supportVersion)
    {
        var state = CreateState();
        var rules = Rules(state);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        // Keep current hashes and other metadata identical: old schema/support labels alone must reject.
        var replay = engine.CaptureReplay() with { Schema = schema, SupportVersion = supportVersion };
        var untouched = CreateState();
        var before = untouched.CaptureTrustedSnapshot().Hash;
        var result = TurnEngine.Replay(replay, () => untouched, rules);
        Assert.False(result.IsSuccess);
        Assert.Equal("replay-version", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(before, untouched.CaptureTrustedSnapshot().Hash);
        Assert.Equal(GamePhase.BeforeRoundStart, untouched.Phase);
    }

    [Fact]
    public void CanonicalEffectsAndExistingActionTagsHaveExactGoldens()
    {
        ImmutableArray<Effect> effects = [new SequenceEffect([new PlayerOrderedEffect([
            new OptionalPaymentEffect(new(Water: 1), [new GainEffect(ResourceKind.Spice, 2)], 2) { PayerId = 0, RecipientId = 1 }])])];
        const string expected = "[{\"$effect\":\"sequence\",\"effects\":[{\"$effect\":\"player-ordered\",\"effects\":[{\"$effect\":\"optional\",\"cost\":{\"solari\":0,\"spice\":0,\"water\":1},\"effects\":[{\"$effect\":\"gain\",\"resource\":\"Spice\",\"amount\":2}],\"decisionOwnerId\":2,\"payerId\":0,\"recipientId\":1}]}]}]";
        Assert.Equal(expected, JsonSerializer.Serialize(effects, ConfigurationJson.Options));
        Assert.Equal(expected, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<Effect>>(expected, ConfigurationJson.Options), ConfigurationJson.Options));
        ImmutableArray<GameAction> actions = [new StartRoundAction(), new RevealAction(0), new ResolveEffectAction(0, 3), new ChooseOptionAction(2, 1), new DeployAction(0, 2), new EndRevealAction(0)];
        Assert.Equal("[{\"$action\":\"start-round\",\"playerId\":-1},{\"$action\":\"reveal\",\"playerId\":0},{\"$action\":\"effect\",\"effectId\":3,\"playerId\":0},{\"$action\":\"option\",\"option\":1,\"playerId\":2},{\"$action\":\"deploy\",\"troops\":2,\"playerId\":0},{\"$action\":\"end-reveal\",\"playerId\":0}]",
            JsonSerializer.Serialize(actions, ConfigurationJson.Options));
    }

    [Fact]
    public void PlacementAndAcquisitionActionTagsRetainCanonicalSelectors()
    {
        GameAction place = new PlaceAgentAction(0, new(new("fixture:card")), new("fixture:space"));
        GameAction acquire = new AcquireCardAction(0, new(new("fixture:card")), new("fixture:row"));
        Assert.Equal("{\"$action\":\"place\",\"card\":{\"definition\":{\"value\":\"fixture:card\"},\"copy\":0},\"space\":{\"value\":\"fixture:space\"},\"paymentOption\":0,\"playerId\":0}", JsonSerializer.Serialize(place, ConfigurationJson.Options));
        Assert.Equal("{\"$action\":\"acquire\",\"card\":{\"definition\":{\"value\":\"fixture:card\"},\"copy\":0},\"source\":{\"value\":\"fixture:row\"},\"playerId\":0}", JsonSerializer.Serialize(acquire, ConfigurationJson.Options));
    }

    [Fact]
    public void CanonicalRulesIdentityIgnoresDefinitionRegistrationOrder()
    {
        var state = CreateState();
        var rules = Rules(state);
        var first = new TurnEngine(state, rules);
        var second = new TurnEngine(CreateState(), rules with { Cards = [.. rules.Cards.Reverse()], Spaces = [.. rules.Spaces.Reverse()], Leaders = [.. rules.Leaders.Reverse()] });
        Assert.Equal(first.CaptureReplay().RulesIdentity, second.CaptureReplay().RulesIdentity);
        Assert.Equal(first.CaptureTrustedSnapshot().Hash, second.CaptureTrustedSnapshot().Hash);
        Assert.Equal("original-imperium-turn-support-v12", first.CaptureReplay().SupportVersion);
        Assert.Equal("imperium-2020-turn-preview-v12", OriginalImperiumTurnRules.Version);
    }

    [Theory]
    [InlineData("[{\"$effect\":\"sequence\",\"effects\":[],\"unknown\":true}]")]
    [InlineData("[{\"$effect\":\"gain\",\"resource\":\"FutureResource\",\"amount\":1}]")]
    [InlineData("[{\"$effect\":\"gain\",\"resource\":0,\"amount\":1}]")]
    [InlineData("[{\"$effect\":\"future-effect\"}]")]
    public void StrictEffectJsonRejectsUnknownMembersEnumsAndTags(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ImmutableArray<Effect>>(json, ConfigurationJson.Options));

    [Fact]
    public void StrictFrameAndActionJsonRejectUnknownMembersEnumsAndTags()
    {
        var frame = new EffectExecutionFrame(1, null, EffectGroupMode.Sequence, 0, [], new(ExecutionContinuationKind.AgentTurn, 0));
        var json = JsonSerializer.Serialize(frame, ConfigurationJson.Options);
        Assert.Equal("{\"id\":1,\"parentId\":null,\"mode\":\"Sequence\",\"chooserId\":0,\"remaining\":[],\"continuation\":{\"kind\":\"AgentTurn\",\"playerId\":0,\"windowId\":null,\"rewardId\":null},\"decision\":null}", json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EffectExecutionFrame>(json.Replace("\"Sequence\"", "\"FutureMode\"", StringComparison.Ordinal), ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EffectExecutionFrame>(json[..^1] + ",\"future\":true}", ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameAction>("{\"$action\":\"future-action\",\"playerId\":0}", ConfigurationJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameAction>("{\"$action\":\"reveal\",\"playerId\":0,\"future\":true}", ConfigurationJson.Options));
    }

    private static void Check(TurnEngine engine, TurnRuleSet rules)
    {
        var replayJson = JsonSerializer.Serialize(engine.CaptureReplay(), ConfigurationJson.Options);
        var replay = JsonSerializer.Deserialize<TurnReplay>(replayJson, ConfigurationJson.Options)!;
        Assert.Equal("dune-turn-replay-v7", replay.Schema);
        var result = TurnEngine.Replay(replay, () => CreateState(), rules);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, result.Engine!.CaptureTrustedSnapshot().Hash);
        foreach (var player in Enumerable.Range(0, 3)) Assert.Equal(engine.LegalActions(player), result.Engine.LegalActions(player));
        Assert.Equal(engine.CaptureTrustedSnapshot().Data.NextFrameId, result.Engine.CaptureTrustedSnapshot().Data.NextFrameId);
        Assert.Equal(engine.CaptureTrustedSnapshot().Data.NextEffectId, result.Engine.CaptureTrustedSnapshot().Data.NextEffectId);
    }

    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new ResolveEffectAction(owner, engine.Observe(owner).PendingEffects.Single(x => x.Kind == typeof(T).Name).Id));
    }
}
