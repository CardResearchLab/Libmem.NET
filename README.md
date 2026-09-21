# LibmemCli — libmem 5.x C++/CLI wrapper (Windows x64 / .NET 8)

This repository contains a reusable C++/CLI wrapper around rdbo/libmem's C ABI. It exposes every public function in the pinned libmem header, using managed models and managed byte arrays; overload pairs represent libmem's normal and `Ex` functions. The native library is a pinned Git submodule and is built automatically before the wrapper.

Official upstream: https://github.com/rdbo/libmem ; C API: https://github.com/rdbo/libmem/blob/master/include/libmem/libmem.h

## Requirements

- Windows x64.
- Visual Studio with **Desktop development with C++** and **C++/CLI support for the v143 build tools**.
- Windows SDK, .NET 8 SDK, CMake, and Git.

## Clone and build

Clone recursively so the pinned libmem source and its own dependencies are present:

```powershell
git clone --recursive https://github.com/HearthstoneModding/Libmem.git
cd Libmem
.\build.ps1 -Configuration Release
```

`build.ps1` initializes all submodules, builds the pinned native library, stages its headers/import library/runtime DLL, and builds the C++/CLI solution. `bootstrap.ps1` remains as a compatibility alias. Opening `LibmemCli.sln` and building `Debug|x64` or `Release|x64` performs the same native prerequisite build automatically.

Generated files are kept out of source directories:

```text
artifacts/native/x64/Release/bin/libmem.dll
artifacts/native/x64/Release/lib/libmem.lib
artifacts/managed/x64/Release/LibmemCli.dll
artifacts/managed/x64/Release/Ijwhost.dll
```

## Consume as a Git submodule

```powershell
git submodule add https://github.com/HearthstoneModding/Libmem.git external/Libmem
git submodule update --init --recursive
```

Add `external/Libmem/src/LibmemCli.vcxproj` to the consuming solution and reference it from the .NET 8 x64 project with a `ProjectReference`. Build the solution with full Visual Studio MSBuild so the C++/CLI toolchain is available. The project path and output paths are based on this repository rather than the consuming solution, so the submodule may be placed at any stable location.

At runtime, deploy the matching `LibmemCli.dll`, `Ijwhost.dll`, and `libmem.dll` beside the consuming executable. Do not mix outputs from different configurations or commits.

## API mapping

- `LM_EnumProcesses`, `LM_GetProcess`, `LM_GetProcessEx`, `LM_FindProcess`, `LM_IsProcessAlive`, `LM_GetCommandLine`, `LM_FreeCommandLine`, `LM_GetBits`, `LM_GetSystemBits` -> `Libmem` process methods.
- Thread/module/symbol/segment `LM_*` enums and find/get/load/unload methods -> `Libmem` corresponding methods. Enumerations return fully materialized `List<T>`.
- `LM_ReadMemory[Ex]`, `LM_WriteMemory[Ex]`, `LM_SetMemory[Ex]`, `LM_ProtMemory[Ex]`, `LM_AllocMemory[Ex]`, `LM_FreeMemory[Ex]`, `LM_DeepPointer[Ex]` -> paired methods with/without a `ProcessInfo` argument.
- `LM_DataScan[Ex]`, `LM_PatternScan[Ex]`, `LM_SigScan[Ex]` -> managed byte/string scanning methods.
- Assembly/disassembly API -> `Libmem.Assemble`, `Libmem.Disassemble`, `Libmem.CodeLength`, `Libmem.GetArchitecture`. Native assembly result buffers are freed after copying into managed memory.
- `LM_HookCode[Ex]`, `LM_UnhookCode[Ex]` -> `Libmem.HookCode` and disposable `HookHandle`. Native VMT API -> disposable `VmtManager`.

## Important behavior and limitations

1. **x64 only** in this sample project. Address arguments/results are `UInt64`; the library's failure sentinel `LM_ADDRESS_BAD` is `UInt64.MaxValue` on x64. `0` is not an error sentinel for all APIs. No automatic permission elevation, remote architecture translation, or kernel-memory support is offered.
2. `ReadMemory` returns **only bytes actually read**. `WriteMemory` returns actual written length. Check full-length writes and short reads; a zero-byte result may indicate an inaccessible address.
3. `ProcessInfo` and `ModuleInfo` are snapshots, not operating-system handles. The process may exit, and addresses/modules can become stale. `IsProcessAlive` checks original identity (`pid` + startup time).
4. `GetCommandLine` returns UTF-8 strings and frees native allocations. The string helper rejects embedded NULs. Callback enumeration is synchronous.
5. `Disassemble(codeAddress, arch,...)` expects a pointer to readable machine code in the **calling process**, not a remote process address. For remote code, call `ReadMemory`, then pass the returned byte array to the safe pinned-buffer `Disassemble(byte[],...)` overload.
6. Hooks require executable native targets and replacements with the correct calling convention, signature, architecture, and lifetime. A **C# delegate address is not automatically a safe detour**. When installing a remote hook, `destination` must refer to code in the **remote process**; this wrapper does not inject it for you. Dispose `HookHandle` explicitly while code and target process are valid. Its finalizer intentionally does not restore code from a GC thread.
7. VMT manager is **local-process only**. Dispose it while the original vtable is valid; do not use it with arbitrary/untrusted addresses. Internal VMT entries are not automatically thread-synchronized with concurrent modifications.
8. Allocation/modify/free methods are low-level and require matching region sizes and protection. Some native APIs work at page granularity. No implicit `VirtualFree` or permission restoration occurs on `ProcessInfo` disposal because it does not own such resources.
9. `libmem.dll` and the matching .NET C++/CLI `Ijwhost.dll` are required beside the consuming executable.

## License

This wrapper repository is distributed under **GNU AGPL-3.0-only**. The pinned upstream libmem submodule uses the same license. See `LICENSE`, `THIRD_PARTY_NOTICES.md`, and the upstream submodule for notices and corresponding source.
