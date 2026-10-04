# Libmem.NET Consumption Guide

> Current official target: Windows x64 / .NET 8.

Libmem.NET supports Runtime ZIP, Git Submodule/source integration, and a validated NuGet package path. The managed assembly and namespace remain `Libmem.NET` for v1.0 compatibility.

## 1. Runtime ZIP — official release consumption

Use the GitHub Release asset when the consuming project only needs built binaries.

Official release assets:

```text
Libmem.NET-windows-x64.zip
Libmem.NET-windows-x64.zip.sha256
```

The runtime directory contains the managed C++/CLI assembly, XML IntelliSense documentation, native libmem runtime, Ijwhost, package metadata, and licensing notices.

Minimum runtime files:

```text
Libmem.NET.dll
Libmem.NET.xml
Ijwhost.dll
libmem.dll
```

The consumer should reference `Libmem.NET.dll` and keep the runtime files together with the executable.

This remains the **primary stable distribution model** until a package-manager path passes the same runtime acceptance level.

## 2. Git Submodule — source/build integration

Projects that want reproducible source-level integration can add this repository as a submodule and build the pinned native + C++/CLI wrapper through the repository build scripts or reusable workflow.

This is useful when the consumer wants:

- the exact pinned libmem revision;
- source-level reproducibility;
- integration into an existing build pipeline;
- direct access to wrapper source and tests.

It is more operationally complex than consuming a prebuilt package.

## 3. NuGet package

The repository contains a validated PackageReference package. Public publication is wired into the release workflow, but the first nuget.org publish still requires the one-time Trusted Publishing account setup described below:

```text
Package ID: Libmem.NET
Status: validated for Windows x64 / .NET 8
Publication: release workflow via nuget.org Trusted Publishing (OIDC)
Target: Windows x64 / .NET 8
```

The package ID is now fixed as `Libmem.NET` before first public publication. Development packages also use a commit-qualified prerelease version such as `0.3.0-dev.<commit>` rather than reusing the already released `0.3.0` version. CI stamps the package with the repository URL and exact Git commit, and the package verifier checks that provenance before the consumer test runs.

### Package layout

```text
lib/net8.0/
├─ Libmem.NET.dll
└─ Libmem.NET.xml

runtimes/win-x64/native/
├─ libmem.dll
└─ Ijwhost.dll

buildTransitive/
└─ Libmem.NET.targets
```

The mixed-mode `Libmem.NET.dll` is currently exposed from `lib/net8.0` so PackageReference can provide the compile-time reference directly.

The native runtime assets are stored under the portable RID `win-x64`.

A transitive MSBuild target:

- rejects non-x64 consumers;
- copies `libmem.dll` and `Ijwhost.dll` into build/publish output;
- keeps the package usable for normal x64 PackageReference projects without requiring consumers to manually copy the two native runtime files.

### Build the prototype locally

After building Libmem.NET x64:

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\eng\package-nuget.ps1 -Configuration Release
```

The script reads `VERSION`, resolves the current Git commit, validates the required x64 binaries, creates the local package, and immediately runs the package layout/provenance verifier.

### Local package test

The CI prototype performs the complete flow:

```text
Build Libmem.NET
    ↓
dotnet pack
    ↓
verify .nupkg layout
    ↓
restore an independent PackageReference consumer
    ↓
build/run the consumer
    ↓
publish the consumer and verify runtime files
    ↓
reject a non-x64 consumer
    ↓
ProcessSession.Open
    ↓
Allocate / Write / Read / Dispose
```

The consumer test references **only the local NuGet package**. It does not use a project reference to Libmem.NET.

This proves more than package creation: it verifies that the restored package is loadable and executable on Windows x64, that publish output receives the required native runtime files, and that unsupported non-x64 consumption fails early.

## Platform/runtime rationale

Microsoft's modern .NET C++/CLI guidance documents two constraints that directly shape this package prototype:

- C++/CLI targeting modern .NET is Windows-only.
- `ijwhost.dll` must be copied from the .NET app host into the output directory for C++/CLI components.

References:

- [Migrate C++/CLI projects to .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/cpp-cli)
- [NuGet multi-targeting and architecture-specific assets](https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks)
- [.NET Runtime Identifier catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)

The package therefore uses the portable `win-x64` RID for native assets and fails early outside Windows x64.

## NuGet platform constraints

Libmem.NET is not a normal AnyCPU managed library:

- `Libmem.NET.dll` is a Windows x64 C++/CLI mixed-mode assembly;
- it depends on native `libmem.dll`;
- it requires `Ijwhost.dll`;
- architecture selection matters at compile and runtime;
- the repository does not currently build a separate AnyCPU metadata/reference assembly.

NuGet's conventional architecture-specific model supports RID-specific runtime assets, but architecture-specific compile-time assembly design needs careful validation for this mixed-mode case.

For that reason, release publication remains gated by the independent PackageReference consumer tests, package provenance verification, and x64 runtime validation.

## NuGet release acceptance criteria

A public NuGet release requires all of the following:

1. local package layout verification passes;
2. an independent x64 PackageReference consumer restores successfully;
3. the consumer builds without a project reference;
4. `Libmem.NET.dll`, `libmem.dll`, and `Ijwhost.dll` reach the consumer output correctly;
5. `Libmem.NET.xml` is available for IDE documentation;
6. the consumer runs real Libmem.NET API calls successfully;
7. publish output also contains the native runtime dependencies;
8. non-x64 consumers fail early with a clear diagnostic;
9. package version/provenance matches the repository release;
10. package publication does not replace ZIP releases until both paths are independently reliable.

## Trusted Publishing setup

The release workflow publishes `Libmem.NET` with nuget.org Trusted Publishing (OIDC), so no long-lived NuGet API key is stored in GitHub.

One-time setup:

1. Sign in to nuget.org and open **Trusted Publishing**.
2. Add a GitHub policy with:
   - Repository owner: `HearthstoneModding`
   - Repository: `Libmem.NET`
   - Workflow file: `release.yml`
   - Environment: leave empty unless the workflow is later moved behind a GitHub Environment.
3. In GitHub Actions secrets, add `NUGET_USER` containing the nuget.org profile username (not the email address).

On a `v*` tag or `release/v*` release branch, the release workflow builds once, creates the runtime ZIP and exact-version `Libmem.NET.<version>.nupkg`, validates both, exchanges GitHub OIDC for a short-lived NuGet credential, publishes the NuGet package, and then creates the GitHub Release.

## Current recommendation

For stable consumption, use either the GitHub Release x64 runtime ZIP, the `Libmem.NET` NuGet package once its first public version is visible on nuget.org, or the repository/submodule/reusable workflow for source-level integration.
