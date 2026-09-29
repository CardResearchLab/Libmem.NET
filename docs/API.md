# LibmemCli API Reference

> Target: Windows x64 / .NET 8  
> Scope: general-purpose managed wrapper over the pinned rdbo/libmem C ABI.

This document focuses on **consumer-facing behavior**. Build instructions, project architecture, release packaging, and contributor workflows remain in the root README and roadmap.

## Recommended entry point

New code should prefer `ProcessSession.Open(...)`.

```csharp
using LibmemCli;

using var session = ProcessSession.Open(Environment.ProcessId)
    ?? throw new InvalidOperationException("Target process was not found.");

Console.WriteLine($"{session.Name} PID={session.Pid}");
```

`Libmem.Attach(...)` and the static `Libmem.*` methods remain available as compatibility APIs, but session-bound Managers are the preferred surface when target binding, ownership, or lifecycle semantics matter.

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

`ProcessInfo` is intentionally an identity/metadata object. Its only behavior method is `IsAlive()`, which checks the exact PID + start-time identity.

Memory and scan operations are not exposed on `ProcessInfo`. Use `ProcessSession.Memory` / `ProcessSession.Scanner` for session-bound operations, or the static `Libmem.*` compatibility facade for one-shot calls.

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

LibmemCli intentionally does not add an exact-identity liveness preflight to every Manager operation. For external processes, exact PID + start-time validation requires process enumeration; doing that before every read/write/scan would add material overhead and still could not eliminate the race between a preflight and the native operation.

Manager operations therefore keep their documented per-operation result/error semantics after target exit unless the method has an explicit managed liveness precondition. In the current frozen contract, `MemoryManager.Allocate` and `InjectorManager.InjectLibrary` explicitly reject a dead target with `InvalidOperationException`. Independently owned handles keep their separate target-exit cleanup semantics.

## Manager APIs

### MemoryManager

Primary operations:

- `Read`
- `Write`
- `ReadInt32`
- `WriteInt32`
- `Set`
- `Protect`
- `Allocate`
- `Free`

Pointer resolution and scanning are owned by `ProcessSession.Scanner`. The temporary `MemoryManager` scan-forwarding aliases from earlier v0.x builds were removed before the v1.0 API freeze.

#### Read / Write result semantics

`Read(address, count)` returns only bytes actually read. The returned array may be shorter than `count`.

`Write(address, data)` returns the number of bytes actually written.

Short operations are therefore observable results and are not automatically converted into exceptions.

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
- `DataScan`
- `PatternScan`
- `SigScan`

Scan misses keep libmem-style sentinel semantics. A normal miss is not an exception.

On x64, the bad-address sentinel corresponds to `UInt64.MaxValue`.

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

### ThreadManager

Primary operations:

- `Enumerate`
- `Main`

The wrapper intentionally exposes only thread capabilities available in the pinned libmem API. It does not invent managed Suspend / Resume / Context APIs.

### SymbolManager

Primary operations:

- `Enumerate`
- `FindAddress`
- `Demangle`

`ModuleInfo` remains a value object; symbol behavior belongs to `SymbolManager`.

### AssemblyManager

Primary operations:

- `Assemble`
- `Disassemble(byte[] ...)`
- `Disassemble(address ...)`
- `CodeLength`

The session's target architecture is used automatically.

The address-based `Disassemble` overload first reads bytes through the current session and then disassembles those bytes. It does not treat a remote address as a local pointer.

Definite failures:

- `Assemble` -> `LibmemException("LM_AssembleEx", ...)`
- non-zero `CodeLength` query failure -> `LibmemException("LM_CodeLengthEx", ...)`

## Owned resources

LibmemCli distinguishes a native operation result from a resource that has an explicit managed ownership lifetime.

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
- static `Libmem.HookCode`

Relevant state:

- `Source`
- `Destination`
- `Trampoline`
- `PatchedBytes`
- `IsInstalled`
- `IsDisposed`

`Remove()` attempts explicit unhooking. After a successful removal, repeated `Remove()` / `Dispose()` calls are idempotent.

A failed explicit restoration does not silently mark the hook as released. The finalizer never rewrites target code.

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

### VmtManager

Local-process-only VMT wrapper.

Primary operations:

- `Hook`
- `Unhook`
- `GetOriginal`
- `Reset`

The VTable and replacement code must remain valid throughout the manager lifetime.

Explicit disposal restores tracked entries deterministically. After successful cleanup, repeated `Dispose()` calls are idempotent; operational methods after disposal throw `ObjectDisposedException`. The finalizer does not rewrite VTable entries.

## Exception model

### LibmemException

Used only when LibmemCli can determine that a native libmem operation **definitely failed**.

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

LibmemCli uses standard .NET exception types for managed contract violations.

Examples:

- invalid argument -> `ArgumentException` / `ArgumentOutOfRangeException`
- null required argument -> `ArgumentNullException`
- using a detached/disposed session or manager -> `ObjectDisposedException`
- unsupported cross-bitness injection -> `NotSupportedException`

During the v0.9 freeze, public string identifiers/paths are normalized to the same managed contract: null values are rejected with `ArgumentNullException`, while empty or whitespace-only process names, module names, and module/library paths are rejected with `ArgumentException`. `VmtManager` also treats a zero VTable address as an invalid managed argument rather than reporting it as a native `LM_VmtNew` failure.

## Normal non-exception results

The wrapper intentionally does **not** turn every unsuccessful result into an exception.

| Situation | Managed result |
| --- | --- |
| `FindProcess` miss | `null` |
| `ModuleManager.Find` miss | `null` |
| scan miss | libmem bad-address sentinel |
| short read | shorter byte array |
| short write | actual byte count |
| explicit Free / Unload style operation | `bool` where the public API exposes a result |
| stale `ProcessSession.Refresh` identity | `null` |

This distinction is part of the public error contract.

## Static compatibility facade

`Libmem` remains the low-level compatibility facade over the pinned native ABI.

It includes static process, thread, module, symbol, segment, memory, scan, assembly/disassembly, and Hook APIs.

Use static methods when a one-shot operation is appropriate.

Prefer `ProcessSession` when code performs multiple operations against one target or when ownership/lifetime semantics matter.

## Upstream compatibility workarounds

LibmemCli pins a specific upstream libmem revision and documents wrapper-level workarounds where the pinned Windows implementation is unsafe or inconsistent.

### LM_GetCommandLine

The pinned Windows implementation has an unsafe current-process-only path.

LibmemCli therefore:

- serves current-process arguments from `System.Environment`
- returns `null` for unsupported external-process command-line queries
- does not execute the unsafe native allocation/free path

### LM_GetProcessEx start time

The pinned Windows implementation populates external `LM_GetProcessEx(...).start_time` using `GetCurrentProcess()` instead of the opened target-process handle.

LibmemCli still calls `LM_GetProcessEx` for native metadata/API coverage, then reconciles the external target's start time through `LM_EnumProcesses`.

This preserves the wrapper's PID + start-time identity model rather than weakening process identity checks.

## x64 policy

Current supported/default target:

```text
Windows x64
.NET 8
```

Existing x86 source/build compatibility paths remain in the repository, but x86 is deferred and is not currently part of official Release assets or the default merge gate.

## IntelliSense documentation

Runtime packages include:

```text
LibmemCli.dll
LibmemCli.xml
Ijwhost.dll
libmem.dll
```

Keep `LibmemCli.xml` beside `LibmemCli.dll` so Visual Studio / C# editors can load generated IntelliSense documentation.

## Distribution and consumption

Stable distribution currently remains the Windows x64 Runtime ZIP and source/reusable-workflow integration.

The repository also contains an **unpublished local NuGet prototype** with the provisional package ID `HearthstoneModding.LibmemCli`. It is validated through an independent PackageReference consumer before any public package publication is considered.

See [CONSUMPTION.md](CONSUMPTION.md) for the package layout, x64 constraints, and NuGet acceptance criteria.

## Public API stability

The repository maintains:

```text
api/LibmemCli.PublicApi.txt
```

CI compares the public declarations in `src/LibmemCli.h` with that baseline.

Intentional public API changes must update the baseline and changelog explicitly. Accidental signature drift fails validation.
