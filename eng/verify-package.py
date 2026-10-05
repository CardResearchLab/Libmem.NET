#!/usr/bin/env python3
"""Validate a Libmem.NET runtime package, archive, checksum, and provenance."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import sys
import zipfile


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fail(message: str) -> None:
    raise ValueError(message)


def safe_package_name(name: str) -> str:
    posix = PurePosixPath(name.replace("\\", "/"))
    if posix.is_absolute() or ".." in posix.parts or len(posix.parts) != 1:
        fail(f"Manifest contains an unsafe package file name: {name!r}")
    return posix.name


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--package-dir", required=True, type=Path)
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--checksum", required=True, type=Path)
    parser.add_argument("--expected-version")
    parser.add_argument("--expected-repository-commit")
    parser.add_argument("--expected-platform")
    parser.add_argument("--expected-configuration")
    args = parser.parse_args()

    package_dir = args.package_dir.resolve()
    archive = args.archive.resolve()
    checksum = args.checksum.resolve()
    manifest_path = package_dir / "manifest.json"

    for path, label in (
        (package_dir, "package directory"),
        (manifest_path, "manifest"),
        (archive, "archive"),
        (checksum, "archive checksum"),
    ):
        if not path.exists():
            fail(f"Missing {label}: {path}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 2:
        fail(f"Unsupported manifest schemaVersion: {manifest.get('schemaVersion')!r}")
    if manifest.get("repository") != "CardResearchLab/Libmem.NET":
        fail(f"Unexpected repository: {manifest.get('repository')!r}")
    if manifest.get("targetFramework") != "net8.0":
        fail(f"Unexpected targetFramework: {manifest.get('targetFramework')!r}")

    expected_fields = {
        "packageVersion": args.expected_version,
        "repositoryCommit": args.expected_repository_commit,
        "platform": args.expected_platform,
        "configuration": args.expected_configuration,
    }
    for key, expected in expected_fields.items():
        if expected is not None and manifest.get(key) != expected:
            fail(f"{key} mismatch: expected {expected!r}, got {manifest.get(key)!r}")

    if manifest.get("repositoryCommit") in (None, "", "unknown"):
        fail("Manifest repositoryCommit is unavailable.")
    if manifest.get("libmemCommit") in (None, "", "unknown"):
        fail("Manifest libmemCommit is unavailable.")

    entries = manifest.get("files")
    if not isinstance(entries, list) or not entries:
        fail("Manifest files must be a non-empty array.")

    expected_names: set[str] = set()
    entry_by_name: dict[str, dict] = {}
    for entry in entries:
        if not isinstance(entry, dict):
            fail("Every manifest files entry must be an object.")
        name = safe_package_name(str(entry.get("name", "")))
        if not name or name in expected_names:
            fail(f"Duplicate or empty manifest file name: {name!r}")
        size = entry.get("size")
        digest = str(entry.get("sha256", "")).lower()
        if not isinstance(size, int) or size < 0:
            fail(f"Invalid size for {name!r}: {size!r}")
        if len(digest) != 64 or any(ch not in "0123456789abcdef" for ch in digest):
            fail(f"Invalid SHA-256 for {name!r}: {digest!r}")

        expected_names.add(name)
        entry_by_name[name] = entry
        path = package_dir / name
        if not path.is_file():
            fail(f"Manifest file is missing from package directory: {name}")
        if path.stat().st_size != size:
            fail(f"Size mismatch for {name}: expected {size}, got {path.stat().st_size}")
        actual = sha256_file(path)
        if actual != digest:
            fail(f"SHA-256 mismatch for {name}: expected {digest}, got {actual}")

    actual_names = {p.name for p in package_dir.iterdir() if p.is_file() and p.name != "manifest.json"}
    if actual_names != expected_names:
        fail(
            "Package directory file set does not match manifest: "
            f"missing={sorted(expected_names - actual_names)}, "
            f"extra={sorted(actual_names - expected_names)}"
        )

    with zipfile.ZipFile(archive, "r") as zf:
        members = [name for name in zf.namelist() if not name.endswith("/")]
        archive_names = {safe_package_name(name) for name in members}
        expected_archive_names = expected_names | {"manifest.json"}
        if archive_names != expected_archive_names or len(members) != len(archive_names):
            fail(
                "Archive file set does not match package directory: "
                f"expected={sorted(expected_archive_names)}, actual={sorted(archive_names)}"
            )

        archived_manifest = zf.read("manifest.json")
        if archived_manifest != manifest_path.read_bytes():
            fail("Archived manifest.json differs from the package directory manifest.")

        for name, entry in entry_by_name.items():
            data = zf.read(name)
            if len(data) != entry["size"]:
                fail(f"Archived size mismatch for {name}.")
            if sha256_bytes(data) != entry["sha256"]:
                fail(f"Archived SHA-256 mismatch for {name}.")

    checksum_text = checksum.read_text(encoding="utf-8").strip()
    parts = checksum_text.split()
    if len(parts) != 2:
        fail("Archive checksum file must contain '<sha256>  <filename>'.")
    expected_archive_hash, expected_archive_name = parts
    if expected_archive_name != archive.name:
        fail(
            f"Archive checksum names {expected_archive_name!r}, "
            f"but archive is {archive.name!r}."
        )
    actual_archive_hash = sha256_file(archive)
    if expected_archive_hash.lower() != actual_archive_hash:
        fail(
            f"Archive SHA-256 mismatch: expected {expected_archive_hash.lower()}, "
            f"got {actual_archive_hash}."
        )

    print(
        "PACKAGE VERIFY PASS "
        f"version={manifest['packageVersion']} "
        f"platform={manifest['platform']} "
        f"commit={manifest['repositoryCommit']} "
        f"files={len(entries)}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError, zipfile.BadZipFile) as exc:
        print(f"PACKAGE VERIFY FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
