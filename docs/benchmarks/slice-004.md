# SLICE-004 lifecycle measurement

These are **synthetic micro workloads and a full synthetic development game**.
They do not establish authentic Dune throughput, self-play readiness or a reason
to optimize the resolver. No production optimization or dependency was added.
Dry runs are execution checks only.

## Reproduction and measured revision

Measured code revision: `170a8f21fbce8af435da08673aa087ba5aba3201` on
`slice-004-lifecycle`. Documentation/handoff was still being edited; no measured
C# differed from that revision. Pre-production baseline:
`3c0229bebcbb3ae7542b15a3b76430e5c7ab745f`.

```sh
rtk proxy mise run benchmark -- --job Dry --filter '*LifecycleBenchmarks*'
rtk proxy mise run benchmark -- --job Dry --filter '*TurnEngineBenchmarks*'
rtk proxy mise run benchmark -- --job Short --filter '*LifecycleBenchmarks*'
rtk proxy mise run benchmark -- --job Short --filter '*TurnEngineBenchmarks*'
```

The two Short runs were sequential on the same host; no project build, test or
other benchmark ran concurrently with their measurements. Harness-internal
builds/warmups are normal BenchmarkDotNet work. Host: Ubuntu 26.04.1 LTS,
Intel Core i9-13900KF, 1 CPU / 24 physical / 32 logical cores, x64 RyuJIT
x86-64-v3; SDK 10.0.402, runtime .NET 10.0.13 (10.0.1326.50504),
BenchmarkDotNet 0.15.8, concurrent workstation GC. ShortRun: one launch,
three warmups, three measured iterations. High-process-priority setup was denied;
no privilege changes were made. Error is half the 99.9% confidence interval;
three measurements provide broad uncertainty, not a strong performance claim.

## Workload boundaries

All new workloads use `tests/Fixtures/LifecycleFixture.cs`, linked into the
benchmark project alongside the existing fixture adapter/policy. Environment seed
42, fixed leader-selection seed 17, deterministic public-observation policy,
3/4 seats. Main decks, Intrigue timing/effects, Conflict rewards and leaders are
invented. Sourced original board-space and supported reserve operations are reused.
The selected recipe is ten Conflicts from an eighteen-definition synthetic pool.

| Workload | Measured operation and exclusions |
|---|---|
| UnchangedLegalEnumeration | One ordered legal enumeration on an unchanged round-one state; initialization/composition excluded. |
| RankingTieDistribution | One `CombatOutcome.Calculate` for strengths 8/8/4 (and 8/8/4/4); this distributes reward tiers, not actual grants. Participant setup excluded. |
| DepthThreeForeignChild | One accepted option completing a Combat child at depth >=3 (Sequence -> foreign player -> optional). Exact depth checked outside measurement. |
| CombatPlay | One accepted Combat Intrigue play; independent round-one checkpoints prepared outside measurement. |
| CombatPass | One accepted Combat opportunity pass; includes its normal engine continuation. |
| CombatChildResume | One accepted optional choice returning from a played Combat child to its suspended window. |
| RewardRoundTransition | One winner PostWin closing pass after required reward work, including remaining grants, cleanup, Makers, Recall, next Conflict and Draw5. Iteration cleanup verifies round two, outside measurement. |
| FullSyntheticLifecycleIncludingInitialization | One complete ten-round invented game from fresh state. Includes fixture catalog/rules composition and identities, mutable setup, engine initialization/initial trusted hashing, observations/legal enumeration/policy selection, all accepted actions/history, terminal scoring and replay-identity hashing. Excludes `EpisodeRunner` wrapper, CLI/logging, final `CaptureReplay()` full-state serialization/hash and replay reconstruction. No per-action snapshot or JSON is added. |

Mutable micro workloads batch **256 independent engines**, prepared in
`IterationSetup`, and run each once per invocation. `OperationsPerInvoke=256`
normalizes time/allocations per accepted transition; setup, composition, initial
hashing and checkpoint driving are excluded. Their retained accepted history and
normal transition allocations are included. BDN enforces invocation/unroll one
for these iteration-setup workloads. The full workload is separately normalized
per complete game with all initialization/composition included. The unchanged
TurnEngine harness retains its original **4096**-engine batches/definitions and
legal-state workload, so its baseline comparison is directly comparable in code.
No replay/state serialization was inserted into the per-action measurement loop.

## Results

### Lifecycle ShortRun

| Method                                        | InvocationCount | UnrollFactor | Players | Mean             | Error             | StdDev           | Gen0       | Gen1      | Allocated    |
|---------------------------------------------- |---------------- |------------- |-------- |-----------------:|------------------:|-----------------:|-----------:|----------:|-------------:|
| **UnchangedLegalEnumeration**                     | **Default**         | **16**           | **3**       |     **228,373.7 ns** |     **126,073.06 ns** |      **6,910.49 ns** |    **47.3633** |   **11.7188** |    **872.92 KB** |
| RankingTieDistribution                        | Default         | 16           | 3       |         338.4 ns |           6.60 ns |          0.36 ns |     0.1001 |         - |      1.84 KB |
| FullSyntheticLifecycleIncludingInitialization | Default         | 16           | 3       | 123,211,288.3 ns | 229,830,264.12 ns | 12,597,772.35 ns | 19000.0000 | 8000.0000 |  353368.3 KB |
| DepthThreeForeignChild                        | 1               | 1            | 3       |      20,259.5 ns |       2,638.83 ns |        144.64 ns |          - |         - |     48.34 KB |
| CombatPlay                                    | 1               | 1            | 3       |      20,729.9 ns |      42,775.75 ns |      2,344.68 ns |          - |         - |        51 KB |
| CombatPass                                    | 1               | 1            | 3       |      14,424.2 ns |      19,122.63 ns |      1,048.18 ns |          - |         - |     26.77 KB |
| CombatChildResume                             | 1               | 1            | 3       |      21,351.1 ns |      26,695.68 ns |      1,463.28 ns |          - |         - |     44.88 KB |
| RewardRoundTransition                         | 1               | 1            | 3       |      23,514.7 ns |       4,886.15 ns |        267.83 ns |          - |         - |     42.68 KB |
| **UnchangedLegalEnumeration**                     | **Default**         | **16**           | **4**       |     **240,425.1 ns** |     **114,711.40 ns** |      **6,287.72 ns** |    **48.3398** |   **11.9629** |    **890.55 KB** |
| RankingTieDistribution                        | Default         | 16           | 4       |         377.9 ns |           8.22 ns |          0.45 ns |     0.1111 |         - |      2.05 KB |
| FullSyntheticLifecycleIncludingInitialization | Default         | 16           | 4       | 135,423,765.0 ns |   5,535,314.52 ns |    303,409.27 ns | 24000.0000 | 8000.0000 | 449733.06 KB |
| DepthThreeForeignChild                        | 1               | 1            | 4       |      26,280.5 ns |         873.49 ns |         47.88 ns |          - |         - |     51.48 KB |
| CombatPlay                                    | 1               | 1            | 4       |      23,865.3 ns |      54,758.74 ns |      3,001.51 ns |          - |         - |     54.14 KB |
| CombatPass                                    | 1               | 1            | 4       |      11,965.9 ns |      28,286.31 ns |      1,550.47 ns |          - |         - |      29.7 KB |
| CombatChildResume                             | 1               | 1            | 4       |      21,491.3 ns |      32,023.22 ns |      1,755.30 ns |          - |         - |     48.02 KB |
| RewardRoundTransition                         | 1               | 1            | 4       |      21,288.4 ns |      12,964.43 ns |        710.62 ns |          - |         - |     47.02 KB |

Time is reported in ns; Gen0/Gen1 are collections per 1000 normalized operations.
`-` means no collection was recorded during the measured batch, not zero allocations.
The three-seat full-game 99.9% error exceeds its mean; retain that uncertainty.

### Unchanged TurnEngine ShortRun

| Method                 | InvocationCount | UnrollFactor | Mean     | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|----------------------- |---------------- |------------- |---------:|----------:|---------:|--------:|-------:|----------:|
| LegalActionEnumeration | Default         | 16           | 96.02 μs | 28.748 μs | 1.576 μs | 18.4326 | 4.5166 | 340.68 KB |
| RoundStart             | 1               | 1            | 26.67 μs |  3.737 μs | 0.205 μs |  1.2207 | 0.2441 |  26.74 KB |
| AgentPlacement         | 1               | 1            | 31.11 μs |  7.049 μs | 0.386 μs |  1.7090 | 0.2441 |  34.37 KB |
| EffectResolution       | 1               | 1            | 27.58 μs |  3.411 μs | 0.187 μs |  2.1973 | 0.4883 |  44.46 KB |

## Pre-production baseline and interpretation

The same unchanged TurnEngine harness was measured before production edits at
`3c0229b`; source evidence was retained in the plan scratch baseline report/CSV/log.

| Method | Mean | Error | StdDev | Gen0/1000 ops | Allocated/op |
|---|---:|---:|---:|---:|---:|
| LegalActionEnumeration | 36.49 us | 1.215 us | 0.067 us | 5.4321 | 100.53 KB |
| RoundStart | 24.50 us | 2.358 us | 0.129 us | 0.9766 | 19.91 KB |
| AgentPlacement | 22.59 us | 2.985 us | 0.164 us | 0.9766 | 21.36 KB |
| EffectResolution | 19.68 us | 5.865 us | 0.321 us | 0.9766 | 19.88 KB |

The unchanged harness's sampled means rose from 36.49/24.50/22.59/19.68 us
to 96.02/26.67/31.11/27.58 us for legal enumeration/RoundStart/placement/optional
resolution respectively. Legal enumeration is approximately **2.63x** the sampled
baseline mean and **3.39x** its allocations (100.53 -> 340.68 KB). Other sampled
mean ratios are approximately 1.09x/1.38x/1.40x; uncertainty remains explicit in
the tables, so ratios are descriptive rather than a statistical guarantee.

This is a measured total-branch cost increase on unchanged synthetic inputs.
The slice adds causal/lifecycle/safety behavior, but these measurements do not
isolate the cause of any individual increase. No optimization was attempted.
Retain it as a performance concern for profiling representative workloads; do
not infer authentic game throughput, peak RSS, worker scaling or training speed.
The lifecycle legal fixture has richer enabled content and cannot be substituted
for the unchanged harness in this comparison. Full-game allocation is managed
allocation per game, not retained/peak memory; its 3-seat confidence interval is
especially wide.

## Evidence and limitations

Raw BenchmarkDotNet reports are ignored local artifacts under
`BenchmarkDotNet.Artifacts/results/Dune.Benchmarks.LifecycleBenchmarks-report.*`
and `Dune.Benchmarks.TurnEngineBenchmarks-report.*`. Task evidence is retained in
`.superpowers/sdd/2026-10-24-slice-004/task-12-lifecycle-{dry,short}.log`,
`task-12-turn-{dry,short}.log`, and copied `task-12-*-results.{md,csv}`. The
baseline files are `baseline-benchmark-results.md`, `.csv`,
`baseline-benchmark.log` in that same scratch directory. Scratch is not a tracked
deliverable; this report preserves commands, revision, boundaries and results.

Lifecycle Dry executed 16/16 workloads; TurnEngine Dry executed 4/4. Proper
Short measurements, rather than Dry timings, populate the tables above. Mutable
micro measurements may have short iteration times because setup enforces one
invocation; these are explicitly normalized batches. Small ShortRun samples,
managed-GC retention, host frequency/load and process priority limit inference.
Full synthetic games have different included costs and cannot be compared to an
isolated micro transition as a speed regression. No authentic deck/game, search
clone, Python bridge, worker scaling or training workload is measured. The
complete source/coverage gates remain in the [handoff](../handoffs/SLICE-004-result.md).
