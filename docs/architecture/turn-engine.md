# Turn engine, legal actions and effects

SLICE-003 introduced the original Imperium (2020) turn executor; SLICE-004 extends
its supported lifecycle. It reuses the
SLICE-002 `GameState`, `PlayerState`, physical `CardInstance`, `CardZone`, setup and
seeded RNG. **No authentic configuration is fully playable or eligible for RL
training.** Missing inventory, unverified card boxes and leader abilities remain
explicit gates. See [coverage](../implemented-mechanics.md) and [ADR 0004](../adr/0004-incremental-turn-execution.md).

## Entry points and ownership

`TurnEngine(GameState, TurnRuleSet)` accepts trusted episode state and immutable
executable definitions. `StartRound()`, `Observe(player)`, `LegalActions(player)`
and `Step(action)` provide the sequential interaction boundary. `GameState` remains
privileged; agents get `TurnObservation`, never the state, seeds or replay records.
Without a lifecycle definition, the bounded preview stops at a nonterminal `Combat` boundary.
Supported lifecycle definitions continue through recurring rounds and Endgame to
the terminal result described below. The generic full-episode interface and Toy
remain unchanged.

Round Start reveals the next Conflict and draws five cards per player through the
existing draw/refill operation. Players act clockwise from First Player. An Agent
turn uses one Agent and moves the selected hand card into in-play. Early Reveal is
allowed. A player's completed Reveal removes them from further scheduling; when
all players finish, lifecycle definitions open Combat decisions; the bounded turn
preview instead stops before Combat with no decision owner.
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
| `PlayIntrigueAction` | Play a supported own card in Combat, winner PostWin, or Endgame |
| `PassPriorityAction` | Finish the current opportunity; consecutive no-play passes close all-seat windows |

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
strength when no troop participates. With lifecycle definitions, the same engine
resolves Combat rewards, Intrigues, Makers, Recall and Endgame; without them the
old bounded preview remains.

Faction influence at two grants its VP. Lifecycle faction definitions enable
recurring threshold-four rewards and sourced Alliance transfers; unresolved holder
losses remain gated. This is an explicit restricted preview,
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
[ability and decision audit](ability-decision-audit.md). It remains a historical
assessment. SLICE-004 implements causal continuation, explicit
decision/payer/recipient roles and Combat windows in
[ADR 0006](../adr/0006-ability-and-decision-execution.md). Scoped modifiers, KH
and Graft remain proposals.


The following stage subsections record deliberate version evolution. Current
contracts are recorded in the SLICE-005 section below.

### SLICE-004 faction prerequisites

Sourced lifecycle faction definitions activate bounded signed Influence, recurring
threshold-four bonuses and authoritative Board.Alliances ownership. Influence uses
0..6 saturation; preview rules lacking a faction definition retain their threshold
gate. A threshold reward pushes a P1 child frame with the effect's original roles
and returns before the parent continues. The primitive drain re-reads the top frame
after pushing that child.

A private, preflight-only faction projection exposes threshold work to the existing
bounded resource, acquisition-stock, permanent-upgrade, funding and special-operation checks. Its
acquisition reservation marker never enters execution or canonical serialization.
Generated mandatory Mentat/control awards and combined Mentat/Swordmaster Agent
capacity participate in the same availability and reservation checks as authored
enclosing work, including suspended parents and later Conflict recipients. The
scarce-operation Choice gate sees projected threshold descendants too; optional
payments retain decline and are checked before selected payment or child activation.
Accepted synthetic threshold graphs have the same precommit contract as sourced
definitions. This F1 correction advances support/preview to v13, explicitly rejects
support v12 replay headers, and changes no serialized shape or authored faction
bonus: setup v5, turn/replay v7, lifecycle authoring v9 and result/identity v1 remain.
With the original three markers and three controllable locations, a coherent owner
with zero spare markers already controls every location, making those grants
idempotent. Tests use that bound, valid one-marker control and competing recipients;
no impossible zero-marker/unowned-target fixture is presented as authentic proof.
Suspended Sequence order is preserved across decisions; unordered competing signed, cross-player
or differently bound payer influence and subsequent influence dependent on conditional ownership
remain explicit unsupported gates. Intrigue draws reserve the shared deck jointly;
no supplied source establishes shared-deck recycling, so unavailable mandatory draws
reject before mutation while optional declines remain legal. Every holder loss below four remains gated, including with no qualifying rival;
the supplied evidence does not establish surrender or retention. Holder losses with
qualifying rival ownership or ties remain gated. A supported holder loss must remain
at least four with a strict lead.

This changes turn/support semantics to v7 and lifecycle authoring to v5 without new
trusted fields; the v3 snapshot and replay schemas remain unchanged.

### SLICE-004 Combat outcome

Combat closure now freezes `CombatOutcome` in the trusted turn snapshot from current
unit and sword strength, including the zero-eligible path. Rankings use official
tie reward positions while tied player IDs serialize in seat order. Reward
execution and recurring cleanup now follow the frozen outcome as described below.
The added trusted field advances
turn snapshot and replay schemas to v4, and turn support/preview semantics to v8;
setup remains v3 and lifecycle authoring remains v5.

### SLICE-004 Conflict rewards and control

The engine now executes the frozen outcome through `ConflictRewardProgress`,
which records ordered recipients, the current recipient, closed post-win
opportunities and its root frame. Trusted snapshots include remaining execution
work and the frozen outcome; no second ranking or effect executor is introduced.
The final Combat pass preflights all mandatory recipients before committing it.
Initial-Combat test seams need explicit revealed Conflict/reward definitions when
any positive-strength recipient receives an award.

The unique winner has one PostWinRewards window linked to an explicit
PlayerOrdered reward root. Remaining root items and post-win cards can interleave.
A card suspends that exact root/window and its child exclusively owns decisions;
returning resumes the same root. Passing closes this opportunity once, after all
mandatory root work, and starts lower recipients. Lower-recipient children use the
same frame stack without a priority window. Preflight includes unfinished root
and later recipients, so selected cards cannot exhaust their required resources,
Intrigue or acquisition stock. Conditional gain safety is conservative and does
not query opponent hidden owned counts; only actual execution evaluates the two
existing Spice Must Flow predicates.

Control belongs to `BoardSpaceState.ControllerId`, with marker supply changed
only on actual controller replacement. Existing `BoardRule.ControlReward` remains
the sole placement bonus definition. A Conflict's `ControlSpace` identifies
matching defense; an explicit `ControlSpaceEffect` grants printed reward control.
RoundStart defense uses the existing OptionalPayment/child-frame machinery and a
typed RoundStart continuation before five-card draws. `DeployFromSupplyEffect`
is reserved for that internal decision and does not enable Combat deployment or
re-entry cards. A Conflict Mentat award records `ProtectedThroughRound` now;
Agent-kind provenance, recurring round transitions and Recall are implemented below.

This advances setup snapshots to v4, turn snapshots/replays to v5, turn
preview/support to v10 and lifecycle authoring to v7 (including the tied shared-draw
safety revision). The official lifecycle
registry remains partial and no authentic configuration becomes playable.
Reward completion continues through cleanup, Makers and Recall as described below;
the Endgame window supplies terminal scoring and the structured result.

Canonical tied-seat reward scheduling excludes positive shared Intrigue draws: no
supplied rule establishes tied distribution of distinct hidden cards. Preflight
expands actual threshold children through the existing faction projector, then
checks wrappers/acquisition/Signet work with a bounded traversal. Zero draws and
unreached faction bonuses remain harmless; unique and untied reward draws remain
supported. Active lower-recipient ConflictRewards frames use normal candidate
support diagnostics even without a priority window. This safety revision changes
support/authoring semantics only; setup/turn/replay shapes stay v4/v5/v5.

### SLICE-004 recurring-round lifecycle

Reward completion now resumes `CompleteCombat` in the same engine. The active
original cleanup policy returns deployed troops to supply, resets swords/markers
and clears completed window/outcome/reward state. `RunMakers` uses resolved board
metadata while occupants still exist. `RunRecall` then records public
`DuneTerminalReason` flags for VP >=10 and/or Conflict deck exhaustion. A trigger
enters the existing Endgame window without Agent recall, marker rotation or a new
hand. Final scoring/results follow the Endgame window described below.

Without a trigger, Recall reconstructs available Agents from typed ownership and
Mentat lifetime, clears placed occupants and rotates First Player clockwise.
`BeginRound` increments the round, moves the previous revealed Conflict to public
discard, reveals the next and reuses the existing typed RoundStart defense
continuation before Draw5. Existing deck/discard reshuffling and Reveal card
cleanup remain the only implementations; no second phase scheduler, fake policy
actions or duplicate card discard were added. `StartRoundAction` remains legal
only before round one.

`PlayerUnits` includes ordinary Agent ownership, acquired/available Swordmasters
and available Mentat. Placement records the exact kind, choosing Mentat, ordinary
Agent, then Swordmaster deterministically for currently interchangeable uses.
Recall preserves permanent Swordmaster/Council/once-game access, resources,
Influence, control and accumulated spice. A Conflict Mentat earned in R stores
R+1 and returns at that round's Recall, rather than gaining an extra round of
protection. Ordinary Mentat returns at the current nonterminal Recall, placed or
unused. Endgame preserves the final placed/unspent state. Trusted/public views
include these typed public counts and terminal flags as detached immutable data.

Closing actions preflight automatic Maker bounds and future Conflict support
before committing. The source-verified Mentat board space draws for its two-Solari
placement even when no token is available. Swordmaster is an eight-Solari,
once-game permanent acquisition. Contested rewards and existing unsupported
content gates remain explicit.

At SLICE-004, schemas were setup **v5**, turn snapshot/replay **v7**, preview/support
**v13**, lifecycle authoring **v9**. The shared synthetic fixture replays multiple
rounds and the entire selected ten-card Conflict deck with conservation and
seeded reshuffles; the official catalog remains incomplete and no authentic
configuration is declared playable.

### Endgame and trusted result

Endgame uses the existing all-seat window, with a null normal player and an explicit
decision owner. A seat can play any number of supported order-independent Endgame
Intrigues before passing. Any play clears earlier passes; closure requires consecutive
no-play passes from every seat. This clockwise opportunity schedule is an engine
convention, not an official card-priority rule. Nested optional/foreign work returns
to the same opportunity. Unsupported faces/timings retain their explicit diagnostics;
lifecycle startup still validates the entire enabled inventory, independently of
hidden deck positions.

Corner the Market reuses the typed conditional runtime: its two printed VP
conditions evaluate separately at resolution. Owned The Spice Must Flow copies in
deck, hand, discard and in-play count; shared trash and Reserve do not. Conservative
preflight bounds both awards without consulting hidden opponent ownership. The
printed outcome may change public VP, but hidden cards remain redacted after completion.

Closing Endgame sets `Completed`, clears decision ownership/window/work, and blocks
every subsequent action, including `StartRound`. `IsTerminal` reports completion;
`GetResult()` throws until then and returns detached immutable `DuneGameResult`
afterwards. Final ranks use VP, spice, Solari, water and garrison troops, in that
order. Applied tiebreaks record only criteria that actually split each seat's
successively tied groups. Equal full tuples share competition rank and winner
status. Standings and winner IDs serialize in seat order; seat order never breaks ties.

The trusted result records setup/configuration/ruleset/content/rules/support
identities and versions, inventory authenticity, both setup seeds, rounds, Recall
reason flags and final standings. It carries no private cards and makes no claim
of a complete authentic playable catalog. Seeds and replay identity stay outside
policy observations. Turn snapshots include the result value. Its replay identity
is SHA-256 of `dune-turn-replay-identity-v1`: the versioned replay header and ordered
accepted actions, excluding the final-state hash and result. Construction occurs
after appending the closing action, avoiding both circular hashing and omitted
final passes. Earlier replay versions are rejected explicitly. `TurnEngine` does
not implement the generic simulation `IGame` contract.

## SLICE-004 final integration evidence

`LifecycleIntegrationTests.EveryAcceptedPrefixReplaysNestedForeignRewardsThroughNaturalEndgame`
reconstructs every accepted prefix of an actual 3/4-seat ten-round synthetic game,
including nested acquisition, foreign optional payment, PostWin and Endgame. It
compares canonical trusted state/hash, both RNG checkpoints, result, next owner
and ordered legal arrays; it checks rejected input and policy redaction at every cut.
Natural empty private Hands in Combat/Endgame/Completed are recorded and asserted,
not counted as nonempty counterfactual coverage.
`RealSelectedWindowActionsReachPhaseTimingCostAndGraphGuards` adds real-card,
correct-actor window rejection cases with exact diagnostics, unaffordable card and
optional payments, and funded/supported positive controls. Its explicitly synthetic
initial-window seams do not imply that unsupported content passes full startup;
`UnsupportedEnabledFaceRejectsAtSetupBeforeAnyWindowCanBeReached` verifies that gate.
`SupportedDrawsProvideDistinctNonemptyHiddenFacesAcrossNaturalWindowsAndCompletion`
uses supported Combat/Endgame draws in actual 3/4-seat ten-round fixtures to provide
nonempty opponent Hand/Intrigue/Deck at PlayerTurns, Combat, PostWin, Endgame and
Completed. Coverage guards require every cut; each exchanged face must differ,
all zone counts/public state must match, and observations/masks/diagnostics remain
equal after hidden exchanges and a verified RNG checkpoint change. Final action
binding is checked directly against the canonical replay-identity payload,
independently of the final-state hash.
`ConcurrentEpisodesShareOnlyImmutableDefinitionsAndRemainScheduleIndependent`
shares resolved definitions/rules while retaining independent mutable episode state.

`tests/Fixtures/LifecycleFixtureGame.cs` is a development-only `IGame` adapter for
the existing `EpisodeRunner`, with `LifecycleFixtureAgent` choosing only from
public observations/legal actions. Engine production types do not depend on
Simulation. The CLI reports accepted Combat/PostWin card plays by typed fixture
timing and labels Makers/Recall as automatic round consequences. Counts are
observations of the selected run, not promises about arbitrary seeds.

See [SLICE-004 measurements](../benchmarks/slice-004.md) and the
[complete handoff](../handoffs/SLICE-004-result.md). No public snapshot-import or
search-clone API and no unexpected-exception rollback guarantee is introduced.

### SLICE-005 selected trash and current version

`SelectedTrashEffect(ImmutableArray<Effect> OnTrashed, bool Optional = true)`
uses the existing frame and `TurnEngine.Execute` path. `LegalActions` offers
`SelectTrashCardAction(PlayerId, DecisionId, Source, Card)` for owner-visible
Hand, Discard and InPlay cards and `DeclineTrashAction(PlayerId, DecisionId)`
only for optional effects. `ValidateSelectedTrash` checks the decision, zone,
selector, physical target and complete generated/suspended work before
`CommitSelectedTrash` moves the card. The private `SelectedTrashDecision` freezes
physical target bindings while policy actions carry no physical instance ID.
`SelectedCardTarget` remains separate from the executing `SourceCard`,
`SourceDefinition` and nullable trusted `SourceOwnerId`. `RootContext` binds a
physical source owner; `Acquire` rebinds it to the acquiring player. Role changes
preserve it across `ForPlayer` and nested continuations. Public observations
show only the chooser's permitted targets; other seats cannot inspect pending
selection, physical IDs, frames or RNG.

`TrashDestination` and `MoveTrashedCard` route each actual Reserve definition to
its matching shared stack and ordinary cards to shared trash. Return-aware
`ValidateAcquisitionStock` credits only a selected, validated return to later
work. A return cannot fund earlier acquisition or an unsafe reacquire-then-
self-trash path; those remain conservative support gates. Selective Breeding
uses mandatory 2-Spice placement and Influence, with an optional one-card
trash-for-two-draw exchange. The existing card, board and Influence ordering
continues to apply; trash precedes draw/refill when the exchange is taken.

Current contracts are setup v5, turn snapshot/replay v9, support/preview v15,
authoring v11 and result/replay-identity v1. Snapshot/replay v8, support/preview
v14 and authoring v10 were intermediate development formats and reject; their
v7/v13/v9 predecessors also reject. No migration is supplied. Configuration
identity v1, ruleset fingerprint v2 and embedded catalog identity remain
unchanged. New authored board execution changes `RulesIdentity` and, together
with new headers/actions, resulting snapshot and replay hashes; the hash
algorithm/version is unchanged. Three- and four-seat synthetic complete games
replay every accepted prefix, including paused selections. No authentic setup
is newly playable.
