# LibmemCli API Stability Policy

> Status: **policy prepared; v0.9 freeze not yet activated.**  
> Current supported/default target: Windows x64 / .NET 8.

LibmemCli is approaching the v0.9 x64 API-freeze phase. This document defines what becomes stable, which changes remain acceptable, and how public API changes must be reviewed.

## Freeze scope

The v0.9 freeze applies to the public managed surface exposed from `LibmemCli.h`, including:

- namespace and public type names;
- enum names and values;
- public properties;
- method names;
- parameter types and ordering;
- return types;
- `IDisposable` ownership semantics;
- documented null/sentinel result semantics;
- documented exception semantics.

The freeze does not prevent internal refactoring.

The following remain implementation details as long as public behavior is preserved:

- translation-unit layout;
- internal helper types;
- native/managed conversion helpers;
- callback implementation;
- build-script organization;
- test implementation details.

## Public API baseline

The committed baseline is:

```text
api/LibmemCli.PublicApi.txt
```

CI regenerates the public shape from:

```text
src/LibmemCli.h
```

and requires exact equality.

This means a public signature cannot change accidentally.

To intentionally regenerate the baseline:

```powershell
python .\eng\check-public-api.py --write
```

Updating the baseline is **not** sufficient by itself. An intentional public change also requires:

1. review of the API diff;
2. an entry in `CHANGELOG.md`;
3. consumer/runtime tests appropriate to the change;
4. documentation updates;
5. an explicit compatibility decision.

## Changes allowed during v0.9

### Internal changes

Allowed without a public baseline change when behavior remains compatible.

Examples:

- move implementations between `.cpp` files;
- refactor `NativeConverter`;
- optimize allocations;
- improve tests;
- change CI internals;
- fix an upstream compatibility workaround without changing the managed contract.

### Additive public APIs

Additive APIs may still be considered before v1.0, but must be deliberate.

Examples:

- a new overload;
- a new Manager capability backed by an existing/upgraded native API;
- a new read-only diagnostic property.

Requirements:

- update the public baseline;
- update `CHANGELOG.md`;
- document the API;
- add runtime/contract tests;
- preserve existing semantics.

Additions should be uncommon after v0.9. The goal is convergence toward v1.0, not continued surface growth.

### Breaking public changes

Breaking changes are not routine v0.9 work.

Examples:

- removing or renaming a public member;
- changing a parameter or return type;
- reordering parameters;
- changing enum values;
- moving a public type to another namespace;
- changing an owning handle into a non-owning handle;
- changing a documented normal miss from `null`/sentinel into an exception;
- changing a documented definite native failure from `LibmemException` into a silent result.

A breaking change requires an explicit versioning decision and must not be hidden inside a refactor.

## Behavioral contracts

The public API baseline protects shape. The following runtime behaviors are also treated as API contracts.

### Process identity

`ProcessSession` identifies a concrete target using PID + process start time.

A recycled PID must not silently become a different target.

`Refresh()` returns `null` when the original identity no longer exists.

### Manager lifetime

Session-bound Managers are valid only while their `ProcessSession` remains attached.

Calls after detach/disposal must fail with `ObjectDisposedException` rather than silently targeting another process.

### Normal misses remain normal results

Examples:

- process/module lookup miss -> `null`;
- scan miss -> libmem bad-address sentinel;
- short read -> shorter byte array;
- short write -> actual byte count.

These are not automatically promoted to exceptions.

### Definite native failures

When LibmemCli can determine that a native operation definitely failed, the recommended Manager API surfaces `LibmemException` with the native operation name.

Examples currently include:

- `LM_AllocMemoryEx`;
- `LM_LoadModuleEx`;
- `LM_AssembleEx`;
- `LM_CodeLengthEx`;
- `LM_HookCodeEx`.

### Resource ownership

The following types own explicit native/target resources:

- `RemoteAllocation`;
- `HookHandle`;
- `InjectedModuleHandle`;
- `VmtManager`.

Explicit disposal performs deterministic cleanup.

If deterministic cleanup fails, ownership state must not be silently discarded.

Finalizers must not perform unsafe remote-process restoration or mutation from the GC finalizer thread.

### Static compatibility facade

`Libmem.*` remains the low-level compatibility facade.

New code should prefer `ProcessSession` and subsystem Managers where target binding or ownership matters.

The static facade should not be removed as part of v0.9 cleanup.

## Native dependency changes

Updating the pinned rdbo/libmem revision is not an ordinary dependency bump.

An upstream bump requires:

1. public `LM_API` coverage audit;
2. compatibility-waiver review;
3. x64 Build/Smoke;
4. Hook/VMT runtime tests;
5. Injector runtime tests;
6. External Process runtime tests;
7. NuGet consumer tests once that path is accepted;
8. public API and behavior review.

If the upstream revision changes semantics underneath an unchanged C ABI, the managed behavior contract still takes priority.

## Documentation contracts

Consumer-facing behavior must agree across:

- XML IntelliSense comments;
- `docs/API.md`;
- README examples;
- `docs/CONSUMPTION.md`;
- `CHANGELOG.md`.

A public signature that exists but is documented with stale result/exception semantics is considered a compatibility defect.

## v1.0 entry criteria

Before declaring Stable x64 / v1.0:

- the v0.9 public baseline has remained stable through normal development;
- no unresolved ownership ambiguity remains;
- normal-result versus exception semantics are documented and tested;
- all default x64 runtime suites pass;
- package/release provenance is verified;
- at least one stable consumer path is validated end-to-end;
- any public NuGet path has passed independent restore/build/run/publish tests;
- known pinned-upstream workarounds are documented.

## x86

The v0.9 freeze is an **x64 contract**.

Existing x86 source/configuration does not expand the stability promise.

If official x86 support returns later, it receives a separate compatibility audit before being added to the stable contract.
