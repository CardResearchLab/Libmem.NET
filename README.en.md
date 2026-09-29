# LibmemCli — libmem 5.x C++/CLI wrapper (Windows x86/x64 / .NET 8)

[简体中文](README.md) | [English](README.en.md)

[![CI Build](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml/badge.svg)](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows x86/x64](https://img.shields.io/badge/Windows-x86%20%7C%20x64-0078D4)


LibmemCli is a reusable C++/CLI wrapper around the C ABI of [rdbo/libmem](https://github.com/rdbo/libmem), intended for Windows x86/x64 / .NET 8 projects.

Except for explicitly documented compatibility waivers, the wrapper covers the public functions in the pinned libmem header through managed models, managed byte arrays, and .NET-friendly APIs. Normal libmem functions and their `Ex` variants are generally represented as overload pairs.

The native libmem library is included as a pinned Git submodule and is built automatically before the C++/CLI wrapper.

- Upstream project: [rdbo/libmem](https://github.com/rdbo/libmem)
- C API: [include/libmem/libmem.h](https://github.com/rdbo/libmem/blob/master/include/libmem/libmem.h)


## Architecture

```mermaid
flowchart LR
    App["C# / .NET 8 x86/x64 project"] --> Cli["LibmemCli.dll<br/>C++/CLI managed wrapper"]
    Cli --> Native["libmem.dll<br/>rdbo/libmem"]
    Native --> Win["Windows native process / memory APIs"]

    Submodule["third_party/libmem<br/>Git Submodule"] --> NativeBuild["eng/build-native.ps1"]
    NativeBuild --> Native
    Native --> Build["build.ps1"]
    Cli --> Package["Runtime Package"]
    Native --> Package
```

The runtime call chain is **C#/.NET → LibmemCli.dll → libmem.dll → Windows Native API**. During builds, the pinned libmem submodule produces the native DLL first, followed by the C++/CLI managed wrapper.

## Requirements

- Windows x86 or x64
- Visual Studio with:
  - **Desktop development with C++**
  - **C++/CLI support for the v143 build tools**
- Windows SDK
- .NET 8 SDK
- CMake
- Git

## Clone and build

Clone recursively so the pinned libmem source and its dependencies are available:

```powershell
git clone --recursive https://github.com/HearthstoneModding/Libmem.git
cd Libmem
.\build.ps1 -Configuration Release
```

`build.ps1` will:

1. initialize all Git submodules;
2. build the pinned native libmem library;
3. stage native headers, import libraries, and runtime DLLs;
4. build the C++/CLI solution.

`bootstrap.ps1` remains available as a compatibility alias.

You can also open `LibmemCli.sln` directly and build `Debug|x64`, `Release|x64`, `Debug|x86`, or `Release|x86`. Visual Studio/MSBuild will perform the same native prerequisite build automatically.

Generated files are kept outside the source directories:

```text
artifacts/native/{x64|x86}/Release/bin/libmem.dll
artifacts/native/{x64|x86}/Release/lib/libmem.lib
artifacts/managed/{x64|x86}/Release/LibmemCli.dll
artifacts/managed/{x64|x86}/Release/Ijwhost.dll
```


## C# quick example

After referencing `LibmemCli.dll`, managed code can access process and module information directly:

```csharp
using LibmemCli;

var process = Libmem.CurrentProcess()
    ?? throw new InvalidOperationException("Current process not found");

using var session = Libmem.Attach(process)
    ?? throw new InvalidOperationException("Attach failed");

Console.WriteLine(
    $"Process: {process.Name}  PID={process.Pid}  Arch={process.Architecture}  Bits={process.Bits}");

foreach (var module in session.Modules.Enumerate())
{
    Console.WriteLine(
        $"{module.Name}  Base=0x{module.Base:X}  Size=0x{module.Size:X}");
}
```

At runtime, keep `LibmemCli.dll`, `Ijwhost.dll`, and `libmem.dll` beside the application executable.

## ProcessSession

`ProcessSession` is an optional general-purpose process context. It binds to one concrete process identity using **PID + process start time** and gives memory, module, hook, and injection calls for the same target an explicit Attach / Detach lifetime; it does not own application state:

```csharp
using var target = Libmem.Attach("Hearthstone.exe");

if (target is null)
    return;

Console.WriteLine($"{target.Name} PID={target.Pid} Arch={target.Architecture}");

if (!target.IsAlive())
    return;

var latest = target.Refresh();
```

`ProcessSession` does not own a native Windows process handle. `MemoryManager`, `ModuleManager`, `HookManager`, and `InjectorManager` only add target binding and necessary resource-lifetime constraints around libmem calls.

The static `Libmem.*` API remains directly usable. Applications that need snapshots, caches, event state, or game-state models should build those models in the caller rather than in LibmemCli.

### ModuleManager

`ProcessSession.Modules` exposes module operations bound to the target process:

```csharp
var modules = target.Modules;

foreach (var module in modules.Enumerate())
    Console.WriteLine($"{module.Name} 0x{module.Base:X}");

var unity = modules.Find("UnityPlayer.dll");
```

It currently provides `Enumerate / Find / Load / Unload`. Like `MemoryManager`, it follows the ProcessSession lifetime and rejects operations after Detach.

Remote loading accepts a positive millisecond timeout via `modules.Load(path, timeoutMilliseconds)`; the default is 10 seconds. A `TimeoutException` means that the remote loading thread may still be running, so callers must not retry or unload automatically. Modules are resolved by normalized full path, and session operations verify the target process identity.

### Injector

`ProcessSession.Injector` is the higher-level DLL injection API above `ModuleManager.Load`. Its purpose is to make ownership of one LoadLibrary reference explicit:

```csharp
using var injected = target.Injector.InjectLibrary(@"C:\Mods\NativeBootstrap.dll")
    ?? throw new InvalidOperationException("Injection failed");

Console.WriteLine($"0x{injected.Module.Base:X} {injected.Module.Name}");
```

`InjectLibrary` normalizes and validates the DLL path and rejects cross-bitness injection between the current runtime and target process. The returned `InjectedModuleHandle` preserves the managed module description and requested path. `IsActive` means **this handle still owns the load reference it created**; it does not claim that the module is the only loaded instance in the process.

`InjectLibrary(path, timeoutMilliseconds)` uses the same bounded loader, with a 10-second default. To keep a DLL loaded after a session ends, use `Modules.Load` instead of an `InjectedModuleHandle`, whose `Dispose()` releases its load reference.

Explicit `Unload()` returns the release result. `Dispose()` deterministically attempts to release the one `LoadLibrary` reference owned by the handle; if native cleanup fails, the failure is surfaced instead of silently marking live ownership as released. Because Windows DLLs are reference-counted and the pinned upstream `LM_UnloadModuleEx` only requests a release, a successful call does not guarantee the module disappears completely from the target process. The GC finalizer never calls `FreeLibrary` in the target process.

### HookManager

`ProcessSession.Hooks` binds hook installation to the current target process:

```csharp
using var hook = target.Hooks.Install(source, destination)
    ?? throw new InvalidOperationException("Hook failed");

Console.WriteLine($"source=0x{hook.Source:X} destination=0x{hook.Destination:X} trampoline=0x{hook.Trampoline:X}");
```

`HookManager` does not aggregate ownership of installed hooks. Each returned `HookHandle` independently owns its hook and trampoline. `Source / Destination / Trampoline / PatchedBytes` preserve installation metadata; `Remove()` exposes the restoration result, while `Dispose()` deterministically restores the original code and throws `LibmemException` on failure instead of silently marking a live hook as released. The finalizer never rewrites process code from the GC thread. `ProcessSession.Detach()` prevents new installations without bulk-removing handles already returned to callers.

### MemoryManager

`ProcessSession.Memory` groups target-process memory operations into one session-bound API:

```csharp
var memory = target.Memory;

using var buffer = memory.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Allocation failed");

memory.Write(buffer.Address, payload);
var copy = memory.Read(buffer.Address, payload.Length);
var hit = memory.SigScan("48 8B ?? ??", start, size);
```

The manager currently exposes Read / Write / ReadInt32 / WriteInt32 / Set / Protect / Allocate / Free / DeepPointer / DataScan / PatternScan / SigScan. It is bound to the `ProcessSession` lifetime; calls after the session is detached throw `ObjectDisposedException`.

### RemoteAllocation

`ProcessSession.Allocate(...)` now returns a disposable `RemoteAllocation`, making ownership of target-process memory explicit:

```csharp
using var memory = target.Allocate(4096, MemoryProtection.ReadWrite)
    ?? throw new InvalidOperationException("Allocation failed");

Console.WriteLine($"0x{memory.Address:X} / {memory.Size} bytes");
```

Calling `Free()` explicitly lets callers inspect the release result. Leaving the `using` scope makes `Dispose()` deterministically release the allocation; if native cleanup fails, the failure is surfaced instead of silently discarding ownership. If the target process has already exited, its address space is considered reclaimed by the OS. The finalizer never mutates another process from the GC thread.

## Consume as a Git submodule

Add this repository to another project as a submodule:

```powershell
git submodule add https://github.com/HearthstoneModding/Libmem.git external/Libmem
git submodule update --init --recursive
```

Then add:

```text
external/Libmem/src/LibmemCli.vcxproj
```

to the consuming solution and reference it from a matching-architecture .NET 8 project with a `ProjectReference`.

Build the full solution with Visual Studio MSBuild so the C++/CLI toolchain is available.

Project and output paths are based on this repository rather than the consuming solution, so the submodule may be placed at any stable location.

At runtime, deploy the following files beside the consuming executable:

- `LibmemCli.dll`
- `Ijwhost.dll`
- `libmem.dll`

Do not mix outputs from different configurations or commits.


## GitHub Actions automation

The repository includes five automation workflows:

- \`.github/workflows/build.yml\`: builds Release x64 and x86 on pushes to \`main\`, pull requests, or manual runs, then uploads separate \`LibmemCli-windows-x64\` and \`LibmemCli-windows-x86\` artifacts.
- \`.github/workflows/reusable-build.yml\`: exposes the build through \`workflow_call\` so other GitHub repositories can reuse it.
- \`.github/workflows/release.yml\`: builds both architectures for \`v*\` tags or \`release/v*\` release branches, verifies package provenance and SHA-256 integrity, creates a GitHub Release, and attaches both architecture ZIPs and checksum files.
- \`.github/workflows/hook-vmt-tests.yml\`: runs dedicated real Hook / trampoline / VMT lifecycle tests separately from the baseline smoke suite.
- \`.github/workflows/injector-tests.yml\`: independently validates DLL injection, module discovery, explicit Unload, and Dispose lifetime behavior.

You can create the same runtime package locally:

```powershell
.\build.ps1 -Configuration Release
.\eng\package-runtime.ps1 -Configuration Release
```

Output:

```text
artifacts/package/LibmemCli-windows-x64/
artifacts/package/LibmemCli-windows-x64.zip
artifacts/package/LibmemCli-windows-x64.zip.sha256

artifacts/package/LibmemCli-windows-x86/
artifacts/package/LibmemCli-windows-x86.zip
artifacts/package/LibmemCli-windows-x86.zip.sha256
```

## Versioning and automated validation

The root `VERSION` file is the source of truth for release versioning. The current version is **0.3.1**, and the generated `LibmemCli.dll` carries matching assembly version metadata.

Each runtime package contains a `manifest.json` recording:

- the LibmemCli package version;
- the repository Git commit;
- the pinned upstream libmem commit;
- target framework (`net8.0`);
- platform (`win-x64` or `win-x86`);
- build configuration (Debug / Release);
- the file name, byte length, and SHA-256 of every packaged file.

Packaging also runs the shared `eng/verify-package.py` verifier. It checks every manifest file entry, byte length, and SHA-256, confirms the ZIP contains exactly the packaged directory contents, and validates the external `.zip.sha256`. Release publication additionally requires the manifest `repositoryCommit` to match the Git commit being released, preventing a correctly versioned package from being published from the wrong commit.

CI validates more than compilation:

1. **API Contract Check** parses the pinned submodule's `include/libmem/libmem.h` and extracts every public `LM_API`. Except for compatibility waivers that are explicitly documented in source with their rationale, CI fails if upstream exposes a public API that the C++/CLI wrapper does not cover.
2. **Runtime Smoke Tests** load `LibmemCli.dll + libmem.dll` and cover process/command-line APIs, threads, modules/exported symbols, memory segments, allocation/read/write/set/protection, DeepPointer, Data/Pattern/Signature scanning, assembly/disassembly, and CodeLength. Controlled memory tests only touch isolated allocations in the test process itself.

Hook and VMT operations are intentionally kept out of the baseline smoke gate and validated by the separate `Hook VMT Runtime Tests` workflow. Explicit `VmtManager.Dispose()` also uses deterministic restoration: if any tracked VMT entry cannot be restored, the manager remains undisposed and throws `LibmemException` instead of discarding the remaining hook bookkeeping. It allocates isolated executable memory in the current process and verifies hook redirection, trampoline execution, Remove, and VMT Hook / Unhook / Reset / Dispose without depending on Hearthstone or any external process.

Injector behavior is also validated separately by `Injector Runtime Tests`. The test copies `libmem.dll` under a unique fixture name and performs real injection, module discovery, Unload, and Dispose against the current test process without depending on Hearthstone.

### Reuse the build from another repository

Another repository can call the reusable workflow directly:

```yaml
jobs:
  build-libmem:
    uses: HearthstoneModding/Libmem/.github/workflows/reusable-build.yml@main
    with:
      ref: main
      configuration: Release
      platform: x64
      artifact-name: LibmemCli-windows-x64

  use-libmem:
    needs: build-libmem
    runs-on: windows-2022
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: LibmemCli-windows-x64
          path: external/Libmem
```

The caller does not need to duplicate Libmem's build scripts; the artifact is uploaded directly to the caller's workflow run.

> While this repository is private, cross-repository reuse requires GitHub Actions access settings that allow the caller repository to use this reusable workflow. If the repository becomes public later, public repositories can reference it directly.

## API stability

The repository now commits a shared x86/x64 public API baseline at `api/LibmemCli.PublicApi.txt`. Every `tests/check_sources.py` run extracts the actual public types, properties, methods, and enum members from `src/LibmemCli.h` and compares them with that baseline.

Accidental removals, signature changes, public-member renames, or enum changes therefore fail CI. An intentional public API change must explicitly run:

```powershell
python .\eng\check-public-api.py --write
```

Then review the API diff, update `CHANGELOG.md`, and apply the appropriate version change. The project is still pre-1.0, so this is not a promise that breaking changes can never happen; it makes them explicit and reviewable instead of silent.

## Error model

When LibmemCli can determine that a **native libmem operation definitely failed**, it throws `LibmemException`. The type derives from `InvalidOperationException` and preserves the corresponding native operation name through the `Operation` property, for example `LM_EnumProcesses`, `LM_ProtMemoryEx`, or `LM_FreeMemoryEx`.

`Find*` operations, scan misses, and APIs where upstream libmem uses `null` / `LM_ADDRESS_BAD` as the normal “not found” result keep their existing return semantics. The wrapper does not turn ordinary misses into exceptions merely for uniformity.

Argument validation continues to use the standard .NET `ArgumentException` family, while lifetime misuse continues to use `ObjectDisposedException`.

## API mapping

### Processes

The following libmem APIs are exposed through `Libmem` process methods:

- `LM_EnumProcesses`
- `LM_GetProcess`
- `LM_GetProcessEx`
- `LM_FindProcess`
- `LM_IsProcessAlive`
- `LM_GetCommandLine`
- `LM_FreeCommandLine`
- `LM_GetBits`
- `LM_GetSystemBits`

> Compatibility note: `LM_GetCommandLine` / `LM_FreeCommandLine` are explicit waivers for the pinned Windows upstream revision. Managed `Libmem.GetCommandLine` preserves the intended contract without executing those unsafe native entry points.

### Threads, modules, symbols, and segments

Thread, module, symbol, and segment `LM_*` enumeration/find/get/load/unload APIs are mapped to their corresponding `Libmem` methods.

Enumeration results are fully materialized as managed `List<T>` values.

### Memory operations

The following APIs are exposed as overloads with or without a `ProcessInfo` argument:

- `LM_ReadMemory[Ex]`
- `LM_WriteMemory[Ex]`
- `LM_SetMemory[Ex]`
- `LM_ProtMemory[Ex]`
- `LM_AllocMemory[Ex]`
- `LM_FreeMemory[Ex]`
- `LM_DeepPointer[Ex]`

### Scanning

The following APIs are exposed as managed byte/string scanning methods:

- `LM_DataScan[Ex]`
- `LM_PatternScan[Ex]`
- `LM_SigScan[Ex]`

### Assembly and disassembly

Assembly/disassembly APIs are exposed through:

- `Libmem.Assemble`
- `Libmem.Disassemble`
- `Libmem.CodeLength`
- `Libmem.GetArchitecture`

Native assembly-result buffers are freed after being copied into managed memory.

### Hooks

- `LM_HookCode[Ex]` → `Libmem.HookCode`
- `LM_UnhookCode[Ex]` → disposable `HookHandle`

`HookHandle` now separates **whether the hook is still installed** from **whether the managed handle is disposed**:

- `Source / Trampoline / PatchedBytes` retain installation metadata;
- `IsInstalled` reports whether the handle still considers the target code hooked;
- `IsDisposed` reports whether the managed lifetime has ended;
- `Remove()` attempts to unhook and clears `IsInstalled` only on success;
- `Dispose()` deterministically attempts to unhook; if native restoration fails it throws `LibmemException` and leaves `IsInstalled=true` rather than silently reporting the live hook as released.

This prevents a failed removal from being reported as a successful unhook. The finalizer still never modifies target-process code from the GC thread.

The native VMT API is wrapped by the disposable `VmtManager`. In the pinned libmem revision, `LM_VmtReset` reads an entry index again after freeing that entry. `VmtManager.Reset / Dispose` therefore remove tracked entries one-by-one with `LM_VmtUnhook` first, then call the upstream Reset/Free only after the list is empty, avoiding that use-after-free path. The GC finalizer never rewrites VTable entries; if explicit `Dispose` is skipped while hooks remain active, a small amount of native bookkeeping may leak rather than mutating the table from the GC thread.

## Important behavior and limitations

1. **Current builds support Windows x86 and x64.** The managed public API continues to use `UInt64` for addresses/sizes, but conversion to native values is range-checked for the current pointer width. On x86, addresses or sizes above `UInt32.MaxValue` throw `ArgumentOutOfRangeException` instead of being silently truncated. `LM_ADDRESS_BAD` is `UInt64.MaxValue` on x64 and `UInt32.MaxValue` on x86. The wrapper does not provide cross-bitness remote translation; injection still requires the current runtime and target process to have matching bitness.

2. `ReadMemory` returns **only the bytes actually read**. `WriteMemory` returns the actual number of bytes written. Callers should check for short reads and partial writes. A zero-byte result may indicate an inaccessible address.

3. `ProcessInfo` and `ModuleInfo` are snapshots, not operating-system handles. A process can exit and module/address information can become stale. `IsProcessAlive` checks the original identity using `pid` plus startup time.

4. `GetCommandLine` currently supports the **current process only**. The pinned Windows upstream `LM_GetCommandLine` has undefined behavior at this revision, so LibmemCli does not invoke it; current-process arguments come from `System.Environment.GetCommandLineArgs()`, while other processes preserve the upstream unsupported behavior and return `null`. Enumeration callbacks are synchronous.

5. `Disassemble(codeAddress, arch, ...)` expects `codeAddress` to point to readable machine code in the **calling process**, not a remote-process address. For remote code, call `ReadMemory` first and pass the resulting byte array to the safe pinned-buffer `Disassemble(byte[], ...)` overload.

6. Hooks require executable native targets and replacements with the correct calling convention, signature, architecture, and lifetime. **A C# delegate address is not automatically a safe detour.** For a remote hook, `destination` must refer to code in the **remote process**; this wrapper does not inject that code for you. Dispose `HookHandle` explicitly while the target code and process are still valid. Its finalizer intentionally does not restore modified code from a GC thread.

7. The VMT manager is **local-process only**. Dispose it while the original vtable is still valid and do not use arbitrary or untrusted addresses. Internal VMT entries are not automatically synchronized with concurrent modifications.

8. Allocation, modification, and free operations are low-level APIs and require matching region sizes and protection settings. Some native APIs operate at page granularity. `ProcessInfo` does not own these allocations, so disposing it does not implicitly call `VirtualFree` or restore memory permissions.

9. Runtime deployment requires matching copies of:
   - `libmem.dll`
   - the .NET C++/CLI `Ijwhost.dll`

   beside the consuming executable.

## License

This C++/CLI wrapper repository is distributed under **GNU AGPL-3.0-only**.

The pinned upstream libmem submodule uses the same license.

See the following for details:

- `LICENSE`
- `THIRD_PARTY_NOTICES.md`
- the upstream libmem submodule license and corresponding source
