# ADR 0004: incremental turn execution and explicit support boundaries

- Date: 2026-10-24
- Status: Accepted for the SLICE-003 development preview

## Context

A Dune turn can contain several decisions, optional costs and effects owned by
another player. A monolithic action combining a card, space, effect order,
deployment and purchases would make action enumeration grow unnecessarily. The
existing runtime already owns physical cards, zones, seeded RNG and private
observations. Its descriptive catalog does not contain complete card effects.

## Decision

Add a small turn facade over existing state rather than another game engine.
Use immutable typed effect definitions with explicit execution contexts and
pending decisions. Card and board-space definitions use reusable primitives;
there is no card-name switch, reflection dispatcher or custom scripting language.
Do not add a dependency: ordinary .NET collections and records cover this bounded
state machine. Existing xUnit and BenchmarkDotNet remain the verification tools.

A placement selects a visible card and a board space. Subsequent choices are
individual actions through the same interface. Preserve strategically meaningful
effect order, optional payment and deployment choices. Acquisitions can occur
between Reveal effects, and ending acquisition is explicit. Stop when every
player has completed Reveal, before Combat execution.

Preflight the selected effect graph before mutating anything. Unsupported content
is distinct from illegal player input. Do not catch corrupted internal state and
report success or an ordinary illegal action. Incomplete decisions remain explicit
state, and snapshots compare that state as well as zones and resources.

Keep active turn ownership separate from pending decision ownership. Occupancy is
an executable base ruleset policy; future infiltration can change that policy.
Combat strength combines typed unit contributions with modifiers and obeys the
base game's zero-strength-without-troops restriction. No expansion unit is added.

Policy card selectors never disclose the existing physical IDs allocated in
sorted definition ranges. Only visible card references are actionable. Detached
observations never include hidden order, seeds, privileged history or mutable
runtime objects. Trusted replay/snapshot APIs remain separate developer data.

Bind replay to engine/runtime, ruleset/content and executable support revisions,
initial configuration/seeds, initial identity and ordered accepted decisions.
Rejected decisions do not enter history. Keep seeded System.Random and existing
shuffle implementation; determinism is scoped to the pinned runtime and versions.

## Consequences

The executable subset does not make a complete original Imperium environment.
Authentic initialization remains blocked by missing catalog inventories, and real
leaders require a complete passive/Signet contract before actions are allowed.
Development CLI, tests and benchmarks identify synthetic inventories explicitly.
No real card receives a fabricated default effect to make a scenario finish.

Content verification is granular: an Agent permission visible on a fanned card is
not evidence for its obscured Reveal box. Missing evidence stays unsupported.
The local PDFs support many board effects and a few complete card faces, but do
not establish the whole starting deck's effects or complete leader inventory.

Independent legal-action and mutable-transition benchmarks establish baselines;
no specialized allocator, clone framework or throughput claim is justified yet.

## Sources

- Local original Imperium rulebook (2020-10-26), pp.7–11,16.
- Local original Imperium Player Aid (2020), pp.1–2.
- [ADR 0001](0001-csharp-python-interop.md): existing RL boundary evaluation;
  no new RL framework or encoder in this slice.
- [ADR 0003](0003-runtime-initialization.md): existing state/RNG boundary.
- [System.Random](https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-random),
  consulted through Microsoft Learn MCP; independent explicitly seeded instances
  and pinned-runtime reproducibility remain required.
