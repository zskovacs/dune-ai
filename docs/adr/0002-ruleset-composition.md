# ADR 0002: Explicit setup-time ruleset composition

- Date: 2026-10-24
- Status: Accepted for SLICE-001

## Context

Dune AI must distinguish two base games, expansions, optional modules, modes and
formats. Compatibility includes replacements and conditional setup changes, not
just unions of components. The supplied local rulebooks are the primary evidence;
the [matrix](../compatibility-matrix.md) records applicability and page references.
The first full game target is original Imperium + Ix + Immortality, competitive
3–4 players. No complete Dune gameplay exists yet.

## Decision

Use immutable configuration/content/module records in the existing Engine library.
Compose once at setup through an explicit dependency graph; additions require vacant
slots and replacements name the expected previous definition. Conflicting writers
fail. Conditional interactions are separate sourced modules. Retain provenance and
hash canonical resolved metadata with SHA-256, excluding episode and selection seeds.
Keep recognition, validated composition, catalog completeness and gameplay readiness
separate. All Dune configurations remain nonplayable in this slice.

Use .NET System.Text.Json, immutable collections and cryptographic hashing. These
established standard libraries cover this bounded requirement without dependencies.
Reviewed official [JSON guidance](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/immutability)
supports immutable records; no custom serializer or generic plugin framework is
needed. JSON reflection at setup is acceptable; dynamic discovery is absent from
game execution. No benchmark evidence justifies specialized hashing, serialization,
PRNGs or collection infrastructure.

## Alternatives

A single boolean configuration permits contradictory options and loses setup
policy structure. Product union plus last-writer-wins hides rule conflicts.
Reflection-discovered plugins and runtime DI add unnecessary discovery and execution
complexity. A new content assembly currently has no independent dependency or
consumer boundary. A third-party full catalog import lacks demonstrated reuse
permission and verified compatibility (see [assessment](../content/sources.md)).

## Consequences

New combinations need explicit evidence, conditional modules and tests before
composition scope expands. Sample inventories cannot support real episodes.
The metadata recipe values must be lowered into typed executable setup/effects in
later slices, with coverage gates; they must never be interpreted as arbitrary
scripts. Stable IDs and fingerprint versions become replay/experiment contracts.
Resolved objects can be shared while all mutable episode state remains private.
