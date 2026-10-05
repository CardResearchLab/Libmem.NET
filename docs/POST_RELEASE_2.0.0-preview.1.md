# Libmem.NET 2.0.0-preview.1 发布后执行计划

目标：完成 `2.0.0-preview.1` 的真实外部消费验收，清理发布遗留，再决定进入 `2.0.0` 正式版还是继续 `preview.2`。

固定边界：

- Windows x64 / .NET 8 为当前正式目标；
- x86 继续延后，不在本轮增加 x86 新功能；
- 不加入 Snapshot、GameState、Entity、IPC、Hearthstone、Unity、Mono 或游戏版本业务逻辑；
- 不为“顺手优化”进行跨范围重构；
- Hook / VMT / Injector 只有在验收发现明确缺陷时才修改行为；
- 每一阶段通过验收后才进入下一阶段。

## 阶段 1 — 公开 NuGet 外部消费者验收

状态：完成。

目的：证明普通用户仅通过 nuget.org 的 `PackageReference` 就能安装、构建、运行和发布 Libmem.NET。

执行：

1. 仅使用 `https://api.nuget.org/v3/index.json` 还原 `Libmem.NET 2.0.0-preview.1`；
2. 使用独立 `tests/Libmem.NET.NuGetConsumer` 项目，不使用 ProjectReference 或本地 feed；
3. Windows x64 Release 构建；
4. 运行真实 API smoke：
   - 程序集 / namespace 身份；
   - ProcessSession；
   - Allocate / Write / Read / Dispose；
5. 验证 NuGet 包中的 `lib/net8.0/Libmem.NET.xml`，并在 `dotnet publish` 输出中检查：
   - `Libmem.NET.dll`
   - `libmem.dll`
   - `Ijwhost.dll`
6. 以 AnyCPU / 非 x64 方式构建必须被包的 MSBuild 约束拒绝。

验收条件：

- Published NuGet Smoke workflow 全绿；
- 还原源只包含 nuget.org；
- 运行时 smoke 成功；
- publish 产物完整；
- 非 x64 拒绝符合预期。

完成记录：Published NuGet Smoke #2 与 Build #288 均通过，PR #91 已合并。

## 阶段 2 — GitHub 分支清理审计

状态：完成。

目的：减少已经被 main 吸收、废弃或仅属于旧迁移阶段的分支，避免后续开发继续在错误分支上发生。

执行顺序：

1. 列出所有远程分支；
2. 对每个分支检查：
   - 是否已经合并到 main；
   - 是否存在 main 没有的独有提交；
   - 是否仍有关联开放 PR；
   - 是否属于历史发布 / 迁移 / API freeze / 已完成 refactor；
3. 分类为：
   - 保留；
   - 可删除；
   - 需要人工复核；
4. 先输出删除清单，不直接删除有独有提交的分支；
5. 删除确认安全的分支；
6. 发布验收结束后删除 `release/v2.0.0-preview.1` 分支，正式 tag 保留。

验收条件：

- 没有开放 PR 指向待删分支；
- 没有误删 main 未包含的有效工作；
- 远程分支数量明显收敛；
- main 与 release tag 不受影响。

完成记录：远程分支从 102 个收敛到 18 个（`main` + 17 个仍有独有提交、待复核分支）；84 个已确认安全的历史分支已删除。

## 阶段 3 — 2.0.0 稳定化审计

状态：进行中。

目的：确认命名迁移、Public API、NuGet、文档和运行时分发已经一致，不再带着 preview 阶段遗留进入正式版。

审计：

1. Public API baseline 与 `src/Libmem.NET.h`；
2. XML IntelliSense 与 `docs/API.md`；
3. Runtime ZIP 与 NuGet 包内容；
4. manifest / repository provenance / version metadata；
5. README / README.en / CONSUMPTION / RELEASES / RELEASE_CHECKLIST；
6. 搜索过时表述：
   - “NuGet 尚未公开发布”；
   - “preview publication pending”；
   - 旧组织 URL；
   - 非历史语境中的旧 `LibmemCli` 身份；
7. Build / runtime smoke / external process / Hook-VMT / Injector / NuGet consumer 全部保持绿色。

验收条件：

- 无未解释的 Public API 漂移；
- 文档准确描述已发布的 `2.0.0-preview.1`；
- 没有发布链路遗留错误；
- 没有必须通过 breaking change 才能修复的问题。

## 阶段 4 — 版本决策

状态：等待阶段 3。

规则：

- 如果阶段 1–3 没有发现需要代码或包布局修复的问题：进入 `2.0.0` 正式版准备；
- 如果发现需要修复但不需要再次 breaking change：发布 `2.0.0-preview.2`，重复阶段 1；
- 如果发现新的 breaking design 问题：停止正式版，先记录设计决策并单独评审，不直接修改 Public API。

## 阶段 5 — 恢复 Libmem.NET 本体开发

状态：等待稳定版决策。

只有在 2.0.0 稳定路线明确后再继续功能开发，优先级：

1. native libmem Public API coverage 差距；
2. Assembly / Symbols 完整性；
3. Hook / VMT 健壮性；
4. Injector 健壮性；
5. 生命周期与异常边界；
6. 消费者体验、文档、测试；
7. 最后才重新评估 x86。

继续保持 Libmem.NET 为独立、通用封装库，不吸收 StandaloneGameMod 或 Hearthstone 业务层职责。
