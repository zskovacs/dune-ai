# ADR 0007: Offline content coverage and runtime version boundaries

- Date: 2026-10-25
- Status: Accepted for SLICE-004.5 audit tooling

## Context and decision

The representative embedded catalog and bounded typed rule registry cannot answer
how much of the official game is sourced, tested, or playable. Keep a separate,
offline, manually reviewed audit in [`inventory.json`](../content/coverage/inventory.json).
The strict standard-library Python model validates typed records and computes
quantity, dependency and readiness projections. Five Markdown reports are
generated from that single JSON source. Markers delimit owned regions; report
generation preserves surrounding prose, orders records deterministically and
adds no current-clock fields. `report --check` and `content:validate` are
read-only freshness/integrity checks. Neither runs a game.

The audit is never loaded by Engine, Simulation or CLI, used as an agent policy
input, parsed during transitions, or included in canonical configuration/ruleset
hashes. Typed immutable runtime definitions remain resolved before gameplay.
This avoids per-action text lookup, reflection or a new interpreter. The Python
standard library, pytest, xUnit and existing mise tasks suffice; no dependency
or generic authoring framework is added.

## Evidence, status and rights

Quantity expectations are scoped physical copies, per-player recipes, selected
subsets or exclusions. Overlapping rows cannot be summed into a whole game.
Nullable unique totals and copy counts mean unknown, not zero. Identity,
quantity, applicability, whole-face semantics, ruling and actual behavior are
different evidence claims. One-based PDF pages and pinned source revisions identify
where each claim came from. `implemented` means a bounded code path exists;
`tested` means a current bounded behavior reference exists. Neither is whole-face
validation or authentic execution. Capability and interaction dependencies and
unknown slots still gate readiness. Initializer, complete-game and RL gates stay
false until exact authentic execution provenance can be independently verified.

Official public documents provide reference facts, not a redistribution grant.
Community catalog rows are discovery candidates only. Their names, quantities,
effect text and art are not imported without independent verification and rights.
The source assessment records limitations; the audit's `rights` field does not
grant permission. Raw PDFs, scans, extracted text and website bodies stay in
lawfully held ignored local storage.

## Versioning matrix

| Change | Required review and identity effect |
| --- | --- |
| Audit-only records, evidence, batch order or report prose | Bump audit revision/schema as needed; no runtime format or fingerprint bump. |
| Embedded content quantity, source/status, availability or revision | Review and regenerate canonical content/ruleset identities even if the schema shape is unchanged; check configuration hashes and golden values. |
| Executable authored card/board/interaction rules | Deliberate authoring and rules identity revision, targeted behavior/privacy tests and replay compatibility review. |
| Action/effect/state/result semantics or support vocabulary | Review configuration, ruleset, setup, turn replay, support, authoring, result and replay identity contracts; migrate with explicit old-header behavior and golden-hash/round-trip/privacy checks. Never silently reinterpret an old replay. |

This audit leaves current configuration v1, ruleset v2, setup v5,
turn-replay v7, support 13, authoring 9, result 1 and replay identity 1 unchanged.

## Future execution and measurement obligations

Selected discard/trash and Reserve return require owner-visible zone plus
definition/copy selectors, payment and target validation before movement, and
correct reservation accounting when Reserve stock returns. A random opponent
Intrigue transfer uses engine RNG, never a private face selector in an agent
action; pre-action legal masks and support metadata must not depend on hidden
faces, order or RNG. Reject invalid placement before RNG consumption and replay
the accepted choice. Public faction and Leader costs/eligibility must match
LegalActions and Step with verified face qualifiers.

Voice marker use needs conservation and creator-next-turn lifetime; Kwisatz
Haderach needs atomic send plus Agent-kind source/destination provenance. Plot,
Reveal and after-Reveal timing, deferred Mentat, Alliance loss, final Reveal
tiebreak, copied definitions and once-game usage require explicit state and
ordering tests. Generated child actions must preflight mandatory stock and later
recipients before committing a parent. Preserve the existing focused regression
families in `EffectSafetyTests`, `FactionRewardTests` and `ConflictRewardTests`.
These are future requirements, not newly passing rule claims.

The offline audit adds no game hot-path work. Before optimizing future authentic
content, benchmark enabled 67-card market, 40-Intrigue and 18-Conflict pools,
legal branching, target choices, nested child/order/payment graphs and 0/1/8
relevant modifiers. Compare identical accepted traces, configuration, host and
runtime; separate setup cost from 3/4-player complete episodes; record elapsed
time, allocations, GC, peak memory and worker scaling. The
[SLICE-004 benchmark](../benchmarks/slice-004.md) used synthetic workloads and
does not promise authentic throughput. No optimizer or generic resolver is
approved by this ADR.
