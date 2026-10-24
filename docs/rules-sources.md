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
