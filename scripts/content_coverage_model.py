"""Typed, offline content coverage audit data and integrity checks."""

from __future__ import annotations

import hashlib
import json
from dataclasses import MISSING, dataclass, fields
from enum import StrEnum
from pathlib import Path
from typing import Any, get_args, get_origin, get_type_hints


class SourceKind(StrEnum):
    OFFICIAL_LOCAL_PDF = "official-local-pdf"
    OFFICIAL_PDF_WEB = "official-pdf-web"
    OFFICIAL_WEB = "official-web"
    COMMUNITY_WEB = "community-web"
    COMMUNITY_REPOSITORY = "community-repository"


class Product(StrEnum):
    IMPERIUM = "Imperium"
    UPRISING = "Uprising"
    IX = "RiseOfIx"
    IMMORTALITY = "Immortality"
    BLOODLINES = "Bloodlines"


class Module(StrEnum):
    UPRISING_CHOAM = "UprisingChoam"
    BLOODLINES_TECH = "BloodlinesTech"


class Mode(StrEnum):
    STANDARD = "Standard"
    EPIC = "Epic"


class Category(StrEnum):
    IMPERIUM_CARD = "ImperiumCard"
    INTRIGUE_CARD = "IntrigueCard"
    CONFLICT_CARD = "ConflictCard"
    RESERVE_CARD = "ReserveCard"
    STARTING_CARD = "StartingCard"
    LEADER = "Leader"
    BOARD_SPACE = "BoardSpace"
    BOARD_LAYOUT = "BoardLayout"
    TECH_TILE = "TechTile"
    TLEILAXU_CARD = "TleilaxuCard"
    CONTRACT = "Contract"
    OBJECTIVE = "Objective"
    RIVAL = "Rival"
    HOUSE_HAGAL_CARD = "HouseHagalCard"
    SARDAUKAR_COMMANDER_SKILL = "SardaukarCommanderSkill"
    GAMEPLAY_COMPONENT = "GameplayComponent"  # Audit-only; never a runtime definition.


class QuantityBasis(StrEnum):
    NAMED_LOCATIONS = "named-locations"
    PER_PLAYER_COPIES = "per-player-copies"
    PER_PLAYER_TOKENS = "per-player-tokens"
    PER_PLAYER_PIECES = "per-player-pieces"
    PHYSICAL_CARDS = "physical-cards"
    PHYSICAL_COPIES = "physical-copies"
    PHYSICAL_PIECE = "physical-piece"
    PHYSICAL_TOKENS = "physical-tokens"
    SELECTED_GAME_CARDS = "selected-game-cards"
    TWO_COMMANDER_DECKS = "two-commander-decks"
    USABLE_UNITS = "usable-units"


class ExpectationScope(StrEnum):
    BOXED = "boxed"
    BOXED_EPIC_VARIANT = "boxed-epic-variant"
    BOXED_IDENTITY_SUBSET = "boxed-identity-subset"
    BOXED_STARTER_REPLACEMENT = "boxed-starter-replacement"
    BOXED_TIER_SUBSET = "boxed-tier-subset"
    BOXED_TIER_SUBSET_EXCLUDED = "boxed-tier-subset-excluded-with-original"
    EXCLUDED_EPIC_VARIANT = "excluded-epic-variant"
    EXCLUDED_SIX_PLAYER_VARIANT = "excluded-six-player-variant"
    EXCLUDED_SOLO_ONLY = "excluded-solo-only"
    EXCLUDED_SOLO_TWO_PLAYER = "excluded-solo-two-player"
    EXCLUDED_UNLESS_CHOAM_AND_IX = "excluded-unless-choam-and-ix"
    EXCLUDED_UNLESS_CHOAM_MODULE = "excluded-unless-choam-module"
    EXCLUDED_UNLESS_TECH_MODULE = "excluded-unless-tech-module"
    LEADER_SPECIFIC_BARON = "leader-specific-baron"
    LEADER_SPECIFIC_PITER = "leader-specific-piter"
    LEADER_SPECIFIC_STEERSMAN = "leader-specific-steersman"
    ORDINARY_PLAYER_COMPONENTS = "ordinary-player-components"
    ORDINARY_PLAYER_RECIPE = "ordinary-player-recipe"
    ORIGINAL_BOARD = "original-board"
    PER_PLAYER_REPLACEMENT = "per-player-replacement"
    STANDARD_SETUP = "standard-setup"
    STANDARD_SETUP_LIMITED_POOL = "standard-setup-limited-pool"
    STANDARD_SETUP_SELECTION = "standard-setup-selection"


class Discovery(StrEnum):
    NOT_DISCOVERED = "not-discovered"
    DISCOVERED = "discovered"


class Sourcing(StrEnum):
    UNSOURCED = "unsourced"
    PARTIALLY_SOURCED = "partially-sourced"
    FULLY_SOURCED = "fully-sourced"


class Verification(StrEnum):
    UNVERIFIED = "unverified"
    VERIFIED = "verified"
    DISPUTED = "disputed"


class Classification(StrEnum):
    PRIMITIVE = "primitive"
    COMPOSITE = "composite"
    SPECIAL = "special"
    UNRESOLVED = "unresolved"


class Implementation(StrEnum):
    NOT_IMPLEMENTED = "not-implemented"
    PARTIALLY_IMPLEMENTED = "partially-implemented"
    IMPLEMENTED = "implemented"
    UNSUPPORTED = "unsupported"


class Validation(StrEnum):
    NOT_TESTED = "not-tested"
    TESTED = "tested"


class Slot(StrEnum):
    SETUP = "Setup"
    PASSIVE = "Passive"
    AGENT = "Agent"
    REVEAL = "Reveal"
    ACQUIRE = "Acquire"
    SIGNET_RING = "SignetRing"
    INTRIGUE = "Intrigue"
    COMBAT = "Combat"
    POST_WIN = "PostWin"
    ENDGAME = "Endgame"
    TRIGGERED = "Triggered"
    PERSISTENT_MODIFIER = "PersistentModifier"


class Timing(StrEnum):
    SETUP = "setup"
    PASSIVE = "passive"
    ON_PLACEMENT = "on-placement"
    ON_REVEAL = "on-reveal"
    ON_ACQUIRE = "on-acquire"
    ON_SIGNET_RING = "on-signet-ring"
    INTRIGUE = "intrigue"
    COMBAT = "combat"
    POST_WIN = "post-win"
    ENDGAME = "endgame"
    TRIGGERED = "triggered"
    PERSISTENT = "persistent"


class DescriptorKind(StrEnum):
    PRECONDITION = "precondition"
    COST = "cost"
    CHOICE = "choice"
    ROLE = "role"
    USAGE = "usage"
    EFFECT = "effect"


class SlotDiscovery(StrEnum):
    UNDISCOVERED = "undiscovered"
    UNREADABLE = "unreadable"
    DISCOVERED = "discovered"
    VERIFIED_ABSENT = "verified-absent"


class EvidenceKind(StrEnum):
    IDENTITY = "identity"
    QUANTITY = "quantity"
    APPLICABILITY = "applicability"
    SEMANTICS = "semantics"
    RULING = "ruling"
    BEHAVIOR = "behavior"
    INTEGRATION = "integration"
    PRIVACY_REPLAY = "privacy-replay"
    INVENTORY = "inventory"
    COMPOSITION = "composition"
    INITIALIZATION = "initialization"
    FULL_GAME = "full-game"
    RL = "rl"


class Authenticity(StrEnum):
    OFFICIAL = "official"
    SYNTHETIC = "synthetic"


class RegistryKind(StrEnum):
    CATALOG = "Catalog"
    CARD_RULE = "CardRule"
    BOARD_RULE = "BoardRule"
    INTRIGUE_RULE = "IntrigueRule"
    LEADER_RULE = "LeaderRule"
    CONFLICT_RULE = "ConflictRule"


@dataclass(frozen=True)
class Descriptor:
    kind: DescriptorKind
    value: str


@dataclass(frozen=True)
class EvidenceClaim:
    sourceId: str
    page: int | None
    locator: str
    kind: EvidenceKind | None = None
    verification: Verification | None = None


@dataclass(frozen=True)
class SourceRecord:
    id: str
    kind: SourceKind
    title: str
    locator: str
    revision: str
    sha256: str | None
    pages: int | None
    products: tuple[Product, ...]
    claimTypes: tuple[str, ...]
    completeness: str
    verification: str
    rights: str
    notes: str
    discrepancies: tuple[str, ...]
    localPath: str | None = None
    reusePermission: bool = False


@dataclass(frozen=True)
class InventoryExpectation:
    id: str
    product: Product
    edition: str
    category: Category
    tier: str | None
    expectedCopies: int
    uniqueDefinitions: int | None
    unit: str
    basis: QuantityBasis
    scope: ExpectationScope
    evidence: tuple[EvidenceClaim, ...]
    note: str | None


@dataclass(frozen=True)
class SlotRecord:
    slot: Slot
    discovery: SlotDiscovery
    reason: str | None = None
    evidence: tuple[EvidenceClaim, ...] = ()
    abilityIds: tuple[str, ...] = ()


@dataclass(frozen=True)
class RegistryReference:
    registryKind: RegistryKind
    key: str
    path: str
    member: str
    slot: Slot | None = None
    authored: bool = True


@dataclass(frozen=True)
class AvailabilityRecord:
    baseGames: tuple[Product, ...]
    requiredExpansions: tuple[Product, ...]
    excludedExpansions: tuple[Product, ...]
    requiredModules: tuple[Module, ...]
    excludedModules: tuple[Module, ...]
    modes: tuple[Mode, ...]
    minimumSeats: int
    maximumSeats: int


@dataclass(frozen=True)
class Blocker:
    id: str
    open: bool
    blocking: bool
    scope: str
    reason: str


@dataclass(frozen=True)
class TestReference:
    path: str
    name: str
    kind: EvidenceKind
    historical: bool = False


@dataclass(frozen=True)
class ContentRecord:
    id: str
    name: str
    product: Product
    edition: str
    category: Category
    version: str
    variantOf: str | None
    authenticity: Authenticity
    boxedQuantity: int | None
    perPlayerQuantity: int | None
    expectationIds: tuple[str, ...]
    evidence: tuple[EvidenceClaim, ...]
    discovery: Discovery
    sourcing: Sourcing
    verification: Verification
    faceComplete: bool
    slots: tuple[SlotRecord, ...]
    abilities: tuple[AbilityRecord, ...]
    availability: AvailabilityRecord
    faceEvidence: tuple[EvidenceClaim, ...] = ()
    runtimeDefinition: bool = False
    replaces: tuple[str, ...] = ()
    excludes: tuple[str, ...] = ()
    implementationReferences: tuple[RegistryReference, ...] = ()


@dataclass(frozen=True)
class AbilityRecord:
    id: str
    contentId: str
    slot: Slot
    timing: Timing
    classification: Classification
    semanticClaim: str | None
    descriptors: tuple[Descriptor, ...]
    evidence: tuple[EvidenceClaim, ...]
    capabilityIds: tuple[str, ...]
    interactionIds: tuple[str, ...]
    implementationRefs: tuple[str, ...]
    testRefs: tuple[TestReference, ...]
    discovery: Discovery
    sourcing: Sourcing
    verification: Verification
    implementation: Implementation
    validation: Validation
    blockers: tuple[Blocker, ...]
    exclusionReason: str | None
    batchId: str | None
    classificationReason: str | None = None


@dataclass(frozen=True)
class CapabilityRecord:
    id: str
    contract: str
    supportedSubset: str | None
    memberRefs: tuple[str, ...]
    testRefs: tuple[TestReference, ...]
    missingDelta: str | None
    prerequisiteIds: tuple[str, ...]
    implementation: Implementation
    validation: Validation
    blockers: tuple[Blocker, ...]


@dataclass(frozen=True)
class InteractionRecord:
    id: str
    participantIds: tuple[str, ...]
    rulesets: tuple[str, ...]
    evidence: tuple[EvidenceClaim, ...]
    exceptions: tuple[str, ...]
    requiredHook: str | None
    implementationRefs: tuple[str, ...]
    testRefs: tuple[TestReference, ...]
    implementation: Implementation
    validation: Validation
    blockers: tuple[Blocker, ...]


@dataclass(frozen=True)
class BatchRecord:
    id: str
    memberIds: tuple[str, ...]
    prerequisiteIds: tuple[str, ...]
    sourceGates: tuple[str, ...]
    mechanics: tuple[str, ...]
    expectedDelta: str
    acceptance: str
    risks: tuple[str, ...]


@dataclass(frozen=True)
class ConfigurationRecord:
    id: str
    base: Product
    expansions: tuple[Product, ...]
    modules: tuple[Module, ...]
    mode: Mode
    seats: int
    leaderPolicy: str
    authenticity: Authenticity
    contentIds: tuple[str, ...]
    interactionIds: tuple[str, ...]
    evidence: tuple[EvidenceClaim, ...]
    initializationTests: tuple[TestReference, ...]
    fullGameTests: tuple[TestReference, ...]
    rlTests: tuple[TestReference, ...]


@dataclass(frozen=True)
class AuditInventory:
    schemaVersion: str
    auditRevision: str
    repositoryRevision: str
    sources: tuple[SourceRecord, ...]
    expectations: tuple[InventoryExpectation, ...]
    content: tuple[ContentRecord, ...]
    capabilities: tuple[CapabilityRecord, ...] = ()
    interactions: tuple[InteractionRecord, ...] = ()
    batches: tuple[BatchRecord, ...] = ()
    configurations: tuple[ConfigurationRecord, ...] = ()


@dataclass(frozen=True)
class AuditIssue:
    severity: str
    code: str
    recordId: str
    path: str
    message: str


@dataclass(frozen=True)
class QuantityResult:
    expectationId: str
    expectedCopies: int
    verifiedCopies: int
    sourcedUnverifiedCopies: int
    unknownCopyRecords: int
    missingResidualCopies: int | None
    overcountCopies: int
    uniqueDefinitions: int | None
    discoveredDefinitions: int
    complete: bool


@dataclass(frozen=True)
class ConfigurationGate:
    configurationId: str
    recognized: bool
    composition: bool
    inventory: bool
    abilities: bool
    interactions: bool
    initialization: bool
    fullGame: bool
    rl: bool
    reasons: tuple[str, ...]


@dataclass(frozen=True)
class AuditReport:
    sourceCount: int
    expectationCount: int
    contentCount: int
    abilityCount: int
    unknownSlots: int
    quantities: tuple[QuantityResult, ...]
    configurationGates: tuple[ConfigurationGate, ...]
    capabilityDependents: tuple[tuple[str, tuple[str, ...]], ...]
    gaps: tuple[str, ...]


def _decode(value: Any, typ: Any, path: str) -> Any:
    origin = get_origin(typ)
    args = get_args(typ)
    if origin in (tuple,):
        if not isinstance(value, list):
            raise ValueError(f"{path}: expected array")
        return tuple(
            _decode(item, args[0], f"{path}[{i}]") for i, item in enumerate(value)
        )
    if origin in (type(None),):
        if value is not None:
            raise ValueError(f"{path}: expected null")
        return None
    if origin is not None and type(None) in args:
        if value is None:
            return None
        return _decode(value, next(arg for arg in args if arg is not type(None)), path)
    if isinstance(typ, type) and issubclass(typ, StrEnum):
        if not isinstance(value, str):
            raise ValueError(f"{path}: expected {typ.__name__} string")
        try:
            return typ(value)
        except ValueError as error:
            raise ValueError(f"{path}: unknown {typ.__name__} {value!r}") from error
    if typ is int:
        if type(value) is not int:
            raise ValueError(f"{path}: expected integer")
        if value < 0:
            raise ValueError(f"{path}: expected nonnegative integer")
        return value
    if typ is bool:
        if type(value) is not bool:
            raise ValueError(f"{path}: expected boolean")
        return value
    if typ is str:
        if not isinstance(value, str):
            raise ValueError(f"{path}: expected string")
        if not value.strip():
            raise ValueError(f"{path}: expected nonempty string")
        return value
    if isinstance(typ, type) and hasattr(typ, "__dataclass_fields__"):
        if not isinstance(value, dict):
            raise ValueError(f"{path}: expected object")
        hints = get_type_hints(typ)
        names = {f.name for f in fields(typ)}
        extra = set(value) - names
        if extra:
            raise ValueError(f"{path}: unknown fields {sorted(extra)}")
        required = {
            f.name for f in fields(typ) if f.default is f.default_factory is MISSING
        }
        missing = required - set(value)
        if missing:
            raise ValueError(f"{path}: missing fields {sorted(missing)}")
        return typ(
            **{
                name: _decode(item, hints[name], f"{path}.{name}")
                for name, item in value.items()
            }
        )
    raise TypeError(f"unsupported schema type {typ!r}")


def load_inventory(path: Path) -> AuditInventory:
    try:
        return _decode(
            json.loads(path.read_text(encoding="utf-8")), AuditInventory, "$"
        )
    except json.JSONDecodeError as error:
        raise ValueError(f"{path}: invalid JSON: {error}") from error


def _is_safe_path(path: str) -> bool:
    candidate = Path(path)
    return (
        not candidate.is_absolute()
        and ".." not in candidate.parts
        and path == candidate.as_posix()
    )


def _has_test(refs: tuple[TestReference, ...], kinds: set[EvidenceKind]) -> bool:
    return any(ref.kind in kinds and not ref.historical for ref in refs)


def _has_open_blocker(blockers: tuple[Blocker, ...]) -> bool:
    return any(blocker.open and blocker.blocking for blocker in blockers)


def _quantity_for(
    content: ContentRecord, expectation: InventoryExpectation
) -> int | None:
    if expectation.basis.startswith("per-player") or expectation.scope in {
        "ordinary-player-recipe",
        "per-player-replacement",
        "excluded-epic-variant",
    }:
        return content.perPlayerQuantity
    return content.boxedQuantity


def _expectation_applies(
    expectation: InventoryExpectation, config: ConfigurationRecord
) -> bool:
    if expectation.product not in (config.base, *config.expansions):
        return False
    scope = expectation.scope
    if scope in {
        ExpectationScope.EXCLUDED_SOLO_ONLY,
        ExpectationScope.EXCLUDED_SOLO_TWO_PLAYER,
        ExpectationScope.EXCLUDED_SIX_PLAYER_VARIANT,
    }:
        return False
    if scope in {
        ExpectationScope.BOXED_EPIC_VARIANT,
        ExpectationScope.EXCLUDED_EPIC_VARIANT,
    }:
        return config.mode == Mode.EPIC
    if scope in {
        ExpectationScope.EXCLUDED_UNLESS_CHOAM_MODULE,
        ExpectationScope.EXCLUDED_UNLESS_CHOAM_AND_IX,
    }:
        return Module.UPRISING_CHOAM in config.modules and (
            scope != ExpectationScope.EXCLUDED_UNLESS_CHOAM_AND_IX
            or Product.IX in config.expansions
        )
    if scope == ExpectationScope.EXCLUDED_UNLESS_TECH_MODULE:
        return Module.BLOODLINES_TECH in config.modules
    if scope == ExpectationScope.BOXED_TIER_SUBSET_EXCLUDED:
        return config.base == Product.UPRISING
    if scope.value.startswith("leader-specific-") and config.leaderPolicy.startswith(
        "fixed:"
    ):
        return (
            scope.value.removeprefix("leader-specific-")
            in config.leaderPolicy.casefold()
        )
    return True


def _content_applies(content: ContentRecord, config: ConfigurationRecord) -> bool:
    availability = content.availability
    return (
        (not availability.baseGames or config.base in availability.baseGames)
        and all(exp in config.expansions for exp in availability.requiredExpansions)
        and not any(exp in config.expansions for exp in availability.excludedExpansions)
        and all(module in config.modules for module in availability.requiredModules)
        and not any(module in config.modules for module in availability.excludedModules)
        and (not availability.modes or config.mode in availability.modes)
        and availability.minimumSeats <= config.seats <= availability.maximumSeats
    )


def _configuration_dependencies(
    config: ConfigurationRecord,
) -> tuple[tuple[str, str, str], ...]:
    failures: list[tuple[str, str, str]] = []
    if Module.BLOODLINES_TECH in config.modules:
        if Product.BLOODLINES not in config.expansions:
            failures.append(
                ("module-dependency", "modules", "BloodlinesTech requires Bloodlines")
            )
        if config.base != Product.UPRISING:
            failures.append(
                ("module-incompatible", "modules", "BloodlinesTech requires Uprising")
            )
    if Module.UPRISING_CHOAM in config.modules and config.base != Product.UPRISING:
        failures.append(
            ("module-incompatible", "modules", "UprisingChoam requires Uprising")
        )
    if config.mode == Mode.EPIC:
        if Product.IX not in config.expansions:
            failures.append(("mode-dependency", "mode", "Epic requires RiseOfIx"))
        if config.base != Product.IMPERIUM:
            failures.append(
                ("mode-incompatible", "mode", "Epic is not composed with Uprising")
            )
    return tuple(failures)


_CARD_SLOTS = frozenset(
    {
        Slot.AGENT,
        Slot.REVEAL,
        Slot.ACQUIRE,
    }
)
_INTRIGUE_SLOTS = frozenset(
    {
        Slot.INTRIGUE,
        Slot.COMBAT,
        Slot.POST_WIN,
        Slot.ENDGAME,
    }
)
_CONFLICT_SLOTS = frozenset(
    {
        Slot.POST_WIN,
        Slot.TRIGGERED,
        Slot.PERSISTENT_MODIFIER,
    }
)
_LEADER_SLOTS = frozenset(
    {
        Slot.PASSIVE,
        Slot.SIGNET_RING,
    }
)
_BOARD_SLOTS = frozenset(
    {
        Slot.AGENT,
    }
)
_FACE_SLOTS: dict[Category, frozenset[Slot]] = {
    Category.IMPERIUM_CARD: _CARD_SLOTS,
    Category.STARTING_CARD: _CARD_SLOTS,
    Category.RESERVE_CARD: _CARD_SLOTS,
    Category.TLEILAXU_CARD: _CARD_SLOTS,
    Category.INTRIGUE_CARD: _INTRIGUE_SLOTS,
    Category.CONFLICT_CARD: _CONFLICT_SLOTS,
    Category.LEADER: _LEADER_SLOTS,
    Category.BOARD_SPACE: _BOARD_SLOTS,
    Category.BOARD_LAYOUT: frozenset(),
    Category.TECH_TILE: _CARD_SLOTS,
    Category.CONTRACT: _CARD_SLOTS,
    Category.OBJECTIVE: _CARD_SLOTS,
    Category.RIVAL: _BOARD_SLOTS,
    Category.HOUSE_HAGAL_CARD: _CARD_SLOTS,
    Category.SARDAUKAR_COMMANDER_SKILL: _CARD_SLOTS,
    Category.GAMEPLAY_COMPONENT: frozenset(),
}


def _official_claim(
    claims: tuple[EvidenceClaim, ...],
    kind: EvidenceKind,
    sources: dict[str, SourceRecord],
) -> bool:
    return any(
        claim.kind == kind
        and claim.verification == Verification.VERIFIED
        and claim.sourceId in sources
        and sources[claim.sourceId].kind
        in {
            SourceKind.OFFICIAL_LOCAL_PDF,
            SourceKind.OFFICIAL_PDF_WEB,
            SourceKind.OFFICIAL_WEB,
        }
        for claim in claims
    )


def _complete_face(item: ContentRecord, sources: dict[str, SourceRecord]) -> bool:
    slots = {slot.slot: slot for slot in item.slots}
    return (
        item.faceComplete
        and item.discovery == Discovery.DISCOVERED
        and item.category not in {Category.GAMEPLAY_COMPONENT, Category.BOARD_LAYOUT}
        and _official_claim(item.faceEvidence, EvidenceKind.SEMANTICS, sources)
        and _FACE_SLOTS[item.category] <= slots.keys()
        and all(
            (
                slot.discovery == SlotDiscovery.VERIFIED_ABSENT
                and _official_claim(slot.evidence, EvidenceKind.SEMANTICS, sources)
            )
            or (slot.discovery == SlotDiscovery.DISCOVERED and bool(slot.abilityIds))
            for slot in slots.values()
        )
    )


def _cycles(graph: dict[str, tuple[str, ...]]) -> set[str]:
    active: set[str] = set()
    done: set[str] = set()
    cyclic: set[str] = set()

    def visit(node: str) -> None:
        if node in active:
            cyclic.add(node)
            return
        if node in done:
            return
        active.add(node)
        for dependency in graph.get(node, ()):
            visit(dependency)
        active.remove(node)
        done.add(node)

    for node in graph:
        visit(node)
    return cyclic


def _capability_supported(
    capability_id: str,
    capabilities: dict[str, CapabilityRecord],
    active: frozenset[str] = frozenset(),
) -> bool:
    capability = capabilities.get(capability_id)
    if capability is None or capability_id in active:
        return False
    return (
        capability.implementation == Implementation.IMPLEMENTED
        and capability.validation == Validation.TESTED
        and bool(capability.memberRefs)
        and _has_test(
            capability.testRefs,
            {EvidenceKind.BEHAVIOR, EvidenceKind.INTEGRATION},
        )
        and not _has_open_blocker(capability.blockers)
        and all(
            _capability_supported(ref, capabilities, active | {capability_id})
            for ref in capability.prerequisiteIds
        )
    )


def validate_inventory(inventory: AuditInventory, root: Path) -> tuple[AuditIssue, ...]:
    issues: list[AuditIssue] = []

    def add(
        code: str, record: str, path: str, message: str, severity: str = "error"
    ) -> None:
        issues.append(AuditIssue(severity, code, record, path, message))

    if inventory.schemaVersion != "dune-content-coverage-v1":
        add("schema-version", "$", "schemaVersion", "unsupported schema version")
    all_abilities = tuple(a for c in inventory.content for a in c.abilities)
    groups = {
        "sources": inventory.sources,
        "expectations": inventory.expectations,
        "content": inventory.content,
        "abilities": all_abilities,
        "capabilities": inventory.capabilities,
        "interactions": inventory.interactions,
        "batches": inventory.batches,
        "configurations": inventory.configurations,
    }
    ids: dict[str, set[str]] = {}
    for group, records in groups.items():
        seen: set[str] = set()
        for record in records:
            if record.id in seen:
                add("duplicate-id", record.id, group, "duplicate ID")
            seen.add(record.id)
        ids[group] = seen

    sources = {s.id: s for s in inventory.sources}
    expectations = {e.id: e for e in inventory.expectations}
    content = {c.id: c for c in inventory.content}
    abilities = {a.id: a for a in all_abilities}
    capabilities = {c.id: c for c in inventory.capabilities}
    interactions = {i.id: i for i in inventory.interactions}
    batches = {b.id: b for b in inventory.batches}

    def refs(
        values: tuple[str, ...],
        valid: dict[str, Any] | set[str],
        record: str,
        path: str,
    ) -> None:
        for ref in values:
            if ref not in valid:
                add("broken-reference", record, path, f"unknown reference {ref}")

    def evidence(
        values: tuple[EvidenceClaim, ...],
        record: str,
        path: str,
        product: Product | None = None,
    ) -> None:
        for claim in values:
            source = sources.get(claim.sourceId)
            if source is None:
                add(
                    "broken-reference", record, path, f"unknown source {claim.sourceId}"
                )
                continue
            if product is not None and product not in source.products:
                add(
                    "source-applicability",
                    record,
                    path,
                    "source does not cover product",
                )
            if claim.page is not None:
                if claim.page < 1 or source.pages is None or claim.page > source.pages:
                    add(
                        "source-page",
                        record,
                        path,
                        f"page {claim.page} outside source bounds",
                    )
            elif source.kind == "official-local-pdf":
                add("source-page", record, path, "local PDF claim needs a page")

    for source in inventory.sources:
        if source.kind not in {
            "official-local-pdf",
            "official-pdf-web",
            "official-web",
            "community-web",
            "community-repository",
        }:
            add("source-kind", source.id, "kind", "unknown source kind")
        if source.kind == "official-local-pdf":
            if not source.sha256 or not source.pages:
                add(
                    "source-metadata",
                    source.id,
                    "sha256/pages",
                    "local PDF requires pinned hash and page bound",
                )
            path = source.locator
        else:
            path = source.localPath
        if source.sha256 and (
            len(source.sha256) != 64
            or any(ch not in "0123456789abcdef" for ch in source.sha256)
        ):
            add("source-hash", source.id, "sha256", "invalid SHA-256")
        if path:
            if not _is_safe_path(path):
                add(
                    "source-path",
                    source.id,
                    "locator/localPath",
                    "path escapes repository or is not normalized",
                )
            else:
                file = root / path
                if not file.is_file():
                    add(
                        "evidence-unavailable",
                        source.id,
                        path,
                        "optional local evidence is unavailable; "
                        "historical verification is retained",
                        "warning",
                    )
                elif (
                    source.sha256
                    and hashlib.sha256(file.read_bytes()).hexdigest() != source.sha256
                ):
                    add(
                        "source-hash",
                        source.id,
                        path,
                        "local bytes do not match pinned SHA-256",
                    )
        if source.reusePermission and source.rights != "licensed-for-copied-artifacts":
            add(
                "source-rights",
                source.id,
                "reusePermission",
                "source rights do not permit copied artifacts",
            )

    for exp in inventory.expectations:
        evidence(exp.evidence, exp.id, "evidence", exp.product)
        if not exp.evidence:
            add(
                "missing-evidence",
                exp.id,
                "evidence",
                "expectation requires source evidence",
            )
        if exp.tier is not None and (
            exp.category != Category.CONFLICT_CARD or exp.tier not in {"I", "II", "III"}
        ):
            add("tier", exp.id, "tier", "invalid conflict tier")
        if exp.scope == "boxed" and exp.basis.startswith("per-player"):
            add(
                "quantity-basis",
                exp.id,
                "basis",
                "boxed scope cannot use per-player basis",
            )

    for item in inventory.content:
        evidence(item.evidence, item.id, "evidence", item.product)
        evidence(item.faceEvidence, item.id, "faceEvidence", item.product)
        seen_registry_refs: set[tuple[RegistryKind, str, Slot | None]] = set()
        for ref in item.implementationReferences:
            ref_key = (ref.registryKind, ref.key, ref.slot)
            if ref_key in seen_registry_refs:
                add(
                    "duplicate-registry-reference",
                    item.id,
                    "implementationReferences",
                    "duplicate registry reference",
                )
            seen_registry_refs.add(ref_key)
            if ref.key != item.id:
                add(
                    "registry-key",
                    item.id,
                    "implementationReferences",
                    "registry key differs from content ID",
                )
            if not _is_safe_path(ref.path) or not (root / ref.path).is_file():
                add(
                    "implementation-path",
                    item.id,
                    "implementationReferences",
                    "registry path is missing or unsafe",
                )
        availability = item.availability
        if (
            availability.minimumSeats < 1
            or availability.maximumSeats > 6
            or availability.minimumSeats > availability.maximumSeats
            or any(
                base not in {Product.IMPERIUM, Product.UPRISING}
                for base in availability.baseGames
            )
            or any(
                expansion in {Product.IMPERIUM, Product.UPRISING}
                for expansion in (
                    *availability.requiredExpansions,
                    *availability.excludedExpansions,
                )
            )
            or set(availability.requiredExpansions)
            & set(availability.excludedExpansions)
            or set(availability.requiredModules) & set(availability.excludedModules)
        ):
            add(
                "availability",
                item.id,
                "availability",
                "invalid or contradictory availability constraints",
            )
        refs(item.expectationIds, expectations, item.id, "expectationIds")
        refs(item.replaces + item.excludes, content, item.id, "replaces/excludes")
        if item.variantOf:
            refs((item.variantOf,), content, item.id, "variantOf")
        if item.category == Category.GAMEPLAY_COMPONENT and item.runtimeDefinition:
            add(
                "audit-only",
                item.id,
                "runtimeDefinition",
                "GameplayComponent has no runtime category",
            )
        if item.discovery == Discovery.NOT_DISCOVERED and (
            item.faceComplete or item.abilities
        ):
            add(
                "discovery-conflict",
                item.id,
                "discovery",
                "undiscovered content cannot have a complete face or abilities",
            )
        if item.verification == Verification.VERIFIED and (
            item.sourcing != Sourcing.FULLY_SOURCED or not item.evidence
        ):
            add(
                "verification-conflict",
                item.id,
                "verification",
                "verified content needs full source evidence",
            )
        if (
            item.verification == Verification.VERIFIED
            and item.discovery == Discovery.DISCOVERED
            and not _official_claim(item.evidence, EvidenceKind.IDENTITY, sources)
        ):
            add(
                "identity-evidence",
                item.id,
                "evidence",
                "verified identity needs an official identity claim",
            )
        if item.faceComplete and not _complete_face(item, sources):
            add(
                "false-complete",
                item.id,
                "faceComplete",
                "complete face needs official face evidence and a full "
                "applicable slot account",
            )
        if len({s.slot for s in item.slots}) != len(item.slots):
            add("duplicate-slot", item.id, "slots", "duplicate slot")
        for slot in item.slots:
            refs(
                slot.abilityIds,
                {a.id for a in item.abilities},
                item.id,
                "slots.abilityIds",
            )
            if slot.discovery == SlotDiscovery.VERIFIED_ABSENT and slot.abilityIds:
                add(
                    "absent-slot-ability",
                    item.id,
                    "slots.abilityIds",
                    "verified-absent slot cannot own an ability",
                )
            if slot.discovery == SlotDiscovery.DISCOVERED and not slot.abilityIds:
                add(
                    "slot-ability",
                    item.id,
                    "slots.abilityIds",
                    "discovered slot needs an ability",
                )
            evidence(slot.evidence, item.id, "slots.evidence", item.product)
            if slot.discovery == SlotDiscovery.VERIFIED_ABSENT and not _official_claim(
                slot.evidence, EvidenceKind.SEMANTICS, sources
            ):
                add(
                    "absent-slot-evidence",
                    item.id,
                    "slots",
                    "verified absence needs face evidence",
                )
            if (
                slot.discovery in {SlotDiscovery.UNDISCOVERED, SlotDiscovery.UNREADABLE}
                and not slot.reason
            ):
                add("slot-reason", item.id, "slots", "unknown slot needs reason")
        for ref in item.expectationIds:
            exp = expectations.get(ref)
            if exp and (
                exp.product != item.product
                or exp.category != item.category
                or exp.edition != item.edition
            ):
                add(
                    "applicability",
                    item.id,
                    "expectationIds",
                    f"{ref} has different product/category/edition",
                )
        if (
            (item.boxedQuantity is not None or item.perPlayerQuantity is not None)
            and item.verification == Verification.VERIFIED
            and not _official_claim(item.evidence, EvidenceKind.QUANTITY, sources)
        ):
            add(
                "quantity-evidence",
                item.id,
                "evidence",
                "verified copies need quantity evidence",
            )
        if (
            item.boxedQuantity is not None
            and item.discovery == Discovery.NOT_DISCOVERED
        ):
            add(
                "quantity-discovery",
                item.id,
                "boxedQuantity",
                "known copies require discovered identity",
            )

    versions: set[tuple[Product, str, Category, str, str, str | None]] = set()
    for item in inventory.content:
        key = (
            item.product,
            item.edition,
            item.category,
            item.name,
            item.version,
            item.variantOf,
        )
        if key in versions:
            add("duplicate-version", item.id, "version", "duplicate identity/version")
        versions.add(key)

    for ability in all_abilities:
        parent = content.get(ability.contentId)
        if parent is None or ability not in parent.abilities:
            add(
                "broken-reference",
                ability.id,
                "contentId",
                "ability must belong to its content",
            )
        elif not any(
            ability.id in slot.abilityIds and slot.slot == ability.slot
            for slot in parent.slots
        ):
            add(
                "reverse-reference",
                ability.id,
                "slot",
                "matching content slot must list ability",
            )
        if (
            parent
            and ability.implementation == Implementation.IMPLEMENTED
            and not any(
                ability.id in slot.abilityIds
                and slot.discovery == SlotDiscovery.DISCOVERED
                for slot in parent.slots
            )
        ):
            add(
                "slot-discovery",
                ability.id,
                "slot",
                "implemented ability needs a discovered slot",
            )
        if not ability.id.startswith(f"{ability.contentId}#slot:"):
            add(
                "ability-id", ability.id, "id", "ability ID must use contentId#slot:key"
            )
        refs(ability.capabilityIds, capabilities, ability.id, "capabilityIds")
        refs(ability.interactionIds, interactions, ability.id, "interactionIds")
        if ability.batchId:
            refs((ability.batchId,), batches, ability.id, "batchId")
        evidence(
            ability.evidence, ability.id, "evidence", parent.product if parent else None
        )
        if (
            ability.batchId
            and ability.batchId in batches
            and ability.id not in batches[ability.batchId].memberIds
        ):
            add(
                "batch-membership",
                ability.id,
                "batchId",
                "primary batch must list ability",
            )
        _validate_support(ability, capabilities, interactions, root, add)

    for capability in inventory.capabilities:
        refs(capability.prerequisiteIds, capabilities, capability.id, "prerequisiteIds")
        if capability.implementation == Implementation.IMPLEMENTED and any(
            not _capability_supported(ref, capabilities)
            for ref in capability.prerequisiteIds
        ):
            add(
                "capability-prerequisite",
                capability.id,
                "prerequisiteIds",
                "implemented capability has an unmet prerequisite",
            )
        if (
            capability.implementation == Implementation.UNSUPPORTED
            and not capability.missingDelta
        ):
            add(
                "unsupported-reason",
                capability.id,
                "missingDelta",
                "unsupported capability needs a missing delta",
            )
        _validate_assertions(
            capability.id,
            capability.implementation,
            capability.validation,
            capability.memberRefs,
            capability.testRefs,
            capability.blockers,
            root,
            add,
        )
    for interaction in inventory.interactions:
        refs(
            interaction.participantIds,
            set(content) | set(abilities) | set(capabilities),
            interaction.id,
            "participantIds",
        )
        evidence(interaction.evidence, interaction.id, "evidence")
        if (
            interaction.implementation == Implementation.UNSUPPORTED
            and not interaction.requiredHook
            and not interaction.blockers
        ):
            add(
                "unsupported-reason",
                interaction.id,
                "requiredHook",
                "unsupported interaction needs a missing hook or blocker",
            )
        _validate_assertions(
            interaction.id,
            interaction.implementation,
            interaction.validation,
            interaction.implementationRefs,
            interaction.testRefs,
            interaction.blockers,
            root,
            add,
        )
    for batch in inventory.batches:
        refs(
            batch.memberIds,
            set(content) | set(abilities) | set(capabilities) | set(interactions),
            batch.id,
            "memberIds",
        )
        refs(batch.prerequisiteIds, batches, batch.id, "prerequisiteIds")
        refs(batch.sourceGates, sources, batch.id, "sourceGates")
    for ability in all_abilities:
        owners = [
            batch.id for batch in inventory.batches if ability.id in batch.memberIds
        ]
        if len(owners) > 1:
            add(
                "batch-membership",
                ability.id,
                "batchId",
                "ability belongs to multiple batches",
            )
    for group, graph in (
        ("capabilities", {c.id: c.prerequisiteIds for c in inventory.capabilities}),
        ("batches", {b.id: b.prerequisiteIds for b in inventory.batches}),
    ):
        for node in _cycles(graph):
            add("prerequisite-cycle", node, group, "cyclic prerequisites")
    for q in build_report(inventory).quantities:
        if q.overcountCopies:
            add(
                "quantity-overcount",
                q.expectationId,
                "expectedCopies",
                f"{q.overcountCopies} known copies over expected total",
            )
        if (
            q.uniqueDefinitions is not None
            and q.discoveredDefinitions > q.uniqueDefinitions
        ):
            add(
                "identity-overcount",
                q.expectationId,
                "uniqueDefinitions",
                "more definitions than official total",
            )
    for configuration in inventory.configurations:
        refs(configuration.contentIds, content, configuration.id, "contentIds")
        for content_id in configuration.contentIds:
            if content_id in content and not _content_applies(
                content[content_id], configuration
            ):
                add(
                    "content-applicability",
                    configuration.id,
                    "contentIds",
                    f"{content_id} is unavailable in this configuration",
                )
        refs(
            configuration.interactionIds,
            interactions,
            configuration.id,
            "interactionIds",
        )
        evidence(configuration.evidence, configuration.id, "evidence")
        if configuration.base not in {
            Product.IMPERIUM,
            Product.UPRISING,
        } or configuration.seats not in {3, 4}:
            add(
                "configuration",
                configuration.id,
                "base/seats",
                "unsupported base or seat count",
            )
        if any(
            expansion in {Product.IMPERIUM, Product.UPRISING}
            for expansion in configuration.expansions
        ):
            add(
                "configuration",
                configuration.id,
                "expansions",
                "base games cannot be expansions",
            )
        for code, path, message in _configuration_dependencies(configuration):
            add(code, configuration.id, path, message)
        if configuration.authenticity == Authenticity.OFFICIAL and any(
            content[c].authenticity == Authenticity.SYNTHETIC
            for c in configuration.contentIds
            if c in content
        ):
            add(
                "authenticity",
                configuration.id,
                "contentIds",
                "official configuration includes synthetic content",
            )
    return tuple(sorted(issues, key=lambda i: (i.severity, i.code, i.recordId, i.path)))


def _validate_assertions(
    record: str,
    implementation: Implementation,
    validation: Validation,
    refs: tuple[str, ...],
    tests: tuple[TestReference, ...],
    blockers: tuple[Blocker, ...],
    root: Path,
    add: Any,
) -> None:
    if implementation == Implementation.IMPLEMENTED and not refs:
        add(
            "implementation-reference",
            record,
            "implementationRefs",
            "implemented claim needs member reference",
        )
    if validation == Validation.TESTED and not _has_test(
        tests,
        {EvidenceKind.BEHAVIOR, EvidenceKind.INTEGRATION, EvidenceKind.PRIVACY_REPLAY},
    ):
        add(
            "behavior-test",
            record,
            "testRefs",
            "tested claim needs current behavioral test",
        )
    if implementation == Implementation.IMPLEMENTED and _has_open_blocker(blockers):
        add(
            "blocked-implementation",
            record,
            "blockers",
            "implemented claim has open blocker",
        )
    for blocker in blockers:
        if not blocker.reason.strip() or not blocker.scope.strip():
            add("blocker-reason", record, "blockers", "blocker needs scope and reason")
    for test in tests:
        if not _is_safe_path(test.path):
            add("test-path", record, "testRefs.path", "test path escapes repository")
        elif not (root / test.path).is_file():
            add("test-path", record, "testRefs.path", "test file does not exist")
    for ref in refs:
        path = ref.split("::", 1)[0]
        if not _is_safe_path(path):
            add(
                "implementation-path",
                record,
                "implementationRefs",
                "implementation path escapes repository",
            )
        elif not (root / path).is_file():
            add(
                "implementation-path",
                record,
                "implementationRefs",
                "implementation file does not exist",
            )


def _validate_support(
    ability: AbilityRecord,
    capabilities: dict[str, CapabilityRecord],
    interactions: dict[str, InteractionRecord],
    root: Path,
    add: Any,
) -> None:
    _validate_assertions(
        ability.id,
        ability.implementation,
        ability.validation,
        ability.implementationRefs,
        ability.testRefs,
        ability.blockers,
        root,
        add,
    )
    if (
        ability.classification == Classification.UNRESOLVED
        and not ability.classificationReason
    ):
        add(
            "classification-reason",
            ability.id,
            "classificationReason",
            "unresolved classification needs reason",
        )
    if ability.verification == Verification.VERIFIED and (
        ability.sourcing != Sourcing.FULLY_SOURCED
        or not any(e.kind == EvidenceKind.SEMANTICS for e in ability.evidence)
    ):
        add(
            "verification-conflict",
            ability.id,
            "verification",
            "verified ability needs fully sourced semantic evidence",
        )
    if (
        ability.implementation == Implementation.UNSUPPORTED
        and not ability.exclusionReason
        and not ability.blockers
    ):
        add(
            "unsupported-reason",
            ability.id,
            "exclusionReason",
            "unsupported ability needs an engine boundary reason",
        )
    if ability.implementation == Implementation.IMPLEMENTED:
        for kind in (
            EvidenceKind.BEHAVIOR,
            EvidenceKind.INTEGRATION,
            EvidenceKind.PRIVACY_REPLAY,
        ):
            if not _has_test(ability.testRefs, {kind}):
                add(
                    "support-test",
                    ability.id,
                    "testRefs",
                    f"implemented ability needs current {kind.value} evidence",
                )
        if (
            not ability.semanticClaim
            or ability.discovery != Discovery.DISCOVERED
            or ability.sourcing != Sourcing.FULLY_SOURCED
            or ability.verification != Verification.VERIFIED
            or not any(e.kind == EvidenceKind.SEMANTICS for e in ability.evidence)
        ):
            add(
                "semantics",
                ability.id,
                "semanticClaim",
                "implemented ability needs complete verified semantics",
            )
        if ability.classification == Classification.UNRESOLVED:
            add(
                "classification",
                ability.id,
                "classification",
                "unresolved ability cannot be implemented",
            )
        if ability.validation != Validation.TESTED:
            add(
                "validation",
                ability.id,
                "validation",
                "implemented ability needs behavioral validation",
            )
        if any(
            not _capability_supported(ref, capabilities)
            for ref in ability.capabilityIds
        ):
            add(
                "capability-dependency",
                ability.id,
                "capabilityIds",
                "required capability is unsupported or unverified",
            )
        if any(
            interactions[i].implementation != Implementation.IMPLEMENTED
            or interactions[i].validation != Validation.TESTED
            for i in ability.interactionIds
            if i in interactions
        ):
            add(
                "interaction-dependency",
                ability.id,
                "interactionIds",
                "required interaction is unsupported or unverified",
            )


def build_report(inventory: AuditInventory) -> AuditReport:
    all_abilities = tuple(a for c in inventory.content for a in c.abilities)
    sources = {s.id: s for s in inventory.sources}
    quantities: list[QuantityResult] = []
    gaps: list[str] = []
    for expectation in inventory.expectations:
        matched = [c for c in inventory.content if expectation.id in c.expectationIds]
        verified = sum(
            _quantity_for(c, expectation) or 0
            for c in matched
            if c.verification == Verification.VERIFIED
            and _official_claim(c.evidence, EvidenceKind.QUANTITY, sources)
        )
        sourced = sum(
            _quantity_for(c, expectation) or 0
            for c in matched
            if c.sourcing != Sourcing.UNSOURCED
            and (
                c.verification != Verification.VERIFIED
                or not _official_claim(c.evidence, EvidenceKind.QUANTITY, sources)
            )
        )
        unknown = sum(_quantity_for(c, expectation) is None for c in matched)
        known = sum(_quantity_for(c, expectation) or 0 for c in matched)
        discovered = sum(c.discovery == Discovery.DISCOVERED for c in matched)
        residual = None if unknown else max(0, expectation.expectedCopies - known)
        overcount = max(0, known - expectation.expectedCopies)
        complete = (
            unknown == 0
            and residual == 0
            and overcount == 0
            and all(
                c.verification == Verification.VERIFIED
                and _official_claim(c.evidence, EvidenceKind.QUANTITY, sources)
                and _official_claim(c.evidence, EvidenceKind.IDENTITY, sources)
                for c in matched
            )
            and (
                expectation.uniqueDefinitions is not None
                and discovered == expectation.uniqueDefinitions
            )
            and discovered == len(matched)
        )
        quantities.append(
            QuantityResult(
                expectation.id,
                expectation.expectedCopies,
                verified,
                sourced,
                unknown,
                residual,
                overcount,
                expectation.uniqueDefinitions,
                discovered,
                complete,
            )
        )
        if not complete:
            gaps.append(expectation.id)
    content = {c.id: c for c in inventory.content}
    abilities = {a.id: a for a in all_abilities}
    capabilities = {c.id: c for c in inventory.capabilities}
    interactions = {i.id: i for i in inventory.interactions}

    def supported_ability(ability_id: str) -> bool:
        ability = abilities.get(ability_id)
        if ability is None:
            return False
        parent = content.get(ability.contentId)
        return (
            parent is not None
            and parent.authenticity == Authenticity.OFFICIAL
            and any(
                ability.id in slot.abilityIds
                and slot.discovery == SlotDiscovery.DISCOVERED
                for slot in parent.slots
            )
            and ability.implementation == Implementation.IMPLEMENTED
            and ability.validation == Validation.TESTED
            and ability.discovery == Discovery.DISCOVERED
            and ability.sourcing == Sourcing.FULLY_SOURCED
            and ability.verification == Verification.VERIFIED
            and ability.classification != Classification.UNRESOLVED
            and bool(ability.semanticClaim)
            and any(e.kind == EvidenceKind.SEMANTICS for e in ability.evidence)
            and bool(ability.implementationRefs)
            and not _has_open_blocker(ability.blockers)
            and all(
                _has_test(ability.testRefs, {kind})
                for kind in (
                    EvidenceKind.BEHAVIOR,
                    EvidenceKind.INTEGRATION,
                    EvidenceKind.PRIVACY_REPLAY,
                )
            )
            and all(
                _capability_supported(ref, capabilities)
                for ref in ability.capabilityIds
            )
            and all(
                ref in interactions
                and interactions[ref].implementation == Implementation.IMPLEMENTED
                and interactions[ref].validation == Validation.TESTED
                and not _has_open_blocker(interactions[ref].blockers)
                for ref in ability.interactionIds
            )
        )

    gates: list[ConfigurationGate] = []
    for config in inventory.configurations:
        expected_ids = {
            e.id for e in inventory.expectations if _expectation_applies(e, config)
        }
        relevant = [q for q in quantities if q.expectationId in expected_ids]
        required_content_ids = {
            c.id
            for c in inventory.content
            if any(ref in expected_ids for ref in c.expectationIds)
            and _content_applies(c, config)
        }
        selected_content_ids = set(config.contentIds)
        composition = (
            config.base in {Product.IMPERIUM, Product.UPRISING}
            and config.seats in {3, 4}
            and not any(
                expansion in {Product.IMPERIUM, Product.UPRISING}
                for expansion in config.expansions
            )
            and not _configuration_dependencies(config)
            and any(e.kind == EvidenceKind.COMPOSITION for e in config.evidence)
        )
        inventory_ok = (
            composition
            and bool(relevant)
            and all(q.complete for q in relevant)
            and bool(required_content_ids)
            and selected_content_ids == required_content_ids
        )
        ability_ids = [
            a.id
            for c in config.contentIds
            if c in content
            for a in content[c].abilities
        ]
        ability_ok = (
            inventory_ok
            and all(_complete_face(content[c], sources) for c in required_content_ids)
            and all(supported_ability(a) for a in ability_ids)
        )
        required_interaction_ids = {
            ref
            for a in ability_ids
            if a in abilities
            for ref in abilities[a].interactionIds
        }
        interactions_ok = (
            ability_ok
            and required_interaction_ids <= set(config.interactionIds)
            and all(
                i in interactions
                and interactions[i].implementation == Implementation.IMPLEMENTED
                and interactions[i].validation == Validation.TESTED
                and not _has_open_blocker(interactions[i].blockers)
                for i in config.interactionIds
            )
        )
        authentic = config.authenticity == Authenticity.OFFICIAL and all(
            content[c].authenticity == Authenticity.OFFICIAL
            for c in config.contentIds
            if c in content
        )
        init = (
            interactions_ok
            and authentic
            and _has_test(config.initializationTests, {EvidenceKind.INITIALIZATION})
        )
        game = init and _has_test(config.fullGameTests, {EvidenceKind.FULL_GAME})
        rl = game and _has_test(
            config.rlTests, {EvidenceKind.RL, EvidenceKind.PRIVACY_REPLAY}
        )
        reasons = tuple(
            name
            for name, ok in (
                ("composition", composition),
                ("inventory", inventory_ok),
                ("abilities", ability_ok),
                ("interactions", interactions_ok),
                ("authenticity", authentic),
                ("initialization", init),
                ("full-game", game),
                ("rl", rl),
            )
            if not ok
        )
        gates.append(
            ConfigurationGate(
                config.id,
                True,
                composition,
                inventory_ok,
                ability_ok,
                interactions_ok,
                init,
                game,
                rl,
                reasons,
            )
        )
    dependents = tuple(
        sorted(
            (
                c.id,
                tuple(sorted(a.id for a in all_abilities if c.id in a.capabilityIds)),
            )
            for c in inventory.capabilities
        )
    )
    return AuditReport(
        len(inventory.sources),
        len(inventory.expectations),
        len(inventory.content),
        len(all_abilities),
        sum(
            len(_FACE_SLOTS[c.category] - {s.slot for s in c.slots})
            + sum(
                s.discovery in {SlotDiscovery.UNDISCOVERED, SlotDiscovery.UNREADABLE}
                for s in c.slots
            )
            for c in inventory.content
        ),
        tuple(quantities),
        tuple(gates),
        dependents,
        tuple(gaps),
    )
