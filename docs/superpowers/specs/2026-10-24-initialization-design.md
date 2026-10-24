# Dune AI initialization design

The user request is the authoritative specification and authorizes execution now.
Build a native Linux monorepo for a future autonomous player for Dune: Imperium –
Uprising (base game). No Dune mechanics or training algorithms are implemented.

## Deliverables and boundaries

- .NET 10 libraries: `Dune.Engine` owns generic game contracts and a separate
  `Toy` namespace; `Dune.Simulation` owns observation-only agents and serial/batch
  orchestration. `Dune.Cli` runs a complete two-player toy episode.
- The toy game removes 1–3 tokens from a seeded pile; the last mover wins. Private
  per-player markers demonstrate observation redaction without affecting moves.
  These are invented toy rules, never Dune rules.
- Reset, observation, legal actions, transition, terminal result and replay APIs.
  Replay records seed, immutable configuration, engine/rules version and actions.
  Use seeded `System.Random`, scoped to the pinned runtime and algorithm version;
  separate environment and agent RNG streams. No shared mutable games or agents
  across parallel episodes; batch output order is independent of scheduling.
- Python 3.12 package at `training/`, with future agents/environments/experiments.
  No duplicate Python toy engine. Test an action-mask utility for the future
  PettingZoo AEC adapter; document observation, reward, termination and truncation.
- Interop ADR compares pythonnet/CoreCLR, NativeAOT C ABI, and batched IPC. Select
  a benchmark spike next, defer production transport. No per-action HTTP service.
- mise manages exact .NET/Python/uv versions and lockfile; uv manages packages and
  `.venv`, cannot download Python, and receives mise's exact interpreter path.
  PyTorch is future optional work; no GPU packages needed to initialize or run CI.
- xUnit tests, pytest/Ruff, BenchmarkDotNet baseline, unified mise tasks and
  official mise GitHub Action. .NET package lockfiles and uv.lock are checked in.
- README, architecture, roadmap, rule-source template, Linux/tooling guides and
  AGENTS policy distinguish current behavior from future work.

## Acceptance

Actually run `mise install`, setup, build/test/lint/format checks, smoke and a
BenchmarkDotNet dry job. Check rejects stale dependency locks, SDK/interpreter
version drift, illegal/out-of-turn actions and terminal misuse; replay and
parallel schedule independence have behavioral tests. Keep source and lockfiles
ready to commit; preserve the existing unborn Git repository and do not push.

## Builder choices

Work in the requested empty checkout: it has no HEAD for a useful worktree.
Use built-in .NET and Python facilities; do not add a generic framework, DI
container, bridge package, database, HTTP server or GPU runtime in this slice.
