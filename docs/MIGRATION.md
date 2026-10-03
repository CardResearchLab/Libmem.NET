# Libmem.NET naming migration

The product and NuGet PackageId remain `Libmem.NET`. The managed namespace and assembly now also use `Libmem.NET`; the former `LibmemCli` identity is intentionally replaced. This requires recompilation of existing consumers.

| Old | New |
| --- | --- |
| `using LibmemCli;` | `using Libmem.NET;` |
| `LibmemCli.dll` / `.xml` / `.pdb` | `Libmem.NET.dll` / `.xml` / `.pdb` |
| `LibmemCli.sln` | `Libmem.NET.sln` |
| `src/LibmemCli.vcxproj` | `src/Libmem.NET.vcxproj` |
| `LibmemCli-windows-x64.zip` | `Libmem.NET-windows-x64.zip` |
| `LibmemCli.*Tests` | `Libmem.NET.*Tests` |
| `LIBMEMCLI_TEST_TARGET_DLL` | `LIBMEM_NET_TEST_TARGET_DLL` |

The static facade still has the public type name `Libmem`. Because `Libmem` is also the new root namespace, use an explicit type alias for static calls:

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

var process = NativeApi.CurrentProcess()
    ?? throw new InvalidOperationException("Current process could not be resolved.");
using var session = ProcessSession.Open(process);
```

Update direct references, `HintPath`, copy/publish paths, reflection strings and any assembly-qualified type names. A type such as `LibmemCli.ProcessSession, LibmemCli` becomes `Libmem.NET.ProcessSession, Libmem.NET`. C++/CLI consumers use `Libmem::NET`; use `::Libmem::NET::Libmem` when naming the static facade explicitly.

Remove old build outputs and restore/build the consumer again. Keep `Libmem.NET.dll`, `libmem.dll`, and `Ijwhost.dll` together in the Windows x64 application output; keep `Libmem.NET.xml` for IntelliSense.

Public member signatures, enum values, exceptions, resource ownership and Hook/VMT/Injector behavior are unchanged. The pinned native dependency and its `libmem.dll` / `LM_*` names are unchanged. Existing x86 source configuration is retained without new support or release assets.

NuGet uses the same PackageId with the new managed assets. No dual namespace compatibility DLL is supplied. Previously compiled consumers cannot obtain compatibility by renaming the old DLL. Existing released versions remain available; this migration does not publish or overwrite a NuGet version.

The implementation and acceptance plan is [LIBMEM_NET_MIGRATION_PLAN.md](LIBMEM_NET_MIGRATION_PLAN.md). Windows CI verifies Debug/Release compilation, the example, all runtime suites, local NuGet restore/run/publish and runtime package integrity before the migration is considered complete.
