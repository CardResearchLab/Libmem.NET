# LibmemCli 1.0.0 → Libmem.NET 2.0.0-preview.1 migration

旧名 `LibmemCli v1.0.0` 已发布。本次命名空间与程序集身份变化破坏源码和二进制兼容性，因此新版本为 `2.0.0-preview.1`，不覆盖旧版 `v1.0.0`。所有消费者和依赖旧程序集的中间库都必须重新编译。

The released `LibmemCli v1.0.0` keeps its tag and assets. This breaking identity migration starts the new major version at `2.0.0-preview.1`, rather than reusing `1.0.0`.

| Metadata | Preview value |
| --- | --- |
| VERSION / NuGet version / informational version | `2.0.0-preview.1` |
| AssemblyVersion / AssemblyFileVersion | `2.0.0.0` |
| Release tag | `v2.0.0-preview.1` |
| NuGet PackageId | `Libmem.NET` |

The preview tag is a planned release identifier until publication completes; this guide does not imply that the package is already on nuget.org.

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

## Upgrade checklist / 升级步骤

1. Update `LibmemCli` namespaces, assembly/project references and reflection strings using the mapping above. Rebuild every dependent library, not only the application.
2. Delete stale consumer `bin` / `obj` outputs and old copied runtime files, then restore and rebuild for Windows x64 / .NET 8. Do not rename an old `LibmemCli.dll` to `Libmem.NET.dll`.
3. For PackageReference, remove any former `HearthstoneModding.LibmemCli` reference. If already using PackageId `Libmem.NET`, update its version explicitly:

   ```xml
   <PackageReference Include="Libmem.NET" Version="2.0.0-preview.1" />
   ```

   Before nuget.org publication, download the exact-version `.nupkg` from the preview's GitHub Release or validated Build artifact into a local feed and restore with `dotnet restore --source <local-feed>`. The package is a preview; Visual Studio's package browser requires **Include prerelease** once it is publicly available.
4. Confirm the rebuilt output contains `Libmem.NET.dll`, `libmem.dll`, `Ijwhost.dll` and IntelliSense XML. Run the consumer's process/memory calls and the Hook/VMT/Injector paths it actually uses.

Runtime ZIP and NuGet packages include this guide and `CHANGELOG.md`. GitHub downloads are published separately from the later, explicitly requested nuget.org push.

The implementation and acceptance plan is [LIBMEM_NET_MIGRATION_PLAN.md](LIBMEM_NET_MIGRATION_PLAN.md). Windows CI verifies Debug/Release compilation, the example, all runtime suites, local NuGet restore/run/publish and runtime package integrity before the migration is considered complete.
