# Dune AI

A local research monorepo for a future autonomous player for **Dune: Imperium –
Uprising**, initially the base game without expansions. The architecture targets
deterministic headless simulation, parallel self-play and Python/PyTorch learning.

**Current scope:** repository scaffolding and an executable two-player **toy
token game**. No actual Dune rules, Python/engine bridge, neural model, PPO or MCTS
are implemented. The toy game is an architecture example with invented rules.

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

The next milestone is a **source-verified rules slice**: select the Uprising
revision/mode, populate the [rule-source ledger](docs/rules-sources.md), and add
setup plus one decision with privacy/replay tests. See the [roadmap](docs/roadmap.md).
Do not commit copyrighted rulebooks/card art, datasets, checkpoints, secrets or
local experiment output. Contributor/agent conventions live in [AGENTS.md](AGENTS.md).
