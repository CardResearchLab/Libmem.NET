# Libmem.NET Consumption Guide

> Current official target: Windows x64 / .NET 8.

Libmem.NET supports Runtime ZIP, Git Submodule/source integration, and NuGet PackageReference consumption. The published `2.0.0-preview.1` uses the `Libmem.NET` managed assembly and namespace and has passed an independent nuget.org consumer acceptance test. Historical v1.0.0 uses `LibmemCli`; the identity migration requires updated references and recompilation. See [MIGRATION.md](MIGRATION.md).

## 1. Runtime ZIP — official release consumption

For current Libmem.NET, use the [v2.0.0-preview.1 GitHub prerelease](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.0.0-preview.1), install the matching nuget.org package, or build from source.

Historical [v1.0.0](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v1.0.0) provides [LibmemCli-windows-x64.zip](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip) and its [SHA-256 file](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256). Those assets contain `LibmemCli.dll`, not `Libmem.NET.dll`, and require the old namespace. Do not rename old binaries to use the new examples.

Current build artifact names:

```text
Libmem.NET-windows-x64.zip
Libmem.NET-windows-x64.zip.sha256
Libmem.NET.2.0.0-preview.1.nupkg
```

The runtime directory contains the managed C++/CLI assembly, XML IntelliSense documentation, native libmem runtime, Ijwhost, package metadata, licensing notices, `CHANGELOG.md` and `MIGRATION.md`. The exact-version `.nupkg` is also published on nuget.org; local feeds remain useful for development and pre-publication acceptance.

Minimum runtime files:

```text
Libmem.NET.dll
Libmem.NET.xml
Ijwhost.dll
libmem.dll
```

The consumer should reference `Libmem.NET.dll` and keep the runtime files together with the executable. Do not mix files from different releases or build commits.

For a .NET 8 x64 application:

1. build or extract the matching current runtime artifact;
2. reference `Libmem.NET.dll`;
3. copy `Libmem.NET.dll`, `libmem.dll`, and `Ijwhost.dll` into application output;
4. keep `Libmem.NET.xml` beside the assembly for IntelliSense documentation;
5. target x64 explicitly rather than AnyCPU.

ZIP and source integration remain available independently of public NuGet setup.

## 2. Git Submodule — source/build integration

Projects that want reproducible source-level integration can add this repository as a submodule and build the pinned native + C++/CLI wrapper through the repository build scripts or reusable workflow.

This is useful when the consumer wants:

- the exact pinned libmem revision;
- source-level reproducibility;
- integration into an existing build pipeline;
- direct access to wrapper source and tests.

It is more operationally complex than consuming a prebuilt package.

## 3. NuGet package

`Libmem.NET 2.0.0-preview.1` is publicly available through nuget.org and was published by the release workflow with Trusted Publishing (OIDC). The same workflow remains the publication path for future versions:

```text
Package ID: Libmem.NET
Status: validated for Windows x64 / .NET 8
Publication: nuget.org via Trusted Publishing (OIDC); 2.0.0-preview.1 published and consumer-tested
Target: Windows x64 / .NET 8
```

The package ID is fixed as `Libmem.NET`. Development packages use a commit-qualified prerelease version derived from `VERSION`, such as `2.0.0-preview.1.dev.<commit>`, rather than reusing a published version. CI stamps the package with the repository URL and exact Git commit, and the package verifier checks that provenance before the consumer test runs.

### Package layout

| Package path | Files |
| --- | --- |
| `lib/net8.0/` | `Libmem.NET.dll`, `Libmem.NET.xml` |
| `runtimes/win-x64/native/` | `libmem.dll`, `Ijwhost.dll` |
| `buildTransitive/` | `Libmem.NET.targets` |

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

For the exact prepared preview instead of a commit-qualified development package:

```powershell
.\eng\package-nuget.ps1 -Configuration Release -PackageVersion 2.0.0-preview.1
```

The automatic Build gate packages the exact `VERSION` and validates restore/run/publish against it. Default local development packages use `2.0.0-preview.1.dev.<commit>`; stable base versions use `<version>-dev.<commit>`.

The script reads `VERSION`, resolves the current Git commit, validates the required x64 binaries, creates the local package, and immediately runs the package layout/provenance verifier.

### Local package test

The CI prototype performs the complete flow:

1. Build Release x64 and pack the NuGet package.
2. Verify `.nupkg` layout and commit provenance.
3. Restore an independent PackageReference consumer from the local feed.
4. Build/run the consumer, including `ProcessSession.Open` and Allocate / Write / Read / Dispose.
5. Publish and verify the runtime dependencies.
6. Reject a non-x64 consumer.

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

For normal Windows x64 / .NET 8 PackageReference consumption, use public `Libmem.NET 2.0.0-preview.1` with prerelease versions enabled. Runtime ZIP, source/submodule and reusable-workflow integration remain available for consumers that need binary bundles or exact source provenance. Historical v1.0.0 remains available under its original names.

See [RELEASES.md](RELEASES.md) for release history, support boundaries and versioning, and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) for the next publication.
