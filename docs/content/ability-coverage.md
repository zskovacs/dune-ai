# Ability Coverage

Each ability is an independently discovered slot on an audited identity.
Classification, source completeness, verification, implementation and test
coverage are independent. `implemented` denotes an existing bounded code path;
it does not certify the whole printed face, integration, privacy/replay or a game.
Behavior test references are evidence links, not proof that authentic episodes
were executed. Missing/obscured slots remain in the unknown count. Shared
capabilities may have several dependent abilities and are not added to ability
totals. See [the contract](coverage-schema.md) for status rules.


<!-- BEGIN GENERATED CONTENT COVERAGE -->
Known authored ability clauses: 59; unknown or unreadable applicable slots: 121. Implemented means code-present for a bounded sourced behavior; it does not mean a whole face or configuration is validated.

Fully sourced authored clauses: 42; source-unreadable authored placeholders: 13. Placeholders denote unreadable printed boxes, not verified effects; unlisted unknown slots remain separate.

### By classification

| classification | Records |
| --- | --- |
| composite | 25 |
| primitive | 10 |
| special | 7 |
| unresolved | 17 |

### By implementation

| implementation | Records |
| --- | --- |
| implemented | 35 |
| not-implemented | 4 |
| partially-implemented | 3 |
| unsupported | 17 |

### By validation

| validation | Records |
| --- | --- |
| not-tested | 24 |
| tested | 35 |

### By sourcing

| sourcing | Records |
| --- | --- |
| fully-sourced | 42 |
| partially-sourced | 4 |
| unsourced | 13 |

### By verification

| verification | Records |
| --- | --- |
| unverified | 17 |
| verified | 42 |

### By owning product

| product | Abilities |
| --- | --- |
| Imperium | 59 |

### By owning category

| category | Abilities |
| --- | --- |
| BoardSpace | 27 |
| ConflictCard | 1 |
| ImperiumCard | 5 |
| IntrigueCard | 5 |
| Leader | 3 |
| ReserveCard | 5 |
| StartingCard | 13 |

### Known abilities

| ID | Product / category | Class | Source | Verification | Implementation | Behavior test | Capabilities | Interactions | Batch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| imperium:conflict:sort-through-the-chaos:v1#slot:deferred-mentat | Imperium / ConflictCard | unresolved | partially-sourced | unverified | not-implemented | not-tested | cap-deferred-mentat | int-original-mentat-contention | batch-12-deferred-mentat |
| imperium:imperium-card:fremen-camp:v1#slot:optional-recruit | Imperium / ImperiumCard | composite | fully-sourced | verified | implemented | tested | cap-optional-payment, cap-payment, cap-recruit | none | unknown |
| imperium:imperium-card:fremen-camp:v1#slot:persuasion-and-sword | Imperium / ImperiumCard | composite | fully-sourced | verified | implemented | tested | cap-card-reveal, cap-resource-gain | none | unknown |
| imperium:imperium-card:reverend-mother-mohiam:v1#slot:opponent-discard-order | Imperium / ImperiumCard | unresolved | partially-sourced | unverified | not-implemented | not-tested | cap-opponent-discard-order | int-original-opponent-discard | batch-17-opponent-discard |
| imperium:imperium-card:sardaukar-legion:v1#slot:persuasion-and-swords | Imperium / ImperiumCard | composite | fully-sourced | verified | implemented | tested | cap-card-reveal, cap-resource-gain | none | unknown |
| imperium:imperium-card:sardaukar-legion:v1#slot:recruit | Imperium / ImperiumCard | primitive | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-recruit | none | unknown |
| imperium:intrigue:ambush:v1#slot:swords | Imperium / IntrigueCard | primitive | fully-sourced | verified | implemented | tested | cap-combat-intrigue, cap-resource-gain | none | unknown |
| imperium:intrigue:calculated-hire:v1#slot:plot | Imperium / IntrigueCard | primitive | fully-sourced | verified | unsupported | not-tested | cap-plot-window | int-original-mentat-contention, int-original-plot | batch-10-plot |
| imperium:intrigue:corner-the-market:v1#slot:more-spice-must-flow | Imperium / IntrigueCard | primitive | fully-sourced | verified | implemented | tested | cap-endgame-conditional, cap-resource-gain | none | unknown |
| imperium:intrigue:corner-the-market:v1#slot:two-spice-must-flow | Imperium / IntrigueCard | primitive | fully-sourced | verified | implemented | tested | cap-endgame-conditional, cap-resource-gain | none | unknown |
| imperium:intrigue:master-tactician:v1#slot:swords-or-retreat | Imperium / IntrigueCard | composite | fully-sourced | verified | implemented | tested | cap-combat-intrigue, cap-resource-gain, cap-retreat-choice | none | unknown |
| imperium:leader:baron-vladimir-harkonnen:v1#slot:private-faction-setup | Imperium / Leader | special | fully-sourced | verified | unsupported | not-tested | cap-leader-private-setup | int-original-private-leader-setup | batch-06c-baron-private-setup |
| imperium:leader:duke-leto-atreides:v1#slot:landsraad-discount | Imperium / Leader | unresolved | partially-sourced | unverified | not-implemented | not-tested | cap-leader-passive-discount | int-original-signet-leader | batch-06-starter-leader |
| imperium:leader:duke-leto-atreides:v1#slot:opponent-influence | Imperium / Leader | unresolved | partially-sourced | unverified | not-implemented | not-tested | cap-leader-signet | int-original-signet-leader | batch-06-starter-leader |
| imperium:reserve:arrakis-liaison:v1#slot:persuasion | Imperium / ReserveCard | primitive | fully-sourced | verified | implemented | tested | cap-card-reveal, cap-resource-gain | none | unknown |
| imperium:reserve:foldspace:v1#slot:unreadable-agent | Imperium / ReserveCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-07-foldspace |
| imperium:reserve:foldspace:v1#slot:unreadable-reveal | Imperium / ReserveCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-07-foldspace |
| imperium:reserve:spice-must-flow:v1#slot:spice | Imperium / ReserveCard | primitive | fully-sourced | verified | implemented | tested | cap-card-reveal, cap-resource-gain | none | unknown |
| imperium:reserve:spice-must-flow:v1#slot:victory-point | Imperium / ReserveCard | primitive | fully-sourced | verified | implemented | tested | cap-reserve-acquire, cap-resource-gain | none | unknown |
| imperium:space:arrakeen:v1#slot:controller-payout | Imperium / BoardSpace | special | fully-sourced | verified | implemented | tested | cap-control-payout, cap-effect-frames, cap-resource-gain | none | unknown |
| imperium:space:arrakeen:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-card-draw, cap-combat-placement, cap-recruit | none | unknown |
| imperium:space:carthag:v1#slot:controller-payout | Imperium / BoardSpace | special | fully-sourced | verified | implemented | tested | cap-control-payout, cap-effect-frames, cap-resource-gain | none | unknown |
| imperium:space:carthag:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | partially-implemented | not-tested | cap-agent-placement, cap-combat-placement, cap-intrigue-draw, cap-intrigue-recycle, cap-recruit | int-original-intrigue-recycling | batch-02-intrigue-recycle |
| imperium:space:conspire:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | partially-implemented | not-tested | cap-agent-placement, cap-influence, cap-intrigue-draw, cap-intrigue-recycle, cap-payment, cap-recruit, cap-resource-gain | int-original-intrigue-recycling | batch-02-intrigue-recycle |
| imperium:space:foldspace:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | unsupported | not-tested | cap-agent-placement, cap-foldspace-face, cap-influence, cap-reserve-acquire | none | batch-07-foldspace |
| imperium:space:great-flat:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-maker-harvest, cap-payment, cap-resource-gain | none | unknown |
| imperium:space:hagga-basin:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-maker-harvest, cap-payment, cap-resource-gain | none | unknown |
| imperium:space:hall-of-oratory:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-recruit | none | unknown |
| imperium:space:hall-of-oratory:v1#slot:reveal-bonus | Imperium / BoardSpace | special | fully-sourced | verified | implemented | tested | cap-resource-gain, cap-reveal-space | none | unknown |
| imperium:space:hardy-warriors:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-influence, cap-payment, cap-recruit | none | unknown |
| imperium:space:heighliner:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-influence, cap-payment, cap-recruit, cap-resource-gain | none | unknown |
| imperium:space:high-council:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-council-seat, cap-payment | none | unknown |
| imperium:space:high-council:v1#slot:reveal-bonus | Imperium / BoardSpace | special | fully-sourced | verified | implemented | tested | cap-council-seat, cap-resource-gain | none | unknown |
| imperium:space:imperial-basin:v1#slot:controller-payout | Imperium / BoardSpace | special | fully-sourced | verified | implemented | tested | cap-control-payout, cap-effect-frames, cap-resource-gain | none | unknown |
| imperium:space:imperial-basin:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-maker-harvest, cap-resource-gain | none | unknown |
| imperium:space:mentat:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-card-draw, cap-mentat, cap-payment | none | unknown |
| imperium:space:rally-troops:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-payment, cap-recruit | none | unknown |
| imperium:space:research-station:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-card-draw, cap-combat-placement, cap-payment | none | unknown |
| imperium:space:secrets:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | unsupported | not-tested | cap-agent-placement, cap-influence, cap-intrigue-draw, cap-intrigue-theft | int-original-secrets-theft | batch-03-secrets |
| imperium:space:secure-contract:v1#slot:placement | Imperium / BoardSpace | primitive | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-resource-gain | none | unknown |
| imperium:space:selective-breeding:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-card-draw, cap-influence, cap-payment, cap-selected-trash | int-original-selected-trash-reserve | batch-01-selected-trash |
| imperium:space:sell-melange:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-payment, cap-placement-choice, cap-resource-gain | none | unknown |
| imperium:space:sietch-tabr:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-recruit, cap-resource-gain | none | unknown |
| imperium:space:stillsuits:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-combat-placement, cap-influence, cap-resource-gain | none | unknown |
| imperium:space:swordmaster:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-payment, cap-swordmaster | none | unknown |
| imperium:space:wealth:v1#slot:placement | Imperium / BoardSpace | composite | fully-sourced | verified | implemented | tested | cap-agent-placement, cap-influence, cap-resource-gain | none | unknown |
| imperium:starter:convincing-argument:v1#slot:unreadable-agent | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:convincing-argument:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:dagger:v1#slot:unreadable-agent | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:dagger:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:diplomacy:v1#slot:unreadable-agent | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:diplomacy:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:dune-desert-planet:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:reconnaissance:v1#slot:unreadable-agent | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:reconnaissance:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:seek-allies:v1#slot:unreadable-agent | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:seek-allies:v1#slot:unreadable-reveal | Imperium / StartingCard | unresolved | unsourced | unverified | unsupported | not-tested | none | none | batch-06-starter-leader |
| imperium:starter:signet-ring:v1#slot:persuasion | Imperium / StartingCard | primitive | fully-sourced | verified | implemented | tested | cap-card-reveal, cap-resource-gain | none | unknown |
| imperium:starter:signet-ring:v1#slot:signet | Imperium / StartingCard | special | fully-sourced | verified | partially-implemented | not-tested | cap-leader-signet | int-original-signet-leader | batch-06-starter-leader |

### Shared mechanic dependencies

| Capability | Implementation / test | Prerequisites | Known dependent abilities | Supported subset | Missing delta | Blockers |
| --- | --- | --- | --- | --- | --- | --- |
| cap-agent-placement | implemented / tested | none | imperium:imperium-card:sardaukar-legion:v1#slot:recruit, imperium:space:arrakeen:v1#slot:placement, imperium:space:carthag:v1#slot:placement, imperium:space:conspire:v1#slot:placement, imperium:space:foldspace:v1#slot:placement, imperium:space:great-flat:v1#slot:placement, imperium:space:hagga-basin:v1#slot:placement, imperium:space:hall-of-oratory:v1#slot:placement, imperium:space:hardy-warriors:v1#slot:placement, imperium:space:heighliner:v1#slot:placement, imperium:space:high-council:v1#slot:placement, imperium:space:imperial-basin:v1#slot:placement, imperium:space:mentat:v1#slot:placement, imperium:space:rally-troops:v1#slot:placement, imperium:space:research-station:v1#slot:placement, imperium:space:secrets:v1#slot:placement, imperium:space:secure-contract:v1#slot:placement, imperium:space:selective-breeding:v1#slot:placement, imperium:space:sell-melange:v1#slot:placement, imperium:space:sietch-tabr:v1#slot:placement, imperium:space:stillsuits:v1#slot:placement, imperium:space:swordmaster:v1#slot:placement, imperium:space:wealth:v1#slot:placement | Original authored placement with available Agent and known card icon. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-agent-recall | unsupported / not-tested | cap-placement-provenance | none | unknown | No typed own-Agent recall operation; distinct from Spy recall. | cap-agent-recall-gap |
| cap-alliance-loss | unsupported / not-tested | cap-influence | none | unknown | FAQ p.1 closes this rule gap; current signed loss path remains gated. Preserve token and VP conservation. | cap-alliance-loss-gap |
| cap-card-draw | implemented / tested | none | imperium:space:arrakeen:v1#slot:placement, imperium:space:mentat:v1#slot:placement, imperium:space:research-station:v1#slot:placement, imperium:space:selective-breeding:v1#slot:placement | Authored direct DrawEffect during placement. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-card-reveal | implemented / tested | none | imperium:imperium-card:fremen-camp:v1#slot:persuasion-and-sword, imperium:imperium-card:sardaukar-legion:v1#slot:persuasion-and-swords, imperium:reserve:arrakis-liaison:v1#slot:persuasion, imperium:reserve:spice-must-flow:v1#slot:spice, imperium:starter:signet-ring:v1#slot:persuasion | Pictured original card/Reserve Reveal boxes. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-combat-intrigue | implemented / tested | none | imperium:intrigue:ambush:v1#slot:swords, imperium:intrigue:master-tactician:v1#slot:swords-or-retreat | Ambush and bounded Master Tactician choice. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-combat-placement | implemented / tested | none | imperium:space:arrakeen:v1#slot:placement, imperium:space:carthag:v1#slot:placement, imperium:space:great-flat:v1#slot:placement, imperium:space:hagga-basin:v1#slot:placement, imperium:space:hardy-warriors:v1#slot:placement, imperium:space:heighliner:v1#slot:placement, imperium:space:imperial-basin:v1#slot:placement, imperium:space:research-station:v1#slot:placement, imperium:space:sietch-tabr:v1#slot:placement, imperium:space:stillsuits:v1#slot:placement | Original troops and authored Combat spaces. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-commander-skills | unsupported / not-tested | cap-typed-combat-units | none | unknown | No Commander/Skill acquisition or once-turn supply recruitment state. | cap-commander-skills-gap |
| cap-control-payout | implemented / tested | none | imperium:space:arrakeen:v1#slot:controller-payout, imperium:space:carthag:v1#slot:controller-payout, imperium:space:imperial-basin:v1#slot:controller-payout | One Solari on Arrakeen/Carthag or one Spice on Imperial Basin, potentially to another player. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-copy-provenance | unsupported / not-tested | cap-graft-participants | none | unknown | Ghola needs copy-loop guard, executing self and definition source; full faces remain gated. | cap-copy-provenance-gap |
| cap-council-seat | implemented / tested | none | imperium:space:high-council:v1#slot:placement, imperium:space:high-council:v1#slot:reveal-bonus | Original High Council once-game purchase. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-deferred-mentat | unsupported / not-tested | none | imperium:conflict:sort-through-the-chaos:v1#slot:deferred-mentat | TakeMentatEffect(true) protects an immediate award, but this FAQ award is deferred. | Next-RoundStart queued Mentat reward and replay state are not authored. | cap-deferred-mentat-open |
| cap-effect-frames | implemented / tested | none | imperium:space:arrakeen:v1#slot:controller-payout, imperium:space:carthag:v1#slot:controller-payout, imperium:space:imperial-basin:v1#slot:controller-payout | Sourced authored effects under current frame and preflight contracts. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-endgame-conditional | implemented / tested | none | imperium:intrigue:corner-the-market:v1#slot:more-spice-must-flow, imperium:intrigue:corner-the-market:v1#slot:two-spice-must-flow | Corner the Market two separate Spice Must Flow conditions. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-expansion-initializer | unsupported / not-tested | cap-effect-frames | none | unknown | RulesetComposer recognizes recipes; expansion Setup is null and no executable adapters exist. | cap-expansion-initializer-gap |
| cap-final-reveal-tiebreak | unsupported / not-tested | cap-endgame-conditional | none | unknown | Current equal-tuple winners differ from FAQ p.2. Derive trusted history or explicitly version persisted ordering; do not silently reinterpret replays. | cap-final-reveal-tiebreak-gap |
| cap-foldspace-face | unsupported / not-tested | cap-reserve-return | imperium:space:foldspace:v1#slot:placement | Reserve stock identity exists, but Agent/Reveal boxes are UnsupportedEffect. | Foldspace whole card face remains unsupported; a complete supported Reserve definition also needs shared return routing for the sourced self-trash/Reserve rule (FAQ1 and I16). | cap-foldspace-face-open |
| cap-graft-participants | unsupported / not-tested | cap-placement-provenance | none | unknown | No paired physical participant bindings or per-card icon provenance; retain existing resolver. | cap-graft-participants-gap |
| cap-influence | implemented / tested | none | imperium:space:conspire:v1#slot:placement, imperium:space:foldspace:v1#slot:placement, imperium:space:hardy-warriors:v1#slot:placement, imperium:space:heighliner:v1#slot:placement, imperium:space:secrets:v1#slot:placement, imperium:space:selective-breeding:v1#slot:placement, imperium:space:stillsuits:v1#slot:placement, imperium:space:wealth:v1#slot:placement | Simple positive authored influence; conditional signed/alliance-loss branches remain gated. | Alliance holder loss below four requires source-backed future branch handling. | none |
| cap-intrigue-draw | implemented / tested | none | imperium:space:carthag:v1#slot:placement, imperium:space:conspire:v1#slot:placement, imperium:space:secrets:v1#slot:placement | Available shared-deck cards only; discard recycle is a separate gap. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-intrigue-recycle | unsupported / not-tested | none | imperium:space:carthag:v1#slot:placement, imperium:space:conspire:v1#slot:placement | Draw from a nonempty shared Intrigue deck is authored. | Pinned FAQ p.2 resolves recycling, but ValidateIntrigueReservations rejects mandatory draws past current deck stock. | intrigue-recycle-open |
| cap-intrigue-theft | unsupported / not-tested | none | imperium:space:secrets:v1#slot:placement | No authenticated hidden theft resolver. | No random opponent Intrigue selection/transfer with replay-safe privacy. | cap-intrigue-theft-open |
| cap-ix-infiltration | unsupported / not-tested | cap-placement-provenance | none | unknown | No runtime Ix placement adapter or icon permission provenance. | cap-ix-infiltration-gap |
| cap-leader-passive-discount | unsupported / not-tested | none | imperium:leader:duke-leto-atreides:v1#slot:landsraad-discount | No original LeaderRule passive hook is authored. | A bounded cost discount cannot be applied until the complete leader qualifier and passive hook are defined. | cap-leader-passive-discount-open |
| cap-leader-private-setup | unsupported / not-tested | cap-effect-frames | imperium:leader:baron-vladimir-harkonnen:v1#slot:private-faction-setup | unknown | Authentic private setup continuation with permitted observation/choice masks and once-game reveal ledger is absent; full Leader trigger remains source-gated. | cap-leader-private-setup-gap |
| cap-leader-signet | unsupported / not-tested | none | imperium:leader:duke-leto-atreides:v1#slot:opponent-influence, imperium:starter:signet-ring:v1#slot:signet | No authenticated original face is claimed supported by this capability. | Create() has no official LeaderRule and the Signet path rejects unsupported leaders. | cap-leader-signet-open |
| cap-maker-harvest | implemented / tested | none | imperium:space:great-flat:v1#slot:placement, imperium:space:hagga-basin:v1#slot:placement, imperium:space:imperial-basin:v1#slot:placement | Three authored Spice Trade harvest spaces. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-mentat | implemented / tested | none | imperium:space:mentat:v1#slot:placement | Original Mentat board space when token is present; contested cases remain preflight gated. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-opponent-discard-order | unsupported / not-tested | none | imperium:imperium-card:reverend-mother-mohiam:v1#slot:opponent-discard-order | No complete Mohiam face or opponent-selection effect is authored. | Card-specific opponent selection, hidden discard choice and deterministic order are not authored. | cap-opponent-discard-order-open |
| cap-optional-payment | implemented / tested | none | imperium:imperium-card:fremen-camp:v1#slot:optional-recruit | Fremen Camp optional two-Spice recruitment. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-payment | implemented / tested | none | imperium:imperium-card:fremen-camp:v1#slot:optional-recruit, imperium:space:conspire:v1#slot:placement, imperium:space:great-flat:v1#slot:placement, imperium:space:hagga-basin:v1#slot:placement, imperium:space:hardy-warriors:v1#slot:placement, imperium:space:heighliner:v1#slot:placement, imperium:space:high-council:v1#slot:placement, imperium:space:mentat:v1#slot:placement, imperium:space:rally-troops:v1#slot:placement, imperium:space:research-station:v1#slot:placement, imperium:space:selective-breeding:v1#slot:placement, imperium:space:sell-melange:v1#slot:placement, imperium:space:swordmaster:v1#slot:placement | Known Solari, Spice and Water costs with bounded affordability. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-placement-choice | implemented / tested | none | imperium:space:sell-melange:v1#slot:placement | Four Sell Melange tiers with one exchange per placement. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-placement-provenance | unsupported / not-tested | cap-agent-placement | none | unknown | Kwisatz Haderach, Voice and per-icon permissions need bounded placement context; no separate resolver. | cap-placement-provenance-gap |
| cap-plot-window | unsupported / not-tested | none | imperium:intrigue:calculated-hire:v1#slot:plot | No authenticated original face is claimed supported by this capability. | The authored Calculated Hire uses UnsupportedEffect and the selected Plot window is gated. | cap-plot-window-open |
| cap-recruit | implemented / tested | none | imperium:imperium-card:fremen-camp:v1#slot:optional-recruit, imperium:imperium-card:sardaukar-legion:v1#slot:recruit, imperium:space:arrakeen:v1#slot:placement, imperium:space:carthag:v1#slot:placement, imperium:space:conspire:v1#slot:placement, imperium:space:hall-of-oratory:v1#slot:placement, imperium:space:hardy-warriors:v1#slot:placement, imperium:space:heighliner:v1#slot:placement, imperium:space:rally-troops:v1#slot:placement, imperium:space:sietch-tabr:v1#slot:placement | Original authored board recruitment and combat deployment. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-research-genetic | unsupported / not-tested | cap-effect-frames | none | unknown | No research/genetic/Beetle/Specimen acquisition state adapter; source complete individual faces first. | cap-research-genetic-gap |
| cap-reserve-acquire | implemented / tested | none | imperium:reserve:spice-must-flow:v1#slot:victory-point, imperium:space:foldspace:v1#slot:placement | Available authored Reserve definitions only; Foldspace face remains gated. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-reserve-return | implemented / tested | cap-selected-trash | none | Actual setup Reserve definitions return to their exact shared stack, including an empty stack; finite-stock preflight credits only a selected return. | No additional delta for the actual three Reserve definitions; unsupported Foldspace face remains gated. | none |
| cap-resource-gain | implemented / tested | none | imperium:imperium-card:fremen-camp:v1#slot:persuasion-and-sword, imperium:imperium-card:sardaukar-legion:v1#slot:persuasion-and-swords, imperium:intrigue:ambush:v1#slot:swords, imperium:intrigue:corner-the-market:v1#slot:more-spice-must-flow, imperium:intrigue:corner-the-market:v1#slot:two-spice-must-flow, imperium:intrigue:master-tactician:v1#slot:swords-or-retreat, imperium:reserve:arrakis-liaison:v1#slot:persuasion, imperium:reserve:spice-must-flow:v1#slot:spice, imperium:reserve:spice-must-flow:v1#slot:victory-point, imperium:space:arrakeen:v1#slot:controller-payout, imperium:space:carthag:v1#slot:controller-payout, imperium:space:conspire:v1#slot:placement, imperium:space:great-flat:v1#slot:placement, imperium:space:hagga-basin:v1#slot:placement, imperium:space:hall-of-oratory:v1#slot:reveal-bonus, imperium:space:heighliner:v1#slot:placement, imperium:space:high-council:v1#slot:reveal-bonus, imperium:space:imperial-basin:v1#slot:controller-payout, imperium:space:imperial-basin:v1#slot:placement, imperium:space:secure-contract:v1#slot:placement, imperium:space:sell-melange:v1#slot:placement, imperium:space:sietch-tabr:v1#slot:placement, imperium:space:stillsuits:v1#slot:placement, imperium:space:wealth:v1#slot:placement, imperium:starter:signet-ring:v1#slot:persuasion | Direct GainEffect from authored cards and spaces. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-retreat-choice | implemented / tested | none | imperium:intrigue:master-tactician:v1#slot:swords-or-retreat | Master Tactician own Conflict troops. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-reveal-draw-timing | unsupported / not-tested | cap-card-draw | none | unknown | Existing gain/draw frames do not prove correct during-Reveal versus after-Reveal handling for unknown faces. | cap-reveal-draw-timing-gap |
| cap-reveal-generation-total | unsupported / not-tested | cap-card-reveal | none | unknown | No generation-total query/turn entitlement; repeated Combat icons must not multiply old-garrison allowance. | cap-reveal-generation-total-gap |
| cap-reveal-space | implemented / tested | none | imperium:space:hall-of-oratory:v1#slot:reveal-bonus | Hall of Oratory while own Agent occupies the space. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-sandworm-reward | unsupported / not-tested | cap-typed-combat-units | none | unknown | No sandworm unit/reward adapter; optional Wall destruction, permanent removal and doubling exclusions must remain explicit. | cap-sandworm-reward-gap |
| cap-selected-trash | implemented / tested | none | imperium:space:selective-breeding:v1#slot:placement | Own Hand, Discard and InPlay with visible zone/definition/copy selectors; bounded direct selected-trash effects only. | No additional delta for this bounded subset; other card-family semantics and copied-card triggers remain separate gates. | none |
| cap-semantic-card-movement | unsupported / not-tested | cap-selected-trash | none | unknown | Unload/paid self-trash/copied self require causal children and source-correct occurrence handling. | cap-semantic-card-movement-gap |
| cap-spy-payment | unsupported / not-tested | cap-placement-provenance | none | unknown | No Spy/observation post state or payment hook; same Spy cannot fund both uses. | cap-spy-payment-gap |
| cap-swordmaster | implemented / tested | none | imperium:space:swordmaster:v1#slot:placement | Original Swordmaster space. | No additional delta for the stated bounded supported subset; full authentic faces and interactions are gated separately. | none |
| cap-tech-usage | unsupported / not-tested | cap-effect-frames | none | unknown | No Tech ownership/usage adapter; FAQ permits once-round activation on Agent or Reveal turn. | cap-tech-usage-gap |
| cap-typed-combat-units | unsupported / not-tested | cap-combat-intrigue | none | unknown | Troop-only counters cannot represent dreadnought durability, sandworms or Commanders. | cap-typed-combat-units-gap |
| cap-voice-lifetime | unsupported / not-tested | cap-agent-placement | none | unknown | Control-marker conservation and next-turn lifetime are absent; full card face remains a source gate. | cap-voice-lifetime-gap |
<!-- END GENERATED CONTENT COVERAGE -->
