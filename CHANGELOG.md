# Changelog

## Unreleased

### Changed

- Added Windows x86 alongside x64 across native builds, C++/CLI solution configurations, sample/test projects, CI matrices, reusable builds, packaging, and release assets.
- Added pointer-width-safe address and size conversion so x86 rejects values that do not fit instead of silently truncating them.
- Made smoke and Hook/VMT runtime tests architecture-aware, including pointer-size VMT slots and x86-specific overflow guards.
- Kept the committed public API baseline unchanged across the dual-architecture implementation.

- Added a committed x64 public API baseline and compatibility checker so accidental public signature changes fail source-contract CI.
- Documented the explicit process for intentional pre-1.0 API changes: regenerate the baseline, review the diff, update the changelog, and version accordingly.

- Upgraded the x64 runtime package manifest to schema v2 with per-file size and SHA-256 metadata.
- Added an external SHA-256 checksum for the runtime ZIP and a shared package verifier used by build/reusable/release workflows.
- Release publication now verifies package version, platform/configuration, file integrity, archive integrity, and repository commit provenance before creating a GitHub Release.

- Expanded x64 runtime smoke coverage across process command lines, threads, modules/exported symbols, memory segments, SetMemory, DeepPointer, single-instruction assembly/disassembly, and CodeLength.
- Kept mutation-oriented tests isolated to memory allocated inside the test process.

- Hardened Hook/VMT ownership: `HookHandle` now retains `Destination`, explicit hook disposal surfaces unhook failure, and `VmtManager.Dispose()` preserves native bookkeeping when restoration fails.
- Mapped Hook/VMT setup and restoration failures to `LibmemException` while keeping GC finalizers non-mutating.

- Added `LibmemException` with an `Operation` property for definite native libmem failures while preserving normal not-found/sentinel return semantics.
- Mapped core enumeration, protection, allocation-release, injection-release, and fixed-size read/write failures to the unified exception type.

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
