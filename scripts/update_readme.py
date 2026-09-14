#!/usr/bin/env python3
"""Regenerate the auto-generated sections of README.md.

The script refreshes:

* the coverage badge, from the Cobertura report produced by ``dotnet test``;
* the public API table, from the XML doc comments in ``src/MessageFlow``;
* the package badges, from the registries the ports are published to.

Usage: ``python scripts/update_readme.py [--check] [--sections coverage api packages]``

The ``packages`` section needs network access and is therefore not part of the default
sections: it is refreshed by ``.github/workflows/badges.yml`` instead of by the pull request
check, so a publish (or an outage) can never fail an unrelated pull request.
"""

from __future__ import annotations

import argparse
import re
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
README = ROOT / "README.md"
SOURCE_DIR = ROOT / "src" / "MessageFlow"

TYPE_PATTERN = re.compile(
    r"public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*"
    r"(?P<kind>class|interface|record|struct|enum)\s+"
    r"(?P<name>\w+)(?P<generics><[^>(]*>)?"
)

DELEGATE_PATTERN = re.compile(
    r"public\s+delegate\s+[\w<>,\.\?\[\]]+\s+(?P<name>\w+)(?P<generics><[^>(]*>)?\s*\("
)

CREF_PATTERN = re.compile(r"<see\s+cref=\"(?:[A-Za-z]:)?([^\"]+)\"\s*/>")

# Each port publishes to a different registry, so the badge of a port can only be dynamic
# once its package exists: shields.io renders "not found" (or "invalid") otherwise, which is
# what ``probe`` guards against.
PACKAGES = (
    {
        "label": "NuGet",
        "probe": "https://api.nuget.org/v3-flatcontainer/messageflow/index.json",
        "badge": "https://img.shields.io/nuget/v/MessageFlow",
        "link": "https://www.nuget.org/packages/MessageFlow",
    },
    {
        "label": "Maven Central",
        "probe": (
            "https://repo1.maven.org/maven2/io/github/charles2ke/messageflow/maven-metadata.xml"
        ),
        "badge": "https://img.shields.io/maven-central/v/io.github.charles2ke/messageflow",
        "link": "https://central.sonatype.com/artifact/io.github.charles2ke/messageflow",
    },
    {
        "label": "PyPI",
        "probe": "https://pypi.org/pypi/messageflow/json",
        "badge": "https://img.shields.io/pypi/v/messageflow",
        "link": "https://pypi.org/project/messageflow/",
    },
    {
        "label": "npm",
        "probe": "https://registry.npmjs.org/@charles2ke%2Fmessageflow",
        "badge": "https://img.shields.io/npm/v/@charles2ke/messageflow",
        "link": "https://www.npmjs.com/package/@charles2ke/messageflow",
    },
)

LICENSE_BADGE = (
    "[![License](https://img.shields.io/badge/license-Apache--2.0-blue)](LICENSE)"
)


def find_coverage_report() -> Path | None:
    """Return the most recently written Cobertura report, if any."""
    candidates = sorted(
        ROOT.glob("**/coverage*.cobertura.xml"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    return candidates[0] if candidates else None


def coverage_section() -> str:
    """Build the coverage badge section."""
    report = find_coverage_report()
    if report is None:
        return "Coverage report not found. Run `dotnet test -p:CollectCoverage=true` first."

    root = ET.parse(report).getroot()
    line_rate = round(float(root.get("line-rate", "0")) * 100)
    branch_rate = round(float(root.get("branch-rate", "0")) * 100)
    color = "brightgreen" if min(line_rate, branch_rate) == 100 else "orange"
    return (
        f"![line coverage](https://img.shields.io/badge/line%20coverage-{line_rate}%25-{color})\n"
        f"![branch coverage](https://img.shields.io/badge/branch%20coverage-{branch_rate}%25-{color})"
    )


def is_published(url: str) -> bool:
    """Return whether the registry knows the package behind ``url``."""
    request = urllib.request.Request(url, headers={"User-Agent": "messageflow-readme"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status == 200
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return False
        raise


def packages_section(current: str) -> str:
    """Build the package badge section, keeping ``current`` when a registry is unreachable."""
    badges = []
    for package in PACKAGES:
        try:
            published = is_published(package["probe"])
        except (urllib.error.URLError, OSError) as error:  # pragma: no cover - network
            print(f"Could not query {package['label']} ({error}); badges left unchanged.")
            return current

        if published:
            badge, link = package["badge"], package["link"]
        else:
            label = package["label"].replace(" ", "%20")
            badge = f"https://img.shields.io/badge/{label}-unreleased-lightgrey"
            link = "#releases-and-versioning"
        badges.append(f"[![{package['label']}]({badge})]({link})")

    badges.append(LICENSE_BADGE)
    return "\n".join(badges)


def summary_for(lines: list[str], index: int) -> str:
    """Return the ``<summary>`` text of the doc comment above ``index``."""
    doc: list[str] = []
    cursor = index - 1
    while cursor >= 0:
        stripped = lines[cursor].strip()
        if stripped.startswith("///"):
            doc.insert(0, stripped)
        elif not stripped.startswith("["):
            break
        cursor -= 1

    text = " ".join(line.lstrip("/").strip() for line in doc)
    match = re.search(r"<summary>(.*?)</summary>", text, re.DOTALL)
    if not match:
        return ""
    summary = CREF_PATTERN.sub(r"\1", match.group(1))
    summary = summary.replace("{", "<").replace("}", ">")
    return " ".join(summary.split())


def api_section() -> str:
    """Build the public API table."""
    rows: list[tuple[str, str, str]] = []
    for path in sorted(SOURCE_DIR.glob("*.cs")):
        lines = path.read_text(encoding="utf-8").splitlines()
        for index, line in enumerate(lines):
            stripped = line.strip()
            if not stripped.startswith("public "):
                continue

            match = DELEGATE_PATTERN.match(stripped)
            kind = "delegate"
            if match is None:
                match = TYPE_PATTERN.match(stripped)
                if match is None:
                    continue
                kind = match.group("kind")

            name = match.group("name") + (match.group("generics") or "")
            rows.append((f"`{name}`", kind, summary_for(lines, index)))

    header = "| Type | Kind | Description |\n| --- | --- | --- |"
    body = "\n".join(
        f"| {name} | {kind} | {summary} |".replace("<", "&lt;").replace(">", "&gt;")
        for name, kind, summary in rows
    )
    return f"{header}\n{body}"


def read_section(content: str, marker: str) -> str:
    """Return the current content between the begin/end markers of ``marker``."""
    begin = f"<!-- BEGIN AUTO-GENERATED: {marker} -->"
    end = f"<!-- END AUTO-GENERATED: {marker} -->"
    match = re.search(f"{re.escape(begin)}\\n(.*?)\\n{re.escape(end)}", content, re.DOTALL)
    if match is None:
        raise SystemExit(f"Marker '{marker}' not found in README.md")
    return match.group(1)


def replace_section(content: str, marker: str, section: str) -> str:
    """Replace the content between the begin/end markers of ``marker``."""
    begin = f"<!-- BEGIN AUTO-GENERATED: {marker} -->"
    end = f"<!-- END AUTO-GENERATED: {marker} -->"
    pattern = re.compile(f"{re.escape(begin)}.*?{re.escape(end)}", re.DOTALL)
    if not pattern.search(content):
        raise SystemExit(f"Marker '{marker}' not found in README.md")
    return pattern.sub(f"{begin}\n{section}\n{end}", content)


def main() -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail instead of writing when README.md is out of date",
    )
    parser.add_argument(
        "--sections",
        nargs="+",
        choices=["coverage", "api", "packages"],
        default=["coverage", "api"],
        help="sections to regenerate (default: coverage api)",
    )
    args = parser.parse_args()

    original = README.read_text(encoding="utf-8")
    updated = original
    if "coverage" in args.sections:
        updated = replace_section(updated, "coverage", coverage_section())
    if "api" in args.sections:
        updated = replace_section(updated, "api", api_section())
    if "packages" in args.sections:
        updated = replace_section(
            updated, "packages", packages_section(read_section(updated, "packages"))
        )

    if updated == original:
        print("README.md is up to date.")
        return 0

    if args.check:
        print("README.md is out of date. Run: python scripts/update_readme.py")
        return 1

    README.write_text(updated, encoding="utf-8")
    print("README.md updated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
