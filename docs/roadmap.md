# Development roadmap

## Initialization — current

Toolchain/dependency locks, .NET engine and simulation boundaries, isolated toy
game, deterministic replay, autonomous random baseline, ordered parallel episode
runner, Python package skeleton/action masks, tests, CI and BenchmarkDotNet.
No actual Dune mechanics, bridge, models or self-play learning are present.

## 1. Verify scope and implement one real rules slice

Select Uprising rulebook revision, player-count mode and optional-module policy.
Populate the source register. Model setup and public/private state from verified
sources, then implement one small decision with illegal-action/privacy tests and
replay. Extend to turn/phase transitions and nested choices in vertical slices.
Keep the toy suite as an independent architecture regression example.

## 2. Cover a complete base-game episode

Add sourced board/card effects incrementally with interaction tests and rule
coverage tracking. Define explicit chance handling, observation/action schema
versions and replay compatibility. Add cross-policy evaluation fixtures. A full
episode requires validated setup, all phases, scoring and terminal behavior.

## 3. Measure and connect Python

Run the ADR 0001 batch interop spike, compare representative alternatives, select
a transport, then implement a PettingZoo AEC adapter and run its conformance tests.
Expose only player information, action masks, rewards and termination/truncation.
Benchmark actual observation encoding and batch copies before optimizing.

## 4. Reproducible self-play infrastructure

Add rollout collection, train/evaluation seed partitions, checkpoints and artifact
manifests (revision, engine/rules/observation versions, dependency locks, seeds,
configs). Choose an established experiment tracker after local file manifests
show a need. Add metrics for invalid actions, game lengths, win rates, throughput
and resource use. Benchmark worker counts on the target i9/64 GB system.

## 5. Learning and search

Add uv-locked PyTorch CPU/CUDA profiles with GPU smoke tests, then select an
established RL implementation suited to multi-agent hidden-information learning.
Evaluate against random and deterministic baselines with held-out seeds and
uncertainty estimates. Investigate information-set search/MCTS only after rule
correctness and strong evaluation exist. No PPO or MCTS is implemented now.
