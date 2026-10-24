# Dune AI — Codex workflow overrides

This file overrides only the global Codex / Superpowers **test-driven development (TDD)** defaults that are not appropriate for this repository.

Before performing repository work, read the sibling `./AGENTS.md` and treat it as the primary source of project architecture, tooling, quality standards, and verification requirements.

## Disabled Superpowers TDD defaults

For work in this repository:

- Do **not** automatically invoke or follow the Superpowers `test-driven-development` skill solely because a feature, bug fix, refactor, or game rule is being implemented.
- The mandatory **red → green → refactor** cycle does not apply by default.
- Do **not** require a failing test before writing production code, or require proof that a test failed before an implementation change.
- Do **not** add tests mechanically for every method, class, or code change merely to satisfy a global TDD workflow.
- Do **not** treat the absence of newly written tests as a failure on its own.

This override changes **test sequencing and automatic TDD triggers**, not the project's correctness or verification expectations.

## Testing and verification in this project

- Follow test requirements explicitly defined by the user, the current task's acceptance criteria, or `./AGENTS.md`.
- Write **focused, direct tests** when they are appropriate for the change; they may be written before, alongside, or after implementation.
- For the Dune engine, particularly valuable test targets include rule correctness, legal actions, state transitions, turn progression, seeded randomness, deterministic replay, and isolation of hidden player information. Choose tests based on actual scope and risk, not a blanket TDD mandate.
- Use **xUnit** for .NET tests and **pytest** for Python tests. Reuse established libraries instead of creating custom test infrastructure.
- Use **BenchmarkDotNet** and representative measurements for performance-sensitive decisions; unit tests are not a substitute for benchmarks.
- Run the relevant existing checks using the repository's **mise** tasks (for example, `mise run test` or `mise run check`), or explain what could not be run. Never claim verification that did not happen.

## Still applicable

Unless `./AGENTS.md` or an explicit user instruction says otherwise:

- Other Superpowers capabilities and workflows (such as brainstorming, design, planning, debugging, and review) remain available and are not disabled by this file.
- Global safety, scope-control, tool-use, and truthful-reporting requirements remain applicable.
- The project's library-first policy, reproducibility rules, and benchmark-driven performance policy remain applicable.
- If the user explicitly requests TDD for a particular task, follow that request for that task.

**Scope:** Disable mandatory Superpowers TDD only; do not disable testing, verification, or Superpowers as a whole.
