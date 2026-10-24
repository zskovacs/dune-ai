# SLICE-003 completion report

Status: **development preview; authentic first-round execution remains partial**.
The engine implements the supported Player Turns lifecycle and is verified through
the Combat boundary with test-only synthetic effects. The CLI runs a partial
sequence of sourced effects and stops explicitly at missing content. No complete
original Imperium configuration or RL training environment is advertised.

## Components and architectural decisions

- Existing setup, zones, physical identities and seeded RNG are reused. The new
  `Turns` facade adds Round Start, scheduling, legal action validation/execution,
  explicit pending effects/decisions and a Combat boundary.
- Immutable typed effects replace handwritten per-card handlers. Visible
  content/copy selectors avoid leaking sorted physical-ID ranges. Active player
  and decision owner are independent. Costs are paid before placement rewards.
- Effect order, optional payments and deployment remain player choices. Reveal
  permits repeated purchases before/between/after Reveal effects. Immediate Acquire
  effects form a nested resolution boundary. No script DSL, reflection dispatcher,
  event-sourcing framework, new dependency or duplicate Toy engine was introduced.
- Unsupported graphs and stock dependencies are preflighted before mutation.
  Illegal actions, unsupported mechanics, incomplete definitions and invariant
  corruption have distinct failure paths. Invariants do not become success/no-op.
- Board capacity and typed unit strength contributions are narrow extension seams;
  no expansion mechanic is implemented.

See [architecture](../architecture/turn-engine.md) and [ADR 0004](../adr/0004-incremental-turn-execution.md).

## Exact support and actions

The [mechanics matrix](../implemented-mechanics.md) names all 14 enabled board
spaces and all card boxes, including gated ones. Sourced complete examples include
Arrakis Liaison, The Spice Must Flow, Fremen Camp and Sardaukar Legion. Dune has
verified Spice Trade/blank Agent behavior; Signet Ring has verified access and
Reveal1 Persuasion but Agent use requires its leader contract. Other obscured
starter boxes stay gated rather than inferred. All real leaders remain unsupported.

Actions: Start Round, place Agent with visible card/space/payment option, select
next effect, choose optional payment/branch, choose deployment count, Reveal,
acquire visible card and end Reveal. No single action enumerates a whole turn's
combinations. See [source ledger](../rules-sources.md) for edition/page/test links.

## Example CLI trace

Run `mise run preview:turns -- --seed 42 --players 4`.

The scenario labels its synthetic shared inventory, Conflict identities and
leaders before execution. It reveals a synthetic Conflict, draws five per player,
then demonstrates the following accepted partial sequence:

```text
Round Start -> active P2
P2: Dune -> Hagga Basin (pay water1; gain spice2), choose deployment0
P3: Dune -> Imperial Basin (gain spice1), choose deployment0
P0: Dune -> Secure Contract (gain Solari3)
P1: unsupported remaining card boxes; stop without mutation
Replay: same accepted actions and final trusted hash
```

Commutative gains resolve automatically in this trace; meaningful effect-order
choices remain explicit in other paths. The default trace has six accepted actions
including Round Start. It does not reach Combat because
P1 cannot legally execute the unverified card boxes. This is the partial scenario
permitted by the requested completion criteria, not an invented game variant.
The complete-boundary test fixture uses distinct synthetic cards/effects and
never assigns invented behavior to real starters.

## Determinism and verification

Replay binds runtime/engine, ruleset/content/support identity, configuration
identity, both seeds and the initial trusted hash. Accepted actions reconstruct
pending work, nested owners and final hash. Rejected actions never enter history.
Observations/legal queries cannot advance RNG or expose hidden decks, opponents'
private cards, raw physical IDs or trusted history. Instances own mutable state.
Determinism is scoped to the pinned runtime and identical definitions/actions.

Verification after the final review fixes:

- `mise run check`: 173 engine tests, 22 simulation tests and 13 Python tests;
  Release build with zero warnings/errors, locked toolchain/dependencies, Ruff
  lint and C#/Python formatting all passed.
- `mise run smoke`: existing toy episode and its replay passed.
- `mise run preview:turns`: seed 42 / four seats accepted six actions, stopped at
  the expected unsupported card boundary and verified the final replay hash.
  A second preview at seed 7 / three seats accepted twelve actions and replayed.
- `mise run benchmark -- --job Dry --filter '*TurnEngineBenchmarks*'`: all four
  harness cases executed. Measured ShortRun means were 36.17 us legal enumeration,
  24.06 us Round Start, 23.00 us placement and 19.02 us optional effect resolution.
- Final whole-branch reviewer: **APPROVE**, no remaining Critical/Important/Minor
  findings; final diff whitespace check passed. No changes were committed or pushed.

Benchmark hardware, allocations, GC, confidence intervals and warnings are in the
[baseline report](../benchmarks/slice-003.md). These short synthetic workloads
establish a baseline and do not demonstrate million-game throughput.

## Known limitations and SLICE-004

The full verified shared inventory and complete starter card faces are unavailable
in the supplied catalog/rulebook illustrations. Real passive/Signet/setup leader
contracts and influence4 alliance/track rewards remain gated. The engine currently
executes the first round only. Conservative stock preflight may exclude a complex
optional branch that a future more precise resolver can prove safe; it never
spends or partially resolves that branch to find out.

SLICE-004 should close the source/leader/first-round support gates, then implement
Combat Intrigue priority, strength changes, ties/rewards, troop returns, Makers,
Recall/First Player/round reset and termination/ranking. Extend source-ledger,
privacy and replay coverage across these boundaries before authentic self-play.
The remaining base cards/spaces and higher faction thresholds need full supported
contracts; expansions, ML encoders and RL algorithms remain later work.
