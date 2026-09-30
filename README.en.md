# LibmemCli

[简体中文](README.md) | [English](README.en.md)

[![CI Build](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml/badge.svg)](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/HearthstoneModding/Libmem)](https://github.com/HearthstoneModding/Libmem/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4)

**LibmemCli** is a Windows C++/CLI wrapper around [rdbo/libmem](https://github.com/rdbo/libmem), exposing process, thread, module, memory, scanning, symbol, assembly/disassembly, Hook, VMT, and DLL injection capabilities to C# / .NET.

Current stable release: **v1.0.0**

Official support target:

- Windows x64
- .NET 8
- C# / .NET consumers
- a pinned rdbo/libmem native backend

> x86 source/build configuration is retained but is outside the current stable support and official Release scope. NuGet remains a local/CI prototype; the GitHub Release ZIP is the primary stable binary distribution.

## Download

Recommended stable release:

- [LibmemCli v1.0.0](https://github.com/HearthstoneModding/Libmem/releases/tag/v1.0.0)
- [LibmemCli-windows-x64.zip](https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip)
- [LibmemCli-windows-x64.zip.sha256](https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256)

v1.0.0 ZIP SHA-256:

```text
647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99
```

See [Release & Versioning Guide](docs/RELEASES.md) for full release and integrity information.

## Quick start

Extract the Release ZIP, reference this assembly from an x64 .NET 8 project:

```text
LibmemCli.dll
```

and keep at least these files beside the application:

```text
LibmemCli.dll
libmem.dll
Ijwhost.dll
```

Keep `LibmemCli.xml` as well for IntelliSense documentation.

### Basic example

```csharp
using LibmemCli;

var process = Libmem.CurrentProcess()
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
using LibmemCli;

using var session = ProcessSession.Open(Environment.ProcessId)
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

```text
ProcessSession
├── Memory      MemoryManager
├── Modules     ModuleManager
├── Threads     ThreadManager
├── Scanner     ScanManager
├── Symbols     SymbolManager
├── Assembly    AssemblyManager
├── Hooks       HookManager
└── Injector    InjectorManager
```

The lower-level static `Libmem.*` surface remains available for one-shot calls and native-style compatibility.

### Process / Thread

- process enumeration, lookup, and current-process access
- process liveness checks
- PID + start-time identity validation
- thread enumeration and main-thread lookup

### Modules / Symbols

- module enumeration, lookup, load, and unload
- exported symbol enumeration
- symbol address lookup
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
git clone --recursive https://github.com/HearthstoneModding/Libmem.git
cd Libmem
.uild.ps1 -Configuration Release -Platform x64
```

The build initializes Git submodules, builds the pinned native libmem revision, builds the C++/CLI assembly, generates XML documentation, and writes outputs under `artifacts/`.

Primary outputs:

```text
artifacts/native/x64/Release/bin/libmem.dll
artifacts/managed/x64/Release/LibmemCli.dll
artifacts/managed/x64/Release/LibmemCli.xml
artifacts/managed/x64/Release/Ijwhost.dll
```

## Runtime package

Build the release-style runtime ZIP locally:

```powershell
.uild.ps1 -Configuration Release -Platform x64
.engpackage-runtime.ps1 -Configuration Release -Platform x64
```

Outputs:

```text
artifacts/package/LibmemCli-windows-x64/
artifacts/package/LibmemCli-windows-x64.zip
artifacts/package/LibmemCli-windows-x64.zip.sha256
```

`manifest.json` records version, repository commit, pinned libmem commit, target framework, platform, build configuration, and per-file SHA-256.

## Git submodule integration

For source-level reproducible integration:

```powershell
git submodule add https://github.com/HearthstoneModding/Libmem.git external/Libmem
git submodule update --init --recursive
```

Then reference:

```text
external/Libmem/src/LibmemCli.vcxproj
```

from the consuming solution.

The repository also provides a reusable GitHub Actions build workflow.

## NuGet status

The repository contains an unpublished Windows x64 / .NET 8 NuGet prototype:

```text
HearthstoneModding.LibmemCli
```

CI validates pack, PackageReference restore/build/run/publish, native runtime asset copy, and rejection of non-x64 consumers.

**v1.0.0 is not published to nuget.org.**

Stable consumption currently uses:

1. GitHub Release ZIP;
2. Git Submodule / source integration.

See [Consumption Guide](docs/CONSUMPTION.md).

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

Specialized runtime workflows are separated from the baseline Build gate, and full validation remains available through manual workflow dispatch.

## Public API stability

Starting with v1.0, the public managed contract is backward-compatible by default.

The repository freezes namespace, public types, methods, properties, and enums through:

```text
api/LibmemCli.PublicApi.txt
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
- [Consumption Guide](docs/CONSUMPTION.md)
- [Release & Versioning Guide](docs/RELEASES.md)
- [v1.0.0 Release Notes](docs/releases/v1.0.0.md)
- [CHANGELOG](CHANGELOG.md)
- [ROADMAP](ROADMAP.md)
- [简体中文 README](README.md)

## License

LibmemCli is licensed under [GNU AGPL-3.0-only](LICENSE).

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for third-party components and licenses.
