# SLICE-003 turn-engine baseline

Command: `mise run benchmark -- --job Short --filter '*TurnEngineBenchmarks*'`.
The Dry harness also executed all four workloads successfully. Dry timings are
not performance measurements. No optimization or throughput claim follows from
this initial baseline.

## Environment and workload

Measured 2026-10-24 on Linux Ubuntu 26.04.1 x64, Intel Core i9-13900KF
(32 logical / 24 physical cores), .NET SDK 10.0.402, runtime 10.0.13,
X64 RyuJIT x86-64-v3, Concurrent Workstation GC, BenchmarkDotNet 0.15.8.
Source: uncommitted `slice-003-turn-engine` work based on SLICE-002 revision `8b3cdce`;
rule/support identifiers are recorded by the engine. These numbers establish a
local development baseline, not a reproducible published artifact/revision.

The fixture has three synthetic players, seed 42, synthetic card/board effects and
no official leader abilities. It does not represent complete authentic gameplay.
The public metadata and physical-card pool are the existing setup fixture.

- Legal enumeration reuses one unchanged started game and returns the current
  implemented fixture choices. It includes candidate construction and validation.
- Mutable workloads batch 4,096 independent games per invocation, with
  `OperationsPerInvoke=4096`. Targeted `IterationSetup` prepares states outside
  timing and allocation measurement; BenchmarkDotNet forces one invocation and
  unroll factor1. Each game receives exactly one measured transition.
- Round Start includes validation/invariants, Conflict reveal and three five-card
  draws. Initialization, rules composition and snapshot hashing are excluded.
- Agent placement includes validation, mandatory payment, movement, occupancy,
  deterministic primitive drain and scheduling at synthetic Secure Contract.
- Effect resolution selects a nested optional payment and resolves its child gain
  and continuation. The outer placement/order choices are prepared beforehand.
- Accepted action recording and invariant validation remain enabled. The measured
  paths include their real allocations; setup/replay/observation are not measured.

## Measured ShortRun

One launch, three warmups and three measured iterations. Error is the half-width
of the 99.9% confidence interval. Allocation is managed bytes per operation.

| Workload | Mean | Error | StdDev | Gen0 / 1,000 ops | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| Legal action enumeration | 36.17 us | 1.860 us | 0.102 us | 5.3101 | 97.78 KB |
| Round Start | 24.06 us | 1.759 us | 0.096 us | 0.9766 | 19.91 KB |
| Agent placement | 23.00 us | 5.027 us | 0.276 us | 0.9766 | 21.22 KB |
| Optional effect resolution | 19.02 us | 3.538 us | 0.194 us | 0.9766 | 19.88 KB |

The host could not raise process priority, an expected unprivileged warning.
Mutable iterations were 77–99 ms, below BenchmarkDotNet's recommended 100 ms minimum.
ShortRun has few samples; repeat a longer job in an otherwise idle environment
before comparing implementations. Raw reports remain in ignored
`BenchmarkDotNet.Artifacts/results/`; rerunning may overwrite them.

## Practical limits

Legal enumeration allocates candidates and recursively validates graphs; each
transition currently scans physical-card conservation. These measured allocation
costs deserve a bounded follow-up before million-game throughput work. There is
no evidence here for parallel scaling, full games/second, ML encoding, cloning or
Python interop performance. No specialized optimization framework was introduced.
