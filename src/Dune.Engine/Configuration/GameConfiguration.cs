using System.Collections.Immutable;
using Dune.Engine.Content;

namespace Dune.Engine.Configuration;

public enum BaseGame { Imperium, Uprising }
public enum Expansion { RiseOfIx, Immortality, Bloodlines }
public enum OptionalModule { UprisingChoam, BloodlinesTech }
public enum GameMode { Standard, Epic }
public enum RuleVariant { GoTo11 }
public enum PlayerFormat { Competitive, SoloWithRivals, TwoPlayerWithRivals, SixPlayerTeams }
public enum LeaderSelectionStrategy { Fixed, Random, SequentialDraft, SnakeDraft }

public sealed record PlayerConfiguration(PlayerFormat Format = PlayerFormat.Competitive, int Count = 4);
public sealed record LeaderAssignment(int PlayerId, ContentId Leader);

/// <summary>Pregame policy only; drafts and random assignment are not executed in SLICE-001.</summary>
public sealed record LeaderSelectionConfiguration
{
    public LeaderSelectionStrategy Strategy { get; init; } = LeaderSelectionStrategy.Random;
    public int Seed { get; init; }
    public ImmutableArray<int> PlayerOrder { get; init; } = [0, 1, 2, 3];
    public ImmutableArray<ContentId> CandidateLeaders { get; init; } = [];
    public ImmutableArray<LeaderAssignment> FixedAssignments { get; init; } = [];
    public ImmutableArray<string> RestrictionIds { get; init; } = [];
}

/// <summary>Filters the representative catalog, never claims a complete official inventory.</summary>
public sealed record ContentSelectionProfile
{
    public string Id { get; init; } = "representative";
    public int Revision { get; init; } = 1;
    public ImmutableArray<ContentId> Included { get; init; } = [];
    public ImmutableArray<ContentId> Excluded { get; init; } = [];
}

public sealed record GameConfiguration
{
    public int SchemaVersion { get; init; } = 1;
    public BaseGame BaseGame { get; init; } = BaseGame.Imperium;
    public ImmutableArray<Expansion> Expansions { get; init; } = [];
    public ImmutableArray<OptionalModule> Modules { get; init; } = [];
    public GameMode Mode { get; init; } = GameMode.Standard;
    public ImmutableArray<RuleVariant> Variants { get; init; } = [];
    public PlayerConfiguration Players { get; init; } = new();
    public LeaderSelectionConfiguration Leaders { get; init; } = new();
    public ContentSelectionProfile Content { get; init; } = new();
    public int RandomSeed { get; init; }
}
