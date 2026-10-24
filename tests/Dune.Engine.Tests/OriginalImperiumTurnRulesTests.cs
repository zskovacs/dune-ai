using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Tests;

public sealed class OriginalImperiumTurnRulesTests
{
    private static readonly ContentId Dune = new("imperium:starter:dune-desert-planet:v1");
    private static readonly ContentId Signet = new("imperium:starter:signet-ring:v1");
    private static readonly ContentId Liaison = new("imperium:reserve:arrakis-liaison:v1");
    private static readonly ContentId Flow = new("imperium:reserve:spice-must-flow:v1");
    private static readonly ContentId Foldspace = new("imperium:reserve:foldspace:v1");
    private static readonly ContentId FremenCamp = new("imperium:imperium-card:fremen-camp:v1");
    private static readonly ContentId Sardaukar = new("imperium:imperium-card:sardaukar-legion:v1");
    private static readonly ContentId FixtureBlankAccess = new("fixture:imperiumcard:0:2:v1");

    [Fact]
    public void FremenCampCanDeclineItsOptionalSpicePayment()
    {
        // I p.9: Fremen Camp may pay 2 spice to recruit 3 troops.
        var (state, engine, owner) = Create(FremenCamp);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Spice = 2 };

        Apply(engine, new PlaceAgentAction(owner, new(FremenCamp), Space("secure-contract")));
        Assert.Equal(2, player.Resources.Spice);
        var optional = Assert.Single(engine.Observe(owner).PendingEffects,
            x => x.Kind == nameof(OptionalPaymentEffect));
        Apply(engine, new ResolveEffectAction(owner, optional.Id));
        Assert.Equal(owner, engine.DecisionOwnerId);
        Apply(engine, new ChooseOptionAction(owner, 0));

        Assert.Equal(2, player.Resources.Spice);
        Assert.Equal(3, player.Units.TroopsInGarrison);
        Assert.Equal(3, player.Resources.Solari);
    }

    [Fact]
    public void FremenCampPaymentPrecedesItsRecruitment()
    {
        // I p.9: the optional 2-spice payment precedes the 3-troop reward.
        var (state, engine, owner) = Create(FremenCamp);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Spice = 2 };

        Apply(engine, new PlaceAgentAction(owner, new(FremenCamp), Space("secure-contract")));
        var optional = Assert.Single(engine.Observe(owner).PendingEffects,
            x => x.Kind == nameof(OptionalPaymentEffect));
        Apply(engine, new ResolveEffectAction(owner, optional.Id));
        Assert.Equal(3, player.Units.TroopsInGarrison);
        Assert.Equal(2, player.Resources.Spice);
        Apply(engine, new ChooseOptionAction(owner, 1));
        Assert.Equal(0, player.Resources.Spice);
        Assert.Equal(6, player.Units.TroopsInGarrison);

        ResolveAll(owner, engine);
        Assert.Equal(6, player.Units.TroopsInGarrison);
        Assert.Equal(3, player.Resources.Solari);
    }

    [Fact]
    public void FremenCampCannotPayFromAPlacementRewardThatHasNotResolved()
    {
        // I p.9 allows effect order choice. Here the player chooses the optional payment before
        // Imperial Basin's spice reward (A p.2), so that reward cannot fund the payment yet.
        var (state, engine, owner) = Create(FremenCamp);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Spice = 1 };

        Apply(engine, new PlaceAgentAction(owner, new(FremenCamp), Space("imperial-basin")));
        var optional = Assert.Single(engine.Observe(owner).PendingEffects,
            x => x.Kind == nameof(OptionalPaymentEffect));
        Apply(engine, new ResolveEffectAction(owner, optional.Id));
        Assert.DoesNotContain(new ChooseOptionAction(owner, 1), engine.LegalActions(owner));
        Assert.Equal(StepStatus.Illegal, engine.Step(new ChooseOptionAction(owner, 1)).Status);
        Assert.Equal(1, player.Resources.Spice);
        Apply(engine, new ChooseOptionAction(owner, 0));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    [InlineData(5, 12)]
    public void SellMelangeUsesTheChosenExchange(int spice, int solari)
    {
        // A p.2: Sell Melange exchanges exactly 2/3/4/5 spice for 6/8/10/12 Solari.
        var (state, engine, owner) = Create(Dune);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Spice = spice };

        Apply(engine, new PlaceAgentAction(owner, new(Dune), Space("sell-melange"), spice - 2));
        Assert.Equal(0, player.Resources.Spice);
        Assert.Equal(solari, player.Resources.Solari);
    }

    [Fact]
    public void SellMelangeRejectsUnaffordableExchangeWithoutMutation()
    {
        // A p.2: the selected exchange must be paid before its reward.
        var (state, engine, owner) = Create(Dune);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Spice = 1 };
        var before = engine.CaptureTrustedSnapshot().Hash;

        var result = engine.Step(new PlaceAgentAction(owner, new(Dune), Space("sell-melange")));

        Assert.Equal(StepStatus.Illegal, result.Status);
        Assert.Equal(before, engine.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void HardyWarriorsPaysWaterAndCanDeployRecruitsPlusTwoOldTroops()
    {
        // A p.2: Hardy Warriors costs 1 water, raises Fremen influence, and recruits 2 troops.
        // I p.10: a combat space may deploy recruited troops plus up to 2 already in garrison.
        var (state, engine, owner) = Create(FixtureBlankAccess);
        var player = state.Players[owner];
        Assert.Equal(1, player.Resources.Water);

        Apply(engine, new PlaceAgentAction(owner, new(FixtureBlankAccess), Space("hardy-warriors")));
        ResolveAll(owner, engine);

        Assert.Equal(0, player.Resources.Water);
        Assert.Equal(1, player.Influence.Single(x => x.Faction == Faction.Fremen).Amount);
        Assert.Equal(5, player.Units.TroopsInGarrison);
        Assert.Contains(new DeployAction(owner, 4), engine.LegalActions(owner));
        Assert.DoesNotContain(new DeployAction(owner, 5), engine.LegalActions(owner));
        Assert.Equal(StepStatus.Illegal, engine.Step(new DeployAction(owner, 5)).Status);
        Apply(engine, new DeployAction(owner, 4));
        Assert.Equal(1, player.Units.TroopsInGarrison);
        Assert.Equal(4, player.Units.TroopsInConflict);
    }

    [Fact]
    public void ImperialBasinHarvestsBonusAndPaysItsController()
    {
        // A p.2: Imperial Basin gains 1 spice plus accumulated bonus; its controller gains 1 spice.
        var (state, engine, owner) = Create(Dune);
        var board = state.Board.Spaces.Single(x => x.Definition.Id == Space("imperial-basin"));
        var controller = state.Players.First(x => x.Id != owner);
        board.BonusSpice = 2;
        board.ControllerId = controller.Id;
        var controllerSpice = controller.Resources.Spice;

        Apply(engine, new PlaceAgentAction(owner, new(Dune), Space("imperial-basin")));
        ResolveAll(owner, engine);

        Assert.Equal(3, state.Players[owner].Resources.Spice);
        Assert.Equal(0, board.BonusSpice);
        Assert.Equal(controllerSpice + 1, controller.Resources.Spice);
    }

    [Fact]
    public void ArrakeenRecruitsOneTroopAndDrawsOneCard()
    {
        // A p.2: Arrakeen recruits 1 troop and draws 1 card.
        // I p.3: Liaison's Agent box is blank and its City access is visible.
        var (state, engine, owner) = Create(Liaison);
        var player = state.Players[owner];
        var deckCount = player.Deck.Count;

        Apply(engine, new PlaceAgentAction(owner, new(Liaison), Space("arrakeen")));
        ResolveAll(owner, engine);

        Assert.Equal(4, player.Units.TroopsInGarrison);
        Assert.Single(player.Hand.Cards);
        Assert.Equal(deckCount - 1, player.Deck.Count);
    }

    [Theory]
    [InlineData("stillsuits", 0, 0, 0, 0, 1, 0, 0, 1, true)]
    [InlineData("rally-troops", 4, 0, 0, 0, 0, 4, 0, 0, false)]
    [InlineData("research-station", 0, 2, 0, 0, 0, 0, 3, 0, true)]
    [InlineData("hagga-basin", 0, 1, 2, 0, 0, 0, 0, 0, true)]
    [InlineData("great-flat", 0, 2, 3, 0, 0, 0, 0, 0, true)]
    [InlineData("secure-contract", 0, 0, 0, 3, 0, 0, 0, 0, false)]
    public void BoardSpaceGuideAppliesMandatoryCostsRewardsAndPlacement(
        string space, int solariCost, int waterCost, int spiceGain, int solariGain,
        int waterGain, int recruits, int draws, int fremenInfluence, bool combat)
    {
        // A p.2: exact original Imperium board-space costs, rewards, and combat icons.
        var (state, engine, owner) = Create(FixtureBlankAccess);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Solari = 4, Water = 2 };
        var deckCount = player.Deck.Count;
        var target = Space(space);

        Apply(engine, new PlaceAgentAction(owner, new(FixtureBlankAccess), target));
        ResolveAll(owner, engine);

        Assert.Equal(4 - solariCost + solariGain, player.Resources.Solari);
        Assert.Equal(2 - waterCost + waterGain, player.Resources.Water);
        Assert.Equal(spiceGain, player.Resources.Spice);
        Assert.Equal(3 + recruits, player.Units.TroopsInGarrison);
        Assert.Equal(draws, player.Hand.Count);
        Assert.Equal(deckCount - draws, player.Deck.Count);
        Assert.Equal(fremenInfluence, player.Influence.Single(x => x.Faction == Faction.Fremen).Amount);
        Assert.Equal(1, player.Units.AvailableAgents);
        Assert.Contains(player.InPlay.Cards, x => x.Definition == FixtureBlankAccess);
        var placement = Assert.Single(state.Board.Spaces.Single(x => x.Definition.Id == target).Occupants);
        Assert.Equal(owner, placement.PlayerId);
        Assert.Equal(combat, engine.LegalActions(owner).Any(x => x is DeployAction));
    }

    [Fact]
    public void HallOfOratoryAddsOnePersuasionOnReveal()
    {
        // A p.2: Hall of Oratory recruits one troop and grants one Persuasion during Reveal.
        // I p.3: Arrakis Liaison has a blank Agent box and reveals two Persuasion.
        var (state, engine, owner) = Create(Liaison, Liaison);
        var player = state.Players[owner];

        Apply(engine, new PlaceAgentAction(owner, new(Liaison), Space("hall-of-oratory")));
        Assert.Equal(4, player.Units.TroopsInGarrison);
        AdvanceTo(owner, engine);
        Apply(engine, new RevealAction(owner));
        ResolveAll(owner, engine);

        Assert.Equal(3, player.Temporary.Persuasion);
    }

    [Fact]
    public void SietchTabrRequiresTwoFremenInfluenceAndDoesNotGrantInfluence()
    {
        // A p.2: Sietch Tabr requires two Fremen influence, then recruits one and grants one water.
        var (state, engine, owner) = Create(Liaison);
        var player = state.Players[owner];
        var action = new PlaceAgentAction(owner, new(Liaison), Space("sietch-tabr"));
        Assert.Equal(StepStatus.Illegal, engine.Step(action).Status);

        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Fremen ? x with { Amount = 2 } : x)];
        Apply(engine, action);
        ResolveAll(owner, engine);
        Assert.Equal(2, player.Influence.Single(x => x.Faction == Faction.Fremen).Amount);
        Assert.Equal(2, player.Resources.Water);
        Assert.Equal(4, player.Units.TroopsInGarrison);
    }

    [Fact]
    public void HighCouncilCostsFiveSolariAndAddsTwoPersuasionOnReveal()
    {
        // A p.2: High Council costs 5 Solari, is once per game, and grants +2 Reveal Persuasion.
        var (state, engine, owner) = Create(Liaison, Liaison);
        var player = state.Players[owner];
        player.Resources = player.Resources with { Solari = 5 };

        Apply(engine, new PlaceAgentAction(owner, new(Liaison), Space("high-council")));
        Assert.Equal(0, player.Resources.Solari);
        Assert.True(player.HasHighCouncilSeat);
        AdvanceTo(owner, engine);
        Apply(engine, new RevealAction(owner));
        ResolveAll(owner, engine);

        Assert.Equal(4, player.Temporary.Persuasion);
    }

    [Fact]
    public void CrossingTwoInfluenceGrantsOneVictoryPoint()
    {
        // I p.7: reaching two influence with a faction grants one VP; A p.2: Wealth raises Emperor influence.
        var (state, engine, owner) = Create(Sardaukar);
        var player = state.Players[owner];
        player.Influence = [.. player.Influence.Select(x => x.Faction == Faction.Emperor ? x with { Amount = 1 } : x)];
        var before = player.Resources.VictoryPoints;

        Apply(engine, new PlaceAgentAction(owner, new(Sardaukar), Space("wealth")));
        ResolveAll(owner, engine);

        Assert.Equal(2, player.Influence.Single(x => x.Faction == Faction.Emperor).Amount);
        Assert.Equal(before + 1, player.Resources.VictoryPoints);
        Assert.Equal(2, player.Resources.Solari);
        Assert.Equal(5, player.Units.TroopsInGarrison);
    }

    [Fact]
    public void NamedImperiumCardsRevealTheirSourcedPersuasionAndSwords()
    {
        // I p.9: Fremen Camp reveals 2 Persuasion and 1 sword; Sardaukar Legion reveals 1 and 2.
        var (state, engine, owner) = Create(FremenCamp, Sardaukar);
        var player = state.Players[owner];

        Apply(engine, new RevealAction(owner));
        ResolveAll(owner, engine);

        Assert.Equal(3, player.Temporary.Persuasion);
        Assert.Equal(3, player.Temporary.Swords);
    }

    [Fact]
    public void SignetRingRevealsOnePersuasion()
    {
        // I p.3: Signet Ring's Reveal box grants 1 Persuasion; no leader ability is invoked.
        var (state, engine, owner) = Create(Signet);

        Apply(engine, new RevealAction(owner));
        ResolveAll(owner, engine);

        Assert.Equal(1, state.Players[owner].Temporary.Persuasion);
    }

    [Fact]
    public void RevealCanBuyMultipleLiaisonsAndFlowButNotFoldspace()
    {
        // I p.3: Liaison costs 2 and Flow costs 9 with acquire 1 VP; I p.11: multiple buys, no Foldspace purchase.
        var (state, engine, owner) = Create();
        var player = state.Players[owner];
        Apply(engine, new RevealAction(owner));
        player.Temporary = player.Temporary with { Persuasion = 13 };
        var before = player.Resources.VictoryPoints;

        var liaisonZone = state.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition == Liaison));
        var flowZone = state.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition == Flow));
        var foldspaceZone = state.Shared.Reserves.Single(x => x.Cards.Any(c => c.Definition == Foldspace));
        Assert.DoesNotContain(new AcquireCardAction(owner, new(Foldspace), foldspaceZone.Id),
            engine.LegalActions(owner));
        Apply(engine, new AcquireCardAction(owner, new(Liaison), liaisonZone.Id));
        Apply(engine, new AcquireCardAction(owner, new(Liaison), liaisonZone.Id));
        Apply(engine, new AcquireCardAction(owner, new(Flow), flowZone.Id));
        Assert.Equal(before + 1, player.Resources.VictoryPoints); // Acquire icon resolves immediately (A p.1).
        ResolveAll(owner, engine);

        Assert.Equal(0, player.Temporary.Persuasion);
        Assert.Equal(before + 1, player.Resources.VictoryPoints);
        Assert.Equal(2, player.Discard.Cards.Count(x => x.Definition == Liaison));
        Assert.Single(player.Discard.Cards, x => x.Definition == Flow);
        Assert.Equal(StepStatus.Illegal,
            engine.Step(new AcquireCardAction(owner, new(Foldspace), foldspaceZone.Id)).Status);
    }

    [Fact]
    public void UnknownStarterAndMissingLeaderContractRemainGated()
    {
        // I p.3: obscured starter effect boxes cannot be assigned invented behavior.
        var (state, engine, owner) = Create();
        var player = state.Players[owner];
        var source =
            player.Deck.Cards.First(x => x.Definition == new ContentId("imperium:starter:convincing-argument:v1"));
        player.Deck.MoveTo(source.Id, player.Hand);
        Assert.Equal(StepStatus.Unsupported, engine.Step(new RevealAction(owner)).Status);

        var unsupportedRules = OriginalImperiumTurnRules.Create();
        var missingLeaderEngine = new TurnEngine(state, unsupportedRules);
        Assert.Equal(StepStatus.Unsupported, missingLeaderEngine.Step(new RevealAction(owner)).Status);
    }

    private static (GameState State, TurnEngine Engine, int Owner) Create(params ContentId[] hand)
    {
        // Two synthetic Imperium slots become one-copy named cards for these tests only; all other
        // incomplete cards and the synthetic leaders retain their explicit unsupported status.
        var definitions = OriginalImperiumFixture.Catalog.Definitions.ToList();
        var slots = definitions.Where(x => x.Category == ContentCategory.ImperiumCard).Take(2).ToArray();
        definitions.RemoveAll(x => slots.Contains(x));
        foreach (var (id, name) in new[] { (FremenCamp, "Fremen Camp"), (Sardaukar, "Sardaukar Legion") })
            definitions.Add(slots[0] with { Id = id, Name = name });
        var composer = new RulesetComposer(new ContentCatalog(definitions, CatalogCompleteness.Representative));
        var initializer = new GameInitializer(composer);
        var configuration = OriginalImperiumFixture.Configuration();
        var definition = composer.Compose(configuration).Definition!;
        var result = initializer.InitializeFixture(configuration, definition);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        var state = result.State!;
        var rules = OriginalImperiumTurnRules.Create() with
        {
            Leaders =
            [
                .. state.Players.Select(x => x.Leader).Distinct()
                    .Select(x => new LeaderRule(x, true, [], Synthetic: true))
            ],
        };
        // This blank card and its access icons exist only in the synthetic test inventory.
        // No obscured official starter Agent or Reveal box is inferred from board examples.
        Assert.Contains(definitions, x => x.Id == FixtureBlankAccess);
        rules = rules with
        {
            Cards =
            [
                .. rules.Cards, new CardRule(FixtureBlankAccess,
                        [AgentIcon.Fremen, AgentIcon.Landsraad, AgentIcon.City, AgentIcon.SpiceTrade], [], [])
                    { Purchasable = false }
            ],
        };
        var engine = new TurnEngine(state, rules);
        Apply(engine, new StartRoundAction());
        var owner = state.FirstPlayerId;
        var player = state.Players[owner];
        foreach (var participant in state.Players)
        {
            foreach (var card in participant.Hand.Cards.ToArray())
                participant.Hand.MoveTo(card.Id, participant.Deck);
        }

        foreach (var id in hand)
        {
            var source = state.Zones.First(x => x != player.Hand && x.Cards.Any(c => c.Definition == id));
            var card = source.Cards.First(x => x.Definition == id);
            source.MoveTo(card.Id, player.Hand);
        }

        return (state, engine, owner);
    }

    private static BoardSpaceId Space(string name) => new($"imperium:space:{name}:v1");

    private static void Apply(TurnEngine engine, GameAction action)
    {
        var result = engine.Step(action);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
    }

    private static void ResolveAll(int owner, TurnEngine engine)
    {
        while (engine.Observe(owner).PendingEffects is { Length: > 0 } effects)
            Apply(engine, new ResolveEffectAction(owner, effects[0].Id));
    }

    private static void AdvanceTo(int owner, TurnEngine engine)
    {
        while (engine.CurrentPlayerId != owner)
        {
            var current = engine.CurrentPlayerId!.Value;
            Apply(engine, new RevealAction(current));
            ResolveAll(current, engine);
            Apply(engine, new EndRevealAction(current));
        }
    }
}
