# Libmem.NET Releases

This document describes the official release channel, current stable release, support boundaries, integrity model, and versioning policy for Libmem.NET.

## Current source and historical release

The published stable line on GitHub and nuget.org is `2.1.1`, with numeric assembly/file versions `2.1.1.0`. `2.1.0` is the previous stable release.

2.1.1 preserves the 2.1.0 Public API and pinned native dependency. It fixes the missing bad-address-sentinel guard on `VmtManager` construction and the untracked `VmtManager.Unhook` page-protection issue, strengthens regression coverage, and advances public-NuGet smoke coverage to the published 2.1.1 baseline.

The historical `LibmemCli` → `Libmem.NET` identity migration remains documented in [MIGRATION.md](MIGRATION.md). Existing historical tags and assets remain immutable.

### Historical v1.0.0

**LibmemCli v1.0.0** is the first stable Windows x64 release.

- Release: https://github.com/CardResearchLab/Libmem.NET/releases/tag/v1.0.0
- Runtime package: https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip
- SHA-256 file: https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256
- Release commit: `e6181b9f74b5d5877e3d1c253d3bbfef61141445`
- Pinned libmem commit: `a07c9942bf1358dabcc83eb0cd072736c749d7f8`
- Runtime ZIP SHA-256: `647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99`

## Support boundary

The supported target remains:

- Windows x64;
- .NET 8;
- C# / .NET consumers using the C++/CLI wrapper;
- the pinned rdbo/libmem native revision recorded by the release;
- Runtime ZIP distribution and source/submodule integration.

The current target does **not** promise:

- official x86 release assets;
- AnyCPU compatibility;
- cross-bitness injection;
- public NuGet availability before release setup and publication are verified;
- game-specific state, Snapshot, Entity, GameState, IPC, Unity, Mono, or Hearthstone business logic.

Those application-level concerns remain the responsibility of consuming projects.

## Public contract

The API baseline and behavior tests enforce:

- namespace, public type, member, overload, and enum shape;
- ProcessSession and Manager responsibilities;
- read-only result-model semantics;
- ownership and deterministic Dispose behavior;
- null / sentinel / exception distinctions;
- target-process identity semantics;
- Windows x64 packaging layout.

The committed `api/Libmem.NET.PublicApi.txt` baseline is validated by CI. Intentional incompatible changes must be explicit, documented, reviewed, and versioned appropriately. The identity migration preserves member signatures and behavior while intentionally changing namespace, assembly, and file names.

## Current package contents

The Windows x64 runtime package contains:

```text
Libmem.NET.dll
Libmem.NET.xml
Ijwhost.dll
libmem.dll
VERSION
LICENSE
THIRD_PARTY_NOTICES.md
CHANGELOG.md
MIGRATION.md
manifest.json
```

A PDB may also be included when produced by the release build.

Minimum runtime files for a consumer are:

```text
Libmem.NET.dll
Ijwhost.dll
libmem.dll
```

Keep `Libmem.NET.xml` beside `Libmem.NET.dll` for IntelliSense documentation.

## Integrity and provenance

Each official release is built by the Release workflow from one exact repository commit.

Before publication, automation verifies:

- release version against `VERSION`;
- assembly version metadata;
- package manifest version;
- repository commit provenance;
- pinned libmem commit;
- Windows x64 platform and Release configuration;
- runtime package file list, file sizes, and SHA-256 hashes;
- ZIP contents against the unpacked package;
- external `.zip.sha256` checksum;
- presence of a matching non-empty CHANGELOG section.

The historical LibmemCli v1.0.0 runtime archive SHA-256 is:

```text
647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99
```

Consumers who require reproducibility should additionally pin the Git tag or exact release commit rather than tracking `main`.

## Versioning policy

After v1.0:

- patch releases such as `1.0.1` are for compatible fixes and maintenance;
- minor releases such as `1.1.0` may add backward-compatible APIs or capabilities;
- incompatible public-contract changes require an explicit major-version decision;
- changes to the pinned upstream libmem revision require compatibility review and runtime validation.

The project does not promise that every internal implementation detail remains unchanged. The stable commitment applies to the documented public managed contract and supported release environment.

## Distribution channels

### GitHub Release ZIP

This is the primary stable binary distribution channel.

Use it when consumers only need built binaries.

### Git submodule / source integration

Use this when a consumer needs exact source provenance, reproducible native builds, or integration into its own build pipeline.

### NuGet

The `Libmem.NET` package path is validated both through local-feed CI and an independent public nuget.org consumer: restore/build/run/publish, native asset copy, XML documentation, provenance, and non-x64 rejection. Historical v1.0.0 did not ship an official NuGet asset.

`2.0.0-preview.1` was successfully published through the account-side Trusted Publishing policy and `NUGET_USER` flow. Future publications must re-verify those settings if the repository, workflow, environment, or publishing account changes. See [CONSUMPTION.md](CONSUMPTION.md).

## Release workflow safety

- `release/v<version>` branches build exact-version Release x64 ZIP and NuGet packages, validate them, and render formal notes from CHANGELOG. They do not log in to NuGet, push a package, create a GitHub Release, or delete the branch.
- Only `v<version>` tags enable GitHub downloads, including the exact-version `.nupkg`. Preview tags are marked as prereleases. OIDC login/push require a later manual Release run on the published tag with `publish-nuget` enabled. The tag, `VERSION`, assembly metadata, CHANGELOG and verified package version must agree.
- A successful dry run is evidence of package and note readiness, not permission to publish. Select the version, verify all Release tests on the exact candidate, and follow [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) before creating a tag.
- Keep existing tags and historical assets immutable. The breaking identity migration requires an explicit major-version decision.
- Ordinary PR/push Build runs validate all Release x64 suites. Debug build and smoke tests are additional manual checks enabled with `workflow_dispatch` input `debug`.

## Release history

| Version | Date | Status | Official platform |
| --- | --- | --- | --- |
| 2.0.0-preview.1 | 2026-10-05 | Published prerelease, Libmem.NET identity | Windows x64 / .NET 8 |
| 2.1.1 | 2026-10-07 | Published stable on GitHub and nuget.org; current public PackageReference baseline | Windows x64 / .NET 8 |
| 2.1.0 | 2026-10-07 | Previous stable release | Windows x64 / .NET 8 |
| 2.0.0 | 2026-10-06 | Published stable, Libmem.NET identity | Windows x64 / .NET 8 |
| 1.0.0 | 2026-09-30 | Historical stable, LibmemCli identity | Windows x64 / .NET 8 |
| 0.3.0 | 2026-09-29 | Historical | Windows x86/x64 |
| 0.2.0 | 2026-09-28 | Historical | Windows |
| 0.1.0 | 2026-09-27 | Historical | Windows |

Detailed release notes: [releases/v1.0.0.md](releases/v1.0.0.md).

Archived concise v1.0.0 release-body draft (not a publication instruction): [releases/v1.0.0-github.md](releases/v1.0.0-github.md).

For change details, see [../CHANGELOG.md](../CHANGELOG.md). For API behavior, see [API.md](API.md). For installation and consumption options, see [CONSUMPTION.md](CONSUMPTION.md).
