# SLICE-004 lifecycle measurement

These are **synthetic micro workloads and a full synthetic development game**.
They do not establish authentic Dune throughput, self-play readiness or a reason
to optimize the resolver. No production optimization or dependency was added.
Dry runs are execution checks only.

## Reproduction and measured revision

Measured code/test revision: `535c7e124480d61d543c33db8b79672b369d6219` on
`slice-004-lifecycle`, recorded before both proper Short runs. This refresh follows
the final-review F1 runtime preflight correction; benchmark harness code is
unchanged. Later documentation-only finalization does not change measured C#.
Pre-production baseline: `3c0229bebcbb3ae7542b15a3b76430e5c7ab745f`.

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

| Method                                        | InvocationCount | UnrollFactor | Players | Mean             | Error            | StdDev          | Gen0       | Gen1       | Allocated    |
|---------------------------------------------- |---------------- |------------- |-------- |-----------------:|-----------------:|----------------:|-----------:|-----------:|-------------:|
| **UnchangedLegalEnumeration**                     | **Default**         | **16**           | **3**       |     **248,077.5 ns** |    **145,049.91 ns** |     **7,950.67 ns** |    **52.7344** |    **12.9395** |    **972.76 KB** |
| RankingTieDistribution                        | Default         | 16           | 3       |         332.7 ns |          6.17 ns |         0.34 ns |     0.1001 |          - |      1.84 KB |
| FullSyntheticLifecycleIncludingInitialization | Default         | 16           | 3       | 137,434,367.7 ns | 12,468,507.36 ns |   683,440.96 ns | 20000.0000 |  7000.0000 |  378816.8 KB |
| DepthThreeForeignChild                        | 1               | 1            | 3       |      23,568.6 ns |     31,455.57 ns |     1,724.19 ns |          - |          - |     49.69 KB |
| CombatPlay                                    | 1               | 1            | 3       |      21,634.7 ns |      2,662.29 ns |       145.93 ns |          - |          - |     52.26 KB |
| CombatPass                                    | 1               | 1            | 3       |      14,813.6 ns |     54,577.18 ns |     2,991.56 ns |          - |          - |     26.77 KB |
| CombatChildResume                             | 1               | 1            | 3       |      20,798.3 ns |     50,392.94 ns |     2,762.21 ns |          - |          - |      45.9 KB |
| RewardRoundTransition                         | 1               | 1            | 3       |      22,519.7 ns |     91,740.27 ns |     5,028.59 ns |          - |          - |     42.68 KB |
| **UnchangedLegalEnumeration**                     | **Default**         | **16**           | **4**       |     **259,338.6 ns** |    **114,237.05 ns** |     **6,261.72 ns** |    **53.7109** |    **13.1836** |    **994.61 KB** |
| RankingTieDistribution                        | Default         | 16           | 4       |         366.9 ns |         16.57 ns |         0.91 ns |     0.1111 |          - |      2.05 KB |
| FullSyntheticLifecycleIncludingInitialization | Default         | 16           | 4       | 189,084,636.7 ns | 70,808,681.96 ns | 3,881,262.81 ns | 26000.0000 | 10000.0000 | 479526.84 KB |
| DepthThreeForeignChild                        | 1               | 1            | 4       |      27,799.7 ns |      2,907.36 ns |       159.36 ns |          - |          - |     52.83 KB |
| CombatPlay                                    | 1               | 1            | 4       |      27,552.1 ns |     63,700.43 ns |     3,491.64 ns |          - |          - |      55.4 KB |
| CombatPass                                    | 1               | 1            | 4       |      13,431.4 ns |     34,298.24 ns |     1,880.00 ns |          - |          - |      29.7 KB |
| CombatChildResume                             | 1               | 1            | 4       |      20,578.9 ns |      9,301.02 ns |       509.82 ns |          - |          - |     49.04 KB |
| RewardRoundTransition                         | 1               | 1            | 4       |      27,366.5 ns |      2,023.86 ns |       110.93 ns |          - |          - |     47.02 KB |

Time is reported in ns; Gen0/Gen1 are collections per 1000 normalized operations.
`-` means no collection was recorded during the measured batch, not zero allocations.
Full-game means 137.434 ms (3p)/189.085 ms (4p), allocations 378816.80 KB/479526.84 KB; 99.9% errors ±12.469 ms/±70.809 ms respectively. Small ShortRun samples retain material uncertainty.

### Unchanged TurnEngine ShortRun

| Method                 | InvocationCount | UnrollFactor | Mean      | Error     | StdDev   | Gen0    | Gen1   | Allocated |
|----------------------- |---------------- |------------- |----------:|----------:|---------:|--------:|-------:|----------:|
| LegalActionEnumeration | Default         | 16           | 102.72 μs | 37.330 μs | 2.046 μs | 20.1416 | 5.0049 | 370.99 KB |
| RoundStart             | 1               | 1            |  26.97 μs |  3.178 μs | 0.174 μs |  1.2207 | 0.2441 |  26.74 KB |
| AgentPlacement         | 1               | 1            |  32.39 μs | 11.638 μs | 0.638 μs |  1.7090 | 0.2441 |  34.77 KB |
| EffectResolution       | 1               | 1            |  29.32 μs |  6.271 μs | 0.344 μs |  2.4414 | 0.4883 |  45.85 KB |

## Pre-production baseline and interpretation

The same unchanged TurnEngine harness was measured before production edits at
`3c0229b`; source evidence was retained in the plan scratch baseline report/CSV/log.

| Method | Mean | Error | StdDev | Gen0/1000 ops | Allocated/op |
|---|---:|---:|---:|---:|---:|
| LegalActionEnumeration | 36.49 us | 1.215 us | 0.067 us | 5.4321 | 100.53 KB |
| RoundStart | 24.50 us | 2.358 us | 0.129 us | 0.9766 | 19.91 KB |
| AgentPlacement | 22.59 us | 2.985 us | 0.164 us | 0.9766 | 21.36 KB |
| EffectResolution | 19.68 us | 5.865 us | 0.321 us | 0.9766 | 19.88 KB |

The unchanged harness's sampled means rose from 36.49/24.50/22.59/19.68 us to 102.72/26.97/32.39/29.32 us for legal enumeration/RoundStart/placement/optional resolution respectively. Legal enumeration is approximately **2.82x** the sampled baseline mean and **3.69x** its allocations (100.53 -> 370.99 KB). Other sampled mean ratios are approximately 1.10x/1.43x/1.49x; uncertainty remains explicit in the tables, so ratios are descriptive rather than a statistical guarantee.

This is a measured total-branch cost increase on unchanged synthetic inputs.
The slice adds causal/lifecycle/safety behavior, but these measurements do not
isolate the cause of any individual increase. No optimization was attempted.
Retain it as a performance concern for profiling representative workloads; do
not infer authentic game throughput, peak RSS, worker scaling or training speed.
The lifecycle legal fixture has richer enabled content and cannot be substituted
for the unchanged harness in this comparison. Full-game allocation is managed
allocation per game, not retained/peak memory; reported confidence intervals
remain material limits.

## Evidence and limitations

Raw BenchmarkDotNet reports are temporary ignored local artifacts under
`BenchmarkDotNet.Artifacts/results/Dune.Benchmarks.LifecycleBenchmarks-report.*`
and `Dune.Benchmarks.TurnEngineBenchmarks-report.*`. Temporary verification logs
and copied report/CSV data use `.superpowers/sdd/2026-10-24-slice-004/` names
`final-fix-lifecycle-{dry,short}.log`, `final-fix-turn-{dry,short}.log`, and
`final-fix-{lifecycle,turn}-results.{md,csv}`. Controller scratch cleanup may remove
these files; the committed tables, exact revision, commands, boundaries and
metadata here preserve the measurement evidence. The pre-production baseline
`3c0229b` is unchanged; its temporary original evidence uses
`baseline-benchmark-results.md`, `.csv`, `baseline-benchmark.log`.

Historical pre-F1 measurement at `170a8f2` had unchanged-harness legal enumeration
96.02 us/340.68 KB and full synthetic games 123.211 ms/353368.30 KB (3p),
135.424 ms/449733.06 KB (4p), with a 3p 99.9% error ±229.830 ms exceeding its mean.
Those figures are history, not current measurements. The post-F1 tables above
replace them; no isolated causal performance claim is made.

Lifecycle Dry executed 16/16 workloads; TurnEngine Dry executed 4/4. Proper
Short measurements, rather than Dry timings, populate the tables above. Mutable
micro measurements may have short iteration times because setup enforces one
invocation; these are explicitly normalized batches. Small ShortRun samples,
managed-GC retention, host frequency/load and process priority limit inference.
Full synthetic games have different included costs and cannot be compared to an
isolated micro transition as a speed regression. No authentic deck/game, search
clone, Python bridge, worker scaling or training workload is measured. The
complete source/coverage gates remain in the [handoff](../handoffs/SLICE-004-result.md).
