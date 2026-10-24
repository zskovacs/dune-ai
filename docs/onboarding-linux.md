# Native Linux onboarding

Use a native Linux x86-64 system with Git, a working HTTPS connection and mise.
The .NET/Python binary distributions need their normal Linux runtime libraries;
on a minimal distribution, follow
Microsoft's [Linux dependency guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
if a loader names a missing shared library. Do not install system packages as an
unrequested part of repository setup. The initial checks require no GPU or CUDA.

If mise is absent, follow its [official installation instructions](https://mise.jdx.dev/getting-started.html).
Review the chosen installation method yourself; this repository does not install
mise. Put it on PATH. Shell activation is optional for `mise run`.

From the cloned repository:

```sh
mise install
mise run setup
mise run check
mise run smoke
```

mise may ask you to trust the checked-out configuration on first use. Review
`mise.toml`, then run `mise trust` if prompted. This is mise's normal trust gate;
no shell activation or virtual-environment activation is required. Tool downloads
are managed by mise in user-owned directories, and uv creates `.venv` locally.
`mise install --locked` is useful for explicitly enforcing the tool lock in a
fresh environment. `setup` enforces the Python and NuGet dependency locks.

```sh
mise exec -- dotnet --version
mise exec -- python --version
mise exec -- uv --version
mise run toolchain-check
```

Use `mise run format` to fix formatting. See [toolchains](toolchains.md) for all
tasks and deliberate version updates. Use `.venv/bin/python` in an IDE and the
mise-selected .NET SDK for C# tooling. Do not replace the repo tasks with global
interpreters or `pip install` commands.

The target i9-13900 / 64 GB / RTX 4070 Ti is a future measurement target. No
GPU-specific optimization is assumed. Installing an NVIDIA driver and selecting
a compatible PyTorch CUDA wheel are later ML-milestone work; neither is necessary
to build the engine or run the toy game.

For failed HTTPS downloads, check the error's host, network and certificates.
For failed NuGet restores, the repo's `NuGet.Config` deliberately clears inherited
feeds and uses the public official source. For an interpreter mismatch, run the
version checks above and reinstall the declared tools; do not let uv download a
replacement interpreter. For stale locks, update them intentionally as documented,
review their diff, and rerun check.
