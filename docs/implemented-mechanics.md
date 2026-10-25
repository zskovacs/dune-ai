# SLICE-004 implemented mechanics and support matrix

For current per-identity source, status and behavior links, see the generated
[ability coverage](content/ability-coverage.md) and
[interaction matrix](content/interaction-matrix.md). Those bounded links do not
establish whole-face or authentic game support; independent gates are in
[readiness](content/readiness-gates.md). Historical registry snapshots below
remain scoped to the slices that recorded them.

The original Imperium (2020) turn engine is a **development preview**. No selected
configuration is authentic complete gameplay or RL-ready. Tests can use explicitly
synthetic inventory and effects; these do not establish full rules coverage.

| Area | Operational subset | Remaining boundary |
| --- | --- | --- |
| Setup | Existing Standard competitive 3–4 player recipe; fixed/random assignments | Authentic full inventory and leader setup/abilities incomplete |
| Round Start | Top Conflict reveal, draw5, empty-deck recycling and seeded shuffle | Real Conflict/leader start triggers need complete supported data |
| Scheduling | Clockwise, early Reveal, skip Revealed, multi-decision turn; lifecycle Combat priority | Authentic full inventory remains blocked |
| Agent | Visible-card access, costs first, occupancy, influence and once-per-game restrictions | Only sourced card boxes/spaces; omitted actions diagnosed |
| Effects | Typed primitive gains, draw, recruit, influence, optional payment/choice, nested owners, acquisition | Conservative composite graphs and unresolved Alliance loss gated |
| Reveal | Hand only, choice of effect order, interleaved purchases, multiple acquisitions, row refill, Reserve, cleanup | Unknown starter/market boxes stay unsupported |
| Deployment | Finite supply/garrison/Conflict, choose zero through turn recruits plus two old troops | No expansion units or combat re-entry |
| Strength | Two per troop plus swords, zero without troops; refresh after applied supported effects including nested decisions | Frozen outcomes and rewards use supported lifecycle definitions |
| Combat outcome | Frozen positive-strength placements, official tie reward tiers and winner at Combat closure | Reward/round/result path supported; official Conflict registry empty |
| Combat priority (support v13) | First Player clockwise; repeated plays/passes; Ambush +4 swords; Master Tactician +3 swords or bounded own retreat to garrison; completed-card eligibility refresh | Winner PostWin window supported; no deployment/re-entry Intrigues, expansion units or authentic complete deck |
| Privacy | Detached owner/public views; no physical-ID ranges, hidden order, RNG or privileged history | No ML encoding or fixed action vocabulary |
| Replay | Versioned accepted history, initial factory, pending-context hash and final comparison | Pinned runtime/content/support versions only; no event sourcing |

## Cards

| Card | Agent support | Reveal/Acquire support | Evidence |
| --- | --- | --- | --- |
| Convincing Argument | No access icons; obscured effect text gated | Obscured Reveal gated | I p.3 fan verifies component/access only |
| Dagger | Landsraad icon; Agent box gated | Reveal gated | I p.3 fan |
| Diplomacy | Four faction icons; Agent box gated | Reveal gated | I p.3 fan |
| Dune, the Desert Planet | Spice Trade; no Agent effect | Reveal gated | I pp.3,10 explicit example |
| Reconnaissance | City icon; Agent box gated | Reveal gated | I p.3 fan |
| Seek Allies | Four faction icons; effect text gated | Reveal gated | I p.3 fan; do not invent missing trash text |
| Signet Ring | Landsraad/City/Spice Trade; dispatch requires supported leader | Reveal 1 Persuasion | I p.3 full face; A p.1 |
| Arrakis Liaison | Landsraad/City, no Agent effect | Cost2; Reveal2 Persuasion | I p.3 full face |
| The Spice Must Flow | No Agent icons | Cost9; acquire1 VP; Reveal1 spice | I p.3 full face |
| Foldspace | Gated | Never bought with Persuasion; box effects gated | I pp.3,11 |
| Fremen Camp | City/Spice Trade; optional2 spice -> recruit3 | Cost4; Reveal2 Persuasion + sword1 | I p.9 full example face |
| Sardaukar Legion | Emperor/Landsraad; recruit2 | Cost5; Reveal1 Persuasion + swords2 | I p.9 full example face |

The two example Imperium cards have executable definitions, not a claimed complete
boxed copy inventory. The main content catalog remains Representative. There is
no import of third-party text, dataset or artwork. All real leaders remain gated;
explicit synthetic test/development leaders are distinct identities with no
official abilities. They never stand in for an unsupported named leader.

## Board spaces

All definitions below use **A p.2**, the original Imperium Player Aid (2020), plus
I pp.9–11 for general cost/deployment/control/Reveal rules.

| Space | Requirements/cost | Effects |
| --- | --- | --- |
| Wealth | Emperor access | Emperor influence1, Solari2 |
| Stillsuits | Fremen access; Combat | Fremen influence1, water1; deployment choice |
| Secure Contract | Spice Trade | Solari3 |
| Hall of Oratory | Landsraad | Recruit1; +1 Persuasion at Reveal while Agent is here |
| Imperial Basin | Spice Trade; Combat | Spice1 + accumulated bonus; controller gains spice1; deployment |
| Rally Troops | Landsraad; Solari4 | Recruit4; no deployment |
| Hardy Warriors | Fremen; water1; Combat | Fremen influence1, recruit2; deployment |
| Arrakeen | City; Combat | Recruit1, draw1; controller gains Solari1; deployment |
| Sietch Tabr | City; Fremen influence >=2; Combat | Recruit1, water1; no Fremen influence gain |
| High Council | Landsraad; Solari5; once/game | Permanent seat; Reveal +2 Persuasion |
| Research Station | City; water2; Combat | Draw3; deployment |
| Hagga Basin | Spice Trade; water1; Combat | Spice2 + accumulated bonus; deployment |
| The Great Flat | Spice Trade; water2; Combat | Spice3 + accumulated bonus; deployment |
| Sell Melange | Spice Trade; choose2/3/4/5 spice first | Gain6/8/10/12 Solari respectively; one exchange |
| Selective Breeding | Bene Gesserit; Spice2 | Influence1; optional selected own trash -> draw2 |

Carthag, Conspire, Heighliner, Mentat and Swordmaster are now enabled sourced
definitions (see the rule ledger). Carthag recruits1/draws Intrigue1; Conspire
costs4 spice for Emperor influence1/Solari5/recruit2/Intrigue1; Heighliner costs6
spice for Guild influence1/recruit5/water2 and deployment; Mentat costs2 Solari
for draw1 and the available token; Swordmaster costs8 Solari once per game.
Foldspace and Secrets remain unsupported board spaces. Selective Breeding's
bounded original placement is described in the SLICE-005 delta below.
No board subset is advertised as the complete 22-space official legal action set.

## CLI and eligible configurations

`mise run preview:turns -- --seed 42 --players 4` explicitly links the same
synthetic setup inventory as tests/benchmarks. It executes verified available
placements, logs legal decisions and transitions, then stops when unverified card
or leader data blocks progress. It verifies replay of the accepted partial trace.
The command exits successfully when it demonstrates and identifies this expected
support boundary; an engine/replay defect exits unsuccessfully.

Complete Player Turns, Combat priority, and retreat are exercised using
**test-only synthetic inventories**, with invented effects confined to resolver
contract fixtures. This is engine verification, not an authentic first-round demo. The existing toy command remains a separate invented
game. Neither command authorizes training on incomplete Dune mechanics.

See [rule ledger](rules-sources.md), [turn architecture](architecture/turn-engine.md)
and [roadmap](roadmap.md) for evidence and remaining work.

At SLICE-004, support/preview semantics were v13, partial lifecycle authoring v9, setup
v5 and turn/replay v7. Current values are in the SLICE-005 delta below. The ordinary
`Create()` preview keeps lifecycle disabled.

## Definition counts and inventory limits

`LifecycleIntegrationTests.OfficialRegistryDefinitionCountsRemainDistinctFromCompleteInventorySupport`
locks current registry counts: 12 `CardRule` definitions (7 starter identities,
3 reserves, 2 Imperium examples), 20 board definitions, 0 production leaders;
4 `IntrigueRule` definitions (3 executable complete faces, 1 explicitly unsupported
Plot face), 0 official `ConflictRule` definitions and 4 faction definitions.
Counts are authored definitions, not boxed physical inventory or complete-game
coverage. Among starters only Signet Ring has both boxes authored, and its Agent
box still requires an unsupported real leader; Dune Desert Planet has its Agent
box only. The two reserve purchase faces and two example Imperium cards are
fully authored; Foldspace is not. Complete official physical inventories remain
unverified/incomplete; no percentage is inferred.

Supported lifecycle mechanics include Combat tie tiers, winner PostWin reward
interleaving, lower-recipient child effects, control/defense, Makers, typed Agent
Recall, round rotation/draws, terminal triggers and immutable final standings.
Source gates remain for holder loss below four, tied positive shared Intrigue
draw order, contested Mentat, Plot and unsupported
composite/future content. [SLICE-004 handoff](handoffs/SLICE-004-result.md).

## SLICE-005 bounded original Imperium delta

Selective Breeding is now the twentieth authored original `BoardRule`. Its Bene
Gesserit Agent placement pays 2 Spice before benefits, grants 1 Bene Gesserit
Influence, and offers a once-only optional exchange: select and trash one own
Hand, Discard or InPlay card, then draw two. Declining keeps the Influence and
draws nothing. The card just played can be the InPlay target. Visible
zone/definition/copy selectors distinguish duplicate faces; trusted execution
binds the chosen physical instance separately from the executing source. Actual
Reserve cards return to their exact shared stacks, even if empty; ordinary
cards enter shared trash. Returned stock can support only proven later work.

The canonical audit now records 35 implemented and 17 unsupported of 59 known
abilities, with three partial and four not implemented. The new ability,
`cap-selected-trash`, `cap-reserve-return`, and
`int-original-selected-trash-reserve` are tested; this does not certify other
card families. Foldspace and Secrets spaces remain unauthored, as does the
Foldspace card face. Current turn snapshot/replay versions are v9,
support/preview v15, and lifecycle authoring v11; setup v5 and result/identity
v1 remain. The preceding v13/v9/v7 statements above describe the historical
SLICE-004 state. All six authentic configurations remain unready.
