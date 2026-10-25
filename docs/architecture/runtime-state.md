# Runtime setup and state (SLICE-002)

Original **Dune: Imperium (2020)** Standard competitive setup supports three or
four seats and fixed or random unique leaders. This setup boundary stops immediately
before Round Start; the later turn engine implements Agent/Reveal, supported
Combat/round/Endgame execution and legal actions. Toy and the explicitly synthetic
lifecycle scenario are executable; authentic full-game initialization remains gated.

## Lifecycle and readiness

`GameConfiguration -> RulesetComposer -> ResolvedGameDefinition -> GameInitializer`

Composition still validates metadata for the broader SLICE-001 combinations.
Only the unchanged original base module receives an executable
`GameSetupDefinition`. Expansions, Uprising, Epic, variants, drafts and explicitly
provided custom module graphs have no executable adapter in this slice. An
unsupported result is a diagnostic, never a fallback to a base or Toy game.
Future module-specific lowering can construct another immutable typed setup using
the same runtime zones, board definitions and state model. Prose recipe strings
are not executable instructions. The selected static setup, including its board,
resource recipe and card-pool/tier counts, participates in ruleset fingerprint v2.

`GameInitializer.Create` composes and validates before allocating mutable state.
`Initialize` accepts a reusable resolved definition and verifies its configuration
binding. A failure returns errors and no state. Config identity canonicalizes
sets ordinally; resolved binding excludes the two seeds, while the episode identity
includes them. A definition can be reused for otherwise identical seeded games.
Share this immutable definition across simulations; never share runtime state.
A thread-safe initializer-local weak cache validates/hashes static metadata once per
resolved object; configuration binding is checked on every initialization. Cold
composition/static validation is excluded from the repeated-initialization benchmark.

The public initializer requires `CatalogCompleteness.Complete`, verified metadata,
applicable original-game sources, required starter/Reserve/board identities, and
exact component totals, including Conflict tiers. The current bundled catalog
fails that gate. See [content coverage](../content/catalog-schema.md).
Verification flags are curated author assertions; source-page checks cannot prove
an individual card's identity or substitute for a physical inventory audit.

Tests and benchmarks compile a shared, internal synthetic source. An internal
friend-assembly initialization entry point skips authenticity checks while retaining
structural/count checks. Its state and snapshots are labeled `SyntheticFixture`.
There is no production fallback, public bypass parameter, copied third-party
catalog or claim that synthetic cards are official Dune content.

## Static definitions and mutable state

`GameState` is privileged engine/debug data. It references the immutable resolved
definition and owns episode-local players, board, shared zones, Conflict state,
random generators, phase/status and temporary state. All mutating operations and
setters are internal. Collection views do not expose mutable lists or arrays.
Separate instances have separate collections even when their definitions match.
Typed module extension state provides an expansion boundary without a property
for every possible future track/resource/unit. Extensions require deliberate
snapshot and observation projections before use.

Player state includes seat/leader, resources, VP, four influence tracks, Agents,
Swordmaster and Council status, troops, control markers, personal zones and
round-local state. Alliance ownership is public. The base setup values belong to
the resolved recipe; they are not universal defaults for future modes.

Board spaces have stable `BoardSpaceId` values, independent of display text.
`BoardDefinition` selects the active layout and space definitions. Episode-local
space state contains occupancy, bonus spice and control. Replacements can supply
new definitions and reuse the state container; effects belong to future rule
consumers rather than board-state methods. Conflict is initialized without a
revealed card. Mentat begins on its own space, with no player Agent deployed.
The bank models unlimited resources, per Setup F2, rather than token scarcity.

## Card identity, ownership and zones

`ContentDefinition` is shared static metadata; `CardInstanceId` identifies one
physical copy inside one episode. `CardInstance` refers to its definition. IDs
are deterministic episode-local values and are not globally unique. Equal card
definitions can have distinct instance IDs. `ConflictTier` is setup metadata,
separate from unimplemented reward/effect behavior.

`CardZone` owns its private list and records stable zone identity, owner and
visibility (`Public`, `OwnerOnly`, `Hidden`). Ownership follows the current zone,
so acquisition/transfer does not replace a physical card. A deck's index zero is
its top. Transfers append to the destination; shuffling requires an explicit
engine RNG. Draw can recycle a matching owner's discard only when the deck is
empty. Invalid moves validate before mutating or consuming randomness. There is
no hidden RNG or card effect execution inside collections.

Setup accounts for all **189 physical cards** in the supported base card pools:
40 starters, 67 Imperium, 40 Intrigue, 24 Reserve and 18 Conflict. In a three-seat
game ten unused starters are outside the active game. Eight unused Conflicts are
always outside it. These zones are hidden from observations. Leaders are separate
components, not deck cards. Personal decks contain ten cards each; nobody has a
hand yet. The Imperium Row contains five visible cards and its deck contains 62.

## Information boundary

Policies receive an explicit detached observation, never GameState, configuration,
resolved definitions, snapshot/history or RNG. Public projection includes leaders,
resources, VP, influence/alliances, units/occupancy/control, visible markets and
public pile counts/cards. It includes private card **counts** for every seat, but
private identities only for the observing seat's hand and acquired Intrigue.
Starting composition may be known; deck order remains hidden even from its owner.

Shared/private deck ordering, unrevealed Conflicts, other players' hands/Intrigue,
outside-game hidden cards, seeds and random checkpoints are excluded by construction.
Visibility metadata alone does not enforce access; the projection explicitly
selects permitted fields. Observations contain copied immutable values and remain
unchanged after later state mutation. Tests exercise nonempty hidden zones.

## Deterministic randomness and snapshots

[ADR 0003](../adr/0003-runtime-initialization.md) records the standard-library
choice. Each episode owns a seeded `System.Random` adapter. Bounded sampling uses
raw `Random.Next()` with rejection of the incomplete remainder; Fisher–Yates
shuffles use that adapter. Checkpoints store seed plus raw draw count, permitting
restoration by replaying raw draws regardless of the previous bounds. Restoration
is linear in consumed draws; it is a debugging/replay primitive, not a measured
fast clone. No static random state, clocks, thread scheduling or runtime hash codes
participate in setup.

The main seed controls player decks, shared decks, Conflict selection/order and
first player. The existing independent leader-selection seed controls random
leader dealing in configured player order. Changing only the leader seed does
not change card pools or first player. One explicit main draw sequence keeps this
small; independently shuffled decks do not mean independently seeded streams.
Both seeds, and the RNG counters, remain privileged.

Identical validated configuration, catalog, setup/rules versions and pinned runtime
produce identical trusted snapshots, including all hidden zone ordering and unused
cards. Canonical JSON and SHA-256 permit exact state comparison. Snapshot metadata
includes schema, runtime, ruleset/config/setup identity and seeds. Snapshots are
detached strings/values and cannot mutate live state. They contain secret data and
must never enter player observations or policy logs.

Replay is scoped to the recorded engine/setup schema, ruleset/content identity and
pinned runtime implementation. **Cross-runtime replay is not promised.** Changes
to draws, pool ordering, IDs or canonicalization require version review. The v2
ruleset fingerprint intentionally differs from SLICE-001 v1. SLICE-003's
`TurnEngine.Replay` now rebuilds through a trusted initial-state factory, rejects
incompatible identities/versions and applies ordered accepted actions, including
suspended decisions. See [turn replay](turn-engine.md#replay). This does not provide
an event store, database, full snapshot restore/import or complete-game execution.

## Verification and performance

Engine tests cover setup values for both seat counts, card conservation, Conflict
tiers/order, ownership/identity, failed content/configuration paths, deterministic
snapshots, concurrent isolation, RNG restoration and nonempty hidden-state
projection. Existing Toy/simulation/Python checks remain regression gates.

Initialization benchmarks use synthetic metadata with authentic setup quantities.
Composition is outside the measured operation; validation and mutable setup remain
inside it. This is infrastructure performance, not a complete-card/game benchmark.
See [baseline results](../benchmarks/slice-002-initialization.md).

## SLICE-004 runtime delta

The current trusted setup schema is `dune-setup-snapshot-v5`. Agent state now
tracks ordinary ownership, acquired/available Swordmasters and available Mentat;
placements record `AgentKind` and Mentat records `ProtectedThroughRound`.
Public phase/decision summaries include Combat/PostWin/Endgame and terminal
reason flags; `CurrentPlayerId` can be null while `DecisionOwnerId` is present.
Played Intrigue faces in the resolving zone are public; their physical IDs and
causal execution/window payloads remain privileged. Turn schemas and result contracts are documented in the
[turn engine](turn-engine.md). Observations remain detached after completion;
private hands/decks, physical card IDs, RNG, role payloads, seeds, history and
replay identity never become policy inputs. Shared immutable definitions do not
authorize sharing mutable episode state.

## SLICE-005 physical identity and Reserve routing

Selected trash binds one existing `CardInstanceId` privately from an owner-visible
Hand, Discard or InPlay zone; the policy action uses zone, definition and copy
index. `CardZone.MoveTo` moves that exact instance. A Reserve definition returns
to its authoritative `reserve:<definition>` shared stack, including an empty
stack; other eligible definitions move to shared trash. There is no second stock
counter or new runtime zone. `EffectContext.SourceOwnerId` is trusted turn
execution state, separate from selected target owner and current recipient. It
is absent from `ObservationProjection`. Setup snapshot stays v5; turn snapshot
v9 includes the new suspended binding. Conservation, owner-private observation,
and seeded refill remain runtime invariants. Six authentic configuration gates
remain closed by incomplete content.
