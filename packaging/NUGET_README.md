# Libmem.NET

**English** | [简体中文](#简体中文)

Libmem.NET is a Windows-focused C++/CLI wrapper for [rdbo/libmem](https://github.com/rdbo/libmem), providing managed APIs for process, thread, module, memory, scanning, symbols, assembly/disassembly, hooks, VMT, and DLL injection.

> Current official support: **Windows x64/x86 + .NET 8**.

## Installation

```powershell
dotnet add package Libmem.NET --version 2.3.0
```

`2.3.0` adds nonbreaking Windows x64/x86 typed-memory helpers (`ReadInt64`/`WriteInt64`, `ReadPointer`/`WritePointer`) plus a pinned native API coverage audit. This candidate is not published yet; use stable 2.2.1 until release verification is complete.

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

- Windows x64 or Windows x86
- .NET 8
- C# / .NET consumers

The package carries architecture-matched C++/CLI and native runtime assets for both Windows x64 and x86.

Consumers must explicitly target x64 or x86. AnyCPU and cross-bitness operation are unsupported.

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

> 当前正式支持范围：**Windows x64/x86 + .NET 8**。

## 安装

```powershell
dotnet add package Libmem.NET --version 2.3.0
```

`2.3.0` 为 Windows x64/x86 新增 64 位整数和目标位宽指针读写，以及固定上游 C API 覆盖审计，保持 2.2.1 接口兼容且不增加 ARM64。候选版尚未正式发布，请在完成验证之前使用稳定版 2.2.1。

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

- Windows x64 或 Windows x86
- .NET 8
- C# / .NET 消费者

包内同时包含 Windows x64 与 x86 的架构匹配 C++/CLI 程序集和原生运行时文件。

消费者必须显式选择 x64 或 x86；AnyCPU 与跨位数运行不属于支持范围。

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
