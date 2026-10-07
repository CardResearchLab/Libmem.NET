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


def validate_manifest(manifest: dict, *, version: str, repository: str, platform: str) -> list[str]:
    if manifest.get("packageVersion") != version:
        fail(
            f"{platform} manifest packageVersion mismatch: "
            f"expected {version!r}, got {manifest.get('packageVersion')!r}"
        )
    if manifest.get("repository") != repository:
        fail(
            f"{platform} manifest repository mismatch: "
            f"expected {repository!r}, got {manifest.get('repository')!r}"
        )
    if manifest.get("platform") != platform:
        fail(f"Expected {platform} manifest, got {manifest.get('platform')!r}.")
    if manifest.get("targetFramework") != "net8.0":
        fail(f"Unexpected target framework in {platform}: {manifest.get('targetFramework')!r}.")
    if not manifest.get("repositoryCommit") or manifest.get("repositoryCommit") == "unknown":
        fail(f"{platform} manifest repositoryCommit is unavailable.")
    if not manifest.get("libmemCommit") or manifest.get("libmemCommit") == "unknown":
        fail(f"{platform} manifest libmemCommit is unavailable.")
    files = manifest.get("files")
    if not isinstance(files, list) or not files:
        fail(f"{platform} manifest files must be a non-empty list.")
    packaged = sorted(
        str(entry.get("name"))
        for entry in files
        if isinstance(entry, dict) and entry.get("name")
    )
    packaged.append("manifest.json")
    return packaged


def render_notes(
    *,
    version: str,
    changelog_section: str,
    manifest_x64: dict,
    manifest_x86: dict,
    x64_sha256: str,
    x86_sha256: str,
    repository: str,
    tag: str,
) -> str:
    x64_files = validate_manifest(
        manifest_x64, version=version, repository=repository, platform="win-x64"
    )
    x86_files = validate_manifest(
        manifest_x86, version=version, repository=repository, platform="win-x86"
    )

    if manifest_x64.get("repositoryCommit") != manifest_x86.get("repositoryCommit"):
        fail("x64 and x86 manifests were not built from the same repository commit.")
    if manifest_x64.get("libmemCommit") != manifest_x86.get("libmemCommit"):
        fail("x64 and x86 manifests do not pin the same libmem commit.")

    release_kind = "preview release" if "-" in version else "official release"
    repository_commit = manifest_x64["repositoryCommit"]
    libmem_commit = manifest_x64["libmemCommit"]

    lines = [
        f"Libmem.NET **v{version}** is a Windows x64/x86 / .NET 8 {release_kind} "
        "of the reusable C++/CLI wrapper around the pinned rdbo/libmem native library.",
        "",
        "## Release highlights",
        "",
        changelog_section,
        "",
        "## Platform and compatibility",
        "",
        "- **OS / architectures:** Windows x64 and Windows x86",
        "- **Managed runtime:** .NET 8",
        "- **Native backend:** pinned rdbo/libmem revision",
        "- **AnyCPU:** not supported; consumers must select x64 or x86 explicitly",
        "",
        "## Downloads",
        "",
        "- **Libmem.NET-windows-x64.zip** — runtime package for Windows x64",
        "- **Libmem.NET-windows-x64.zip.sha256** — SHA-256 checksum for the x64 archive",
        "- **Libmem.NET-windows-x86.zip** — runtime package for Windows x86",
        "- **Libmem.NET-windows-x86.zip.sha256** — SHA-256 checksum for the x86 archive",
        f"- **Libmem.NET.{version}.nupkg** — one PackageReference package carrying both architectures; nuget.org publication is a separate step",
        "",
        "## x64 package contents",
        "",
    ]
    lines.extend(f"- `{name}`" for name in x64_files)
    lines.extend(["", "## x86 package contents", ""])
    lines.extend(f"- `{name}`" for name in x86_files)
    lines.extend(
        [
            "",
            "## Integrity and provenance",
            "",
            f"- **x64 archive SHA-256:** `{x64_sha256}`",
            f"- **x86 archive SHA-256:** `{x86_sha256}`",
            f"- **Repository commit:** `{repository_commit}`",
            f"- **Pinned libmem commit:** `{libmem_commit}`",
            "- Both runtime manifests record each packaged file's size and SHA-256.",
            "- Release publication verifies both package versions, repository commit, platform, configuration, archive contents, and checksums before upload.",
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
    parser.add_argument("--manifest-x64", required=True, type=Path)
    parser.add_argument("--checksum-x64", required=True, type=Path)
    parser.add_argument("--manifest-x86", required=True, type=Path)
    parser.add_argument("--checksum-x86", required=True, type=Path)
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

    manifest_x64 = json.loads(args.manifest_x64.read_text(encoding="utf-8"))
    manifest_x86 = json.loads(args.manifest_x86.read_text(encoding="utf-8"))

    x64_sha256 = read_checksum(
        args.checksum_x64, "Libmem.NET-windows-x64.zip"
    )
    x86_sha256 = read_checksum(
        args.checksum_x86, "Libmem.NET-windows-x86.zip"
    )

    notes = render_notes(
        version=version,
        changelog_section=changelog_section,
        manifest_x64=manifest_x64,
        manifest_x86=manifest_x86,
        x64_sha256=x64_sha256,
        x86_sha256=x86_sha256,
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
