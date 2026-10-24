# Development roadmap

## Initialization — complete

mise/toolchain locks, .NET engine/simulation boundaries, isolated playable toy,
replay, autonomous agents, ordered parallel episodes, Python package skeleton and
BenchmarkDotNet. The invented Toy rules remain an independent regression example.

## SLICE-001 — composition foundation

Typed configuration, sourced representative module composition, explicit conflict
handling, immutable content metadata, deterministic fingerprints and configuration
presets. All seven supplied rulebooks are indexed; compatibility, content-source
assessment, leader-policy boundary and implementation status are documented. **No Dune configuration is playable.**
Supported metadata resolution is narrower
than the recognized official product/mode/format vocabulary. See the
[composition architecture](architecture/ruleset-composition.md),
[rules index](rules-index.md), [matrix](compatibility-matrix.md), and
[content assessment](content/sources.md).

## SLICE-002 — base setup and runtime state

Implemented original Imperium Standard competitive 3/4-player setup infrastructure:
typed resolved recipes and board, isolated runtime state, physical card zones,
fixed/random unique leaders, deterministic first player/decks, explicit private
observations and trusted canonical snapshots. Stops before Round Start. Tests and
initialization benchmarks use internal synthetic content with authentic quantities.
The public initializer remains blocked by the incomplete authentic catalog. No
Dune game is playable. See [runtime architecture](architecture/runtime-state.md).

## SLICE-003 — turn engine development preview

Round Start, clockwise Agent/early Reveal scheduling, incremental legal decisions,
typed effect resolution, optional payment/order/deployment, interleaved card
acquisition, preliminary strength, private observations and versioned replay are
implemented for a documented subset. Sourced board/card examples and test-only
synthetic effect fixtures verify the engine through the Combat boundary. The CLI
executes a partial sourced sequence with synthetic inventories and stops explicitly
at missing card evidence. See [coverage](implemented-mechanics.md),
[architecture](architecture/turn-engine.md) and [benchmarks](benchmarks/slice-003.md).

**Not a complete authentic game or RL-ready environment.** The existing content
gate remains: audit full card/Conflict inventories, obscured starting effects,
remaining leaders and passive/Signet/setup contracts. No missing effect receives a
substitute. The original+Ix+Immortality complete-game target remains unchanged.

## SLICE-003.5 — ability and decision architecture audit

Documentation-only assessment of actual effect/decision capabilities, KH, Combat
Intrigue, Graft and representative expansion interactions. The
[audit](architecture/ability-decision-audit.md), [ADR 0006](adr/0006-ability-and-decision-execution.md),
and [self-contained handoff](handoffs/SLICE-003.5-result.md) prepare the next slices.
That documentation-only slice added no gameplay support. It established the
causal continuation, role and window prerequisites subsequently implemented by
SLICE-004 below.

## SLICE-004 — supported lifecycle implemented; final review pending

Causal frames, explicit roles, phase-aware windows, Combat play/pass/retreat,
tie rewards, faction prerequisites, PostWin interleaving, control/defense, Makers,
typed Agent Recall, recurring rounds and immutable Endgame results now use one
`TurnEngine`. A development-only ten-Conflict synthetic game completes through
the existing episode runner and verifies replay. Every accepted prefix, invalid
actions, privacy and concurrent isolation are integration-tested; synthetic
measurements and retained gates are recorded in the
[handoff](handoffs/SLICE-004-result.md) and [benchmarks](benchmarks/slice-004.md).
Controller task/final reviews remain pending.

Authentic initialization remains blocked by missing catalog, leader and Conflict
evidence. Plot/Alliance-loss/tied shared draws/contested Mentat/composite gates
remain. No authentic complete-game, expansion gameplay or RL-readiness claim.

## SLICE-004.5 — card catalog and ability coverage audit

After SLICE-004 establishes actual timing/reward/result contracts, reconcile sourced
inventories and classify each executable box, ability capability and interaction.
Keep source/evidence, metadata completeness and implementation status separate.
Reuse SLICE-003.5's API map/capability matrix/case studies; audit the implementation
delta and catalog coverage instead of repeating the architecture audit. Follow the
[preparation plan](architecture/ability-execution-design.md#slice-0045-preparation-plan-catalog-and-ability-coverage-audit)
for fields, gaps, batching and exclusions. No comprehensive catalog audit is included
in SLICE-003.5.

## Complete-game target

Extend the original+Ix+Immortality fixture through all phases, board actions,
conflicts, factions, deck building, card/leader effects, scoring and terminal rules.
Track per-effect verification and combination coverage; full gameplay readiness
requires a complete selected inventory and all conditional rules. Preserve the
Toy suite throughout. Use seeded replay and public/private observation tests as
gates, then complete autonomous random-baseline episodes.

## Subsequent rules slices

Build Uprising base and then its Ix/Bloodlines/optional-module combinations with
separate sourced coverage. Add official solo and two-player House Hagal/Rival
behavior as dedicated formats, followed by six-player teams and their own setup,
turn ownership and observations. Add Epic, Go to 11 and further official variants
as verified execution paths. Custom leader drafts remain optional setup policies;
interactive drafting needs pregame observation/legal-action and privacy tests.
Unresolved combinations in the matrix remain excluded until evidence resolves them.

## Measure and connect Python

Run ADR 0001's representative batch interop comparison before selecting a transport.
Implement the PettingZoo AEC adapter and conformance checks with observations,
action masks, rewards and termination/truncation. Benchmark real transition,
cloning, encoding, batch-copy and worker-scaling workloads before optimizing.

## Reproducible self-play and learning

Record revision, fingerprint, schemas, runtime/tool locks, independent seeds,
actions, checkpoints and evaluation partitions. Add rollout/evaluation manifests
before choosing external tracking infrastructure. Later introduce uv-locked PyTorch
and an established multi-agent learning implementation, with held-out baselines
and uncertainty estimates. PPO, MCTS and neural training remain outside current scope.
