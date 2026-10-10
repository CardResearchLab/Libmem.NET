# Libmem.NET 开发路线图

**2.5.0 已正式发布**（2026-10-11 北京时间）：主线 Build #461、发布预演 #50、GitHub Release #51、NuGet Trusted Publishing #52 完成；发布后独立 NuGet x64/x86 Smoke #48 已在 PR #148 通过。现阶段优先稳定性维护，不扩展 ARM64 或跨平台范围。

> 当前策略：**Windows x64 与 x86 同级支持；共享设计保持架构中立，为后续 ARM64 留出扩展点。**

## 平台策略

Libmem.NET 2.2.0 的正式开发、默认 CI、运行时验收、NuGet 与 GitHub Release 均覆盖 **Windows x64 / x86 + .NET 8**。

x86 状态：

- Release x86 构建、Smoke、Hook/VMT、Injector 与 ExternalProcessTests 已进入默认验证；
- Runtime ZIP、manifest 与 SHA-256 校验同时覆盖 x64 和 x86；
- 一个 NuGet 包同时携带 `win-x64` 与 `win-x86` 的匹配 C++/CLI/native 资产；
- `buildTransitive` 根据显式 `Platform` / `PlatformTarget` 选择架构；
- `AnyCPU` 明确拒绝，跨位数操作不作为支持能力；
- ARM64 暂不属于 2.2.0 正式支持范围，但共享测试与打包结构不得写死 x86。

## 架构原则

Libmem.NET 保持独立、通用的 libmem .NET/C++/CLI 封装，不与 StandaloneGameMod、Hearthstone、Unity、Mono 或任何游戏状态模型绑定。

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

## 已完成阶段：v2.1.1 — 发布后维护

2.1.1 是 2.1.0 的向后兼容 patch，不增加 Public API，也不升级 pinned libmem。已完成：

- 修复 `VmtManager` 构造函数未拒绝 bad-address sentinel 的托管边界缺口；
- 为该无效 VTable 基址补充运行时和源码契约回归测试；
- 保留 2.1.0 初次 nuget.org restore 失败作为历史发布证据，并在 2.1.1 发布后将自动 public-NuGet smoke 基线推进到 2.1.1；
- 清理 2.1.1 发布后残留的 candidate / stable / NuGet 状态文档。

## 已完成阶段：v2.1.0 — Hook / VMT Hardening

`2.0.0` 已于 2026-10-06 正式发布，Windows x64 / .NET 8 的程序集、NuGet 包、运行时 ZIP、校验文件和 Public API 基线已经形成稳定基线。2.1.0 不进行新的命名迁移，也不主动引入 breaking change。

2.1.0 的目标是：

> 在保持 2.0.0 Public API 兼容的前提下，把 Hook / VMT 从“可用”推进到“失败路径清晰、生命周期稳定、运行时覆盖完整”。

最终验收前审计结论：

- `HookManager.Install` 的零值/bad-address、definite native failure 与 disposed-session 契约已经冻结；
- self-process 与 external-process runtime tests 已覆盖 hook 跳转、trampoline、指令边界、Remove/Dispose 幂等、目标退出与失败后 ownership 重试；
- `VmtManager` 已覆盖重复 Hook、untracked Unhook、Reset/reuse、Dispose 以及 restore-failure retry 生命周期；
- remote unhook 已对 trampoline 完整读取做 preflight，避免 pinned `LM_UnhookCodeEx` 失败时泄漏 source protection 状态；
- duplicate/overlapping code-hook 冲突不由 Libmem.NET 建立全局 registry 处理；relative-control-flow trampoline 安全性继承 pinned libmem 能力，这两项已作为调用方/上游边界明确记录。

### 2.1.0 工作项

1. **Managed 参数与状态契约**
   - 审计 source / destination / trampoline 地址的零值、bad-address 与位宽处理；
   - 明确 session disposed、target exited、重复 Remove、Dispose 后 Remove 的行为；
   - 不在所有 Hook 热路径加入昂贵的通用进程枚举 preflight。

2. **Hook 安装 / 卸载失败路径**
   - 验证 `LM_HookCodeEx` 失败不会留下可观察的半安装 managed handle；
   - 验证 `LM_UnhookCodeEx` 失败时 `HookHandle` 保留 ownership，允许显式重试；
   - 增加重复 hook、重叠 source、无效 destination 等回归场景；
   - 保持 static `Libmem.HookCode` 的兼容 facade 语义，Manager 层继续负责明确的 definite-failure exception。

3. **Trampoline / 指令边界**
   - 验证 `PatchedBytes` 与 trampoline 元数据的一致性；
   - 对短函数、边界指令、相对跳转等场景补 runtime coverage；
   - 不在 managed 层重新实现 native libmem 已负责的反汇编/重定位算法。

4. **VMT 生命周期加固**
   - 增加重复 Hook/Unhook、未 Hook index、Reset 后复用、Dispose 异常路径测试；
   - 明确 replacement address / index 参数契约；
   - 保持 VMT 为 local-process-only，不扩展为远程 VMT 抽象。

5. **独立运行时测试**
   - 保留当前 self-process Hook/VMT 测试；
   - 增加基于 `Libmem.NET.TestTarget` 的外部进程 Hook 生命周期测试；
   - 覆盖 target exit 后 owning handle 的状态收敛；
   - CI 默认继续只要求 Windows x64 Release。

6. **消费者文档与示例**
   - 在 `docs/API.md` 补充 Hook/VMT 的失败、ownership、线程和 target-exit 契约；
   - 增加 C# Hook consumer 示例；
   - Public API baseline 作为合并门禁，2.1.0 默认不增加破坏性成员变更。

### 2.1.0 明确不做

- Mono / Unity / Hearthstone 方法解析；
- Harmony-compatible Patch API；
- GameState / Entity / Snapshot / IPC；
- 游戏版本适配；
- x86 正式支持；
- 为某个具体游戏增加 Hook policy。

这些能力属于调用方（例如 StandaloneGameMod），而不是 Libmem.NET。

### 2.1.0 验收

- Windows x64 Release build 通过；
- Hook/VMT runtime tests 全部通过；
- 新增外部进程 Hook 失败/退出路径通过；
- Public API baseline 无未记录 breaking change；
- XML IntelliSense / `docs/API.md` 与实现一致；
- NuGet consumer restore/build/run smoke test 通过。

## 已完成：v2.2.0 — Windows x86 正式支持

2.2.0 的发布目标是把已经通过验证的 x86 能力正式交付给消费者：

- x64 / x86 都通过默认 Release CI 与独立运行时测试；
- ExternalProcessTests/TestTarget 使用架构 fixture 隔离机器码差异；
- Runtime ZIP 与 checksum 对两个架构分别发布；
- NuGet 以单包提供 x64/x86 架构匹配资产，并拒绝 AnyCPU；
- 保持 2.1.1 Public API 与 pinned libmem revision 不变。

## 已完成：v2.3.0 — Native API Coverage / 基础 API 补全

- 对照 pinned upstream C API 建立可复查的 Native → Managed coverage 清单，不因为版本数字而强行更新依赖；
- 补充 x86/x64 目标位宽的 Typed Memory/Pointer 读写，保持既有 Read/Write 短读短写契约不变；
- 扫描/注入/资源生命周期开展回归审计，完善异常与正常 miss 的区分；
- 已通过 Windows x86/x64 Release CI、NuGet consumer、GitHub Release #42 和公网 NuGet Smoke #30。

## 已完成：v2.4.0 — Assembly / Symbols / Hook 实用增强

- 优先完善现有 AssemblyManager / SymbolManager 的实用辅助接口、参数边界和运行时覆盖；
- Hook/VMT 继续验证指令边界、生命周期、trampoline/原始字节一致性，避免未审核的跳板重定位改写；
- 必须继承 2.3.0 的公共 API 和 native ABI 验收，不再改变已正式支持的平台；
- 2.4.0 已正式发布；阶段性停止新功能，转入问题修复、回归测试和文档维护。
- PR #129/#132、主线 Build #425、发布预演 Release #44、GitHub Release #45、NuGet #46 及公网测试 #36 均已完成。

## 已完成：v2.4.1 — 稳定性维护补丁

- 修复 `ReadAlignedCode` 在不可读内存页边界的完整指令解析问题。
- 远程 Allocate/Free/Protect/Hook/Load/Unload 增加 PID + 进程创建时间预检查；保留非原子 TOCTOU 风险说明。
- 将 Hook 删除失败时的 trampoline 回归测试改为确定性的 `PAGE_NOACCESS` 场景。
- 发布证据：主线 Build #445、Release 预演 #47、GitHub Release #48、NuGet OIDC #49；发布后公网消费测试以实际 CI 结果为准。

## 已完成：v2.5.0 — Try API 与 Hook/VMT 稳定性增强

PR #144 新增 ScanManager 三个 Try 扫描方法，#145 新增 MemoryManager.TryRead/TryWrite，#146 加固本地 Hook 卸载并补充 VMT 重试测试；PR #147 整理发布候选元数据。主线 Build #461 与发布预演 #50 全绿，GitHub Release #51 和 NuGet #52 发布成功。PR #148 的 Published NuGet Smoke #48 已确认来自 nuget.org 的 x64/x86 消费者、XML/native 资产和不支持平台拒绝行为，合并后继续核查主线 Smoke。继续不支持 ARM64、AnyCPU、跨位数和游戏业务。

## 已完成：v2.0.0 — Stable Libmem.NET identity

2.0.0 完成了从旧 `LibmemCli` identity 到 `Libmem.NET` 的 namespace、assembly、package 和文档统一；正式支持目标为 Windows x64 / .NET 8。2.x 后续版本以兼容 2.0.0 Public API 为默认约束。

## v0.4 — x64 架构整理

重点：

- 完成 ProcessSession 聚合模型；
- 完成 Core / Memory / Modules / Threads / Scanning / Symbols / Assembly 源码拆分；
- 建立 Interop / NativeConverter 边界；
- 保持旧静态 `NativeApi.*` API 兼容；
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
- XML 文档（已建立 `Libmem.NET.xml` 生成与打包链路，持续补全公开 API 注释）；
- README / API 文档（已建立 `docs/API.md` 消费者行为参考）。

全部以 x64 为默认验收平台。

## v0.8 — 包装与发布

重点：

- x64 Runtime package；
- manifest / SHA-256；
- GitHub Release；
- 可复用 workflow；
- NuGet 或更标准的消费方式评估（已完成本地 x64 `.nupkg`、公开 prerelease、独立 `PackageReference` restore/build/run/publish 与 Trusted Publishing 全链路验收）。

正式 Release 只发布：

```text
Libmem.NET-windows-x64.zip
Libmem.NET-windows-x64.zip.sha256
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

> Libmem.NET 成为稳定、通用、可被其他 .NET 项目消费的 Windows x64 libmem C++/CLI 封装。

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
