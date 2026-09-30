# LibmemCli Development Roadmap

> Current strategy: **x64 first; x86 deferred.**

## Platform policy

Official LibmemCli development, default CI, runtime acceptance, and GitHub Releases currently target **Windows x64 / .NET 8**.

x86 status:

- existing x86 code, solution configurations, and build-script compatibility paths remain in the repository;
- x86 is not a near-term feature-development target;
- new features are not required to maintain immediate x86 parity;
- x86 is not part of the default CI merge gate;
- GitHub Releases do not currently publish x86 ZIP/checksum assets;
- successful manual x86 builds are best-effort compatibility, not a stability commitment;
- if x86 development resumes, pointer width, Hook/VMT, assembler/disassembler, injector, packaging, and runtime tests will receive a dedicated compatibility audit.

## Architecture principle

LibmemCli remains an independent, general-purpose .NET/C++/CLI wrapper around libmem. It must not depend on StandaloneGameMod, Hearthstone, Unity, Mono, or game-state models.

Preferred model:

```text
ProcessSession
├── Memory
├── Modules
├── Threads
├── Scanner
├── Symbols
├── Assembly
├── Hooks
└── Injector
```

Snapshots, caches, entities, game state, event state, IPC, and game-version adaptation belong to consumers.

## Current phase: v0.9 — x64 API Freeze

The x64 API, resource lifetimes, error model, Hook/VMT layer, external-process tests, consumer documentation, and local NuGet consumption prototype are now established. Development is therefore moving from broad expansion to a **Windows x64 managed-contract freeze**.

Current audit priorities:

1. freeze the managed namespace, public types, method names, signatures, and overload shapes;
2. define `ProcessSession` / Manager behavior for target identity, process exit, and disposal;
3. freeze ownership and idempotency semantics for `RemoteAllocation`, `HookHandle`, `VmtManager`, and `InjectedModuleHandle`;
4. normalize null/invalid arguments, normal misses, native failures, return sentinels, and exception semantics;
5. remove public APIs that exist only for v0.x migration when carrying them into v1.0 would create permanent compatibility debt;
6. reconcile XML IntelliSense, `docs/API.md`, and the committed public API baseline;
7. identify any design that would otherwise force a post-v1.0 breaking change.

This phase still excludes Snapshot, GameState, Entity, IPC, Hearthstone-specific behavior, and game-version logic.

Completed freeze cleanup: the temporary v0.x `MemoryManager` forwarding aliases for `DeepPointer / DataScan / PatternScan / SigScan` have been removed. Session-bound scanning is frozen on `ProcessSession.Scanner`, while the static `Libmem.*` compatibility facade remains.

Completed contract freeze: target-process exit does not implicitly dispose `ProcessSession`; the bound identity remains readable, `IsAlive()` returns false, `Refresh()` returns null, and Manager properties remain accessible. A universal Manager liveness preflight is intentionally avoided so exact external-process identity checks do not pollute read/write/scan hot paths.

Completed freeze cleanup: the early convenience `ProcessInfo.Read / Write / ReadInt32 / WriteInt32 / SigScan` methods have been removed. `ProcessInfo` now keeps only identity-related behavior through `IsAlive()`; memory and scanning belong to the `ProcessSession` Managers, while the static `Libmem.*` compatibility facade remains.

Completed freeze cleanup: audited XML IntelliSense and `docs/API.md` against the frozen managed surface, completed documentation for the recommended `ProcessSession` / Manager / ownership members, and explicitly retained `ProcessSession.Allocate` as an ownership convenience. This does not change the public API baseline or runtime behavior.

Completed contract freeze: `ProcessInfo` is now library-created read-only identity/metadata. Consumers can no longer rewrite `Pid / StartTime` or fabricate an empty identity through a public default constructor, so `IsAlive()`, `Open(ProcessInfo)`, and the PID + StartTime exact-identity model share the same immutable foundation.

Completed freeze cleanup: `ModuleInfo` is now a LibmemCli-created read-only module descriptor. Consumers can no longer rewrite `Base / End / Size / Name / Path` and then pass a forged or mutated native module record back into unload or symbol APIs.

Completed freeze cleanup: `ThreadInfo` is now a LibmemCli-created read-only thread descriptor. Consumers can no longer rewrite `Id / OwnerPid` and then pass a forged or mutated native thread record back into `GetThreadProcess`.

Completed freeze cleanup: `SymbolInfo` is now a LibmemCli-created read-only symbol result. Consumers can read `Address / Name` but cannot construct or mutate forged symbol results.

Completed freeze cleanup: `SegmentInfo` is now a LibmemCli-created read-only memory-segment result. Consumers can read `Base / End / Size / Protection` but cannot construct or mutate forged segment metadata.

Current freeze cleanup: `InstructionInfo` is being frozen as a LibmemCli-created deeply read-only instruction result. Scalar/string properties are getter-only and `Bytes` returns a defensive copy so callers cannot mutate the stored instruction state.

## v0.4 — x64 architecture cleanup

Focus:

- complete the ProcessSession aggregation model;
- complete Core / Memory / Modules / Threads / Scanning / Symbols / Assembly source separation;
- establish the Interop / NativeConverter boundary;
- preserve existing static `Libmem.*` compatibility;
- keep application/game state outside the wrapper.

Acceptance:

- x64 Build passes;
- x64 Runtime Smoke passes;
- Public API baseline passes;
- upstream libmem API coverage passes.

## v0.5 — lifetime and error model

Focus:

- RemoteAllocation lifetime;
- HookHandle lifetime;
- VMT lifetime;
- InjectedModuleHandle lifetime;
- consistent ObjectDisposedException / argument exception / LibmemException semantics;
- finalizers must not perform unsafe remote restoration work on the GC thread.

## v0.6 — Hook / VMT / Assembly completeness

Focus:

- stable Hook API;
- stable trampoline metadata;
- complete VMT Hook / Unhook / Reset / Dispose semantics;
- stable Assembly / Disassembly / CodeLength APIs;
- Session APIs become the recommended surface while static APIs move into compatibility maintenance.

## v0.7 — tests and consumer experience

Focus:

- Smoke Tests;
- Hook/VMT Tests;
- Injector Tests;
- independent TestTarget (x64 external-process target established);
- C# consumer sample (updated to the recommended `ProcessSession` / Manager / IDisposable / `LibmemException` usage);
- XML documentation (the `LibmemCli.xml` build/package pipeline is established; public API comments continue to expand);
- README / API documentation (consumer behavior reference established in `docs/API.md`).

All default acceptance runs target x64.

## v0.8 — packaging and release

Focus:

- x64 runtime package;
- manifest / SHA-256;
- GitHub Release;
- reusable workflow;
- evaluate NuGet or a more standard consumption model (a local x64 `.nupkg` plus independent `PackageReference` consumer test is established; public publication still requires full-path acceptance).

Official releases publish only:

```text
LibmemCli-windows-x64.zip
LibmemCli-windows-x64.zip.sha256
```

## v0.9 — x64 API Freeze

Freeze:

- naming;
- namespaces;
- public types;
- method signatures;
- IDisposable behavior;
- exception semantics.

Breaking public API changes must be explicitly documented from this phase onward.

## v1.0 — Stable x64

v1.0 means:

> LibmemCli is a stable, general-purpose Windows x64 C++/CLI wrapper around libmem for consumption by other .NET projects.

v1.0 does not require x86 completion.

## v1.x / later — reconsider x86

Only after the x64 line is stable should official x86 support be reconsidered.

If resumed, x86 must be revalidated across:

- pointer/address width;
- native conversions;
- allocator;
- assembler/disassembler;
- Hook trampoline;
- VMT;
- Injector;
- C++/CLI runtime;
- package;
- CI;
- consumer compatibility.

Official x86 releases return only after that matrix passes.
