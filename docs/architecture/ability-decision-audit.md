# SLICE-003.5: ability and decision architecture audit

Audited 2026-10-24 against revision `3082e3b` (initial working tree clean). This is
a documentation-only assessment. No Combat, KH, Graft, expansion runtime, or card
catalog implementation is included. The [handoff](../handoffs/SLICE-003.5-result.md)
is the self-contained external review result; [execution design](ability-execution-design.md)
and [case studies](complex-ability-case-studies.md) specify proposed extensions.

## Assessment and evidence conventions

Retain the existing engine. It already has incremental actions, private projections,
typed effects, explicit decision ownership, and replay of suspended decisions.
It is **not ready to implement Combat by simply adding more effect subclasses**.
The immediate prerequisites are phase-independent continuation, explicit timing
windows, distinct decision/recipient/source context, and validation of those new
operations before mutation. General catalog coverage follows those prerequisites.

`Supported` below means working behavior demonstrated by inspected execution and
tests, within the existing synthetic/partial preview. It never implies authentic
catalog completeness. `Partial` identifies operational subsets; `Missing` means no
suitable executable implementation. No row remains `Unknown` merely because its
code was not inspected. Unresolved official interpretations are separately marked.

Rule citations use the exact PDF keys/one-based page convention in
[rules-index.md](../rules-index.md). This file primarily reports code behavior;
official-rule interpretations are in the cited case studies. All proposals are
unimplemented. Tests were inspected before the final check; execution results live
in handoff section I.

## Actual subsystems

Paths below are relative to the repository root. `Turns/` abbreviates
`src/Dune.Engine/Turns/`; `Runtime/` abbreviates `src/Dune.Engine/Runtime/`.

| Subsystem and files                                                                                            | Actual API and responsibility                                                                                                                                                                                               | Verified behavior / limitation / risk                                                                                                                                                                                                                                                                                 | Minimum recommendation                                                                                                                                                                                  |
|----------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Runtime ownership: `Runtime/GameState.cs`, `PlayerState.cs`                                                    | `GameState.Observe(int)`, `CaptureTrustedSnapshot()`; internal setters on state; `PlayerResources`, `PlayerUnits`, `TemporaryPlayerState`                                                                                   | Per-game mutable state, read-only collection views, immutable value records. State remains privileged even through public getters. `ActiveEffect(ContentId Source, string EffectId)` and `ModuleExtensionState` are placeholders, not executable modifiers. `IsCompleted` is never set by TurnEngine.                 | Add typed window/continuation state with explicit snapshot/privacy ownership; do not turn placeholder strings into an interpreter.                                                                      |
| Board and physical cards: `Runtime/BoardState.cs`, `CardZone.cs`; `Content/ContentDefinition.cs`               | `AgentPlacement(int PlayerId, AgentKind Kind)`; `CardInstance(CardInstanceId Id, ContentId Definition)`; internal `MoveTo`, `TryDrawTo`, `Shuffle`                                                                          | Physical card identity survives moves; card conservation checked. Agents have owner/kind, no physical ID; placement always adds `AgentKind.Agent`. Counts alone cannot select an existing Agent or preserve Mentat provenance. Raw transfers have no semantic discard/trash notifications.                            | Reuse zones, add semantic card operations at trigger integration; use an opaque placement selector and add Agent identity only if required.                                                             |
| Phase/scheduler: `Turns/TurnEngine.cs`, `Runtime/GameState.cs`                                                 | `StartRound()`, private `BeginRound`, `NextPlayer`, `EndAgentTurn`, `EndReveal`                                                                                                                                             | Only `BeforeRoundStart`, `PlayerTurns`, `CombatBoundary`. Start accepts only round 0; begin writes round 1; next skips Revealed seats and clears both owners at CombatBoundary. `CurrentPlayerId` and `DecisionOwnerId` already differ for nested foreign work.                                                         | Keep turn scheduling separate from a Combat window cursor. Generalize next-round initialization and phase completion; retain nullable active player outside a normal turn.                              |
| Actions: `Turns/GameAction.cs`                                                                                 | Eight explicit `$action` variants; `StepResult` with Applied/Illegal/Unsupported/ContentError                                                                                                                               | Placement, Reveal, pending effect/option, deploy, acquisition, end Reveal, trusted round start. No Intrigue/pass/end-window/relocation/Graft/target-card action. `StartRoundAction` is recorded but never policy-legal.                                                                                               | Add only needed typed variants to this hierarchy; no separate Combat action engine.                                                                                                                     |
| Legal enumeration/validation: `Turns/TurnEngine.cs`                                                            | `ImmutableArray<GameAction> LegalActions(int)`; `SupportDiagnostics(int)`; private `Candidates`, `Validate`, `Preflight`, `ValidateContext`                                                                                 | Candidates are filtered through the same validator used by `Step`; deterministic selector/space ordering; inactive viewers receive empty arrays. Phase gate hardcodes PlayerTurns; validation assumes nonnull active player. Preflight recursively checks all choice/optional branches and aggregate stock/influence. | Phase/window dispatch before accessing active turn; compose placement checks individually; retain shared validation path and explicit unsupported diagnostics.                                          |
| Execution: `Turns/TurnEngine.cs`                                                                               | `StepResult Step(GameAction)`; private `Place`, `Reveal`, `Execute`, `Choose`, `Acquire`, `ContinueTurn`, `Enqueue`, `Gain`, `Pay`                                                                                          | No standalone `EffectResolver` type exists. A bounded effect-type switch executes reusable operations, without a card-name switch. Meaningful effect order is selectable; some deterministic gains drain automatically.                                                                                               | Extend this resolver first. Extract a cohesive internal helper only if needed while preserving one ownership/continuation mechanism.                                                                    |
| Pending work: `Turns/TurnSnapshot.cs`                                                                          | `EffectContext(int OwnerId, CardInstanceId? SourceCard, BoardSpaceId? Space, int AcquisitionDepth=0)`; `PendingEffect(int Id, Effect Effect, EffectContext Context)`; `DecisionFrame(Effect Effect, EffectContext Context)` | Pending list plus option stack. Optional decisions transfer OwnerId, which also becomes payer and beneficiary. `ForPlayerEffect` transfers ownership of child effects. AcquisitionDepth prioritizes acquire chains only; ordinary child effects rejoin the same unordered list.                                       | Separate chooser/payer/recipient/target and causal group from source. Add resumable sequence/order groups; replace acquisition-only priority with general child-before-parent continuation.             |
| Executable content: `Turns/TurnRules.cs`, `OriginalImperiumTurnRules.cs`                                       | `CardRule`, `BoardRule`, `LeaderRule`, `TurnRuleSet`; `OriginalImperiumTurnRules.Create()`                                                                                                                                  | Twelve card identities and fourteen board rules in sourced preset; several boxes are `UnsupportedEffect`. Leaders array empty; all official leaders rejected even if `FullySupported=true` without Synthetic. Card arrays are order choices, not strict sequences. No persistent passive handler exists.              | Preserve per-box support; add verified typed predicates/special handlers as evidence requires; no class per ordinary card.                                                                              |
| Composition: `Rulesets/RulesetComposer.cs`, `RuleModule.cs`, `ResolutionResult.cs`, `RulesetFingerprint.cs`    | `ResolutionResult Compose(GameConfiguration?)`; immutable `ResolvedGameDefinition` with Fingerprint/Modules/Definitions/Content/EligibleLeaders/Setup                                                                       | Deterministic dependency graph, explicit replacement provenance, seed-independent fingerprint v2. Runtime initializer and TurnEngine reject expanded/custom recipes. `Gameplay` always NotImplemented. TurnRuleSet is separately supplied, hashed, not composed from metadata.                                        | Eventually lower supported executable rules once from resolved modules into immutable tables. Explicitly gate interactions and preserve separate composition/setup/gameplay readiness.                  |
| Setup/content: `Setup/GameInitializer.cs`, `GameSetupDefinition.cs`, `Content/ContentCatalog.cs`               | `InitializationResult Create(GameConfiguration?)`, `Initialize(GameConfiguration?, ResolvedGameDefinition?)`; internal `InitializeFixture`                                                                                  | Base Standard competitive 3/4 only. Bundled representative inventories fail public authentic initialization. Complete labels do not bypass quantity/source checks.                                                                                                                                                     | Preserve authenticity gates; future capability compiler must not promote metadata-only entries to executable cards.                                                                                     |
| Observation: `Runtime/GameObservation.cs`, `Turns/GameAction.cs`, `TurnEngine.Observe(int)`                    | `GameObservation`, `TurnObservation(GameObservation Game, ImmutableArray<ObservedPendingEffect> PendingEffects, PendingDecisionSummary? Decision, ImmutableArray<GameAction> LegalActions)`                                 | Detached values; owner sees hand/Intrigue, public zones projected, pending payloads hidden. Visible selectors use definition+duplicate index rather than allocation-range physical IDs. Decision kinds/labels are descriptive, not typed encodings.                                                                   | Add typed public decision/window IDs and permitted targets; maintain owner-only choices and sanitize future labels/diagnostics. Opaque instance handles only when duplicate copies gain distinct state. |
| RNG/replay/hash: `Runtime/SeededRandom.cs`, `TrustedGameSnapshot.cs`, `Turns/TurnSnapshot.cs`, `TurnEngine.cs` | Internal seeded streams, `CaptureReplay()`, `static TurnReplayResult Replay(TurnReplay, Func<GameState>, TurnRuleSet)`                                                                                                      | Main/leader streams per episode, count-based checkpoint, stable JSON SHA-256, initial factory plus accepted actions reconstruct pending execution. Full trusted hash includes history; no public snapshot import/clone. Module extension snapshot/observation contracts absent and rejected.                          | Include every new cursor, source binding, usage counter, trigger and modifier lifetime in versioned trusted snapshots/replay. Do not use the history-dependent hash as a search transposition key.      |
| Simulation boundary: `src/Dune.Engine/IGame.cs`, `src/Dune.Simulation/EpisodeRunner.cs`                        | Generic Observe/LegalActions/Step/terminal/result contract                                                                                                                                                                  | Toy implements the complete boundary. TurnEngine does not implement IGame, lacks Reset/GetResult/IsTerminal, and is not a complete Dune training environment.                                                                                                                                                         | Keep conceptual Observe -> LegalActions -> Step; adapt only after terminal and content contracts are established.                                                                                       |

## Capability matrix

Evidence anchors: **V**=`TurnEngine.Validate/Preflight/ValidateContext`; **E**=`Execute/Choose/Gain/Pay/Acquire`; **Q**=
`Candidates/ContinueTurn/Enqueue`; **S**=`TurnSnapshot.cs`; **R**=`TurnRules.cs`.
All are under `src/Dune.Engine/Turns/`. Behavioral evidence follows the matrix.

| Capability                      | Status    | Actual evidence and limitation                                                                         | Recommended change                                                                   |
|---------------------------------|-----------|--------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|
| Gain resources                  | Supported | E: nonnegative Solari/spice/water, Persuasion/swords and VP                                            | Reuse typed primitive.                                                               |
| Spend resources                 | Supported | V/E: mandatory placement/option cost and optional costs, affordability before Pay                      | General standalone loss only when needed; keep payer explicit.                       |
| Draw cards                      | Supported | E + `CardZone.TryDrawTo`: draw/refill/shuffle                                                          | Reuse, retain deterministic RNG and timing.                                          |
| Discard cards                   | Partial   | EndReveal transfers all in-play/hand to discard; no selected discard cost/effect                       | Add semantic selected-card operation; distinguish cleanup discard reason.            |
| Trash cards                     | Partial   | TrashSelf moves exact SourceCard, any current zone, once absent from trash; no arbitrary target choice | Add allowed-zone/target selection and notification.                                  |
| Acquire cards                   | Supported | Public row/Reserve purchase and named `AcquireEffect`; discard destination/refill/acquire children     | Broader markets/destination replacements need hooks.                                 |
| Recruit troops                  | Supported | Finite supply -> garrison, recruited count tracked for active turn                                     | Reuse, future unit kind must remain explicit.                                        |
| Deploy troops                   | Supported | Explicit count0..recruits+up to 2 old troops at Combat board space                                      | No arbitrary deployment effect or Reveal combat icon.                                |
| Gain Influence                  | Partial   | Nonnegative faction gain, threshold 2 VP; V blocks reaching 4                                            | Implement complete alliance/bonus thresholds before full games.                      |
| Lose Influence                  | Missing   | V rejects negative InfluenceEffect                                                                     | Dedicated loss semantics including threshold/Alliance changes.                       |
| Gain VP                         | Supported | GainEffect and Influence threshold                                                                     | Reuse.                                                                               |
| Lose VP                         | Missing   | V rejects negative gain                                                                                | Typed loss with verified lower-bound semantics.                                      |
| Sequential effects              | Partial   | Arrays enqueue into selectable pending list; acquisition children have special priority                | Explicit strict sequence vs unordered choice group.                                  |
| Conditional effects             | Partial   | Fixed checks for influence/payment/leader/context only; no typed general condition node                | Add a small evidenced predicate set, evaluate at defined timing.                     |
| Optional effects                | Supported | OptionalPaymentEffect can cost zero; decline/pay choices, nested children                              | Separate chooser, payer and recipient.                                               |
| Multiple choices                | Supported | ChoiceEffect, per-option ResourceCost and children                                                     | Reuse affordable-options enumeration; define empty feasible-choice handling.         |
| Player-selected effect ordering | Supported | ResolveEffectAction chooses owned pending work; Reveal purchases interleave                            | Preserve legal freedom within explicit order group, not across interrupt boundaries. |
| Additional payment              | Supported | Optional/choice/board PlacementOptions                                                                 | Card/unit costs need new typed costs and decisions.                                  |
| Different-player effects        | Partial   | ForPlayerEffect/optional owner handoff work for fixed seat IDs                                         | Relative/all-opponent targeting, ordered victims and distinct beneficiary missing.   |
| Nested decisions                | Partial   | Option frames/child effects resume, acquisition nested priority tested                                 | General continuation and strict parent suspension missing.                           |
| Triggered effects               | Partial   | Explicit AcquireEffects and board Reveal effects; Signet is explicit invocation                                      | No reusable semantic-event subscriptions or triggered child scheduling.              |
| Replacement effects             | Missing   | No pre-operation replacement query                                                                     | Narrow typed hook when an evidenced operation needs it.                              |
| Temporary rule modifiers        | Missing   | TemporaryPlayerState stores totals; ActiveEffect strings never consulted                               | Typed scope/source/expiration, no global ignore-restrictions flag.                   |
| Persistent rule modifiers       | Partial   | High Council/Swordmaster hardcoded; generic passive/Skill/Tech modifier absent                         | Immutable rule descriptors + per-game acquisition/usage state.                       |
| Once per turn                   | Missing   | No turn serial or usage ledger                                                                         | Typed source+ability+turn usage key.                                                 |
| Once per round                  | Missing   | No ability ledger; only first round starts                                                             | Round serial/usage and explicit reset.                                               |
| Once per game                   | Partial | UsedSpace set and permanent-upgrade guards                                                             | Preserve across rounds and relocation; general ability usage not yet present.        |
| Special action eligibility      | Partial   | Costs/icons/influence/capacity gates only                                                              | Placement plan queried before ordinary unused-Agent rule.                            |
| Agent relocation                | Missing   | Action has no origin; Place only decrements unused pool/adds occupant                                  | Atomic move/recall operation with source selector and provenance.                    |
| Selective restriction bypass    | Missing   | Icon+capacity combined check; Capacity is static                                                       | Independent icon/occupancy/access/cost/ban checks, scoped typed permissions.         |
| Copy card ability               | Missing   | No related-source binding/copy node                                                                    | Definition-of-ability separate from physical executor; cycle guard.                  |
| Discard/trash triggers          | Missing   | Raw transfers/self-trash do not emit semantic occurrences                                              | Per-occurrence trigger + child continuation; cleanup reason explicit.                |
| Additional actions/turns        | Missing   | NextPlayer always clockwise skipping Revealed                                                          | Explicit sourced continuation/extra-turn scheduling, deferred until needed.          |
| Cross-player reactions          | Missing   | Foreign optional choice is not a shared reaction window                                                | Reuse typed windows and existing action/decision boundary.                           |
| Delayed effects                 | Missing   | No timing/lifetime queue; ActiveEffects inert                                                          | Typed future timing/source binding when needed.                                      |
| Combat decision windows         | Missing   | LegalActions rejects outside PlayerTurns; no Intrigue/pass actions                                     | Add typed multi-player priority state and resume path.                               |
| Cross-expansion interactions    | Partial   | Sourced static modules/replacement provenance work; runtime rejects expansions                         | Compose executable capabilities once; explicit interaction tests.                    |
| Deterministic replay            | Supported | S + Replay rebuild and hash existing pending state                                                     | Pinned runtime/support only; no clone/import; version all extensions.                |

## Behavioral evidence and limits of the tests

`tests/Dune.Engine.Tests/TurnEngineTests.cs` directly exercises:

- placement atomic rejection, no-Agent eligibility, occupancy/access, once/game:
  `PlacementChecksAccessCostsOccupancyInfluenceAndOncePerGameBeforeMutation`,
  `OccupancyAndCardAccessFailuresAreImmutableAndDoNotLeakHiddenCards`;
- primitive/order/payment/deploy/reveal behavior:
  `EffectOrderAllowsBoardGainToFundOptionalCardPayment`,
  `DeploymentAllowsRecruitsAndOnlyTwoOldGarrisonTroops`,
  `RevealUsesOnlyHandAndAllowsPurchasesBetweenEffectsWithAcquireRewardsAndRefill`,
  `SelfTrashMovesOnlyItsPhysicalSourceAndPreservesConservation`;
- foreign and nested ownership:
  `NestedOptionalDecisionsPreserveOwnerIndependentOfActivePlayer`,
  `ForeignOnlyRevealEffectsHandOffAndReturnToActiveRevealWithReplay`,
  `ForeignOnlyAcquireEffectsHandOffAndReturnToActiveRevealWithReplay`;
- causal acquire priority and stock safety:
  `AcquireOptionalDecisionBlocksOuterRevealEffectsAndPurchasesUntilResolved`,
  `NestedAcquireGainCompletesBeforeParentAcquireDecisionAndOuterReveal`,
  `InterleavedPurchasesPreserveStockReservedForPendingMandatoryGrants`;
- unsupported graphs/contexts/thresholds and corruption:
  `UnsupportedNestedGraphsFailBeforeCostsCardsRandomnessOrHistoryChange`,
  `InvalidEffectContextAndNegativeCostsReportContentErrorsBeforeMutation`,
  `InfluenceCrossingTwoAwardsVpAndUnsupportedAllianceThresholdIsAtomic`,
  `InvariantCorruptionThrowsInsteadOfMasqueradingAsIllegalInput`;
- replay/privacy/isolation:
  `DeterministicReplayBindsRulesRuntimeConfigSeedsAndPendingDecisions`,
  `LegalEnumerationAndObservationsAreReadOnlyPrivateAndDoNotExposePhysicalIds`,
  `DrawRecyclesDiscardDeterministicallyAndEpisodesRemainIsolated`.

`OriginalImperiumTurnRulesTests.cs` tests sourced board/card examples; synthetic
fixture identities/leaders deliberately do not validate a full card catalog.
`RulesetCompositionTests.cs`, `SetupDefinitionTests.cs`, `GameInitializerTests.cs`,
`CardZoneTests.cs` and `SeededRandomTests.cs` establish static composition, setup,
projection, transfer and RNG contracts. No test currently proves Combat, KH,
Graft, Unload, general pass windows, persistent modifiers or full-game completion.

## Determinism, safety and architectural risks

1. **Continuation is the blocking structural gap.** `Choose` pops its frame and
   appends children at the same AcquisitionDepth. Foreign work can be deferred
   behind active-player siblings. That is tested current behavior, not a general
   guarantee that an effect and its consequences finish before outer work. A strict
   sequence or interrupt must suspend its parent explicitly.
2. **Decision owner also means recipient/payer.** OptionalPaymentEffect's
   DecisionOwnerId changes the children's OwnerId; ForPlayerEffect does the same.
   A victim choosing a discard, with a reward for the initiator, needs two roles.
   SourceCard is singular, Space is singular, and neither captures related/copy
   origin or ability timing. Retain source identity even after its card moves.
3. **Validation is deliberately conservative, not transactional.** All optional
   and choice branches are preflighted, including stock demand and unsupported
   thresholds. Safe preview exclusion can become an incomplete official action
   set. Runtime-sensitive branches must eventually validate at their decision
   boundary without losing initial atomic support checks. Do not silently weaken
   the existing stock protection.
4. **Post-validation exceptions can leave mutation.** Step mutates, appends history,
   then checks invariants; there is no rollback. A source trace of two
   SwordmasterEffect nodes can pass type preflight, decrement the unacquired count
   twice, then throw after mutation. This malformed-definition trace was inspected,
   not executed in a new test. Validate definition shape/permanent-operation limits
   once and dynamic prerequisites immediately before commitment; keep invariant
   exceptions as engine defects rather than successful/illegal actions.
5. **General modifier lifetime is absent.** ActiveEffects are included in trusted
   data but never executed or expired. A future Voice ban needs creator/player,
   destination, expiration before the creator's next turn, and snapshot ownership (I p.13); round-reset booleans alone
   cannot express that.
6. **Hash/replay boundary is sound for current behavior.** Canonical order, version
   checks, accepted history, RNG checkpoints and pending frames are captured. New
   window state/usage/targets must be included; otherwise distinct futures can hash
   identically. Snapshot hash includes action history and is a diagnostic replay
   identity, not position equivalence. Seeded System.Random remains scoped to the
   pinned runtime
   ([Microsoft documentation](https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-random)).
7. **Episode isolation must remain explicit.** Definitions are immutable; mutable
   lists, counters, RNG and execution queues are per engine/game. A single engine
   is sequential, not thread-safe. Do not share one GameState between wrappers or
   cache mutable handler/usage state in shared resolved definitions.

## RL action boundary

The conceptual Observe -> LegalActions -> Step loop fits incremental decisions.
Every new supported choice must be generated and accepted by the same validator;
unsupported cards/timing must remain excluded with honest support diagnostics.
Do not infer legality from an enum or expose a zero-effect placeholder.

Existing `CardSelector(ContentId, Copy)` and pending EffectId are reproducible for
the same visible state/action prefix, not a global fixed neural action vocabulary.
Provide a future versioned structured encoding (action kind + permitted card/space/
target/count/option + decision ID) with masks aligned to the current owner's legal
array. Variable arrays suit incremental target/order/cost selections; avoid
card-pair x origin x destination x all-effect-order products. Multi-agent training
needs DecisionOwnerId, not a normal-turn seat guess. Batched games can be at
different phases and nested decisions; batch by decision kind if measurement
justifies it. No encoder, RL algorithm, clone API or Python bridge is added here.

Drain deterministic work internally when there is no strategic ordering or Reveal
purchase interleave. Today some single Optional/ForPlayer/Acquire nodes still need
ResolveEffectAction merely to open the next decision. Remove those administrative
choices only with replay-version review; preserve actual order choices. Future
labels, diagnostics and target lists must be tested against nonempty opponent
hands and private Intrigue so action masks cannot leak hidden information.

## Performance assessment

No benchmark code changed and no measurements were taken in this slice. Existing
[SLICE-003 ShortRun](../benchmarks/slice-003.md), on its synthetic 3-player fixture,
reported legal enumeration 36.17 us / 97.78 KB, Round Start 24.06 us / 19.91 KB,
placement 23.00 us / 21.22 KB, optional resolution 19.02 us / 19.88 KB. These are historical
local measurements, with short iterations and few samples, not full-game throughput.

Source explains candidate creation/repeated recursive preflight, pending scans,
LINQ/materialization, per-Step whole-card conservation scans and history growth.
There is no measurement isolating virtual dispatch or proving a custom interpreter,
allocator, PRNG, reflection dispatcher or event framework would help. Ordinary typed
records/switch dispatch and BCL collections remain the simplest library-first choice.

After prerequisites work, compare baseline vs explicit continuation/scoped query
using existing BenchmarkDotNet: unchanged legal placement enumeration; a depth 3
nested foreign decision; Combat pass/play/resume with 3/4 seats; modifier query with
0/1/8 relevant modifiers. Separate setup from measured operations, match accepted
actions/state outcomes, record allocations/GC and runtime/hardware. Run Dry to
verify harness, then a representative statistical job. Compile static support and
lookup tables once; measure before caching mutable legality or removing checks.
