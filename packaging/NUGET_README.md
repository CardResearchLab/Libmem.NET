# Libmem.NET

**English** | [简体中文](#简体中文)

Libmem.NET is a Windows-focused C++/CLI wrapper for [rdbo/libmem](https://github.com/rdbo/libmem), providing managed APIs for process, thread, module, memory, scanning, symbols, assembly/disassembly, hooks, VMT, and DLL injection.

> Current official support: **Windows x64 + .NET 8**.

## Installation

```powershell
dotnet add package Libmem.NET --version 2.1.1
```

`2.1.1` is the current maintenance package line for the Libmem.NET identity and preserves the 2.1.0 Public API.

## Highlights

- Process and thread inspection
- Module enumeration and management
- Memory read/write/protect/allocate/free
- Deep pointer and signature/pattern scanning
- Symbol enumeration, lookup, and demangling
- Assembly/disassembly helpers
- Native hooks and VMT hooks
- DLL injection
- Explicit managed ownership with `IDisposable`
- Native failure mapping through `LibmemException`

## Quick start

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

## Runtime requirements

The package is currently intended for:

- Windows x64
- .NET 8
- C# / .NET consumers

The package carries the managed assembly and the required Windows x64 native runtime assets.

x86 source/build configuration may exist in the repository, but x86 is not part of the current official NuGet support scope.

## 2.0 migration note

`2.0.0` is the first stable release under the `Libmem.NET` identity. The breaking identity migration was introduced and validated in `2.0.0-preview.1`.

Existing v1.0.0 consumers must update assembly references, namespaces, paths, and any reflection strings that use the old identity, then rebuild their applications. Renaming the old DLL is not sufficient.

See the full [migration guide](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/MIGRATION.md).

## Documentation

- [GitHub repository](https://github.com/CardResearchLab/Libmem.NET)
- [English README](https://github.com/CardResearchLab/Libmem.NET/blob/main/README.en.md)
- [简体中文 README](https://github.com/CardResearchLab/Libmem.NET/blob/main/README.md)
- [API reference](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/API.md)
- [Consumption guide](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/CONSUMPTION.md)
- [Release & versioning guide](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/RELEASES.md)
- [Changelog](https://github.com/CardResearchLab/Libmem.NET/blob/main/CHANGELOG.md)

## License

Libmem.NET is licensed under [GNU AGPL-3.0-only](https://github.com/CardResearchLab/Libmem.NET/blob/main/LICENSE).

Third-party license information is available in [THIRD_PARTY_NOTICES.md](https://github.com/CardResearchLab/Libmem.NET/blob/main/THIRD_PARTY_NOTICES.md).

---

# 简体中文

[English](#libmemnet) | **简体中文**

Libmem.NET 是一个面向 Windows 的 [rdbo/libmem](https://github.com/rdbo/libmem) C++/CLI 封装，为 C# / .NET 提供进程、线程、模块、内存、扫描、符号、汇编/反汇编、Hook、VMT 与 DLL 注入等托管 API。

> 当前正式支持范围：**Windows x64 + .NET 8**。

## 安装

```powershell
dotnet add package Libmem.NET --version 2.1.1
```

`2.1.1` 是 Libmem.NET 当前维护修复包版本，并保持与 2.1.0 Public API 兼容。

## 主要功能

- 进程与线程查询
- 模块枚举与管理
- 内存读写、保护、分配与释放
- Deep Pointer、特征码与模式扫描
- 符号枚举、查找与 demangle
- 汇编与反汇编辅助
- Native Hook 与 VMT Hook
- DLL 注入
- 基于 `IDisposable` 的明确资源生命周期
- 通过 `LibmemException` 映射明确的原生失败

## 快速开始

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

## 运行环境

当前 NuGet 包正式面向：

- Windows x64
- .NET 8
- C# / .NET 消费者

包内包含托管程序集以及 Windows x64 所需的原生运行时文件。

仓库中可能仍保留 x86 源码或构建配置，但 x86 当前不属于 NuGet 正式支持范围。

## 2.0 迁移说明

`2.0.0` 是 `Libmem.NET` 新身份下的首个稳定版本；这次破坏兼容性的身份迁移已在 `2.0.0-preview.1` 中引入并完成验证。

现有 v1.0.0 使用者需要更新程序集引用、命名空间、路径以及使用旧身份的反射字符串，并重新编译应用。仅重命名旧 DLL 无法完成迁移。

完整说明见 [迁移指南](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/MIGRATION.md)。

## 文档

- [GitHub 仓库](https://github.com/CardResearchLab/Libmem.NET)
- [English README](https://github.com/CardResearchLab/Libmem.NET/blob/main/README.en.md)
- [简体中文 README](https://github.com/CardResearchLab/Libmem.NET/blob/main/README.md)
- [API 参考](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/API.md)
- [消费指南](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/CONSUMPTION.md)
- [发布与版本策略](https://github.com/CardResearchLab/Libmem.NET/blob/main/docs/RELEASES.md)
- [变更记录](https://github.com/CardResearchLab/Libmem.NET/blob/main/CHANGELOG.md)

## 许可证

Libmem.NET 使用 [GNU AGPL-3.0-only](https://github.com/CardResearchLab/Libmem.NET/blob/main/LICENSE) 许可证。

第三方组件与许可信息见 [THIRD_PARTY_NOTICES.md](https://github.com/CardResearchLab/Libmem.NET/blob/main/THIRD_PARTY_NOTICES.md)。
