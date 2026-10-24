# Dune AI Initialization Implementation Plan

> **For agentic workers:** Use Superpowers task-by-task implementation and review.

**Goal:** Deliver the executable, reproducible repository requested by the user. **Architecture:** Dependency-free game
engine → simulation → CLI/benchmarks.
Python remains independently testable until a measured interop spike chooses a bridge. **Tech stack:** .NET 10, Python
3.12, mise, uv, xUnit, pytest, Ruff, BenchmarkDotNet. **Spec:**
`docs/superpowers/specs/2026-10-24-initialization-design.md`

## Global constraints

- Native Linux; GPU-free checks. No real Dune rules, training, UI, HTTP or database.
- Exact mise tool pins, synchronized SDK/Python declarations, committed locks.
- Use mise tasks; no global configuration changes, system installs or remote writes.
- Small libraries and standard-library functionality before new infrastructure.
- Honor `AGENTS.override.md`: use focused behavioral tests without mandatory TDD
  sequencing or per-method test requirements.

## Review focus

1. Interpreter/SDK drift: verify declarations and actual executables.
2. Hidden data: agents receive observations/legal actions only; opponent data absent.
3. Invalid actions: reject before mutation, including wrong player/terminal state.
4. Replay: reset clears history; snapshots/history cannot mutate live game state.
5. Parallelism: independent episode RNG/agents; results ordered by input, cancellation.

## Task 1: Toolchain and project scaffold

**Files:** mise.toml/mise.lock, global.json, .python-version, pyproject.toml/uv.lock,
Directory.Build.props, solution, six csproj files, editor/git config, scripts, CI. **Produces:** mise setup/build/test:
dotnet/test:python/lint/format/format-check/
benchmark/smoke/check tasks; package restore lockfiles; net10.0 project references.

- [x] Pin available .NET 10 SDK, Python 3.12 patch and uv using official backends.
- [x] Generate project and dependency locks; setup uses locked restore/sync.
- [x] Build independent C# shells; make task exit codes propagate.
- [x] Add `mise run toolchain-check` to `check`: compare mise pins to global.json
  and .python-version and actual executables; run locked NuGet restore and
  `uv lock --check` so drift fails locally and in CI.
- [x] Verify mise install/setup/build and Python tooling; focused task review.

## Task 2: Engine and deterministic replay

**Files:** src/Dune.Engine, tests/Dune.Engine.Tests. **Consumes:** Task 1 project references and tasks. **Produces:**
generic game interface; isolated ToyGame; replay from versioned seed,
configuration and action history. Player IDs zero-based, active player exposed,
observation-only data separated from internal state; illegal actions throw.

- [x] Implement contracts with focused xUnit tests for the specified behaviors.
- [x] Cover seeded reset and replay, immutable observations/history, private marker
  redaction, legal action bounds, wrong turns, invalid configuration, terminal guards.
- [x] Run focused engine tests, inspect diff and obtain task review; resolve findings.

## Task 3: Autonomous simulation, CLI and benchmark

**Files:** src/Dune.Simulation, src/Dune.Cli, tests/Dune.Simulation.Tests,
benchmarks/Dune.Benchmarks. **Consumes:** Task 2 game contract and toy implementation. **Produces:** observation-only
agent interface; random baseline; serial and ordered
batch runners; complete headless CLI episode and full-episode baseline benchmark.

- [x] Agent receives only player observation and legal actions; independent seeded
  random baseline. Assert repeated runs and parallel degrees 1/4 match action traces.
- [x] Bound runaway games, propagate cancellation, validate agents and CLI arguments.
- [x] Run mise test:dotnet, build, smoke, format and BenchmarkDotNet dry baseline;
  inspect diff and obtain focused review; fix Important/Critical findings.

## Task 4: Python boundary and project documentation

**Files:** training package/tests; README; AGENTS.md; docs architecture, roadmap,
rules-sources, onboarding, toolchains, ADR; .agents/skills placeholder. **Produces:** tested validation of discrete
legal-action masks, no Python engine;
documented future AEC mapping and measured interop decision criteria.

- [x] Implement a standard-library mask utility and pytest tests for empty,
  duplicate, invalid and out-of-range action IDs.
- [x] Document library/license rationale, runtime-scoped determinism, separate RNG
  streams, batch ownership, private data, replay schema and performance baseline.
- [x] Explain PyTorch/PettingZoo/OpenSpiel evaluation and deferred GPU/bridge install.
- [x] Add verified primary source links and mechanics evidence template.
- [x] Run mise check and smoke, review complete diff, final whole-branch review,
  fix findings, rerun affected checks and report actual evidence and limitations.
