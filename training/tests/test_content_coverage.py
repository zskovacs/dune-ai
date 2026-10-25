"""Focused mutation checks for the offline content audit contract."""

from __future__ import annotations

import copy
import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from content_coverage_model import build_report, load_inventory, validate_inventory  # noqa: E402


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
            {"sourceId": "src-I", "page": 3, "locator": "component", "kind": "quantity"}
        ],
        "discovery": "discovered",
        "sourcing": "fully-sourced",
        "verification": "verified",
        "faceComplete": False,
        "slots": [
            {"slot": "Agent", "discovery": "unreadable", "reason": "face not inspected"}
        ],
        "abilities": [],
    }


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


def test_canonical_s1_is_valid_but_incomplete(tmp_path, raw):
    inventory = load(raw, tmp_path)
    assert not [i for i in validate_inventory(inventory, ROOT) if i.severity == "error"]
    report = build_report(inventory)
    assert report.sourceCount == 12
    assert report.expectationCount == 80
    assert len(report.gaps) == 80
    assert all(not q.complete for q in report.quantities)


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
    assert build_report(load(raw, tmp_path)).unknownSlots == 1


def test_optional_pdf_unavailable_offline_without_freshness(tmp_path, raw, monkeypatch):
    root = tmp_path / "empty-root"
    root.mkdir()

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
            "mode": "standard",
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
