#!/usr/bin/env python3
"""Contract test for the formal GitHub Release notes renderer."""

from __future__ import annotations

import json
import runpy
from pathlib import Path
import subprocess
import sys
import tempfile


root = Path(__file__).resolve().parents[1]
renderer = root / "eng" / "render-release-notes.py"


def manifest_payload(platform: str, version: str = "9.8.7") -> dict:
    return {
        "schemaVersion": 2,
        "packageVersion": version,
        "repository": "CardResearchLab/Libmem.NET",
        "repositoryCommit": "a" * 40,
        "libmemCommit": "b" * 40,
        "targetFramework": "net8.0",
        "platform": platform,
        "configuration": "Release",
        "files": [
            {"name": "Libmem.NET.dll", "size": 1, "sha256": "0" * 64},
            {"name": "libmem.dll", "size": 1, "sha256": "1" * 64},
        ],
    }


with tempfile.TemporaryDirectory() as temp:
    temp_dir = Path(temp)
    changelog = temp_dir / "CHANGELOG.md"
    manifest_x64 = temp_dir / "manifest-x64.json"
    manifest_x86 = temp_dir / "manifest-x86.json"
    checksum_x64 = temp_dir / "Libmem.NET-windows-x64.zip.sha256"
    checksum_x86 = temp_dir / "Libmem.NET-windows-x86.zip.sha256"
    output = temp_dir / "release-notes.md"

    changelog.write_text(
        """# Changelog

## Unreleased

- Work in progress.

## 9.8.7 - 2026-09-30

A focused test release.

### Added

- Session-bound example capability.

### Changed

- Improved lifecycle behavior.

## 9.8.6

Previous release.
""",
        encoding="utf-8",
    )
    manifest_x64.write_text(json.dumps(manifest_payload("win-x64")), encoding="utf-8")
    manifest_x86.write_text(json.dumps(manifest_payload("win-x86")), encoding="utf-8")
    checksum_x64.write_text(
        f"{'c' * 64}  Libmem.NET-windows-x64.zip\n",
        encoding="utf-8",
    )
    checksum_x86.write_text(
        f"{'d' * 64}  Libmem.NET-windows-x86.zip\n",
        encoding="utf-8",
    )

    def command(version: str) -> list[str]:
        return [
            sys.executable,
            str(renderer),
            "--version",
            version,
            "--changelog",
            str(changelog),
            "--manifest-x64",
            str(manifest_x64),
            "--checksum-x64",
            str(checksum_x64),
            "--manifest-x86",
            str(manifest_x86),
            "--checksum-x86",
            str(checksum_x86),
            "--repository",
            "CardResearchLab/Libmem.NET",
            "--tag",
            "v" + version,
            "--output",
            str(output),
        ]

    subprocess.run(command("9.8.7"), cwd=root, check=True)

    notes = output.read_text(encoding="utf-8")
    required = [
        "Libmem.NET **v9.8.7**",
        "## Release highlights",
        "A focused test release.",
        "### Added",
        "Session-bound example capability.",
        "## Platform and compatibility",
        "Windows x64 and Windows x86",
        "## Downloads",
        "Libmem.NET-windows-x64.zip",
        "Libmem.NET-windows-x86.zip",
        "## x64 package contents",
        "## x86 package contents",
        "`Libmem.NET.dll`",
        "## Integrity and provenance",
        "c" * 64,
        "d" * 64,
        "## Documentation",
    ]
    for marker in required:
        assert marker in notes, f"Missing release-notes marker: {marker}"

    forbidden = [
        "What's Changed",
        "/pull/",
        "New Contributors",
        "--generate-notes",
    ]
    for marker in forbidden:
        assert marker not in notes, f"Generated notes contain PR-feed marker: {marker}"

    preview = "2.0.0-preview.1"
    changelog.write_text(changelog.read_text().replace("9.8.7", preview), encoding="utf-8")
    x64_metadata = manifest_payload("win-x64", preview)
    x86_metadata = manifest_payload("win-x86", preview)
    manifest_x64.write_text(json.dumps(x64_metadata), encoding="utf-8")
    manifest_x86.write_text(json.dumps(x86_metadata), encoding="utf-8")

    preview_command = command(preview)
    subprocess.run(preview_command, cwd=root, check=True)
    notes = output.read_text(encoding="utf-8")
    assert "preview release" in notes
    assert f"Libmem.NET.{preview}.nupkg" in notes
    assert f"/blob/v{preview}/docs/MIGRATION.md" in notes
    assert "official release" not in notes

    x86_metadata["packageVersion"] = "2.0.0"
    manifest_x86.write_text(json.dumps(x86_metadata), encoding="utf-8")
    result = subprocess.run(preview_command, cwd=root, capture_output=True, text=True)
    assert result.returncode != 0 and "win-x86 manifest packageVersion mismatch" in result.stderr

normalize = runpy.run_path(str(renderer))["normalize_version"]
for valid in ["1.0.0", "v2.0.0-preview.1", "2.0.0-rc.2", "2.0.0-0"]:
    assert normalize(valid) == valid.removeprefix("v")
for invalid in ["2.0", "02.0.0", "2.0.0-preview.01", "2.0.0-preview..1", "2.0.0-", "2.0.0+build"]:
    try:
        normalize(invalid)
    except ValueError:
        pass
    else:
        raise AssertionError(f"Accepted unsupported/invalid release version: {invalid}")

print("FORMAL RELEASE NOTES TEST PASS")
