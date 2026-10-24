using System.Collections.Immutable;
using Dune.Engine.Fixtures;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;
using static Dune.Engine.Fixtures.TurnEngineFixture;
using static Dune.Engine.Tests.ConflictRewardTests;

namespace Dune.Engine.Tests;

public sealed class BoardControlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void ControlAwardsReplaceControllerAndConserveEveryMarkerIncludingRepeatedSameOwner(int? previous)
    {
        GameState Create()
        {
            var result = CombatState([8, 6, 4]);
            result.Board.Spaces.Single(x => x.Definition.Id == Space("carthag")).ControllerId = previous;
            if (previous is { } old) result.Players[old].Units = result.Players[old].Units with { ControlMarkers = 2 };
            return result;
        }
        var state = Create();
        var space = Space("carthag");
        var board = state.Board.Spaces.Single(x => x.Definition.Id == space);
        var totals = state.Players.Select(x => x.Units.ControlMarkers + (x.Id == previous ? 1 : 0)).ToArray();
        var rules = Rewards(LifecycleFixture.Rules(state), 1, [new ControlSpaceEffect(space), new ControlSpaceEffect(space)]);
        var engine = new TurnEngine(state, rules);
        CloseCombat(engine);
        Resolve<ControlSpaceEffect>(engine);
        CheckReplay(engine, Create, rules);
        FinishRewards(engine);
        Assert.Equal(0, board.ControllerId);
        foreach (var player in state.Players)
            Assert.Equal(totals[player.Id], player.Units.ControlMarkers + (board.ControllerId == player.Id ? 1 : 0));
        Assert.Equal(2, state.Players[0].Units.ControlMarkers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingConflictDefenseSuspendsRoundStartBeforeDrawAndReplaysToPlayerTurns(bool deploy)
    {
        GameState Create()
        {
            var result = LifecycleFixture.CreateState();
            var board = result.Board.Spaces.Single(x => x.Definition.Id == Space("carthag"));
            board.ControllerId = 2;
            result.Players[2].Units = result.Players[2].Units with { ControlMarkers = 2 };
            return result;
        }
        var state = Create();
        var rules = WithControl(LifecycleFixture.Rules(state), Space("carthag"));
        var engine = new TurnEngine(state, rules);
        var supply = state.Players[2].Units.TroopsInSupply;
        var garrison = state.Players[2].Units.TroopsInGarrison;
        Apply(engine, new StartRoundAction());
        Assert.Equal(GamePhase.RoundStart, engine.Phase);
        Assert.Equal(2, engine.DecisionOwnerId);
        Assert.Null(engine.CurrentPlayerId);
        Assert.All(state.Players, x => Assert.Empty(x.Hand.Cards));
        Assert.Equal(ExecutionContinuationKind.RoundStart, engine.CaptureTrustedSnapshot().Data.Frames.Single().Continuation.Kind);
        Reject(engine, new RevealAction(2));
        Reject(engine, new DeployAction(2, 1));
        CheckReplay(engine, Create, rules);
        Apply(engine, new ChooseOptionAction(2, deploy ? 1 : 0));
        if (deploy)
        {
            Assert.Equal(GamePhase.RoundStart, engine.Phase);
            Assert.All(state.Players, x => Assert.Empty(x.Hand.Cards));
            CheckReplay(engine, Create, rules);
            Resolve<DeployFromSupplyEffect>(engine);
        }
        Assert.Equal(supply - (deploy ? 1 : 0), state.Players[2].Units.TroopsInSupply);
        Assert.Equal(deploy ? 1 : 0, state.Players[2].Units.TroopsInConflict);
        Assert.Equal(garrison, state.Players[2].Units.TroopsInGarrison);
        Assert.Equal(GamePhase.PlayerTurns, engine.Phase);
        Assert.Equal(state.FirstPlayerId, engine.CurrentPlayerId);
        Assert.All(state.Players, x => Assert.Equal(5, x.Hand.Count));
        Assert.Equal(2, state.Board.Spaces.Single(x => x.Definition.Id == Space("carthag")).ControllerId);
        CheckReplay(engine, Create, rules);
    }

    [Fact]
    public void EmptySupplyAllowsOnlyDeclineAndDifferentControlledSpaceGivesNoDefense()
    {
        var state = LifecycleFixture.CreateState();
        var player = state.Players[1];
        player.Units = player.Units with { TroopsInGarrison = player.Units.TroopsInGarrison + player.Units.TroopsInSupply, TroopsInSupply = 0 };
        state.Board.Spaces.Single(x => x.Definition.Id == Space("carthag")).ControllerId = 1;
        var engine = new TurnEngine(state, WithControl(LifecycleFixture.Rules(state), Space("carthag")));
        Apply(engine, new StartRoundAction());
        Assert.Equal(new GameAction[] { new ChooseOptionAction(1, 0) }, engine.LegalActions(1));
        Reject(engine, new ChooseOptionAction(1, 1));
        Apply(engine, new ChooseOptionAction(1, 0));
        Assert.Equal(GamePhase.PlayerTurns, engine.Phase);
        state = LifecycleFixture.CreateState();
        state.Board.Spaces.Single(x => x.Definition.Id == Space("arrakeen")).ControllerId = 1;
        engine = new TurnEngine(state, WithControl(LifecycleFixture.Rules(state), Space("carthag")));
        Apply(engine, new StartRoundAction());
        Assert.Equal(GamePhase.PlayerTurns, engine.Phase);
        Assert.Equal(0, state.Players[1].Units.TroopsInConflict);
    }

    [Theory]
    [InlineData("carthag", 0, 1, 0, 0, 1, true, null)]
    [InlineData("conspire", 4, 2, 5, 0, 1, false, Faction.Emperor)]
    [InlineData("heighliner", 6, 5, 0, 2, 0, true, Faction.SpacingGuild)]
    public void CompleteSourcedBoardSpacesGrantPrintedEffects(string name, int cost, int troops, int solari, int water,
        int intrigue, bool combat, Faction? faction)
    {
        var state = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(state) with { Spaces = OriginalImperiumTurnRules.Create().Spaces };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var player = state.Players[0];
        player.Resources = player.Resources with { Spice = cost };
        var oldWater = player.Resources.Water;
        var oldGarrison = player.Units.TroopsInGarrison;
        Apply(engine, new PlaceAgentAction(0, Card(player.Hand.Cards[0]), Space(name)));
        ResolveAll(engine);
        Assert.Equal(0, player.Resources.Spice);
        Assert.Equal(solari, player.Resources.Solari);
        Assert.Equal(oldWater + water, player.Resources.Water);
        Assert.Equal(oldGarrison + troops, player.Units.TroopsInGarrison);
        Assert.Equal(intrigue, player.Intrigue.Count);
        if (faction is { } track) Assert.Equal(1, player.Influence.Single(x => x.Faction == track).Amount);
        var definition = rules.Spaces.Single(x => x.Id == Space(name));
        Assert.Equal(combat, definition.Combat);
    }

    [Theory]
    [InlineData("arrakeen", 0, ResourceKind.Solari)]
    [InlineData("arrakeen", 1, ResourceKind.Solari)]
    [InlineData("carthag", 0, ResourceKind.Solari)]
    [InlineData("carthag", 1, ResourceKind.Solari)]
    [InlineData("imperial-basin", 0, ResourceKind.Spice)]
    [InlineData("imperial-basin", 1, ResourceKind.Spice)]
    public void AuthoritativePlacementBonusPaysControllerIncludingOwnAgent(string name, int controller, ResourceKind resource)
    {
        var state = LifecycleFixture.CreateState();
        var rules = LifecycleFixture.Rules(state) with { Spaces = OriginalImperiumTurnRules.Create().Spaces };
        state.Board.Spaces.Single(x => x.Definition.Id == Space(name)).ControllerId = controller;
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        Apply(engine, new PlaceAgentAction(0, Card(state.Players[0].Hand.Cards[0]), Space(name)));
        ResolveAll(engine);
        Assert.Equal(resource == ResourceKind.Solari ? 1 : controller == 0 ? 2 : 1,
            resource == ResourceKind.Solari ? state.Players[controller].Resources.Solari : state.Players[controller].Resources.Spice);
    }

    [Fact]
    public void SupplyDeploymentCannotEnableNormalCombatIntrigueAndUnavailableMentatRejectsFinalPass()
    {
        var state = CombatState([8, 6, 4]);
        var card = state.Shared.IntrigueDeck.TryDrawTo(state.Players[0].Intrigue)!;
        var rules = LifecycleFixture.Rules(state);
        rules = rules with
        {
            Lifecycle = rules.Lifecycle! with
            {
                Intrigues = [.. rules.Lifecycle.Intrigues.Select(x => x.Id == card.Definition
            ? x with { Effects = [new DeployFromSupplyEffect(1)] } : x)]
            }
        };
        var engine = new TurnEngine(state, rules);
        Reject(engine, new PlayIntrigueAction(0, Card(card)));
        state = CombatState([8, 6, 4]);
        state.Board.Mentat = new(null, 1);
        state.Players[1].Units = state.Players[1].Units with { AvailableAgents = 3, AvailableMentats = 1 };
        engine = new TurnEngine(state, Rewards(LifecycleFixture.Rules(state), 1, [new TakeMentatEffect(true)]));
        Apply(engine, new PassPriorityAction(0));
        Apply(engine, new PassPriorityAction(1));
        Reject(engine, new PassPriorityAction(2));
    }

    private static TurnRuleSet WithControl(TurnRuleSet rules, BoardSpaceId space) => rules with
    { Lifecycle = rules.Lifecycle! with { Conflicts = [.. rules.Lifecycle.Conflicts.Select(x => x with { ControlSpace = space })] } };
}
