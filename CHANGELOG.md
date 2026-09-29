# Changelog

## Unreleased

### Added

- Added `ProcessSession.Open(...)` as the preferred object-oriented factory while preserving `Libmem.Attach(...)` for compatibility.
- Added session-bound `ThreadManager` through `ProcessSession.Threads`, including thread enumeration and main-thread lookup.
- Added session-bound `ScanManager` through `ProcessSession.Scanner` for DeepPointer, data, pattern, and signature scanning.

### Changed

- Began the Blackbone-inspired process aggregation refactor: `ProcessSession` now exposes memory, modules, threads, scanning, hooks, and injection as explicit subsystems.
- Kept the existing scan methods on `MemoryManager` as v0.x compatibility APIs so current consumers are not broken during the migration.

## 0.3.0 - 2026-09-29

LibmemCli 0.3.0 completes the wrapper's Windows x86/x64 stabilization and release pipeline while keeping the library independent from application-specific state models.

### Added

- Full Windows x86 support alongside x64 across native builds, C++/CLI configurations, samples, runtime tests, CI matrices, reusable builds, packaging, and release assets.
- A unified `LibmemException` error model that preserves the underlying native operation name for definite libmem failures.
- A committed shared public API baseline with CI enforcement so accidental signature or enum changes cannot land silently.
- Manifest schema v2 with per-file size/SHA-256 metadata, archive checksums, package verification, and repository-commit provenance validation.
- Architecture-aware Runtime Smoke, Hook/VMT, and Injector validation for both x86 and x64.

### Changed

- Removed `ProcessSnapshot` / `ModuleSnapshot` and snapshot-specific workflow code; snapshots, caches, events, and game-state models remain responsibilities of wrapper consumers.
- Hardened `RemoteAllocation`, `InjectedModuleHandle`, `HookHandle`, and `VmtManager` deterministic cleanup so failed native restoration/release is surfaced without silently discarding ownership state.
- Added pointer-width-safe address/size/index conversion so x86 rejects values above `UInt32.MaxValue` instead of truncating them.
- Expanded smoke coverage for processes, command lines, threads, modules, exported symbols, memory segments, allocation/read/write/set/protection, DeepPointer, scans, assembly/disassembly, and CodeLength.
- Release automation now produces and verifies both `LibmemCli-windows-x64` and `LibmemCli-windows-x86` packages.

### Fixed

- Avoided the pinned Windows upstream `LM_GetCommandLine` undefined-behavior path; current-process command-line arguments are now provided safely from the managed runtime while unsupported external-process queries return `null`.
- Made symbol smoke validation runtime-independent by selecting a loaded module with usable exports instead of assuming `kernel32.dll` is discoverable by name in every runner environment.

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
