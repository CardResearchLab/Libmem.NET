# Libmem.NET Releases

This document describes the official release channel, current stable release, support boundaries, integrity model, and versioning policy for Libmem.NET.

## Current stable release

**2.5.0 is now the published stable release** (assembly/file `2.5.0.0`). Exact source `190ed68e3d420932e19a724edfdc0e9482518e14` passed main Build #461 and Release dry run #50; GitHub Release #51 published five public assets and NuGet OIDC Release #52 pushed the package. Independent nuget.org-only x64/x86 consumers passed Published NuGet Smoke #48 on PR #148; the main-branch smoke remains a post-merge check.

Stable 2.5.0 adds five managed Try APIs, hardens local Hook removal and covers VMT partial-reset retry, without breaking existing managed signatures or changing the pinned native libmem revision. Version 2.4.1 remains an available previous stable patch.

### Libmem.NET v2.4.1 — published stable

- Includes ReadAlignedCode readable-page-boundary handling, stale process identity guards for remote mutations, and deterministic failed HookHandle.Remove regression coverage (PRs #137–#141).
- PRs #137–#142 merged; exact commit `036556f6a508edee9401fe02fe0a8f7cdca39083` passed main Build #445, Release #47 dry run, GitHub Release #48 and NuGet Trusted Publishing #49.
- Independent public nuget.org restore/build/run/publish on x64/x86 and unsupported-platform rejection passed in Published NuGet Smoke #40 and post-merge main #41; later main smokes #45 and #47 also passed.
- Stale PID identity preflights cannot eliminate the race between validation and native PID-only operations; no native handle-bound redesign is included.
- Full release notes: [releases/v2.4.1.md](releases/v2.4.1.md).

### Libmem.NET v2.5.0 — published stable

- Five additive public methods: `MemoryManager.TryRead`, `TryWrite`, and `ScanManager.TryDataScan`, `TryPatternScan`, `TrySigScan`. No existing public declarations were removed relative to v2.4.1.
- Local Hook removal now preflights a readable trampoline; VMT multi-entry partial Reset failure/retry is covered by deterministic tests. Native libmem remains pinned.
- Feature PRs #144 (Scan), #145 (Memory), #146 (Hook/VMT), plus candidate PR #147 merged; main Build #461 succeeded. `release/v2.5.0` exact-commit Release #50 completed dry-run validation, and tagged GitHub Release #51 published both ZIPs, checksum files and the 2.5.0 NuGet asset.
- Separate NuGet Trusted Publishing/OIDC Release #52 pushed `Libmem.NET.2.5.0.nupkg` successfully. Independent nuget.org-only consumer restore/build/run/publish on x64/x86, XML/native assets and unsupported-target rejection passed independently in PR #148 Published NuGet Smoke #48; main-branch follow-up is required after merge. See [the release checklist](RELEASE_CHECKLIST.md) and the [2.5.0 release notes](releases/v2.5.0.md).

### Libmem.NET v2.4.0

- Adds `AssemblyManager.ReadAlignedCode` and `SymbolManager.TryFindAddress` without changing existing 2.3.0 public signatures, pinned libmem or supported platforms.
- Release: https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.4.0
- Accepted commit: `e73393d9805862db32c4e58dba1eadd91a191e56`; main Build #425; dry run Release #44; tagged GitHub Release #45; NuGet OIDC publication Release #46; public x64/x86 NuGet Smoke #36.
- Both runtime ZIP checksums and exact-version multi-architecture NuGet artifacts passed release verification. See [releases/v2.4.0.md](releases/v2.4.0.md).

### Libmem.NET v2.3.0

- Release: https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.3.0
- Release commit: `561bf1dfa82b7df2da4dc649a016c103f3af71ff`; GitHub Release #42; NuGet Trusted Publishing #43; public x64/x86 NuGet consumer smoke #30 passed. Supported targets: Windows x64/x86 / .NET 8.

### Libmem.NET v2.2.1

- Release: https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.2.1
- Release commit: `5e2b051a61943d0ac0825b2ac0125ffb29cf1721`
- Pinned libmem commit: `a07c9942bf1358dabcc83eb0cd072736c749d7f8`
- NuGet package: `Libmem.NET 2.2.1`
- The release contains x64 and x86 runtime ZIPs, matching SHA-256 files, and one multi-architecture NuGet package.
- GitHub Release automation: Release #39; nuget.org OIDC publish: Release #40.

### Libmem.NET v2.2.0

- Release: https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.2.0
- Release commit: `fefc0819d6b1c62a00f469ad64f98c64c4accc0f`
- Pinned libmem commit: `a07c9942bf1358dabcc83eb0cd072736c749d7f8`
- x64 Runtime ZIP SHA-256: `57615efad451832584c510e83ff60656c8e6d81a241bd1d8d288d56e6f19eff3`
- x86 Runtime ZIP SHA-256: `dbf07c4047f61505e95ca45f5a3cc0e93802bda933ac6713269c49220fa9e94e`
- NuGet package: `Libmem.NET 2.2.0`

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

The supported target for published 2.5.0 is:

- Windows x64 and Windows x86;
- .NET 8;
- C# / .NET consumers using the C++/CLI wrapper;
- the pinned rdbo/libmem native revision recorded by the release;
- Runtime ZIP distribution and source/submodule integration.

The current target does **not** promise:

- ARM64 production support;
- AnyCPU compatibility;
- cross-bitness injection;
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
- Windows x64/x86 packaging layout.

The committed `api/Libmem.NET.PublicApi.txt` baseline is validated by CI. Intentional incompatible changes must be explicit, documented, reviewed, and versioned appropriately. The identity migration preserves member signatures and behavior while intentionally changing namespace, assembly, and file names.

## Current package contents

Each Windows x64/x86 runtime package contains:

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
- matching Windows x64/x86 platform and Release configuration;
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

The published 2.5.0 `Libmem.NET` package was pushed by NuGet OIDC Release #52 and passed independent nuget.org-only x64/x86 PackageReference restore/build/run/publish, XML/native asset checks and unsupported target rejection in PR #148 Published NuGet Smoke #48. Confirm the main-branch CI after merge. Historical v1.0.0 did not ship an official NuGet asset.

`2.0.0-preview.1` was successfully published through the account-side Trusted Publishing policy and `NUGET_USER` flow. Future publications must re-verify those settings if the repository, workflow, environment, or publishing account changes. See [CONSUMPTION.md](CONSUMPTION.md).

## Release workflow safety

- `release/v<version>` branches build exact-version Release x64 and x86 ZIPs plus the multi-architecture NuGet package, validate them, and render formal notes from CHANGELOG. They do not log in to NuGet, push a package, create a GitHub Release, or delete the branch.
- Only `v<version>` tags enable GitHub downloads, including the exact-version `.nupkg`. Preview tags are marked as prereleases. OIDC login/push require a later manual Release run on the published tag with `publish-nuget` enabled. The tag, `VERSION`, assembly metadata, CHANGELOG and verified package version must agree.
- A successful dry run is evidence of package and note readiness, not permission to publish. Select the version, verify all Release tests on the exact candidate, and follow [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) before creating a tag.
- Keep existing tags and historical assets immutable. The breaking identity migration requires an explicit major-version decision.
- Ordinary PR/push Build runs validate Release x64, Release x86, and the multi-architecture NuGet consumer gate. Debug build and smoke tests remain additional manual checks enabled with `workflow_dispatch` input `debug`.

## Release history

| Version | Date | Status | Official platform |
| --- | --- | --- | --- |
| 2.5.0 | 2026-10-11 | Published stable; GitHub #51 and NuGet #52 succeeded; independent public x64/x86 Smoke #48 passed on PR #148; post-merge main run pending | Windows x64/x86 / .NET 8 |
| 2.4.1 | 2026-10-10 | Published stable; GitHub Release #48, NuGet #49, public smoke #40/#41 passed | Windows x64/x86 / .NET 8 |
| 2.4.0 | 2026-10-09 | Previous stable; x64/x86 smoke #36 passed | Windows x64/x86 / .NET 8 |
| 2.3.0 | 2026-10-08 | Previous published stable; public x64/x86 smoke #30 passed | Windows x64/x86 / .NET 8 |
| 2.2.1 | 2026-10-08 | Previous published stable | Windows x64/x86 / .NET 8 |
| 2.2.0 | 2026-10-08 | Previous published stable | Windows x64/x86 / .NET 8 |
| 2.0.0-preview.1 | 2026-10-05 | Published prerelease, Libmem.NET identity | Windows x64 / .NET 8 |
| 2.1.1 | 2026-10-07 | Previous published stable | Windows x64 / .NET 8 |
| 2.1.0 | 2026-10-07 | Previous stable release | Windows x64 / .NET 8 |
| 2.0.0 | 2026-10-06 | Published stable, Libmem.NET identity | Windows x64 / .NET 8 |
| 1.0.0 | 2026-09-30 | Historical stable, LibmemCli identity | Windows x64 / .NET 8 |
| 0.3.0 | 2026-09-29 | Historical | Windows x86/x64 |
| 0.2.0 | 2026-09-28 | Historical | Windows |
| 0.1.0 | 2026-09-27 | Historical | Windows |

Detailed release notes: [releases/v1.0.0.md](releases/v1.0.0.md).

Archived concise v1.0.0 release-body draft (not a publication instruction): [releases/v1.0.0-github.md](releases/v1.0.0-github.md).

For change details, see [../CHANGELOG.md](../CHANGELOG.md). For API behavior, see [API.md](API.md). For installation and consumption options, see [CONSUMPTION.md](CONSUMPTION.md).
