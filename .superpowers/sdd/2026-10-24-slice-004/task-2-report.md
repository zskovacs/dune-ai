# Task 2 report — P1b graph safety and replay v2

Status: DONE (implementation checkpoint; independent parent task review still required).
Base: `a9c00003189030c731f40edaee38b5280b3c81ff`.
Worktree: `/home/wild/.codex/worktrees/e80e/dune-ai`.
Scope: approved Task 2 only, with parent-approved targeted frame/version integration.

## Changes and contracts

- Added `src/Dune.Engine/Turns/EffectGraphValidator.cs`: public `Validate(ImmutableArray<Effect>)` returns immutable shape/support diagnostics cached by immutable array identity. Uses BCL reference-identity path detection, not reflection/serialization dispatch. Validates primitive amounts/enums, costs, choice shape/null entries, unsupported primitives, cycles, and bounded structural/JSON nesting. Shared acyclic effect nodes are allowed. Structural nesting remains bounded at 32; the additional 48 JSON-container budget leaves space inside the existing strict serializer's default depth for enclosing rules/snapshot objects.
- `TurnEngine` normalizes definitions once and caches shape results against their normalized boxes. Contextual support results include immutable box, recipient, card/board-source availability and nesting depth (depth is part of the key so cached short paths cannot hide overlong acquisition/Signet paths). Source requirements, role IDs, acquisition references and Signet/acquisition cycles are checked before activation. Partial registries remain constructible; a bad box/definition diagnoses only when used. Board/card invalid enums and negative definition costs are diagnosed before canonical serialization or selected actions.
- Dynamic `Validate` checks selected branches before payment, decision/frame removal, card movement, RNG use or history changes. Selected mandatory permanent counts are grouped by recipient and checked against current ownership. Optional/choice permanent branches are checked when selected, allowing decline of an impossible grant. Selected branches are checked jointly with suspended mandatory frames. Existing conservative influence-threshold-four and public-stock reservations remain; optional acquisition demand cannot consume mandatory reserved stock, while a standalone unavailable optional acquisition can be declined. Unsupported mechanics in any optional branch still reject its activation. Resource integer bounds include bound recipients, nested grants, influence VP, actual harvest board sources, control rewards and the Reveal council bonus. Checks conservatively bound possible gains/choice branches, consistent with existing influence/stock preflight policy.
- `EffectExecutionFrame.Decision` is now the authoritative active decision. Removed the independent live decision stack. `TurnSnapshotData` canonical data contains ordered frames and next-frame/effect IDs, without duplicate Pending/Decisions payloads. Existing trusted inspection callers retain `[JsonIgnore]` computed `Pending`/`Decisions` views. Frames retain remaining-work order (the sequence cursor), contexts, all actor roles, physical/definition sources, provenance and typed return continuation. No frame data is projected into public observations.
- Versions: setup snapshot `dune-setup-snapshot-v2`; turn snapshot/replay remain deliberately `dune-turn-snapshot-v2` / `dune-turn-replay-v2`; default support revision `original-imperium-turn-support-v2`; official executable preview rules revision `imperium-2020-turn-preview-v2`. Configuration identity format and Toy replay versions unchanged. Replay v1 is rejected with `replay-version` during metadata comparison, before applying any action.
- Added `EffectSafetyTests.cs` and `LifecycleReplayTests.cs`: duplicate/already-owned Council/Swordmaster grants, selected optional/choice failures, complete trusted-state atomicity, unavailable optional versus mandatory acquisition, unsupported optional rejection, acquire/Signet/reference cycles, malformed/deep/enumerated graphs, cache behavior, resource bounds, automatic sequence-tail preflight, foreign acquire checkpoints, strict JSON and canonical effect/frame/all-eight-existing-action-tag goldens. Replay tests compare every player's next legal actions and final trusted hash before/inside/after the foreign child and at CombatBoundary.

## Invalid-box identity policy

Malformed effects can themselves be impossible to serialize safely (for example, invalid enum values, unknown runtime types or reference cycles). Before rules identity serialization, the affected box becomes an allowlisted `UnsupportedEffect` marker containing deterministic diagnostic code/message text. `_shapes` binds the original immutable diagnostics to that exact normalized box; `_cardErrors`/`_spaceErrors` retain definition diagnostics. Support validation returns those diagnostics and never executes the marker or discarded malformed input. Unrelated boxes remain usable.

For these rejected boxes, the rules identity binds the **normalized nonexecutable definition**, not the original discarded malformed payload. Different invalid graphs with identical diagnostic markers intentionally may share a normalized rules identity. `InvalidBoxMarkersBindNormalizedIdentityAndRetainOriginalDiagnostics` proves this for draw counts -1/-2, proves both remain rejected, and proves valid draw counts 1/2 retain distinct full semantic identities. Supported valid definitions retain their complete canonical semantics; registration ordering is canonicalized, and explicit empty/default arrays retain the previous normalization equivalence. Strict JSON deserialization is unchanged; unknown members/enums/tags are rejected rather than normalized at the JSON boundary.

## Verification evidence

Commands ran in the assigned worktree through RTK and mise. Focused tests were run first, then the full engine task; no packages/tool pins/locks were changed.

1. Initial existing-suite checkpoint after validation/frame migration:
   `rtk proxy mise run test:engine`
   Output: `Build succeeded. 0 Warning(s), 0 Error(s). Passed! Failed: 0, Passed: 188, Skipped: 0, Total: 188.` Exit 0.
2. Focused final behavior tests:
   `rtk proxy mise exec -- dotnet test tests/Dune.Engine.Tests --configuration Release --no-restore --filter 'FullyQualifiedName~EffectSafetyTests|FullyQualifiedName~LifecycleReplayTests'`
   Output: Engine and Engine.Tests built; `.NETCoreApp,Version=v10.0`; `Passed! - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 372 ms`. Exit 0.
3. Final full engine task:
   `rtk proxy mise run test:engine`
   Output: locked restore reported all projects up-to-date; solution Release build produced Engine, Simulation, CLI, all three test projects and Benchmarks; `Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:00.39`; `Passed! - Failed: 0, Passed: 219, Skipped: 0, Total: 219, Duration: 775 ms - Dune.Engine.Tests.dll (net10.0)`. Exit 0.
4. Scoped formatting applied and then verified with these exact commands (same include list):

   ```sh
   rtk proxy mise exec -- dotnet format dune-ai.slnx --no-restore --include src/Dune.Engine/Turns/EffectGraphValidator.cs src/Dune.Engine/Turns/TurnEngine.cs src/Dune.Engine/Turns/EffectExecution.cs src/Dune.Engine/Turns/TurnSnapshot.cs src/Dune.Engine/Turns/TurnRules.cs src/Dune.Engine/Turns/OriginalImperiumTurnRules.cs src/Dune.Engine/Runtime/TrustedGameSnapshot.cs tests/Dune.Engine.Tests/EffectSafetyTests.cs tests/Dune.Engine.Tests/LifecycleReplayTests.cs
   rtk proxy mise exec -- dotnet format dune-ai.slnx --no-restore --verify-no-changes --include src/Dune.Engine/Turns/EffectGraphValidator.cs src/Dune.Engine/Turns/TurnEngine.cs src/Dune.Engine/Turns/EffectExecution.cs src/Dune.Engine/Turns/TurnSnapshot.cs src/Dune.Engine/Turns/TurnRules.cs src/Dune.Engine/Turns/OriginalImperiumTurnRules.cs src/Dune.Engine/Runtime/TrustedGameSnapshot.cs tests/Dune.Engine.Tests/EffectSafetyTests.cs tests/Dune.Engine.Tests/LifecycleReplayTests.cs
   ```

   Both exited 0 with no output. Final verification was after the last runtime refinement.
5. `rtk git diff --check`: exit 0, no whitespace errors. Inspected the runtime and new-test diffs plus status; changes limited to the nine listed runtime/test files and this report. No generated files, lock files, unrelated source or original checkout changes.

During implementation, one focused build exposed CS0136 for reused switch-scope diagnostic variable names. Renamed the Reveal-specific locals; subsequent focused/full builds and tests pass. Earlier focused runs passed 23 and then 29 tests before the remaining assertions were added. No unknown test failure remains.

Official .NET consultation: Microsoft Learn MCP `microsoft_docs_search` for `System.Collections.Generic ReferenceEqualityComparer ImmutableArray IsDefault documentation`; retained BCL immutable-array default handling and reference equality. No new dependencies or custom test infrastructure.

## Self-review and limits

- Checked validation ordering for placement, Reveal, selected options, effect resolution, purchases and automatic child/sequence continuations. No known expected invalid-content/unsupported/illegal path mutates trusted state first. Tests compare complete canonical JSON/hash, RNG checkpoint and action history for representative failures.
- This is prevalidation, **not transactional rollback**. Unexpected runtime exceptions or externally corrupted trusted state can still leave partial mutation; no rollback guarantee is claimed. Existing invariant corruption remains an exception. Immutability of executable definitions is assumed after construction, as elsewhere in the engine.
- No Combat/lifecycle windows were added; Player Turns still stop at CombatBoundary and the unsupported faction-alliance threshold-four gate is preserved for Task 6.
- No performance claims; no benchmarks needed for correctness-oriented caching. No `mise run check`/smoke at this bounded task checkpoint; parent owns broad final branch verification and independent review. The required focused/full-engine/scoped-format checks above all passed.
- Next action: parent independent task review against the Task 2 brief before advancing the stage. This local commit is a checkpoint, not task approval or final branch handoff.
