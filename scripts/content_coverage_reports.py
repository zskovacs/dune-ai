"""Deterministic Markdown projections of the offline audit inventory."""

from __future__ import annotations

from collections import Counter
from collections.abc import Iterator
from pathlib import Path

if __package__:
    from .content_coverage_model import AuditInventory, AuditReport
else:
    from content_coverage_model import AuditInventory, AuditReport

BEGIN = "<!-- BEGIN GENERATED CONTENT COVERAGE -->"
END = "<!-- END GENERATED CONTENT COVERAGE -->"
ROOT = Path("docs/content")


def _cell(value: object) -> str:
    if isinstance(value, (tuple, list)):
        value = ", ".join(sorted(map(str, value))) if value else "none"
    if value is None:
        return "unknown"
    if isinstance(value, bool):
        return "yes" if value else "no"
    return str(value).replace("|", "\\|").replace("\n", " ")


def _table(headers: tuple[str, ...], rows: list[tuple[object, ...]]) -> str:
    return "\n".join(
        [
            "| " + " | ".join(headers) + " |",
            "| " + " | ".join("---" for _ in headers) + " |",
            *("| " + " | ".join(_cell(v) for v in row) + " |" for row in rows),
        ]
    )


def _counts(items: list[object], attributes: tuple[str, ...]) -> str:
    lines = []
    for attribute in attributes:
        counts = Counter(getattr(item, attribute) for item in items)
        rows = [(key, count) for key, count in sorted(counts.items())]
        lines.extend(
            (f"### By {attribute}", "", _table((attribute, "Records"), rows), "")
        )
    return "\n".join(lines)


def _ability_owners(abilities: list[object], content: list[object]) -> Iterator[str]:
    owners = {c.id: c for c in content}
    for label, attribute in (("product", "product"), ("category", "category")):
        counts = Counter(getattr(owners[a.contentId], attribute) for a in abilities)
        yield (
            f"### By owning {label}\n\n"
            + _table((label, "Abilities"), sorted(counts.items()))
            + "\n"
        )


def _refs(refs: tuple[object, ...]) -> tuple[str, ...]:
    return tuple(f"{r.sourceId} p.{r.page or '?'} {r.locator}" for r in refs)


def render_reports(report: AuditReport, inventory: AuditInventory) -> dict[Path, str]:
    """Return stable generated region bodies, keyed by repository-relative path."""
    content = sorted(inventory.content, key=lambda c: c.id)
    abilities = sorted((a for c in content for a in c.abilities), key=lambda a: a.id)
    face_counts = Counter(c.category for c in content if c.faceComplete)
    total_by_category = Counter(c.category for c in content)
    non_board_faces = sum(
        count for category, count in face_counts.items() if category != "BoardSpace"
    )
    non_board_categories = ", ".join(
        sorted(str(category) for category in face_counts if category != "BoardSpace")
    )
    non_board_label = (
        "card/Intrigue"
        if all(
            str(category).endswith("Card")
            for category in face_counts
            if category != "BoardSpace"
        )
        else "non-board"
    )
    unreadable_placeholders = sum(
        a.discovery == "not-discovered"
        and a.sourcing == "unsourced"
        and any(
            slot.slot == a.slot
            and slot.discovery == "unreadable"
            and a.id in slot.abilityIds
            for slot in c.slots
        )
        for c in content
        for a in c.abilities
    )
    sourced_clauses = sum(a.sourcing == "fully-sourced" for a in abilities)
    quantities = sorted(report.quantities, key=lambda q: q.expectationId)
    expectations = {e.id: e for e in inventory.expectations}
    inventory_body = [
        f"Audit revision: `{inventory.auditRevision}`; "
        f"schema: `{inventory.schemaVersion}`.",
        "",
        f"Sources: {report.sourceCount}; expectations: {report.expectationCount}; "
        f"content identity/component-group records: {report.contentCount}; "
        "identity/quantity "
        f"complete expectations: {sum(q.complete for q in quantities)}; "
        f"explicit gaps: {len(report.gaps)}; unknown ability slots: "
        f"{report.unknownSlots}.",
        "",
        "Expectations overlap across pools, subsets, recipes and exclusions. "
        "These counts are not additive game totals. A complete quantity row "
        "does not certify a complete face. Content records are audited identities "
        "or component groups, not physical copies or runtime instances.",
        "",
        f"Complete source faces: {sum(face_counts.values())}, including "
        f"{face_counts.get('BoardSpace', 0)} board spaces and "
        f"{non_board_faces} {non_board_label} faces ({non_board_categories}). "
        "A complete "
        "source face does not establish executable support.",
        "",
        "### Face completeness by category",
        "",
        _table(
            ("Category", "Complete faces", "Incomplete faces", "Audited records"),
            [
                (
                    category,
                    face_counts[category],
                    total_by_category[category] - face_counts[category],
                    total_by_category[category],
                )
                for category in sorted(total_by_category)
            ],
        ),
        "",
        "### Content identity and metadata status",
        "",
        "Discovery, sourcing and verification below concern audited identity/"
        "metadata evidence. They do not establish whole ability faces or known "
        "copy quantities.",
        "",
        _counts(
            content, ("product", "category", "discovery", "sourcing", "verification")
        ),
        "### Quantity reconciliation",
        "",
        _table(
            (
                "Expectation",
                "Product / category / scope",
                "Expected copies",
                "Verified copies",
                "Sourced unverified copies",
                "Unknown-copy records",
                "Missing residual",
                "Overcount",
                "Official unique total",
                "Discovered definitions",
                "Complete",
            ),
            [
                (
                    q.expectationId,
                    f"{expectations[q.expectationId].product} / "
                    f"{expectations[q.expectationId].category} / "
                    f"{expectations[q.expectationId].scope}",
                    q.expectedCopies,
                    q.verifiedCopies,
                    q.sourcedUnverifiedCopies,
                    q.unknownCopyRecords,
                    q.missingResidualCopies,
                    q.overcountCopies,
                    q.uniqueDefinitions,
                    q.discoveredDefinitions,
                    q.complete,
                )
                for q in quantities
            ],
        ),
        "",
        "### Content identity and face status",
        "",
        _table(
            (
                "ID",
                "Product",
                "Category",
                "Boxed / per-player copies",
                "Source / verification",
                "Face complete",
                "Applicability",
                "Known abilities",
            ),
            [
                (
                    c.id,
                    c.product,
                    c.category,
                    f"{_cell(c.boxedQuantity)} / {_cell(c.perPlayerQuantity)}",
                    f"{c.sourcing} / {c.verification}",
                    c.faceComplete,
                    c.abilityApplicability,
                    len(c.abilities),
                )
                for c in content
            ],
        ),
    ]
    ability_body = [
        f"Known authored ability clauses: {report.abilityCount}; unknown or unreadable "
        f"applicable slots: {report.unknownSlots}. Implemented means code-present "
        "for a bounded sourced behavior; it does not mean a whole face or "
        "configuration is validated.",
        "",
        f"Fully sourced authored clauses: {sourced_clauses}; source-unreadable "
        f"authored placeholders: {unreadable_placeholders}. Placeholders denote "
        "unreadable printed boxes, not verified effects; unlisted unknown slots "
        "remain separate.",
        "",
        _counts(
            abilities,
            (
                "classification",
                "implementation",
                "validation",
                "sourcing",
                "verification",
            ),
        ),
        *tuple(_ability_owners(abilities, content)),
        "### Known abilities",
        "",
        _table(
            (
                "ID",
                "Product / category",
                "Class",
                "Source",
                "Verification",
                "Implementation",
                "Behavior test",
                "Capabilities",
                "Interactions",
                "Batch",
            ),
            [
                (
                    a.id,
                    f"{next(c.product for c in content if c.id == a.contentId)} / "
                    f"{next(c.category for c in content if c.id == a.contentId)}",
                    a.classification,
                    a.sourcing,
                    a.verification,
                    a.implementation,
                    a.validation,
                    a.capabilityIds,
                    a.interactionIds,
                    a.batchId,
                )
                for a in abilities
            ],
        ),
        "",
        "### Shared mechanic dependencies",
        "",
        _table(
            (
                "Capability",
                "Implementation / test",
                "Prerequisites",
                "Known dependent abilities",
                "Supported subset",
                "Missing delta",
                "Blockers",
            ),
            [
                (
                    c.id,
                    f"{c.implementation} / {c.validation}",
                    c.prerequisiteIds,
                    dict(report.capabilityDependents).get(c.id, ()),
                    c.supportedSubset,
                    c.missingDelta,
                    tuple(b.id for b in c.blockers if b.open),
                )
                for c in sorted(inventory.capabilities, key=lambda c: c.id)
            ],
        ),
    ]
    interaction_body = [
        f"Known interactions: {len(inventory.interactions)}. A sourced ruling, "
        "implemented hook and current behavior/integration/privacy tests "
        "are separate claims.",
        "",
        _table(
            (
                "ID",
                "Participants",
                "Rulesets",
                "Ruling",
                "Implementation",
                "Tests",
                "Required hook",
                "Exceptions",
                "Scenarios",
                "Open blockers",
            ),
            [
                (
                    i.id,
                    i.participantIds,
                    i.rulesets,
                    i.rulingStatus,
                    i.implementation,
                    i.validation,
                    i.requiredHook,
                    i.exceptions,
                    i.scenarios,
                    tuple(b.id for b in i.blockers if b.open),
                )
                for i in sorted(inventory.interactions, key=lambda i: i.id)
            ],
        ),
    ]
    batch_body = [
        f"Prioritized batches: {len(inventory.batches)}. Order and membership "
        "are authored in the inventory; source discovery does not promise "
        "executable ability gains.",
        "",
    ]
    for b in sorted(inventory.batches, key=lambda b: (b.order, b.id)):
        batch_body.extend(
            (
                f"### {b.order}. {b.title or b.id} (`{b.id}`)",
                "",
                _table(
                    ("Field", "Canonical value"),
                    [
                        (
                            "Priority / rationale",
                            f"{b.priority}: {b.priorityRationale}",
                        ),
                        ("Exact member IDs", b.memberIds),
                        ("Prerequisites", b.prerequisiteIds),
                        ("Source gates", b.sourceGates),
                        ("Source references", _refs(b.evidence)),
                        ("Mechanics", b.mechanics),
                        ("Expected delta", b.expectedDelta),
                        ("Risks", b.risks),
                        ("Required tests / validation", b.validationRequirements),
                        ("Acceptance", b.acceptance),
                    ],
                ),
                "",
            )
        )
    readiness_body = [
        f"Configurations assessed: {len(report.configurationGates)}. "
        "`validate` checks audit integrity only. Initialization, full-game "
        "and RL require verified authentic execution provenance for the exact "
        "preset, seats and Leader policy; metadata test references alone "
        "cannot establish it.",
        "",
        _table(
            (
                "Configuration",
                "Recognized",
                "Composition",
                "Inventory",
                "Abilities",
                "Interactions",
                "Initialization",
                "Full game",
                "RL",
                "Failed reasons",
            ),
            [
                (
                    g.configurationId,
                    g.recognized,
                    g.composition,
                    g.inventory,
                    g.abilities,
                    g.interactions,
                    g.initialization,
                    g.fullGame,
                    g.rl,
                    g.reasons,
                )
                for g in sorted(
                    report.configurationGates, key=lambda g: g.configurationId
                )
            ],
        ),
        "",
        "### Selected recipe and evidence",
        "",
        _table(
            (
                "ID",
                "Base / expansions / modules",
                "Mode / seats / Leader policy",
                "Preset",
                "Selected content",
                "Interactions",
                "Open execution blockers",
            ),
            [
                (
                    c.id,
                    f"{c.base} / {_cell(c.expansions)} / {_cell(c.modules)}",
                    f"{c.mode} / {c.seats} / {c.leaderPolicy}",
                    c.preset,
                    c.contentIds,
                    c.interactionIds,
                    tuple(b.id for b in c.executionBlockers if b.open),
                )
                for c in sorted(inventory.configurations, key=lambda c: c.id)
            ],
        ),
    ]
    return {
        ROOT / "inventory-audit.md": "\n".join(inventory_body).rstrip() + "\n",
        ROOT / "ability-coverage.md": "\n".join(ability_body).rstrip() + "\n",
        ROOT / "interaction-matrix.md": "\n".join(interaction_body).rstrip() + "\n",
        ROOT / "implementation-batches.md": "\n".join(batch_body).rstrip() + "\n",
        ROOT / "readiness-gates.md": "\n".join(readiness_body).rstrip() + "\n",
    }


def update_region(existing: str, rendered: str) -> str:
    """Replace only the marked region in an existing Markdown document."""
    if existing.count(BEGIN) != 1 or existing.count(END) != 1:
        raise ValueError("expected exactly one generated content coverage marker pair")
    start = existing.index(BEGIN) + len(BEGIN)
    end = existing.index(END)
    if start > end:
        raise ValueError("generated content coverage markers are reversed")
    return existing[:start] + "\n" + rendered + existing[end:]
