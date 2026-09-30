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

## 当前阶段：v0.9 — x64 API Freeze

当前主线已经完成 x64 API、资源生命周期、错误模型、Hook/VMT、外部进程测试、消费者文档与本地 NuGet 消费原型。现在停止无目的扩展，转入 **Windows x64 managed contract 冻结**。

当前审计重点：

1. 冻结 managed namespace、public 类型、方法名、签名与 overload 形状；
2. 固定 `ProcessSession` / Manager 的目标进程、退出与 Dispose 行为；
3. 固定 `RemoteAllocation`、`HookHandle`、`VmtManager`、`InjectedModuleHandle` 的 ownership / 幂等语义；
4. 统一 null、非法参数、正常 miss、native failure 的返回值与异常契约；
5. 清理仅为 v0.x 迁移保留、若进入 v1.0 会形成长期负担的 public API；
6. 核对 XML IntelliSense、`docs/API.md` 与 public API baseline；
7. 识别任何会迫使 v1.0 之后 breaking change 的设计。

这一阶段仍然不加入 Snapshot、GameState、Entity、IPC、Hearthstone 或游戏版本业务逻辑。

已完成的收口项：`MemoryManager` 上仅用于 v0.x 迁移的 `DeepPointer / DataScan / PatternScan / SigScan` 转发入口已移除，session-bound 扫描统一冻结在 `ProcessSession.Scanner`；静态 `Libmem.*` 兼容层继续保留。

已完成的收口项：目标进程退出不会隐式 Dispose `ProcessSession`；Session 保留原始身份元数据，`IsAlive()` 返回 false、`Refresh()` 返回 null，Manager 属性保持可访问。为避免外部进程精确身份检查污染读写/扫描热路径，不对所有 Manager 操作追加统一 liveness preflight。

已完成的收口项：`ProcessInfo` 上仅用于早期便捷调用的 `Read / Write / ReadInt32 / WriteInt32 / SigScan` 已移除，仅保留与进程身份直接相关的 `IsAlive()`；Memory / Scan 操作统一归属 `ProcessSession` Managers，静态 `Libmem.*` 兼容层继续保留。

已完成的收口项：完成冻结后 managed surface 的 XML IntelliSense / `docs/API.md` 一致性审计；补齐推荐 `ProcessSession` / Manager / ownership 类型的成员说明，并明确 `ProcessSession.Allocate` 作为正式 ownership convenience 保留。该项不改变 Public API baseline 或 runtime 行为。

已完成的收口项：`ProcessInfo` 冻结为由 LibmemCli 创建的只读身份/元数据对象。消费者不能再修改 `Pid / StartTime` 等字段，也不能通过 public 默认构造器伪造空身份；`IsAlive()`、`Open(ProcessInfo)` 与 PID + StartTime 精确身份模型因此共享同一不可变基础。

当前收口项：`ModuleInfo` 冻结为由 LibmemCli 创建的只读模块描述对象。消费者不再能够修改 `Base / End / Size / Name / Path` 后再把伪造或变异后的模块记录传回 Unload / Symbol API。

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
- README / API 文档（已建立 `docs/API.md` 消费者行为参考）。

全部以 x64 为默认验收平台。

## v0.8 — 包装与发布

重点：

- x64 Runtime package；
- manifest / SHA-256；
- GitHub Release；
- 可复用 workflow；
- NuGet 或更标准的消费方式评估（已建立本地 x64 `.nupkg` + 独立 `PackageReference` 消费者验证，公开发布仍待全链路验收）。

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
