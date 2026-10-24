# ADR 0006: extend the existing ability and decision executor

- Date: 2026-10-24
- Status: Proposed for SLICE-004 prerequisites; runtime changes are not implemented
- Scope: Ability composition, causal decisions, timing and narrow rule exceptions

## Context

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

Recommend evolving the existing typed resolver, actions and pending state. Add
explicit causal sequence/player-order frames and phase/window continuations;
separate decision owner, payer, recipient, target and executing/definition source
where needed. Combat priority and nested decisions use that same mechanism.
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
| Existing resolver + explicit contexts/frames/windows/queries | Reuses working contracts and adds only demonstrated missing semantics. Selected recommendation.                                                                           |

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

This is a proposed design, not code approval for SLICE-004 or a completed engine.
Exact KH occupied-destination exceptions, dynamic participant re-entry/pass details,
and simultaneous/post-win ordering beyond supplied examples require evidence for
enabled content. The full catalog/leader inventory remains incomplete. No generic
trigger bus, new PRNG, public snapshot restore, ML encoding or expansion-state
framework is selected. Update this ADR's status only after the external design
review and corresponding verified implementation milestones.
