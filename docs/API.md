# Libmem.NET API Reference

> Target: Windows x64/x86 / .NET 8  
> Scope: general-purpose managed wrapper over the pinned rdbo/libmem C ABI.

This document focuses on **consumer-facing behavior**. Build instructions, project architecture, release packaging, and contributor workflows remain in the root README and roadmap.

## Recommended entry point

New code should prefer `ProcessSession.Open(...)`.

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

using var session = ProcessSession.Open((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Target process was not found.");

Console.WriteLine($"{session.Name} PID={session.Pid}");
```

`NativeApi.Attach(...)` and the static `NativeApi.*` methods remain available as compatibility APIs, but session-bound Managers are the preferred surface when target binding, ownership, or lifecycle semantics matter.

## ProcessSession model

```text
ProcessSession
├─ Memory      -> MemoryManager
├─ Modules     -> ModuleManager
├─ Threads     -> ThreadManager
├─ Scanner     -> ScanManager
├─ Symbols     -> SymbolManager
├─ Assembly    -> AssemblyManager
├─ Hooks       -> HookManager
└─ Injector    -> InjectorManager
```

A session binds to one concrete process identity using:

- PID
- process start time

This prevents a recycled PID from silently becoming a different target.

`ProcessSession` does not own a native Windows process handle. It is a managed lifetime and aggregation boundary around libmem operations.

## Core process types

### ProcessInfo

A managed description of one native process.

Important fields:

- `Pid`
- `ParentPid`
- `Architecture`
- `Bits`
- `StartTime`
- `Name`
- `Path`

`ProcessInfo` is intentionally a **read-only identity/metadata object** created by Libmem.NET. Consumers cannot rewrite its PID, start time, architecture, name, path, or other identity fields after creation. Its only behavior method is `IsAlive()`, which checks the exact PID + start-time identity.

This immutability is part of the frozen identity contract: an object returned by `GetProcess`, `FindProcess`, `EnumProcesses`, `Refresh`, or `ProcessSession.Info` continues to describe the same captured process identity for its lifetime. Consumers resolve a new `ProcessInfo` instead of mutating an existing one.

Memory and scan operations are not exposed on `ProcessInfo`. Use `ProcessSession.Memory` / `ProcessSession.Scanner` for session-bound operations, or the static `NativeApi.*` compatibility facade for one-shot calls.

### ProcessSession.Refresh

`Refresh()` returns updated metadata only while the original PID + start-time identity is still valid.

Return behavior:

- matching process identity -> updated `ProcessInfo`
- process exited -> `null`
- PID reused by another process -> `null`

### ProcessSession.IsAlive

Checks the exact attached process identity, not only whether the PID currently exists.

### ProcessSession.Allocate

`ProcessSession.Allocate(size, protection)` is intentionally retained as a session-level ownership convenience. It delegates to the session's `MemoryManager.Allocate` contract and returns a `RemoteAllocation` owning handle.

Both entry points therefore share the same allocation, liveness, exception, and disposal semantics. A dead target is rejected with `InvalidOperationException`; a definite native allocation failure is surfaced as `LibmemException`.

### ProcessSession lifetime

`Detach()` and `Dispose()` are idempotent. After the first detach/dispose, session members and previously obtained session-bound Managers reject operational use with `ObjectDisposedException`. Independently owned resource handles keep their own lifetime and are not bulk-cleaned by session detachment.

### Target process exit

Target exit does **not** implicitly detach or dispose a `ProcessSession`. The session remains an identity object for the process it originally bound to:

- `IsDisposed` remains `false` until `Detach()` / `Dispose()` is called;
- `Pid`, `Name`, `Architecture`, `Bits`, and `Info` remain readable from the bound identity;
- `IsAlive()` returns `false`;
- `Refresh()` returns `null`;
- Manager properties remain accessible.

Libmem.NET intentionally does not add an exact-identity liveness preflight to every Manager operation. For external processes, exact PID + start-time validation requires process enumeration; doing that before every read/write/scan would add material overhead and still could not eliminate the race between a preflight and the native operation.

Manager operations therefore keep their documented per-operation result/error semantics after target exit unless the method has an explicit managed liveness precondition. In the current frozen contract, `MemoryManager.Allocate`, `ProcessSession.Allocate`, static `AllocateMemory(process, ...)`, and `InjectorManager.InjectLibrary` reject a dead or stale target with `InvalidOperationException`. `MemoryManager.Free` and static `FreeMemory(process, ...)` return `false` if the target PID + start-time identity is no longer alive, rather than passing a stale PID to native `LM_FreeMemoryEx`. Remote `ProtectMemory(process, ...)` and `HookCode(process, ...)` (including their session Manager entry points) reject stale identities with `InvalidOperationException` before changing page protection or installing a hook; remote `UnloadModule(process, module)` (including `ModuleManager.Unload`) returns `false` after module provenance validation if its owner identity is stale. Remote `LoadModule(process, path)` (including `ModuleManager.Load`) rejects a stale identity with `InvalidOperationException` before attempting LoadLibrary. These safety preflights do not eliminate the race between validation and the native PID-based operation, nor do they add liveness checks to hot-path reads, writes or scans. Independently owned handles keep their separate target-exit cleanup semantics.

## Manager APIs

### SegmentInfo

`SegmentInfo` is a library-created, read-only virtual-memory segment result. Consumers can inspect `Base`, `End`, `Size`, and `Protection`, but cannot construct an empty descriptor or rewrite the resolved segment metadata.

The type remains a pure query result; memory mutation continues through `MemoryManager` / the static compatibility facade rather than by editing a segment object.

### MemoryManager

Primary operations:

- `Read`
- `TryRead`
- `Write`
- `TryWrite`
- `ReadInt32`
- `WriteInt32`
- `ReadInt64`
- `WriteInt64`
- `ReadPointer`
- `WritePointer`
- `Set`
- `Protect`
- `Allocate`
- `Free`

Pointer resolution and scanning are owned by `ProcessSession.Scanner`. The temporary `MemoryManager` scan-forwarding aliases from earlier v0.x builds were removed before the v1.0 API freeze.

#### Read / Write result semantics

`Read(address, count)` returns only bytes actually read. The returned array may be shorter than `count`.

`Write(address, data)` returns the number of bytes actually written.

Short operations are therefore observable results and are not automatically converted into exceptions.

`TryRead(address, count, out byte[] data)` returns `true` exactly when all `count` bytes were transferred. Even when it returns `false`, `data` contains the bytes actually read, just like `Read`. `TryWrite(address, byte[] data, out int bytesWritten)` returns `true` exactly when every input byte was written; `bytesWritten` reports the actual transferred count even on `false`. Partial writes may already have changed the target process and are **not rolled back**. These helpers perform one underlying read or write operation each; they do not retry, change memory protection or make the transfer atomic.

Both helpers return `true` for valid zero-length operations (`data = Array.Empty<byte>()` for `TryRead`, `bytesWritten = 0` for `TryWrite`). Invalid count, null input, unrepresentable x86 addresses and detached sessions keep the underlying `Read` / `Write` exception semantics; ordinary incomplete transfers are reported as `false`, not `LibmemException`. These are additive unreleased 2.5.0 APIs, not present in 2.4.1 packages.

Typed `ReadInt32`/`WriteInt32` and `ReadInt64`/`WriteInt64` require a complete 4- or 8-byte transfer and throw `LibmemException` on a short transfer. `ReadPointer` and `WritePointer` follow the target session's 32- or 64-bit pointer size (not `IntPtr.Size` of an unrelated caller), return zero-extended `ulong` values, and likewise require a complete transfer. `WritePointer` rejects a value above `uint.MaxValue` for 32-bit targets with `ArgumentOutOfRangeException("value")`. Only supported x86/x64 target architectures are accepted; byte order follows the Windows runtime's little-endian representation.

#### Zero-size memory operations

Zero is not treated uniformly across all memory APIs because the pinned Windows libmem ABI assigns different meanings to it.

- `Read(address, 0)` / static `ReadMemory(..., 0)` -> empty byte array.
- `TryRead(address, 0, out data)` -> `true` and an empty byte array.
- `Write(address, Array.Empty<byte>())` / static `WriteMemory(..., empty)` -> `0` bytes written.
- `TryWrite(address, Array.Empty<byte>(), out bytesWritten)` -> `true` with `bytesWritten = 0`.
- `Set(address, value, 0)` / static `SetMemory(..., 0)` -> `0` bytes set.
- `Protect(address, 0, protection)` preserves the pinned Windows libmem behavior where zero means one system page.
- Static `NativeApi.AllocateMemory(0, protection)` preserves the pinned Windows libmem behavior where zero requests one system page.
- `MemoryManager.Allocate(0, protection)` throws `ArgumentOutOfRangeException("size")` because the owned `RemoteAllocation` contract requires a non-zero managed size.
- On the pinned Windows implementation, `FreeMemory(..., size)` releases the whole region with `MEM_RELEASE`; the supplied size is not a release length.

These differences are intentional parts of the managed contract rather than candidates for blanket normalization.

#### Allocate

```csharp
using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);
```

A definite native allocation failure throws:

```text
LibmemException
Operation = "LM_AllocMemoryEx"
```

### ScanManager

Primary operations:

- `DeepPointer`
- `DataScan` / `TryDataScan`
- `PatternScan` / `TryPatternScan`
- `SigScan` / `TrySigScan`

Existing `DataScan`, `PatternScan`, and `SigScan` methods keep their original libmem-style miss sentinel semantics; a normal miss is not an exception. On x64, the bad-address sentinel corresponds to `UInt64.MaxValue`; on x86 it is `UInt32.MaxValue` widened to `UInt64`.

The **2.5.0 additive `Try*` overloads** return `true` and set `out ulong foundAddress` to the matching address, or return `false` and set it to `0` for a normal miss (including an empty scan window). They call the same underlying remote scan logic, including the final legal candidate fallback. Invalid data/signatures/masks, invalid address ranges and detached sessions retain their existing exception behavior. No additional remote reads, preflight, global state, native ABI change or scanner algorithm is introduced by the `Try*` surface.

```csharp
if (session.Scanner.TryDataScan(pattern, startAddress, scanLength, out ulong matchAddress))
{
    Console.WriteLine($"Match at 0x{matchAddress:X}");
}
```


### ModuleInfo

`ModuleInfo` is a library-created, read-only description of one native module mapping. Consumers can read `Base`, `End`, `Size`, `Name`, and `Path`, but cannot construct an empty descriptor or rewrite those values after Libmem.NET resolves the module.

This matters because a resolved `ModuleInfo` may later be passed back to module unload and symbol APIs. Freezing the descriptor prevents consumer-side mutation from silently changing the native module record used by those operations.

Libmem.NET also tracks the originating process identity internally. Session-bound `ModuleManager.Unload` and `SymbolManager` operations, plus the static `UnloadModule` overloads, reject a `ModuleInfo` captured from a different PID + start-time identity with `ArgumentException("module")`. This validation is an in-memory identity comparison and does not enumerate modules or add a target-liveness preflight. The internal provenance is intentionally not exposed as additional public `ModuleInfo` metadata.

### ModuleManager

Primary operations:

- `Enumerate`
- `Find`
- `Load`
- `Unload`

Result behavior:

- `Find` miss -> `null`
- definite `Load` failure -> `LibmemException("LM_LoadModuleEx", ...)`
- `Unload` -> explicit `bool` result

### ThreadInfo

`ThreadInfo` is a library-created, read-only description of one native thread record. Consumers can inspect `Id` and `OwnerPid`, but cannot construct an empty descriptor or rewrite those values after Libmem.NET resolves the thread.

This matters because a resolved `ThreadInfo` may later be passed to `NativeApi.GetThreadProcess(ThreadInfo)`. Freezing the descriptor prevents consumer-side mutation from silently changing the native thread record used by that lookup.

### ThreadManager

Primary operations:

- `Enumerate`
- `Main`

The wrapper intentionally exposes only thread capabilities available in the pinned libmem API. It does not invent managed Suspend / Resume / Context APIs.

### SymbolInfo

`SymbolInfo` is a library-created, read-only symbol result. Consumers can inspect `Address` and `Name`, but cannot construct an empty symbol descriptor or rewrite the resolved values after enumeration.

`SymbolInfo` is a result model only; symbol lookup input remains the module plus symbol-name arguments exposed by `SymbolManager` / the static compatibility facade.

### SymbolManager

Primary operations:

- `Enumerate`
- `FindAddress`
- `TryFindAddress`
- `Demangle`

`ModuleInfo` remains a value object; symbol behavior belongs to `SymbolManager`.

`TryFindAddress(module, name, demangle, out ulong address)` returns `true` with the resolved address or `false` with `address = 0` for a normal native bad-address miss. It does not swallow bad arguments, stale/foreign module identities, or target-session disposal. The existing `FindAddress` sentinel contract is unchanged.

### InstructionInfo

`InstructionInfo` is a Libmem.NET-created, deeply read-only assembly/disassembly result. `Address`, `Size`, `Mnemonic`, and `OperandString` are getter-only.

`Bytes` is also getter-only and returns a defensive copy. Mutating the returned `byte[]` therefore does not modify the instruction state retained by the `InstructionInfo` instance.

### AssemblyManager

Primary operations:

- `Assemble`
- `Disassemble(byte[] ...)`
- `Disassemble(address ...)`
- `CodeLength`
- `ReadAlignedCode`

The session's target architecture is used automatically.

The address-based `Disassemble` overload first reads bytes through the current session and then disassembles those bytes. It does not treat a remote address as a local pointer.

`ReadAlignedCode(address, minimumLength)` first uses the target architecture's native `CodeLength` to determine a complete-instruction span, then reads exactly that span through the target session. When the pinned native decoder requests bytes across a non-readable page boundary, the helper falls back to shrinking target-memory reads and decoding only the bytes successfully read (up to a 15-byte x86/x64 instruction). An instruction entirely within the readable region can still succeed; an incomplete instruction or a request reaching unreadable memory fails. Zero length returns an empty array. A nonzero query that cannot decode or fully read the range throws `LibmemException`. It is a **read-only inspection helper**, not an atomic snapshot, instruction-relocation engine, or patch installer. Callers must still synchronize target execution and validate address/protection before mutation.

Typical consumers can combine these *read-only* inspection operations with explicit native symbol lookup:

```csharp
var code = session.Assembly.ReadAlignedCode(sourceAddress, minimumLength: 5);
// code covers whole instructions, not necessarily exactly five bytes.
if (session.Symbols.TryFindAddress(module, exportedName, demangle: false, out var address))
    Console.WriteLine($"Export at 0x{address:X}");
```

The sample projects in `samples/Example.cs` and `samples/HookLifecycle/` demonstrate discovery, bounded inspection and explicit HookHandle removal. A successful code read alone does not certify trampoline relocation safety, concurrent thread safety or permission to modify the target.

Zero-size / empty-input behavior:

- `Disassemble(byte[0], ...)` returns an empty list.
- `CodeLength(..., minimumLength: 0)` returns `0`; zero is the natural result for a zero-length request, not a native failure.

Definite failures:

- `Assemble` -> `LibmemException("LM_AssembleEx", ...)`
- non-zero `CodeLength` query failure -> `LibmemException("LM_CodeLengthEx", ...)`

## Owned resources

Libmem.NET distinguishes a native operation result from a resource that has an explicit managed ownership lifetime.

### RemoteAllocation

Created by:

- `ProcessSession.Allocate`
- `MemoryManager.Allocate`

Owns one target-process allocation.

```csharp
using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);
```

Relevant state:

- `Address`
- `Size`
- `IsDisposed`

`Free()` exposes the release result directly. A successful release, a prior release, or target-process exit leaves the handle released; repeated `Free()` / `Dispose()` calls are idempotent. If native cleanup fails while the target is still alive, ownership is preserved so the caller can retry.

Explicit disposal performs deterministic cleanup. The finalizer does **not** mutate another process from the GC thread.

### HookHandle

Created by:

- `HookManager.Install`
- static `NativeApi.HookCode`

Relevant state:

- `Source`
- `Destination`
- `Trampoline`
- `PatchedBytes`
- `IsInstalled`
- `IsDisposed`

`Remove()` attempts explicit unhooking. After a successful removal, repeated `Remove()` / `Dispose()` calls are idempotent.

For a remote target, installation returns an owned handle only after native installation succeeds. If installation fails, `HookManager.Install` throws `LibmemException("LM_HookCodeEx", ...)` and no `HookHandle` is exposed.

`PatchedBytes` is the native relocation span consumed by libmem. It ends on an instruction boundary and can therefore be larger than the minimum jump encoding.

If explicit remote removal fails while the target is still alive, `Remove()` returns `false`, `IsInstalled` stays `true`, and ownership is retained so the caller can repair a transient condition and retry. Libmem.NET preflights the complete remote trampoline read before calling the pinned `LM_UnhookCodeEx` path so a definitely unreadable trampoline cannot leak a temporary source-page protection change.

Starting in the *unreleased 2.5.0* source, handles created through static **local-process** `NativeApi.HookCode(source, destination)` also preflight the complete trampoline with a safe self-process read before entering pinned `LM_UnhookCode`. The pinned local native function does not verify the return count of its source-code write; without this guard an unreadable trampoline could appear to have been successfully removed while the source remained patched. A failed preflight now returns `false` and retains the handle for retry, leaving the source page and redirection unchanged. This check is only a preflight: concurrent changes, unrelated native write errors, and trampoline relocation limitations remain possible. It does **not** make unhooking atomic or implement a new native hook engine.

If the remote target has exited, the target address space no longer exists; `Remove()` converges the handle to the released state and returns `true`. The finalizer never rewrites target code.

#### Hook conflict and trampoline boundaries

Libmem.NET does not maintain a global registry of installed code hooks. Installing multiple live hooks whose source ranges are identical or overlap is therefore not rejected by a managed conflict layer. Consumers must coordinate HookHandle ownership so concurrently installed hooks do not patch the same source bytes.

Trampoline relocation behavior is inherited from the pinned libmem implementation. Libmem.NET aligns `PatchedBytes` to complete instructions, but it does not rewrite copied PC-relative/relative-control-flow instructions in managed code. Consumers that hook functions whose overwritten span contains relative branches/calls or other location-sensitive instructions must validate that target against the pinned native backend rather than assuming every instruction sequence is trampoline-safe.

### InjectedModuleHandle

Created by:

- `InjectorManager.InjectLibrary`

Owns one load reference created by the injection call.

Relevant state:

- `Module`
- `RequestedPath`
- `IsActive`
- `IsDisposed`

`Unload()` releases the reference owned by this handle. After a successful release or target-process exit, repeated `Unload()` / `Dispose()` calls are idempotent.

A successful unload request does not guarantee the DLL disappears from the target process, because Windows DLL loading is reference-counted.

If the requested file exists but the target process cannot load it, or the load does not produce a module that can be resolved by the normalized requested full path, `InjectorManager.InjectLibrary` reports `LibmemException("LM_LoadModuleEx", ...)`. Module enumeration failures raised by `Libmem::EnumModules` remain `LM_EnumModulesEx`; a successful enumeration that simply cannot confirm the injected module is not misreported as an enumeration failure.

### VmtManager

Local-process-only VMT wrapper.

Primary operations:

- `Hook`
- `Unhook`
- `GetOriginal`
- `Reset`

The VTable and replacement code must remain valid throughout the manager lifetime.

Explicit disposal restores tracked entries deterministically. After successful cleanup, repeated `Dispose()` calls are idempotent. `Hook`, `Unhook`, `GetOriginal`, and `Reset` all throw `ObjectDisposedException` after disposal; for `Hook`, disposed-state precedence is checked before replacement-address validation. The finalizer does not rewrite VTable entries.

`VmtManager` is intentionally local-process only. The VTable storage and every replacement function pointer must remain valid for the manager lifetime.

If `Reset()` or `Dispose()` cannot restore a tracked entry, the operation surfaces the native restoration failure and retains bookkeeping/ownership instead of pretending cleanup succeeded. The manager remains available for a later retry after the underlying condition is repaired. Restoration is **not transactional**: in a multi-entry VTable, a reset may already have restored some entries before encountering an inaccessible slot. Successfully restored entries stay restored; the remaining tracked entries are retained for a retry. Unreleased 2.5.0 regression tests cover this ordering across a decommitted page boundary.

## Exception model

### LibmemException

Used only when Libmem.NET can determine that a native libmem operation **definitely failed**.

The `Operation` property carries the native operation name.

Example:

```csharp
try
{
    using var allocation = session.Memory.Allocate(
        4096,
        MemoryProtection.ReadWrite);
}
catch (LibmemException ex)
{
    Console.Error.WriteLine($"{ex.Operation}: {ex.Message}");
}
```

### Standard .NET exceptions

Libmem.NET uses standard .NET exception types for managed contract violations.

Examples:

- invalid argument -> `ArgumentException` / `ArgumentOutOfRangeException`
- null required argument -> `ArgumentNullException`
- using a detached/disposed session or manager -> `ObjectDisposedException`
- unsupported cross-bitness injection -> `NotSupportedException`

During the v0.9 freeze, public string identifiers/paths are normalized to the same managed contract: null values are rejected with `ArgumentNullException`, while empty or whitespace-only process names, module names, and module/library paths are rejected with `ArgumentException`. Embedded NUL characters are also rejected before UTF-8/native dispatch, and the resulting `ArgumentException.ParamName` reports the actual public parameter (for example `name`, `path`, `mask`, `signature`, or `code`) rather than the internal conversion helper's parameter name. `VmtManager` also treats a zero or libmem bad-address-sentinel VTable address as an invalid managed argument rather than retaining an invalid native VMT base.

Caller-supplied enums are validated at the managed boundary as well. Undefined `Architecture` values and `MemoryProtection` flag combinations containing unknown bits are rejected with `ArgumentOutOfRangeException` before native libmem is called.

## Normal non-exception results

Empty scan patterns and textual scan definitions are treated as managed argument errors rather than normal misses: empty data/pattern inputs, an empty pattern mask, and empty/whitespace signatures are rejected with `ArgumentException`. A valid non-empty scan that finds no match still returns the native bad-address sentinel.

The wrapper intentionally does **not** turn every unsuccessful result into an exception.

| Situation | Managed result |
| --- | --- |
| `FindProcess` miss | `null` |
| `ModuleManager.Find` miss | `null` |
| `FindSegment` miss | `null` |
| `FindSymbolAddress` miss | libmem bad-address sentinel |
| scan / `DeepPointer` miss | libmem bad-address sentinel |
| short read | shorter byte array |
| short write | actual byte count |
| explicit Free / Unload style operation | `bool` where the public API exposes a result |
| stale `ProcessSession.Refresh` identity | `null` |

This distinction is part of the public error contract.

## Static compatibility facade

`Libmem` remains the low-level compatibility facade over the pinned native ABI.

The compatibility facade intentionally preserves native-style failure values where practical. For example, static/process `LoadModule` returns `null` when native loading fails, while `ModuleManager.Load` promotes that same definite failure to `LibmemException("LM_LoadModuleEx", ...)`. Likewise, `HookCode` compatibility calls may return `null`, while `HookManager.Install` throws on installation failure. This split is intentional: the static surface tracks the pinned ABI closely, while Manager/ownership APIs provide stronger managed failure semantics.



It includes static process, thread, module, symbol, segment, memory, scan, assembly/disassembly, and Hook APIs.

Use static methods when a one-shot operation is appropriate.

Prefer `ProcessSession` when code performs multiple operations against one target or when ownership/lifetime semantics matter.

## Upstream compatibility workarounds

Libmem.NET pins a specific upstream libmem revision and documents wrapper-level workarounds where the pinned Windows implementation is unsafe or inconsistent.

### LM_GetCommandLine

The pinned Windows implementation has an unsafe current-process-only path.

Libmem.NET therefore:

- serves current-process arguments from `System.Environment`
- returns `null` for unsupported external-process command-line queries
- does not execute the unsafe native allocation/free path

### LM_GetProcessEx start time

The pinned Windows implementation populates external `LM_GetProcessEx(...).start_time` using `GetCurrentProcess()` instead of the opened target-process handle.

Libmem.NET still calls `LM_GetProcessEx` for native metadata/API coverage, then reconciles the external target's start time through `LM_EnumProcesses`.

This preserves the wrapper's PID + start-time identity model rather than weakening process identity checks.

### LM_DataScan / LM_PatternScan final candidate

The pinned common scan loops stop when the candidate pointer becomes the calculated end pointer instead of evaluating that final legal start position. This means a native exact-window scan where the scan size equals the pattern length reports a miss, and matches located only at the last legal candidate are also skipped. `LM_SigScan` inherits the same boundary through `LM_PatternScan`.

Libmem.NET preserves the pinned scanner for its original candidate order, then checks the omitted final candidate only when the native scan reports no match. Local fallback reads use the process-safe `LM_ReadMemoryEx` path against the current process; external scans read the same final candidate from the bound target. The wrapper also rejects scan ranges whose final candidate calculation would wrap the current architecture-width address space.

This is an internal compatibility correction; the public scan API and bad-address-sentinel miss semantics are unchanged.

### LM_VmtUnhook untracked-index protection

The pinned `LM_VmtUnhook` changes the target VTable slot page to executable/read/write protection before checking whether the index is present in its hook-entry list. If the index is not tracked, upstream returns success without restoring the previous protection.

Libmem.NET preserves the existing idempotent `VmtManager.Unhook` contract by checking its tracked-entry list first. An untracked index returns `true` without entering the pinned native path, so the VTable page protection is left unchanged.

## x64 policy

Current supported/default target:

```text
Windows x64
.NET 8
```

Windows x86 is a first-class 2.2.0 release architecture alongside x64. Architecture-specific mixed-mode/native assets are selected from explicit `Platform` / `PlatformTarget`; AnyCPU remains unsupported.

## IntelliSense documentation

Runtime packages include:

```text
Libmem.NET.dll
Libmem.NET.xml
Ijwhost.dll
libmem.dll
```

Keep `Libmem.NET.xml` beside `Libmem.NET.dll` so Visual Studio / C# editors can load generated IntelliSense documentation.

## Distribution and consumption

The current Libmem.NET 2.2.0 stable release targets Windows x64/x86 / .NET 8 through runtime ZIP, source/reusable-workflow integration, and a validated multi-architecture `Libmem.NET` NuGet package path. Historical v1.0.0 binaries retain the `LibmemCli` identity; see [MIGRATION.md](MIGRATION.md).

Independent PackageReference consumers validate the published NuGet package on both x64 and x86. Published stable `Libmem.NET 2.2.0` is the current public nuget.org baseline; 2.2.0 preserves the 2.1.1 Public API while adding official x86 delivery. Public publication uses Trusted Publishing (OIDC) as an explicit step after release-branch validation and GitHub Release creation; `release/v*` branches validate packages and notes without publishing.

See [CONSUMPTION.md](CONSUMPTION.md) for package layout, architecture constraints, release gating, and Trusted Publishing setup.

## Public API stability

The repository maintains:

```text
api/Libmem.NET.PublicApi.txt
```

CI compares the public declarations in `src/Libmem.NET.h` with that baseline.

Intentional public API changes must update the baseline and changelog explicitly. Accidental signature drift fails validation.


## Hook consumer example

A complete buildable x64 example lives in `samples/HookLifecycle`. It demonstrates:

- allocating executable source and destination stubs in the current process;
- installing through `ProcessSession.Hooks`;
- calling the trampoline to reach original behavior;
- explicit `Remove()` and idempotent cleanup;
- handling definite installation failure through `LibmemException`.

The example deliberately uses self-process generated code. Applications that hook another process are responsible for ensuring source/destination addresses are valid executable addresses in that target and for coordinating target lifetime.
