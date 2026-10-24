# Proposed ability and decision execution design

SLICE-003.5 design recommendation, 2026-10-24, with SLICE-004 status delta.
Causal frames, explicit roles, phase windows, supported Combat/reward/Recall and
Endgame are implemented and tested; illustrative proposal names below are not
a declaration that every proposed interface exists. Scoped modifiers, KH, Graft,
expansion mechanics and SLICE-004.5 remain future work. Current APIs are in the
[turn architecture](turn-engine.md); historical findings are in the
[audit](ability-decision-audit.md); official mechanic evidence is in the
[case studies](complex-ability-case-studies.md). See
[ADR0006](../adr/0006-ability-and-decision-execution.md).

## Keep one engine and three ability categories

Keep `TurnEngine.Step(GameAction)`, its shared candidate/validation path, private
observations, typed Effect graph, and trusted replay. There is no standalone
EffectResolver to replace. The existing effect-type switch is small and appropriate;
do not grow it into a switch on individual card identities.

- **Primitive:** existing Gain/Draw/Recruit/Influence/Acquire and semantic game
  operations. Add selected discard/trash, unit loss/return, and control only for
  required verified mechanics. Different reasons for the same zone transfer are
  explicit; reshuffling is not a discard trigger.
- **Composite:** existing OptionalPayment/Choice plus small typed predicates and
  explicit execution groups. A `Sequence` completes each child and its consequences
  before the next child; a `PlayerOrdered` group exposes strategically meaningful
  order selections. Existing flat arrays behave like PlayerOrdered groups and must
  not silently acquire strict sequence semantics.
- **Special:** narrow typed handlers for exceptional eligibility/placement, copy
  binding or sourced timing. Handlers query/build an operation plan, then use the
  same validator, game operations and continuation machinery. KH should not run as
  an ordinary placement followed by another relocation. (M p.14.)

No natural-language interpretation, script language, card-text parsing, reflection
discovery, handler-per-trivial-card, event store, DI container or new dependency is
needed. BCL collections, immutable records, System.Text.Json, xUnit and
BenchmarkDotNet already satisfy the bounded requirements. A generic workflow/rules
library would add another execution and persistence model without demonstrated
benefit. Reconsider only with concrete missing capability or representative evidence.

## Execution and decision ownership

Evolve `EffectContext` incrementally. The required semantic roles are:

| Role                         | Existing representation                     | Proposed distinction                                                                                           |
|------------------------------|---------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| Normal turn / initiator      | GameState.CurrentPlayerId                   | Nullable normal-turn seat plus stable turn/action-group serial; an initiator remains available outside a turn. |
| Chooser                      | Context.OwnerId / GameState.DecisionOwnerId | Explicit decision owner; foreign decisions do not change normal turn or effect recipient implicitly.           |
| Payer / recipient            | Both inferred from OwnerId                  | Explicit when different; owner defaults remain for ordinary self effects.                                      |
| Executing source             | SourceCard / Space                          | Typed source: card, board, leader, future Tech/Skill; physical source identity and ability key retained.       |
| Definition source            | Implicit same as executing card             | Copy provenance separated from executing-self binding; never mutate shared definitions.                        |
| Participants / related cards | Missing                                     | Immutable action-group participants and role bindings, persisted for delayed references when needed.           |
| Target                       | Fixed ForPlayerEffect.PlayerId only         | Typed chosen player/card/Agent/space target with legal visibility and current eligibility.                     |
| Timing and continuation      | AcquisitionDepth plus flags                 | Typed timing point, causal frame/group and explicit resumption target.                                         |

Do not add a universal dictionary. Use typed compact records and optional fields
only for implemented mechanics. Context must outlive source movement; binding is
not a pointer to its current zone or a lambda capturing a mutable card list.

Replace the acquisition-only priority rule with a small serializable execution
frame stack. A frame owns source/context, group mode, remaining work, selected
branch and continuation destination. Child frames complete before parent resumes.
Pending IDs remain deterministic. Existing DecisionFrame optional/choice states
join that structure; no parallel Combat decision stack. Snapshot actual values,
not delegates or handlers. Keep suspension distinct from player-choice ordering.

Phase/window continuation destinations minimally include Agent completion,
Reveal acquisition continuation, Combat opportunity, reward resolution and phase
completion. `ContinueTurn` must not default to EndAgentTurn merely because pending
work empties during Combat. Only the appropriate parent may advance the scheduler.

## Timing, triggers, modifiers and validation

Start with typed timing points actually needed for original base Combat: normal
Combat priority, outcome established/reward resolution, post-win opportunity,
end-Combat, Round Start, Recall/endgame. I pp.12-13 establishes distinct normal and
post-win Intrigue play; I p.13's Demand Respect requires reward/play interleaving.
Timing categories are an allowlist for supported definitions, not a license to
invent every card's exact timing.

Later semantic discard/trash/acquire/placement occurrences can enqueue typed
triggered child work through the same stack. X p.7's Unload example needs two
separate occurrences for discarding then trashing the same card. Stable source
order is an engineering tie-break only when official player-selected ordering or
priority does not apply. Keep simultaneous optional trigger ordering unresolved
until sourced for the selected content.

Rule modifications use narrow operation queries: placement access/own occupancy/
enemy occupancy/destination ban/cost/requirements, acquisition destination, combat
participation/contribution, reward transformation. Split checks rather than add
`IgnoreAllRestrictions`. A transient placement plan carries permissions for that
one operation; persistent acquired abilities use immutable descriptors plus
per-game ownership and usage. Typed lifetime values may later identify action,
turn, round, next owner turn or game. Voice requires next-creator-turn expiry (I p.13); Tech and Commander Skills
require round usage (X p.4; B p.4).

Resolve static module contributions once, then query only applicable descriptors
in gameplay. Do not run RulesetComposer per Step. Preserve explicit interaction
modules from ADR0002; runtime does not currently consume them as executable effects.
Extend a capability-lowering seam only when supporting an actual recipe.

Validation has two stages: validate immutable graph shape/capability/source and
cycles at construction; validate current actor, timing, selected target, affordability,
remaining stock and operation eligibility immediately before committing a decision.
Preflight must keep unsupported children from causing partial action mutation.
For dynamic branches, validate only the applicable/selected path for gameplay
legality while retaining explicit support gating for content not yet executable.
Do not reserve hidden deck identities to make grants appear guaranteed.

Commit a complete validated placement/play/move atomically, then resolve its
consequences. Rejected actions preserve state, queue, usage, RNG and accepted
history. Invariant failures still throw as engine defects; this proposal does not
promise rollback after arbitrary corruption. Avoid a full clone/transaction on
every Step without a measured need. Selected card-cost/target stages must either
commit nothing until a complete plan exists or specify a sourced irreversible
decision boundary and cancellation behavior.

## LegalActions, observations and replay

Dispatch by phase and pending decision/window before accessing a normal active
player. The current PlayerTurns-only guard and null-forgiving active-player read
must change before Combat. Use the same operation validator for generation and
Step. Add Intrigue/pass/finish and later source/target actions to GameAction's
explicit JSON allowlist. Unsupported timing/cards must be absent, with safe support
diagnostics rather than fabricated empty effects.

Provide owner-scoped decision/window kind, stable decision ID and permitted target
handles. Preserve definition+copy selectors while copies are equivalent. Never
expose physical allocation-range IDs, seeds, source snapshots or opponent private
options through observations/masks/labels/diagnostics. Deterministic internal work
drains automatically; deliberate ordering and optional choices remain actions.

Version action/effect discriminators and trusted schema when semantics change.
Snapshot every frame, pending group, window cursor/pass evidence, selected source,
outcome/reward progress, usage and modifier lifetime. Existing replay reconstructs
from trusted initial factory plus accepted action list; retain this instead of
event sourcing. Replay paused before and after foreign choices must reconstruct
the same next legal actions and hash, as well as eventual outcome.

## SLICE-004 preparation plan: Combat, round completion and endgame

This is a plan for future implementation, not work performed by SLICE-003.5.
Initial scope is original Imperium Standard competitive 3/4 players. Actual complete
content remains a separate source gate. Implement as bounded tasks in dependency
order, each reviewed before the next. Preserve Toy regression checks.

| Task                                                     | Exact existing files / proposed new responsibility                                                                                                                             | Acceptance and verification gate                                                                                                                                                                                                                                                                                                                                |
|----------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 004-P1: causal execution and role context                | Extend `Turns/TurnSnapshot.cs` context/frames and `Turns/TurnEngine.cs` Enqueue/Execute/Choose/ContinueTurn; add cohesive internal frame helper only if needed                 | Synthetic strict child sequence cannot interleave with outer siblings; player-order group still supports board gain before optional payment; acquire children still preempt Reveal purchases; distinct chooser/payer/recipient works and resumes active owner; reject malformed permanent-operation graphs before mutation. `mise run test:engine`.             |
| 004-P2: phase-aware decision windows                     | Extend `Runtime/GameState.cs` phase enum, `Turns/GameAction.cs`, `TurnRules.cs`, `TurnSnapshot.cs`, `TurnEngine.cs` candidate/validation/scheduler dispatch                    | Combat window with nullable normal active player; clockwise current participant priority; separate opportunity completion from turn completion; all legal actions accepted; replay/privacy at suspended foreign decisions. No Intrigue text guessed. `mise run test:engine`.                                                                                    |
| 004-1: sourced original Combat content seam              | Add typed Intrigue timing/effects and Conflict reward/outcome definitions under `Turns/`; extend `OriginalImperiumTurnRules.cs` only with independently sourced complete boxes | Selected Conflict tiers/rewards/control and Intrigue boxes carry PDF/page+coverage tests in `docs/rules-sources.md`; unverified cards stay Unsupported; synthetic fixtures are explicitly labeled and isolated. No authentic completeness claim.                                                                                                                |
| 004-2: Intrigue opportunity loop                         | Reuse P1/P2 frames, existing player.Intrigue and shared.IntrigueDiscard, same Step/LegalActions                                                                                | I p.12: multi-card opportunity, voluntary pass, earlier passer returns, pass evidence reset by play, no-troop exclusion, marker refresh. Define dynamic membership edge interpretation with evidence. Post-win timing absent from ordinary priority.                                                                                                            |
| 004-3: outcomes and rewards                              | Reuse `Turns/CombatStrength.cs`; typed ranking/reward progress, reusable game operations; add necessary faction/control operations with focused support                        | I p.12: 3/4-player placements, all tie patterns, zero strength no reward, no winner on first tie, reward choices; I p.13: post-win reward interleaving for Demand Respect/To the Victor. Complete Alliance/control/threshold semantics when selected rewards require them (I pp.7,10). Stop before unsupported reward mutation.                                 |
| 004-4: Combat cleanup, Makers, Recall, next round        | Extend `TurnEngine` BeginRound/NextPlayer/EndReveal and state snapshots; reuse board bonus fields, troop counters, Mentat state and Conflict zones                             | I pp.12-13: all base troops to supply, markers reset, spice only at empty Maker spaces, threshold/deck-empty check at Recall, otherwise return Agents/Mentat, advance First Player, next Conflict/draw5. Preserve acquired Swordmaster and once/game use. Existing EndReveal already discards cards; do not discard twice or add cleanup triggers accidentally. |
| 004-5: endgame/result boundary                           | Extend phases/IsCompleted; add typed Dune result/ranking and Endgame Intrigue window; adapt generic IGame only if its result/reset contract is ready                           | I p.13: threshold/conflict exhaustion -> Endgame Intrigue -> final VP and spice/Solari/water/garrison tie-break chain. Preserve a true unresolved equality if book gives no further tiebreak; do not invent seat-order winner. No actions after completion, result inaccessible before completion.                                                              |
| 004-6: full supported-scope verification and measurement | `tests/Dune.Engine.Tests/`, relevant simulation/CLI tests; extend existing benchmarks only for changed measured paths                                                          | Replay every phase/window cut, illegal timing atomicity, nonempty hidden Intrigue privacy, separate episodes/parallel schedules; `mise run check`, `mise run smoke`, bounded preview; `mise run benchmark -- --job Dry --filter '*TurnEngineBenchmarks*'` if harness changed, statistical comparison before speed claims.                                       |

P1/P2 may be prerequisites inside a divided SLICE-004 or a named prerequisite
slice. Do not postpone them until after Combat reward code. Modifiers/relocation/
Graft support is not required to build original base Combat; context should preserve
their extension path without implementing unused expansion state.

High risks: automatically ending the wrong turn on child completion; rotating
after each card rather than each opportunity; treating pass as permanent; stale
participant/strength data; rewards needing currently gated Influence>=4; reward
versus post-win cost timing; clearing usage too early; Mentat/Agent-kind accounting;
snapshot omission; full ties; enabling missing real card data to force demos through.

Completion means a verified selected scope with honest diagnostics and full
phase/replay/privacy tests. A claim of an authentic complete playable environment
additionally requires a complete applicable inventory and every enabled leader,
card, Intrigue, Conflict and interaction supported; fixtures alone never satisfy it.
No expansion mechanics, KH/Graft implementation, broad card audit, RL algorithm,
encoder, interop transport or speculative hot-loop rewrite belongs in SLICE-004.

## SLICE-004.5 preparation plan: catalog and ability coverage audit

Depend on completed P1/P2 and Combat timing/reward/result interfaces. Source gathering
can begin sooner, but do not finalize ability coverage against hypothetical Combat
APIs or promote real gameplay readiness before full enabled-path verification.

| Task                                          | Existing foundation / exact future responsibility                                                                                                                          | Acceptance and verification gate                                                                                                                                                                                                                                                                                                                              |
|-----------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 004.5-1: evidence/coverage schema             | Keep `ContentDefinition`, `AvailabilityRequirements`, `SourceReference`, `ContentCatalog` IDs/quantity/product; extend coverage metadata separately from runtime instances | Per-box status and source, not a single Implemented flag. Version changes affecting fingerprints deliberately. Link source edition/page/section, missing-face/evidence/license status and inventory multiplicity.                                                                                                                                             |
| 004.5-2: reconcile selected inventories       | `Content/base-setup.json`, `representative.json`, catalog schema/source docs, existing setup validators                                                                    | Check identity, quantity, category/tier, base/module/format availability, duplicates/replacements. Missing inventories/card faces stay explicit. No unlicensed community import or inference from names.                                                                                                                                                      |
| 004.5-3: classify ability boxes               | `Turns/CardRule/LeaderRule` and future Intrigue/Conflict definitions plus coverage records                                                                                 | Record primitive/composite/special; timing/trigger/condition/cost/target/source traits; capability dependencies; implemented handler/effect key; unsupported reason; behavioral/privacy/replay test IDs and review status. Setup/passive/Signet/Agent/Reveal/Acquire/Combat/post-win/endgame are distinct coverage units.                                     |
| 004.5-4: interaction graph and batches        | Reuse this audit's matrix/case studies and existing `docs/compatibility-matrix.md`/module provenance                                                                       | Track capability+interaction cases and applicability, not a full Cartesian product. Explicit KH+Graft/Ghola, copied self/trait identity, Unload occurrences, permission provenance, unit participation, Skill usage, market/destination replacement and timing.                                                                                               |
| 004.5-5: prioritized implementation proposals | Output batches tied to actual completed SLICE-004 contracts                                                                                                                | First verified starter/leader/inventory blockers; then primitive-only base cards; conditional/payment/target cards; base Intrigue/Conflict gaps; special placement/trigger/copy/delayed mechanics; expansion batches behind independently executable setup/modules. Each batch has sources, prerequisites, test plan, exclusions and measurable completeness. |

Suggested coverage record fields: content ID/revision/product/category/quantity;
applicability and replacement lineage; box/ability ID; source PDF edition/page/section;
evidence completeness and reuse rights; timing and trigger; typed mechanics/tags;
source/participant/target and payer roles; conditions/costs/limits/lifetime;
primitive/composite/special classification; handler/effect implementation location;
Supported/Partial/Missing/Unknown status with unsupported reason; capability and
cross-card/module dependencies; behavioral/privacy/replay tests; review state and
batch priority. Keep descriptive evidence text out of gameplay execution.

Already completed and reusable: actual API map, capability gaps, rule-backed KH/
Combat/Graft/representative expansion findings, core design alternatives, current
test evidence, support/authenticity gates and measured baseline scope. Verify the
delta after SLICE-004; do not commission this same architecture audit again.

Known missing sources: complete readable starter/Imperium/Intrigue/Conflict faces,
authentic leader setup/passive/Signet contracts, exact inventory coverage and
unresolved KH occupancy/edge timing rulings. The supplied PDFs illustrate only
some complete faces. SLICE-004.5 should find and record these gaps rather than
silently acquire rights or claim all text was verified. Its default deliverable
is a catalog/coverage audit and prioritized batches, not permission to implement
every batch, expansion, training system or a new effect language.
