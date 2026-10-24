using System.Collections.Immutable;

namespace Dune.Engine.Configuration;

public static class ConfigurationPresets
{
    public static ImmutableDictionary<string, GameConfiguration> All { get; } =
        new Dictionary<string, GameConfiguration>(StringComparer.Ordinal)
        {
            ["imperium-standard"] = new(),
            ["imperium-ix"] = new() { Expansions = [Expansion.RiseOfIx] },
            ["imperium-ix-immortality"] = new() { Expansions = [Expansion.RiseOfIx, Expansion.Immortality] },
            ["imperium-ix-immortality-epic"] = new() { Expansions = [Expansion.RiseOfIx, Expansion.Immortality], Mode = GameMode.Epic },
            ["imperium-ix-epic"] = new() { Expansions = [Expansion.RiseOfIx], Mode = GameMode.Epic },
            ["imperium-ix-immortality-go-to-11"] = new() { Expansions = [Expansion.RiseOfIx, Expansion.Immortality], Variants = [RuleVariant.GoTo11] },
            ["uprising-standard"] = new() { BaseGame = BaseGame.Uprising },
            ["uprising-ix"] = new() { BaseGame = BaseGame.Uprising, Expansions = [Expansion.RiseOfIx] },
            ["uprising-bloodlines"] = new() { BaseGame = BaseGame.Uprising, Expansions = [Expansion.Bloodlines] },
            ["uprising-bloodlines-tech"] = new() { BaseGame = BaseGame.Uprising, Expansions = [Expansion.Bloodlines], Modules = [OptionalModule.BloodlinesTech] },
            ["uprising-choam"] = new() { BaseGame = BaseGame.Uprising, Modules = [OptionalModule.UprisingChoam] },
        }.ToImmutableDictionary(StringComparer.Ordinal);
}
