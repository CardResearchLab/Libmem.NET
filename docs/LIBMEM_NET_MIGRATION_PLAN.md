# Libmem.NET 全面命名统一实施方案

日期：2026-10-04（Asia/Shanghai）。本次范围是 Windows x64 / .NET 8 通用 C++/CLI 封装库的命名迁移。

## 1. 已核对的基线与此前失败

- 仓库：`HearthstoneModding/Libmem.NET`，目标分支 `main`。
- 本次读取到的主线 SHA：`6850e023487c1b87abae9a2285b858c353dcefb5`，来自 PR #74。
- 主线已采用 `Libmem.NET` 仓库名、产品名和 NuGet PackageId；Solution、源码 namespace、托管程序集及部分发布产物仍采用 `LibmemCli`。
- 此前全面迁移 PR #76–#86 均已关闭、未合并。本次从主线重新实施，不叠加那些迁移分支。
- 基线 Build run `36728507029` 成功；基线 Release run `36745443028` 失败。构建成功不等于已成功发布 NuGet。
- 旧集成 Build run `37154740333` / job `111295717283` 中，C++/CLI 编译及 XML 文档生成已完成，随后 `samples/Example.cs:15` 报 CS0234：`Libmem.CurrentProcess` 被解析为根命名空间 `Libmem`。
- 该日志证明一个明确的名称解析问题；不能据此假定其余运行时测试已经通过，因为后续步骤被跳过。
- 当前执行环境为 Linux，无 Windows MSVC C++/CLI 编译器。这里运行源码契约、API、打包验证器测试；真实编译和运行时验证由 Windows CI 完成。

## 2. 目标命名与保留边界

| 对象 | 当前名称 | 最终名称 |
| --- | --- | --- |
| 产品 / 仓库 / NuGet PackageId | Libmem.NET | Libmem.NET |
| Solution | LibmemCli.sln | Libmem.NET.sln |
| 托管项目 | src/LibmemCli.vcxproj | src/Libmem.NET.vcxproj |
| 主头文件 / 主翻译单元 | LibmemCli.h / LibmemCli.cpp | Libmem.NET.h / Libmem.NET.cpp |
| C# namespace | LibmemCli | Libmem.NET |
| C++/CLI namespace | LibmemCli | Libmem::NET |
| 内部转换 namespace | LibmemCli::Interop | Libmem::NET::Interop |
| TargetName / 程序集身份 | LibmemCli | Libmem.NET |
| 托管产物 | LibmemCli.dll / .pdb / .xml | Libmem.NET.dll / .pdb / .xml |
| 公共 API 基线 | api/LibmemCli.PublicApi.txt | api/Libmem.NET.PublicApi.txt |
| 测试目录与工程 | LibmemCli.*Tests / TestTarget / NuGetConsumer | Libmem.NET.*Tests / TestTarget / NuGetConsumer |
| 本地消费版本属性 | LibmemCliTestPackageVersion | LibmemNetTestPackageVersion |
| TestTarget 环境变量 | LIBMEMCLI_TEST_TARGET_DLL | LIBMEM_NET_TEST_TARGET_DLL |
| runtime ZIP / artifact | LibmemCli-windows-x64 | Libmem.NET-windows-x64 |

保留：所有公开类型名、成员签名、默认参数、枚举值、异常语义及资源生命周期。保留 `libmem.dll`、`libmem.lib`、`Ijwhost.dll`、native `LM_*` 名称和固定的上游 submodule commit。现有 x86 配置保留，但不开发、不发布、不新增验证目标。

项目继续独立、通用；不增加 GameState、Entity、Snapshot、IPC、Hearthstone 业务或版本逻辑；不修改 StandaloneGameMod 仓库。本次也不升级 native libmem、SDK、依赖或重新设计 Hook/VMT/Injector。

## 3. 兼容性决策

这是有意的源代码与二进制兼容性变更。旧消费者需要更新 `using`、程序集引用、构建路径及反射字符串并重新编译；旧 DLL 不能仅改文件名冒充新程序集。

不额外维护两套公开 namespace 或两套 DLL。旧发布和 Git 历史保留，迁移说明明确新旧对应关系。正式版本号、Release tag 和 NuGet 上传不在本次代码修改中自动创建。

`Libmem.NET` 为正式 namespace，全部大写 `NET`；不混入 `Libmem.Net`。C++ 使用 `namespace Libmem::NET`，项目已有 C++17 配置支持嵌套 namespace 写法。

现有静态类仍叫 `Libmem`。C# 示例和测试使用显式别名：

```csharp
using Libmem.NET;
using NativeApi = global::Libmem.NET.Libmem;

var process = NativeApi.CurrentProcess();
```

C++ 中涉及同名根 namespace 与静态类时采用明确的全限定类型，必要时限定 `::Libmem::NET::Architecture` 等引用。`RootNamespace` 只作为工程元数据同步，显式源码 namespace 必须另外迁移。

## 4. 实施顺序与提交边界

使用一个新的迁移分支 `migration/libmem-net-unified-20261004` 和一个最终 PR。阶段提交方便审查与回退；只有完整迁移后的提交进入正式 CI，不逐文件提交到远端触发重复运行。

### 阶段 0：方案与基线

1. 记录上述主线 SHA、成功 Build 和失败日志。
2. 初始化固定 submodule；运行原版源码契约、公共 API 与 release-notes 验证器测试。
3. 保存本方案，建立本地迁移分支。
4. 基线测试如有失败，先区分环境缺失与代码问题，不把失败隐藏在改名中。

交付：本方案、原版 API 与 native commit 的可对比记录。这里不把 Linux 源码检查称为 Windows 编译成功。

### 阶段 1：工程、程序集与源码同步迁移

1. 改 Solution、vcxproj、主头文件和翻译单元名称；保持项目 GUID、x64 配置、链接库和 native 构建行为。
2. 更新 TargetName、RootNamespace、AssemblyTitle、IntDir、项目引用及 build.ps1。
3. 迁移所有 namespace、using namespace 和显式类型引用。
4. 处理静态类 Libmem 与根 namespace 同名造成的 C++/C# 解析问题。
5. 先比较迁移前后的 API：规范化允许的 namespace 变化后，公开声明必须逐条一致，再写入新的 API 基线。

交付：可以继续验证的完整工程与源码；不增加业务代码或改变行为。

### 阶段 2：测试与示例

1. 改全部测试工程目录、文件、程序集名及引用路径。
2. 保留测试断言与执行逻辑，迁移类型引用、输出标签和 TestTarget 定位。
3. 在实际静态调用处使用 NativeApi 别名，保留 typeof/反射对新身份的校验。
4. 编译并运行 Example，避免再次出现库已编译但示例未通过的情况。
5. 补充程序集名、公开类型 namespace 和实际 facade 调用的验证，覆盖本次真实故障。

交付：Smoke、ExternalProcess、HookVmt、Injector、NuGetConsumer 与示例的新名称入口。

### 阶段 3：NuGet、runtime 与发布链

1. NuGet PackageId 保持 Libmem.NET，更新包内 managed DLL/XML 文件及验证器预期。
2. 更新 package-runtime、package-nuget、manifest、ZIP/checksum、release-notes 和 reusable workflow。
3. 修正当前 runtime manifest 中仍写旧仓库 `HearthstoneModding/Libmem` 的来源字段，并同步严格验证规则。
4. 验证 runtime 包包含托管 DLL/XML、native DLL、IJW host、许可和 provenance；符号按现有机制处理，不凭空承诺已存在 snupkg。
5. 本地 NuGet 消费必须 restore、run、publish，发布目录包含三项运行时 DLL；继续拒绝 AnyCPU 等非 x64 消费配置。

交付：新名称的完整打包路径；不自动上传 nuget.org。

### 阶段 4：集中 CI 与文档

1. 将 PR 自动验证集中到 Build workflow：一次干净 checkout，Debug 与 Release x64 各构建一次，在 Release 产物上依次执行所有已有运行时验证与 NuGet 消费验证。
2. 原四个专项 workflow 保留完整的手动诊断入口，改为 workflow_dispatch，避免同一个 PR 重复进行五套 native/managed 构建。
3. 保留 reusable 和 Release 入口，不减少断言、不加 continue-on-error、不跳过失败验证。
4. 更新中英文 README、消费指南、API 文档和当前发布说明。历史 CHANGELOG 原记录不做改写，只追加本次 breaking migration 条目。
5. 最终旧名审计区分当前有效入口与历史记录；方案、迁移映射和历史记录允许出现旧名，其余命中必须解释或修复。

交付：一个自动验证入口、保留专项诊断能力、与实际代码一致的文档。

## 5. 验收矩阵

| 验证 | 环境 | 通过标准 |
| --- | --- | --- |
| 源码/工程契约 | Linux + Windows | 原有断言全部通过，XML 和路径有效 |
| Public API 对比 | Linux + Windows | 仅 namespace/身份变化，公开签名和枚举保持一致 |
| 改名与引用审计 | Linux | 活跃入口没有旧名，文件改名无遗漏 |
| 打包/release-notes 验证器测试 | Linux + Windows | 原有正反例通过，预期产物采用新名称 |
| Debug / Release 编译 | Windows MSVC | x64 两种配置及 Example 编译通过 |
| Example + Smoke | Windows | 新 DLL 身份正确，当前进程、内存、扫描、资源释放及已有 smoke checks 通过 |
| ExternalProcess | Windows | 新 TestTarget 定位成功，实际跨进程操作断言通过 |
| Hook / VMT | Windows | 原有安装、调用、恢复、生命周期断言通过 |
| Injector | Windows | 原有本地/远程加载及释放测试通过 |
| Runtime package | Windows | ZIP、checksum、manifest、文件集合与提交来源一致 |
| NuGet consumer | Windows | 本地包 restore/run/publish 成功，运行时依赖完整，非 x64 拒绝测试通过 |
| 最终 CI | GitHub Windows runner | 所有步骤在同一 head SHA 成功，无跳过或放宽 |

## 6. 风险与处置

- 同名解析：显式别名和全限定名，并以真实消费者编译验证，不能仅靠搜索判定成功。
- 残留路径：检查 Solution/project references、CopyToOutputDirectory、targets、脚本、workflow env 与包内路径。
- API 基线误更新：先做规范化前后比较，确认没有意外签名变化再接受新基线。
- 旧产物污染：CI 干净 checkout，Debug/Release 和测试目录分离；不把旧 DLL 带进本地消费验证。
- 编译环境差异：当前 Linux 验证只覆盖其适用范围；Windows CI 失败时读取首个真实失败并修复，不能宣布迁移完成。
- CI 消耗：仅完整提交创建 PR，集中构建；只在有修复提交时重跑，不重复触发全部专项 workflow。
- 并发修改 main：远端提交前复核主线 SHA，若变化则重新对齐并复验。

## 7. 合并与回退规则

本次先提交迁移分支与 PR。main 保持基线；Windows 验证完成前 PR 保持 draft，不开启自动合并，不创建 Release 或发布包。

未合并时，关闭本次 PR 即可恢复原工作状态，不移动 main、不删除旧迁移分支。若后续明确批准合并且出现回归，使用对应合并提交的 git revert，保留历史；避免 force push。发布前保留旧版包与版本说明，不能覆盖已发布 NuGet 版本。

最终交付需报告：变更文件范围、最终 branch/head SHA/PR 链接、通过的验证、未完成的验证与实际失败原因。只有全部验收通过，才称“全面迁移完成”。
