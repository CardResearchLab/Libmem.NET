# Changelog

## Unreleased

### Changed

- Removed `ProcessSnapshot` / `ModuleSnapshot` and snapshot-specific APIs from LibmemCli. Application-level snapshots and caches belong to wrapper consumers.
- `HookHandle` now exposes `Destination` in addition to source/trampoline metadata.
- Added `LibmemException : InvalidOperationException` with an `Operation` property for definite native libmem failures.
- Added Windows x86 alongside x64 across native/C++/CLI builds, tests, packaging, and release artifacts.
- Explicit `HookHandle.Dispose()` now surfaces restoration failure instead of silently orphaning an active hook; finalization performs best-effort cleanup.
- Snapshot-specific runtime tests/workflow were removed and Hook lifecycle coverage was strengthened.
- Clarified that LibmemCli is a reusable libmem wrapper and is not coupled to StandaloneGameMod or any game-specific state model.


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
