#!/usr/bin/env python3
"""Validate the local x64 NuGet prototype package layout and provenance."""

from __future__ import annotations

import argparse
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def find_child(parent: ET.Element, name: str) -> ET.Element | None:
    for child in parent:
        if local_name(child.tag) == name:
            return child
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-commit", required=True)
    args = parser.parse_args()

    if not args.package.is_file():
        raise FileNotFoundError(args.package)

    with zipfile.ZipFile(args.package) as archive:
        names = set(archive.namelist())

        required = {
            "lib/net8.0/Libmem.NET.dll",
            "lib/net8.0/Libmem.NET.xml",
            "runtimes/win-x64/native/libmem.dll",
            "runtimes/win-x64/native/Ijwhost.dll",
            "buildTransitive/Libmem.NET.targets",
            "README.md",
            "LICENSE",
            "THIRD_PARTY_NOTICES.md",
            "CHANGELOG.md",
            "MIGRATION.md",
        }
        missing = sorted(required - names)
        if missing:
            raise AssertionError("NuGet package is missing: " + ", ".join(missing))

        forbidden_fragments = [
            "win-x86",
            "/x86/",
            "Libmem.NET.pdb",
        ]
        for name in names:
            for fragment in forbidden_fragments:
                if fragment.lower() in name.lower():
                    raise AssertionError(
                        f"Unexpected x86/debug asset in NuGet package: {name}"
                    )

        nuspec_names = [name for name in names if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise AssertionError(
                f"Expected exactly one .nuspec, found {len(nuspec_names)}"
            )

        root = ET.fromstring(archive.read(nuspec_names[0]))
        metadata = find_child(root, "metadata")
        if metadata is None:
            raise AssertionError("NuGet .nuspec has no metadata element.")

        package_id = find_child(metadata, "id")
        version = find_child(metadata, "version")
        authors = find_child(metadata, "authors")
        description = find_child(metadata, "description")
        license_element = find_child(metadata, "license")
        readme = find_child(metadata, "readme")
        project_url = find_child(metadata, "projectUrl")
        repository = find_child(metadata, "repository")

        if package_id is None or package_id.text != "Libmem.NET":
            raise AssertionError("Unexpected NuGet package ID.")

        if version is None or version.text != args.expected_version:
            raise AssertionError(
                f"NuGet version mismatch: expected {args.expected_version!r}, "
                f"got {None if version is None else version.text!r}"
            )

        if authors is None or authors.text != "xiaohei7972":
            raise AssertionError("Unexpected NuGet authors metadata.")

        if description is None or "Windows x64" not in (description.text or ""):
            raise AssertionError("NuGet description does not declare the Windows x64 scope.")

        if (
            license_element is None
            or license_element.attrib.get("type") != "file"
            or license_element.text != "LICENSE"
        ):
            raise AssertionError("NuGet license file metadata mismatch.")

        if readme is None or readme.text != "README.md":
            raise AssertionError("NuGet readme metadata mismatch.")

        if (
            project_url is None
            or project_url.text != "https://github.com/QuietForge/Libmem.NET"
        ):
            raise AssertionError("NuGet project URL mismatch.")

        if repository is None:
            raise AssertionError("NuGet package has no repository metadata.")

        if repository.attrib.get("type") != "git":
            raise AssertionError("NuGet repository type is not git.")

        if repository.attrib.get("url") != "https://github.com/QuietForge/Libmem.NET":
            raise AssertionError("NuGet repository URL mismatch.")

        if repository.attrib.get("commit") != args.expected_commit:
            raise AssertionError(
                f"NuGet repository commit mismatch: expected {args.expected_commit!r}, "
                f"got {repository.attrib.get('commit')!r}"
            )

    print("NUGET PACKAGE LAYOUT AND PROVENANCE PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
