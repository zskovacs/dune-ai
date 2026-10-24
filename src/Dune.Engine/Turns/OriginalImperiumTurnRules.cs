using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

/// <summary>
/// Verified, incomplete original Imperium (2020) turn definitions. This is never a complete
/// legal-action catalog or a playable/RL-ready ruleset. Missing card boxes stay unsupported.
/// </summary>
public static class OriginalImperiumTurnRules
{
    public const string Version = "imperium-2020-turn-preview-v12";
    private const string Rules = "DUNE_IMPERIUM_Rules_2020_10_26.pdf";
    private const string Aid = "DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf";

    public static TurnRuleSet Create() => new(Version, Cards(), Spaces(), []);

    /// <summary>Partial source authoring, not a complete inventory or a claim of lifecycle playability.</summary>
    public static LifecycleRuleSet CreateLifecycleRules() => new("imperium-2020-lifecycle-authoring-v9",
    [
        new(new("imperium:intrigue:ambush:v1"), IntrigueTiming.Combat, new(),
            [new GainEffect(ResourceKind.Swords, 4)], [new(Rules, 7, "Ambush complete face"), new(Rules, 12, "Combat Intrigue example")]),
        new(new("imperium:intrigue:master-tactician:v1"), IntrigueTiming.Combat, new(),
            [new ChoiceEffect([
                new("Gain 3 swords", new(), [new GainEffect(ResourceKind.Swords, 3)]),
                new("Retreat up to 3 own troops", new(), [new RetreatEffect(3)])])],
            [new(Rules, 12, "Master Tactician complete OR face: 3 swords or retreat up to 3 own troops"),
             new(Rules, 16, "Retreat returns Conflict troops to garrison"), new(Aid, 1, "Retreat icon")]),
        new(new("imperium:intrigue:corner-the-market:v1"), IntrigueTiming.Endgame, new(),
            [new ConditionalEffect(EndgameCondition.OwnAtLeastTwoSpiceMustFlow, [new GainEffect(ResourceKind.VictoryPoints, 1)]),
             new ConditionalEffect(EndgameCondition.OwnMoreSpiceMustFlowThanEachOpponent, [new GainEffect(ResourceKind.VictoryPoints, 1)])],
            [new(Rules, 7, "Corner the Market complete face: two independent VP conditions")]),
        new(new("imperium:intrigue:calculated-hire:v1"), IntrigueTiming.Plot, new(Spice: 1),
            [new UnsupportedEffect("Plot play is not implemented.")], [new(Rules, 7, "Calculated Hire pictured one-spice cost and Plot timing; no runtime support")]),
    ], Conflicts: [], Factions:
    [
        FactionDefinition(Faction.Emperor, [new RecruitEffect(2)]),
        FactionDefinition(Faction.SpacingGuild, [new GainEffect(ResourceKind.Solari, 3)]),
        FactionDefinition(Faction.BeneGesserit, [new DrawIntrigueEffect(1)]),
        FactionDefinition(Faction.Fremen, [new GainEffect(ResourceKind.Water, 1)]),
    ]);

    private static FactionRule FactionDefinition(Faction faction, ImmutableArray<Effect> reward) =>
        new(faction, 6, reward,
            [new(Rules, 4, "Faction threshold-four printed bonus icons"),
             new(Rules, 7, "Maximum 6: VISUAL INFERENCE from seven cells anchored at 0, 2 and 4; not a printed prose numeral"),
             new(Rules, 16, "Bonus icon meanings; threshold reward retains original effect roles")]);

    private static ImmutableArray<CardRule> Cards()
    {
        static ImmutableArray<Effect> Unknown(string box) =>
            [new UnsupportedEffect($"{box} is not legible in the supplied rulebook's fanned starter illustration; card-level evidence is required.")];
        static ContentId Starter(string name) => new($"imperium:starter:{name}:v1");
        static CardRule StarterRule(string name, ImmutableArray<AgentIcon> icons,
            ImmutableArray<Effect>? agent = null, ImmutableArray<Effect>? reveal = null) =>
            new(Starter(name), icons, agent ?? Unknown($"{name} Agent box"),
                reveal ?? Unknown($"{name} Reveal box"))
            { Purchasable = false, Sources = [new(Rules, 3, "Visible starter access icons only; obscured effect boxes explicitly gated")] };

        return
        [
            StarterRule("convincing-argument", []),
            StarterRule("dagger", [AgentIcon.Landsraad]),
            StarterRule("diplomacy", [AgentIcon.Emperor, AgentIcon.SpacingGuild, AgentIcon.BeneGesserit, AgentIcon.Fremen]),
            StarterRule("dune-desert-planet", [AgentIcon.SpiceTrade], []) with
            { Sources = [new(Rules, 3, "Visible Spice Trade icon"), new(Rules, 10, "John's Imperial Basin example: no additional Agent effect")] },
            StarterRule("reconnaissance", [AgentIcon.City]),
            StarterRule("seek-allies", [AgentIcon.Emperor, AgentIcon.SpacingGuild, AgentIcon.BeneGesserit, AgentIcon.Fremen]),
            StarterRule("signet-ring", [AgentIcon.Landsraad, AgentIcon.City, AgentIcon.SpiceTrade],
                [new SignetEffect()], [new GainEffect(ResourceKind.Persuasion, 1)]) with
            { Sources = [new(Rules, 3, "Visible complete Signet Ring face"), new(Aid, 1, "Signet Ring triggers leader ability")] },
            new(new("imperium:reserve:arrakis-liaison:v1"), [AgentIcon.Landsraad, AgentIcon.City], [],
                [new GainEffect(ResourceKind.Persuasion, 2)], 2, [])
            { Sources = [new(Rules, 3, "Complete Arrakis Liaison face: cost, access and effects")] },
            new(new("imperium:reserve:spice-must-flow:v1"), [], [],
                [new GainEffect(ResourceKind.Spice, 1)], 9, [new GainEffect(ResourceKind.VictoryPoints, 1)])
            { Sources = [new(Rules, 3, "Complete The Spice Must Flow face: cost, acquire VP and Reveal spice")] },
            new(new("imperium:reserve:foldspace:v1"), [], Unknown("Foldspace Agent box"), Unknown("Foldspace Reveal box"))
            { Purchasable = false, Sources = [new(Rules, 3, "Reserve component; not a Persuasion purchase (p.11)")] },
            new(new("imperium:imperium-card:fremen-camp:v1"), [AgentIcon.City, AgentIcon.SpiceTrade],
                [new OptionalPaymentEffect(new(Spice: 2), [new RecruitEffect(3)])],
                [new GainEffect(ResourceKind.Persuasion, 2), new GainEffect(ResourceKind.Swords, 1)], 4, [])
            { Sources = [new(Rules, 9, "Complete Fremen Camp face and optional payment example")] },
            new(new("imperium:imperium-card:sardaukar-legion:v1"), [AgentIcon.Emperor, AgentIcon.Landsraad],
                [new RecruitEffect(2)],
                [new GainEffect(ResourceKind.Persuasion, 1), new GainEffect(ResourceKind.Swords, 2)], 5, [])
            { Sources = [new(Rules, 9, "Complete Sardaukar Legion face and Agent icon example")] },
        ];
    }

    private static ImmutableArray<BoardRule> Spaces()
    {
        static BoardRule Space(string name, AgentIcon icon, ImmutableArray<Effect> effects,
            ResourceCost? cost = null, bool combat = false, Faction? faction = null,
            int influence = 0, bool once = false, ResourceKind? control = null) =>
            new(new($"imperium:space:{name}:v1"), icon, cost ?? new(), effects,
                combat, faction, influence, once, control)
            {
                Sources = name is "carthag" or "conspire" or "heighliner"
                ? [new(Rules, 17, name + ": complete original Board Space Guide"), new(Aid, 2, name + ": original Imperium Board Space Guide")]
                : [new(Aid, 2, name + ": original Imperium Board Space Guide")]
            };

        return
        [
            Space("wealth", AgentIcon.Emperor, [new InfluenceEffect(Faction.Emperor), new GainEffect(ResourceKind.Solari, 2)], faction: Faction.Emperor),
            Space("stillsuits", AgentIcon.Fremen, [new InfluenceEffect(Faction.Fremen), new GainEffect(ResourceKind.Water, 1)], combat: true, faction: Faction.Fremen),
            Space("secure-contract", AgentIcon.SpiceTrade, [new GainEffect(ResourceKind.Solari, 3)]),
            Space("hall-of-oratory", AgentIcon.Landsraad, [new RecruitEffect(1)]) with
            { RevealEffects = [new GainEffect(ResourceKind.Persuasion, 1)] },
            Space("imperial-basin", AgentIcon.SpiceTrade, [new GainEffect(ResourceKind.Spice, 1), new HarvestSpiceEffect()], combat: true, control: ResourceKind.Spice),
            Space("rally-troops", AgentIcon.Landsraad, [new RecruitEffect(4)], new(Solari: 4)),
            Space("hardy-warriors", AgentIcon.Fremen, [new InfluenceEffect(Faction.Fremen), new RecruitEffect(2)], new(Water: 1), combat: true, faction: Faction.Fremen),
            Space("carthag", AgentIcon.City, [new RecruitEffect(1), new DrawIntrigueEffect(1)], combat: true, control: ResourceKind.Solari),
            Space("conspire", AgentIcon.Emperor, [new InfluenceEffect(Faction.Emperor), new GainEffect(ResourceKind.Solari, 5), new RecruitEffect(2), new DrawIntrigueEffect(1)], new(Spice: 4), faction: Faction.Emperor),
            Space("heighliner", AgentIcon.SpacingGuild, [new InfluenceEffect(Faction.SpacingGuild), new RecruitEffect(5), new GainEffect(ResourceKind.Water, 2)], new(Spice: 6), combat: true, faction: Faction.SpacingGuild),
            Space("arrakeen", AgentIcon.City, [new RecruitEffect(1), new DrawEffect(1)], combat: true, control: ResourceKind.Solari),
            Space("sietch-tabr", AgentIcon.City, [new RecruitEffect(1), new GainEffect(ResourceKind.Water, 1)], combat: true, faction: Faction.Fremen, influence: 2),
            Space("mentat", AgentIcon.Landsraad, [new DrawEffect(1), new TakeMentatEffect(false)], new(Solari: 2)) with
            { Sources = [new(Rules, 18, "Mentat: draw regardless of token availability; take token if present"), new(Aid, 2, "Mentat")] },
            Space("swordmaster", AgentIcon.Landsraad, [new SwordmasterEffect()], new(Solari: 8), once: true) with
            { Sources = [new(Rules, 18, "Swordmaster: permanent third Agent, once per game"), new(Aid, 2, "Swordmaster")] },
            Space("high-council", AgentIcon.Landsraad, [new HighCouncilEffect()], new(Solari: 5), once: true),
            Space("research-station", AgentIcon.City, [new DrawEffect(3)], new(Water: 2), combat: true),
            Space("hagga-basin", AgentIcon.SpiceTrade, [new GainEffect(ResourceKind.Spice, 2), new HarvestSpiceEffect()], new(Water: 1), combat: true),
            Space("great-flat", AgentIcon.SpiceTrade, [new GainEffect(ResourceKind.Spice, 3), new HarvestSpiceEffect()], new(Water: 2), combat: true),
            Space("sell-melange", AgentIcon.SpiceTrade, []) with
            {
                PlacementOptions = [
                    new("2 spice for 6 Solari", new(Spice: 2), [new GainEffect(ResourceKind.Solari, 6)]),
                    new("3 spice for 8 Solari", new(Spice: 3), [new GainEffect(ResourceKind.Solari, 8)]),
                    new("4 spice for 10 Solari", new(Spice: 4), [new GainEffect(ResourceKind.Solari, 10)]),
                    new("5 spice for 12 Solari", new(Spice: 5), [new GainEffect(ResourceKind.Solari, 12)]),
                ]
            },
        ];
    }
}
