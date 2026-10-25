"""Offline integrity command for the authored content coverage inventory."""

from __future__ import annotations

import argparse
from pathlib import Path

if __package__:
    from .content_coverage_model import (
        build_report,
        load_inventory,
        validate_inventory,
    )
else:
    from content_coverage_model import build_report, load_inventory, validate_inventory

__all__ = ["build_report", "load_inventory", "validate_inventory", "main"]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate",))
    parser.add_argument(
        "--inventory", type=Path, default=Path("docs/content/coverage/inventory.json")
    )
    args = parser.parse_args(argv)
    try:
        inventory = load_inventory(args.inventory)
    except (OSError, ValueError) as error:
        print(f"error: {error}")
        return 1
    issues = validate_inventory(inventory, Path.cwd())
    for issue in issues:
        print(
            f"{issue.severity}: {issue.code}: {issue.recordId}: "
            f"{issue.path}: {issue.message}"
        )
    failed = any(issue.severity == "error" for issue in issues)
    if not failed:
        print(f"valid: {args.inventory}")
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
