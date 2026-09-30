# LibmemCli

[简体中文](README.md) | [English](README.en.md)

[![CI Build](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml/badge.svg)](https://github.com/HearthstoneModding/Libmem/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/HearthstoneModding/Libmem)](https://github.com/HearthstoneModding/Libmem/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](LICENSE)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4)

**LibmemCli** 是 [rdbo/libmem](https://github.com/rdbo/libmem) 的 Windows C++/CLI 封装，为 C# / .NET 提供进程、线程、模块、内存、扫描、符号、汇编/反汇编、Hook、VMT 与 DLL 注入能力。

当前稳定版本：**v1.0.0**

正式支持范围：

- Windows x64
- .NET 8
- C# / .NET 消费者
- 固定版本的 rdbo/libmem native backend

> x86 代码与构建配置仍保留，但不属于当前稳定支持和正式 Release 范围。NuGet 目前仍是本地/CI 原型，正式二进制分发以 GitHub Release ZIP 为主。

## 下载

推荐直接使用正式 Release：

- [LibmemCli v1.0.0](https://github.com/HearthstoneModding/Libmem/releases/tag/v1.0.0)
- [LibmemCli-windows-x64.zip](https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip)
- [LibmemCli-windows-x64.zip.sha256](https://github.com/HearthstoneModding/Libmem/releases/download/v1.0.0/LibmemCli-windows-x64.zip.sha256)

v1.0.0 ZIP SHA-256：

```text
647f93c73bbd9fc77e2eb84dc2d5530953b12212c75c09d97b19388e4b331b99
```

详细版本、完整性与版本策略见 [Release 指南](docs/RELEASES.md)。

## 快速开始

解压 Release ZIP，在 x64 .NET 8 项目中引用：

```text
LibmemCli.dll
```

运行目录至少保留：

```text
LibmemCli.dll
libmem.dll
Ijwhost.dll
```

建议同时保留 `LibmemCli.xml`，以获得 IntelliSense API 文档。

### 基本示例

```csharp
using LibmemCli;

var process = Libmem.CurrentProcess()
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
using LibmemCli;

using var session = ProcessSession.Open(Environment.ProcessId)
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

```text
ProcessSession
├── Memory      MemoryManager
├── Modules     ModuleManager
├── Threads     ThreadManager
├── Scanner     ScanManager
├── Symbols     SymbolManager
├── Assembly    AssemblyManager
├── Hooks       HookManager
└── Injector    InjectorManager
```

底层静态 `Libmem.*` API 继续保留，适合一次性调用以及需要更接近 native libmem 语义的场景。

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
git clone --recursive https://github.com/HearthstoneModding/Libmem.git
cd Libmem
.uild.ps1 -Configuration Release -Platform x64
```

构建流程会初始化 Git Submodule、编译固定版本 native libmem、构建 C++/CLI assembly、生成 XML 文档，并把产物写入 `artifacts/`。

主要输出：

```text
artifacts/native/x64/Release/bin/libmem.dll
artifacts/managed/x64/Release/LibmemCli.dll
artifacts/managed/x64/Release/LibmemCli.xml
artifacts/managed/x64/Release/Ijwhost.dll
```

## Runtime Package

生成正式风格 Runtime ZIP：

```powershell
.uild.ps1 -Configuration Release -Platform x64
.engpackage-runtime.ps1 -Configuration Release -Platform x64
```

输出：

```text
artifacts/package/LibmemCli-windows-x64/
artifacts/package/LibmemCli-windows-x64.zip
artifacts/package/LibmemCli-windows-x64.zip.sha256
```

`manifest.json` 记录版本、仓库 commit、固定 libmem commit、目标框架、平台、构建配置以及包内文件 SHA-256。

## Git Submodule 集成

需要源码级可复现构建时：

```powershell
git submodule add https://github.com/HearthstoneModding/Libmem.git external/Libmem
git submodule update --init --recursive
```

然后在消费方解决方案中引用：

```text
external/Libmem/src/LibmemCli.vcxproj
```

仓库也提供 reusable GitHub Actions build workflow。

## NuGet 状态

仓库包含一个未发布的 Windows x64 / .NET 8 NuGet 原型：

```text
HearthstoneModding.LibmemCli
```

CI 会验证 pack、PackageReference restore/build/run/publish、native runtime 文件复制以及非 x64 consumer 拒绝。

**v1.0.0 未发布到 nuget.org。**

当前稳定消费方式仍是：

1. GitHub Release ZIP；
2. Git Submodule / source integration。

详见 [消费指南](docs/CONSUMPTION.md)。

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

专项 runtime workflow 与基础 Build 分离，并可按需手动运行完整测试。

## Public API 稳定性

v1.0 起，公开 managed contract 默认保持向后兼容。

仓库通过：

```text
api/LibmemCli.PublicApi.txt
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
- [Consumption Guide](docs/CONSUMPTION.md)
- [Release & Versioning Guide](docs/RELEASES.md)
- [v1.0.0 Release Notes](docs/releases/v1.0.0.md)
- [CHANGELOG](CHANGELOG.md)
- [ROADMAP](ROADMAP.md)
- [English README](README.en.md)

## License

本项目按 [GNU AGPL-3.0-only](LICENSE) 授权。

第三方组件与对应许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
