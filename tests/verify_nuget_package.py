#!/usr/bin/env python3
"""Validate the local x64 NuGet prototype package layout."""

from __future__ import annotations

import argparse
from pathlib import Path
import zipfile


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True, type=Path)
    args = parser.parse_args()

    if not args.package.is_file():
        raise FileNotFoundError(args.package)

    with zipfile.ZipFile(args.package) as archive:
        names = set(archive.namelist())

    required = {
        "lib/net8.0/LibmemCli.dll",
        "lib/net8.0/LibmemCli.xml",
        "runtimes/win-x64/native/libmem.dll",
        "runtimes/win-x64/native/Ijwhost.dll",
        "buildTransitive/HearthstoneModding.LibmemCli.targets",
        "README.md",
    }
    missing = sorted(required - names)
    if missing:
        raise AssertionError("NuGet package is missing: " + ", ".join(missing))

    forbidden_fragments = [
        "win-x86",
        "/x86/",
        "LibmemCli.pdb",
    ]
    for name in names:
        for fragment in forbidden_fragments:
            if fragment.lower() in name.lower():
                raise AssertionError(f"Unexpected x86/debug asset in NuGet package: {name}")

    print("NUGET PACKAGE LAYOUT PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
