"""Focused mutation checks for the offline content audit contract."""

from __future__ import annotations

import copy
import json
import sys
from dataclasses import replace
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from content_coverage_model import (  # noqa: E402
    _expectation_applies,
    build_report,
    load_inventory,
    validate_inventory,
)
from content_coverage_reports import _cell, render_reports, update_region  # noqa: E402


@pytest.fixture
def raw() -> dict:
    return json.loads((ROOT / "docs/content/coverage/inventory.json").read_text())


def load(raw: dict, tmp_path: Path):
    path = tmp_path / "inventory.json"
    path.write_text(json.dumps(raw))
    return load_inventory(path)


def codes(raw: dict, tmp_path: Path, root: Path = ROOT) -> set[str]:
    return {
        issue.code
        for issue in validate_inventory(load(raw, tmp_path), root)
        if issue.severity == "error"
    }


def test_generated_reports_are_stable_and_preserve_prose(raw, tmp_path):
    inventory = load(raw, tmp_path)
    report = build_report(inventory)
    first = render_reports(report, inventory)
    assert len(first) == 5
    assert first == render_reports(build_report(inventory), inventory)
    for body in first.values():
        document = (
            "intro\n<!-- BEGIN GENERATED CONTENT COVERAGE -->\nold\n"
            "<!-- END GENERATED CONTENT COVERAGE -->\noutro\n"
        )
        updated = update_region(document, body)
        assert updated.startswith("intro\n") and updated.endswith("outro\n")
        assert update_region(updated, body) == updated
        assert "old\n" not in updated
    assert "unknown" in first[Path("docs/content/inventory-audit.md")]
    assert (
        "authentic-execution-unverified"
        in first[Path("docs/content/readiness-gates.md")]
    )


def test_generated_reports_sort_set_like_records(raw, tmp_path):
    first = render_reports(build_report(load(raw, tmp_path)), load(raw, tmp_path))
    for key in (
        "sources",
        "expectations",
        "content",
        "capabilities",
        "interactions",
        "batches",
        "configurations",
    ):
        raw[key].reverse()
    reordered = load(raw, tmp_path)
    assert first == render_reports(build_report(reordered), reordered)


def test_report_check_detects_stale_without_writing(raw, tmp_path, monkeypatch):
    import content_coverage  # noqa: PLC0415

    inventory = load(raw, tmp_path)
    monkeypatch.setattr(content_coverage, "load_inventory", lambda _: inventory)
    monkeypatch.setattr(content_coverage, "validate_inventory", lambda *_: ())
    monkeypatch.chdir(tmp_path)
    (tmp_path / "docs/content").mkdir(parents=True)
    assert (
        content_coverage.main(["report", "--inventory", "fixture.json", "--check"]) == 1
    )
    assert not (tmp_path / "docs/content/inventory-audit.md").exists()
    assert content_coverage.main(["report", "--inventory", "fixture.json"]) == 0
    paths = sorted((tmp_path / "docs/content").glob("*.md"))
    assert len(paths) == 5
    original = {path: path.read_bytes() for path in paths}
    assert content_coverage.main(["report", "--inventory", "fixture.json"]) == 0
    assert {path: path.read_bytes() for path in paths} == original
    assert (
        content_coverage.main(["report", "--inventory", "fixture.json", "--check"]) == 0
    )
    stale_path = tmp_path / "docs/content/inventory-audit.md"
    stale_path.write_text(stale_path.read_text().replace("Sources:", "Stale sources:"))
    stale = {path: path.read_bytes() for path in paths}
    assert (
        content_coverage.main(["report", "--inventory", "fixture.json", "--check"]) == 1
    )
    assert {path: path.read_bytes() for path in paths} == stale


def test_quantity_null_and_zero_are_distinct(raw, tmp_path):
    inventory = load(raw, tmp_path)
    report = build_report(inventory)
    quantity = next(q for q in report.quantities if q.uniqueDefinitions is None)
    path = Path("docs/content/inventory-audit.md")
    unknown = render_reports(report, inventory)[path]
    zero = render_reports(
        replace(
            report,
            quantities=tuple(
                replace(q, uniqueDefinitions=0) if q == quantity else q
                for q in report.quantities
            ),
        ),
        inventory,
    )[path]
    assert unknown != zero
    assert f"| {quantity.expectationId} |" in unknown
    assert f"| unknown | {quantity.discoveredDefinitions} |" in unknown
    assert f"| 0 | {quantity.discoveredDefinitions} |" in zero


def test_report_count_projections_match_canonical_records(raw, tmp_path):
    inventory = load(raw, tmp_path)
    report = build_report(inventory)
    rendered = render_reports(report, inventory)
    assert (
        f"content identity/component-group records: {len(inventory.content)}"
        in rendered[Path("docs/content/inventory-audit.md")]
    )
    assert (
        f"Known authored ability clauses: {report.abilityCount}"
        in rendered[Path("docs/content/ability-coverage.md")]
    )
    assert (
        f"Known interactions: {len(inventory.interactions)}"
        in rendered[Path("docs/content/interaction-matrix.md")]
    )
    assert (
        f"Prioritized batches: {len(inventory.batches)}"
        in rendered[Path("docs/content/implementation-batches.md")]
    )
    assert (
        f"Configurations assessed: {len(inventory.configurations)}"
        in rendered[Path("docs/content/readiness-gates.md")]
    )


def test_face_and_placeholder_summaries_come_from_canonical_records(raw, tmp_path):
    inventory = load(raw, tmp_path)
    rendered = render_reports(build_report(inventory), inventory)
    faces = [c for c in inventory.content if c.faceComplete]
    board_faces = [c for c in faces if c.category == "BoardSpace"]
    assert (len(faces), len(board_faces), len(faces) - len(board_faces)) == (30, 22, 8)
    inventory_body = rendered[Path("docs/content/inventory-audit.md")]
    assert (
        f"Complete source faces: {len(faces)}, including "
        f"{len(board_faces)} board spaces and "
        f"{len(faces) - len(board_faces)} card/Intrigue faces" in inventory_body
    )
    for category in sorted({c.category for c in inventory.content}):
        complete = sum(
            c.category == category and c.faceComplete for c in inventory.content
        )
        total = sum(c.category == category for c in inventory.content)
        assert (
            f"| {category} | {complete} | {total - complete} | {total} |"
            in inventory_body
        )
    abilities = [a for c in inventory.content for a in c.abilities]
    sourced = sum(a.sourcing == "fully-sourced" for a in abilities)
    unreadable = sum(
        a.discovery == "not-discovered"
        and a.sourcing == "unsourced"
        and any(
            slot.slot == a.slot
            and slot.discovery == "unreadable"
            and a.id in slot.abilityIds
            for slot in c.slots
        )
        for c in inventory.content
        for a in c.abilities
    )
    assert (sourced, unreadable) == (42, 13)
    assert (
        f"Fully sourced authored clauses: {sourced}; "
        f"source-unreadable authored placeholders: {unreadable}"
        in rendered[Path("docs/content/ability-coverage.md")]
    )


def test_table_cell_escapes_sequence_members():
    assert _cell(("second|part", "first\npart")) == "first part, second\\|part"


def content_record() -> dict:
    return {
        "id": "Imperium/StartingCard/test/v1",
        "name": "Test",
        "product": "Imperium",
        "edition": "2020-10-26",
        "category": "StartingCard",
        "version": "v1",
        "variantOf": None,
        "authenticity": "official",
        "boxedQuantity": 1,
        "perPlayerQuantity": None,
        "expectationIds": ["exp-imperium-starters-four-decks"],
        "evidence": [
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "component count",
                "kind": "quantity",
                "verification": "verified",
            },
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "printed identity",
                "kind": "identity",
                "verification": "verified",
            },
        ],
        "discovery": "discovered",
        "sourcing": "fully-sourced",
        "verification": "verified",
        "availability": {
            "baseGames": ["Imperium"],
            "requiredExpansions": [],
            "excludedExpansions": [],
            "requiredModules": [],
            "excludedModules": [],
            "modes": ["Standard"],
            "minimumSeats": 3,
            "maximumSeats": 4,
        },
        "faceComplete": False,
        "slots": [
            {"slot": "Agent", "discovery": "unreadable", "reason": "face not inspected"}
        ],
        "abilities": [],
    }


def test_content_registry_reference_validates_key_path_and_duplicates(tmp_path, raw):
    item = content_record()
    ref = {
        "registryKind": "Catalog",
        "key": item["id"],
        "path": "src/Dune.Engine/Content/base-setup.json",
        "member": "ContentCatalog.LoadRepresentative",
    }
    item["implementationReferences"] = [ref]
    raw["content"] = [item]
    assert "registry-key" not in codes(raw, tmp_path)
    item["implementationReferences"] = [ref, copy.deepcopy(ref)]
    assert "duplicate-registry-reference" in codes(raw, tmp_path)
    item["implementationReferences"] = [{**ref, "key": "wrong", "path": "../outside"}]
    assert {"registry-key", "implementation-path"} <= codes(raw, tmp_path)
    item["implementationReferences"] = [{**ref, "registryKind": "Unknown"}]
    with pytest.raises(ValueError, match="unknown RegistryKind"):
        load(raw, tmp_path)


def ability_record(content_id: str) -> dict:
    return {
        "id": f"{content_id}#slot:Agent",
        "contentId": content_id,
        "slot": "Agent",
        "timing": "on-placement",
        "classification": "unresolved",
        "classificationReason": "face unreadable",
        "semanticClaim": None,
        "descriptors": [],
        "evidence": [],
        "capabilityIds": [],
        "interactionIds": [],
        "implementationRefs": [],
        "testRefs": [],
        "discovery": "not-discovered",
        "sourcing": "unsourced",
        "verification": "unverified",
        "implementation": "not-implemented",
        "validation": "not-tested",
        "blockers": [],
        "exclusionReason": None,
        "batchId": None,
    }


def test_canonical_inventory_is_valid_but_incomplete(tmp_path, raw):
    inventory = load(raw, tmp_path)
    assert not [i for i in validate_inventory(inventory, ROOT) if i.severity == "error"]
    report = build_report(inventory)
    assert report.sourceCount == 12
    assert report.expectationCount == 93
    assert len(report.gaps) == 72
    assert sum(q.complete for q in report.quantities) == 21


def test_original_physical_components_accounted_for_without_double_counting(
    raw, tmp_path
):
    inventory = load(raw, tmp_path)
    report = build_report(inventory)
    quantities = {q.expectationId: q for q in report.quantities}
    physical_counts = {
        "first-player-marker": 1,
        "water-tokens": 15,
        "solari-tokens": 24,  # Four value-five plus 20 value-one pieces.
        "spice-tokens": 23,  # Three value-five plus 20 value-one pieces.
        "agents-per-player": 3,
        "swordmaster-per-player": 1,
        "cubes-per-player": 16,
        "influence-cubes-per-player": 4,
        "troop-cubes-per-player": 12,
        "discs-per-player": 2,
        "score-marker-per-player": 1,
        "councilor-token-per-player": 1,
        "combat-marker-per-player": 1,
        "control-per-player": 3,
    }
    for suffix, expected in physical_counts.items():
        quantity = quantities[f"exp-imperium-{suffix}"]
        assert quantity.expectedCopies == expected
        assert quantity.verifiedCopies == expected
        assert quantity.complete
    assert quantities["exp-imperium-agents-per-player"].verifiedCopies == 2 + 1
    assert quantities["exp-imperium-cubes-per-player"].verifiedCopies == 4 + 12
    assert quantities["exp-imperium-discs-per-player"].verifiedCopies == 1 + 1
    components = [
        c for c in inventory.content if c.category.value == "GameplayComponent"
    ]
    assert all(
        not c.runtimeDefinition and not c.abilities and not c.slots for c in components
    )


def test_board_face_requires_placement_slot_without_extra_slots(raw, tmp_path):
    board = next(c for c in raw["content"] if c["id"] == "imperium:space:foldspace:v1")
    assert board["faceComplete"]
    assert [slot["slot"] for slot in board["slots"]] == ["Agent"]
    assert "false-complete" not in codes(raw, tmp_path)
    board["slots"] = []
    assert "false-complete" in codes(raw, tmp_path)


def test_duplicate_content_and_ability_ids_rejected(tmp_path, raw):
    item = content_record()
    ability = ability_record(item["id"])
    item["slots"][0]["abilityIds"] = [ability["id"]]
    item["abilities"] = [ability, copy.deepcopy(ability)]
    raw["content"] = [item, copy.deepcopy(item)]
    assert "duplicate-id" in codes(raw, tmp_path)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda d: d.update(extra=1),
        lambda d: d["content"].append({**content_record(), "product": "Ix"}),
        lambda d: d["content"].append(
            {
                **content_record(),
                "slots": [{"slot": "Wrong", "discovery": "unreadable"}],
            }
        ),
        lambda d: d["content"].append({**content_record(), "boxedQuantity": True}),
        lambda d: d["content"].append({**content_record(), "boxedQuantity": "1"}),
        lambda d: d["content"].append(
            {
                **content_record(),
                "availability": {
                    **content_record()["availability"],
                    "requiredModules": ["Choam"],
                },
            }
        ),
        lambda d: d["expectations"][0].update(basis="made-up"),
        lambda d: d["expectations"][0].update(scope="made-up"),
    ],
)
def test_unknown_member_product_slot_and_quantity_type_rejected(tmp_path, raw, mutate):
    mutate(raw)
    with pytest.raises(ValueError):
        load(raw, tmp_path)


def test_broken_refs_pages_and_bad_paths_hashes(tmp_path, raw):
    raw["content"] = [{**content_record(), "expectationIds": ["missing"]}]
    raw["expectations"][0]["evidence"][0]["page"] = 0
    raw["expectations"][1]["evidence"][0]["page"] = 999
    raw["sources"][0]["locator"] = "../outside.pdf"
    raw["sources"][1]["sha256"] = "0" * 64
    assert {"broken-reference", "source-page", "source-path", "source-hash"} <= codes(
        raw, tmp_path
    )


def test_prerequisite_cycles_rejected(tmp_path, raw):
    def cap(id_, dep):
        return {
            "id": id_,
            "contract": "test",
            "supportedSubset": None,
            "memberRefs": [],
            "testRefs": [],
            "missingDelta": None,
            "prerequisiteIds": [dep],
            "implementation": "not-implemented",
            "validation": "not-tested",
            "blockers": [],
        }

    raw["capabilities"] = [cap("cap-a", "cap-b"), cap("cap-b", "cap-a")]
    raw["batches"] = [
        {
            "id": "batch-a",
            "memberIds": [],
            "prerequisiteIds": ["batch-b"],
            "sourceGates": [],
            "mechanics": [],
            "expectedDelta": "none",
            "acceptance": "none",
            "risks": [],
        },
        {
            "id": "batch-b",
            "memberIds": [],
            "prerequisiteIds": ["batch-a"],
            "sourceGates": [],
            "mechanics": [],
            "expectedDelta": "none",
            "acceptance": "none",
            "risks": [],
        },
    ]
    assert "prerequisite-cycle" in codes(raw, tmp_path)


def test_wrong_identity_does_not_complete_equal_total(tmp_path, raw):
    exp = next(
        e for e in raw["expectations"] if e["id"] == "exp-imperium-starters-four-decks"
    )
    item = content_record()
    item["boxedQuantity"] = exp["expectedCopies"]
    item["expectationIds"] = ["exp-imperium-reserve"]
    raw["content"] = [item]
    report = build_report(load(raw, tmp_path))
    target = next(q for q in report.quantities if q.expectationId == exp["id"])
    assert target.missingResidualCopies == exp["expectedCopies"]
    assert not target.complete
    assert "applicability" in codes(raw, tmp_path)


def test_unknown_copies_and_unique_totals_remain_unknown(tmp_path, raw):
    item = content_record()
    item["boxedQuantity"] = None
    item["category"] = "ImperiumCard"
    item["expectationIds"] = ["exp-imperium-imperium"]
    raw["content"] = [item]
    report = build_report(load(raw, tmp_path))
    q = next(
        q for q in report.quantities if q.expectationId == item["expectationIds"][0]
    )
    assert q.unknownCopyRecords == 1
    assert q.missingResidualCopies is None
    assert q.uniqueDefinitions is None
    assert not q.complete


def test_explicit_residual_is_valid_but_incomplete(tmp_path, raw):
    raw["content"] = [content_record()]
    raw.update(interactions=[], batches=[], configurations=[])
    assert not codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    q = next(
        q
        for q in report.quantities
        if q.expectationId == raw["content"][0]["expectationIds"][0]
    )
    assert q.missingResidualCopies > 0
    assert not q.complete


def test_undiscovered_slots_and_unverified_dependencies_block_implementation(
    tmp_path, raw
):
    item = content_record()
    ability = ability_record(item["id"])
    item["slots"][0]["abilityIds"] = [ability["id"]]
    item["abilities"] = [ability]
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="claim",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        implementation="implemented",
        validation="tested",
        implementationRefs=["src/Dune.Engine/Foo.cs::Do"],
        testRefs=[{"path": "tests/Foo.cs", "name": "Test", "kind": "inventory"}],
        capabilityIds=["cap-x"],
    )
    raw["content"] = [item]
    raw["capabilities"] = [
        {
            "id": "cap-x",
            "contract": "behavior",
            "supportedSubset": None,
            "memberRefs": [],
            "testRefs": [],
            "missingDelta": "missing",
            "prerequisiteIds": [],
            "implementation": "unsupported",
            "validation": "not-tested",
            "blockers": [],
        }
    ]
    assert {"behavior-test", "capability-dependency"} <= codes(raw, tmp_path)
    assert build_report(load(raw, tmp_path)).unknownSlots >= 1


def test_optional_pdf_unavailable_offline_without_freshness(tmp_path, raw, monkeypatch):
    root = tmp_path / "empty-root"
    root.mkdir()
    raw["content"] = []  # Isolate optional source files from required registry paths.
    raw["capabilities"] = []
    raw.update(interactions=[], batches=[], configurations=[])

    def fail(*_args, **_kwargs):
        raise AssertionError("network called")

    monkeypatch.setattr("urllib.request.urlopen", fail)
    issues = validate_inventory(load(raw, tmp_path), root)
    assert sum(i.code == "evidence-unavailable" for i in issues) == 7
    assert not [i for i in issues if i.severity == "error"]


def test_per_player_recipe_uses_per_player_quantity(tmp_path, raw):
    item = content_record()
    item["expectationIds"] = ["exp-imperium-starter-per-player"]
    item["boxedQuantity"] = 40
    item["perPlayerQuantity"] = 2
    raw["content"] = [item]
    q = next(
        q
        for q in build_report(load(raw, tmp_path)).quantities
        if q.expectationId == "exp-imperium-starter-per-player"
    )
    assert q.verifiedCopies == 2
    assert q.missingResidualCopies == 8
    assert q.overcountCopies == 0


def test_verified_absent_slot_cannot_own_ability(tmp_path, raw):
    item = content_record()
    ability = ability_record(item["id"])
    item["slots"][0] = {
        "slot": "Agent",
        "discovery": "verified-absent",
        "evidence": [
            {"sourceId": "src-I", "page": 3, "locator": "face", "kind": "semantics"}
        ],
        "abilityIds": [ability["id"]],
    }
    item["abilities"] = [ability]
    raw["content"] = [item]
    assert "absent-slot-ability" in codes(raw, tmp_path)


def test_historical_or_metadata_test_does_not_validate_behavior(tmp_path, raw):
    item = content_record()
    ability = ability_record(item["id"])
    item["slots"][0]["abilityIds"] = [ability["id"]]
    item["abilities"] = [ability]
    ability["validation"] = "tested"
    ability["testRefs"] = [
        {"path": "tests/Foo.cs", "name": "Metadata", "kind": "inventory"},
        {
            "path": "tests/Foo.cs",
            "name": "Historical",
            "kind": "behavior",
            "historical": True,
        },
    ]
    raw["content"] = [item]
    assert "behavior-test" in codes(raw, tmp_path)


def test_known_overcount_is_integrity_error(tmp_path, raw):
    item = content_record()
    item["boxedQuantity"] = 41
    raw["content"] = [item]
    assert "quantity-overcount" in codes(raw, tmp_path)


def test_empty_slots_cannot_claim_complete(tmp_path, raw):
    item = content_record()
    item["slots"] = []
    item["faceComplete"] = True
    raw["content"] = [item]
    assert "false-complete" in codes(raw, tmp_path)


def test_unknown_rights_cannot_authorize_copied_artifacts(tmp_path, raw):
    raw["sources"][0]["reusePermission"] = True
    assert "source-rights" in codes(raw, tmp_path)


def test_configuration_without_composition_or_inventory_fails_closed(tmp_path, raw):
    raw["configurations"] = [
        {
            "id": "cfg-original-3p",
            "base": "Imperium",
            "expansions": [],
            "modules": [],
            "mode": "Standard",
            "seats": 3,
            "leaderPolicy": "all-official",
            "authenticity": "official",
            "contentIds": [],
            "interactionIds": [],
            "evidence": [],
            "initializationTests": [],
            "fullGameTests": [],
            "rlTests": [],
        }
    ]
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.recognized
    assert not gate.composition
    assert not gate.inventory
    assert not gate.abilities
    assert not gate.fullGame
    assert not gate.rl


def populated_inventory(raw: dict) -> tuple[dict, dict]:
    """A structurally complete one-definition fixture, separate from authored S1."""
    raw.update(interactions=[], batches=[], configurations=[])
    expectation = next(
        e
        for e in raw["expectations"]
        if e["id"] == "exp-imperium-reserve-arrakis-liaison"
    )
    raw["expectations"] = [expectation]
    item = content_record()
    item.update(
        id="Imperium/ReserveCard/fixture/v1",
        name="Fixture definition",
        category="ReserveCard",
        boxedQuantity=8,
        expectationIds=[expectation["id"]],
        faceComplete=True,
        faceEvidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "whole face fixture",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
    )
    # The category's potentially relevant slots are accounted for individually.
    slot_names = [
        "Setup",
        "Passive",
        "Agent",
        "Reveal",
        "Acquire",
        "SignetRing",
        "Combat",
        "PostWin",
        "Endgame",
        "Triggered",
        "PersistentModifier",
    ]
    item["slots"] = [
        {
            "slot": name,
            "discovery": "verified-absent",
            "evidence": [
                {
                    "sourceId": "src-I",
                    "page": 3,
                    "locator": f"{name} absence fixture",
                    "kind": "semantics",
                    "verification": "verified",
                }
            ],
        }
        for name in slot_names
    ]
    raw["content"] = [item]
    config = {
        "id": "cfg-fixture",
        "base": "Imperium",
        "expansions": [],
        "modules": [],
        "mode": "Standard",
        "seats": 3,
        "leaderPolicy": "all-official",
        "authenticity": "official",
        "contentIds": [item["id"]],
        "interactionIds": [],
        "evidence": [
            {
                "sourceId": "src-I",
                "page": 4,
                "locator": "setup fixture",
                "kind": "composition",
                "verification": "verified",
            }
        ],
        "initializationTests": [],
        "fullGameTests": [],
        "rlTests": [],
    }
    raw["configurations"] = [config]
    return item, config


def test_populated_full_face_requires_all_applicable_slots(tmp_path, raw):
    item, _ = populated_inventory(raw)
    assert not codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    assert report.configurationGates[0].inventory
    assert report.configurationGates[0].abilities  # All relevant slots verified absent.

    item["slots"] = [slot for slot in item["slots"] if slot["slot"] != "Reveal"]
    assert "false-complete" in codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    assert report.unknownSlots == 1
    assert report.configurationGates[0].inventory
    assert not report.configurationGates[0].abilities


def test_populated_face_needs_whole_face_evidence(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item["faceEvidence"] = []
    assert "false-complete" in codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    assert report.configurationGates[0].inventory
    assert not report.configurationGates[0].abilities


def test_complete_identity_quantity_with_unreadable_face_blocks_gameplay(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item.update(
        faceComplete=False,
        faceEvidence=[],
        slots=[
            {"slot": "Agent", "discovery": "unreadable", "reason": "face unavailable"}
        ],
    )
    assert not codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    gate = report.configurationGates[0]
    assert gate.inventory
    assert not gate.abilities
    assert not gate.initialization
    assert not gate.fullGame
    assert not gate.rl


def test_primitive_dependency_does_not_complete_partial_card(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item["faceComplete"] = False
    item["faceEvidence"] = []
    item["slots"][0] = {
        "slot": "Setup",
        "discovery": "undiscovered",
        "reason": "whole face still unavailable",
    }
    ability = ability_record(item["id"])
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="one sourced reward primitive",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        evidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "reward primitive",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
    )
    item["abilities"] = [ability]
    next(s for s in item["slots"] if s["slot"] == "Agent").update(
        discovery="discovered", abilityIds=[ability["id"]]
    )
    assert not codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    assert not report.configurationGates[0].abilities


@pytest.mark.parametrize("slot_name", ["Reveal", "Acquire"])
def test_unknown_card_box_blocks_whole_face_support(tmp_path, raw, slot_name):
    item, _ = populated_inventory(raw)
    item["faceComplete"] = False
    item["faceEvidence"] = []
    slot = next(s for s in item["slots"] if s["slot"] == slot_name)
    slot.update(discovery="unreadable", reason="printed box not available", evidence=[])
    assert not codes(raw, tmp_path)
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory
    assert not gate.abilities


def test_synthetic_tested_ability_cannot_raise_authentic_gate(tmp_path, raw):
    item, config = populated_inventory(raw)
    item["authenticity"] = "synthetic"
    ability = ability_record(item["id"])
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="tested synthetic reward",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        implementation="implemented",
        validation="tested",
        implementationRefs=["scripts/content_coverage_model.py::build_report"],
        evidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "synthetic fixture",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
        testRefs=[
            {
                "path": "training/tests/test_content_coverage.py",
                "name": "fixture",
                "kind": kind,
            }
            for kind in ("behavior", "integration", "privacy-replay")
        ],
    )
    item["abilities"] = [ability]
    next(s for s in item["slots"] if s["slot"] == "Agent").update(
        discovery="discovered", abilityIds=[ability["id"]]
    )
    config["authenticity"] = "synthetic"
    assert not codes(raw, tmp_path)
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory
    assert not gate.abilities


def test_implemented_behavior_can_lack_full_readiness_evidence(tmp_path, raw):
    item, _ = populated_inventory(raw)
    ability = ability_record(item["id"])
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="sourced executable reward",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        implementation="implemented",
        validation="tested",
        evidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "complete reward",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
        implementationRefs=["scripts/content_coverage_model.py::build_report"],
        testRefs=[
            {
                "path": "training/tests/test_content_coverage.py",
                "name": "test_implemented_behavior_can_lack_full_readiness_evidence",
                "kind": "behavior",
            }
        ],
    )
    item["abilities"] = [ability]
    next(s for s in item["slots"] if s["slot"] == "Agent").update(
        discovery="discovered", abilityIds=[ability["id"]]
    )
    assert not codes(raw, tmp_path)
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory
    assert not gate.abilities  # Integration and privacy/replay evidence still absent.


def test_canonical_tested_abilities_do_not_underreport_implementation(raw):
    tested = [
        ability
        for content in raw["content"]
        for ability in content["abilities"]
        if ability["validation"] == "tested"
    ]
    assert len(tested) == 34
    assert all(ability["implementation"] == "implemented" for ability in tested)
    master = next(a for a in tested if "master-tactician" in a["id"])
    assert master["implementation"] == "implemented"


def test_selected_configuration_cannot_omit_reconciled_definition(tmp_path, raw):
    item, config = populated_inventory(raw)
    expectation = raw["expectations"][0]
    expectation["expectedCopies"] = 8
    expectation["uniqueDefinitions"] = 2
    item["boxedQuantity"] = 4
    second = copy.deepcopy(item)
    second["id"] = "Imperium/ReserveCard/fixture-two/v1"
    second["name"] = "Fixture definition two"
    second["boxedQuantity"] = 4
    unsupported = ability_record(second["id"])
    unsupported.update(
        implementation="unsupported", exclusionReason="missing engine hook"
    )
    second["abilities"] = [unsupported]
    agent_slot = next(slot for slot in second["slots"] if slot["slot"] == "Agent")
    agent_slot.update(discovery="discovered", abilityIds=[unsupported["id"]])
    agent_slot.pop("evidence")
    raw["content"].append(second)

    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].complete
    assert not report.configurationGates[0].inventory
    assert not report.configurationGates[0].abilities

    config["contentIds"].append(second["id"])
    included = build_report(load(raw, tmp_path)).configurationGates[0]
    assert included.inventory
    assert not included.abilities


def test_excluded_mode_and_module_expectations_do_not_block_standard_gate(
    tmp_path, raw
):
    item, config = populated_inventory(raw)
    for excluded_id in ("exp-imperium-hagal",):
        excluded = json.loads(
            (ROOT / "docs/content/coverage/inventory.json").read_text()
        )
        raw["expectations"].append(
            next(e for e in excluded["expectations"] if e["id"] == excluded_id)
        )
    assert build_report(load(raw, tmp_path)).configurationGates[0].inventory
    assert item["id"] in config["contentIds"]


def test_transitive_capability_prerequisite_blocks_ability(tmp_path, raw):
    item, _ = populated_inventory(raw)
    ability = ability_record(item["id"])
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="Fixture effect",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        implementation="implemented",
        validation="tested",
        evidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "effect fixture",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
        implementationRefs=["scripts/content_coverage_model.py::_capability_supported"],
        testRefs=[
            {
                "path": "training/tests/test_content_coverage.py",
                "name": "fixture",
                "kind": kind,
            }
            for kind in ("behavior", "integration", "privacy-replay")
        ],
        capabilityIds=["cap-a"],
    )
    item["abilities"] = [ability]
    agent_slot = next(s for s in item["slots"] if s["slot"] == "Agent")
    agent_slot.update(discovery="discovered", abilityIds=[ability["id"]])
    agent_slot.pop("evidence")

    def cap(cap_id, prerequisites, implementation="implemented", validation="tested"):
        return {
            "id": cap_id,
            "contract": "Fixture behavior",
            "supportedSubset": None,
            "memberRefs": ["scripts/content_coverage_model.py::_capability_supported"]
            if implementation == "implemented"
            else [],
            "testRefs": [
                {
                    "path": "training/tests/test_content_coverage.py",
                    "name": "fixture",
                    "kind": "behavior",
                }
            ]
            if validation == "tested"
            else [],
            "missingDelta": "Unsupported prerequisite"
            if implementation == "unsupported"
            else None,
            "prerequisiteIds": prerequisites,
            "implementation": implementation,
            "validation": validation,
            "blockers": [],
        }

    raw["capabilities"] = [
        cap("cap-a", ["cap-b"]),
        cap("cap-b", ["cap-c"]),
        cap("cap-c", [], "unsupported", "not-tested"),
    ]
    assert {"capability-prerequisite", "capability-dependency"} <= codes(raw, tmp_path)
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory
    assert not gate.abilities


def test_verified_identity_needs_identity_claim(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item["evidence"] = [
        claim for claim in item["evidence"] if claim["kind"] != "identity"
    ]
    assert "identity-evidence" in codes(raw, tmp_path)
    report = build_report(load(raw, tmp_path))
    assert report.quantities[0].discoveredDefinitions == 1
    assert not report.quantities[0].complete
    assert not report.configurationGates[0].inventory


def test_selected_ability_requires_its_interaction_in_configuration(tmp_path, raw):
    item, config = populated_inventory(raw)
    ability = ability_record(item["id"])
    ability.update(
        classification="primitive",
        classificationReason=None,
        semanticClaim="Fixture effect",
        discovery="discovered",
        sourcing="fully-sourced",
        verification="verified",
        implementation="implemented",
        validation="tested",
        interactionIds=["int-fixture"],
        evidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "effect fixture",
                "kind": "semantics",
                "verification": "verified",
            }
        ],
        implementationRefs=["scripts/content_coverage_model.py::_capability_supported"],
        testRefs=[
            {
                "path": "training/tests/test_content_coverage.py",
                "name": "fixture",
                "kind": kind,
            }
            for kind in ("behavior", "integration", "privacy-replay")
        ],
    )
    item["abilities"] = [ability]
    agent_slot = next(s for s in item["slots"] if s["slot"] == "Agent")
    agent_slot.update(discovery="discovered", abilityIds=[ability["id"]])
    agent_slot.pop("evidence")
    raw["interactions"] = [
        {
            "id": "int-fixture",
            "participantIds": [item["id"]],
            "rulesets": [config["id"]],
            "evidence": [],
            "exceptions": [],
            "requiredHook": None,
            "implementationRefs": [
                "scripts/content_coverage_model.py::_capability_supported"
            ],
            "testRefs": [
                {
                    "path": "training/tests/test_content_coverage.py",
                    "name": "fixture",
                    "kind": kind,
                }
                for kind in ("behavior", "integration", "privacy-replay")
            ],
            "implementation": "implemented",
            "validation": "tested",
            "blockers": [],
        }
    ]
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory
    assert gate.abilities
    assert not gate.interactions
    config["interactionIds"] = ["int-fixture"]
    assert build_report(load(raw, tmp_path)).configurationGates[0].interactions


def test_typed_availability_selects_expansion_dependent_content(tmp_path, raw):
    item, config = populated_inventory(raw)
    raw["expectations"][0]["expectedCopies"] = 16
    raw["expectations"][0]["uniqueDefinitions"] = 2
    second = copy.deepcopy(item)
    second["id"] = "Imperium/ReserveCard/ix-dependent/v1"
    second["name"] = "Ix dependent fixture"
    second["availability"]["requiredExpansions"] = ["RiseOfIx"]
    raw["content"].append(second)
    assert build_report(load(raw, tmp_path)).configurationGates[0].inventory

    config["expansions"] = ["RiseOfIx"]
    assert not build_report(load(raw, tmp_path)).configurationGates[0].inventory
    config["contentIds"].append(second["id"])
    assert build_report(load(raw, tmp_path)).configurationGates[0].inventory


@pytest.mark.parametrize(
    "bad_availability",
    [
        {"minimumSeats": 5, "maximumSeats": 4},
        {"requiredExpansions": ["RiseOfIx"], "excludedExpansions": ["RiseOfIx"]},
    ],
)
def test_contradictory_availability_rejected(tmp_path, raw, bad_availability):
    item, _ = populated_inventory(raw)
    item["availability"].update(bad_availability)
    assert "availability" in codes(raw, tmp_path)


def test_module_expectations_follow_typed_module_selection(tmp_path, raw):
    _, config = populated_inventory(raw)
    original = json.loads((ROOT / "docs/content/coverage/inventory.json").read_text())
    wanted = {
        "exp-uprising-choam-contracts",
        "exp-bloodlines-tech-tiles",
    }
    raw["expectations"] = [e for e in original["expectations"] if e["id"] in wanted]
    config["base"] = "Uprising"
    config["expansions"] = ["Bloodlines"]
    loaded = load(raw, tmp_path)
    assert all(
        not _expectation_applies(expectation, loaded.configurations[0])
        for expectation in loaded.expectations
    )
    config["modules"] = ["UprisingChoam", "BloodlinesTech"]
    loaded = load(raw, tmp_path)
    assert all(
        _expectation_applies(expectation, loaded.configurations[0])
        for expectation in loaded.expectations
    )


@pytest.mark.parametrize(
    ("base", "expansions", "modules", "mode", "expected_code"),
    [
        ("Imperium", [], ["BloodlinesTech"], "Standard", "module-dependency"),
        (
            "Imperium",
            ["Bloodlines"],
            ["BloodlinesTech"],
            "Standard",
            "module-incompatible",
        ),
        ("Imperium", [], ["UprisingChoam"], "Standard", "module-incompatible"),
        ("Imperium", [], [], "Epic", "mode-dependency"),
        ("Uprising", ["RiseOfIx"], [], "Epic", "mode-incompatible"),
    ],
)
def test_invalid_module_and_epic_composition_fails_validation_and_gate(
    tmp_path, raw, base, expansions, modules, mode, expected_code
):
    _, config = populated_inventory(raw)
    config.update(
        base=base, expansions=expansions, modules=modules, mode=mode, contentIds=[]
    )
    assert expected_code in codes(raw, tmp_path)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].composition


@pytest.mark.parametrize(
    ("base", "expansions", "modules", "mode"),
    [
        ("Uprising", ["Bloodlines"], ["BloodlinesTech"], "Standard"),
        ("Uprising", [], ["UprisingChoam"], "Standard"),
        ("Imperium", ["RiseOfIx"], [], "Epic"),
    ],
)
def test_valid_module_and_epic_dependencies_allow_composition(
    tmp_path, raw, base, expansions, modules, mode
):
    _, config = populated_inventory(raw)
    config.update(
        base=base, expansions=expansions, modules=modules, mode=mode, contentIds=[]
    )
    errors = codes(raw, tmp_path)
    assert not errors & {
        "module-dependency",
        "module-incompatible",
        "mode-dependency",
        "mode-incompatible",
    }
    assert build_report(load(raw, tmp_path)).configurationGates[0].composition


def test_physical_only_evidence_can_pass_ability_gate_without_face(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item.update(
        category="GameplayComponent",
        faceComplete=False,
        faceEvidence=[],
        slots=[],
        abilityApplicability="physical-only",
        abilityApplicabilityEvidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "physical inventory fixture",
                "kind": "applicability",
                "verification": "verified",
            }
        ],
    )
    raw["expectations"][0]["category"] = "GameplayComponent"
    assert not codes(raw, tmp_path)
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory and gate.abilities and gate.interactions
    assert not gate.initialization
    item["abilityApplicabilityEvidence"] = []
    assert "ability-applicability" in codes(raw, tmp_path)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].abilities
    item.pop("abilityApplicability")
    assert not build_report(load(raw, tmp_path)).configurationGates[0].abilities


def test_physical_only_cannot_hide_unknown_executable_boxes(tmp_path, raw):
    item, _ = populated_inventory(raw)
    item.update(
        abilityApplicability="physical-only",
        abilityApplicabilityEvidence=[
            {
                "sourceId": "src-I",
                "page": 3,
                "locator": "false physical claim",
                "kind": "applicability",
                "verification": "verified",
            }
        ],
    )
    assert "ability-applicability" in codes(raw, tmp_path)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].abilities


@pytest.mark.parametrize("reference_kind", ["existing-synthetic-test", "invented-test"])
def test_terminal_synthetic_evidence_cannot_certify_authentic_or_rl(
    tmp_path, raw, reference_kind
):
    item, config = populated_inventory(raw)
    path, name = (
        (
            "tests/Dune.Engine.Tests/LifecycleIntegrationTests.cs",
            "EveryAcceptedPrefixReplaysNestedForeignRewardsThroughNaturalEndgame",
        )
        if reference_kind == "existing-synthetic-test"
        else ("training/tests/test_content_coverage.py", "positive gate fixture")
    )
    for field, kinds in (
        ("initializationTests", ["initialization"]),
        ("fullGameTests", ["full-game"]),
        ("rlTests", ["rl", "privacy-replay"]),
    ):
        config[field] = [
            {
                "path": path,
                "name": name,
                "kind": kind,
            }
            for kind in kinds
        ]
    assert item["authenticity"] == config["authenticity"] == "official"
    assert not codes(raw, tmp_path)
    official = build_report(load(raw, tmp_path)).configurationGates[0]
    assert official.inventory and official.abilities and official.interactions
    assert not official.initialization and not official.fullGame and not official.rl
    assert "authentic-execution-unverified" in official.reasons
    config["authenticity"] = item["authenticity"] = "synthetic"
    negative = build_report(load(raw, tmp_path)).configurationGates[0]
    assert not negative.abilities
    assert not negative.initialization and not negative.fullGame and not negative.rl


@pytest.mark.parametrize("interaction_id", ["int-unknown", "int-unsupported"])
def test_unknown_or_unsupported_interaction_blocks_populated_gate(
    tmp_path, raw, interaction_id
):
    item, config = populated_inventory(raw)
    config["interactionIds"] = [interaction_id]
    raw["interactions"] = [
        {
            "id": "int-unsupported",
            "participantIds": [item["id"]],
            "rulesets": [config["id"]],
            "evidence": [],
            "exceptions": [],
            "requiredHook": "not implemented",
            "implementationRefs": [],
            "testRefs": [],
            "implementation": "unsupported",
            "validation": "not-tested",
            "blockers": [],
        }
    ]
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory and gate.abilities
    assert not gate.interactions and not gate.initialization and not gate.rl


def test_fixed_leader_subset_cannot_certify_all_official_policy(tmp_path, raw):
    item, config = populated_inventory(raw)
    item["category"] = raw["expectations"][0]["category"] = "Leader"
    item["boxedQuantity"] = 1
    raw["expectations"][0].update(expectedCopies=3, uniqueDefinitions=3)
    raw["content"] = [
        {**copy.deepcopy(item), "id": f"fixture:leader:{n}:v1", "name": f"Leader {n}"}
        for n in range(3)
    ]
    config["contentIds"] = [c["id"] for c in raw["content"]]
    config.update(
        leaderPolicy="fixed-subset", leaderContentIds=config["contentIds"].copy()
    )
    assert not codes(raw, tmp_path)
    assert build_report(load(raw, tmp_path)).configurationGates[0].abilities
    config["leaderPolicy"] = "all-official"
    assert "leader-policy" in codes(raw, tmp_path)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].composition
    config["leaderContentIds"] = []
    fourth = {
        **copy.deepcopy(raw["content"][0]),
        "id": "fixture:leader:3:v1",
        "name": "Leader 3",
        "faceComplete": False,
    }
    raw["content"].append(fourth)
    raw["expectations"][0].update(expectedCopies=4, uniqueDefinitions=4)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].inventory
    config["contentIds"].append(fourth["id"])
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.inventory and not gate.abilities


def test_composed_expansion_does_not_certify_executable_setup(tmp_path, raw):
    _, config = populated_inventory(raw)
    config["expansions"] = ["RiseOfIx"]
    config["initializationTests"] = [
        {
            "path": "training/tests/test_content_coverage.py",
            "name": "fixture",
            "kind": "initialization",
        }
    ]
    config["executionBlockers"] = [
        {
            "id": "no-adapter",
            "scope": "setup",
            "reason": "No executable expansion adapter",
            "open": True,
            "blocking": True,
        }
    ]
    gate = build_report(load(raw, tmp_path)).configurationGates[0]
    assert gate.composition and gate.inventory and gate.abilities and gate.interactions
    assert not gate.initialization and not gate.fullGame and not gate.rl


def test_known_blockers_have_one_primary_batch_and_topological_order(raw):
    batches = {b["id"]: b for b in raw["batches"]}
    known = [
        a
        for c in raw["content"]
        for a in c["abilities"]
        if a["implementation"] != "implemented"
    ]
    assert len(known) == 25
    for a in known:
        assert [b["id"] for b in batches.values() if a["id"] in b["memberIds"]] == [
            a["batchId"]
        ]
    assert len({b["order"] for b in batches.values()}) == len(batches)
    for b in batches.values():
        assert b["title"] and b["priorityRationale"] and b["validationRequirements"]
        assert b["evidence"] and b["acceptance"] and b["expectedDelta"]
        assert all(batches[p]["order"] < b["order"] for p in b["prerequisiteIds"])
    assert batches["batch-01-selected-trash"]["priority"] == "high"
    assert not batches["batch-01-selected-trash"]["prerequisiteIds"]


@pytest.mark.parametrize(
    "mutation", ["cycle", "dangling", "double-primary", "unassigned"]
)
def test_batch_graph_and_primary_membership_reject_invalid_edits(
    tmp_path, raw, mutation
):
    first, second = raw["batches"][:2]
    if mutation == "cycle":
        first["prerequisiteIds"] = [second["id"]]
        second["prerequisiteIds"] = [first["id"]]
        expected = "prerequisite-cycle"
    elif mutation == "dangling":
        first["prerequisiteIds"] = ["batch-does-not-exist"]
        expected = "broken-reference"
    elif mutation == "double-primary":
        ability = next(
            a
            for c in raw["content"]
            for a in c["abilities"]
            if a["batchId"] == first["id"]
        )
        second["memberIds"].append(ability["id"])
        expected = "batch-membership"
    else:
        ability = next(
            a
            for c in raw["content"]
            for a in c["abilities"]
            if a["batchId"] == first["id"]
        )
        first["memberIds"].remove(ability["id"])
        ability["batchId"] = None
        expected = "batch-membership"
    assert expected in codes(raw, tmp_path)


def test_canonical_seat_configurations_keep_composition_and_execution_separate(
    tmp_path, raw
):
    gates = build_report(load(raw, tmp_path)).configurationGates
    assert len(gates) == 6
    assert all(g.recognized and g.composition for g in gates)
    assert all(
        not g.inventory
        and not g.abilities
        and not g.interactions
        and not g.initialization
        and not g.fullGame
        and not g.rl
        for g in gates
    )
    assert {c["seats"] for c in raw["configurations"]} == {3, 4}
    assert all(
        c["leaderPolicy"] == "all-official" and not c["leaderContentIds"]
        for c in raw["configurations"]
    )
    assert all(
        c["compositionTests"] and c["executionBlockers"] for c in raw["configurations"]
    )


def test_unknown_ability_reference_blocks_even_a_claimed_complete_face(tmp_path, raw):
    item, _ = populated_inventory(raw)
    slot = next(s for s in item["slots"] if s["slot"] == "Agent")
    slot.update(discovery="discovered", abilityIds=[item["id"] + "#slot:unknown"])
    assert "broken-reference" in codes(raw, tmp_path)
    assert not build_report(load(raw, tmp_path)).configurationGates[0].abilities


def test_baron_full_face_has_implementation_owner_after_source_gate(raw):
    batches = {b["id"]: b for b in raw["batches"]}
    owner = batches["batch-06c-baron-private-setup"]
    assert "imperium:leader:baron-vladimir-harkonnen:v1" in owner["memberIds"]
    assert "batch-05-leader-sources" in owner["prerequisiteIds"]
    assert owner["id"] in batches["batch-20-authentic-validation"]["prerequisiteIds"]
    assert "Passive/Signet/Setup" in owner["acceptance"]
    assert "Future Passive/Signet ability IDs/counts" in owner["expectedDelta"]
    baron = next(
        c
        for c in raw["content"]
        if c["id"] == "imperium:leader:baron-vladimir-harkonnen:v1"
    )
    assert not baron["faceComplete"]
    assert {s["slot"] for s in baron["slots"] if s["discovery"] == "undiscovered"} >= {
        "Passive",
        "SignetRing",
    }
    assert all(a["batchId"] == owner["id"] for a in baron["abilities"])


def test_bounded_effects_link_existing_direct_behavior_instead_of_duplicate_batch(raw):
    abilities = {a["id"]: a for c in raw["content"] for a in c["abilities"]}
    crossing = "CrossingTwoInfluenceGrantsOneVictoryPoint"
    hall = "HallOfOratoryAddsOnePersuasionOnReveal"
    heighliner = "CompleteSourcedBoardSpacesGrantPrintedEffects"
    mentat = "MentatSpaceAlwaysDrawsForPaidPlacementWhenTokenIsAbsent"
    swordmaster = (
        "SwordmasterAndHighCouncilPersistAfterUsedOrUnusedAgent"
        "AndOnceGameSpacesRemainClosed"
    )
    expected = {
        "imperium:imperium-card:sardaukar-legion:v1#slot:recruit": crossing,
        "imperium:space:wealth:v1#slot:placement": crossing,
        "imperium:space:hall-of-oratory:v1#slot:placement": hall,
        "imperium:space:heighliner:v1#slot:placement": heighliner,
        "imperium:space:mentat:v1#slot:placement": mentat,
        "imperium:space:swordmaster:v1#slot:placement": swordmaster,
    }
    assert all(b["id"] != "batch-19-existing-evidence" for b in raw["batches"])
    for aid, name in expected.items():
        ability = abilities[aid]
        assert ability["implementation"] == "implemented"
        assert ability["validation"] == "tested"
        assert ability["batchId"] is None
        reference = next(t for t in ability["testRefs"] if t["name"] == name)
        assert reference["kind"] == "behavior" and not reference.get(
            "historical", False
        )
        assert name in (ROOT / reference["path"]).read_text()
        # These bounded assertions alone do not supply full readiness evidence.
        assert {t["kind"] for t in ability["testRefs"]} == {"behavior"}


def test_foldspace_reuses_sourced_reserve_return_dependency(raw):
    batches = {b["id"]: b for b in raw["batches"]}
    capabilities = {c["id"]: c for c in raw["capabilities"]}
    foldspace = batches["batch-07-foldspace"]
    routing = batches["batch-01-selected-trash"]
    assert {"batch-01-selected-trash", "batch-04-starter-reserve-sources"} <= set(
        foldspace["prerequisiteIds"]
    )
    assert routing["order"] < foldspace["order"]
    assert "cap-reserve-return" in routing["memberIds"]
    assert "cap-reserve-return" in capabilities["cap-foldspace-face"]["prerequisiteIds"]
    # I16 routes trashed Reserve cards; FAQ1 names Foldspace's self-trash.
    assert any(
        e["sourceId"] == "src-I" and e["page"] == 16 for e in foldspace["evidence"]
    )
    assert any(
        e["sourceId"] == "src-publisher-faq-2025" and e["page"] == 1
        for e in foldspace["evidence"]
    )
    face = next(c for c in raw["content"] if c["id"] == "imperium:reserve:foldspace:v1")
    assert not face["faceComplete"]
    assert all(a["implementation"] == "unsupported" for a in face["abilities"])
