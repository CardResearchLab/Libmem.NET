# Libmem.NET

[简体中文](README.md) | [English](README.en.md)

[![CI Build](https://github.com/CardResearchLab/Libmem.NET/actions/workflows/build.yml/badge.svg)](https://github.com/CardResearchLab/Libmem.NET/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/CardResearchLab/Libmem.NET)](https://github.com/CardResearchLab/Libmem.NET/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows x64/x86](https://img.shields.io/badge/Windows-x64%20%7C%20x86-0078D4)

**Libmem.NET** is a Windows C++/CLI wrapper around [rdbo/libmem](https://github.com/rdbo/libmem), exposing process, thread, module, memory, scanning, symbol, assembly/disassembly, Hook, VMT, and DLL injection capabilities to C# / .NET.

Author and maintainer: [xiaohei7972](https://github.com/xiaohei7972). Project organization: [CardResearchLab](https://github.com/CardResearchLab).

**Libmem.NET 2.5.0 is officially published** through GitHub Release #51 and NuGet Trusted Publishing #52. This release adds five Scan/Memory Try APIs and strengthens local Hook removal preflight and VMT retry regression coverage. Windows x64/x86, .NET 8, existing public APIs and pinned native libmem remain supported. Independent public-feed x64/x86 consumer verification passed Published NuGet Smoke #48 on PR #148.

**2.5.0 stable release:** feature PRs #144–#146 and release-candidate PR #147 merged. Main Build #461, dry-run Release #50, tagged GitHub Release #51 and NuGet OIDC publication #52 completed. See [2.5.0 release notes](docs/releases/v2.5.0.md) and [release checklist](docs/RELEASE_CHECKLIST.md).

Official support target:

- Windows x64
- Windows x86
- .NET 8
- C# / .NET consumers
- a pinned rdbo/libmem native backend

> Stable 2.5.0 supports Windows x64/x86 and .NET 8 with matching managed/native binaries. Explicit x64/x86 platform selection is mandatory; AnyCPU, ARM64 and cross-bitness operation remain unsupported.

## Download

Stable **Libmem.NET 2.5.0** is published: [v2.5.0 GitHub Release](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.5.0) and NuGet `Libmem.NET 2.5.0`. Release #51 and NuGet OIDC #52 succeeded. The independent x64/x86 [Published NuGet Smoke](https://github.com/CardResearchLab/Libmem.NET/actions/workflows/published-nuget-smoke.yml) for 2.5.0 passed in PR #148's Published NuGet Smoke #48; the post-merge main smoke remains to be checked.

Historical v1.0.0 still provides these old-name assets. They do not support the new-name examples below:

- [LibmemCli v1.0.0](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v1.0.0)
- [LibmemCli-windows-x64.zip](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip)
- [LibmemCli-windows-x64.zip.sha256](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256)

Historical ZIP SHA-256: `647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99`.

See the [Release & Versioning Guide](docs/RELEASES.md) for release and integrity information.

## Quick start

Build current source or extract a current Build artifact, then reference this assembly from a .NET 8 project that explicitly targets x64 or x86:

```text
Libmem.NET.dll
```

and keep at least these files beside the application:

```text
Libmem.NET.dll
libmem.dll
Ijwhost.dll
```

Keep `Libmem.NET.xml` as well for IntelliSense documentation.

### Basic example

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

var process = NativeApi.CurrentProcess()
    ?? throw new InvalidOperationException("Current process not found.");

using var session = ProcessSession.Open(process)
    ?? throw new InvalidOperationException("Failed to open process session.");

Console.WriteLine(
    $"{session.Name} PID={session.Pid} Arch={session.Architecture} Bits={session.Bits}");

foreach (var module in session.Modules.Enumerate())
{
    Console.WriteLine(
        $"{module.Name} Base=0x{module.Base:X} Size=0x{module.Size:X}");
}
```

### Memory example

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

using var session = ProcessSession.Open((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Failed to open process session.");

using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);

session.Memory.Write(allocation.Address, [1, 2, 3, 4]);

var data = session.Memory.Read(allocation.Address, 4);

Console.WriteLine(string.Join(", ", data));
```

`RemoteAllocation` implements `IDisposable`; use `using` for deterministic cleanup.

## API structure

New code should generally use `ProcessSession` as the process-scoped entry point:

| ProcessSession property | Manager |
| --- | --- |
| `Memory` | `MemoryManager` |
| `Modules` | `ModuleManager` |
| `Threads` | `ThreadManager` |
| `Scanner` | `ScanManager` |
| `Symbols` | `SymbolManager` |
| `Assembly` | `AssemblyManager` |
| `Hooks` | `HookManager` |
| `Injector` | `InjectorManager` |

The lower-level static `NativeApi.*` (`global::Libmem.NET.Libmem`) surface remains available for one-shot calls and native-style compatibility.

### Process / Thread

- process enumeration, lookup, and current-process access
- process liveness checks
- PID + start-time identity validation
- thread enumeration and main-thread lookup

### Modules / Symbols

- module enumeration, lookup, load, and unload
- exported symbol enumeration
- symbol address lookup; 2.4.0 adds `TryFindAddress` for normal misses
- symbol demangling

### Memory / Scanning

- Read / Write
- Set / Protect
- Allocate / Free
- owned `RemoteAllocation`
- DeepPointer
- DataScan
- PatternScan
- SigScan

### Assembly

- Assemble
- Disassemble
- CodeLength
- `ReadAlignedCode` (2.4.0: read-only instruction inspection, not automatic Hook installation)

`AssemblyManager` defaults to the target process architecture.

### Hook / VMT

- native code Hook
- trampoline metadata
- Hook Remove / Dispose
- VMT Hook / Unhook / Reset

`HookHandle` and `VmtManager` provide explicit lifetime management.

### Injection

`InjectorManager.InjectLibrary(...)` returns an `InjectedModuleHandle` representing one owned load reference.

Cross-bitness injection is not supported.

## Lifetime model

Resources that require ownership use explicit `IDisposable` semantics:

- `ProcessSession`
- `RemoteAllocation`
- `HookHandle`
- `VmtManager`
- `InjectedModuleHandle`

Explicit `Dispose()` performs deterministic cleanup. When native cleanup definitively fails, the ownership API surfaces that failure instead of silently discarding still-live resource state.

Finalizers never perform dangerous remote memory release, remote code restoration, VMT restoration, or remote module unload operations on the GC thread.

## Results and exceptions

v1.0 distinguishes normal misses from actual operation failures.

| Case | Behavior |
| --- | --- |
| Process / Module / Segment miss | `null` |
| Symbol / Scan / DeepPointer miss | libmem bad-address sentinel |
| Definite Manager native failure | `LibmemException` |
| Invalid arguments | standard .NET `Argument*` exceptions |
| Manager use after disposal | `ObjectDisposedException` |

See [API Reference](docs/API.md) for the full contract.

## Build from source

Requirements:

- Windows x64
- Visual Studio 2022
- Desktop development with C++
- C++/CLI support for v143 build tools
- Windows SDK
- .NET 8 SDK
- CMake
- Git

Clone recursively:

```powershell
git clone --recursive https://github.com/CardResearchLab/Libmem.NET.git
cd Libmem.NET
.\build.ps1 -Configuration Release -Platform x64
```

The build initializes Git submodules, builds the pinned native libmem revision, builds the C++/CLI assembly, generates XML documentation, and writes outputs under `artifacts/`.

Primary outputs:

```text
artifacts/native/x64/Release/bin/libmem.dll
artifacts/managed/x64/Release/Libmem.NET.dll
artifacts/managed/x64/Release/Libmem.NET.xml
artifacts/managed/x64/Release/Ijwhost.dll
```

## Runtime package

The **published 2.5.0 source** uses `VERSION` / informational version `2.5.0` and assembly/file version `2.5.0.0`. [v2.5.0 GitHub Release](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v2.5.0) includes the x64/x86 runtime ZIPs, checksums and `Libmem.NET.2.5.0.nupkg`.

Build the release-style runtime ZIP locally:

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\eng\package-runtime.ps1 -Configuration Release -Platform x64
```

Outputs:

```text
artifacts/package/Libmem.NET-windows-x64/
artifacts/package/Libmem.NET-windows-x64.zip
artifacts/package/Libmem.NET-windows-x64.zip.sha256
```

`manifest.json` records version, repository commit, pinned libmem commit, target framework, platform, build configuration, and per-file SHA-256.

## Git submodule integration

For source-level reproducible integration:

```powershell
git submodule add https://github.com/CardResearchLab/Libmem.NET.git external/Libmem.NET
git submodule update --init --recursive
```

Then reference:

```text
external/Libmem.NET/src/Libmem.NET.vcxproj
```

from the consuming solution.

The repository also provides a reusable GitHub Actions build workflow.

```yaml
jobs:
  build-libmem:
    uses: CardResearchLab/Libmem.NET/.github/workflows/reusable-build.yml@main
    with:
      ref: main
      configuration: Release
      platform: x64
      artifact-name: Libmem.NET-windows-x64
```

For reproducible builds, pin both the workflow reference and `ref` to a reviewed commit.

## NuGet status

The package ID is `Libmem.NET`, targeting Windows x64/x86 / .NET 8. CI validates multi-architecture pack composition, independent x64 and x86 PackageReference restore/build/run/publish, architecture-matched runtime asset copy, and explicit AnyCPU rejection.

Create local packages with `eng/package-nuget.ps1`; development versions include the commit identifier. `v*` tags create GitHub downloads, marking preview versions as prereleases. A later manual run on the published tag with `publish-nuget` enabled performs NuGet OIDC login and push. `release/v*` branches validate packages and render release notes without publication.

**2.5.0** was published by GitHub Release #51 and separate NuGet Trusted Publishing/OIDC Release #52. This PR advances the default public NuGet Smoke baseline to **2.5.0**; independent published-feed x64/x86 consumer checks passed in PR #148's Smoke #48; verify main smoke after merge. Historical 2.4.1 public smoke #40/#41 remains documented. See [Consumption Guide](docs/CONSUMPTION.md) and [Release Checklist](docs/RELEASE_CHECKLIST.md).

## Tests and CI

The repository uses layered validation:

- Build + Runtime Smoke
- Hook / VMT Runtime Tests
- Injector Runtime Tests
- External Process Runtime Tests
- NuGet Consumer Tests
- Public API baseline validation
- pinned libmem public API coverage validation
- runtime package integrity validation

Build is the single automatic PR gate. It builds and validates **Release x64 and Release x86** by default, retaining all Release suites and multi-architecture package checks above. Enable `debug` under Actions → Build → Run workflow to additionally build Debug x64 and run Debug smoke tests.

Specialized workflows remain manual entry points:

- [Hook/VMT](.github/workflows/hook-vmt-tests.yml), with manual x64 / x86 selection
- [Injector](.github/workflows/injector-tests.yml), with manual x64 / x86 selection
- [External Process](.github/workflows/external-process-tests.yml), using the independent `Libmem.NET.TestTarget`
- [NuGet Consumer](.github/workflows/nuget-consumer-tests.yml)

## Public API stability

The baseline enforces public member contracts. The current `LibmemCli` → `Libmem.NET` identity migration is an intentional breaking change requiring recompilation; member signatures, ownership, and error semantics are preserved. Historical v1.x compatibility does not imply that the new assembly can replace the old DLL.

The repository freezes namespace, public types, methods, properties, and enums through:

```text
api/Libmem.NET.PublicApi.txt
```

Any intentional breaking change requires an explicit API baseline update, CHANGELOG entry, semantic-versioning review, and relevant validation.

## Pinned upstream

The native backend is currently pinned to:

```text
rdbo/libmem
a07c9942bf1358dabcc83eb0cd072736c749d7f8
```

See [UPSTREAM.txt](UPSTREAM.txt).

## Documentation

- [API Reference](docs/API.md)
- [Migration Guide](docs/MIGRATION.md)
- [Release Checklist](docs/RELEASE_CHECKLIST.md)
- [Consumption Guide](docs/CONSUMPTION.md)
- [Release & Versioning Guide](docs/RELEASES.md)
- [v1.0.0 Release Notes](docs/releases/v1.0.0.md)
- [CHANGELOG](CHANGELOG.md)
- [ROADMAP](ROADMAP.md)
- [简体中文 README](README.md)

## License

Libmem.NET is licensed under [GNU AGPL-3.0-only](LICENSE).

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for third-party components and licenses.
