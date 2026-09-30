# LibmemCli Releases

This document describes the official release channel, current stable release, support boundaries, integrity model, and versioning policy for LibmemCli.

## Current stable release

**LibmemCli v1.0.0** is the first stable Windows x64 release.

- Release: https://github.com/HearthstoneModding/Libmem/releases/tag/v1.0.0
- Runtime package: https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip
- SHA-256 file: https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256
- Release commit: `e6181b9f74b5d5877e3d1c253d3bbfef61141445`
- Pinned libmem commit: `a07c9942bf1358dabcc83eb0cd072736c749d7f8`
- Runtime ZIP SHA-256: `647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99`

## Stable support boundary

v1.0 officially supports:

- Windows x64;
- .NET 8;
- C# / .NET consumers using the C++/CLI wrapper;
- the pinned rdbo/libmem native revision recorded by the release;
- Runtime ZIP distribution and source/submodule integration.

v1.0 does **not** promise:

- official x86 release assets;
- AnyCPU compatibility;
- cross-bitness injection;
- public NuGet distribution;
- game-specific state, Snapshot, Entity, GameState, IPC, Unity, Mono, or Hearthstone business logic.

Those application-level concerns remain the responsibility of consuming projects.

## What v1.0 freezes

The v1.0 line treats the public managed contract as stable by default:

- namespace, public type, member, overload, and enum shape;
- ProcessSession and Manager responsibilities;
- read-only result-model semantics;
- ownership and deterministic Dispose behavior;
- null / sentinel / exception distinctions;
- target-process identity semantics;
- Windows x64 packaging layout.

The committed public API baseline is validated by CI. Intentional incompatible changes must be explicit, documented, reviewed, and versioned appropriately.

## Official package contents

The Windows x64 runtime package contains:

```text
LibmemCli.dll
LibmemCli.xml
Ijwhost.dll
libmem.dll
VERSION
LICENSE
THIRD_PARTY_NOTICES.md
manifest.json
```

A PDB may also be included when produced by the release build.

Minimum runtime files for a consumer are:

```text
LibmemCli.dll
Ijwhost.dll
libmem.dll
```

Keep `LibmemCli.xml` beside `LibmemCli.dll` for IntelliSense documentation.

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

The v1.0.0 runtime archive SHA-256 is:

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

NuGet remains a local/CI prototype. It is not an official v1.0.0 distribution channel and is not published to nuget.org.

Public NuGet publication will be treated as a separate release decision after package-manager-specific acceptance is complete.

## Release history

| Version | Date | Status | Official platform |
| --- | --- | --- | --- |
| 1.0.0 | 2026-09-30 | Stable | Windows x64 / .NET 8 |
| 0.3.0 | 2026-09-29 | Historical | Windows x86/x64 |
| 0.2.0 | 2026-09-28 | Historical | Windows |
| 0.1.0 | 2026-09-27 | Historical | Windows |

Detailed release notes: [releases/v1.0.0.md](releases/v1.0.0.md).

Recommended concise GitHub Release body: [releases/v1.0.0-github.md](releases/v1.0.0-github.md).

For change details, see [../CHANGELOG.md](../CHANGELOG.md). For API behavior, see [API.md](API.md). For installation and consumption options, see [CONSUMPTION.md](CONSUMPTION.md).
