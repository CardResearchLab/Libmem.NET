#!/usr/bin/env python3
"""Render user-facing GitHub Release notes from CHANGELOG and package metadata."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys


def fail(message: str) -> None:
    raise ValueError(message)


def normalize_version(value: str) -> str:
    version = value.strip()
    if version.startswith("v"):
        version = version[1:]
    if not re.fullmatch(
        r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
        r"(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)"
        r"(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?", version
    ):
        fail(f"Version must use MAJOR.MINOR.PATCH[-PRERELEASE] format: {value!r}")
    return version


def extract_changelog_section(text: str, version: str) -> str:
    heading = re.compile(
        rf"^##\s+{re.escape(version)}(?:\s+-\s+[^\n]+)?\s*$",
        flags=re.MULTILINE,
    )
    match = heading.search(text)
    if match is None:
        fail(
            f"CHANGELOG does not contain a release section for {version}. "
            "Move the intended release notes out of Unreleased before publishing."
        )

    next_heading = re.search(r"^##\s+", text[match.end():], flags=re.MULTILINE)
    end = match.end() + next_heading.start() if next_heading else len(text)
    section = text[match.end():end].strip()
    if not section:
        fail(f"CHANGELOG section for {version} is empty.")
    return section


def read_checksum(path: Path, archive_name: str) -> str:
    parts = path.read_text(encoding="utf-8").strip().split()
    if len(parts) != 2:
        fail("Checksum file must contain '<sha256>  <filename>'.")
    digest, filename = parts
    digest = digest.lower()
    if filename != archive_name:
        fail(
            f"Checksum names {filename!r}, but the release archive is {archive_name!r}."
        )
    if len(digest) != 64 or any(ch not in "0123456789abcdef" for ch in digest):
        fail(f"Invalid SHA-256 digest: {digest!r}")
    return digest


def render_notes(
    *,
    version: str,
    changelog_section: str,
    manifest: dict,
    archive_sha256: str,
    repository: str,
    tag: str,
) -> str:
    platform = manifest.get("platform")
    framework = manifest.get("targetFramework")
    repository_commit = manifest.get("repositoryCommit")
    libmem_commit = manifest.get("libmemCommit")
    files = manifest.get("files")

    if platform != "win-x64":
        fail(f"Formal release notes currently require win-x64; got {platform!r}.")
    if framework != "net8.0":
        fail(f"Unexpected target framework: {framework!r}.")
    if not repository_commit or repository_commit == "unknown":
        fail("Manifest repositoryCommit is unavailable.")
    if not libmem_commit or libmem_commit == "unknown":
        fail("Manifest libmemCommit is unavailable.")
    if not isinstance(files, list) or not files:
        fail("Manifest files must be a non-empty list.")

    package_name = "Libmem.NET-windows-x64.zip"
    checksum_name = package_name + ".sha256"

    packaged_files = sorted(
        str(entry.get("name"))
        for entry in files
        if isinstance(entry, dict) and entry.get("name")
    )
    packaged_files.append("manifest.json")

    release_kind = "preview release" if "-" in version else "official release"
    lines = [
        f"Libmem.NET **v{version}** is a Windows x64 / .NET 8 {release_kind} "
        "of the reusable C++/CLI wrapper around the pinned rdbo/libmem native library.",
        "",
        "## Release highlights",
        "",
        changelog_section,
        "",
        "## Platform and compatibility",
        "",
        "- **OS / architecture:** Windows x64",
        "- **Managed runtime:** .NET 8",
        "- **Native backend:** pinned rdbo/libmem revision",
        "- **x86:** retained only as deferred/best-effort source compatibility; no x86 asset is published by this release",
        "",
        "## Downloads",
        "",
        f"- **{package_name}** — runtime package for Windows x64",
        f"- **{checksum_name}** — SHA-256 checksum for the runtime archive",
        f"- **Libmem.NET.{version}.nupkg** — exact-version package for a local NuGet feed; nuget.org publication is a separate step",
        "",
        "## Package contents",
        "",
    ]
    lines.extend(f"- `{name}`" for name in packaged_files)
    lines.extend(
        [
            "",
            "## Integrity and provenance",
            "",
            f"- **Archive SHA-256:** `{archive_sha256}`",
            f"- **Repository commit:** `{repository_commit}`",
            f"- **Pinned libmem commit:** `{libmem_commit}`",
            "- The runtime manifest records each packaged file's size and SHA-256.",
            "- Release publication verifies the package version, repository commit, platform, configuration, archive contents, and checksum before upload.",
            "",
            "## Documentation",
            "",
            f"- [README](https://github.com/{repository}/blob/{tag}/README.md)",
            f"- [English README](https://github.com/{repository}/blob/{tag}/README.en.md)",
            f"- [CHANGELOG](https://github.com/{repository}/blob/{tag}/CHANGELOG.md)",
            f"- [Migration guide](https://github.com/{repository}/blob/{tag}/docs/MIGRATION.md)",
            f"- [Roadmap](https://github.com/{repository}/blob/{tag}/ROADMAP.md)",
            "",
            "> Libmem.NET remains a general-purpose libmem wrapper. Application snapshots, caches, game state, IPC, and other product-specific models belong in consuming projects.",
        ]
    )
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True)
    parser.add_argument("--changelog", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--checksum", required=True, type=Path)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    version = normalize_version(args.version)
    tag = args.tag.strip()
    if tag != f"v{version}":
        fail(f"Tag/version mismatch: tag={tag!r}, version={version!r}")

    changelog_text = args.changelog.read_text(encoding="utf-8")
    changelog_section = extract_changelog_section(changelog_text, version)

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    if manifest.get("packageVersion") != version:
        fail(
            "Manifest packageVersion mismatch: "
            f"expected {version!r}, got {manifest.get('packageVersion')!r}"
        )
    if manifest.get("repository") != args.repository:
        fail(
            "Manifest repository mismatch: "
            f"expected {args.repository!r}, got {manifest.get('repository')!r}"
        )

    archive_sha256 = read_checksum(
        args.checksum, "Libmem.NET-windows-x64.zip"
    )
    notes = render_notes(
        version=version,
        changelog_section=changelog_section,
        manifest=manifest,
        archive_sha256=archive_sha256,
        repository=args.repository,
        tag=tag,
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(notes, encoding="utf-8")
    print(f"Release notes: {args.output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"RELEASE NOTES FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
