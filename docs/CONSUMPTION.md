# Libmem.NET Consumption Guide

> Current stable target: Windows x64/x86 / .NET 8.

Libmem.NET supports Runtime ZIP, Git submodule/source integration, and NuGet PackageReference consumption. Stable `2.5.0` is published via GitHub Release #51 and NuGet OIDC Release #52. Independent nuget.org-only x64/x86 consumer verification passed in PR #148 Published NuGet Smoke #48; the main-branch run must also be verified after merge. Historical v1.0.0 used `LibmemCli`; see [MIGRATION.md](MIGRATION.md).

## 1. Runtime ZIP — official release consumption

The 2.5.0 release pipeline produces architecture-specific runtime archives:

```text
Libmem.NET-windows-x64.zip
Libmem.NET-windows-x64.zip.sha256
Libmem.NET-windows-x86.zip
Libmem.NET-windows-x86.zip.sha256
Libmem.NET.2.5.0.nupkg
```

Each runtime directory contains the architecture-matched C++/CLI assembly, XML IntelliSense documentation, native libmem runtime, Ijwhost, package metadata, licensing notices, `CHANGELOG.md`, `MIGRATION.md`, and a manifest.

Minimum runtime files:

```text
Libmem.NET.dll
Libmem.NET.xml
Ijwhost.dll
libmem.dll
```

For a .NET 8 application:

1. explicitly target `x64` or `x86`;
2. use the runtime archive matching that process architecture;
3. reference the matching `Libmem.NET.dll`;
4. keep `Libmem.NET.dll`, `libmem.dll`, and `Ijwhost.dll` together in application output;
5. keep `Libmem.NET.xml` beside the assembly for IntelliSense;
6. do not mix assets from different architectures, versions, or build commits.

AnyCPU and cross-bitness operation are unsupported.

## 2. Git Submodule — source/build integration

Projects that want reproducible source-level integration can add this repository as a submodule and build the pinned native + C++/CLI wrapper through the repository build scripts or reusable workflow.

This is useful when the consumer wants:

- the exact pinned libmem revision;
- source-level reproducibility;
- integration into an existing build pipeline;
- direct access to wrapper source and tests.

## 3. NuGet package

The 2.5.0 stable release uses one multi-architecture package:

```text
Package ID: Libmem.NET
Stable version: 2.5.0
Published public baseline: 2.5.0 (PR #148 public Smoke #48 passed)
Target: Windows x64/x86 / .NET 8
Architecture selection: explicit Platform / PlatformTarget
Unsupported: AnyCPU
```

Development packages use commit-qualified prerelease versions based on `VERSION`, for example `2.5.0-dev.<commit>` on the historic 2.5 candidate branch. Published stable consumers should use exact version `2.5.0`. CI stamps repository URL and commit provenance into packages.

### Package layout

| Package path | Files |
| --- | --- |
| `runtimes/win-x64/lib/net8.0/` | `Libmem.NET.dll`, `Libmem.NET.xml` |
| `runtimes/win-x64/native/` | `libmem.dll`, `Ijwhost.dll` |
| `runtimes/win-x86/lib/net8.0/` | `Libmem.NET.dll`, `Libmem.NET.xml` |
| `runtimes/win-x86/native/` | `libmem.dll`, `Ijwhost.dll` |
| `buildTransitive/` | `Libmem.NET.targets` |

Because `Libmem.NET.dll` is a mixed-mode C++/CLI assembly, the package does not expose one architecture-neutral compile assembly. The transitive MSBuild target resolves the matching `win-x64` or `win-x86` managed/native assets from the consumer's explicit architecture and rejects AnyCPU early.

### Build the package locally

Build both runtime architectures first, then compose the package:

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\eng\package-runtime.ps1 -Configuration Release -Platform x64
.\build.ps1 -Configuration Release -Platform x86
.\eng\package-runtime.ps1 -Configuration Release -Platform x86
.\eng\package-nuget.ps1 -Configuration Release -PackageVersion 2.5.0
```

The automatic Build gate performs equivalent composition from verified x64/x86 runtime artifacts. The above exact-version 2.5.0 command builds an **exact-version local package**; consumers can use published `2.5.0` from nuget.org; independent x64/x86 Smoke #48 passed on PR #148.

### Local package test

CI verifies:

1. Release x64 runtime build/tests/package.
2. Release x86 runtime build/tests/package.
3. Multi-architecture `.nupkg` layout and commit provenance.
4. Independent x64 PackageReference restore/run/publish.
5. Independent x86 PackageReference restore/run/publish.
6. Correct managed/native assets in each publish output.
7. Explicit AnyCPU rejection.

The consumer tests reference only the local NuGet package, not the Libmem.NET project.

## Platform/runtime rationale

Modern .NET C++/CLI is Windows-only and requires architecture-matched mixed-mode/native assets. `Ijwhost.dll` must be present beside the application for C++/CLI hosting. The package therefore carries separate portable `win-x64` and `win-x86` runtime trees and selects one explicitly.

References:

- [Migrate C++/CLI projects to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/cpp-cli)
- [NuGet multi-targeting and architecture-specific assets](https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks)
- [.NET Runtime Identifier catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)

## NuGet platform constraints

Libmem.NET is not an AnyCPU managed library:

- x64 consumers use the x64 C++/CLI assembly plus x64 `libmem.dll` / `Ijwhost.dll`;
- x86 consumers use the x86 C++/CLI assembly plus x86 `libmem.dll` / `Ijwhost.dll`;
- architecture must be explicit at build and runtime;
- when `PlatformTarget` is explicitly set, it must be `x64` or `x86`; unsupported values such as `AnyCPU` or `arm64` are rejected even if `Platform` is x64/x86;
- `Platform` is used as the architecture fallback only when `PlatformTarget` is empty; `Win32` maps to x86;
- cross-bitness operation is not promised;
- ARM64 is not a 2.5.0 production target.

## NuGet release acceptance criteria

The public NuGet Smoke baseline in this post-release PR targets `2.5.0` with x64/x86 NuGet-only restore, build, run, publish and unsupported-target rejection; its public-feed x64/x86 acceptance passed in PR #148 Smoke #48; post-merge main CI remains to verify. Historical 2.4.1 public smoke #40/#41 and 2.4.0 smoke #36 passed.

1. multi-architecture package layout/provenance verification;
2. independent x64 restore/build/run/publish success;
3. independent x86 restore/build/run/publish success;
4. architecture-matched `Libmem.NET.dll`, `libmem.dll`, and `Ijwhost.dll` in output;
5. XML documentation availability;
6. AnyCPU rejection with a clear diagnostic;
7. rejection of unsupported explicit `PlatformTarget` values even when `Platform` names a supported architecture;
8. version/provenance agreement with the exact repository release;
9. post-publication nuget.org smoke for both x64 and x86.

## Trusted Publishing setup

The release workflow publishes `Libmem.NET` with nuget.org Trusted Publishing (OIDC), so no long-lived NuGet API key is stored in GitHub.

One-time setup:

1. Sign in to nuget.org and open **Trusted Publishing**.
2. Add a GitHub policy with:
   - Repository owner: `CardResearchLab`
   - Repository: `Libmem.NET`
   - Workflow file: `release.yml`
   - Environment: leave empty unless the workflow is later moved behind a GitHub Environment.
3. In GitHub Actions secrets, add `NUGET_USER` containing the nuget.org profile username (not the email address).

The repository is now `CardResearchLab/Libmem.NET`. Trusted Publishing's Repository owner must be `CardResearchLab`, matching the current GitHub organization login. NuGet `Authors` credits `xiaohei7972`; the Package Owner and `NUGET_USER` refer to the publishing NuGet account, currently `xiaohei`.

Both `v*` tags and `release/v*` branches build the runtime ZIP and exact-version `Libmem.NET.<version>.nupkg`, validate both, and render release notes. Tags create GitHub downloads; versions with prerelease suffixes use the prerelease flag and do not replace the latest stable release. Release branches remain dry runs. Automatic pushes do not log in to nuget.org or publish there.

For `2.0.0-preview.1`, this manual tagged run completed successfully: OIDC login and NuGet push both passed without recreating the GitHub Release. For future versions, use the same explicit **Release** workflow opt-in on an already published `v*` tag. Ordinary branches and unpublished tags remain rejected for NuGet publication.

The successful `2.0.0-preview.1` publication proves the current Trusted Publishing path worked at release time. Re-verify account-side policy and `NUGET_USER` before future publications if repository ownership, workflow names, environments, or publishing accounts change.

## Current recommendation

Public PackageReference consumption should use published stable `Libmem.NET 2.5.0` with explicit x64 or x86 PlatformTarget. PR #148 Published NuGet Smoke #48 validated both architectures and unsupported PlatformTarget rejection directly from nuget.org; post-merge main CI is the final confirmation. The prior 2.4.1 stable version remains available.

See [RELEASES.md](RELEASES.md) for release history, support boundaries and versioning, and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) for the next publication.
