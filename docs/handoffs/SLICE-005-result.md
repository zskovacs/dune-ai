# BEGIN SLICE-005 RESULT FOR CHATGPT

## A. Repository State

- Current revision: assessed accepted runtime/test revision `0baed8b486044c7b806fa928d7de26574e06454a`; this handoff and canonical audit are the subsequent Task 5 documentation change. The Task 5 documentation revision is the commit containing this file (see Git history); controller task and final whole-branch reviews remain pending. This file does not claim its own SHA or a completed review.
- Branch/worktree: `main`, existing `/home/wild/code/personal/dune-ai` checkout; no additional worktree.
- Files created: `docs/superpowers/specs/2026-10-25-slice-005-design.md` and `docs/superpowers/plans/2026-10-25-slice-005.md` in the approved planning stage; `src/Dune.Engine/Turns/TurnEngine.Trashing.cs`; `tests/Dune.Engine.Tests/SelectedTrashTests.cs`, `ReserveReturnTests.cs`, `SelectiveBreedingTests.cs`, `SelectedTrashReplayPrivacyTests.cs`; this handoff.
- Files modified: `src/Dune.Engine/Turns/{GameAction,TurnRules,EffectExecution,EffectGraphValidator,TurnEngine,TurnEngine.Rewards,OriginalImperiumTurnRules}.cs`; `tests/Dune.Engine.Tests/{EffectContinuationTests,LifecycleReplayTests,LifecycleDefinitionTests,LifecycleIntegrationTests,ContentCoverageRegistryTests}.cs`; `tests/Fixtures/{LifecycleFixture,LifecycleFixtureGame}.cs`; `tests/Dune.Simulation.Tests/LifecycleEpisodeTests.cs`; `training/tests/test_content_coverage.py`; `docs/content/coverage/inventory.json`; generated `docs/content/{inventory-audit,ability-coverage,interaction-matrix,implementation-batches}.md` (the fifth readiness-gates projection was regenerated and stayed byte-identical); `docs/{implemented-mechanics,rules-sources,roadmap}.md`; `docs/architecture/{turn-engine,runtime-state}.md`; `docs/adr/0006-ability-and-decision-execution.md`. The planning docs remain controller-owned work outside this Task 5 commit; the user-owned `docs/handoffs/SLICE-003.5-result.md` was preserved byte-for-byte.
- Runtime changes: typed selected-card decision/actions and target binding, shared ordinary/Reserve trash routing, return-aware preflight, trusted executing-source owner, one original Selective Breeding BoardRule, and deterministic fixture-agent fallbacks.
- Architecture/ADR changes: the existing typed `TurnEngine`/causal-frame design now documents distinct executing source and selected target, source owner, privacy, stock and deliberate versions. No separate resolver, schema or dependency was added.
- Existing functionality preserved: Toy is separate; existing card/board effects, exact setup and embedded catalog, old action tags, six unready configurations and synthetic lifecycle tests remain supported within their prior bounds. Task 3's carried source-continuation Minor was closed by Task 4's reviewed 3/4-seat replay case.

## B. Implemented Mechanics

### Selected Trash

- Actual action and effect types: `SelectedTrashEffect(ImmutableArray<Effect> OnTrashed, bool Optional = true)`, `SelectTrashCardAction(int PlayerId, int DecisionId, CardZoneId Source, CardSelector Card)`, `DeclineTrashAction(int PlayerId, int DecisionId)`; `$effect: selected-trash`, `$action: select-trash-card` and `$action: decline-trash` use the strict allowlist.
- Supported target zones: chooser-owned Hand, Discard and InPlay, including the currently played source after placement has moved it to InPlay.
- Selection representation: visible zone plus `CardSelector(ContentId Definition, int Copy = 0)` and decision serial; trusted `SelectedTrashOption` freezes the exact `CardInstanceId`. Physical IDs are absent from policy actions.
- Duplicate-card behavior: Copy selects the exact frozen instance. A stale decision or compacted duplicate selector rejects before mutation instead of silently choosing another copy.
- Executing source versus selected target: `EffectContext.SourceCard`/`SourceDefinition`/`SourceOwnerId` retain the card causing an effect; separate `SelectedCardTarget` records target instance, definition, original zone and owner. Moving the played source does not rebind pending Agent work.
- Optional/mandatory semantics: optional decline works even without a target or supported success child; chosen success trashes exactly one card before `OnTrashed` children. Mandatory selected trash has no decline and requires a proven target and supported success before activation.
- Unsupported cases: copied-card/Unload/general trigger semantics, unsupported faces, speculative future targets and unsafe reacquire-then-source-self-trash ordering remain gated. A selected target is not a general authority to take another player's card.

### Reserve Return

- Routing implementation: `TurnEngine.TrashDestination(CardInstance)` and `TurnEngine.MoveTrashedCard(CardInstance, CardZone)` use setup/category/zone identity. Ordinary cards enter `Shared.Trashed`; actual Reserve cards enter `reserve:<definition>`.
- Supported Reserve definitions: actual Arrakis Liaison, Foldspace and The Spice Must Flow instances; a synthetic fixture Reserve is tested separately. Routing a Foldspace instance does not implement its unreadable face.
- Finite stock handling: `ValidateAcquisitionStock(..., ContentId? returnedReserve)` credits exactly one verified selected return to subsequent work, including generated children/suspended later recipients. Earlier acquisitions and unselected optional branches get no credit. Empty stacks can receive their own card.
- Card conservation: the same physical instance moves once; no mutable stock counter is introduced. Exact per-definition total and a previously earned The Spice Must Flow VP are preserved; returned cards may later be acquired or bought under existing rules.
- Interaction with acquisition: post-return acquisition can use stock and trigger actual supported Acquire effects; pre-return acquisition cannot borrow it.
- Unsupported cases: foreign/misidentified Reserve destination and unsafe source reacquisition paths fail before mutation. The Foldspace card face and other missing Reserve abilities remain unsupported.

### Selective Breeding

- Actual implemented rule: `imperium:space:selective-breeding:v1`, one original `BoardRule` in `OriginalImperiumTurnRules.Spaces()` with Bene Gesserit Agent access. It adds the twentieth authored board key.
- Placement cost: mandatory 2 Spice, paid before any benefit; matching icon, available Agent and vacancy remain required.
- Influence behavior: mandatory +1 Bene Gesserit Influence even when the exchange is declined. Existing player order of card, board and Influence effects applies.
- Trash decision: one optional arrow exchange. Taking it selects and trashes exactly one eligible own card; declining leaves Influence but no exchange reward. It cannot repeat.
- Draw behavior: taking the exchange pushes Draw2 after the trash movement, using the seeded current-discard refill when the personal deck is empty.
- Effect ordering: authored Influence and `SelectedTrashEffect([DrawEffect(2)])` are separate ordered roots; selected children finish before suspended parent work. Existing generated threshold descendants remain preflighted.
- Official source references: original 2020 player aid `src-A` p.2; 2020-10-26 original rules `src-I` p.9 and p.16; publisher FAQ revised 2025-01-13 p.3 for optional arrow/mandatory ordinary effect and p.4 for retained The Spice Must Flow VP. I and A local SHA-256 match canonical inventory; approved FAQ hash is `7b54c283357244e5107d1d0f4e87817d39297e914c2014239acbb2c460c0c6b9`, locator `https://d19y2ttatozxjp.cloudfront.net/pdfs/DUNE_IMPERIUM_FAQ_25-1-13.pdf`.
- Remaining limitations: only this sourced original placement is promoted; neither Secrets nor Foldspace space, other card faces or a complete legal board are implied.

## C. Engine Integration

- Relevant source paths: `src/Dune.Engine/Turns/{GameAction,TurnRules,EffectExecution,TurnEngine,TurnEngine.Trashing,OriginalImperiumTurnRules}.cs`; physical movement reuses `src/Dune.Engine/Runtime/CardZone.cs`.
- LegalActions changes: `TurnEngine.LegalActions(int playerId)` includes chooser-only selected targets from `TrashTargets(int playerId)` and optional decline. Actions expose the visible selector and decision ID, never trusted instance IDs.
- Step validation changes: `TurnEngine.Step(GameAction action)` and `Validate(GameAction action)` use `ValidateSelectedTrash(GameAction action, DecisionFrame decision)` before commit. Target ownership, zone, decision serial, frozen binding, mandatory later work, projected stock/resources and source safety are checked before any card/RNG/history mutation.
- EffectResolver/continuation changes: there is no separate `EffectResolver`; `TurnEngine.Execute(Effect effect, EffectContext context)` dispatches `BeginSelectedTrash`, and `CommitSelectedTrash` pushes `OnTrashed` via existing causal child frames. `ValidateRemaining` and `ValidateAcquisitionStock` cover suspended/generated work with the exact contracts below. `RootContext` and `Acquire` bind `SourceOwnerId` for physical executing sources; `ForPlayer`/other role changes preserve it.

Actual relevant signatures in the existing partial `TurnEngine` (private helpers are included for the next slice’s preflight boundary):

```csharp
public ImmutableArray<GameAction> LegalActions(int playerId)
public StepResult Step(GameAction action)
public TurnReplay CaptureReplay()
public static TurnReplayResult Replay(TurnReplay replay, Func<GameState> initialStateFactory, TurnRuleSet rules)
private TurnDiagnostic? ValidateRemaining(
    IEnumerable<(Effect Effect, int Owner)>? selected = null,
    BoardSpaceId? sourceSpace = null,
    IEnumerable<(int Payer, ResourceKind Resource, int Amount)>? payments = null,
    int? selectedPayer = null, int? selectedChooser = null,
    SelectedCardTarget? removedTrash = null, ContentId? returnedReserve = null)
private TurnDiagnostic? ValidateAcquisitionStock(
    IEnumerable<(Effect Effect, int Owner)> work,
    CardInstance? consumed = null, IReadOnlySet<Effect>? selectedChoices = null,
    ContentId? returnedReserve = null)
```

- GameState/card-zone changes: no new state zone or setup field; `CardZone.MoveTo` transfers the same instance to shared trash or the authoritative Reserve stack. A trusted suspended target and source-owner field are turn snapshot state.
- Observation changes: owner-only legal actions/summary can present a pending selection; nonowners cannot see target choices, physical IDs, private cards, frame context, RNG or privileged history. Resolved public trash/Reserve definition visibility follows existing observation projection.
- Compatibility with existing mechanics: explicit self-trash shares destination routing; acquisition, seeded refill, faction thresholds, payment/order and Combat/round/Endgame continuations remain on the existing executor. Existing synthetic lifecycle policy priorities are retained when these new actions are absent.

## D. Determinism, Privacy and Safety

- Card identity guarantees: trusted selection freezes `CardInstanceId` while policy actions use only visible definition/copy/zone. Exact movement and conservation are asserted, including duplicates and emptied Reserve stacks.
- Replay behavior: 3/4-seat accepted-prefix tests reconstruct canonical trusted state/hash, both RNG checkpoints, frames, owners, ordered legal actions, history, observations, terminal results and replay identity at each new decision and normal boundary.
- Invalid-action behavior: stale/wrong-owner/wrong-zone/invalid-copy/unsupported-success/stock and generated-child failures reject without changing trusted state, RNG, frames or accepted history. Strict JSON rejects unknown tags/members/enums.
- Hidden-information guarantees: nonempty opponent Hand/Intrigue/Deck counterfactual swaps with changed RNG checkpoints leave another seat's observation, legal mask and support diagnostics unchanged, including pending foreign decisions and completion. Publicly resolved trash definitions may become visible.
- Generated-child preflight: resource, acquisition-stock, Intrigue, faction threshold, permanent and special-operation reservations include the chosen success, suspended parent and later recipients; optional unchosen success creates no mandatory demand.
- Known unresolved safety risks: conservative `trash-source-order` and competing mandatory-target gates exclude cases not proven safe. Unsupported content graphs, tied positive shared Intrigue draw order and contested Mentat remain diagnosed. No unexpected-exception rollback guarantee is added.

## E. Runtime Versioning

- Old versions: setup snapshot v5; turn snapshot/replay v7; default support and original preview v13; lifecycle authoring v9; configuration identity v1, ruleset fingerprint v2, catalog identity, game result v1 and replay-identity algorithm v1.
- New versions: setup v5 unchanged; turn snapshot/replay v9; support and original preview v15; authoring v11; configuration v1, ruleset fingerprint v2, catalog, result v1 and replay-identity algorithm v1 unchanged.
- Changed serialized contracts: selected action/effect discriminators, private selected decision/target payload and trusted nullable `EffectContext.SourceOwnerId`; changed authored board execution. Intermediate development v8/v14/v10 also existed during Tasks 1/2 and are rejected.
- Replay compatibility: prior v7/support13 and intermediate v8/support14 or mismatched v8/support15 and v9/support14 headers reject; no migration or cross-runtime `System.Random` promise. Current replay uses the pinned runtime and exact rules/content identity.
- Fingerprint changes: configuration identity, ruleset fingerprint and embedded catalog are unchanged. Authored `RulesIdentity` changes with the new BoardRule; new headers/actions/rule identity change resulting snapshot/replay hashes while replay-identity algorithm stays v1.
- Tests proving version behavior: `LifecycleReplayTests` old/intermediate header rejection and JSON goldens; `LifecycleDefinitionTests` current authoring assertion; `SelectedTrashReplayPrivacyTests.StrictSelectionActionJsonRejectsUnknownTagsAndMembers`, `.StrictSelectionEffectJsonRejectsUnknownTagsMembersAndEnums` and `.EveryAcceptedPrefixReplaysSelectionReturnDrawAndTurnBoundary`.

## F. Coverage Changes

- `cap-selected-trash`: unsupported/not-tested with `cap-selected-trash-open` -> implemented/tested, resolved blocker closed; bounded owner-visible Hand/Discard/InPlay selected trash only.
- `cap-reserve-return`: unsupported/not-tested with `cap-reserve-return-gap` -> implemented/tested, resolved blocker closed; actual three Reserve definitions and proven finite-stock return.
- Selective Breeding placement slot: unsupported/not-tested with `placement-unsupported` -> implemented/tested; optional exchange descriptor, I9/I16/FAQ3 evidence, behavior refs and authored BoardRule reference added. `runtimeDefinition: false` still denotes absence from embedded catalog, not absence of executable BoardRule.
- `int-original-selected-trash-reserve`: unsupported/not-tested with `int-original-selected-trash-reserve-gap` -> implemented/tested, blocker closed for original Standard 3/4-seat interaction.
- Original board-space registry count: 19 -> 20 BoardRules; 22 known original board identities remain, with Foldspace and Secrets unauthored.
- Implemented ability count changes: 34 -> 35; unsupported 18 -> 17; partially implemented 3 and not implemented 4 unchanged; total known 59, unknown/unreadable applicable slots 121. Shared capabilities and interactions are separate from ability totals.
- Coverage schema changes: none; `dune-content-coverage-v1` retained.
- Canonical audit revision: `slice-004.5-d1-review-fix2-2026-10-25` -> `slice-005-batch-01-2026-10-25`; `repositoryRevision` assesses runtime/test revision `0baed8b486044c7b806fa928d7de26574e06454a`.
- Generated report status: `content:report` regenerated all five regions; `content:validate` confirmed inventory integrity and freshness. The five projections are inventory audit, ability coverage, interaction matrix, implementation batches and readiness gates.
- Authentic readiness state: all six configurations remain unready. Source/inventory, complete ability/interaction, initializer, full-game and RL gates are not promoted.

## G. Tests and Performance

- Focused tests: Task 1 selected-trash/replay/definition gate 94/94; Task 2 full Engine 645/645 plus 277-case required subset; Task 3 Selective Breeding/board 34/34, integration 88/88 and definition gate 33/33; Task 4 replay/privacy/integration 205/205 and Simulation 31/31. These task runs predate this final content/report change and are documented in their task reports.
- Regression tests: Task 4 added seven named tests with 16 discovered cases; every-prefix 3/4-seat selected/decline/ordinary/Reserve/duplicate/played-source branches; source Agent continuation after self-selection closes the Task 3 Minor. The Task 5 registry drift gate has 5/5 passes.
- .NET test results: `rtk proxy mise run check` passed Engine 670, Simulation 31, CLI 23; zero failed/skipped, Release build zero warnings/errors. `content:registry` passed 5/5.
- Python/content test results: `content:test` passed 79/79 after updating three baseline-dependent assertions. The initial run failed 3 of 79 solely on old 34/25 counts and a now-implemented Batch 01 mutation fixture; it was corrected and rerun. Full `check` pytest passed 92/92. No suite was disabled.
- Content validation: `content:report` exit 0; `content:validate` exit 0, reports current and inventory valid.
- `mise run check`: exit 0 via `rtk proxy`; toolchain/locks, build, .NET/Python tests, Ruff lint and C#/Python formatting completed.
- `mise run smoke`: exit 0; independent Toy seed42, winner player1, 8 turns, replay verified.
- 3-player lifecycle preview: exit 0, synthetic seed42/policy17, 10 rounds, `ConflictDeckExhausted`, winner P2, 537 accepted actions including StartRound (536 policy), replay identity `828098871da4fdb111705b642f8d9d7be26439f5c4702c003034c93023859bac`; placements63/deployments38/Combat Intrigue22/PostWin3/optional10/passes63.
- 4-player lifecycle preview: exit 0, same seeds, 10 rounds, `ConflictDeckExhausted`, winner P1, 667 accepted actions including StartRound (666 policy), replay identity `a2afa8bf89381b1323fcba134793d27979d231876696db20742076ca50aaef54`; placements88/deployments43/Combat Intrigue25/PostWin4/optional9/passes64. The historical SLICE-004 4-seat trace had 660 accepted actions and winner P3. New available board decisions changed the fixture policy path as well as rule/header identity; current accepted-prefix replay and integration tests pass. No old literal golden was edited.
- Benchmarks, if run: none for SLICE-005; no latency, throughput or allocation claim. Historical SLICE-004 measurements remain separate.
- Known failures or regressions: no unresolved failure in executed gates. Current 4-seat synthetic trace differs from the older trace as described; no authentic-play inference follows. Final whole-branch review is pending at this draft stage.

## H. Architectural Findings

- Existing components successfully reused: `TurnEngine.Execute`, `DecisionFrame`/`EffectExecutionFrame`, `ExecutionContinuation`, `CardZone.MoveTo`, seeded draw/refill, existing preflight/faction projections, `ObservationProjection`, strict `ConfigurationJson.Options`, `EpisodeRunner` and existing `LifecycleFixtureAgent`.
- New reusable primitives: `SelectedTrashEffect`, `SelectTrashCardAction`, `DeclineTrashAction`, frozen `SelectedTrashDecision`/`SelectedCardTarget`, semantic `TrashDestination`/`MoveTrashedCard`, bounded return stock credit, trusted nullable `SourceOwnerId`.
- Design limitations discovered: executing source owner cannot always be inferred from initiator or recipient (foreign Acquire child); bound source owner is required. Dynamic reacquisition with later executing-source self-trash and competing mandatory targets retain explicit conservative gates. No general branch solver or card-trigger system was introduced.
- Future card families benefiting from this implementation: sourced selected-trash consumers, actual Reserve-return operations and source/target-distinct nested decisions after their own face/source evidence and tests. Foldspace/Unload/copy mechanics receive no automatic promotion.
- Potential performance concerns: target enumeration and return-aware preflight may add work to decision boundaries; no SLICE-005 benchmark ran, so effect size is unknown and no optimization claim is made.
- Recommended follow-up architecture work: implement the next sourced Intrigue recycling batch within the existing stock/RNG/preflight boundary, then measure representative decision workloads only if a concrete performance choice arises.
- Ruling: use the same folder on `main` — explicit user preference and approved package; cost if wrong: branch placement must be adjusted without changing the tree.
- Ruling: retain the repository's mandatory-TDD override while writing focused correctness tests — direct user/repository instruction; cost if wrong: test sequencing differs from the generic Superpowers cycle, though required evidence still ran.
- Ruling: add trusted nullable `EffectContext.SourceOwnerId` at `RootContext`/`Acquire` — the valid foreign Acquire/self-trash example has source owner distinct from initiator and recipient; cost if wrong: a redundant trusted scalar and extra snapshot/replay incompatibility, without policy exposure.
- Ruling: advance intermediate snapshot/replay8 -> 9, support/preview14 -> 15, authoring10 -> 11 for the new persisted source owner — avoids silently sharing Task 1 headers; cost if wrong: extra intermediate-version churn and golden updates. Baseline v7/v13/v9 also reject.

## I. Remaining Content Blockers

- Missing original board-space effects: Foldspace and Secrets remain unauthored; the audited original board has 20 authored of 22 known identities, not a complete legal action set.
- Missing card faces: obscured starter boxes, Foldspace's unreadable Agent/Reveal face, incomplete Imperium/Intrigue pools and card-specific effects remain gated. Routing a card instance does not certify its face.
- Missing Leader effects: all official passive/Signet and complete setup contracts remain unready; synthetic leaders are fixture-only.
- Missing Conflict content: official Conflict registry remains empty and real classifications/numeric rewards are incomplete; the ten-Conflict scenario uses invented rewards.
- Missing Intrigue mechanics: shared exhaustion recycling, Secrets theft and unsupported Plot/other faces remain; current Batch 02 precedes Secrets.
- Unresolved source rules: tied positive shared Intrigue draw order and arbitrary simultaneous conditional Influence chains remain unresolved. Alliance holder loss is source-resolved by FAQ p.1 (holder below four, prior tied transfer and holder choice), but its full handler is still partially implemented/not-tested. FAQ p.4 sources named deferred Mentat timing; the full Conflict face and general contested ownership remain gated. Unsupported composite/copy/expansion cases retain their own evidence and implementation gates.
- Authentic-game readiness blockers: incomplete physical catalogs and abilities/interactions block initializer, full-game and RL readiness in all six configurations. Synthetic completion and coverage percentages do not bypass those gates.

## J. Recommended Next Slice

1. Recommended slice title: Shared Intrigue exhaustion recycling.
2. Canonical batch ID: `batch-02-intrigue-recycle`, second in the audit, with no prerequisite batch; `batch-03-secrets` depends on it.
3. Exact scope: `cap-intrigue-recycle`, `imperium:space:carthag:v1#slot:placement`, `imperium:space:conspire:v1#slot:placement`, `int-original-intrigue-recycling` for the sourced original 3/4-seat subset.
4. Required source evidence: publisher FAQ revised 2025-01-13 p.2 on Intrigue discard recycling; retain A p.2 for existing Carthag/Conspire board effects and I original placement/timing where applicable. Verify local/source hashes and exact exhaustion/reshuffle wording before implementation.
5. Relevant existing engine interfaces: `TurnEngine.LegalActions`, `Step`, `ValidateRemaining`, `ValidateIntrigueReservations`, `Execute`, shared Intrigue deck/discard zones, seeded RNG and `CaptureReplay`/`Replay`.
6. Missing mechanics to implement: on empty shared Intrigue deck, recycle eligible discard with engine RNG shuffle and continue the draw; account for finite stock and generated/suspended mandatory draws. Current direct Intrigue draw with remaining deck stock stays intact.
7. Important dependencies: none at the batch level; preserve current card-zone conservation, privacy and typed causal child ordering. Secrets follows recycling, while source-complete face and tied-recipient decisions are separate gates.
8. Essential validation scenarios: nonempty discard exhaustion and seeded order; empty deck plus empty discard rejection before mutation; simultaneous/suspended/generated draw demand; Carthag/Conspire cost/effect order; 3/4-seat legal/Step agreement, replay and unchanged Toy/lifecycle suites.
9. Replay/privacy risks: shuffle uses only episode RNG and must replay exactly; rejected placement must preserve RNG/history. The shuffled shared Intrigue deck order and resulting private Intrigue faces must stay hidden; preserve existing public `Shared.IntrigueDiscard` visibility and avoid leaks through legal masks or support diagnostics.
10. Explicit exclusions: Secrets theft, tied positive shared-recipient draw order without sourced ruling, Plot and other unreadable Intrigue faces, expansion rules, authentic full-game/RL promotion and unrelated performance optimization.
11. Acceptance criteria: FAQ-backed exhaustion semantics; deterministic successful recycle/draw and immutable rejection when no stock; bounded Carthag/Conspire abilities and interaction evidenced; generated/suspended preflight, privacy and every-prefix replay pass; canonical audit/report updated without prematurely declaring any configuration ready.

## K. Final Assessment

1. Is Selective Breeding fully implemented according to verified official rules? **Yes for its verified original Imperium placement slot**, including mandatory cost/Influence and optional one-use trash-for-draw; this is not a whole-game claim.
2. Does selected-card trashing work through the existing action system? **Yes**, through the typed selected/decline actions, `LegalActions`, `Step` and existing effect frames.
3. Are Reserve cards returned correctly? **Yes for actual supported Reserve definitions**, to their exact stacks with physical conservation and bounded stock credit; unsupported faces remain gated.
4. Are privacy and deterministic replay preserved? **Yes for the executed 3/4-seat synthetic scenarios and focused counterfactuals**, on the pinned runtime/current headers.
5. Were existing lifecycle scenarios preserved? **Yes as executable, replay-verified synthetic scenarios**; the 4-seat policy trace and winner changed under the newly available board rule, as recorded in G.
6. Did canonical coverage measurably improve? **Yes:** implemented known abilities 34 -> 35, unsupported 18 -> 17, BoardRules 19 -> 20, while total known abilities stay 59.
7. Is any authentic original Imperium configuration playable yet? **No**; all six audited configurations remain unready.
8. What is the most important risk for the next batch? **Shared Intrigue recycle shuffle/stock preflight leaking hidden order or consuming RNG on a rejected action**; source-backed exhaustion semantics and 3/4-seat privacy/replay are the gate.

# END SLICE-005 RESULT FOR CHATGPT
