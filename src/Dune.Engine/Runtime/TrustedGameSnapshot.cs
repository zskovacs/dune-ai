using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Runtime;

public sealed record SnapshotZone(
    CardZoneId Id,
    string Name,
    ZoneVisibility Visibility,
    int? OwnerId,
    ImmutableArray<CardInstance> Cards);

public sealed record SnapshotPlayer(
    int Id,
    ContentId Leader,
    PlayerResources Resources,
    PlayerUnits Units,
    ImmutableArray<FactionInfluence> Influence,
    bool HasHighCouncilSeat,
    int CombatMarker,
    TemporaryPlayerState Temporary,
    ImmutableArray<ActiveEffect> ActiveEffects,
    ImmutableArray<StartingCardRecipe> StartingComposition);

public sealed record SnapshotBoardSpace(
    BoardSpaceDefinition Definition,
    ImmutableArray<AgentPlacement> Occupants,
    int BonusSpice,
    int? ControllerId);

public sealed record SnapshotBoard(
    ContentId Definition,
    ImmutableArray<SnapshotBoardSpace> Spaces,
    ImmutableArray<AllianceToken> Alliances,
    MentatLocation Mentat,
    ResourceBank Bank);

public sealed record SnapshotData(
    string Schema,
    string EngineVersion,
    string RuntimeVersion,
    string SetupIdentity,
    string RulesetIdentity,
    string ConfigurationIdentity,
    string ConfigurationIdentityFormat,
    string ContentIdentity,
    ContentAuthenticity Authenticity,
    int Round,
    GamePhase Phase,
    int FirstPlayerId,
    int? DecisionOwnerId,
    int? CurrentPlayerId,
    bool IsCompleted,
    RandomCheckpoint MainRandom,
    RandomCheckpoint LeaderRandom,
    ImmutableArray<SnapshotPlayer> Players,
    SnapshotBoard Board,
    ImmutableArray<SnapshotZone> Zones,
    ImmutableArray<ActiveEffect> ActiveEffects)
{
    public DuneTerminalReason TerminalReason { get; init; }
}

/// <summary>WARNING: trusted debug data contains all hidden cards and RNG seeds. Never expose to policies.</summary>
public sealed class TrustedGameSnapshot
{
    private TrustedGameSnapshot(SnapshotData data)
    {
        Data = data;
        CanonicalJson = JsonSerializer.Serialize(data, ConfigurationJson.Options);
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson)));
    }

    public string DebugLabel => "TRUSTED HIDDEN DATA — includes private cards, deck order and seeds";
    public SnapshotData Data { get; }
    public string CanonicalJson { get; }
    public string Hash { get; }

    internal static TrustedGameSnapshot Capture(GameState state)
    {
        if (!state.Extensions.IsEmpty)
            throw new NotSupportedException("Module extensions require an explicit typed snapshot contract.");
        return new(new("dune-setup-snapshot-v5", typeof(GameState).Assembly.GetName().Version!.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            state.Definition.Setup!.Id, state.Definition.Fingerprint, state.ConfigurationIdentity,
            "dune-configuration-sha256-v1", state.ContentIdentity, state.Authenticity,
            state.Round, state.Phase, state.FirstPlayerId, state.DecisionOwnerId, state.CurrentPlayerId,
            state.IsCompleted, state.Random.Checkpoint, state.LeadersRandom.Checkpoint,
            state.Players.OrderBy(x => x.Id).Select(x => new SnapshotPlayer(x.Id, x.Leader, x.Resources, x.Units,
                x.Influence, x.HasHighCouncilSeat, x.CombatMarker, x.Temporary, x.ActiveEffects,
                x.StartingComposition)).ToImmutableArray(),
            new(state.Board.Definition.Id, state.Board.Spaces.Select(x => new SnapshotBoardSpace(x.Definition,
                    x.Occupants, x.BonusSpice, x.ControllerId)).ToImmutableArray(), state.Board.Alliances,
                state.Board.Mentat, state.Board.Bank),
            state.Zones.OrderBy(x => x.Id.Value, StringComparer.Ordinal).Select(x =>
                new SnapshotZone(x.Id, x.Name, x.Visibility, x.OwnerId, [.. x.Cards])).ToImmutableArray(),
            state.ActiveEffects)
        { TerminalReason = state.TerminalReason });
    }
}
