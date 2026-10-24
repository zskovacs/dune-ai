# Toolchains and tasks

`mise.toml` is the primary toolchain and task definition. It selects .NET SDK **10.0.402**, CPython **3.12.15**, and uv
**0.13.5**. These versions are pinned
through official mise core backends for .NET/Python and the registry's aqua uv
backend. `mise.lock` records the resolved versions and available download metadata
for Linux x86-64. `global.json` pins the same SDK without roll-forward;
`.python-version` pins the same Python patch. Neither file selects an alternative
to mise. Never depend on the machine's globally installed SDK or Python.

mise itself must already be available. Configuration is verified with mise
2026.8.5; no global settings are changed by this project. See the
[mise lockfile documentation](https://mise.jdx.dev/dev-tools/mise-lock.html) for
backend-specific guarantees: the .NET core entry records a version, while archive
backends can also record URLs/checksums. This is not a promise of hermetic OS inputs.

## Daily commands

| Task                       | Responsibility                                               |
|----------------------------|--------------------------------------------------------------|
| `mise run setup`           | Locked NuGet restore and `uv sync --locked` into `.venv`     |
| `mise run toolchain-check` | Reject tool declaration/executable drift and stale locks     |
| `mise run build`           | Release build of the whole .NET solution, analyzers enabled  |
| `mise run test`            | xUnit and pytest suites                                      |
| `mise run test:engine`     | Focused xUnit engine suite                                   |
| `mise run test:simulation` | Focused xUnit simulation suite                               |
| `mise run test:dotnet`     | All C# tests                                                 |
| `mise run test:python`     | Python boundary tests                                        |
| `mise run lint`            | Ruff static checks; C# analyzers also run during build/check |
| `mise run format`          | Apply C# and Python formatting                               |
| `mise run format-check`    | Verify formatting without modifying source                   |
| `mise run benchmark`       | BenchmarkDotNet baseline in Release                          |
| `mise run smoke`           | Complete autonomous two-player toy game                      |
| `mise run check`           | Tool/lock checks, build, tests, lint and formatting          |

Tasks use native dependencies and propagate exit codes. A single invocation
deduplicates shared prerequisites. Build/test/format use the same solution and
Release configuration. Benchmarks are separate from `check`: statistical timing
is hardware-sensitive and not an ordinary pass/fail test.

For a fast benchmark harness check:

```sh
mise run benchmark -- --job Dry --filter '*'
```

Extra benchmark arguments are forwarded by mise unchanged. A dry job runs one
measurement to verify execution; use the default task for normal measurement.
Reports are written to ignored `BenchmarkDotNet.Artifacts/`. Process-priority
warnings in an unprivileged session do not require elevated setup.

## Python ownership

uv owns Python dependencies in `pyproject.toml`/`uv.lock`; mise owns the executable
and interpreter. `UV_PYTHON` names mise's exact Python executable.
`UV_PYTHON_DOWNLOADS=never` and `python-preference=only-system` prevent uv from
installing an interpreter. In uv terminology, mise's Python is a "system"
interpreter because uv did not install it. The project packages install in
`.venv`; `uv run --locked` provides the environment without activation. See uv's
[interpreter policy](https://docs.astral.sh/uv/concepts/python-versions/).

The default dependencies are pytest/Ruff and the package build backend. Torch,
CUDA, PettingZoo, OpenSpiel and pythonnet are evaluated future dependencies;
initialization installs no GPU stack or unused bridge.

## Intentional updates

1. Update the exact tool version in `mise.toml`; synchronize `global.json` or
   `.python-version` when appropriate. Run `mise install` and `mise lock` to update
   concrete entries (add platform entries before supporting another platform).
2. For Python package edits, run `mise exec -- uv lock`, then `mise run setup`.
3. For NuGet package edits, run
   `mise exec -- dotnet restore dune-ai.slnx --force-evaluate`, inspect all
   `packages.lock.json` changes, then `mise run setup` in locked mode.
4. Run `mise run check` and `mise run smoke`, inspect source/lock changes together,
   and keep replay compatibility explicit if the runtime or rules changed.

CI uses [the official mise action](https://github.com/jdx/mise-action) with locked
installation and the same setup/check/smoke tasks. It has no separate SDK/Python
version matrix and requires no NVIDIA GPU. A remote CI run is possible only after
the owner connects/pushes the repository; initialization does not do that.
