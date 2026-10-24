# Rule sources and implementation ledger

The supplied local PDFs in `docs/rules/` are the primary mechanics authority.
[The rules index](rules-index.md) identifies all seven documents, SHA-256
checksums and every page. All references below use **1-based PDF page numbers**.
[The compatibility matrix](compatibility-matrix.md) separates official
compatibility from implemented composition scope and records unresolved questions.
Do not substitute older external links for these supplied editions.

SLICE-001 implements **representative setup metadata resolution**, not playable
Dune mechanics. Every Dune result has gameplay NotImplemented; catalog coverage is
Representative. The playable token game in `Dune.Engine.Toy` remains invented and
separate. No real card effects or turn transitions are implemented. SLICE-002 now verifies
the complete starter/Reserve metadata and executes fixture-backed base setup, as
recorded separately below; the SLICE-001 tests prove metadata composition only.

| Rule/contract | Local source and page | Implemented interpretation | Code and verification |
| --- | --- | --- | --- |
| Original/Uprising setup identities | I pp.3–6; U pp.3–6 | Distinct bases, board/deck recipe identities, ordinary target; no cross-base union | Engine Rulesets builtins; ruleset composition tests |
| Original Ix overlay | X p.3 | Replace original board-region recipe, add Ix mechanics metadata | Engine Rulesets builtins; original Ix resolution test |
| Uprising Ix overlay | U p.18 | Folded overlay and virtual observation-post recipe, distinct from original | Engine Rulesets builtins; Uprising Ix resolution test |
| Immortality starting deck | M p.5 | Replace two Dune, the Desert Planet with two Experimentation in metadata | Engine Rulesets builtins; Immortality recipe test |
| Epic | X p.10 | Separate target/conflict/starting-resource/deck recipe selections | Engine Rulesets builtins; Epic composition test |
| Immortality + Epic | M p.12 | Explicitly replace Epic deck instruction with Control the Spice in discard | Engine Rulesets interaction module; conditional override test |
| Go to 11 | M p.12 | Variant changes target and four-player start; limited supported scope | Engine Configuration/Rulesets; player-count variant test |
| CHOAM opt-in | U p.16 | Module-gated content, contracts and leader availability | Engine Content/Rulesets; availability tests |
| Bloodlines setup and Tech | B pp.3,6–7 | Conditional setup metadata and optional Tech gate; scoped combinations only | Engine Content/Rulesets; module gating tests |
| Leader policy validation | I pp.2,4; U pp.2,4; X p.3; B pp.3,6 | Fixed/random policy descriptions; sequential/snake drafts explicitly custom policies | Engine Configuration; invalid/eligible leader tests |
| Unsupported formats | I pp.19–20; S pp.3–14; B pp.8–10 | Recognized but no metadata resolution of these distinct format procedures | Configuration diagnostics; unsupported-format tests |

I/A/X/M/U/S/B abbreviations resolve to exact local filenames in the
[index](rules-index.md#citation-convention-and-document-inventory).
The concrete source references also travel with builtin definitions and content.
Tests live in [`RulesetCompositionTests.cs`](../tests/Dune.Engine.Tests/RulesetCompositionTests.cs)
in the existing engine test project. Named checks include
`ImmortalityReplacesBothStartingCardsAndEpicHasExplicitOverrideHistory`,
`IxUsesDifferentOverlayOnEachBaseAndUprisingVirtualPosts`,
`BloodlinesTechIsOptInAndExcludesChoamTransportsWithoutChoam`,
`GoTo11AccountsForFourPlayerException`, and
`EveryPresetResolvesMetadataAndExplicitlyRemainsUnplayable`. The
[composition architecture](architecture/ruleset-composition.md) describes the API.

Add ledger entries and focused tests when implementing real setup/effects. Audit
applicability, quantity, hidden-information behavior and conditional overrides;
metadata tests cannot stand in for gameplay correctness. Keep uncertain behavior
excluded, explicitly reject unsupported combinations, and version changed semantics.
Do not commit copyrighted rulebooks/card scans or copied effect prose without rights.

## SLICE-002 executable setup ledger

The rules below initialize a state **before** Round Start. This does not make Dune
playable. Full authentic setup remains content-gated; behavior tests use explicitly
synthetic identities for missing pools. I is
`DUNE_IMPERIUM_Rules_2020_10_26.pdf`; A is
`DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf`.

| Implemented setup rule | Official source (1-based PDF page) | Runtime interpretation | Behavioral tests |
| --- | --- | --- | --- |
| Starting deck | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [I p.5 E](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | 2 Convincing Argument, 2 Dagger, 1 Diplomacy, 2 Dune the Desert Planet, 1 Reconnaissance, 1 Seek Allies, 1 Signet Ring; shuffle each player's ten copies | SetupDefinitionTests; GameInitializerTests |
| Resources and bank | [I p.5 F1–F2](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | One water, zero spice/Solari; unlimited bank, no physical-token scarcity | GameInitializerTests |
| Agents/Swordmaster | [I p.5 G1](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | Two available Agents; third unacquired, none deployed | GameInitializerTests |
| Victory points | [I p.5 G2](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | 3p starts at zero; 4p starts at one | GameInitializerTests; SetupDefinitionTests |
| Influence, troops, control and combat marker | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [I p.5 G3–G6](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | Four zero influence cubes; twelve troops (three garrison, nine supply); three available control markers; no control/High Council seat; combat strength zero | GameInitializerTests |
| Board, Mentat and alliances | [I p.4 A1–A2](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4), [I pp.17–18](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=17), [A p.2](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=2) | 22 named spaces with stable IDs; no player occupancy/control/bonus spice; Mentat on Mentat; four unowned tokens | GameInitializerTests; SetupDefinitionTests |
| Leader assignment | [I p.4 D](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4) | Fixed or seeded random, one eligible unique leader per seat; no restriction by house. Draft policies explicitly unsupported | GameInitializerTests; existing composition eligibility tests |
| First player | [I p.5 H](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=5) | Uniform random seat from main seeded RNG; leader seed is separate | GameInitializerTests |
| Shared pools/Reserve | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [I p.4 C](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4) | 67 Imperium shuffled into 62-card deck + five visible row; 40 Intrigue shuffled; Reserve 8/10/6 | GameInitializerTests; SetupDefinitionTests |
| Conflict selection/order | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [I p.4 B](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4) | Available tiers 4/10/4; independently shuffled within tiers, active top-first 1 I + 5 II + 4 III; eight unused hidden outside game | GameInitializerTests |
| Setup boundary | [I p.8](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=8) | No hand draw or Conflict reveal during setup; phase BeforeRoundStart, internal round index zero, no decision owner | GameInitializerTests |
| Ownership and hidden information | [I pp.6–7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=6), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Deck order private from all, hand only owner; acquired Intrigue face down, public resources/units/discards; explicit immutable projection | GameInitializerTests; CardZoneTests |
| Empty personal deck refill | [I p.6](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=6), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Reusable draw primitive reshuffles matching owner's discard only when required; not yet a gameplay action | CardZoneTests |

Round zero is an engine indexing convention, not an additional official phase.
The seeded random algorithm, separate leader seed and fixture path are engineering
policies. Officially unused Conflicts are returned without inspection; trusted
snapshots retain them for conservation/debugging and observations never disclose
their identities. Card/leader effects, including setup overrides on individual
components, remain unimplemented and must be audited before real content is enabled.


SLICE-002 behavioral tests are in
[`GameInitializerTests.cs`](../tests/Dune.Engine.Tests/GameInitializerTests.cs),
[`SetupDefinitionTests.cs`](../tests/Dune.Engine.Tests/SetupDefinitionTests.cs),
[`CardZoneTests.cs`](../tests/Dune.Engine.Tests/CardZoneTests.cs) and
[`SeededRandomTests.cs`](../tests/Dune.Engine.Tests/SeededRandomTests.cs).
`SetupCreatesExactBoxedPoolsAndResourcesBeforeRoundStart` covers the table's initial
resources/units/board/card pools for both seat counts;
`SameInputsHaveCanonicalSnapshotsAndSeedsChangeOnlyTheirOwnRandomStreams` covers
seeded setup and first-player independence;
`RandomLeaderPoolIsCanonicalAndAssignmentUsesPlayerOrder` covers dealing policy;
`ObservationsRedactNonemptyOpponentPrivateZonesAndRemainDetached` covers visibility;
`TrustedSnapshotContainsAllPhysicalCardsSeedsAndTemporaryState` covers privileged
comparison data. Content failures have direct tests and never return partial state.

## SLICE-003 source verification boundary

I and A below retain the exact local 2020 editions and **one-based PDF pages**
from the source index. Card art was inspected locally; it is not redistributed.
The starter fan on I p.3 verifies access icons but obscures most effect boxes.
No effect is inferred from a card name. The initial seven starter identities and
copy counts remain verified setup metadata, separately from executable effects.

| Mechanic | Official source | Executable scope | Behavioral evidence |
| --- | --- | --- | --- |
| Round Start | [I p.8](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=8) | Reveal top Conflict, draw five from own deck, first player begins | Turn engine tests |
| Recycling empty deck | [I p.6](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=6), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Reuse existing draw/reshuffle operation, preserve physical identity | CardZoneTests; turn engine tests |
| Turn scheduling | [I p.8](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=8) | Clockwise Agent or early Reveal, skip Revealed, stop before Combat | Turn engine tests |
| Placement restrictions and ordering | [I p.9](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=9) | Icon access, vacant space, mandatory payment first, player chooses effect order | Turn engine tests; sourced examples tests |
| Optional arrow payments | [I p.9](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=9), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Pay or decline, exact nested effect context | Turn engine tests; Fremen Camp example |
| Finite recruitment and deployment | [I p.10](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=10) | Supply -> garrison; Combat space deploy any recruits this turn plus up to two old troops; zero permitted | Turn engine tests |
| Reveal and acquisitions | [I p.11](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=11) | Remaining hand only, ordered effects; purchases before/between/after, multiple cards, refill row, acquire to discard, discard all in-play | Turn engine tests |
| Combat strength | [I p.11](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=11) | Two per deployed troop plus swords, zero without troops; no battle resolution | Turn engine tests |
| Influence threshold | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7) | Crossing two grants VP; downward crossing loses it. Sourced lifecycle faction definitions enable four/alliance; preview without them remains gated | `OrderedOneTwoOneTwoAdjustsVictoryPointsAtEveryCrossing`; turn engine tests |
| Board space definitions | [A p.2](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=2) | Exact per-space requirements/costs/rewards in implementation matrix | Sourced examples tests |
| Control/bonus spice | [I p.10](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=10), [A p.2](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=2) | Arrakeen/Imperial Basin controller receives bonus; harvest takes accumulated bonus spice | Sourced examples tests |
| Signet Ring | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Three access icons, Reveal 1 Persuasion; Agent use requires complete leader ability contract | Sourced examples tests |
| Dune, the Desert Planet | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3), [I p.10](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=10) | Spice Trade; explicitly no additional Agent effect in Imperial Basin example; obscured Reveal stays gated | Sourced examples tests |
| Arrakis Liaison / The Spice Must Flow | [I p.3](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=3) | Complete faces: Liaison cost2/access Landsraad+City/Reveal2 Persuasion; Flow cost9/acquire1 VP/Reveal1 spice | Sourced examples tests |
| Fremen Camp / Sardaukar Legion | [I p.9](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=9) | Complete faces and examples: Camp optional2 spice ->3 recruits, Reveal2 Persuasion+1 sword; Legion2 recruits, Reveal1 Persuasion+2 swords | Sourced examples tests |

Unverified starter boxes, Foldspace effects, full Intrigue coverage and all real leader
contracts remain unsupported. The p.3 fan's visible access icons are not evidence
for the obscured boxes. No complete authentic game or RL environment is enabled.
Engineering decisions (seeds, selectors, replay schema, diagnostics and synthetic
fixture inventory) are not official game rules; see ADR 0004.

Direct SLICE-003 behavioral tests:

- [`TurnEngineTests`](../tests/Dune.Engine.Tests/TurnEngineTests.cs):
  `RoundStartDrawsFiveRevealsConflictAndSchedulesClockwise`,
  `PlacementChecksAccessCostsOccupancyInfluenceAndOncePerGameBeforeMutation`,
  `EffectOrderAllowsBoardGainToFundOptionalCardPayment`,
  `NestedOptionalDecisionsPreserveOwnerIndependentOfActivePlayer`,
  `DeploymentAllowsRecruitsAndOnlyTwoOldGarrisonTroops`,
  `RevealUsesOnlyHandAndAllowsPurchasesBetweenEffectsWithAcquireRewardsAndRefill`,
  `RevealStrengthIgnoresSwordsWithoutTroopsAndKeepsDeployedStrength`,
  `DrawRecyclesDiscardDeterministicallyAndEpisodesRemainIsolated`,
  `DeterministicReplayBindsRulesRuntimeConfigSeedsAndPendingDecisions`, and
  `LegalEnumerationAndObservationsAreReadOnlyPrivateAndDoNotExposePhysicalIds`.
  These use visibly synthetic card/board definitions to isolate primitives; they
  do not establish the named board-space/card catalog's coverage on their own.
- [`OriginalImperiumTurnRulesTests`](../tests/Dune.Engine.Tests/OriginalImperiumTurnRulesTests.cs)
  executes the sourced preset: `FremenCampCanDeclineItsOptionalSpicePayment`,
  `FremenCampPaymentPrecedesItsRecruitment`, `SellMelangeUsesTheChosenExchange`,
  `HallOfOratoryAddsOnePersuasionOnReveal`,
  `SietchTabrRequiresTwoFremenInfluenceAndDoesNotGrantInfluence`,
  `HighCouncilCostsFiveSolariAndAddsTwoPersuasionOnReveal`,
  `CrossingTwoInfluenceGrantsOneVictoryPoint`,
  `NamedImperiumCardsRevealTheirSourcedPersuasionAndSwords`, and
  `RevealCanBuyMultipleLiaisonsAndFlowButNotFoldspace`.
  The helper substitutes two explicitly one-copy test inventory entries for named
  examples and uses synthetic leaders; it never claims their boxed counts or
  replaces missing starter abilities.

Additional direct named-space/card tests in `OriginalImperiumTurnRulesTests`:
`HardyWarriorsPaysWaterAndCanDeployRecruitsPlusTwoOldTroops`,
`ImperialBasinHarvestsBonusAndPaysItsController`,
`ArrakeenRecruitsOneTroopAndDrawsOneCard`,
`BoardSpaceGuideAppliesMandatoryCostsRewardsAndPlacement` (Stillsuits, Rally Troops,
Research Station, Hagga Basin, The Great Flat and Secure Contract), and
`SignetRingRevealsOnePersuasion`. The exact method names in the linked test source
are authoritative; the synthetic blank access card isolates the sourced board rule.

Acquire timing (A p.1) is pinned by an immediate Spice Must Flow VP assertion in
`RevealCanBuyMultipleLiaisonsAndFlowButNotFoldspace`, with nested-priority tests
`AcquireOptionalDecisionBlocksOuterRevealEffectsAndPurchasesUntilResolved` and
`NestedAcquireGainCompletesBeforeParentAcquireDecisionAndOuterReveal`.
`CommutativeSameOwnerAgentGainsResolveWithoutExtraActions` verifies that only
order-independent Agent gains drain automatically; Reveal acquisition interleaving
and meaningful optional-cost ordering retain explicit decisions.

### SLICE-004 C1: partial lifecycle authoring and support gates

`OriginalImperiumTurnRules.CreateLifecycleRules()` is a separate partial registry.
`Create()` remains a bounded turn preview without lifecycle activation. Source
records are authoring assertions, not proof of a complete authentic inventory.
No catalog quantities or completeness flags change in this task.

| Definition / policy | Supplied source | Authored behavior and limit | Direct test in `LifecycleDefinitionTests` |
| --- | --- | --- | --- |
| Ambush | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12) | Combat timing; +4 swords through the existing effect resolver. Strength and priority update through C2; outcome rewards remain unsupported. | `OfficialAmbushRetainsSourceAndExecutesFourSwordsThroughRealWindow` |
| Master Tactician | [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12), [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Complete OR: +3 swords **or** retreat 0..min(3, own deployed troops) to garrison. C2 enables both alternatives through the existing resolver and bounded count choice. | `CombatPriorityTests.AmbushAndMasterTacticianCanBothPlayBeforeFinishingOneOpportunity`, `CombatPriorityTests.MasterTacticianRetreatOffersBoundedCountAndReturnsOnlyOwnTroopsToGarrison` |
| Corner the Market | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7) | Two independent +1 VP conditions: own at least 2 The Spice Must Flow; own more than each opponent. Typed conditional scoring remains Unsupported. | `CompleteMasterTacticianIsSupportedWhileIndependentCornerConditionsStayUnsupported` |
| Plot / Calculated Hire | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7) | Calculated Hire's pictured cost is 1 spice. Plot timing is recognized; all Plot play is Unsupported, including Agent/Reveal turns. Any configured Plot entry blocks lifecycle startup. | `PlotAnywhereInEnabledInventoryBlocksStartupWithoutRevealingItsIdentity`, `PlotPlayDuringAgentAndRevealTurnsPreservesRngStateFramesAndHistory` |
| Demand Respect / To the Victor | [I p.13](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=13) | Timing-only evidence: after a unique Conflict win and before the next phase, with reward/play interleaving permitted. Conflict spice may be taken first, and To the Victor may fund Demand Respect. Complete numeric faces/costs remain unknown, so both are absent from the executable registry. | `PartialSourceRegistryDoesNotInventConflictTiersQuantitiesOrPostWinValues`, `PlotAndUnknownPostWinFacesAreExcludedFromCombatAndEndgameWindows` |
| Faction threshold-four authoring | [I p.4](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4), [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16) | Emperor: recruit 2 troops; Guild: 3 Solari; Bene Gesserit: draw 1 Intrigue; Fremen: 1 water. Maximum 6 is explicitly a **visual inference** from seven cells anchored at 0/2/4, not a prose numeral. Runtime uses these sourced definitions for each upward crossing of four; downward crossing retains earned resources. | `PartialSourceRegistryDoesNotInventConflictTiersQuantitiesOrPostWinValues`; `FourRewardIsGrantedAgainOnUpwardRecrossAndLossKeepsEarnedResources` |
| Original Combat participation / strength | [I p.11](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=11), [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12) | Eligibility queries the immutable OriginalTroops policy using typed unit contributions. Strength reuses the existing calculator; zero-strength troop contributions and other unit kinds do not redefine original eligibility. | `ParticipationUsesOriginalTroopsIndependentlyOfStrength` |
| Skirmish / Siege of Arrakeen evidence limit | [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12), [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16) | Pictured first/second/third **reward positions** do not establish Conflict deck classes I/II/III or quantities. No executable official Conflict entry is authored. | `PartialSourceRegistryDoesNotInventConflictTiersQuantitiesOrPostWinValues`; `FourRewardIsGrantedAgainOnUpwardRecrossAndLossKeepsEarnedResources` |

Engineering checks (not official rules) validate lifecycle sources, IDs, enums,
costs, unique reward positions 1/2/3, deck-tier agreement, and capability support.
Before StartRound changes state or RNG, all configured Intrigue/Conflict pools
are checked independently of hidden deck order and selected/unused Conflicts;
all active main/reserve/player card definitions are checked too. Declared control
rewards remain Unsupported until their runtime task. Rejected malformed policies
receive a reserved nonexecutable identity before safe enum serialization. Legitimately
unused starter storage is excluded. Failures disclose generic codes without card
identities, categories, counts, or hidden effect text. The existing no-lifecycle
preview remains available. `UnknownConflictAndMandatoryUnsupportedRewardRejectBeforeAnyStartupMutation`,
`HiddenUnsupportedIdentityAndOrderNeverChangePublicDiagnostics`,
`MalformedLifecycleDefinitionsRejectWithoutMutation`, and
`ActiveReserveAndHiddenMainDeckAreIncludedInStartupSupportGate` cover the gates.

The engine-only `tests/Fixtures/LifecycleFixture.cs` explicitly substitutes six
synthetic reserve cards for the old fixture's six unsupported Foldspace copies.
It preserves their physical IDs and total card count, changes the fixture recipe
and definition to the synthetic ID, and recomputes configuration/content identities
and the ruleset fingerprint. It declares all 40 synthetic Intrigue definitions
and all 18 synthetic Conflict definitions (including unused candidates). This is
an invented test inventory, not an authentic deck pruned for playability. The
standard initializer and official Foldspace stay unchanged and unsupported.
`CompleteSyntheticInventoryStartsAndPreservesPhysicalCopiesWithDistinctHonestIdentity`
and the migrated `PhaseDecisionTests.LastRevealAutomaticallyOpensCombatWindowWithNoCurrentPlayer`
exercise startup and replay through the real actions. Initial Combat/Endgame
fixtures remain explicit trusted seams with per-play support/timing validation.

C2 advances support semantics and official preview authoring to v5 and partial
lifecycle authoring to v3 for runtime retreat and fresh Combat strength markers.
Serialized state/replay shapes remain v3: the existing effect, decision and action
records represent bounded retreat without new fields. Previous support revisions,
including v4, remain rejected before replay history applies
(`LifecycleReplayTests.OlderReplayMetadataFailsValidationBeforeAnyActionsAreApplied`).

### SLICE-004 C2: Combat opportunities and retreat

| Rule | Supplied source | Implemented behavior / direct tests in `CombatPriorityTests` |
| --- | --- | --- |
| Combat opportunities | [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12) | Begin at First Player and proceed clockwise among players with troops. Zero eligible seats close; one seat still receives explicit decisions. `PriorityStartsAtFirstPlayerAndWrapsAcrossEligibleSeats` covers 0/1/3/4 participants. |
| Any number of Combat Intrigues; consecutive passes | [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12) | Completed cards keep the opportunity with their player and clear prior no-play passes. Finishing a played opportunity rotates without contributing a no-play pass; a later fresh pass is required even for one seat. Earlier passers may play again. `AmbushAndMasterTacticianCanBothPlayBeforeFinishingOneOpportunity`, `EarlierPasserCanPlayAgainAfterOtherPlayersPlayAndPass`. |
| Immediate strength adjustment | [I p.11](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=11), [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12) | Use the shared troop/sword calculator after applied changes, including nested interruption cuts. No troops gives zero strength despite swords; eligibility separately queries original troops. `MasterTacticianRetreatOffersBoundedCountAndReturnsOnlyOwnTroopsToGarrison`, `SyntheticForeignDecisionUsesRecipientTroopsAndRestoresPriorityWithFreshStrength`, `ZeroStrengthTroopsRemainEligibleAndSwordChoiceFiltersAtDerivedStrengthBound`. |
| Retreat destination and last troop | [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12), [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16), [A p.1](rules/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf#page=1) | Retreat returns selected own troops to garrison, distinct from later Combat cleanup to supply. On completed-card return, remove ineligible seats and find the next eligible seat clockwise from the actor. `MasterTacticianRetreatOffersBoundedCountAndReturnsOnlyOwnTroopsToGarrison`, `SoleParticipantRetreatClosesAtUnsupportedOutcomeBoundary`. |
| Intrigue discard and post-win separation | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), [I p.12](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=12), [I p.13](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=13) | Keep the card publicly resolving until its final child returns, then discard it. Normal Combat windows reject post-win timing. `SyntheticForeignDecisionUsesRecipientTroopsAndRestoresPriorityWithFreshStrength`, `UnsupportedOrUnsafePlayPreservesResourcesZonesRandomFramesWindowAndHistory`. |

Engineering checks additionally replay serialized accepted history at suspended
choice/frame cuts, compare complete trusted snapshots and each player's legal
actions, validate retreat counts before mutation, and reject known derived strength
overflow before card movement or payment. Synthetic role-override tests verify that
the decision owner chooses the recipient's troop count; the official Master
Tactician face always binds its own player. They are execution-contract tests,
not evidence for a new official opponent-targeting effect. Optional decline and
mandatory-choice reservations retain the P1 bounded analysis; no general solver
or rollback mechanism is introduced.

Retreat is enabled only for original-troop Combat Intrigues. Combat deployment,
re-entry, expansion units, unsupported effect graphs, other retreat timings and
ordinary Agent/Reveal retreat boxes remain gated. Closure still stops at the
unsupported Conflict outcome boundary; this task does not resolve rewards or
assert an authentic complete Conflict deck or game.


## SLICE-004 C3a — faction reward prerequisites

| Mechanic | Supplied source | Supported behavior and direct tests |
| --- | --- | --- |
| Track bounds and signed influence | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), board [I p.4](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4) | Cubes stay on the original track, interpreted as saturation at 0/6 (the upper endpoint is the documented visual inference). Full-track faction-space placement remains available; oversize signed changes use widened arithmetic and saturate. Malformed initial values outside 0..6 are ContentError. `SignedMovementClampsToTrackEndpointsWithoutExtraAccessRestriction`, `FactionSpaceRemainsUsableAtTopAndDoesNotRepeatEarnedReward`, `OriginalFactionPolicyRejectsUnsupportedMaximumBeforeStartup`. |
| VP threshold and repeat resource bonus | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), printed board [I p.4](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=4), icons [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16) | Crossing two adjusts one VP in either direction; crossing four upward grants Emperor recruit 2, Guild 3 Solari, Bene Gesserit 1 Intrigue, Fremen 1 water every time. Loss retains earned resources. Finite troop supply remains authoritative. `OrderedOneTwoOneTwoAdjustsVictoryPointsAtEveryCrossing`, `FourRewardIsGrantedAgainOnUpwardRecrossAndLossKeepsEarnedResources`, `PlayerOrderedMonotoneGainsAcquireOneAllianceAndOneThresholdReward`. |
| Alliance token | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7) | First to four gains token/one VP, equal challenger leaves incumbent, strict lead transfers token and one VP. A holder may lose influence only while remaining at least four and retaining a strict lead. Every holder loss below four is explicitly Unsupported before mutation, including with no qualifying rival; I p.7 does not establish surrender or retention for that case. Holder loss with a rival at or above the resulting track/at least four remains Unsupported because supplied evidence does not settle that transfer/tie. `EqualityKeepsIncumbentAndStrictLeadTransfersExactlyOnePoint`, `HolderLossRequiresAnUnambiguousRemainingOwner`, `HolderLossBelowFourWithoutQualifyingRivalRejectsWithUnchangedReplayRandomAndHistory`. |
| Intrigue draw and unavailable deck | [I p.7](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=7), Intrigue icon [I p.16](rules/DUNE_IMPERIUM_Rules_2020_10_26.pdf#page=16) | Move top shared-deck cards into the effect recipient's private Intrigue zone. The supplied source does **not** establish shared-deck recycling or skipping an unavailable mandatory draw: `intrigue-empty` rejects the action/selection before commitment; optional payment may decline. No discard recycling or RNG consumption. `IntrigueExhaustionRejectsBeforeActionCommitAndNeverRecycles`, `OptionalUnavailableIntrigueDrawCanBeDeclinedButCannotBePaid`. |

Threshold bonuses are children in the existing P1 frame stack, completed before the
parent's next sibling; chooser, payer, recipient and continuation remain bound.
The preflight-only projection includes VP changes and threshold graphs in existing
stock/resource/permanent/funding checks and reserves shared Intrigue across mandatory
siblings and choices. Competing signed, cross-player or differently bound payer influence in unordered sibling
work is conservatively gated by `influence-order`; influence after conditional track
or ownership changes is gated by `influence-branch`. These implementation limits do
not add a game rule. Explicit Sequence work and monotone same-recipient faction gains
are supported. `ThresholdChildFinishesBeforeParentSiblingAndPreservesForeignRolesAndReplay`,
`ThresholdChildrenParticipateInMandatorySiblingSafety`,
`ThresholdChoiceReservesIntrigueForMandatorySiblingAndCanChooseEmptyAlternative`,
`ChoiceMustHaveOneOptionSatisfyingBothIntrigueStockAndResourceCapacity` verify these contracts.

Turn preview/support semantics advance to v7 and lifecycle authoring to v5. Trusted
setup/turn/replay schemas remain v3 because no serialized state field changes; older
support versions, including v5, are rejected explicitly before replay actions.
