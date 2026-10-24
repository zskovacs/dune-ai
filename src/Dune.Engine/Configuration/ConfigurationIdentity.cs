using System.Security.Cryptography;
using System.Text.Json;

namespace Dune.Engine.Configuration;

internal static class ConfigurationIdentity
{
    // v1 canonical config binding. Ruleset identity remains separately versioned.
    public static string Compute(GameConfiguration c, bool includeSeeds = false)
    {
        var canonical = c with
        {
            RandomSeed = includeSeeds ? c.RandomSeed : 0,
            Expansions = [.. c.Expansions.Order()],
            Modules = [.. c.Modules.Order()],
            Variants = [.. c.Variants.Order()],
            Leaders = c.Leaders with
            {
                Seed = includeSeeds ? c.Leaders.Seed : 0,
                CandidateLeaders = [.. c.Leaders.CandidateLeaders.OrderBy(x => x.Value, StringComparer.Ordinal)],
                FixedAssignments = [.. c.Leaders.FixedAssignments.OrderBy(x => x.PlayerId)],
                RestrictionIds = [.. c.Leaders.RestrictionIds.Order(StringComparer.Ordinal)],
            },
            Content = c.Content with
            {
                Included = [.. c.Content.Included.OrderBy(x => x.Value, StringComparer.Ordinal)],
                Excluded = [.. c.Content.Excluded.OrderBy(x => x.Value, StringComparer.Ordinal)],
            },
        };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical, ConfigurationJson.Options)));
    }
}
