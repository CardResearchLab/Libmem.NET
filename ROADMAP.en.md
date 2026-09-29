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

## Current phase: source architecture split

The goal is to break up the historical large `LibmemCli.cpp` into clear implementation boundaries while keeping the public API stable.

Priority order:

1. split ProcessSession and manager implementations;
2. extract Interop / NativeConverter;
3. extract native address / size / string / callback conversion helpers;
4. split resource-lifetime types such as RemoteAllocation and InjectedModuleHandle;
5. split Hook / VMT;
6. leave `LibmemCli.cpp` only as a necessary static compatibility facade, or continue splitting by responsibility.

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
- C# consumer sample;
- XML documentation (the `LibmemCli.xml` build/package pipeline is established; public API comments continue to expand);
- README / API documentation.

All default acceptance runs target x64.

## v0.8 — packaging and release

Focus:

- x64 runtime package;
- manifest / SHA-256;
- GitHub Release;
- reusable workflow;
- evaluate NuGet or a more standard consumption model.

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
