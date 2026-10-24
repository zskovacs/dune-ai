using System.Collections.Immutable;
using Dune.Engine.Runtime;

namespace Dune.Engine.Turns;

public enum DuneTiebreaker { Spice, Solari, Water, GarrisonTroops }

/// <summary>Final score and shared competition rank; criteria list only actual splits of this seat's tied groups.</summary>
public sealed record DuneStanding(int PlayerId, int VictoryPoints, int Spice, int Solari, int Water,
    int GarrisonTroops, int Rank, ImmutableArray<DuneTiebreaker> AppliedTiebreakers);

/// <summary>Detached trusted episode result. Seeds and replay identity are never policy observation data.
/// Authenticity describes the inventory; it does not certify a complete playable Dune ruleset.</summary>
public sealed record DuneGameResult(string Schema, string EngineVersion, string RuntimeVersion,
    string SetupIdentity, string ConfigurationIdentity, string ConfigurationIdentityFormat,
    string RulesetIdentity, string ContentIdentity, string RulesVersion, string RulesIdentity,
    string SupportVersion, string LifecycleVersion, ContentAuthenticity Authenticity,
    int EnvironmentSeed, int LeaderSeed, int Rounds, DuneTerminalReason TerminalReason,
    ImmutableArray<DuneStanding> Standings, ImmutableArray<int> WinnerIds, string ReplayIdentity);

/// <summary>Canonical identity input deliberately excludes final state and result hashes.</summary>
internal sealed record TurnReplayIdentityData(string Schema, string ReplaySchema, string EngineVersion,
    string RuntimeVersion, string RulesIdentity, string RulesetIdentity, string ConfigurationIdentity,
    string ContentIdentity, string SupportVersion, int EnvironmentSeed, int LeaderSeed,
    string InitialStateHash, ImmutableArray<GameAction> Actions)
{
    internal static TurnReplayIdentityData From(TurnReplay replay) => new("dune-turn-replay-identity-v1",
        replay.Schema, replay.EngineVersion, replay.RuntimeVersion, replay.RulesIdentity, replay.RulesetIdentity,
        replay.ConfigurationIdentity, replay.ContentIdentity, replay.SupportVersion, replay.EnvironmentSeed,
        replay.LeaderSeed, replay.InitialStateHash, replay.Actions);
}
