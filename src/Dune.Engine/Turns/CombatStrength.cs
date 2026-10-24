namespace Dune.Engine.Turns;

/// <summary>Resolved coefficients for the original troop/sword calculation.</summary>
public sealed record CombatStrengthRule(int TroopStrength = 2, int SwordStrength = 1,
    bool RequiresDeployedUnits = true);
/// <summary>A unit kind contributes its own coefficient; future unit kinds add contributions here.</summary>
public sealed record CombatUnitContribution(string Kind, int Count, int StrengthPerUnit);

public static class CombatStrength
{
    public static int Calculate(IEnumerable<CombatUnitContribution> units, int swords,
        int swordStrength = 1, bool requiresDeployedUnits = true)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentOutOfRangeException.ThrowIfNegative(swords);
        ArgumentOutOfRangeException.ThrowIfNegative(swordStrength);
        var count = 0;
        var strength = 0;
        foreach (var unit in units)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(unit.Count);
            ArgumentOutOfRangeException.ThrowIfNegative(unit.StrengthPerUnit);
            count = checked(count + unit.Count);
            strength = checked(strength + unit.Count * unit.StrengthPerUnit);
        }
        return requiresDeployedUnits && count == 0 ? 0 : checked(strength + swords * swordStrength);
    }
}
