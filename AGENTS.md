# Dune AI agent instructions

## Scope and architecture

This is a native Linux monorepo for a future self-play AI for **Dune: Imperium –
Uprising**, initially the base game without expansions. The only playable game
currently implemented is a deliberately separate **toy token game**. Never label
toy rules as Dune rules. No training algorithm or Python/engine bridge exists yet.

- `src/Dune.Engine`: game contracts, internal state, observations, transitions and
  replay. No dependencies on Python, simulation agents or ML libraries.
- `src/Dune.Simulation`: agents and headless episode/batch orchestration.
- `src/Dune.Cli`: small executable composition root.
- `training`: Python agents/environments/experiments; no duplicate rules engine.
- `tests`, `training/tests`: xUnit and pytest behavioral tests.
- `benchmarks`: BenchmarkDotNet; `docs`: architecture, roadmap, sources and ADRs.

Keep changes small and independently testable. Follow existing types and naming.
Use explicit ownership and simple constructors; introduce DI only for a concrete
need. No UI, servers, databases, generic plugin framework or unrelated refactoring.

## Reproducible developer interface

**Use the repository's mise tasks by default.** Prefer `mise run test` over
reconstructing commands. Read `README.md` and `docs/toolchains.md` first.

```sh
mise install
mise run setup
mise run check
mise run smoke
```

mise owns .NET SDK, Python interpreter and uv executable. uv owns Python packages
and `.venv`. Never use global dotnet/Python, pip, Poetry or Conda for this repo.
Never install/update mise or system packages without authorization. Do not change
global mise settings. Preserve exact pins in `mise.toml`, `mise.lock`,
`global.json`, `.python-version`; uv must receive mise's interpreter and must not
download another Python. Commit `uv.lock` and NuGet package lockfiles. Refresh
locks deliberately, inspect changes, then run setup/check. CI uses the same tasks.

## Library-first decisions

Before writing a nontrivial generic component, check established maintained
libraries. Evaluate compatibility, license, maintenance, complexity and performance.
Use standard libraries where adequate, xUnit/pytest for tests, BenchmarkDotNet for
benchmarks, and PyTorch for future neural networks. Evaluate established RL
interfaces (especially PettingZoo AEC and OpenSpiel) before adding a custom one.
Document a custom alternative's reason in an ADR. Do not add dependencies that
merely replace simple built-in behavior, or custom test/logging/config frameworks.

## Performance

Performance-critical paths may warrant specialized implementations **after
measurement**. Establish a representative baseline; compare throughput, latency,
allocations, GC, memory use and parallel scaling where relevant. Benchmark state
transitions, legal actions, cloning/search expansion, interop and observation
batches before choosing abstractions or custom code. Avoid reflection, redundant
serialization and allocations in measured hot paths. Do not assume library versus
custom performance. Prefer the simpler established choice when differences are
negligible. Record hardware, runtime, workload, commands, results and trade-offs.
Without evidence, document a conservative assumption; make no speed claims.
Dry benchmarks verify execution only. Optimize without changing correctness,
hidden-information boundaries or reproducibility.

## Mechanics and information security

Every real rule must link to a verified Uprising source with edition/version,
page/section and a behavioral test in `docs/rules-sources.md`. Document assumptions
explicitly, keep unresolved mechanics blocked or excluded, and never invent a
rule/card effect silently. Do not borrow original Imperium/expansion rules without
verification that they apply. Never redistribute rulebooks/card art without rights.

The engine owns all randomness and complete hidden state. Agents receive only
their observation and permitted legal actions; never pass a game/state/replay
reference. Public observations and legal actions must not reveal opponents'
private cards, RNG state or privileged history, including through derived fields.
Test redaction and the failure paths, not only the happy path.

## Determinism and concurrency

No clocks, global random instances, unordered iteration affecting decisions, or
shared mutable episode state. Seed environment and each agent independently.
Agent constructor/factory seeds must depend only on independent public policy
seeds and public episode identities; environment seeds remain privileged.
Record engine/rules version, seed, config, ordered actions and runtime metadata
for replay; a seed alone cannot reproduce a changed policy. `System.Random`
reproducibility is scoped to the pinned runtime/implementation; do not promise
cross-version replay. Keep older replay versions explicit, never silently reinterpret.
Snapshots/history returned to callers must not mutate live state. Validate before
mutation. One game and agent set per episode; results must not depend on scheduling.
Future chance/subdecision handling must preserve private information and action order.

## Tests and handoff

Use meaningful xUnit/pytest tests for determinism, reset/replay, illegal actions,
terminal guards, privacy, batch schedule independence and cancellation. Do not
test mocks or mirror implementation details. Run the smallest relevant task while
working, then `mise run check` and `mise run smoke` before final handoff. Changes
to benchmark code need an executable dry job; performance claims need proper runs.
Inspect the final diff and report actual commands/results and limitations. Never
claim an unexecuted check passed. Use the configured final branch reviewer for
nontrivial branch work. Do not push or publish without authorization.

Update architecture/ADRs for boundary or dependency changes and roadmap status for
new vertical slices. Keep datasets, checkpoints, secrets, `.venv`, build products
and local experiment output out of Git. Store experiment manifests (seeds,
revision, configs, tool/dependency versions and metrics) when that feature arrives.
