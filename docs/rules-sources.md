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
separate. No real card effects, turn transitions or full starting inventory are
implemented. The behavioral tests here prove metadata composition decisions only.

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
