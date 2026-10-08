# Libmem.NET

[简体中文](README.md) | [English](README.en.md)

[![CI Build](https://github.com/CardResearchLab/Libmem.NET/actions/workflows/build.yml/badge.svg)](https://github.com/CardResearchLab/Libmem.NET/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/CardResearchLab/Libmem.NET)](https://github.com/CardResearchLab/Libmem.NET/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows x64/x86](https://img.shields.io/badge/Windows-x64%20%7C%20x86-0078D4)

**Libmem.NET** 是 [rdbo/libmem](https://github.com/rdbo/libmem) 的 Windows C++/CLI 封装，为 C# / .NET 提供进程、线程、模块、内存、扫描、符号、汇编/反汇编、Hook、VMT 与 DLL 注入能力。

作者与维护者：[xiaohei7972](https://github.com/xiaohei7972)。项目组织：[CardResearchLab](https://github.com/CardResearchLab)。

当前源码版本为 **2.2.1（发布候选，尚未发布）**，GitHub 和 nuget.org 上最新正式稳定版仍为 **2.2.0**。2.2.1 在保持 2.2.0 Public API、固定 rdbo/libmem revision 及 x64/x86 包结构不变的前提下修复扫描边界、注入异常分类、NuGet 架构选择与发布工作流。历史稳定版本 **v1.0.0** 使用 `LibmemCli` 名称；身份迁移说明见 [迁移指南](docs/MIGRATION.md)。

正式支持范围：

- Windows x64
- Windows x86
- .NET 8
- C# / .NET 消费者
- 固定版本的 rdbo/libmem native backend

> 2.2.0 的 x64 与 x86 都使用架构匹配的 C++/CLI 与 native runtime。消费者必须显式选择 `x64` 或 `x86`；`AnyCPU` 与跨位数运行不在支持范围内。

## 下载

GitHub 与 nuget.org 均已发布稳定版 **Libmem.NET 2.2.0**。`2.1.1` 作为上一稳定版继续保留。

历史 v1.0.0 继续提供以下旧名称资产，不能用于下面的新命名示例：

- [LibmemCli v1.0.0](https://github.com/CardResearchLab/Libmem.NET/releases/tag/v1.0.0)
- [LibmemCli-windows-x64.zip](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip)
- [LibmemCli-windows-x64.zip.sha256](https://github.com/CardResearchLab/Libmem.NET/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256)

历史 ZIP SHA-256：`647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99`。

详细版本、完整性与版本策略见 [Release 指南](docs/RELEASES.md)。

## 快速开始

构建当前源码，或解压当前 Build 产物，在显式指定 x64 或 x86 的 .NET 8 项目中引用：

```text
Libmem.NET.dll
```

运行目录至少保留：

```text
Libmem.NET.dll
libmem.dll
Ijwhost.dll
```

建议同时保留 `Libmem.NET.xml`，以获得 IntelliSense API 文档。

### 基本示例

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

var process = NativeApi.CurrentProcess()
    ?? throw new InvalidOperationException("Current process not found.");

using var session = ProcessSession.Open(process)
    ?? throw new InvalidOperationException("Failed to open process session.");

Console.WriteLine(
    $"{session.Name} PID={session.Pid} Arch={session.Architecture} Bits={session.Bits}");

foreach (var module in session.Modules.Enumerate())
{
    Console.WriteLine(
        $"{module.Name} Base=0x{module.Base:X} Size=0x{module.Size:X}");
}
```

### 内存读写

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

using var session = ProcessSession.Open((uint)Environment.ProcessId)
    ?? throw new InvalidOperationException("Failed to open process session.");

using var allocation = session.Memory.Allocate(
    4096,
    MemoryProtection.ReadWrite);

session.Memory.Write(allocation.Address, [1, 2, 3, 4]);

var data = session.Memory.Read(allocation.Address, 4);

Console.WriteLine(string.Join(", ", data));
```

`RemoteAllocation` 实现 `IDisposable`，推荐使用 `using` 进行确定性释放。

## API 结构

推荐的新代码使用 `ProcessSession` 作为进程级入口：

| ProcessSession property | Manager |
| --- | --- |
| `Memory` | `MemoryManager` |
| `Modules` | `ModuleManager` |
| `Threads` | `ThreadManager` |
| `Scanner` | `ScanManager` |
| `Symbols` | `SymbolManager` |
| `Assembly` | `AssemblyManager` |
| `Hooks` | `HookManager` |
| `Injector` | `InjectorManager` |

底层静态 `NativeApi.*` (`global::Libmem.NET.Libmem`) API 继续保留，适合一次性调用以及需要更接近 native libmem 语义的场景。

### Process / Thread

- 进程枚举、查找与当前进程查询
- 进程存活检测
- PID + 启动时间身份校验
- 线程枚举与主线程查询

### Modules / Symbols

- 模块枚举、查找、加载与卸载
- 导出符号枚举
- 符号地址查找
- 符号 demangle

### Memory / Scanning

- Read / Write
- Set / Protect
- Allocate / Free
- `RemoteAllocation`
- DeepPointer
- DataScan
- PatternScan
- SigScan

### Assembly

- Assemble
- Disassemble
- CodeLength

`AssemblyManager` 默认使用目标进程架构。

### Hook / VMT

- Native code Hook
- trampoline
- Hook Remove / Dispose
- VMT Hook / Unhook / Reset

`HookHandle` 和 `VmtManager` 都提供明确的生命周期管理。

### Injection

`InjectorManager.InjectLibrary(...)` 返回 `InjectedModuleHandle`，用于表示一次由当前对象拥有的加载引用。

当前不支持跨位宽注入。

## 生命周期

需要资源所有权的对象采用明确的 `IDisposable` 模型：

- `ProcessSession`
- `RemoteAllocation`
- `HookHandle`
- `VmtManager`
- `InjectedModuleHandle`

显式 `Dispose()` 会执行确定性清理；如果原生清理明确失败，ownership API 会报告失败，而不是静默丢失仍然有效的资源状态。

Finalizer 不会在 GC 线程中执行危险的远程内存释放、远程代码恢复、VMT 恢复或远程模块卸载。

## 返回值与异常

v1.0 明确区分“正常未找到”和“操作失败”。

| 场景 | 行为 |
| --- | --- |
| Process / Module / Segment 未找到 | `null` |
| Symbol / Scan / DeepPointer 未命中 | libmem bad-address sentinel |
| 明确的 Manager 原生操作失败 | `LibmemException` |
| 参数错误 | 标准 .NET `Argument*` 异常 |
| Dispose 后继续使用 Manager | `ObjectDisposedException` |

完整契约见 [API 参考](docs/API.md)。

## 从源码构建

要求：

- Windows x64
- Visual Studio 2022
- Desktop development with C++
- C++/CLI support for v143 build tools
- Windows SDK
- .NET 8 SDK
- CMake
- Git

递归克隆：

```powershell
git clone --recursive https://github.com/CardResearchLab/Libmem.NET.git
cd Libmem.NET
.\build.ps1 -Configuration Release -Platform x64
```

构建流程会初始化 Git Submodule、编译固定版本 native libmem、构建 C++/CLI assembly、生成 XML 文档，并把产物写入 `artifacts/`。

主要输出：

```text
artifacts/native/x64/Release/bin/libmem.dll
artifacts/managed/x64/Release/Libmem.NET.dll
artifacts/managed/x64/Release/Libmem.NET.xml
artifacts/managed/x64/Release/Ijwhost.dll
```

## Runtime Package

当前源码候选版为 **2.2.1**：源码 `VERSION` / informational version 为 `2.2.1`，程序集和文件版本为 `2.2.1.0`。正式发布版仍是 **2.2.0**；其 GitHub Release 提供 x64/x86 Runtime ZIP、对应 SHA-256 和 `Libmem.NET.2.2.0.nupkg`，nuget.org 提供 `2.2.0` PackageReference。2.2.1 发布前请继续使用 2.2.0 公开包。

生成正式风格 Runtime ZIP：

```powershell
.\build.ps1 -Configuration Release -Platform x64
.\eng\package-runtime.ps1 -Configuration Release -Platform x64
```

输出：

```text
artifacts/package/Libmem.NET-windows-x64/
artifacts/package/Libmem.NET-windows-x64.zip
artifacts/package/Libmem.NET-windows-x64.zip.sha256
```

`manifest.json` 记录版本、仓库 commit、固定 libmem commit、目标框架、平台、构建配置以及包内文件 SHA-256。

## Git Submodule 集成

需要源码级可复现构建时：

```powershell
git submodule add https://github.com/CardResearchLab/Libmem.NET.git external/Libmem.NET
git submodule update --init --recursive
```

然后在消费方解决方案中引用：

```text
external/Libmem.NET/src/Libmem.NET.vcxproj
```

仓库也提供 reusable GitHub Actions build workflow。

```yaml
jobs:
  build-libmem:
    uses: CardResearchLab/Libmem.NET/.github/workflows/reusable-build.yml@main
    with:
      ref: main
      configuration: Release
      platform: x64
      artifact-name: Libmem.NET-windows-x64
```

为可复现构建，将 workflow 引用与 `ref` 固定到审核过的 commit。

## NuGet 状态

包 ID 为 `Libmem.NET`，目标是 Windows x64/x86 / .NET 8。CI 验证 pack、独立 x64/x86 PackageReference restore/build/run/publish、native runtime 文件复制以及 AnyCPU consumer 拒绝。

本地包通过 `eng/package-nuget.ps1` 生成；开发包使用 commit 限定的预发布版本。`v*` tag 创建 GitHub 下载，预览版本标记为 prerelease；后续手动选择已发布 tag 并启用 `publish-nuget` 才执行 NuGet OIDC 登录与 push。`release/v*` 分支只验证产物并生成发布说明。

Trusted Publishing / OIDC、NuGet push 与公开 PackageReference 消费链路均已实际验证。稳定版 `2.2.0` 已发布到 GitHub 与 nuget.org，公开 NuGet smoke 基线推进到 2.2.0，并覆盖 x64/x86；`2.1.1` 作为上一稳定版保留。见 [消费指南](docs/CONSUMPTION.md) 与 [发布清单](docs/RELEASE_CHECKLIST.md)。

## 测试与 CI

仓库使用多层验证：

- Build + Runtime Smoke
- Hook / VMT Runtime Tests
- Injector Runtime Tests
- External Process Runtime Tests
- NuGet Consumer Tests
- Public API baseline validation
- pinned libmem public API coverage validation
- Runtime package integrity validation

PR 自动验证集中在 Build，默认构建并验证 **Release x64 与 Release x86**，保留以上全部 Release 测试与多架构包验证。Actions → Build → Run workflow 中勾选 `debug`，会额外构建 Debug x64 并运行 Debug smoke tests。

以下专项工作流保留为手动入口：

- [Hook/VMT](.github/workflows/hook-vmt-tests.yml)，可手动选择 x64 / x86
- [Injector](.github/workflows/injector-tests.yml)，可手动选择 x64 / x86
- [External Process](.github/workflows/external-process-tests.yml)，使用独立的 `Libmem.NET.TestTarget`
- [NuGet Consumer](.github/workflows/nuget-consumer-tests.yml)

## Public API 稳定性

公开成员契约通过 baseline 检查。当前 `LibmemCli` → `Libmem.NET` 身份迁移是有意的 breaking change；成员签名、所有权和错误语义保持不变，消费者需要重新编译。历史 v1.x 的兼容策略不代表新身份可替换旧 DLL。

仓库通过：

```text
api/Libmem.NET.PublicApi.txt
```

冻结 namespace、公开类型、方法、属性和枚举。

有意的 breaking change 需要显式更新 API baseline、CHANGELOG，并重新评估语义版本。

## 固定上游

当前 native backend 固定到：

```text
rdbo/libmem
a07c9942bf1358dabcc83eb0cd072736c749d7f8
```

详细信息见 [UPSTREAM.txt](UPSTREAM.txt)。

## 文档

- [API Reference](docs/API.md)
- [Migration Guide](docs/MIGRATION.md)
- [Release Checklist](docs/RELEASE_CHECKLIST.md)
- [Consumption Guide](docs/CONSUMPTION.md)
- [Release & Versioning Guide](docs/RELEASES.md)
- [v1.0.0 Release Notes](docs/releases/v1.0.0.md)
- [CHANGELOG](CHANGELOG.md)
- [ROADMAP](ROADMAP.md)
- [English README](README.en.md)

## License

本项目按 [GNU AGPL-3.0-only](LICENSE) 授权。

第三方组件与对应许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
