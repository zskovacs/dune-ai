# Inventory Audit

This is an offline projection of [the canonical audit](coverage/inventory.json).
`mise run content:report` updates the marked region; `mise run content:validate`
checks integrity and freshness without editing it. The source assessment and
copying limits are in [sources](sources.md). A physical record can be a board
space, layout or piece group; it is not necessarily a card definition. Unknown
unique totals and copy counts stay unknown. Subset expectations overlap pools,
and physical piece counts are distinct from resource values.


<!-- BEGIN GENERATED CONTENT COVERAGE -->
Audit revision: `slice-004.5-d1-review-fix2-2026-10-25`; schema: `dune-content-coverage-v1`.

Sources: 12; expectations: 93; physical content records: 90; identity/quantity complete expectations: 21; explicit gaps: 72; unknown ability slots: 121.

Expectations overlap across pools, subsets, recipes and exclusions. These counts are not additive game totals. A complete quantity row does not certify a complete face.

### By product

| product | Records |
| --- | --- |
| Imperium | 90 |

### By category

| category | Records |
| --- | --- |
| BoardLayout | 1 |
| BoardSpace | 22 |
| ConflictCard | 2 |
| GameplayComponent | 17 |
| ImperiumCard | 16 |
| IntrigueCard | 14 |
| Leader | 8 |
| ReserveCard | 3 |
| StartingCard | 7 |

### By discovery

| discovery | Records |
| --- | --- |
| discovered | 90 |

### By sourcing

| sourcing | Records |
| --- | --- |
| fully-sourced | 90 |

### By verification

| verification | Records |
| --- | --- |
| verified | 90 |

### Quantity reconciliation

| Expectation | Product / category / scope | Expected copies | Verified copies | Sourced unverified copies | Unknown-copy records | Missing residual | Overcount | Official unique total | Discovered definitions | Complete |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| exp-bloodlines-choam-contracts | Bloodlines / Contract / excluded-unless-choam-module | 8 | 0 | 0 | 0 | 8 | 0 | unknown | 0 | no |
| exp-bloodlines-choam-imperium | Bloodlines / ImperiumCard / excluded-unless-choam-module | 5 | 0 | 0 | 0 | 5 | 0 | unknown | 0 | no |
| exp-bloodlines-choam-intrigue | Bloodlines / IntrigueCard / excluded-unless-choam-module | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-bloodlines-commanders-usable | Bloodlines / GameplayComponent / standard-setup-limited-pool | 7 | 0 | 0 | 0 | 7 | 0 | unknown | 0 | no |
| exp-bloodlines-conflict | Bloodlines / ConflictCard / boxed | 2 | 0 | 0 | 0 | 2 | 0 | unknown | 0 | no |
| exp-bloodlines-conflict-i | Bloodlines / ConflictCard / boxed-tier-subset-excluded-with-original | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-bloodlines-conflict-ii | Bloodlines / ConflictCard / boxed-tier-subset-excluded-with-original | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-bloodlines-hagal | Bloodlines / HouseHagalCard / excluded-solo-two-player | 6 | 0 | 0 | 0 | 6 | 0 | unknown | 0 | no |
| exp-bloodlines-imperium | Bloodlines / ImperiumCard / boxed | 32 | 0 | 0 | 0 | 32 | 0 | unknown | 0 | no |
| exp-bloodlines-intrigue | Bloodlines / IntrigueCard / boxed | 18 | 0 | 0 | 0 | 18 | 0 | unknown | 0 | no |
| exp-bloodlines-leader | Bloodlines / Leader / boxed | 9 | 0 | 0 | 0 | 9 | 0 | unknown | 0 | no |
| exp-bloodlines-navigation | Bloodlines / GameplayComponent / leader-specific-steersman | 10 | 0 | 0 | 0 | 10 | 0 | unknown | 0 | no |
| exp-bloodlines-rival | Bloodlines / Rival / excluded-solo-two-player | 6 | 0 | 0 | 0 | 6 | 0 | unknown | 0 | no |
| exp-bloodlines-skills | Bloodlines / SardaukarCommanderSkill / boxed | 14 | 0 | 0 | 0 | 14 | 0 | unknown | 0 | no |
| exp-bloodlines-tech-imperium | Bloodlines / ImperiumCard / excluded-unless-tech-module | 2 | 0 | 0 | 0 | 2 | 0 | unknown | 0 | no |
| exp-bloodlines-tech-intrigue | Bloodlines / IntrigueCard / excluded-unless-tech-module | 2 | 0 | 0 | 0 | 2 | 0 | unknown | 0 | no |
| exp-bloodlines-tech-leader | Bloodlines / Leader / excluded-unless-tech-module | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-bloodlines-tech-tiles | Bloodlines / TechTile / excluded-unless-tech-module | 18 | 0 | 0 | 0 | 18 | 0 | unknown | 0 | no |
| exp-bloodlines-twisted-intrigue | Bloodlines / GameplayComponent / leader-specific-piter | 12 | 0 | 0 | 0 | 12 | 0 | unknown | 0 | no |
| exp-immortality-experimentation | Immortality / StartingCard / per-player-replacement | 2 | 0 | 0 | 0 | 2 | 0 | 1 | 0 | no |
| exp-immortality-experimentation-boxed | Immortality / StartingCard / boxed-starter-replacement | 8 | 0 | 0 | 0 | 8 | 0 | 1 | 0 | no |
| exp-immortality-hagal | Immortality / HouseHagalCard / excluded-solo-only | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-immortality-imperium | Immortality / ImperiumCard / boxed | 30 | 0 | 0 | 0 | 30 | 0 | unknown | 0 | no |
| exp-immortality-intrigue | Immortality / IntrigueCard / boxed | 15 | 0 | 0 | 0 | 15 | 0 | unknown | 0 | no |
| exp-immortality-reserve | Immortality / ReserveCard / boxed | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-immortality-tleilaxu | Immortality / TleilaxuCard / boxed | 18 | 0 | 0 | 0 | 18 | 0 | unknown | 0 | no |
| exp-imperium-agents-per-player | Imperium / GameplayComponent / ordinary-player-components | 3 | 3 | 0 | 0 | 0 | 0 | 2 | 2 | yes |
| exp-imperium-alliance-tokens | Imperium / GameplayComponent / standard-setup | 4 | 4 | 0 | 0 | 0 | 0 | unknown | 1 | no |
| exp-imperium-baron-tokens | Imperium / GameplayComponent / leader-specific-baron | 4 | 4 | 0 | 0 | 0 | 0 | unknown | 1 | no |
| exp-imperium-board-locations | Imperium / BoardSpace / original-board | 22 | 22 | 0 | 0 | 0 | 0 | 22 | 22 | yes |
| exp-imperium-combat-marker-per-player | Imperium / GameplayComponent / ordinary-player-components | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-conflict | Imperium / ConflictCard / boxed | 18 | 0 | 0 | 2 | unknown | 0 | unknown | 2 | no |
| exp-imperium-conflict-i | Imperium / ConflictCard / boxed-tier-subset | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-imperium-conflict-ii | Imperium / ConflictCard / boxed-tier-subset | 10 | 0 | 0 | 0 | 10 | 0 | unknown | 0 | no |
| exp-imperium-conflict-iii | Imperium / ConflictCard / boxed-tier-subset | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-imperium-control-per-player | Imperium / GameplayComponent / ordinary-player-components | 3 | 3 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-councilor-token-per-player | Imperium / GameplayComponent / ordinary-player-components | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-cubes-per-player | Imperium / GameplayComponent / ordinary-player-components | 16 | 16 | 0 | 0 | 0 | 0 | 2 | 2 | yes |
| exp-imperium-discs-per-player | Imperium / GameplayComponent / ordinary-player-components | 2 | 2 | 0 | 0 | 0 | 0 | 2 | 2 | yes |
| exp-imperium-first-player-marker | Imperium / GameplayComponent / standard-setup | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-hagal | Imperium / HouseHagalCard / excluded-solo-two-player | 31 | 0 | 0 | 0 | 31 | 0 | unknown | 0 | no |
| exp-imperium-imperium | Imperium / ImperiumCard / boxed | 67 | 2 | 0 | 15 | unknown | 0 | unknown | 16 | no |
| exp-imperium-influence-cubes-per-player | Imperium / GameplayComponent / ordinary-player-components | 4 | 4 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-intrigue | Imperium / IntrigueCard / boxed | 40 | 0 | 0 | 14 | unknown | 0 | unknown | 14 | no |
| exp-imperium-leaders | Imperium / Leader / boxed | 8 | 0 | 0 | 8 | unknown | 0 | 8 | 8 | no |
| exp-imperium-mentat | Imperium / GameplayComponent / standard-setup | 1 | 1 | 0 | 0 | 0 | 0 | unknown | 1 | no |
| exp-imperium-reserve | Imperium / ReserveCard / boxed | 24 | 24 | 0 | 0 | 0 | 0 | 3 | 3 | yes |
| exp-imperium-reserve-arrakis-liaison | Imperium / ReserveCard / boxed-identity-subset | 8 | 8 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-reserve-foldspace | Imperium / ReserveCard / boxed-identity-subset | 6 | 6 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-reserve-spice-must-flow | Imperium / ReserveCard / boxed-identity-subset | 10 | 10 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-score-marker-per-player | Imperium / GameplayComponent / ordinary-player-components | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-selected-conflict-i | Imperium / ConflictCard / standard-setup-selection | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-imperium-selected-conflict-ii | Imperium / ConflictCard / standard-setup-selection | 5 | 0 | 0 | 0 | 5 | 0 | unknown | 0 | no |
| exp-imperium-selected-conflict-iii | Imperium / ConflictCard / standard-setup-selection | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-imperium-selected-conflicts | Imperium / ConflictCard / standard-setup-selection | 10 | 0 | 0 | 0 | 10 | 0 | unknown | 0 | no |
| exp-imperium-solari-tokens | Imperium / GameplayComponent / standard-setup | 24 | 24 | 0 | 0 | 0 | 0 | 2 | 2 | yes |
| exp-imperium-spice-tokens | Imperium / GameplayComponent / standard-setup | 23 | 23 | 0 | 0 | 0 | 0 | 2 | 2 | yes |
| exp-imperium-starter-per-player | Imperium / StartingCard / ordinary-player-recipe | 10 | 10 | 0 | 0 | 0 | 0 | 7 | 7 | yes |
| exp-imperium-starters-four-decks | Imperium / StartingCard / boxed | 40 | 40 | 0 | 0 | 0 | 0 | 7 | 7 | yes |
| exp-imperium-swordmaster-per-player | Imperium / GameplayComponent / ordinary-player-components | 1 | 1 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-troop-cubes-per-player | Imperium / GameplayComponent / ordinary-player-components | 12 | 12 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-imperium-water-tokens | Imperium / GameplayComponent / standard-setup | 15 | 15 | 0 | 0 | 0 | 0 | 1 | 1 | yes |
| exp-ix-conflict | RiseOfIx / ConflictCard / boxed | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-ix-conflict-i | RiseOfIx / ConflictCard / boxed-tier-subset | 2 | 0 | 0 | 0 | 2 | 0 | unknown | 0 | no |
| exp-ix-conflict-ii | RiseOfIx / ConflictCard / boxed-tier-subset | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-ix-conflict-iii | RiseOfIx / ConflictCard / boxed-tier-subset | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-ix-epic-starter | RiseOfIx / StartingCard / excluded-epic-variant | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-ix-epic-starters-boxed | RiseOfIx / StartingCard / boxed-epic-variant | 4 | 0 | 0 | 0 | 4 | 0 | 1 | 0 | no |
| exp-ix-hagal | RiseOfIx / HouseHagalCard / excluded-solo-two-player | 9 | 0 | 0 | 0 | 9 | 0 | unknown | 0 | no |
| exp-ix-imperium | RiseOfIx / ImperiumCard / boxed | 35 | 0 | 0 | 0 | 35 | 0 | unknown | 0 | no |
| exp-ix-intrigue | RiseOfIx / IntrigueCard / boxed | 17 | 0 | 0 | 0 | 17 | 0 | unknown | 0 | no |
| exp-ix-leaders | RiseOfIx / Leader / boxed | 6 | 0 | 0 | 0 | 6 | 0 | unknown | 0 | no |
| exp-ix-rival-aids | RiseOfIx / Rival / excluded-solo-two-player | 2 | 0 | 0 | 0 | 2 | 0 | unknown | 0 | no |
| exp-ix-tech | RiseOfIx / TechTile / boxed | 18 | 0 | 0 | 0 | 18 | 0 | unknown | 0 | no |
| exp-uprising-choam-contracts | Uprising / Contract / excluded-unless-choam-module | 20 | 0 | 0 | 0 | 20 | 0 | unknown | 0 | no |
| exp-uprising-choam-imperium | Uprising / ImperiumCard / excluded-unless-choam-module | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-uprising-choam-intrigue | Uprising / IntrigueCard / excluded-unless-choam-module | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-uprising-choam-ix-contracts | Uprising / Contract / excluded-unless-choam-and-ix | 10 | 0 | 0 | 0 | 10 | 0 | unknown | 0 | no |
| exp-uprising-choam-leader | Uprising / Leader / excluded-unless-choam-module | 1 | 0 | 0 | 0 | 1 | 0 | unknown | 0 | no |
| exp-uprising-conflict | Uprising / ConflictCard / boxed | 16 | 0 | 0 | 0 | 16 | 0 | unknown | 0 | no |
| exp-uprising-conflict-i | Uprising / ConflictCard / boxed-tier-subset | 3 | 0 | 0 | 0 | 3 | 0 | unknown | 0 | no |
| exp-uprising-conflict-ii | Uprising / ConflictCard / boxed-tier-subset | 9 | 0 | 0 | 0 | 9 | 0 | unknown | 0 | no |
| exp-uprising-conflict-iii | Uprising / ConflictCard / boxed-tier-subset | 4 | 0 | 0 | 0 | 4 | 0 | unknown | 0 | no |
| exp-uprising-hagal | Uprising / HouseHagalCard / excluded-solo-two-player | 37 | 0 | 0 | 0 | 37 | 0 | unknown | 0 | no |
| exp-uprising-imperium | Uprising / ImperiumCard / boxed | 69 | 0 | 0 | 0 | 69 | 0 | unknown | 0 | no |
| exp-uprising-intrigue | Uprising / IntrigueCard / boxed | 44 | 0 | 0 | 0 | 44 | 0 | unknown | 0 | no |
| exp-uprising-leaders | Uprising / Leader / boxed | 9 | 0 | 0 | 0 | 9 | 0 | unknown | 0 | no |
| exp-uprising-objective | Uprising / Objective / boxed | 5 | 0 | 0 | 0 | 5 | 0 | unknown | 0 | no |
| exp-uprising-reserve | Uprising / ReserveCard / boxed | 18 | 0 | 0 | 0 | 18 | 0 | unknown | 0 | no |
| exp-uprising-rival | Uprising / Rival / excluded-solo-two-player | 10 | 0 | 0 | 0 | 10 | 0 | unknown | 0 | no |
| exp-uprising-six-player-starters | Uprising / StartingCard / excluded-six-player-variant | 20 | 0 | 0 | 0 | 20 | 0 | unknown | 0 | no |
| exp-uprising-starter-per-player | Uprising / StartingCard / ordinary-player-recipe | 10 | 0 | 0 | 0 | 10 | 0 | 7 | 0 | no |
| exp-uprising-starters-four-decks | Uprising / StartingCard / boxed | 40 | 0 | 0 | 0 | 40 | 0 | 7 | 0 | no |

### Content identity and face status

| ID | Product | Category | Boxed / per-player copies | Source / verification | Face complete | Applicability | Known abilities |
| --- | --- | --- | --- | --- | --- | --- | --- |
| imperium:board:main:v1 | Imperium | BoardLayout | 1 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:alliance-tokens:v1 | Imperium | GameplayComponent | 4 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:baron-tokens:v1 | Imperium | GameplayComponent | 4 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:combat-marker:v1 | Imperium | GameplayComponent | unknown / 1 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:control-markers:v1 | Imperium | GameplayComponent | unknown / 3 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:councilor-token:v1 | Imperium | GameplayComponent | unknown / 1 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:first-player-marker:v1 | Imperium | GameplayComponent | 1 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:influence-cubes:v1 | Imperium | GameplayComponent | unknown / 4 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:mentat-piece:v1 | Imperium | GameplayComponent | 1 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:ordinary-agents:v1 | Imperium | GameplayComponent | unknown / 2 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:score-marker:v1 | Imperium | GameplayComponent | unknown / 1 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:solari-token-large:v1 | Imperium | GameplayComponent | 4 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:solari-token-small:v1 | Imperium | GameplayComponent | 20 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:spice-token-large:v1 | Imperium | GameplayComponent | 3 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:spice-token-small:v1 | Imperium | GameplayComponent | 20 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:swordmaster-agent:v1 | Imperium | GameplayComponent | unknown / 1 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:troop-cubes:v1 | Imperium | GameplayComponent | unknown / 12 | fully-sourced / verified | no | physical-only | 0 |
| imperium:component:water-token:v1 | Imperium | GameplayComponent | 15 / unknown | fully-sourced / verified | no | physical-only | 0 |
| imperium:conflict:siege-of-arrakeen:v1 | Imperium | ConflictCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:conflict:sort-through-the-chaos:v1 | Imperium | ConflictCard | unknown / unknown | fully-sourced / verified | no | executable | 1 |
| imperium:imperium-card:assassination-mission:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:carryall:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:duncan-idaho:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:fremen-camp:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:imperium-card:guild-bankers:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:gun-thopter:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:imperial-spy:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:kwisatz-haderach:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:liet-kynes:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:missionaria-protectiva:v1 | Imperium | ImperiumCard | 2 / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:reverend-mother-mohiam:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 1 |
| imperium:imperium-card:sardaukar-legion:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:imperium-card:smugglers-thopter:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:stilgar:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:test-of-humanity:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:imperium-card:the-voice:v1 | Imperium | ImperiumCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:ambush:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:intrigue:bindu-suspension:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:bribery:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:calculated-hire:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:intrigue:charisma:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:corner-the-market:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:intrigue:councilors-dispensation:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:demand-respect:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:dispatch-an-envoy:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:double-cross:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:master-tactician:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:intrigue:poison-snooper:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:recruitment-mission:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:intrigue:refocus:v1 | Imperium | IntrigueCard | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:baron-vladimir-harkonnen:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 1 |
| imperium:leader:count-ilban-richese:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:countess-ariana-thorvald:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:duke-leto-atreides:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 2 |
| imperium:leader:earl-memnon-thorvald:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:glossu-rabban:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:helena-richese:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:leader:paul-atreides:v1 | Imperium | Leader | unknown / unknown | fully-sourced / verified | no | executable | 0 |
| imperium:reserve:arrakis-liaison:v1 | Imperium | ReserveCard | 8 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:reserve:foldspace:v1 | Imperium | ReserveCard | 6 / unknown | fully-sourced / verified | no | executable | 2 |
| imperium:reserve:spice-must-flow:v1 | Imperium | ReserveCard | 10 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:arrakeen:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:carthag:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:conspire:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:foldspace:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:great-flat:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:hagga-basin:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:hall-of-oratory:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:hardy-warriors:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:heighliner:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:high-council:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:imperial-basin:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 2 |
| imperium:space:mentat:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:rally-troops:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:research-station:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:secrets:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:secure-contract:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:selective-breeding:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:sell-melange:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:sietch-tabr:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:stillsuits:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:swordmaster:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:space:wealth:v1 | Imperium | BoardSpace | 1 / unknown | fully-sourced / verified | yes | executable | 1 |
| imperium:starter:convincing-argument:v1 | Imperium | StartingCard | 8 / 2 | fully-sourced / verified | no | executable | 2 |
| imperium:starter:dagger:v1 | Imperium | StartingCard | 8 / 2 | fully-sourced / verified | no | executable | 2 |
| imperium:starter:diplomacy:v1 | Imperium | StartingCard | 4 / 1 | fully-sourced / verified | no | executable | 2 |
| imperium:starter:dune-desert-planet:v1 | Imperium | StartingCard | 8 / 2 | fully-sourced / verified | no | executable | 1 |
| imperium:starter:reconnaissance:v1 | Imperium | StartingCard | 4 / 1 | fully-sourced / verified | no | executable | 2 |
| imperium:starter:seek-allies:v1 | Imperium | StartingCard | 4 / 1 | fully-sourced / verified | no | executable | 2 |
| imperium:starter:signet-ring:v1 | Imperium | StartingCard | 4 / 1 | fully-sourced / verified | no | executable | 2 |
<!-- END GENERATED CONTENT COVERAGE -->
