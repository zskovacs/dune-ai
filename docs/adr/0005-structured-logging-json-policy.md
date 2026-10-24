# ADR 0005: Structured host diagnostics and measured JSON choices

- Date: 2026-10-24
- Status: Accepted

## Context

Developer diagnostics and later investigation of failed runs need structured,
persistent events. Engine throughput and replay compatibility are priorities.
The CLI currently prints intended user output; engine operations already return
typed diagnostics for expected Illegal/Unsupported/ContentError outcomes.

The user also requested evaluation of Newtonsoft.Json as a possible replacement
for the runtime's System.Text.Json. Serialization participates in configuration,
ruleset and snapshot identities and replay verification. Actions and effects use
explicit System.Text.Json `$action` and `$effect` polymorphic tags.

## Logging decision

Use Microsoft's ILogger abstraction and source-generated LoggerMessage methods at
CLI orchestration/error boundaries. Serilog is configured at the composition root
with a human-readable stderr sink and rolling JSON-line file sink. Keep command
results/help on stdout. Persist stable event IDs/names, run/command correlation,
diagnostic code/status and unexpected exception details. Dispose/flush on ordinary
exit and expose persistent-sink initialization failures visibly.

Configure one synchronous, low-volume pipeline with bounded retention/size and
concurrent-process-safe shared file handling. Async buffering is not selected:
it adds overflow/drop or producer-blocking decisions without a measured need.
Do not log each engine transition/legal action or capture replay merely to make
a log field. Engine and Simulation retain their current dependencies and hot-loop
source. If future library diagnostics are necessary, use ILogger abstractions,
no-op defaults and representative overhead measurements before instrumentation.

Microsoft's [library logging guidance](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/library-guidance)
recommends source generation and explicit guards before expensive argument
evaluation. The [Console sink](https://github.com/serilog/serilog-sinks-console)
documents synchronous output; the [Async sink](https://github.com/serilog/serilog-sinks-async)
documents bounded buffering, dropping/blocking and shutdown flushing. None of
these mechanisms makes logging free. Host log timestamps/run IDs never affect
engine state, random streams, action order or canonical game identities.

Ordinary events exclude raw command arguments, private seeds and automatic
destructuring of state, observations and replay history. Logs remain local
developer artifacts, excluded from Git and inaccessible through agent APIs.
Expected partial-preview support boundaries are recorded distinctly from defects;
logging does not turn expected outcomes into errors or suppress failure exits.

## JSON decision

Retain System.Text.Json in application/engine code. A representative BenchmarkDotNet
comparison found lower mean time and managed allocation for System.Text.Json in
all four tested pairs: configuration serialize/deserialize, representative catalog
serialization, and trusted-snapshot JSON parsing. See the [measurement report](../benchmarks/logging-json.md)
for exact workloads, settings, results and limitations. Newtonsoft.Json 13.0.4 is
an explicit benchmark-only dependency, not a runtime migration.

This is a local workload decision, not a universal claim about either library.
Microsoft's [migration guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft)
documents differences in strictness, escaping and features. Newtonsoft's
[performance guidance](https://www.newtonsoft.com/json/help/html/Performance.htm)
recommends reusing contract resolvers; the comparison uses cached settings.

Reuse existing read-only ConfigurationJson.Options and preserve strict parsing,
string enum validation, polymorphic allowlists and all canonical hash/replay
contracts. Semantic equality does not guarantee identical bytes. Future changes
need measured compatible benefits or a concrete unsupported feature, deliberate
format/version decisions, and golden-hash, round-trip and privacy verification.
Source-generated JSON serialization remains a possible measured follow-up for
stable payloads, not an automatic refactor in this feature.
