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
Renaming display text must not silently reinterpret an old ID. A future runtime
card instance will hold its own episode-local instance ID and a definition ID;
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
