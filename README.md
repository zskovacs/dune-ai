# Dune AI

A local research monorepo for future autonomous play of **Dune: Imperium** and
**Dune: Imperium – Uprising**. The first complete-game target is original Imperium
with Rise of Ix and Immortality, competitive 3–4 players. The architecture targets
deterministic headless simulation, parallel self-play and Python/PyTorch learning.

**Current scope:** SLICE-001 composition, SLICE-002 runtime/setup and SLICE-003
incremental turn execution, legal actions, effects and deterministic replay.
Authentic Dune initialization remains blocked by incomplete card/leader data.
The turn preview uses a visibly synthetic inventory and stops on unsupported
content; tests exercise complete Player Turns with test-only synthetic effects.
The two-player **toy token game** is executable.
No Dune configuration is playable. No Python/engine bridge, neural model, PPO or
MCTS exists. The toy game uses invented rules.

## Native Linux quick start

With Git and [mise](https://mise.jdx.dev/getting-started.html) already on PATH,
run from this checkout:

```sh
mise install
mise run setup
mise run check
mise run smoke
```

If mise prompts for configuration trust, review `mise.toml` and run `mise trust`.
No manual virtual-environment activation, global .NET/Python, GPU or CUDA install
is required. mise selects **.NET SDK 10.0.402**, **Python 3.12.15** and **uv
0.13.5**; uv installs the locked Python packages in `.venv`. `mise.lock`,
`uv.lock` and NuGet package locks are versioned inputs ready to commit.

See [native Linux onboarding](docs/onboarding-linux.md) for prerequisites and
troubleshooting, and [toolchains/tasks](docs/toolchains.md) for dependency updates.
CI uses the same mise tasks through the official `jdx/mise-action`.

## Developer tasks

| Command                 | Purpose                                                           |
|-------------------------|-------------------------------------------------------------------|
| `mise run setup`        | Restore .NET and sync Python dependencies with locks enforced     |
| `mise run build`        | Build all six .NET projects in Release                            |
| `mise run test`         | Run xUnit and pytest tests                                        |
| `mise run lint`         | Ruff; C# analyzers also run during build/check                    |
| `mise run format`       | Apply C# and Python formatting                                    |
| `mise run format-check` | Check formatting without editing source                           |
| `mise run benchmark`    | Run BenchmarkDotNet baseline                                      |
| `mise run smoke`        | Run a seeded headless toy game with two random agents             |
| `mise run preview:turns` | Run a partial Dune turn preview with explicit support diagnostics |
| `mise run check`        | Validate tool/lock consistency, build, tests, lint and formatting |

Focused tasks: `test:engine`, `test:simulation`, `test:dotnet`, `test:python`,
and `toolchain-check`. Use `mise tasks` to list task descriptions. Benchmark
timings are separate from correctness checks; see the performance notes in the
[architecture](docs/architecture.md).
For a quick harness check, use `mise run benchmark -- --job Dry --filter '*'`.
Dry timings are not a reliable performance comparison.

## Repository layout

```text
.
├── AGENTS.md, AGENTS.override.md, README.md
├── mise.toml, mise.lock, global.json, .python-version
├── dune-ai.slnx, Directory.Build.props, NuGet.Config
├── pyproject.toml, uv.lock
├── .editorconfig, .gitattributes, .gitignore
├── src/
│   ├── Dune.Engine/             # contracts, private state, isolated Toy game/replay
│   ├── Dune.Simulation/         # agents and episode/batch runners
│   └── Dune.Cli/                # headless executable
├── tests/
│   ├── Dune.Engine.Tests/
│   └── Dune.Simulation.Tests/
├── training/
│   ├── agents/
│   ├── environments/            # action-mask utility; future AEC adapter
│   ├── experiments/
│   └── tests/
├── benchmarks/Dune.Benchmarks/
├── scripts/                    # toolchain consistency checks
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   ├── rules-sources.md
│   ├── onboarding-linux.md
│   ├── toolchains.md
│   ├── adr/0001-csharp-python-interop.md
│   └── superpowers/            # initialization design and implementation plan
├── .agents/skills/
└── .github/workflows/ci.yml
```

## Architecture and next work

The engine depends only on .NET. Simulation agents see player observations and
legal actions. CLI and benchmarks compose these libraries. Python packages are
independently tested; the future environment will use an established multi-agent
interface and a measured batched boundary. [ADR 0001](docs/adr/0001-csharp-python-interop.md)
compares pythonnet, NativeAOT and IPC without prematurely selecting a transport.

[SLICE-001 composition](docs/architecture/ruleset-composition.md) provides typed
configuration/presets, sourced module replacements, representative content metadata
and stable fingerprints. See the [rules index](docs/rules-index.md),
[compatibility matrix](docs/compatibility-matrix.md) and
[content-source assessment](docs/content/sources.md). Validated composition is
separate from gameplay readiness; all Dune results remain nonplayable.

See the [runtime setup architecture](docs/architecture/runtime-state.md),
[turn engine](docs/architecture/turn-engine.md) and
[implemented mechanics matrix](docs/implemented-mechanics.md). Run
`mise run preview:turns -- --seed 42 --players 4` for the partial development
scenario and replay. A complete verified inventory and leader/card effects remain
required before authentic play. The eventual
complete-game target remains **Imperium + Ix + Immortality**.
See the [roadmap](docs/roadmap.md) and [rule ledger](docs/rules-sources.md).
Do not commit copyrighted rulebooks/card art, datasets, checkpoints, secrets or
local experiment output. Contributor/agent conventions live in [AGENTS.md](AGENTS.md).
