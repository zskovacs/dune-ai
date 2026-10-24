using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;

namespace Dune.Engine.Configuration;

internal static class ConfigurationValidation
{
    public static ImmutableArray<ConfigurationError> Validate(GameConfiguration c)
    {
        var errors = ImmutableArray.CreateBuilder<ConfigurationError>();
        void Error(string code, string path, string message) => errors.Add(new(code, path, message));
        void EnumValue<T>(T value, string path) where T : struct, Enum
        {
            if (!Enum.IsDefined(value)) Error("unknown-value", path, $"Unknown {typeof(T).Name} value.");
        }
        void EnumSet<T>(ImmutableArray<T> values, string path) where T : struct, Enum
        {
            if (values.IsDefault) { Error("invalid-collection", path, "An initialized array is required."); return; }
            foreach (var value in values) EnumValue(value, path);
            if (values.Distinct().Count() != values.Length) Error("duplicate-value", path, "Set entries must be unique.");
        }
        if (c.SchemaVersion != 1) Error("schema-version", "schemaVersion", "Only schema version 1 is recognized.");
        EnumValue(c.BaseGame, "baseGame");
        EnumValue(c.Mode, "mode");
        EnumSet(c.Expansions, "expansions");
        EnumSet(c.Modules, "modules");
        EnumSet(c.Variants, "variants");
        if (c.Players is null) Error("required", "players", "Player configuration is required.");
        else
        {
            EnumValue(c.Players.Format, "players.format");
            if (c.Players.Count is < 1 or > 6) Error("player-count", "players.count", "Recognized player counts are 1 through 6.");
            var validCount = c.Players.Format switch
            {
                PlayerFormat.Competitive => c.Players.Count is 3 or 4,
                PlayerFormat.SoloWithRivals => c.Players.Count == 1,
                PlayerFormat.TwoPlayerWithRivals => c.Players.Count == 2,
                PlayerFormat.SixPlayerTeams => c.Players.Count == 6,
                _ => false,
            };
            if (!validCount) Error("player-format-count", "players", "The player count does not match the format.");
            if (c.Players.Format != PlayerFormat.Competitive)
                Error("unsupported-format", "players.format", "Rivals and team setup are recognized but composition is not implemented in SLICE-001.");
        }
        if (c.Leaders is null) Error("required", "leaders", "Leader selection is required.");
        else
        {
            var l = c.Leaders;
            EnumValue(l.Strategy, "leaders.strategy");
            if (l.PlayerOrder.IsDefault || l.CandidateLeaders.IsDefault || l.FixedAssignments.IsDefault || l.RestrictionIds.IsDefault)
                Error("invalid-collection", "leaders", "All leader selection arrays must be initialized.");
            else
            {
                if (c.Players is not null && (l.PlayerOrder.Length != c.Players.Count || !l.PlayerOrder.Order().SequenceEqual(Enumerable.Range(0, Math.Clamp(c.Players.Count, 0, 6)))))
                    Error("leader-order", "leaders.playerOrder", "Player order must list each zero-based seat exactly once.");
                if (l.CandidateLeaders.Any(x => !ContentCatalog.ValidId(x.Value)) || l.CandidateLeaders.Distinct().Count() != l.CandidateLeaders.Length)
                    Error("leader-candidates", "leaders.candidateLeaders", "Candidate IDs must be valid and unique.");
                if (!l.RestrictionIds.IsEmpty) Error("unsupported-restriction", "leaders.restrictionIds", "Ban/pick restrictions are reserved for a later slice.");
                if (l.Strategy != LeaderSelectionStrategy.Fixed && !l.FixedAssignments.IsEmpty)
                    Error("leader-assignment", "leaders.fixedAssignments", "Fixed assignments require the Fixed strategy.");
                if (l.Strategy == LeaderSelectionStrategy.Fixed && c.Players is not null)
                {
                    if (l.FixedAssignments.Any(x => x is null || !ContentCatalog.ValidId(x.Leader.Value)) ||
                        l.FixedAssignments.Length != c.Players.Count ||
                        !l.FixedAssignments.Where(x => x is not null).Select(x => x.PlayerId).Order().SequenceEqual(Enumerable.Range(0, Math.Clamp(c.Players.Count, 0, 6))) ||
                        l.FixedAssignments.Where(x => x is not null).Select(x => x.Leader).Distinct().Count() != l.FixedAssignments.Length)
                        Error("leader-assignment", "leaders.fixedAssignments", "Assign one distinct valid leader to each seat.");
                }
            }
        }
        if (c.Content is null) Error("required", "content", "Content profile is required.");
        else
        {
            if (!ContentCatalog.ValidId(c.Content.Id) || c.Content.Revision <= 0)
                Error("content-profile", "content", "A stable profile ID and positive revision are required.");
            if (c.Content.Included.IsDefault || c.Content.Excluded.IsDefault) Error("invalid-collection", "content", "Profile arrays must be initialized.");
            else if (c.Content.Included.Concat(c.Content.Excluded).Any(x => !ContentCatalog.ValidId(x.Value)) ||
                c.Content.Included.Distinct().Count() != c.Content.Included.Length ||
                c.Content.Excluded.Distinct().Count() != c.Content.Excluded.Length || c.Content.Included.Intersect(c.Content.Excluded).Any())
                Error("content-profile", "content", "Content IDs must be valid, unique and not both included and excluded.");
        }
        if (errors.Count != 0) return errors.ToImmutable();
        if (c.Modules.Contains(OptionalModule.BloodlinesTech) && !c.Expansions.Contains(Expansion.Bloodlines))
            Error("module-dependency", "modules", "Bloodlines Tech requires Bloodlines (Bloodlines PDF page 6).");
        if (c.Modules.Contains(OptionalModule.UprisingChoam) && c.BaseGame != BaseGame.Uprising)
            Error("module-incompatible", "modules", "The Uprising CHOAM module requires Uprising (Uprising PDF page 16).");
        if (c.Mode == GameMode.Epic && !c.Expansions.Contains(Expansion.RiseOfIx))
            Error("mode-dependency", "mode", "Epic is the Rise of Ix mode (Ix PDF page 10).");
        if (c.Mode == GameMode.Epic && c.Variants.Contains(RuleVariant.GoTo11))
            Error("unresolved-interaction", "variants", "Epic plus Go to 11 is unresolved; no official mutual exclusion is asserted.");
        var ix = c.Expansions.Contains(Expansion.RiseOfIx);
        var imm = c.Expansions.Contains(Expansion.Immortality);
        var blood = c.Expansions.Contains(Expansion.Bloodlines);
        bool supported = c.BaseGame switch
        {
            BaseGame.Imperium => !blood && c.Modules.IsEmpty && (!imm || ix) &&
                (c.Mode == GameMode.Standard || ix) &&
                (c.Variants.IsEmpty || (ix && imm && c.Mode == GameMode.Standard)),
            BaseGame.Uprising => c.Mode == GameMode.Standard && c.Variants.IsEmpty && !imm &&
                ((c.Expansions.IsEmpty && c.Modules.All(x => x == OptionalModule.UprisingChoam)) ||
                 (ix && !blood && c.Modules.IsEmpty) ||
                 (blood && !ix && c.Modules.All(x => x == OptionalModule.BloodlinesTech))),
            _ => false,
        };
        if (!supported) Error("unsupported-combination", "configuration", "This recognized combination is outside SLICE-001 composition scope; consult the compatibility matrix.");
        return errors.ToImmutable();
    }
}
