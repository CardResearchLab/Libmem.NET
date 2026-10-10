# Libmem.NET Development Roadmap

**2.5.0 is officially published** (2026-10-10 UTC): main Build #461, Release dry run #50, GitHub Release #51 and NuGet Trusted Publishing #52 succeeded. Independent public NuGet x64/x86 consumer tests passed Published NuGet Smoke #48 on PR #148. Focus remains on stability; ARM64 and cross-platform targets are out of scope.

> Current strategy: **Windows x64 and x86 are first-class targets; shared design stays architecture-neutral for a future ARM64 phase.**

## Platform policy

Libmem.NET 2.2.0 official development, default CI, runtime acceptance, NuGet, and GitHub Releases cover **Windows x64 / x86 + .NET 8**.

x86 status:

- Release x86 build, smoke, Hook/VMT, Injector, and ExternalProcessTests are part of the default validation path;
- runtime ZIP, manifest, and SHA-256 verification cover both x64 and x86;
- one NuGet package carries matching `win-x64` and `win-x86` C++/CLI/native assets;
- `buildTransitive` selects architecture from explicit `Platform` / `PlatformTarget`;
- `AnyCPU` is rejected and cross-bitness operation is not supported;
- ARM64 is outside the 2.2.0 production support scope, while shared test/package structure must remain extensible.

## Architecture principle

Libmem.NET remains an independent, general-purpose .NET/C++/CLI wrapper around libmem. It must not depend on StandaloneGameMod, Hearthstone, Unity, Mono, or game-state models.

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

## Completed phase: v2.1.1 — post-release maintenance

2.1.1 is a backward-compatible patch over 2.1.0. It does not add Public API and does not upgrade the pinned libmem revision. Completed work:

- reject the bad-address sentinel at the `VmtManager` managed construction boundary;
- add runtime and source-contract regression coverage for that invalid VTable base;
- preserve the initial failed 2.1.0 nuget.org restore as historical release evidence and advance the automatic public-NuGet smoke baseline to 2.1.1 after publication;
- remove stale candidate/stable/NuGet wording left after the 2.1.1 publication.

## Completed phase: v2.1.0 — Hook / VMT Hardening

`2.0.0` was released on 2026-10-06 and establishes the stable Windows x64 / .NET 8 assembly, NuGet, runtime archive, checksum, and Public API baseline. 2.1.0 does not perform another identity migration and does not intentionally introduce breaking changes.

The 2.1.0 goal is:

> Keep the 2.0.0 Public API compatible while moving Hook / VMT from "usable" to "well-defined failure paths, stable ownership, and complete runtime coverage."

Pre-release audit findings:

- `HookManager.Install` zero/bad-address, definite-native-failure, and disposed-session contracts are frozen;
- self-process and external-process runtime tests cover redirection, trampoline execution, instruction boundaries, Remove/Dispose idempotency, target exit, and retryable failed ownership cleanup;
- `VmtManager` covers repeated Hook, untracked Unhook, Reset/reuse, Dispose, and restore-failure retry lifecycles;
- remote unhook preflights the complete trampoline read so a pinned `LM_UnhookCodeEx` read failure cannot leak source-protection state;
- duplicate/overlapping code-hook conflicts are not managed through a global Libmem.NET registry, and relative-control-flow trampoline safety remains a pinned-libmem capability; both are now explicit consumer/upstream boundaries.

### 2.1.0 work items

1. **Managed argument and state contracts**
   - audit zero/bad-address/width handling for source, destination, and trampoline addresses;
   - define behavior for disposed sessions, exited targets, repeated Remove, and Remove after Dispose;
   - avoid adding an expensive universal process-enumeration preflight to every Hook hot path.

2. **Hook install/remove failure paths**
   - verify `LM_HookCodeEx` failure cannot expose a partially installed managed handle;
   - verify `LM_UnhookCodeEx` failure preserves `HookHandle` ownership for explicit retry;
   - add duplicate-hook, overlapping-source, and invalid-destination regression scenarios;
   - preserve the static `Libmem.HookCode` compatibility-facade semantics while the Manager layer continues to promote definite failures.

3. **Trampoline / instruction boundaries**
   - validate `PatchedBytes` and trampoline metadata consistency;
   - add runtime coverage for short functions, instruction boundaries, and relative-control-flow cases;
   - do not reimplement native libmem relocation/disassembly logic in the managed layer.

4. **VMT lifetime hardening**
   - add repeated Hook/Unhook, untracked-index, Reset-reuse, and Dispose-failure tests;
   - define replacement-address and index argument contracts;
   - keep VMT local-process-only rather than inventing a remote VMT abstraction.

5. **Independent runtime tests**
   - retain current self-process Hook/VMT coverage;
   - add external-process Hook lifecycle tests using `Libmem.NET.TestTarget`;
   - cover owning-handle state convergence after target exit;
   - keep Windows x64 Release as the default CI requirement.

6. **Consumer documentation and samples**
   - document Hook/VMT failure, ownership, threading, and target-exit behavior in `docs/API.md`;
   - add a C# Hook consumer example;
   - keep the Public API baseline as a merge gate; 2.1.0 should not add unplanned breaking members.

### Explicitly out of scope for 2.1.0

- Mono / Unity / Hearthstone method resolution;
- Harmony-compatible Patch APIs;
- GameState / Entity / Snapshot / IPC;
- game-version adaptation;
- official x86 support;
- game-specific Hook policy.

Those concerns belong to consumers such as StandaloneGameMod, not Libmem.NET.

### 2.1.0 acceptance

- Windows x64 Release build passes;
- all Hook/VMT runtime tests pass;
- new external-process Hook failure/exit coverage passes;
- Public API baseline shows no undocumented breaking change;
- XML IntelliSense / `docs/API.md` match implementation behavior;
- NuGet consumer restore/build/run smoke passes.

## Completed: v2.2.0 — Official Windows x86 support

The 2.2.0 release goal is to formally deliver the already validated x86 capability:

- x64 and x86 both pass default Release CI and independent runtime tests;
- ExternalProcessTests/TestTarget isolate machine-code differences behind architecture fixtures;
- runtime ZIPs and checksums are published separately for both architectures;
- one NuGet package provides architecture-matched x64/x86 assets and rejects AnyCPU;
- the 2.1.1 Public API and pinned libmem revision remain unchanged.

## Completed: v2.4.0 — Assembly / Symbols practical enhancements

- Added read-only instruction-aligned `ReadAlignedCode` and normal-miss `TryFindAddress` without changing native ABI or existing managed Hook/VMT/Injector behavior.
- PR #129/#132, main Build #425, dry run Release #44, GitHub Release #45, NuGet Trusted Publishing #46 and public x64/x86 consumer smoke #36 passed.
- New feature development is paused after 2.4.0; prioritize maintenance, fixes and regression coverage.

## Completed: v2.3.0 — Native API coverage and typed pointer helpers

After stable 2.2.0 publication, systematically compare against the pinned rdbo/libmem revision:

- maintain a native → managed API coverage matrix;
- identify appropriate upstream APIs not yet wrapped;
- evaluate and update the pinned upstream revision;
- run ABI / interop / runtime regression;
- preserve the general-purpose library boundary without application models.

## Completed: v2.4.1 — stability patch

- Fix complete-instruction reads at inaccessible memory page boundaries in `ReadAlignedCode`.
- Preflight remote mutations against PID + process start time; the native PID-only TOCTOU limit remains documented.
- Make failed `HookHandle.Remove` regression deterministic using `PAGE_NOACCESS` trampoline protection.
- Evidence: main Build #445, Release dry run #47, GitHub Release #48 and NuGet OIDC push #49; independent public-consumer results are gated by post-release CI.

## Completed: v2.5.0 — Try APIs and Hook/VMT lifecycle hardening

PR #144 added three ScanManager Try methods; #145 added MemoryManager.TryRead/TryWrite; #146 hardened local Hook removal and VMT retry regression tests; #147 prepared candidate metadata. Main Build #461, Release dry run #50, GitHub Release #51 and separate NuGet OIDC publication #52 succeeded. Independent nuget.org x64/x86 consumers, XML/native assets and unsupported-target rejection passed Published NuGet Smoke #48 in PR #148; main smoke is verified separately after merge. ARM64, AnyCPU, cross-bitness and game-specific features remain unsupported.

## Completed: v2.0.0 — Stable Libmem.NET identity

2.0.0 completed the identity migration from `LibmemCli` to the `Libmem.NET` namespace, assembly, package, and documentation model. The official target is Windows x64 / .NET 8, and later 2.x work defaults to compatibility with the 2.0.0 Public API baseline.

## v0.4 — x64 architecture cleanup

Focus:

- complete the ProcessSession aggregation model;
- complete Core / Memory / Modules / Threads / Scanning / Symbols / Assembly source separation;
- establish the Interop / NativeConverter boundary;
- preserve existing static `NativeApi.*` compatibility;
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
- XML documentation (the `Libmem.NET.xml` build/package pipeline is established; public API comments continue to expand);
- README / API documentation (consumer behavior reference established in `docs/API.md`).

All default acceptance runs target x64.

## v0.8 — packaging and release

Focus:

- x64 runtime package;
- manifest / SHA-256;
- GitHub Release;
- reusable workflow;
- evaluate NuGet or a more standard consumption model (local x64 packaging, public prerelease publication, independent PackageReference restore/build/run/publish, and Trusted Publishing have completed end-to-end acceptance).

Official releases publish only:

```text
Libmem.NET-windows-x64.zip
Libmem.NET-windows-x64.zip.sha256
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

> Libmem.NET is a stable, general-purpose Windows x64 C++/CLI wrapper around libmem for consumption by other .NET projects.

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
