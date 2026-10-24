using System.Collections.Immutable;
using Dune.Engine.Configuration;
using Dune.Engine.Content;

namespace Dune.Engine.Rulesets;

/// <summary>Explicit setup-time registration; values are sourced recipes, not executable game effects.</summary>
internal static class BuiltinModules
{
    private const string I = "DUNE_IMPERIUM_Rules_2020_10_26.pdf";
    private const string U = "DUNE_IMPERIUM_UPRISING_Main_Rulebook_23-10-12.pdf";
    private const string X = "DUNE_IMPERIUM_RISE_OF_IX_Rulebook_22-2-11.pdf";
    private const string M = "DUNE_IMPERIUM_IMMORTALITY_Rulebook.pdf";
    private const string B = "DUNE_IMPERIUM_BLOODLINES_Rulebook.pdf";

    public static ImmutableArray<RuleModule> Select(GameConfiguration c)
    {
        var modules = ImmutableArray.CreateBuilder<RuleModule>();
        bool uprising = c.BaseGame == BaseGame.Uprising;
        string game = uprising ? "uprising" : "imperium";
        string source = uprising ? U : I;
        string baseId = "base." + game;
        var baseRules = ImmutableArray.CreateBuilder<RuleContribution>();
        baseRules.Add(Add(game + ".board.standard", DefinitionKind.BoardLayout, "main", "standard-front-board", source, 4, "Setup A"));
        baseRules.Add(Add(game + ".deck.standard", DefinitionKind.StartingDeck, "player", "10 cards: Convincing Argument x2; Dagger x2; Diplomacy; Dune, the Desert Planet x2; Reconnaissance; Seek Allies; Signet Ring", source, 3, "Starting deck components"));
        baseRules.Add(Add(game + ".resources.standard", DefinitionKind.Resources, "initial", $"water:1;garrison:3;intrigue:0;vp:{(c.Players.Count == 4 ? 1 : 0)}", source, 5, "Setup F/G"));
        baseRules.Add(Add(game + ".conflicts.standard", DefinitionKind.ConflictDeck, "recipe", "I:1;II:5;III:4", source, 4, "Setup B"));
        baseRules.Add(Add(game + ".victory.standard", DefinitionKind.VictoryCondition, "threshold", "10", source, 6, "Objective"));
        baseRules.Add(Add(game + ".research.standard", DefinitionKind.BoardSpace, "research-station", game + "-research-station", source, 4, "Game board"));
        baseRules.Add(Add(game + ".pool.standard", DefinitionKind.CardPool, "base", uprising ? "Uprising; omit CHOAM-marked cards unless CHOAM module enabled" : "original Imperium", source, 4, "Setup C"));
        baseRules.Add(Add(game + ".leaders.standard", DefinitionKind.LeaderPool, "base", "representative eligible metadata only; choose or select at random", source, 4, "Leader setup"));
        if (uprising)
        {
            baseRules.Add(Add("uprising.spies", DefinitionKind.Mechanic, "spies", "spies and observation posts", U, 11, "Spies"));
            baseRules.Add(Add("uprising.sandworms", DefinitionKind.Mechanic, "sandworms", "sandworms, maker hooks and shield wall", U, 10, "Sandworms"));
            baseRules.Add(Add("uprising.objectives", DefinitionKind.Setup, "first-player", "player-count-filtered public Objectives determine first player", U, 5, "Setup H"));
        }
        else baseRules.Add(Add("imperium.first-player", DefinitionKind.Setup, "first-player", "determine first player randomly", I, 5, "Setup H"));
        modules.Add(new(new(baseId), 1, [], [new(uprising ? "base.imperium" : "base.uprising")], baseRules.ToImmutable()));

        if (c.Expansions.Contains(Expansion.RiseOfIx))
        {
            var rules = ImmutableArray.CreateBuilder<RuleContribution>();
            rules.Add(Replace(game + ".board.ix", DefinitionKind.BoardLayout, "main",
                uprising ? "Ix folded overlay covers Assembly Hall and Gather Support only; remaining Landsraad unchanged" : "Ix overlay replaces upper-right Landsraad and CHOAM section",
                uprising ? U : X, uprising ? 18 : 3, "Rise of Ix setup", game + ".board.standard"));
            rules.Add(Add("ix.shipping", DefinitionKind.Mechanic, "shipping", "shipping track", X, 5, "Shipping track"));
            rules.Add(Add("ix.tech", DefinitionKind.Mechanic, "tech", "Ix tech negotiation and acquisition", X, 4, "Tech tiles"));
            rules.Add(Add("ix.dreadnoughts", DefinitionKind.Mechanic, "dreadnoughts", "dreadnoughts", X, 6, "Dreadnoughts"));
            rules.Add(Add("ix.pool", DefinitionKind.CardPool, "ix", uprising ? "Ix cards; recommended omit all Ix conflicts in Standard; Ilesa requires original Foldspace exclusively" : "Ix Imperium, Intrigue, leader and Conflict additions", uprising ? U : X, uprising ? 18 : 3, "Ix compatibility/setup"));
            if (uprising) rules.Add(Add("uprising.ix.posts", DefinitionKind.BoardSpace, "virtual-observation-posts", "Interstellar Shipping/Smuggling; Tech Negotiation/Dreadnought", U, 18, "Rise of Ix observation posts"));
            modules.Add(Module("expansion.ix", [baseId], rules.ToImmutable()));
        }
        if (c.Expansions.Contains(Expansion.Immortality))
        {
            modules.Add(Module("expansion.immortality", [baseId], [
                Replace("immortality.deck.standard", DefinitionKind.StartingDeck, "player", "10 starting cards; replace BOTH Dune, the Desert Planet with Experimentation x2", M, 5, "Setup", game + ".deck.standard"),
                Replace("immortality.research", DefinitionKind.BoardSpace, "research-station", "Immortality Research Station overlay", M, 5, "Setup", game + ".research.standard"),
                Add("immortality.research-tracks", DefinitionKind.Mechanic, "research", "research and Tleilaxu tracks, specimens and graft", M, 6, "Research tracks", 7, 8, 10),
                Add("immortality.family-atomics", DefinitionKind.Setup, "family-atomics", "each player receives a Family Atomics token", M, 12, "Family Atomics"),
                Add("immortality.pool", DefinitionKind.CardPool, "immortality", "Immortality Imperium/Intrigue additions and separate Tleilaxu deck", M, 4, "Setup", 5),
            ]));
        }
        if (c.Mode == GameMode.Epic)
        {
            modules.Add(Module("mode.epic", [baseId, "expansion.ix"], [
                Replace("epic.victory", DefinitionKind.VictoryCondition, "threshold", "12", X, 10, "Epic Game Mode", game + ".victory.standard"),
                Replace("epic.conflicts", DefinitionKind.ConflictDeck, "recipe", "I:0;II:5;III:5", X, 10, "Epic Game Mode", game + ".conflicts.standard"),
                Replace("epic.resources", DefinitionKind.Resources, "initial", $"water:1;garrison:5;intrigue:1;vp:{(c.Players.Count == 4 ? 1 : 0)}", X, 10, "Epic Game Mode", game + ".resources.standard"),
            ]));
            bool imm = c.Expansions.Contains(Expansion.Immortality);
            modules.Add(Module(imm ? "interaction.immortality-epic" : "setup.epic-deck",
                imm ? ["mode.epic", "expansion.immortality"] : ["mode.epic"], [
                    Replace(imm ? "immortality.deck.epic" : "ix.deck.epic", DefinitionKind.StartingDeck, "player",
                        imm ? "retain all 10 Immortality starting cards; Control the Spice x1 starts in discard (11 total)" : "10 starting cards; replace one Dune, the Desert Planet with Control the Spice",
                        imm ? M : X, imm ? 12 : 10, "Epic starting deck", imm ? "immortality.deck.standard" : game + ".deck.standard"),
                ]));
        }
        if (c.Variants.Contains(RuleVariant.GoTo11))
        {
            modules.Add(Module("variant.go-to-11", [baseId, "expansion.immortality"], [
                Replace("go-to-11.victory", DefinitionKind.VictoryCondition, "threshold", c.Players.Count == 4 ? "10" : "11", M, 12, "Go to 11", game + ".victory.standard"),
                Replace("go-to-11.resources", DefinitionKind.Resources, "initial", "water:1;garrison:3;intrigue:0;vp:0", M, 12, "Go to 11", game + ".resources.standard"),
            ]));
        }
        if (c.Expansions.Contains(Expansion.Bloodlines))
        {
            modules.Add(Module("expansion.bloodlines", [baseId], [
                Add("bloodlines.commanders", DefinitionKind.Mechanic, "sardaukar-commanders", "Sardaukar Commanders and skill stack", B, 3, "Setup"),
                Add("bloodlines.commander-setup", DefinitionKind.Setup, "commanders", c.Players.Count == 4 ? "five main board Commanders plus Assembly Hall; one in bank" : "five main board Commanders; one in bank; sixth returned", B, 3, "Setup step 1"),
                Add("bloodlines.pool.uprising", DefinitionKind.CardPool, "bloodlines", "Uprising Bloodlines additions; Tech Module cards excluded unless enabled; CHOAM additions excluded unless CHOAM enabled", B, 3, "Setup", 5, 6),
            ]));
        }
        if (c.Modules.Contains(OptionalModule.BloodlinesTech))
        {
            modules.Add(Module("module.bloodlines-tech", ["expansion.bloodlines"], [
                Add("bloodlines.tech.embassy", DefinitionKind.BoardLayout, "tech", "Ixian Embassy board; three evenly divided tile stacks", B, 6, "Tech Module setup"),
                Add("bloodlines.tech.pool", DefinitionKind.CardPool, "bloodlines-tech", "Tech Module cards and tiles; exclude CHOAM Transports without CHOAM", B, 6, "Tech Module setup"),
                Add("bloodlines.tech.acquisition", DefinitionKind.Mechanic, "tech", "Tech Module acquisition", B, 7, "Acquiring Tech"),
            ]));
        }
        if (c.Modules.Contains(OptionalModule.UprisingChoam))
        {
            modules.Add(Module("module.uprising-choam", [baseId], [
                Add("uprising.choam.contracts", DefinitionKind.Mechanic, "contracts", "CHOAM contracts and marked cards; Shaddam eligible", U, 16, "CHOAM Module"),
            ]));
        }
        return modules.ToImmutable();
    }

    private static RuleModule Module(string id, string[] dependencies, ImmutableArray<RuleContribution> rules) =>
        new(new(id), 1, dependencies.Select(x => new ModuleId(x)).ToImmutableArray(), [], rules);
    private static RuleContribution Add(string id, DefinitionKind kind, string slot, string value, string doc, int page, string section, params int[] relatedPages) =>
        new(new(new(id), new(kind, slot), value, new[] { new SourceReference(doc, page, section) }
            .Concat(relatedPages.Select(p => new SourceReference(doc, p, "Related rules"))).ToImmutableArray()));
    private static RuleContribution Replace(string id, DefinitionKind kind, string slot, string value, string doc, int page, string section, string expected) =>
        Add(id, kind, slot, value, doc, page, section) with { Replaces = new(expected) };
}
