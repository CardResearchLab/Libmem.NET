# LibmemCli 开发路线图

> 当前策略：**x64 主线优先，x86 延后。**

## 平台策略

LibmemCli 当前正式开发、默认 CI、运行时验收和 GitHub Release 均以 **Windows x64 / .NET 8** 为目标。

x86 现状：

- 现有 x86 代码、解决方案配置、构建脚本兼容入口暂时保留；
- x86 不再作为近期功能开发目标；
- 新功能不要求同步完成 x86 适配；
- x86 不作为默认 CI 合并门禁；
- GitHub Release 暂不发布 x86 ZIP / checksum；
- x86 若能继续手动构建，视为 best-effort compatibility，不构成稳定性承诺；
- 后续恢复 x86 时，单独进行 pointer width、Hook/VMT、Assembler/Disassembler、Injector、打包与 Runtime Tests 全量审计。

## 架构原则

LibmemCli 保持独立、通用的 libmem .NET/C++/CLI 封装，不与 StandaloneGameMod、Hearthstone、Unity、Mono 或任何游戏状态模型绑定。

推荐结构：

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

Snapshot、缓存、Entity、GameState、事件状态、IPC 和游戏版本适配属于调用方。

## 当前阶段：架构拆分

目标是把历史上的大型 `LibmemCli.cpp` 拆成职责清晰的实现层，同时保持 Public API 稳定。

优先顺序：

1. 拆分 `ProcessSession` 与各 Manager 实现；
2. 抽离 `Interop / NativeConverter`；
3. 抽离 native address / size / string / callback 转换；
4. 拆分 Resource Lifetime：RemoteAllocation、InjectedModuleHandle；
5. 拆分 Hook / VMT；
6. 最后让 `LibmemCli.cpp` 只保留必要的静态兼容 facade，或继续按职责拆除。

## v0.4 — x64 架构整理

重点：

- 完成 ProcessSession 聚合模型；
- 完成 Core / Memory / Modules / Threads / Scanning / Symbols / Assembly 源码拆分；
- 建立 Interop / NativeConverter 边界；
- 保持旧静态 `Libmem.*` API 兼容；
- 不加入应用或游戏业务状态。

验收标准：

- x64 Build 通过；
- x64 Runtime Smoke 通过；
- Public API baseline 通过；
- 上游 libmem API coverage 通过。

## v0.5 — 生命周期与错误模型

重点：

- RemoteAllocation 生命周期；
- HookHandle 生命周期；
- VMT 生命周期；
- InjectedModuleHandle 生命周期；
- ObjectDisposedException / 参数异常 / LibmemException 语义统一；
- Finalizer 不在 GC 线程中对远程进程执行危险恢复操作。

验收标准：

- x64 生命周期测试独立通过；
- double Dispose / invalid target / process exit 等错误路径明确。

## v0.6 — Hook / VMT / Assembly 完整化

重点：

- Hook API 稳定；
- trampoline 元数据稳定；
- VMT Hook / Unhook / Reset / Dispose 完整；
- Assembly / Disassembly / CodeLength API 稳定；
- Session API 成为推荐入口，静态 API 进入兼容维护状态。

## v0.7 — 测试与消费者体验

重点：

- Smoke Tests；
- Hook/VMT Tests；
- Injector Tests；
- 独立 TestTarget（已建立 x64 外部进程测试靶）；
- C# consumer sample（已更新为推荐的 `ProcessSession` / Manager / IDisposable / `LibmemException` 使用方式）；
- XML 文档（已建立 `LibmemCli.xml` 生成与打包链路，持续补全公开 API 注释）；
- README / API 文档。

全部以 x64 为默认验收平台。

## v0.8 — 包装与发布

重点：

- x64 Runtime package；
- manifest / SHA-256；
- GitHub Release；
- 可复用 workflow；
- NuGet 或更标准的消费方式评估。

正式 Release 只发布：

```text
LibmemCli-windows-x64.zip
LibmemCli-windows-x64.zip.sha256
```

## v0.9 — x64 API Freeze

开始冻结：

- 命名；
- namespace；
- public 类型；
- 方法签名；
- IDisposable 行为；
- 异常语义。

从这一阶段开始，破坏性 Public API 变更必须明确记录。

## v1.0 — Stable x64

v1.0 的定义是：

> LibmemCli 成为稳定、通用、可被其他 .NET 项目消费的 Windows x64 libmem C++/CLI 封装。

v1.0 不要求完成 x86。

## v1.x / 后续 — 重新评估 x86

只有 x64 主线稳定后，再决定是否恢复 x86。

如果恢复，不直接宣称“同一代码天然支持 x86”，而是重新验证：

- pointer/address width；
- native conversions；
- allocator；
- assembler/disassembler；
- Hook trampoline；
- VMT；
- Injector；
- C++/CLI runtime；
- package；
- CI；
- consumer compatibility。

通过完整测试后才恢复 x86 官方发布。
