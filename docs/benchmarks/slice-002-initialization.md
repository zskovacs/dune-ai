# SLICE-002 initialization baseline

Date: 2026-10-24. Source: branch `slice-002-base-setup`, SLICE-002 working changes
based on foundation commit `7398841`. No optimization or speed comparison is claimed.

## Workload

`GameInitializationBenchmarks.InitializeResolvedFixture`, parameterized for 3/4
players. The internal synthetic catalog uses authentic base setup quantities:
189 physical cards, eight distinct synthetic leaders, 22 real board-space IDs,
ten starting cards per player, Reserve 8/10/6, row five and Conflicts 1/5/4.
Synthetic card identities have **no official effects or authenticity claim**.

Each operation allocates fresh mutable state and instances, validates/binds the
configuration, assigns leaders, shuffles/selects decks, chooses first player and
builds read-only zone/state views. Main seed 42; leader seed 17. The immutable
resolved definition is reused. GlobalSetup composes it and warms the initializer's
static metadata validation cache. Cold composition, static fingerprint/content
validation, observations, snapshot serialization and gameplay are excluded. There
is no object pool. This models repeated simulations from an already-resolved
ruleset, not total application startup.

## Commands and environment

```sh
mise run benchmark -- --job Dry --filter '*GameInitialization*'
mise run benchmark -- --filter '*GameInitialization*' --warmupCount 5 --iterationCount 15
```

Both Dry cases completed; Dry values are execution checks only. The measured run
used BenchmarkDotNet 0.15.8, SDK 10.0.402, .NET runtime 10.0.13, X64 RyuJIT
x86-64-v3, Ubuntu 26.04.1 LTS, Intel Core i9-13900KF, 32 logical / 24 physical
cores, Concurrent Workstation GC. Five warmup and fifteen measurement iterations
per case; one outlier removed in each case. The ordinary unprivileged process
could not obtain high priority; no elevation or host changes were attempted.

## Results

| Players | Mean | 99.9% error half-width | Std. deviation | Gen0 / 1,000 ops | Gen1 / 1,000 ops | Allocated / op |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 32.65 µs | 1.348 µs | 1.195 µs | 5.1270 | 0.3662 | 94.36 KiB |
| 4 | 32.35 µs | 0.208 µs | 0.184 µs | 5.2490 | 0.3662 | 97.18 KiB |

The wider three-player interval and the absence of other controlled comparisons
make the small difference between seat counts uninformative. These are baselines
for this fixture, host and warmed resolved-input path. They do not predict full
Dune throughput, concurrent scaling, cold startup, RL encoding or search cloning.
Allocation remains measurable baseline work; no custom allocator or PRNG was added.

Raw log, CSV, HTML and Markdown reports are retained locally in the ignored
`BenchmarkDotNet.Artifacts/slice-002-initialization/` directory. Rerun the commands
to reproduce measurements on another host. Future optimization proposals should
compare the same workload plus cold setup, observation and clone scenarios as
appropriate, preserving all privacy, identity and determinism tests.
