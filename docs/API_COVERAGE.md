# Native → Managed API coverage (v2.3.0 audit)

## Reference

- Pinned upstream: [rdbo/libmem](https://github.com/rdbo/libmem), commit `a07c9942bf1358dabcc83eb0cd072736c749d7f8`.
- Source of truth: `third_party/libmem/include/libmem/libmem.h`.
- Audit: `python eng/audit-native-api.py` (run after initializing submodules).
- Status as of 2026-10-08: **71 upstream C API function declarations, 69 directly referenced by wrapper C++ sources, 2 intentional substitutions**, 0 unexplained omissions. This is a *native reachability* audit, not a claim that every C function is independently exposed as a distinct managed method.

## Functional groups

| Native C API | Managed surface / handling |
| --- | --- |
| Process + command line | `Libmem`, `ProcessInfo`, `ProcessSession`; self command line through `.NET Environment` workaround |
| Thread | `Libmem` + `ThreadManager` |
| Module | `Libmem` + `ModuleManager`; injection lifetime through `InjectorManager` |
| Symbols | `Libmem` + `SymbolManager`; native demangled-name memory is released internally |
| Segments / memory | `Libmem`, `MemoryManager`, `RemoteAllocation` |
| Scans / pointers | `Libmem` + `ScanManager` |
| Assembler/disassembler | `Libmem` + `AssemblyManager`; native payload/instruction buffers are released internally |
| Code hooks / VMT | `Libmem`, `HookManager`, `HookHandle`, `VmtManager` |

### Deliberate command-line exceptions

- `LM_GetCommandLine` is **not invoked**: the pinned native Windows implementation is limited to self-process and has pointer/identity correctness risks. `Libmem.GetCommandLine` instead calls `Environment.GetCommandLineArgs()` for the current process, and returns `null` for another process.
- `LM_FreeCommandLine` is likewise **not invoked** because no native command-line buffer is allocated.

These exceptions are tracked explicitly in the audit script. Do not blindly replace the managed path with native calls when syncing upstream.

## Validation and interpretation

`audit-native-api.py` fails if a pinned native function is not referenced and lacks a documented exception. It intentionally does **not** certify behavior, exception semantics, x86/x64 ABI or release readiness. Those require real Windows runtime tests, checked public API baseline, NuGet consumer tests, and review of ownership / lifetime.

For 2.3.0 and 2.4.0, keep the pinned revision unchanged unless a separate upstream change is available and passes native ABI + both architecture regression checks. Do not add game/Mono-specific APIs.
