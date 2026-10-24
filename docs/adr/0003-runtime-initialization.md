# ADR 0003: typed runtime setup, visibility and standard-library randomness

- Date: 2026-10-24
- Status: Accepted for SLICE-002

## Context

SLICE-001 has immutable composition metadata, representative content, seeded config
and separate leader policy seeds. Setup needs physical card identity, isolated
mutable state, hidden information and reproducibility. Verified full card data and
reuse permission for community catalogs remain unavailable. Generic effects,
plugins, event sourcing and allocator infrastructure would add scope without a
consumer or performance evidence.

## Decision

Lower only the supported unchanged original base composition into immutable typed
setup data. Keep broader composition recognition distinct from executable setup
and gameplay. Bind resolved definitions to canonical configuration identity.
Require complete sourced inventories at the public initializer; an internal-only
synthetic fixture path exists in friend tests/benchmarks, visibly labeled synthetic.

Keep mutable state instance-local, with internal mutations and read-only views.
Share immutable content/setup definitions. Use existing physical CardInstance IDs
and explicit owner/visibility zones. Use typed module-state extensions for future
additional resources, units and tracks; no untyped state dictionary. Explicit
observation DTO projection and separate trusted snapshots are mandatory boundaries.

Use .NET standard collections, immutable values, JSON and SHA-256; no dependency is
needed. Retain seeded `System.Random`, whose documented determinism does not extend
across runtime versions. The small adapter counts raw `Next()` draws and implements
unbiased bounded sampling and Fisher–Yates. It enables a reconstructible checkpoint
without custom PRNG algorithms or an external random library. Two instance-local
streams use existing main/leader seed inputs. This is a conservative compatibility
choice, not a claim that it outperforms other generators.

## Alternatives and consequences

A custom fixed-algorithm PRNG could promise cross-runtime streams, but would need
algorithm/version review and additional correctness evidence. No measured workload
justifies that change now. A third-party RNG package adds licensing/dependency
surface without solving current pinned-runtime replay needs. Random.Shared or
clock seeds violate episode isolation and are excluded.

A public fixture bypass could accidentally initialize invented content as authentic;
friend-only entry points and a synthetic provenance marker keep that distinction
explicit. Verification/source flags remain honest authoring claims, not cryptographic
proof of copyrighted data correctness. Full inventory audit and leader setup-effect
review are required before promoting real content.

Seed plus raw count restore is linear, unsuitable as an assumed fast search clone.
Benchmark actual cloning before choosing another representation. Snapshots allocate
and contain secrets; agents must receive detached projections instead. Future
expansion adapters must version typed setup and their snapshot/privacy contracts.

## Sources

- Local `DUNE_IMPERIUM_Rules_2020_10_26.pdf`, pp.3–8,17–18.
- Local `DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf`, p.2.
- [System.Random documentation](https://learn.microsoft.com/dotnet/fundamentals/runtime-libraries/system-random)
  explains seed reproducibility and version limitations; consulted through Microsoft Learn.
- [ADR 0002](0002-ruleset-composition.md) retains the immutable composition contract.
