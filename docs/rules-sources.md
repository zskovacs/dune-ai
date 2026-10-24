# Rule sources and implementation ledger

**No Dune rule is implemented or verified by this scaffold.** The token game in
`Dune.Engine.Toy` is an architectural example with invented rules.

Start with the publisher's [Uprising page](https://www.direwolfdigital.com/dune-imperium-uprising/)
and [resources index](https://direwolfdigital.com/dune-imperium/resources/), which
lists Uprising rules and supplements. Index checked 2026-10-24; the rule text has
not been audited here. Future work must select the relevant edition and rules
mode explicitly, including optional modules and player count. Do not infer game
mechanics from AI summaries or from the original Imperium/expansion rulebooks.

Do not commit copyrighted rulebooks or card scans. Link to authorized sources;
record edition, revision, retrieval date and checksum of a privately obtained
document when useful. Store concise interpretations and tests, not copied pages.

## Source register template

| Source ID | Official URL | Title / edition | Revision | Retrieved | SHA-256 (optional) | Scope / verification status |
| --- | --- | --- | --- | --- | --- | --- |
| SOURCE-001 | To verify | Uprising main rulebook | To verify | — | — | Pending; no mechanics derived |

## Rule ledger template

| Rule ID | Mechanic | Source + page/section | Interpretation / explicit assumption | Code | Tests | Status / reviewer |
| --- | --- | --- | --- | --- | --- | --- |
| RULE-001 | To scope | SOURCE-001, exact page/section required | Unresolved | — | — | Blocked pending source review |

For each implementation: check source applicability, record interpretation and
ambiguities, add behavioral tests (including interactions/privacy), implement a
small slice, and link code/tests here. Conflicting text requires a documented
resolution or exclusion. An assumption is provisional evidence, not a verified
rule; unknown behavior must remain explicit rather than silently invented.
