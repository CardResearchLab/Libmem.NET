# Changelog

## Unreleased

No changes yet.

## 2.0.0-preview.1 - 2026-10-05

### Breaking identity migration

- Unified the managed namespace, assembly, solution/project names, test projects and runtime assets as `Libmem.NET`; the NuGet PackageId remains `Libmem.NET`.
- Existing `LibmemCli` consumers must update references, namespaces, paths and reflection strings, then recompile. See `docs/MIGRATION.md`.
- Preserved public member signatures, native ABI mapping, ownership, exception behavior and the pinned native dependency. Static facade calls use an explicit type alias to avoid the new root namespace collision.
- Corrected runtime manifest provenance to `CardResearchLab/Libmem.NET`.
- Consolidated automatic PR validation into Build, including Release x64 by default, optional manual Debug, all runtime suites and local NuGet restore/run/publish. Specialized workflows retain manual entry points.

### Release preparation

- Bumped the major version because the already released `LibmemCli` v1.0.0 namespace and assembly identity are not binary compatible with `Libmem.NET`. Consumers must rebuild; renaming a DLL is insufficient.
- Set `VERSION` and informational version to `2.0.0-preview.1`; numeric assembly/file versions are `2.0.0.0`.
- Enabled prerelease version validation for runtime manifests, NuGet packaging and formal Release Notes; GitHub Release publication marks preview tags as prereleases.
- Included the changelog and migration guide in runtime ZIP and NuGet packages. Build artifacts include an exact-version `Libmem.NET.2.0.0-preview.1.nupkg` for local-feed acceptance.
- Separated GitHub downloads from nuget.org publication. NuGet Trusted Publishing is explicitly invoked after the GitHub release and account setup are ready.
- Updated assembly company metadata to `CardResearchLab` and NuGet author attribution to `xiaohei7972`. Updated repository URLs, reusable workflow references, manifest provenance, and publishing setup for the renamed organization.

The preview was subsequently published as GitHub prerelease `v2.0.0-preview.1` and pushed to nuget.org through Trusted Publishing (OIDC). A separate nuget.org-only PackageReference smoke test restored, built, ran and published the package successfully. The existing v1.0.0 tag and assets remain unchanged.

### CI and documentation follow-up

- Default automatic Build validation to Release x64; retain every Release runtime, example, consumer and package check. Manual `workflow_dispatch` can additionally enable Debug x64 build and smoke tests through `debug`.
- Restrict GitHub Release creation to tags and NuGet OIDC login/push to an explicit manual run on an existing published tag. `release/v*` branches validate artifacts and render notes without publishing or deleting branches.
- Consolidate the unfinished documentation from #70 and release readiness work from #75: concise bilingual README, release/versioning guide, historical v1.0.0 notes and a checklist for the next release. Keep historical LibmemCli assets distinct from the current Libmem.NET identity.

## 1.0.0 - 2026-09-30

### Added

- Added `eng/package-nuget.ps1` so local and CI NuGet prototype packaging share the same version/provenance/layout verification path.
- Added an unpublished `HearthstoneModding.LibmemCli` x64 NuGet prototype, package-layout verification, and an independent `PackageReference` consumer test covering restore/build/run with native runtime dependencies.
- Added `docs/API.md` as a consumer-facing behavior reference for the session model, Managers, ownership types, result semantics, exceptions, static compatibility APIs, x64 policy, and pinned-upstream workarounds.
- Added generated `LibmemCli.xml` IntelliSense documentation to x64 builds and runtime packages; MSVC `/doc` output is merged by XDCMake and shipped beside `LibmemCli.dll`.
- Added an x64 external-process `LibmemCli.TestTarget` plus runtime tests for remote attach, read/write, remote allocation/protection/free, signature scan, segment lookup, and process-exit observation.
- Added `ProcessSession.Open(...)` as the preferred object-oriented factory while preserving `Libmem.Attach(...)` for compatibility.
- Added session-bound `ThreadManager` through `ProcessSession.Threads`, including thread enumeration and main-thread lookup.
- Added session-bound `ScanManager` through `ProcessSession.Scanner` for DeepPointer, data, pattern, and signature scanning.
- Added `SymbolManager` through `ProcessSession.Symbols` for module symbol enumeration, lookup, and demangling.
- Added `AssemblyManager` through `ProcessSession.Assembly`; it defaults to the target process architecture and can disassemble bytes read from the target process.

### Changed

- Completed the final managed-contract consistency pass for v0.9: clarified XML IntelliSense for symbol/deep-pointer sentinel results and ownership-handle idempotency, and added regression coverage that HookHandle.Remove remains idempotent after successful Dispose.

- Frozen sentinel/definite-failure layering: process/module/segment lookups keep nullable miss results, symbol/scan/deep-pointer misses keep the native bad-address sentinel, and the low-level static compatibility facade preserves native-style failure values while Manager/ownership APIs promote definite failures where their contract already requires exceptions.

- Frozen zero-size contracts without broad normalization: read/write/set remain no-ops, Windows protect/allocation preserve pinned libmem page-size semantics, owned `MemoryManager.Allocate(0)` remains invalid, and zero-length code/disassembly queries keep natural empty/zero results.

- Normalized empty scan-input contracts: empty pattern masks and empty/whitespace signatures now fail with `ArgumentException` before native dispatch, while valid non-empty scan misses continue to return the native bad-address sentinel.

- Preserved public parameter names when rejecting embedded NUL characters in process/module names, module paths, scan strings, symbol names, and assembly source; internal UTF-8 helper details no longer leak through `ArgumentException.ParamName`.

- Added managed-boundary validation for caller-supplied `Architecture` and `MemoryProtection` values. Undefined architectures and protection flags containing unknown bits now fail with `ArgumentOutOfRangeException` before native dispatch.

- Bound `ModuleInfo` results to their originating PID + start-time identity internally. Session-bound module unload/symbol operations and static module-unload overloads now reject cross-process `ModuleInfo` values before native dispatch, without expanding the public `ModuleInfo` surface or adding module-enumeration preflights.

- Normalized public argument contracts for scan, symbol, and assembly inputs so null and clearly single-parameter invalid cases report the public parameter name instead of leaking internal helper names or omitting `ParamName`.

- Froze `ProcessInfo` as library-created read-only identity metadata so PID/start-time and related process fields cannot be rewritten after capture; this intentionally removes the pre-v1.0 public setter/default-construction surface while preserving all process lookup, liveness, session, and static-operation entry points.
- Completed the v0.9 managed-contract documentation audit: expanded XML IntelliSense coverage for the frozen `ProcessSession` / Manager / ownership surface and documented `ProcessSession.Allocate` as an intentional ownership convenience, with no public API or runtime behavior change.
- Froze target-process exit semantics: process exit no longer implies session disposal, bound identity metadata remains readable, `IsAlive()`/`Refresh()` expose staleness, Manager accessors remain available, and hot-path Manager calls do not gain a universal exact-identity preflight.
- Froze ownership-lifetime idempotency with runtime coverage for repeated session detach/dispose, repeated resource disposal, and remote-allocation cleanup after target-process exit.
- Extended the committed public API baseline to freeze the managed `LibmemCli` namespace in addition to public types and members, and updated the roadmap to the active v0.9 x64 API Freeze phase.
- Started the v0.9 API-freeze contract hardening by normalizing null/blank process/module/library arguments to standard .NET argument exceptions and treating a zero VMT address as `ArgumentOutOfRangeException` before native dispatch.
- Modernized the C# consumer sample around the recommended `ProcessSession` and Manager APIs, including owned memory, scanning, protection restore, deterministic disposal, and `LibmemException` handling.
- Replaced GitHub auto-generated PR-feed release bodies with formal user-facing Release Notes generated from the versioned changelog and verified package metadata.
- Tightened the session-bound manager error contract: definite allocation, module-load, assembly, and code-length failures now surface as `LibmemException`, while static compatibility APIs retain their existing sentinel/nullable semantics.
- Split the remaining static `Libmem.*`, `ProcessInfo`, and `LibmemException` implementations into subsystem translation units, leaving `LibmemCli.cpp` as a thin compatibility translation unit.
- Extracted Hook/HookHandle and VMT implementations into dedicated `Hooks` source files while preserving the existing managed API and lifecycle semantics.
- Extracted `RemoteAllocation` and injection ownership implementations from the static facade into dedicated Memory/Injection source files without changing the public API.
- Extracted native/managed conversion, address/size validation, enumeration callbacks, and model translation into an internal `Interop/NativeConverter` boundary.
- Split the high-level ProcessSession and manager implementations into dedicated Core, Memory, Modules, Threads, Scanning, Symbols, and Assembly source files without changing the public API.
- Updated source-contract validation to aggregate all C++ implementation files under `src/`, so architectural file splits remain covered by CI.
- Switched the active development and release strategy to x64-first: Windows x64 is now the default CI, runtime-test, packaging, and official Release target.
- Deferred x86 feature work and official x86 Release assets while keeping existing x86 code/configuration available for future manual compatibility work.
- Added dedicated x64-first roadmap documents in `ROADMAP.md` and `ROADMAP.en.md`.
- Continued the Blackbone-inspired process aggregation refactor: `ProcessSession` now exposes memory, modules, threads, scanning, symbols, assembly/disassembly, hooks, and injection as explicit subsystems.

### Changed

- Froze `InstructionInfo` as a LibmemCli-created deeply read-only instruction result. All public properties are getter-only, and `Bytes` returns a defensive copy so callers cannot mutate the stored instruction bytes.
- Froze `SegmentInfo` as a LibmemCli-created read-only memory-segment result. `Base / End / Size / Protection` are getter-only and the implicit public construction/mutation surface is removed before v1.0.
- Froze `SymbolInfo` as a LibmemCli-created read-only symbol result. `Address / Name` are getter-only and the implicit public construction/mutation surface is removed before v1.0.
- Froze `ThreadInfo` as a LibmemCli-created read-only thread descriptor. `Id / OwnerPid` are getter-only and the implicit public construction/mutation surface is removed before v1.0.
- Froze `ModuleInfo` as a LibmemCli-created read-only module descriptor. `Base / End / Size / Name / Path` are getter-only and the implicit public construction/mutation surface is removed before v1.0.

### Removed

- Removed the early convenience `ProcessInfo.Read`, `Write`, `ReadInt32`, `WriteInt32`, and `SigScan` forwarding methods before v1.0. `ProcessInfo` now remains focused on process identity/metadata plus `IsAlive()`; session-bound memory/scanning lives on Managers and static `Libmem.*` compatibility APIs remain available.
- Removed the temporary v0.x `MemoryManager.DeepPointer`, `DataScan`, `PatternScan`, and `SigScan` forwarding aliases before the v1.0 API freeze. Session-bound scanning now lives only on `ProcessSession.Scanner`; static `Libmem.*` compatibility APIs remain available.

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

- Worked around the pinned Windows `LM_GetProcessEx` start-time bug for external processes by reconciling the target start time through `LM_EnumProcesses`, preserving PID + start-time identity checks for `ProcessSession`.
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
