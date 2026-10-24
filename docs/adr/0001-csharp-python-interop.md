# ADR 0001: Measure a batched engine boundary before selecting interop

- Status: **Accepted for scaffolding; production transport deferred**
- Date: 2026-10-24
- Context: .NET 10 engine, Python 3.12/PyTorch learning on native Linux, potentially
  millions of games. No bridge is implemented in this initialization.

## Options

| Approach | Benefits | Costs / compatibility to verify |
| --- | --- | --- |
| pythonnet embedding CoreCLR in Python | Established MIT library; calls managed APIs in process; retains ordinary .NET testing and debugging | Test Python 3.12/.NET 10 compatibility, runtime loading, object conversion, lifetime and GIL behavior; per-action crossings may dominate a small game |
| NativeAOT exports + Python `ctypes` or CFFI | Standard C ABI, self-contained native library, explicit batch buffers and lifetimes | Additional Linux build prerequisites, AOT constraints, error/handle/ownership ABI design, deployment per architecture; native libraries cannot be unloaded safely as a general lifecycle mechanism |
| Persistent worker processes with batched IPC | Runtime isolation, independent worker seeds, amortized boundary costs and fault recovery | Process startup, copies and scheduling; shared-memory ownership/backpressure must be designed; use standard process/shared-memory facilities before bespoke infrastructure |
| Per-action HTTP/JSON | Familiar debugging and interoperability | Network stack and text conversion per transition are unnecessary for this local workload; no reason to select it here |

These costs are engineering expectations, **not measured performance claims**.
The official [pythonnet runtime documentation](https://pythonnet.github.io/pythonnet/python.html)
describes explicit CoreCLR selection before importing `clr`. Microsoft's
[NativeAOT library guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)
describes exported methods and deployment constraints. Both remain candidates;
neither is installed or asserted compatible with this exact stack yet.

## Decision

Keep the rules in C#, expose observations/action IDs through a future adapter,
and benchmark **batched** reset/step/observe with pythonnet first. It is an
established bridge with a smaller initial implementation burden than a C ABI.
Compare NativeAOT or a persistent process only if compatibility or measurements
justify the extra work. Do not commit to a transport or claim a winner now.

Candidate batch shape (design intent, not an implemented API): reset ordered
episode IDs with config and seeds; submit `(episode ID, decision ID, action ID)`;
return packed player observations, legal masks, rewards, termination/truncation
and next decision owner. Use integer action IDs and dense numeric buffers;
define shape/dtype, copy count, lifetime and invalid-action errors explicitly.
Keep privileged state and full replays off the policy side of this boundary.

## Required spike and decision gate

1. First measure pure C# reset, legal-actions, step and full-episode baselines.
2. Compare identical seeded action scripts through batch sizes 1/32/256/1024;
   measure warm throughput, p50/p95 latency, startup, allocations, resident memory
   and copies. Separate boundary time from observation encoding and policy time.
3. Compare one/multiple workers on the target i9/64 GB host. Sweep bounded worker
   counts; avoid CPU thread oversubscription with PyTorch. Fix seeds independently
   of scheduling; assert byte-equivalent traces and mask/observation semantics.
4. Verify Python 3.12, CoreCLR .NET 10 loading, error propagation, disposal,
   cancellation and multithread/process behavior. Keep all measurements CPU-only
   first; measure GPU inference batches separately on the RTX 4070 Ti later.
5. Record workload, hardware, OS, revision, exact versions and raw reports. Prefer
   the simpler maintained solution if differences are negligible. A follow-up ADR
   selects the production mechanism only after representative results exist.

## RL interface evaluation

Prefer a future **PettingZoo AEC** adapter for sequential multi-agent decisions.
Its [AEC API](https://pettingzoo.farama.org/api/aec/) defines agent iteration,
reward/termination/truncation and legal-action masks. The Parallel API describes
simultaneous actions; running many independent episodes does not itself make the
game simultaneous. Add the package and its conformance tests with the adapter.

[OpenSpiel](https://github.com/google-deepmind/open_spiel/blob/master/docs/api_reference.md)
offers game/state interfaces, chance nodes, information-state representations and
search/evaluation tools under Apache-2.0. It is a useful future research adapter,
but adopting its C++ game core now would add another engine boundary. PettingZoo
(MIT) fits the initial Python interface with less architectural coupling. This
is a complexity decision, not a measured performance comparison. Revisit for
search/research needs; do not reimplement those frameworks here.

Use [PyTorch](https://pytorch.org/get-started/locally/) when neural models are added.
Choose a uv-locked CPU/CUDA installation profile against that milestone's driver
requirements. Default setup and CI currently need neither GPU nor torch download.
