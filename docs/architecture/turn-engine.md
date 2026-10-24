# Turn engine, legal actions and effects

SLICE-003 adds a bounded original Imperium (2020) turn executor. It reuses the
SLICE-002 `GameState`, `PlayerState`, physical `CardInstance`, `CardZone`, setup and
seeded RNG. **No authentic configuration is fully playable or eligible for RL
training.** Missing inventory, unverified card boxes and leader abilities remain
explicit gates. See [coverage](../implemented-mechanics.md) and [ADR 0004](../adr/0004-incremental-turn-execution.md).

## Entry points and ownership

`TurnEngine(GameState, TurnRuleSet)` accepts trusted episode state and immutable
executable definitions. `StartRound()`, `Observe(player)`, `LegalActions(player)`
and `Step(action)` provide the sequential interaction boundary. `GameState` remains
privileged; agents get `TurnObservation`, never the state, seeds or replay records.
The facade deliberately stops at `CombatBoundary`, not at a terminal game result.
The existing generic full-episode interface and Toy remain unchanged.

Round Start reveals the next Conflict and draws five cards per player through the
existing draw/refill operation. Players act clockwise from First Player. An Agent
turn uses one Agent and moves the selected hand card into in-play. Early Reveal is
allowed. A player's completed Reveal removes them from further scheduling; when
all players finish, no decision owner remains and the engine stops before Combat.
Current player identifies the active turn; decision owner identifies who must
choose now. Effects can keep the same turn active across many decisions or give a
nested choice to another player.

## Action model

| Action | Meaning |
| --- | --- |
| `StartRoundAction` | Explicit trusted chance/round transition; recorded in replay |
| `PlaceAgentAction` | Select visible hand card, space and a bounded mandatory payment option |
| `ResolveEffectAction` | Select the next pending effect when order is a player choice |
| `ChooseOptionAction` | Resolve an optional payment or one named effect choice |
| `DeployAction` | Choose troop count, including zero |
| `RevealAction` | Reveal all remaining hand cards and enqueue their Reveal effects |
| `AcquireCardAction` | Buy a visible row/Reserve card during Reveal |
| `EndRevealAction` | Finish acquisition and cleanup after required effects resolve |

A card selector uses visible content identity plus its duplicate index in the
visible zone. It never contains the raw physical ID allocated by setup in sorted
content ranges. Internal identities remain stable across transfers. Identical
visible copies are interchangeable in the currently implemented mechanics; a
future card-specific modifier must introduce an opaque per-instance policy handle
before that assumption changes. Legal actions are deterministically ordered and
empty for inactive viewers. Legal enumeration returns only the implemented subset,
and `SupportDiagnostics` explains omitted unsupported choices. This subset must
never be described as the complete official action set.

The engine validates ownership, phase, icons, occupancy, mandatory costs,
influence, once-per-game restrictions and complete selected effect graphs before
mutation. Mandatory payment cannot use resources earned by that placement. Sell
Melange's four payment options are bounded placement choices, not a Cartesian
product of an entire turn. Unsupported card and leader mechanics are diagnosed
separately from illegal player input. Rejected actions do not consume randomness,
move cards, change pending work or enter accepted history.

## Effects and suspended execution

Executable card/board rules contain immutable typed effects. Primitives cover
resource/Persuasion/sword gains, draw, recruitment, influence, acquisition,
optional costs, choices, explicit target-player contexts, self-trash, High Council,
Swordmaster, bonus-spice harvest and Signet dispatch. Unsupported definitions have
an explicit unsupported primitive rather than an empty fake effect list.

Pending effects retain source card, source space and effect owner. A decision frame
suspends execution while an optional payment or choice waits. The engine preserves
meaningful resolution order and never assumes each action finishes a turn.
Reveal acquisitions remain available between effect resolutions; mandatory nested
choices are completed before resuming the outer decision. Acquired cards enter
discard and do not trigger their Agent or Reveal box. Their Acquire effects happen
once. Row acquisitions refill from the deck immediately; Foldspace is excluded
from Persuasion purchases.

Recruitment moves at most the available supply into garrison. Deployment at a
Combat space permits any troops recruited this turn plus up to two troops that
were already in garrison. A player chooses the count explicitly. Preliminary
Combat strength uses participating troop contributions and swords, with zero
strength when no troop participates. No combat rewards, Intrigue responses,
Makers, Recall or termination are executed here.

Faction influence at two grants its VP. Dependencies at the higher alliance/bonus
threshold remain gated until implemented. This is an explicit restricted preview,
not a modified faction rule. Every production leader needs complete supported
passive and Signet behavior; metadata or a name does not authorize a no-op leader.

## Observations and trusted data

`TurnObservation` wraps the detached SLICE-002 projection and adds the viewer's
permitted pending summary and legal actions. Public spaces, resources, influence,
in-play/discard/row/Reserve cards and revealed Conflict are visible. Only the owner
sees their hand and Intrigue identities. Hidden deck order, unrevealed Conflicts,
opponents' private cards, RNG state and privileged history are absent. Asking for
an observation or legal actions cannot advance RNG or mutate execution.

`CaptureTrustedSnapshot()` returns canonical detached turn data and SHA-256,
including the complete setup state/RNG checkpoints, pending effects, decision
stack, deployment eligibility, once-per-game uses and accepted history. It is
explicitly privileged debugging data. It must never be policy input.

## Replay

`CaptureReplay()` records versioned metadata, environment/leader seeds, initial
trusted identity and the ordered accepted actions. `TurnEngine.Replay(record,
initialStateFactory, rules)` reconstructs the same starting state and rejects
foreign runtime, engine, ruleset, content, support revision or initial identity.
It then reapplies actions and compares the final complete turn hash. The caller
supplies the appropriate initializer/factory; the replay API is trusted developer
infrastructure, not a public hidden-state restore path.

Replay determinism requires the pinned runtime, identical content/rules and the
same initial setup and actions. It does not promise cross-runtime System.Random
streams, changed-policy replay, or compatibility with changed rules. No clock or
shared random generator participates. Separate games own all mutable execution.

## Extensibility and limits

Typed effects, explicit owners and immutable resolved definitions accommodate
future dedicated complex effects without a script interpreter. Occupancy and
combat-strength contributions have narrow rule seams for future infiltration and
unit types. No Ix/Uprising/Immortality/Bloodlines mechanic is implemented, and
unsupported combinations cannot borrow these base effects automatically.

This slice establishes ordinary managed baselines. No dependency, allocation
framework, reflection dispatcher, event-sourcing system or custom PRNG was added.
See [benchmark report](../benchmarks/slice-003.md) for measured scope and limits.

SLICE-003.5 verifies these extension limits in the
[ability and decision audit](ability-decision-audit.md). Nested option ownership
and acquisition priority work today; general causal continuation, Combat priority
windows, source/recipient role separation and scoped modifiers are proposed in
[ADR 0006](../adr/0006-ability-and-decision-execution.md), not implemented support.


### SLICE-004 faction prerequisites

Sourced lifecycle faction definitions activate bounded signed Influence, recurring
threshold-four bonuses and authoritative Board.Alliances ownership. Influence uses
0..6 saturation; preview rules lacking a faction definition retain their threshold
gate. A threshold reward pushes a P1 child frame with the effect's original roles
and returns before the parent continues. The primitive drain re-reads the top frame
after pushing that child.

A private, preflight-only faction projection exposes threshold work to the existing
bounded resource, acquisition-stock, permanent-upgrade and funding checks. Its
acquisition reservation marker never enters execution or canonical serialization.
Suspended Sequence order is preserved across decisions; unordered competing signed, cross-player
or differently bound payer influence and subsequent influence dependent on conditional ownership
remain explicit unsupported gates. Intrigue draws reserve the shared deck jointly;
no supplied source establishes shared-deck recycling, so unavailable mandatory draws
reject before mutation while optional declines remain legal. Holder-loss cases with
qualifying rival ownership or ties remain gated pending source clarification.

This changes turn/support semantics to v6 and lifecycle authoring to v4 without new
trusted fields; the v3 snapshot and replay schemas remain unchanged.
