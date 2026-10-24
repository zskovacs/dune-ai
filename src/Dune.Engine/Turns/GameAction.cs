using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

/// <summary>Visible content and duplicate index in its visible zone; never a physical instance ID.</summary>
public sealed record CardSelector(ContentId Definition, int Copy = 0);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$action")]
[JsonDerivedType(typeof(StartRoundAction), "start-round")]
[JsonDerivedType(typeof(PlaceAgentAction), "place")]
[JsonDerivedType(typeof(RevealAction), "reveal")]
[JsonDerivedType(typeof(ResolveEffectAction), "effect")]
[JsonDerivedType(typeof(ChooseOptionAction), "option")]
[JsonDerivedType(typeof(DeployAction), "deploy")]
[JsonDerivedType(typeof(AcquireCardAction), "acquire")]
[JsonDerivedType(typeof(EndRevealAction), "end-reveal")]
public abstract record GameAction(int PlayerId);
public sealed record StartRoundAction() : GameAction(-1);
public sealed record PlaceAgentAction(int PlayerId, CardSelector Card, BoardSpaceId Space, int PaymentOption = 0) : GameAction(PlayerId);
public sealed record RevealAction(int PlayerId) : GameAction(PlayerId);
public sealed record ResolveEffectAction(int PlayerId, int EffectId) : GameAction(PlayerId);
/// <summary>Optional payments use 0 = decline, 1 = pay; choices use zero-based option indexes.</summary>
public sealed record ChooseOptionAction(int PlayerId, int Option) : GameAction(PlayerId);
public sealed record DeployAction(int PlayerId, int Troops) : GameAction(PlayerId);
public sealed record AcquireCardAction(int PlayerId, CardSelector Card, CardZoneId Source) : GameAction(PlayerId);
public sealed record EndRevealAction(int PlayerId) : GameAction(PlayerId);

public enum StepStatus { Applied, Illegal, Unsupported, ContentError }
public sealed record TurnDiagnostic(StepStatus Status, string Code, string Message);
public sealed record StepResult(StepStatus Status, ImmutableArray<TurnDiagnostic> Diagnostics)
{
    public bool IsSuccess => Status == StepStatus.Applied;
}
public sealed record ObservedPendingEffect(int Id, int OwnerId, string Kind);
public sealed record PendingDecisionSummary(int OwnerId, string Kind, ImmutableArray<string> Options);
public sealed record TurnObservation(GameObservation Game, ImmutableArray<ObservedPendingEffect> PendingEffects,
    PendingDecisionSummary? Decision, ImmutableArray<GameAction> LegalActions);
