using System.Collections.Immutable;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;

namespace Dune.Engine.Tests;

public sealed class FactionRewardTests
{
    [Fact]
    public void OrderedOneTwoOneTwoAdjustsVictoryPointsAtEveryCrossing()
    {
        var (state, engine, action, _) = Prepare(Faction.Emperor, 1,
            [new SequenceEffect([new InfluenceEffect(Faction.Emperor), Pause(),
                new InfluenceEffect(Faction.Emperor, -1), Pause(), new InfluenceEffect(Faction.Emperor)])]);
        Apply(engine, action);
        Resolve<SequenceEffect>(engine);
        Assert.Equal(2, Track(state, 0, Faction.Emperor));
        Assert.Equal(1, state.Players[0].Resources.VictoryPoints);
        ContinuePause(engine);
        Assert.Equal(1, Track(state, 0, Faction.Emperor));
        Assert.Equal(0, state.Players[0].Resources.VictoryPoints);
        ContinuePause(engine);
        Assert.Equal(2, Track(state, 0, Faction.Emperor));
        Assert.Equal(1, state.Players[0].Resources.VictoryPoints);
    }

    [Theory]
    [InlineData(Faction.Emperor)]
    [InlineData(Faction.SpacingGuild)]
    [InlineData(Faction.BeneGesserit)]
    [InlineData(Faction.Fremen)]
    public void FourRewardIsGrantedAgainOnUpwardRecrossAndLossKeepsEarnedResources(Faction faction)
    {
        var (state, engine, action, _) = Prepare(faction, 3,
            [new SequenceEffect([new InfluenceEffect(faction), Pause(), new InfluenceEffect(faction, -1), Pause(), new InfluenceEffect(faction)])]);
        var physical = state.Zones.Sum(x => x.Count);
        var troops = Troops(state.Players[0]);
        var before = RewardCount(state, faction);
        Apply(engine, action);
        Resolve<SequenceEffect>(engine);
        Assert.Equal(before + RewardAmount(faction), RewardCount(state, faction));
        Assert.Equal(2, state.Players[0].Resources.VictoryPoints);
        ContinuePause(engine);
        Assert.Equal(before + RewardAmount(faction), RewardCount(state, faction));
        Assert.Null(state.Board.Alliances.Single(x => x.Faction == faction).OwnerId);
        Assert.Equal(1, state.Players[0].Resources.VictoryPoints);
        ContinuePause(engine);
        Assert.Equal(before + 2 * RewardAmount(faction), RewardCount(state, faction));
        Assert.Equal(2, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(physical, state.Zones.Sum(x => x.Count));
        Assert.Equal(troops, Troops(state.Players[0]));
        if (faction == Faction.BeneGesserit) Assert.Equal(38, state.Shared.IntrigueDeck.Count);
    }

    [Fact]
    public void PlayerOrderedMonotoneGainsAcquireOneAllianceAndOneThresholdReward()
    {
        var (state, engine, action, _) = Prepare(Faction.SpacingGuild, 1,
            [new InfluenceEffect(Faction.SpacingGuild), new InfluenceEffect(Faction.SpacingGuild), new InfluenceEffect(Faction.SpacingGuild)]);
        Apply(engine, action);
        Assert.Equal(4, Track(state, 0, Faction.SpacingGuild));
        Assert.Equal(2, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(3, state.Players[0].Resources.Solari);
        Assert.Equal(0, state.Board.Alliances.Single(x => x.Faction == Faction.SpacingGuild).OwnerId);
    }

    [Fact]
    public void AutomaticPrimitiveDrainSuspendsParentSiblingUntilThresholdDecisionCompletes()
    {
        var (state, engine, action, _) = Prepare(Faction.Fremen, 3,
            [new InfluenceEffect(Faction.Fremen), new GainEffect(ResourceKind.Spice, 7)],
            [new OptionalPaymentEffect(new(Water: 1), [new GainEffect(ResourceKind.Solari, 2)])]);
        Apply(engine, action);
        Assert.Equal(4, Track(state, 0, Faction.Fremen));
        Assert.Equal(0, state.Players[0].Resources.Spice);
        var frames = engine.CaptureTrustedSnapshot().Data.Frames;
        Assert.Equal(2, frames.Length);
        Assert.Equal(frames[0].Id, frames[1].ParentId);
        Assert.IsType<GainEffect>(Assert.Single(frames[0].Remaining).Effect);
        Resolve<OptionalPaymentEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Equal(2, state.Players[0].Resources.Solari);
        Assert.Equal(7, state.Players[0].Resources.Spice);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    public void EqualityKeepsIncumbentAndStrictLeadTransfersExactlyOnePoint(int challengerAfter, int expectedHolder)
    {
        var (state, engine, action, _) = Prepare(Faction.Fremen, 4,
            [new ForPlayerEffect(1, [new InfluenceEffect(Faction.Fremen, challengerAfter - 3)])], configure: state =>
            {
                SetTrack(state, 1, Faction.Fremen, 3);
                SetHolder(state, Faction.Fremen, 0);
                state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 2 };
            });
        Apply(engine, action);
        ResolveAll(engine);
        Assert.Equal(expectedHolder, state.Board.Alliances.Single(x => x.Faction == Faction.Fremen).OwnerId);
        Assert.Equal(expectedHolder == 0 ? 2 : 1, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(expectedHolder == 1 ? 2 : 1, state.Players[1].Resources.VictoryPoints);
    }

    [Theory]
    [InlineData(0, 6, 4, -1, true)]
    [InlineData(0, 5, 4, -1, false)]
    [InlineData(0, 4, 4, -1, false)]
    [InlineData(1, 5, 4, -1, true)]
    public void HolderLossRequiresAnUnambiguousRemainingOwner(int recipient, int holderTrack, int rivalTrack, int amount, bool supported)
    {
        var (state, engine, action, _) = Prepare(Faction.Fremen, holderTrack,
            [new ForPlayerEffect(recipient, [new InfluenceEffect(Faction.Fremen, amount)])], configure: state =>
            {
                SetTrack(state, 1, Faction.Fremen, rivalTrack);
                SetHolder(state, Faction.Fremen, 0);
                state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 2 };
            });
        if (!supported) { RejectUnchanged(engine, action, "alliance-loss"); return; }
        Apply(engine, action);
        ResolveAll(engine);
        Assert.Equal(0, state.Board.Alliances.Single(x => x.Faction == Faction.Fremen).OwnerId);
        Assert.Equal((recipient == 0 ? holderTrack : rivalTrack) - 1, Track(state, recipient, Faction.Fremen));
    }

    [Theory]
    [InlineData(int.MaxValue, 6)]
    [InlineData(int.MinValue, 0)]
    public void SignedMovementClampsToTrackEndpointsWithoutExtraAccessRestriction(int amount, int expected)
    {
        var (state, engine, action, _) = Prepare(Faction.Emperor, 6, [new InfluenceEffect(Faction.Emperor, amount)], configure: state =>
        {
            SetHolder(state, Faction.Emperor, 0);
            state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 2 };
        });
        Apply(engine, action);
        Assert.Equal(expected, Track(state, 0, Faction.Emperor));
        Assert.Equal(expected == 6 ? 2 : 0, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(3, state.Players[0].Units.TroopsInGarrison);
    }

    [Fact]
    public void ThresholdChildFinishesBeforeParentSiblingAndPreservesForeignRolesAndReplay()
    {
        var reward = ImmutableArray.Create<Effect>(new DrawIntrigueEffect(1),
            new OptionalPaymentEffect(new(Water: 1), [new GainEffect(ResourceKind.Solari, 2)]));
        var (state, engine, action, rules) = Prepare(Faction.BeneGesserit, 0,
            [new ForPlayerEffect(1, [new SequenceEffect([new InfluenceEffect(Faction.BeneGesserit), new GainEffect(ResourceKind.Spice, 7)])])
            { DecisionOwnerId = 2, PayerId = 0 }], reward,
            state => SetTrack(state, 1, Faction.BeneGesserit, 3));
        Apply(engine, action);
        Resolve<ForPlayerEffect>(engine);
        Resolve<SequenceEffect>(engine);
        Assert.Equal(2, engine.DecisionOwnerId);
        Resolve<DrawIntrigueEffect>(engine);
        Assert.Single(state.Players[1].Intrigue.Cards);
        Assert.Equal(0, state.Players[1].Resources.Spice);
        Resolve<OptionalPaymentEffect>(engine);
        var context = engine.CaptureTrustedSnapshot().Data.Frames[^1].Decision!.Context;
        Assert.Equal((2, 0, 1), (context.DecisionOwnerId, context.PayerId, context.RecipientId));
        CheckReplay(engine, rules);
        Apply(engine, new ChooseOptionAction(2, 1));
        Assert.Equal(0, state.Players[0].Resources.Water);
        Assert.Equal(2, state.Players[1].Resources.Solari);
        Assert.Equal(7, state.Players[1].Resources.Spice);
        Assert.Empty(state.Players[2].Intrigue.Cards);
        CheckReplay(engine, rules);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntrigueExhaustionRejectsBeforeActionCommitAndNeverRecycles(bool threshold)
    {
        var (state, engine, action, _) = Prepare(Faction.BeneGesserit, 3,
            threshold ? [new InfluenceEffect(Faction.BeneGesserit)] : [new DrawIntrigueEffect(1)], configure: EmptyIntrigueDeck);
        RejectUnchanged(engine, action, "intrigue-empty");
        Assert.Empty(state.Players[0].Intrigue.Cards);
        Assert.Equal(40, state.Shared.IntrigueDiscard.Count);
    }

    [Fact]
    public void OptionalUnavailableIntrigueDrawCanBeDeclinedButCannotBePaid()
    {
        var (_, engine, action, _) = Prepare(Faction.BeneGesserit, 0,
            [new OptionalPaymentEffect(new(Water: 1), [new DrawIntrigueEffect(1)])], configure: EmptyIntrigueDeck);
        Apply(engine, action);
        Resolve<OptionalPaymentEffect>(engine);
        RejectUnchanged(engine, new ChooseOptionAction(0, 1), "intrigue-empty");
        Apply(engine, new ChooseOptionAction(0, 0));
    }

    [Fact]
    public void ThresholdChoiceReservesIntrigueForMandatorySiblingAndCanChooseEmptyAlternative()
    {
        var (state, engine, action, _) = Prepare(Faction.Fremen, 3,
            [new InfluenceEffect(Faction.Fremen), new DrawIntrigueEffect(1)],
            [new ChoiceEffect([new("Draw", new(), [new DrawIntrigueEffect(1)]), new("Decline", new(), [])])],
            state => { while (state.Shared.IntrigueDeck.Count > 1) state.Shared.IntrigueDeck.TryDrawTo(state.Shared.IntrigueDiscard); });
        Apply(engine, action);
        // The root has two different primitives, so the player chooses Influence first.
        Resolve<InfluenceEffect>(engine);
        Resolve<ChoiceEffect>(engine);
        RejectUnchanged(engine, new ChooseOptionAction(0, 0), "intrigue-empty");
        Apply(engine, new ChooseOptionAction(0, 1));
        Assert.Single(state.Players[0].Intrigue.Cards);
        Assert.Empty(state.Shared.IntrigueDeck.Cards);
    }

    [Fact]
    public void ChoiceMustHaveOneOptionSatisfyingBothIntrigueStockAndResourceCapacity()
    {
        var (_, engine, action, _) = Prepare(Faction.Fremen, 3, [new InfluenceEffect(Faction.Fremen)],
            [new ChoiceEffect([
                new("Missing Intrigue", new(), [new DrawIntrigueEffect(1)]),
                new("Overflow", new(), [new GainEffect(ResourceKind.Water, int.MaxValue)])])], EmptyIntrigueDeck);
        RejectUnchanged(engine, action, "choice-unavailable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignedSequenceChecksVictoryPointPrefixesBeforeAnyMutation(bool overflowing)
    {
        var (_, engine, action, _) = Prepare(Faction.Emperor, overflowing ? 1 : 2,
            [new SequenceEffect([new InfluenceEffect(Faction.Emperor, overflowing ? 1 : -1),
                new InfluenceEffect(Faction.Emperor, overflowing ? -1 : 1)])], configure: state =>
                state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = int.MaxValue });
        if (overflowing) RejectUnchanged(engine, action, "resource-bounds");
        else { Apply(engine, action); Resolve<SequenceEffect>(engine); }
    }

    [Fact]
    public void UnorderedSignedInfluenceRejectsBeforeMutationWhileDistinctMonotoneFactionsWork()
    {
        var (_, unsafeEngine, unsafeAction, _) = Prepare(Faction.Emperor, 3,
            [new InfluenceEffect(Faction.Emperor), new InfluenceEffect(Faction.Emperor, -1)]);
        RejectUnchanged(unsafeEngine, unsafeAction, "influence-order");
        var (state, engine, action, _) = Prepare(Faction.Emperor, 3,
            [new InfluenceEffect(Faction.Emperor), new InfluenceEffect(Faction.Fremen)],
            configure: state => SetTrack(state, 0, Faction.Fremen, 3));
        Apply(engine, action);
        Assert.Equal(4, Track(state, 0, Faction.Emperor));
        Assert.Equal(4, Track(state, 0, Faction.Fremen));
        Assert.Equal(2, state.Board.Alliances.Count(x => x.OwnerId == 0));
    }

    [Fact]
    public void FactionSpaceRemainsUsableAtTopAndDoesNotRepeatEarnedReward()
    {
        var (state, engine, _, _) = Prepare(Faction.Emperor, 6, [], configure: state =>
        {
            SetHolder(state, Faction.Emperor, 0);
            state.Players[0].Resources = state.Players[0].Resources with { VictoryPoints = 2 };
        });
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("wealth")));
        Assert.Equal(6, Track(state, 0, Faction.Emperor));
        Assert.Equal(2, state.Players[0].Resources.VictoryPoints);
        Assert.Equal(3, state.Players[0].Units.TroopsInGarrison);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConditionalInfluenceRequiresACommonTrackOutcomeBeforeAnotherAdvance(bool commonOutcome)
    {
        var (state, engine, action, _) = Prepare(Faction.Emperor, 0,
            [new SequenceEffect([new ChoiceEffect([
                new("Advance", new(), [new InfluenceEffect(Faction.Emperor)]),
                new("Alternative", new(), commonOutcome ? [new InfluenceEffect(Faction.Emperor)] : [])]),
                new InfluenceEffect(Faction.Emperor)])]);
        if (!commonOutcome) { RejectUnchanged(engine, action, "influence-branch"); return; }
        Apply(engine, action);
        Resolve<SequenceEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 0));
        Assert.Equal(2, Track(state, 0, Faction.Emperor));
        Assert.Equal(1, state.Players[0].Resources.VictoryPoints);
    }

    [Fact]
    public void UnorderedInfluenceWithDifferentThresholdPayersRequiresSequence()
    {
        var (_, engine, action, _) = Prepare(Faction.Fremen, 2,
            [new ForPlayerEffect(0, [new InfluenceEffect(Faction.Fremen)]) { PayerId = 1 },
             new ForPlayerEffect(0, [new InfluenceEffect(Faction.Fremen)]) { PayerId = 2 }]);
        RejectUnchanged(engine, action, "influence-order");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void MalformedTrackStateIsRejectedRatherThanSilentlyClamped(int initial)
    {
        var (_, engine, action, _) = Prepare(Faction.Emperor, initial, [new InfluenceEffect(Faction.Emperor)]);
        RejectUnchanged(engine, action, "influence-bounds");
    }

    [Theory]
    [InlineData("deck")]
    [InlineData("stock")]
    [InlineData("permanent")]
    [InlineData("capacity")]
    [InlineData("funding")]
    public void ThresholdChildrenParticipateInMandatorySiblingSafety(string kind)
    {
        ImmutableArray<Effect> bonus = kind switch
        {
            "deck" => [new DrawIntrigueEffect(1)],
            "stock" => [new AcquireEffect(new("imperium:reserve:spice-must-flow:v1"))],
            "permanent" => [new HighCouncilEffect()],
            "capacity" => [new GainEffect(ResourceKind.Water, int.MaxValue)],
            "funding" => [new ChoiceEffect([new("Pay", new(Water: 2), [new GainEffect(ResourceKind.Spice, 1)])])],
            _ => throw new InvalidOperationException(),
        };
        var (state, engine, action, _) = Prepare(Faction.Fremen, 3,
            kind == "deck" ? [new InfluenceEffect(Faction.Fremen), new DrawIntrigueEffect(1)] :
            kind == "permanent" ? [new InfluenceEffect(Faction.Fremen), new HighCouncilEffect()] : [new InfluenceEffect(Faction.Fremen)],
            bonus, state =>
            {
                if (kind == "deck") while (state.Shared.IntrigueDeck.Count > 1) state.Shared.IntrigueDeck.TryDrawTo(state.Shared.IntrigueDiscard);
                if (kind == "stock") foreach (var zone in state.Shared.Reserves)
                    foreach (var card in zone.Cards.ToArray()) zone.MoveTo(card.Id, state.Shared.Trashed);
            });
        var before = engine.CaptureTrustedSnapshot().Hash;
        Assert.False(engine.Step(action).IsSuccess);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
        Assert.Equal(3, Track(state, 0, Faction.Fremen));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(99)]
    public void OriginalFactionPolicyRejectsUnsupportedMaximumBeforeStartup(int maximum)
    {
        var state = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            { Factions = [OriginalImperiumTurnRules.CreateLifecycleRules().Factions[0] with { MaximumInfluence = maximum }] }
        };
        RejectUnchanged(new TurnEngine(state, rules), new StartRoundAction(), "lifecycle-shape");
    }

    [Fact]
    public void PreviewWithoutFactionDefinitionsRetainsThresholdGate()
    {
        var state = CreateState();
        SetTrack(state, 0, Faction.Emperor, 3);
        var engine = new TurnEngine(state, Rules(state));
        Apply(engine, new StartRoundAction());
        RejectUnchanged(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space("wealth")), "alliance-bonus");
    }

    private static (GameState State, TurnEngine Engine, PlaceAgentAction Action, TurnRuleSet Rules) Prepare(
        Faction faction, int initial, ImmutableArray<Effect> effects, ImmutableArray<Effect>? reward = null, Action<GameState>? configure = null)
    {
        var state = Initial();
        SetTrack(state, 0, faction, initial);
        configure?.Invoke(state);
        var card = state.Players[0].Deck.Cards[0];
        var rules = LifecycleFixture.Rules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Factions =
            [.. OriginalImperiumTurnRules.CreateLifecycleRules().Factions.Select(x => x.Faction == faction && reward is { } bonus ? x with
            {
                ThresholdFourReward = bonus,
                Sources = [new("Synthetic faction reward fixture, not official content", 1, "Invented threshold reward graph")],
            } : x)]
            },
            Spaces = [.. rules.Spaces.Select(x => x.Id == Space("secure-contract") ? x with { Effects = [] } : x)]
        };
        rules = CardEffects(rules, card.Definition, agent: effects);
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        return (state, engine, new(0, Card(card), Space("secure-contract")), rules);
    }

    private static GameState Initial() => LifecycleFixture.CreateState();
    private static OptionalPaymentEffect Pause() => new(new(), []);
    private static void ContinuePause(TurnEngine engine)
    {
        if (engine.CaptureTrustedSnapshot().Data.Frames[^1].Decision is null) Resolve<OptionalPaymentEffect>(engine);
        Apply(engine, new ChooseOptionAction(0, 0));
    }
    private static int Track(GameState state, int owner, Faction faction) => state.Players[owner].Influence.Single(x => x.Faction == faction).Amount;
    private static void SetTrack(GameState state, int owner, Faction faction, int amount)
    {
        state.Players[owner].Influence = [.. state.Players[owner].Influence.Select(x => x.Faction == faction ? x with { Amount = amount } : x)];
        state.Players[owner].Resources = state.Players[owner].Resources with { VictoryPoints = amount >= 2 ? 1 : 0 };
    }
    private static void SetHolder(GameState state, Faction faction, int owner) =>
        state.Board.Alliances = [.. state.Board.Alliances.Select(x => x.Faction == faction ? x with { OwnerId = owner } : x)];
    private static void EmptyIntrigueDeck(GameState state) { while (state.Shared.IntrigueDeck.Count > 0) state.Shared.IntrigueDeck.TryDrawTo(state.Shared.IntrigueDiscard); }
    private static int Troops(PlayerState player) => player.Units.TroopsInSupply + player.Units.TroopsInGarrison + player.Units.TroopsInConflict;
    private static int RewardAmount(Faction faction) => faction switch { Faction.Emperor => 2, Faction.SpacingGuild => 3, _ => 1 };
    private static int RewardCount(GameState state, Faction faction) => faction switch
    {
        Faction.Emperor => state.Players[0].Units.TroopsInGarrison,
        Faction.SpacingGuild => state.Players[0].Resources.Solari,
        Faction.BeneGesserit => state.Players[0].Intrigue.Count,
        _ => state.Players[0].Resources.Water
    };
    private static void Resolve<T>(TurnEngine engine) where T : Effect
    {
        var owner = engine.DecisionOwnerId!.Value;
        Apply(engine, new ResolveEffectAction(owner, engine.Observe(owner).PendingEffects.Single(x => x.Kind == typeof(T).Name).Id));
    }
    private static void RejectUnchanged(TurnEngine engine, GameAction action, string code)
    {
        var before = engine.CaptureTrustedSnapshot().Hash;
        var result = engine.Step(action);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }
    private static void CheckReplay(TurnEngine engine, TurnRuleSet rules)
    {
        var result = TurnEngine.Replay(engine.CaptureReplay(), () =>
        {
            var state = Initial();
            SetTrack(state, 1, Faction.BeneGesserit, 3);
            return state;
        }, rules);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics));
        Assert.Equal(engine.CaptureTrustedSnapshot().Hash, result.Engine!.CaptureTrustedSnapshot().Hash);
        foreach (var owner in Enumerable.Range(0, 3)) Assert.Equal(engine.LegalActions(owner), result.Engine.LegalActions(owner));
    }
}
