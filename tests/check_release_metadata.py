"""Validate candidate version alignment without claiming that release publication happened."""

from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
version = (root / "VERSION").read_text(encoding="utf-8").strip()
assert re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?", version), (
    f"VERSION is not a semantic version: {version!r}"
)

changelog = (root / "CHANGELOG.md").read_text(encoding="utf-8")
assert re.search(rf"^##\s+{re.escape(version)}(?:\s+-\s+[^\n]+)?\s*$", changelog, re.MULTILINE), (
    f"CHANGELOG has no renderable heading for VERSION={version}"
)

package = ET.parse(root / "packaging/Libmem.NET.csproj").getroot()
release_notes = package.findtext("./PropertyGroup/PackageReleaseNotes")
assert release_notes and f"Libmem.NET {version}" in release_notes, (
    "NuGet PackageReleaseNotes does not describe the candidate VERSION"
)
assert f"docs/releases/v{version}.md" in release_notes, (
    "NuGet PackageReleaseNotes must link the matching candidate release notes"
)

readme = (root / "packaging/NUGET_README.md").read_text(encoding="utf-8")
assert readme.count(f"dotnet add package Libmem.NET --version {version}") == 2, (
    "Both language sections of the packaged NuGet README must use candidate VERSION"
)

candidate_notes = root / "docs/releases" / f"v{version}.md"
assert candidate_notes.is_file(), "Candidate release notes file is missing"
notes = candidate_notes.read_text(encoding="utf-8")
assert f"Libmem.NET {version}" in notes
assert "nuget.org" in notes, (
    "Release notes must describe the separate NuGet distribution channel"
)
# Do not make the test depend on the current publication state: candidate
# notes may legitimately be promoted to official notes after public release.

workflow = (root / ".github/workflows/release.yml").read_text(encoding="utf-8")
assert "publish-nuget:" in workflow
assert "if: github.ref_type == 'tag' && inputs.publish-nuget != true" in workflow
assert "NuGet/login@v1" in workflow

print(f"PASS release metadata: VERSION={version}, CHANGELOG, NuGet, bilingual README, notes and publication guards")
