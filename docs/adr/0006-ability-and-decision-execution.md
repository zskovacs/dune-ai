# ADR 0006: extend the existing ability and decision executor

- Date: 2026-10-24
- Status: Accepted; implemented and tested for the SLICE-004 supported original-base scope; final branch review pending
- Scope: Ability composition, causal decisions, timing and narrow rule exceptions

## Context at the SLICE-003.5 decision

SLICE-003 has typed effects and a single `TurnEngine` over existing state. It can
transfer decision ownership, replay nested choices and prioritize acquire children.
It cannot execute outside PlayerTurns, represent a Combat pass window or guarantee
general child-before-parent continuation. `EffectContext.OwnerId` conflates chooser,
payer and beneficiary; one SourceCard cannot bind Graft/copy relationships.

Official representative cases justify shared extension points: normal and post-win
Combat Intrigue (original I pp.12-13); KH relocation with preserved restrictions (I p.13), single sending operation with
Graft/Ghola (Immortality M p.14); two-card
source relationships (M pp.10-11,14); per-icon infiltration and per-occurrence
Unload (Ix X p.7); immediate after-placement intelligence (Uprising U p.11);
Commander conditional Skills/usage (Bloodlines B pp.4-5). Exact PDF filenames and
editions are in [case studies](../architecture/complex-ability-case-studies.md).

## Decision

Evolve the existing typed resolver, actions and pending state. SLICE-004
implements explicit causal sequence/player-order frames, phase/window
continuations and separate initiator, decision owner, payer and recipient roles
with physical/definition provenance for the supported original-base lifecycle.
Combat priority and nested decisions use that same mechanism. Richer target and
executing/copy-source relationships remain future work when sourced content needs them.
Persist value state sufficient for trusted snapshot/replay, not callbacks.

Keep primitive effects reusable, composites typed and limited, and special handlers
narrow. Exceptional placement handlers produce a validated single-operation plan;
they do not run another Agent turn or bypass shared payment/ban/use checks. Typed
rule queries and scoped permissions replace broad ignore-all flags. Add persistent
modifiers and semantic triggers only when evidenced implemented mechanics need them.
Never dynamically interpret natural-language card descriptions.

Retain setup-time immutable module composition, explicit replacements/interactions,
strict content/authenticity gates, shared candidate/Step validation, private
observations and versioned action replay. Compile static executable information
once when supporting a recipe; no runtime repeated composition. This ADR does not
declare any additional expansion or real leader supported.

## Alternatives and selection rationale

| Alternative                                                  | Assessment                                                                                                                                                                |
|--------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Keep current flags and add Combat-only loops                 | Small initial edit, but incorrectly couples continuation to EndAgentTurn and duplicates ownership/interrupt rules. Reject.                                                |
| Separate Combat effect/action engine                         | Duplicates validation, private decisions and replay without a demonstrated need. Reject.                                                                                  |
| Full generic script/workflow/event-sourcing framework        | Large interpretation, persistence and performance surface; no existing consumer requires it. Reject.                                                                      |
| Dedicated KH action/one handler per card                     | Appropriate narrow handlers may exist, but standalone mutation paths duplicate placement logic and trivial handlers duplicate primitives. Reject as general architecture. |
| Existing resolver + explicit contexts/frames/windows/queries | Reuses working contracts and adds only demonstrated missing semantics. Selected and implemented for supported original-base execution.                                                                           |

## Consequences and performance

P1/P2 in the [preparation plan](../architecture/ability-execution-design.md) precede
Combat. New discriminators/state semantics require deliberate snapshot/replay/support
versioning and golden/privacy tests. Existing arrays retain player-order freedom;
strict sequences must be explicit. Generic position equality remains separate
from the current history-dependent replay hash.

Use existing .NET immutable records/collections, System.Text.Json, xUnit and
BenchmarkDotNet; no new library is justified. Ordinary typed dispatch is sufficient
until representative measurement proves otherwise. Frames/contexts add state and
objects; reducing repeated preflight/candidate work may offset costs but is not
a performance claim. Compare nested decisions, pass windows, legal enumeration
and scoped queries with the existing baseline before optimization.

## Known limits and unresolved decisions

Implementation was authorized by the reviewed SLICE-004 plan. This does not certify a complete authentic engine.
KH occupied-destination exceptions, dynamic participant re-entry and
simultaneous/post-win ordering beyond supplied examples still require evidence
before enabling content. The full catalog/leader inventory remains incomplete. No generic
trigger bus, new PRNG, public snapshot restore, ML encoding or expansion-state
framework is selected. The implementation uses `EffectExecutionFrame`, `ExecutionContinuation`,
`EffectContext` and `DecisionWindowState` within `TurnEngine`, with explicit
`InitiatorId`, `DecisionOwnerId`, `PayerId` and `RecipientId`.

## SLICE-004 evidence and retained gates

`LifecycleReplayTests`, `EffectContinuationTests`, Combat/Conflict reward/faction/round/Endgame
tests and `LifecycleIntegrationTests` verify causal suspension, deterministic
replay, rejection, hidden-information boundaries and independent episodes.
The current boundary is setup v5, turn/replay v7, support/preview v13, authoring v9.
`DuneGameResult` and replay identity are independently v1; final action identity
excludes final-state hash to avoid circular hashing.

[Measurements](../benchmarks/slice-004.md) distinguish unchanged micro workloads
from synthetic full games. No optimization, dependency or per-action JSON was
added. The catalog/leader/Conflict evidence, Plot, holder loss, tied positive
shared Intrigue draws, contested Mentat and conservative composite-graph gates
remain explicit. No expansion capability, general trigger framework, unexpected
exception rollback, authentic complete-game or RL-ready claim follows.
All task reviews are complete. The final whole-branch review is controller-owned and pending.
