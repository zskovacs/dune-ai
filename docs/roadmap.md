# Development roadmap

## Initialization — complete

mise/toolchain locks, .NET engine/simulation boundaries, isolated playable toy,
replay, autonomous agents, ordered parallel episodes, Python package skeleton and
BenchmarkDotNet. The invented Toy rules remain an independent regression example.

## SLICE-001 — composition foundation

Typed configuration, sourced representative module composition, explicit conflict
handling, immutable content metadata, deterministic fingerprints and configuration
presets. All seven supplied rulebooks are indexed; compatibility, content-source
assessment, leader-policy boundary and implementation status are documented.
**No Dune configuration is playable.** Supported metadata resolution is narrower
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

## SLICE-003 — audited content and Round Start

Complete source/permission decisions and verify base card identities/copy counts,
Conflict tiers, remaining leaders and any leader-specific setup instructions. Keep
real initialization blocked until that gate passes. Implement Round Start only:
reveal the top Conflict, draw five cards per player, advance round/phase and create
the first decision owner. Add privacy/reshuffle/round transition and versioned replay
tests. Then scope the first bounded Agent action with sourced costs/effects and
legal-action/observation contracts; do not combine all board/card effects at once.
The original+Ix+Immortality complete-game target remains unchanged.

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
