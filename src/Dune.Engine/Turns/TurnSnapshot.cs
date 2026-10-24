using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public sealed record UsedSpace(int PlayerId, BoardSpaceId Space);
public sealed record TurnSnapshotData(string Schema, string RulesIdentity, string InitialStateHash, SnapshotData Game,
    ImmutableArray<EffectExecutionFrame> Frames, int NextFrameId,
    bool Revealing, bool Deploying, int Recruited, int OldGarrison, int NextEffectId,
    ImmutableArray<UsedSpace> UsedSpaces, ImmutableArray<GameAction> Actions)
{
    public DecisionWindowState? Window { get; init; }
    public int NextWindowId { get; init; }
    public CombatOutcome? CombatOutcome { get; init; }
    public ConflictRewardProgress? ConflictRewards { get; init; }
    public DuneGameResult? Result { get; init; }
    [JsonIgnore]
    public ImmutableArray<PendingEffect> Pending => [.. Frames.SelectMany(x => x.Remaining)];
    [JsonIgnore]
    public ImmutableArray<DecisionFrame> Decisions => [.. Frames.Where(x => x.Decision is not null).Select(x => x.Decision!)];
}
/// <summary>Complete trusted turn state. Contains hidden cards, seeds, pending execution and history.</summary>
public sealed class TrustedTurnSnapshot
{
    internal TrustedTurnSnapshot(TurnSnapshotData data)
    {
        Data = data;
        CanonicalJson = JsonSerializer.Serialize(data, ConfigurationJson.Options);
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson)));
    }
    public string DebugLabel => "TRUSTED HIDDEN TURN DATA — never pass to policies";
    public TurnSnapshotData Data { get; }
    public string CanonicalJson { get; }
    public string Hash { get; }
}
public sealed record TurnReplay(string Schema, string EngineVersion, string RuntimeVersion,
    string RulesIdentity, string RulesetIdentity, string ConfigurationIdentity, string ContentIdentity,
    string SupportVersion, int EnvironmentSeed, int LeaderSeed, string InitialStateHash,
    ImmutableArray<GameAction> Actions, string FinalStateHash);
public sealed record TurnReplayResult(TurnEngine? Engine, ImmutableArray<TurnDiagnostic> Diagnostics)
{
    public bool IsSuccess => Engine is not null && Diagnostics.IsEmpty;
}
