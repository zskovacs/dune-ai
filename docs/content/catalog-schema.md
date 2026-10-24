# Content catalog schema

The catalog is static setup metadata in `Dune.Engine.Content`, authored separately
from gameplay. The bundled JSON is a **representative sample**, never a full
inventory or an executable rules database. No community dataset, card images or
full card-effect text is imported; see [sources](sources.md).

## Definition identity

A `ContentId` identifies a definition, not a physical copy. Use a globally unique,
versioned product/category/component form such as
`imperium:starter:dune-desert-planet:v1`. Display names are not keys: products can
reuse a name, and same-name conflicts can have different rewards/versions.
Renaming display text must not silently reinterpret an old ID. A runtime
card instance holds its own episode-local instance ID and a definition ID;
quantity does not require duplicate definitions or shared mutable card state.

## Required metadata

| Field | Purpose |
| --- | --- |
| ID and display name | Stable reference versus human-readable label. |
| Owning product | Original Imperium, Uprising or expansion that supplied the component. |
| Category | Imperium, Starting, Reserve, Intrigue, Conflict, Leader, Tech, Tleilaxu, Objective, Contract, Commander Skill, House Hagal, Rival, Board Space or Board Layout. |
| Quantity | Physical multiplicity of this definition; setup recipes specify per-player counts and destinations separately. |
| Availability | Allowed base games, required expansions/modules and explicit exclusions. Empty restrictions mean no added restriction, not proof of official universal compatibility. |
| Sources | Local PDF identity and 1-based PDF page, with section/interpretation where useful. |
| Implementation status | Metadata versus implemented behavior; no bundled card has executable effects. |
| Verification status | Which claims have actually been checked, distinct from behavior implementation. |

Exact serialized property names and enum values are defined by the C# records and
bundled JSON. The catalog rejects duplicate IDs instead of collapsing records by
name. Content selection filters on owning product and availability; explicit
profile requests cannot override incompatible requirements. A complete inventory
will need additional card-level evidence because rulebooks chiefly provide totals,
setup changes and mechanic descriptions.

## Recipes and effects

Rule definitions select board layouts/overlays, starting-deck recipes, conflict
recipes and pools. These describe verified setup changes without pretending every
component is present. Quantity belongs to a content definition; a deck recipe
will later specify player count, number of copies, initial zone and shuffle rules.
For example, an initial discard is different from a shuffled starting deck;
interaction modules must retain that distinction. Source citations for these
cases are in the [matrix](../compatibility-matrix.md).

Simple future effects may use dedicated typed data once real consumers exist.
Complex effects can use dedicated C# implementations keyed by stable content ID.
Do not add a general-purpose scripting engine, expression evaluator or generic
effect DSL. Do not infer missing effects from names or images, and never use a
no-op placeholder to make an incomplete ruleset appear playable.

## Authoring and promotion

Add new entries with provenance, availability and honest status. Verify totals and
conditional exclusions against the local rules before declaring a product pool
complete. Promote metadata verification independently from effect implementation;
promote playable status only after the whole selected setup/phase/card/scoring
combination is covered. Changes to content semantics participate in the ruleset
fingerprint and require a deliberate revision/replay decision. Source/reuse
permission and game-rule verification are separate checks.

## SLICE-002 coverage and readiness

The bundled catalog remains **Representative**. Added `base-setup.json` contains
six missing starter definitions and Arrakis Liaison, transcribed only as minimum
name/quantity/category metadata from original rulebook p.3. Together with existing
entries, original Imperium now has:

| Pool | Verified definitions | Verified physical copies | Required |
| --- | ---: | ---: | ---: |
| Starting | 7 | 40 (four player decks) | 40 |
| Reserve | 3 | 24 (8 Arrakis Liaison / 10 Spice Must Flow / 6 Foldspace) | 24 |
| Leaders | 4 | 4 | 8 |
| Imperium deck | 0 | 0 | 67 |
| Intrigue | 0 | 0 | 40 |
| Conflict | 0 | 0 | 18 (4 I / 10 II / 4 III) |

The four other leader identities/metadata and all shared-deck card identities,
copy counts and Conflict tiers need audited evidence. No card/leader effects are
implemented. A complete inventory does not imply complete gameplay; leader-specific
setup instructions also need review before authentic content is enabled.

`ConflictTier` is optional typed metadata on a definition; runtime setup requires
it for every selected Conflict card. The fingerprint format is deliberately v2
and includes this field plus typed setup. Box quantity is distinct from per-seat
copies in `StartingCardRecipe`; three-seat setup leaves a full unused starter deck
outside the game. Exact required identities/categories/counts are validated, so
category totals alone cannot hide a wrong starter or Reserve stack.

Production initialization requires a complete catalog, curated RulebookVerified
status and applicable original-rulebook citations (component p.3; leader pp.3–4;
board pp.4,17–18), plus structural inventory checks. Citations establish component
applicability; they cannot machine-verify every identity or grant redistribution
rights. A future card audit may add per-card evidence without importing effects/art.
Test-only synthetic definitions remain Unverified/Representative and are accepted
only by the internal fixture entry point. State and snapshots label them synthetic.
