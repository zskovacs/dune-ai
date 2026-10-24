# Structured Logging and JSON Implementation Plan

> **For agentic workers:** Execute and review each bounded task using the configured
> custom agents. Honor AGENTS.override.md; no mandatory failing-test proof.

**Goal:** Add usable persistent/console diagnostics and establish measured JSON policy. **Architecture:**
ILogger/Serilog in CLI orchestration; dependency-free engine hot
paths and unchanged deterministic serialization contracts. **Stack:** .NET 10, Serilog, xUnit, BenchmarkDotNet, existing
mise tasks. **Spec:** `docs/superpowers/specs/2026-10-24-logging-json-design.md`.

## Constraints and review focus

- No game-rule changes, silent hash/replay changes, or runtime JSON.NET assumption.
- No hidden game state/seeds in routine logs; no replay capture for disabled logs.
- No async queue by default; verify flush and visible file initialization failures.
- Keep stdout machine/user output separate from operational stderr diagnostics.
- Preserve preview's expected partial-success behavior and error exit semantics.
- New packages must be explicit and locked; use mise tasks and existing conventions.

## Task 1: Measure JSON choices and compatibility

**Ownership:** `benchmarks/Dune.Benchmarks/JsonBenchmarks.cs`, benchmark csproj/lock,
`docs/benchmarks/logging-json.md` initial JSON findings. **Inputs:** existing ConfigurationJson.Options,
OriginalImperiumFixture,
representative ContentCatalog, prepared trusted snapshot JSON. **Output:** repeatable BDN comparison with cached
settings/contract resolver and
actual same payloads. Newtonsoft.Json remains isolated to benchmarks unless a
compatible measured runtime migration is justified by controller review.

- [x] Compare configuration serialize/deserialize, representative catalog and
  trusted-snapshot parsing; prepare fixtures outside timing and report allocations.
- [x] Use safe explicit Newtonsoft settings and account for enum/naming/default
  differences; document unsupported STJ-specific polymorphism and canonical hashes.
- [x] Run `mise run benchmark -- --job Short --filter '*JsonBenchmarks*'` and
  focused existing engine serialization tests via mise; record real results.
- [x] Inspect focused diff and obtain task review; resolve Important/Critical items.

## Task 2: Structured CLI diagnostics

**Ownership:** `src/Dune.Cli/**`, optional `tests/Dune.Cli.Tests/**`, solution and
test-task wiring/package locks as necessary. No Engine/Simulation behavior edits. **Inputs:** existing Program and
TurnPreviewCommand; typed TurnDiagnostic/StepResult. **Output:** source-generated ILogger events, Serilog console/file
configuration,
central command error boundary and documented config environment variables.

- [x] Preserve stdout command results/help and return codes; attach RunId/Command
  to logs, stderr sink and rolling JSONL file (bounded retention/size).
- [x] Record start/end/failure, replay failures and expected typed diagnostics at
  appropriate levels. Exception includes stack; cancellation is not an engine error.
- [x] Defaults: Information, `artifacts/logs`, daily rolling and size/count limits.
  `DUNE_LOG_LEVEL` and `DUNE_LOG_DIRECTORY` override them. Initialization failure
  must be visible with nonzero exit. Dispose/flush before returning.
- [x] Add focused xUnit tests for real sinks/temp files, level filtering, exceptions,
  diagnostic status/code, privacy, output separation and failure behavior. Prefer
  a conventional small CLI test project over a custom test framework.
- [x] Run focused mise tests, smoke and preview; inspect diff and obtain review.

## Task 3: Policy, performance verification and handoff

**Ownership:** AGENTS.md, README, docs/toolchains/architecture, ADR and benchmark report;
focused benchmark/test refinements only if evidence requires them.

- [x] Record logging ownership, levels, stable templates, source generation,
  IsEnabled guards for expensive arguments, persistence and privacy in AGENTS.md.
- [x] Record JSON default/evidence-based exceptions, reused read-only options,
  strict parsing and explicit canonical/replay format versioning in AGENTS.md/ADR.
- [x] Compare existing turn-engine benchmark against the documented current
  baseline; report that no engine/runner instrumentation was introduced, without
  claiming statistical equivalence from noisy short measurements.
- [x] Run mise check, smoke, preview, format checks and actual benchmark commands;
  inspect final diff, final branch review, fix findings and report evidence.
