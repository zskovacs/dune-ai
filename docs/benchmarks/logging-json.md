# JSON serializer comparison (2026-10-24)

## Scope and method

Run `mise run benchmark -- --job Short --filter '*JsonBenchmarks*'` after
`mise exec -- dotnet restore benchmarks/Dune.Benchmarks --force-evaluate`. BenchmarkDotNet 0.15.8 ShortRun used one
launch, three warmups, and three measured iterations. Host: Ubuntu 26.04.1, Intel Core i9-13900KF (24 physical/32
logical cores), .NET SDK 10.0.402 and runtime 10.0.13, X64 RyuJIT x86-64-v3, concurrent workstation GC. Newtonsoft.Json
13.0.4 is a direct benchmark-only dependency. The corrected run's raw log and CSV are preserved in ignored
`BenchmarkDotNet.Artifacts/logging-json-short/`.

`GlobalSetup` prepares an original-Imperium fixture configuration, the representative content catalog, and a trusted
setup snapshot JSON string outside timing. Both serializers operate on the same configuration object, catalog object, or
input JSON per pair. System.Text.Json (STJ) uses the engine's shared read-only `ConfigurationJson.Options`; JSON.NET
uses a cached camel-case contract resolver, string-only enum converter, unknown-member rejection, and
`TypeNameHandling.None`. Setup separately asserts semantic JSON equality, exact byte equality, and round-trip
configuration content for both serialization workloads. These two prepared samples produce **exactly equal output**
(configuration: 371 characters; catalog: 14,945 characters). That is sample evidence, not a guarantee that every
canonical payload or future schema has identical bytes.

The snapshot pair parses the same trusted canonical JSON to a DOM; STJ disposes its `JsonDocument` in each operation.
STJ rents pooled buffers here, so its reported managed allocation is not the full temporary memory footprint. This does
not compare typed snapshot deserialization or engine snapshot creation/hashing. This JSON contains hidden cards and
seeds and is used only within the benchmark; no snapshot payload is stored in this report.

## Corrected ShortRun results

Times are per operation. Error is BenchmarkDotNet's half-width of the 99.9% confidence interval; allocations are managed
bytes per operation.

| Workload                         |  STJ mean ± error | STJ allocated | JSON.NET mean ± error | JSON.NET allocated |
|----------------------------------|------------------:|--------------:|----------------------:|-------------------:|
| Configuration serialize          |   635.9 ± 20.7 ns |       1,112 B |    1,454.2 ± 126.5 ns |            3,720 B |
| Configuration deserialize        | 1,414.8 ± 26.3 ns |       1,624 B |    2,568.7 ± 141.8 ns |            4,897 B |
| Representative catalog serialize | 17,813 ± 4,267 ns |      32,888 B |     39,841 ± 2,916 ns |           78,709 B |
| Trusted snapshot DOM parse       | 37,056 ± 2,144 ns |          72 B |    224,504 ± 2,834 ns |          808,016 B |

The setup asserts semantic and exact byte equality separately for the two serialized payloads.
Configuration deserialization consumes the same STJ-produced input in both methods. The catalog serialization
measurement is exploratory: production `ContentCatalog.LoadRepresentative()` deserializes embedded arrays, so this does
not measure its cold-load path. Snapshot DOM parsing likewise is not a measured engine hot path. Three iterations in one
launch on one host establish a local comparison, not a universal speed ranking or a statistical guarantee for other
payloads.

## Compatibility and decision

`ConfigurationJson` currently rejects unknown JSON members and integer enum values. The JSON.NET benchmark settings
approximate those rules, but serializer defaults still differ (including property matching, constructors/defaults,
escaping, and attribute support); setup validates only the prepared fixtures. STJ's `[JsonDerivedType]` tags (`$action`
and `$effect`) on turn actions/effects are not honored by this JSON.NET setup. Enabling unsafe `TypeNameHandling` would
not reproduce the existing tagged format. Replay, trusted snapshot, configuration-identity, and ruleset-fingerprint JSON
participate in versioned canonical bytes and SHA-256 identities. Equal bytes for the two benchmark samples do not
authorize changing any of those formats or hashes.

No measured compatible workload favors JSON.NET here, and there is no justification for a runtime serializer migration
from this evidence. Keep STJ as the default and require a separate compatibility design plus representative typed/byte
tests before reconsidering a specific runtime path.

## Existing turn-engine workload verification

`mise run benchmark -- --job Short --filter '*TurnEngineBenchmarks*'` completed
all four existing workloads after the CLI logging changes. The environment is
the same as above; ShortRun used one launch, three warmups and three measured
iterations. Mutable workloads retain their existing 4,096-game batching and
setup exclusions described in [the SLICE-003 baseline](slice-003.md). Source for
Engine, Simulation, turn benchmarks and their fixtures is unchanged from the
feature's base revision `8b8eec8`.

| Workload                   | Current mean ± error | Current allocation | Earlier mean | Earlier allocation |
|----------------------------|---------------------:|-------------------:|-------------:|-------------------:|
| Legal action enumeration   |     38.63 ± 2.897 us |          100.53 KB |     36.17 us |           97.78 KB |
| Round Start                |     23.77 ± 6.109 us |           19.91 KB |     24.06 us |           19.91 KB |
| Agent placement            |     22.75 ± 4.621 us |           21.36 KB |     23.00 us |           21.22 KB |
| Optional effect resolution |     19.51 ± 1.316 us |           19.88 KB |     19.02 us |           19.88 KB |

The earlier report describes an uncommitted SLICE-003 snapshot based on revision
`8b3cdce`, so it is not an exact pre-feature A/B baseline. Both timings and some
allocation figures differ despite unchanged core source in this branch; these
differences cannot be attributed to logging from these measurements. Few samples,
ordinary process priority and short mutable iterations limit comparison. No
statistical performance-equivalence or no-regression claim follows from this run.
Raw reports and the execution log are retained under ignored
`BenchmarkDotNet.Artifacts/logging-core-short/`.

The architectural protection is explicit: no logger callbacks, logging packages
or per-action events were added to the engine/simulation hot loops. This benchmark
does not measure CLI sink I/O, formatting, startup or flush costs. Console/file
logging has real cost at command boundaries; `DUNE_LOG_LEVEL=None` disables it and
skips log-file setup. Future hot-path instrumentation requires its own disabled
and enabled-path measurements rather than assuming that logging is free.
