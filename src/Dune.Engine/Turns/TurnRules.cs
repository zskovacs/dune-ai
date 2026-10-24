using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public enum AgentIcon { Emperor, SpacingGuild, BeneGesserit, Fremen, Landsraad, City, SpiceTrade }
public enum ResourceKind { Solari, Spice, Water, VictoryPoints, Persuasion, Swords }
public sealed record ResourceCost(int Solari = 0, int Spice = 0, int Water = 0);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$effect")]
[JsonDerivedType(typeof(GainEffect), "gain")]
[JsonDerivedType(typeof(DrawEffect), "draw")]
[JsonDerivedType(typeof(RecruitEffect), "recruit")]
[JsonDerivedType(typeof(InfluenceEffect), "influence")]
[JsonDerivedType(typeof(OptionalPaymentEffect), "optional")]
[JsonDerivedType(typeof(ChoiceEffect), "choice")]
[JsonDerivedType(typeof(ForPlayerEffect), "player")]
[JsonDerivedType(typeof(TrashSelfEffect), "trash-self")]
[JsonDerivedType(typeof(AcquireEffect), "acquire")]
[JsonDerivedType(typeof(HighCouncilEffect), "council")]
[JsonDerivedType(typeof(SwordmasterEffect), "swordmaster")]
[JsonDerivedType(typeof(HarvestSpiceEffect), "harvest")]
[JsonDerivedType(typeof(UnsupportedEffect), "unsupported")]
[JsonDerivedType(typeof(SignetEffect), "signet")]
[JsonDerivedType(typeof(SequenceEffect), "sequence")]
[JsonDerivedType(typeof(PlayerOrderedEffect), "player-ordered")]
[JsonDerivedType(typeof(RetreatEffect), "retreat")]
[JsonDerivedType(typeof(DrawIntrigueEffect), "draw-intrigue")]
[JsonDerivedType(typeof(ConditionalEffect), "conditional")]
public abstract record Effect;
public sealed record GainEffect(ResourceKind Resource, int Amount) : Effect;
public sealed record DrawEffect(int Amount) : Effect;
public sealed record RecruitEffect(int Amount) : Effect;
public sealed record InfluenceEffect(Faction Faction, int Amount = 1) : Effect;
/// <summary>A legacy chooser override also binds payer/recipient unless explicitly overridden.</summary>
public sealed record OptionalPaymentEffect(ResourceCost Cost, ImmutableArray<Effect> Effects,
    int? DecisionOwnerId = null) : Effect
{
    public int? PayerId { get; init; }
    public int? RecipientId { get; init; }
}
public sealed record EffectOption(string Label, ResourceCost Cost, ImmutableArray<Effect> Effects);
public sealed record ChoiceEffect(ImmutableArray<EffectOption> Options) : Effect;
/// <summary>The target binds all roles by default; each explicit role override replaces that binding.</summary>
public sealed record ForPlayerEffect(int PlayerId, ImmutableArray<Effect> Effects) : Effect
{
    public int? DecisionOwnerId { get; init; }
    public int? PayerId { get; init; }
    public int? RecipientId { get; init; }
}
public sealed record SequenceEffect(ImmutableArray<Effect> Effects) : Effect;
public sealed record PlayerOrderedEffect(ImmutableArray<Effect> Effects) : Effect;
public sealed record TrashSelfEffect : Effect;
public sealed record SignetEffect : Effect;
public sealed record AcquireEffect(ContentId Card, bool AllowEmpty = false) : Effect;
public sealed record HighCouncilEffect : Effect;
public sealed record SwordmasterEffect : Effect;
public sealed record HarvestSpiceEffect : Effect;
public sealed record UnsupportedEffect(string Reason) : Effect;
/// <summary>Choose 0..Maximum deployed recipient troops to return to their garrison during Combat.</summary>
public sealed record RetreatEffect(int Maximum) : Effect;
/// <summary>Draw from the shared Intrigue deck without unverified recycling.</summary>
public sealed record DrawIntrigueEffect(int Amount) : Effect;
public enum EndgameCondition { OwnAtLeastTwoSpiceMustFlow, OwnMoreSpiceMustFlowThanEachOpponent }
public sealed record ConditionalEffect(EndgameCondition Condition, ImmutableArray<Effect> Effects) : Effect;

public sealed record CardRule(ContentId Id, ImmutableArray<AgentIcon> Icons,
    ImmutableArray<Effect> AgentEffects, ImmutableArray<Effect> RevealEffects,
    int Cost = 0, ImmutableArray<Effect> AcquireEffects = default)
{
    public bool Purchasable { get; init; } = true;
    public ImmutableArray<SourceReference> Sources { get; init; } = [];
}
public sealed record BoardRule(BoardSpaceId Id, AgentIcon Icon, ResourceCost Cost,
    ImmutableArray<Effect> Effects, bool Combat = false, Faction? Faction = null,
    int RequiredInfluence = 0, bool OncePerGame = false, ResourceKind? ControlReward = null)
{
    public int Capacity { get; init; } = 1;
    public ImmutableArray<EffectOption> PlacementOptions { get; init; } = [];
    public ImmutableArray<Effect> RevealEffects { get; init; } = [];
    public ImmutableArray<SourceReference> Sources { get; init; } = [];
}
/// <summary>A complete passive and Signet contract is required for an official leader.</summary>
public sealed record LeaderRule(ContentId Id, bool FullySupported,
    ImmutableArray<Effect> SignetEffects, bool Synthetic = false);
public sealed record TurnRuleSet(string Version, ImmutableArray<CardRule> Cards,
    ImmutableArray<BoardRule> Spaces, ImmutableArray<LeaderRule> Leaders)
{
    public CombatStrengthRule Strength { get; init; } = new();
    public LifecycleRuleSet? Lifecycle { get; init; }
    public string SupportVersion { get; init; } = "original-imperium-turn-support-v6";
}
