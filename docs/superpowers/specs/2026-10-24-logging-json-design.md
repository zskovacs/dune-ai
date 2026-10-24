# Structured logging and JSON evaluation

The user requests implementation of the previously discussed logging approach,
with developer console diagnostics and persistent error investigation, evaluation
of System.Text.Json versus Newtonsoft.Json, conditional replacement only when
justified, and durable conventions in AGENTS.md. Execute locally; no push is
requested for this feature. Existing game behavior and serialized identities stay
compatible. Repository AGENTS.override.md disables mandatory TDD sequencing.

## Design

- Configure Serilog at the CLI composition root behind Microsoft's ILogger API.
  Preserve user-facing stdout/help/results and exit codes. Operational events go
  to stderr and rolling JSON-line files under ignored artifacts/logs by default.
  Include run ID, command, event name/ID, level, timestamp, and exception details.
  Support documented environment overrides for minimum level and log directory.
  Bound retention and file size; flush/dispose at ordinary exit. Fail visibly if
  required persistent logging cannot initialize. No Generic Host or DI container.
- Use source-generated LoggerMessage methods. Record command start/completion,
  unexpected failure, replay mismatch and typed preview diagnostics. Expected
  unsupported preview content is distinct from an engine defect. Cancellation
  is distinct from unexpected failure. Preserve exception stack traces and
  nonzero failure exit; report an unexpected exception once at its owning boundary.
- Keep engine and simulation hot-loop source free of logging dependencies and
  per-action instrumentation in this slice. The CLI owns diagnostic orchestration;
  future simulation/library events must use ILogger abstractions and a measured
  disabled path. Do not serialize snapshots or capture replay just for a log.
  Log stable diagnostic codes/status and cheap scalar context. Privileged seeds,
  observations, hands and complete replay/state are excluded from ordinary logs.
- Use maintained stable Serilog packages; a synchronous low-volume console/file
  pipeline avoids asynchronous buffer loss and shutdown complexity. No per-step
  success events or per-episode console spam for future self-play batches.
- JSON choice is evidence-based: map current persistence/hash/polymorphic contracts,
  benchmark representative payloads with both libraries, preserve strict parsing,
  tagged action/effect handling, canonical bytes/hashes and replay behavior.
  Newtonsoft may be an explicit benchmark-only dependency while evaluating it.
  Keep System.Text.Json unless a concrete compatible benefit justifies migration.
  No broad serializer refactor or source-generation optimization without evidence.
- Update AGENTS.md, README/tooling/architecture and a short ADR/benchmark report.
  Record actual measurements with workload and limitations; never promise zero
  logging overhead or infer performance from a dry benchmark.

## Verification

Focused tests cover console/file event structure, exception persistence and flush,
log level filtering, diagnostic classification, unchanged stdout/exit behavior,
configuration errors and privacy. A small CLI xUnit project is justified if
necessary; avoid production-only test hooks or a custom logging test framework.
Run JSON BenchmarkDotNet comparisons and existing representative engine workload,
then mise check/smoke/preview. Inspect diff and obtain task/final reviews.
