# Changelog

## Unreleased

### Changed

- Hardened owned-resource disposal: `RemoteAllocation` and `InjectedModuleHandle` no longer silently discard ownership when native cleanup fails.
- Kept GC finalizers non-mutating for remote-process resources; deterministic cleanup remains the caller's responsibility.

- Removed `ProcessSnapshot` / `ModuleSnapshot` and snapshot-specific APIs/workflow; application snapshots, caches, and game-state models belong to wrapper consumers.
- Reaffirmed the x64-first boundary: the pinned libmem public C API is fully referenced by LibmemCli, while x86/x64 dual-architecture work remains deferred.
- Clarified `ProcessSession` as an optional target-bound convenience wrapper rather than an application state container.

## 0.2.0

LibmemCli 0.2.0 turns the wrapper into a session-oriented injection and memory toolkit for .NET 8 / Windows x64.

### Added

- `ProcessSession` lifecycle and process identity tracking.
- `RemoteAllocation` ownership for remote memory.
- Session-bound `MemoryManager` and `ModuleManager`.
- `HookHandle` lifecycle hardening and session-bound `HookManager`.
- Safer `VmtManager` reset/dispose behavior.
- `InjectorManager` and `InjectedModuleHandle` ownership semantics.
- Immutable `ProcessSnapshot` and `ModuleSnapshot` state models.
- Dedicated Hook/VMT, Injector, and Snapshot runtime-test workflows.

### Integration

- The API is ready for direct consumption by StandaloneGameMod-style launchers through `ProcessSession`, module snapshots, and explicit ownership models.
- Legacy static `Libmem.*` APIs remain available for compatibility.

### Packaging

- Windows x64 / .NET 8 runtime package remains self-contained around `LibmemCli.dll`, `Ijwhost.dll`, and `libmem.dll`.
- Release automation now verifies the release tag/branch version matches the packaged `VERSION` before publishing.

## 0.1.0

Initial packaged release of the C++/CLI wrapper around the pinned rdbo/libmem revision.
