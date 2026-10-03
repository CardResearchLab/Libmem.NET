#!/usr/bin/env python3
"""Contract test for the formal GitHub Release notes renderer."""

from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys
import tempfile


root = Path(__file__).resolve().parents[1]
renderer = root / "eng" / "render-release-notes.py"

with tempfile.TemporaryDirectory() as temp:
    temp_dir = Path(temp)
    changelog = temp_dir / "CHANGELOG.md"
    manifest = temp_dir / "manifest.json"
    checksum = temp_dir / "Libmem.NET-windows-x64.zip.sha256"
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
    manifest.write_text(
        json.dumps(
            {
                "schemaVersion": 2,
                "packageVersion": "9.8.7",
                "repository": "HearthstoneModding/Libmem",
                "repositoryCommit": "a" * 40,
                "libmemCommit": "b" * 40,
                "targetFramework": "net8.0",
                "platform": "win-x64",
                "configuration": "Release",
                "files": [
                    {"name": "Libmem.NET.dll", "size": 1, "sha256": "0" * 64},
                    {"name": "libmem.dll", "size": 1, "sha256": "1" * 64},
                ],
            }
        ),
        encoding="utf-8",
    )
    checksum.write_text(
        f"{'c' * 64}  Libmem.NET-windows-x64.zip\n",
        encoding="utf-8",
    )

    subprocess.run(
        [
            sys.executable,
            str(renderer),
            "--version",
            "9.8.7",
            "--changelog",
            str(changelog),
            "--manifest",
            str(manifest),
            "--checksum",
            str(checksum),
            "--repository",
            "HearthstoneModding/Libmem",
            "--tag",
            "v9.8.7",
            "--output",
            str(output),
        ],
        cwd=root,
        check=True,
    )

    notes = output.read_text(encoding="utf-8")
    required = [
        "Libmem.NET **v9.8.7**",
        "## Release highlights",
        "A focused test release.",
        "### Added",
        "Session-bound example capability.",
        "## Platform and compatibility",
        "Windows x64",
        "## Downloads",
        "Libmem.NET-windows-x64.zip",
        "## Package contents",
        "`Libmem.NET.dll`",
        "## Integrity and provenance",
        "c" * 64,
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

print("FORMAL RELEASE NOTES TEST PASS")
