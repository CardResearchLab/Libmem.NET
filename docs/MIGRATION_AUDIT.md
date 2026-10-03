# Libmem.NET Migration Audit

Date: 2026-10-04  
Base branch: `main`  
Base commit: `6850e023487c1b87abae9a2285b858c353dcefb5`  
Audit branch: `migration/docs-compat-audit`

## Goal

Audit the pre-v1.0 identity migration from the legacy `LibmemCli` naming to the unified `Libmem.NET` identity without redesigning the library API.

## Target identity

| Surface | Target |
| --- | --- |
| Repository | `HearthstoneModding/Libmem.NET` |
| NuGet package ID | `Libmem.NET` |
| Managed namespace | `Libmem.NET` |
| Managed assembly | `Libmem.NET.dll` |
| IntelliSense XML | `Libmem.NET.xml` |
| Solution | `Libmem.NET.sln` |
| C++/CLI project | `src/Libmem.NET.vcxproj` |
| Public API baseline | `api/Libmem.NET.PublicApi.txt` |
| Runtime artifact | `Libmem.NET-windows-x64` |
| Release target | Windows x64 / .NET 8 |

The native upstream runtime remains `libmem.dll`. The public static facade type `Libmem` also remains intentional.

## Initial scan on main

| Legacy token | Matching files | Notes |
| --- | ---: | --- |
| `Libmem.CLI` | 0 | The repository primarily used `LibmemCli`. |
| `LibmemCli` | 72 | Source, tests, build, CI, packaging, docs and samples. |
| `LibmemCli.dll` | 16 | Managed binary references. |
| `LibmemCli.vcxproj` | 5 | README, solution, sample and source-contract test. |
| `LibmemCli.sln` | 4 | README, build script and source-contract test. |
| `StandaloneGameMod` | 3 | ROADMAP zh/en and CHANGELOG. |
| old `HearthstoneModding/Libmem/...` workflow path | 2 | README zh/en. |
| `Hearthstone.exe` | 3 | README zh/en plus a forbidden-coupling validator. |

There is no standalone `RELEASES.md` in the repository tree. GitHub Release notes are generated from `CHANGELOG.md` and verified package metadata by `eng/render-release-notes.py`.

## Completed in this branch

Updated: `README.md`, `README.en.md`, `ROADMAP.md`, `ROADMAP.en.md`, `CHANGELOG.md`, `docs/API.md`, `docs/CONSUMPTION.md`, `api/README.md`, `samples/Example.cs`, and `samples/Example.csproj`.

Changes include namespace, managed DLL/XML, solution/project, public API baseline, runtime artifact and reusable-workflow references; C# samples now use `using Libmem.NET;`. Product-specific process/injector examples were replaced with application-neutral examples.

A second scan of those ten modified files found zero occurrences of `Libmem.CLI`, `LibmemCli`, `LibmemCli.dll`, `LibmemCli.vcxproj`, `LibmemCli.sln`, `HearthstoneModding/Libmem/`, `StandaloneGameMod`, or `Hearthstone.exe`.

This audit file intentionally contains legacy identifiers so the migration mapping remains reviewable.

## Remaining work outside this branch

The initial `LibmemCli` search matched 72 files. Ten documentation/sample files were cleaned here, leaving the legacy identity concentrated in about 62 source/build/test/CI files owned by the parallel migration work.

### Source / API

- `api/LibmemCli.PublicApi.txt`
- `src/LibmemCli.cpp`
- `src/LibmemCli.h`
- `src/LibmemCli.vcxproj`
- C++ files using the legacy namespace
- `src/AssemblyInfo.cpp`

### Build / package / release

- `LibmemCli.sln` and `build.ps1`
- `eng/check-public-api.py`
- `eng/package-runtime.ps1` and `eng/package-nuget.ps1`
- `eng/render-release-notes.py` and `eng/verify-package.py`
- `packaging/Libmem.NET.csproj` and `packaging/Libmem.NET.targets`

### Tests / CI

The current `tests/LibmemCli.*` projects, test source imports, package verification tests and source-contract checks still belong to the test migration branch. The GitHub Actions workflows still containing legacy names belong to the NuGet/CI/release migration branch.

The `Hearthstone.exe` string remaining in `tests/check_sources.py` is a negative coupling check, not a runtime dependency.

## Compatibility assessment

This rename is intentionally completed before v1.0. Even if public types, methods and runtime behavior stay the same, changing namespace, assembly filename and project/package-facing identity requires existing consumers to update imports and references. Do not describe the new managed assembly as binary-compatible with the old identity.

The compatibility goal is to preserve API shape and behavior while changing identity once before v1.0. No duplicate legacy assembly or compatibility shim is introduced by this documentation branch.

## Application-coupling audit

Public documentation no longer describes Libmem.NET as integrated with StandaloneGameMod or a Hearthstone-specific process. `HearthstoneModding` occurrences in repository URLs, organization ownership or publishing metadata are expected and are not application-layer coupling.

Generic exclusion terms such as Snapshot, GameState or IPC may remain when they explicitly state that those responsibilities belong outside this library.

## Final merge acceptance

After all parallel migration branches are merged, run:

~~~powershell
git grep -n -I -E "Libmem\.CLI|LibmemCli|LibmemCli\.dll|LibmemCli\.vcxproj|LibmemCli\.sln|StandaloneGameMod|Hearthstone\.exe|HearthstoneModding/Libmem/" -- . ":(exclude)docs/MIGRATION_AUDIT.md"
~~~

Expected result: no active legacy references outside explicitly reviewed historical/audit text.

Also verify positive identity references with:

~~~powershell
git grep -n -I "Libmem.NET"
~~~

Review that active references consistently point to `Libmem.NET.sln`, `src/Libmem.NET.vcxproj`, `Libmem.NET.dll`, `Libmem.NET.xml`, `using Libmem.NET;`, `api/Libmem.NET.PublicApi.txt`, `Libmem.NET-windows-x64`, and NuGet package ID `Libmem.NET`.

## Merge note

This branch is designed to merge with the parallel source, solution/project, tests, and NuGet/CI/release migration branches. Until those companion changes land, this branch intentionally documents final names that the current source tree has not yet fully adopted.
