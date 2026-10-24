using System.Collections.Immutable;
using Dune.Engine.Configuration;
using Dune.Engine.Content;

namespace Dune.Engine.Setup;

public readonly record struct BoardSpaceId(string Value);
public enum Faction { Emperor, SpacingGuild, BeneGesserit, Fremen }
public sealed record StartingCardRecipe(ContentId Definition, int CopiesPerPlayer);
public sealed record ReserveStackDefinition(ContentId Definition, int Quantity);
public sealed record BoardSpaceDefinition(BoardSpaceId Id, string Name, bool AccumulatesSpice, bool Controllable);
public sealed record BoardDefinition(ContentId Id, ImmutableArray<BoardSpaceDefinition> Spaces,
    ImmutableArray<SourceReference> Sources)
{
    public BoardSpaceId? InitialMentatSpace { get; init; }
}
public sealed record ConflictTierSetup(ConflictTier Tier, int Available, int Active);
public sealed record SharedCardSetup(int ImperiumCards, int IntrigueCards, int ImperiumRowSize, int Leaders);
public sealed record InitialPlayerSetup(int VictoryPoints, int Solari, int Spice, int Water,
    int AvailableAgents, int UnacquiredSwordmasters, int TroopsInSupply, int TroopsInGarrison,
    int ControlMarkers);

/// <summary>Immutable executable setup data. Board effects and card effects are separate future consumers.</summary>
public sealed record GameSetupDefinition(string Id, BoardDefinition Board,
    InitialPlayerSetup Player, ImmutableArray<StartingCardRecipe> StartingDeck,
    ImmutableArray<ReserveStackDefinition> Reserves, ImmutableArray<ConflictTierSetup> Conflicts,
    SharedCardSetup SharedCards, ImmutableArray<SourceReference> Sources)
{
    internal static GameSetupDefinition? Resolve(GameConfiguration c)
    {
        if (c.BaseGame != BaseGame.Imperium || c.Mode != GameMode.Standard ||
            c.Players.Format != PlayerFormat.Competitive || c.Players.Count is not (3 or 4) ||
            !c.Expansions.IsEmpty || !c.Modules.IsEmpty || !c.Variants.IsEmpty ||
            c.Leaders.Strategy is not (LeaderSelectionStrategy.Fixed or LeaderSelectionStrategy.Random))
            return null;
        const string rules = "DUNE_IMPERIUM_Rules_2020_10_26.pdf";
        var names = new[]
        {
            ("arrakeen", "Arrakeen"), ("carthag", "Carthag"), ("conspire", "Conspire"),
            ("foldspace", "Foldspace"), ("great-flat", "The Great Flat"), ("hagga-basin", "Hagga Basin"),
            ("hall-of-oratory", "Hall of Oratory"), ("hardy-warriors", "Hardy Warriors"),
            ("heighliner", "Heighliner"), ("high-council", "High Council"),
            ("imperial-basin", "Imperial Basin"), ("mentat", "Mentat"), ("rally-troops", "Rally Troops"),
            ("research-station", "Research Station"), ("secrets", "Secrets"),
            ("secure-contract", "Secure Contract"), ("selective-breeding", "Selective Breeding"),
            ("sell-melange", "Sell Melange"), ("sietch-tabr", "Sietch Tabr"),
            ("stillsuits", "Stillsuits"), ("swordmaster", "Swordmaster"), ("wealth", "Wealth"),
        };
        var board = new BoardDefinition(new("imperium:board:main:v1"), names.Select(x =>
            new BoardSpaceDefinition(new("imperium:space:" + x.Item1 + ":v1"), x.Item2,
                x.Item1 is "great-flat" or "hagga-basin" or "imperial-basin",
                x.Item1 is "arrakeen" or "carthag" or "imperial-basin")).ToImmutableArray(),
            [new(rules, 17, "Board Space Guide"), new(rules, 18, "Board Space Guide"),
                new("DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf", 2, "Board Space Guide")])
        {
            InitialMentatSpace = new("imperium:space:mentat:v1"),
        };
        return new("imperium.setup.standard.v1", board,
            new(c.Players.Count == 4 ? 1 : 0, 0, 0, 1, 2, 1, 9, 3, 3),
            [Starter("convincing-argument", 2), Starter("dagger", 2), Starter("diplomacy", 1),
                Starter("dune-desert-planet", 2), Starter("reconnaissance", 1), Starter("seek-allies", 1),
                Starter("signet-ring", 1)],
            [Reserve("arrakis-liaison", 8), Reserve("spice-must-flow", 10), Reserve("foldspace", 6)],
            [new(ConflictTier.I, 4, 1), new(ConflictTier.II, 10, 5), new(ConflictTier.III, 4, 4)],
            new(67, 40, 5, 8),
            [new(rules, 3, "Components and starting deck"), new(rules, 4, "Setup A–D"),
                new(rules, 5, "Setup E–H"), new(rules, 7, "Faction influence and private Intrigue"),
                new(rules, 8, "Round Start (not executed during setup)")]);
    }

    private static StartingCardRecipe Starter(string name, int copies) => new(new("imperium:starter:" + name + ":v1"), copies);
    private static ReserveStackDefinition Reserve(string name, int copies) => new(new("imperium:reserve:" + name + ":v1"), copies);
}
