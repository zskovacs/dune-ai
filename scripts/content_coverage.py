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
    from .content_coverage_reports import render_reports, update_region
else:
    from content_coverage_model import build_report, load_inventory, validate_inventory
    from content_coverage_reports import render_reports, update_region

__all__ = [
    "build_report",
    "load_inventory",
    "validate_inventory",
    "render_reports",
    "main",
]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("validate", "report"))
    parser.add_argument(
        "--check", action="store_true", help="check report freshness without writing"
    )
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
    if failed:
        return 1
    if args.command == "report":
        stale = []
        for path, body in render_reports(build_report(inventory), inventory).items():
            if path.exists():
                try:
                    updated = update_region(path.read_text(encoding="utf-8"), body)
                except ValueError as error:
                    print(f"error: {path}: {error}")
                    return 1
            else:
                if args.check:
                    stale.append(path)
                    continue
                title = path.stem.replace("-", " ").title()
                updated = (
                    f"# {title}\n\n"
                    f"<!-- BEGIN GENERATED CONTENT COVERAGE -->\n{body}"
                    "<!-- END GENERATED CONTENT COVERAGE -->\n"
                )
            if not path.exists() or path.read_text(encoding="utf-8") != updated:
                if args.check:
                    stale.append(path)
                else:
                    path.write_text(updated, encoding="utf-8")
        if stale:
            for path in stale:
                print(f"stale: {path}")
            return 1
        print("reports current" if args.check else "reports written")
    else:
        print(f"valid: {args.inventory}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
