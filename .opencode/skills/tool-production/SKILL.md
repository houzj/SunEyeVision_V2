---
name: tool-production
description: 按原厂证据驱动开发、迁移、复核和验收 SunEyeVision 工具（单项目结构、公共参数链、Release 构建、探针与 Agent Desktop 门禁）。新增工具、补齐参数/结果/UI、批量接入或单工具闭口时使用。
---

# 工具生产流水线

用一条可重复的循环生产工具：证据 → 声明 → 单项目 → 公共链路 → 静态门禁 → Release → 运行复核 → 差异修复 → 回归 → 闭口。

本技能是操作规程，不是需求文档。需求与当前进度以仓库为准：

- `AGENTS.md`（项目硬规则，优先级高于本技能）
- `docs/collaboration/CURRENT_TASK.md` 顶部摘要与当前有效批次
- `docs/evidence/tool-batch-manifest-20261120.json`（12 个工具的机器可读清单）
- `docs/collaboration/acceptance/完整同构与批量落地方案-20261120.md`
- `docs/collaboration/acceptance/工具生产流水线与自迭代机制-20261120.md`

## 一、硬规则（会被门禁或清单直接核查）

1. **证据先行**：工具身份、页签、分组、字段、控件、端口、结果、资源、生命周期都必须有原厂证据（反编译源码 > 原厂配置 > 原厂运行快照）。没有证据的行为写进 `unconfirmed`，不写猜测性代码。
2. **单项目**：每个工具一个产品项目，内部分 `Algorithms/`、`Parameters/`、`Results/`、`Editor/`、`Resources/`。不伪造原厂 `Algorithm.dll` / `Bridge.dll`，不复制原厂程序集数量。
3. **公共链路优先复用**：参数页、页签、字段模板、ROI、AssistGroup、结果树、图像叠加、底栏和窗口生命周期由 `Plugin.Wpf` 承担；工具只提供身份、声明、语义、资源身份和专属 Editor 接缝。不得为单个工具先改公共框架。
4. **不改算法外部行为**：算法库可用 HALCON / OpenCvSharp，但参数语义、端口、结果结构和生命周期保持原厂对应。适配层负责第三方类型与资源所有权。
5. **未确认只登记**：`unconfirmed` 项留在清单和证据里，不生成代码、不在汇报里写成已实现。
6. **自测闭口 = 构建 + 静态门禁 + 探针 + Release 运行**；人工视觉验收单独记录，不能互相代替。
7. **提交用中文**；阶段成果由主线决定提交与推送，自动迭代循环内不提交。
8. **不保留旧兼容层**：不设旧 SDK、旧接口、旧参数模型、旧控件的双轨实现或兼容回退。
9. **静态声明优先**：动态数量的列表（区间、条件行）默认用固定槽位 + 行显隐实现，槽位就是普通声明字段，直接复用成对提交和引用会话。只有静态槽位确实不成立时，才评估动态声明，并先填 `references/contract-change-checklist.md`。
10. **同一批改测试**：改字段模型、数量语义或声明时，声明测试和执行测试必须在同一批更新，不能留到下一轮。
11. **顺序证据优先**：页签、分组和字段的可见顺序以原厂声明映射和 `FieldPresentation.Order` 为准。控件创建顺序、LINQ 枚举顺序和字典顺序不能作为顺序依据。原厂只给出 XML 声明顺序时，先登记 `Order` 的未确认状态，不把推断写成事实。
12. **显示对象完整登记**：原厂 `ImageViewConfig` 的每一项都要先登记为结果页行。`CustomVisible`、`PanVisible` 只代表叠加对象的初始状态或操作能力，不能据此删除结果页行。结果页图形行、纯显隐行、文本行分别对照 `Display.xml` 和 `AlgorithmTab.xml`。
13. **动态列表要证据分流**：有原厂动态子项证据时，字段身份、数量键、增删语义和端点能力必须先写入合同检查表。固定槽位只在公共框架能够保留原厂字段身份、成对引用和行显隐时采用。若固定槽位会改变持久化语义，停在 `unconfirmed`，不要用自定义文本框把多个字段压成一个字符串。
14. **发布前置检查**：任何 Release 构建前先执行 `Check-ReleasePreflight.ps1`。它检查工作区根、HALCON 运行时、Release 目录、已知宿主进程和关键文件可写性。文件被占用或权限异常时停止在环境门禁，不能继续改代码或把启动卡页归因到业务逻辑。
15. **封存新鲜度不可省略**：写入 Release 的最后一个构建完成后必须重新封存，并在启动前核对 `release-payload.json` 与当前文件哈希。旧清单哈希导致的启动准入失败统一归类为发布封存问题。

## 二、一轮迭代

### Step 0 读状态

```powershell
git status --short
git log --oneline -5
```

再读 `CURRENT_TASK.md` 顶部摘要，确认当前批次、暂停项和上一次验证结论。工作区有未提交的大改动时，先判断是否属于同一批次，不要覆盖。

### Step 1 选定工具

一次只推进一个工具。工具身份必须来自清单，不从目录名猜：

```powershell
$m = Get-Content docs/evidence/tool-batch-manifest-20261120.json -Raw | ConvertFrom-Json
$m | Select-Object toolId,status,pages
```

### Step 2 拉差异队列

差异来源只允许三类：清单里的 `unconfirmed`、门禁失败输出、原厂证据对照出的字段/控件缺口。把本轮要修的差异写成列表，标明证据文件和预期出口；没有证据的条目直接转入登记，不进代码。

### Step 3 实施

- 只动当前工具目录，以及共享合同**已经固定**的接口点。
- 基本参数页使用标准装配 `NativeStandardBasicPage`（输入行 → ROI 区域 → 位置修正 → Assist），工具只提供字段身份和原厂差异；不得复制 ROI/Assist 装配代码。详见 `references/standard-basic-page.md`。
- 新字段/新面板先补声明和资源，再补 Editor 接缝，最后补测试。
- 涉及单项目迁移时按固定顺序：发布合同允许共享程序集双声明 → 合并源码到 `Algorithms/Parameters/Results/Editor/Resources` → 验证同一程序集双角色加载 → 删除旧 `Runtime/`、`Wpf/`。
- 同步更新：`tools/<Tool>/SunEyeVision.Tool.<Tool>.csproj`、`SunEyeVision.slnx`、`product/ToolComponents.props`、`src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json`、清单 JSON、证据文件。

结构、项目和注册细节见 `references/single-project-layout.md`。涉及字段模型、持久化键、成对提交或编辑器接缝时，先填 `references/contract-change-checklist.md`，再动代码。

### Step 4 跑门禁

> **封存铁律**：任何会写 `Release/SunEyeVision_v1.0.0/SunEyeVision` 的构建（工具、框架、Contracts、Application 都算）之后，必须先 `installer/Seal-ReleasePayload.ps1` 重新封存，再启动 Release。否则启动准入会报 `Payload SHA mismatch` 或 `模块加载不完整`，表现为卡在启动页。

最小闭环（按顺序）：

```powershell
# 0.5 发布前置检查。发现锁、路径或 HALCON 部署问题先停在这里
powershell -NoProfile -ExecutionPolicy Bypass -File .opencode/skills/tool-production/scripts/Check-ReleasePreflight.ps1

# 0 一条命令完成构建 + 封存 + 启动（推荐，避免漏封存）
powershell -NoProfile -ExecutionPolicy Bypass -File .opencode/skills/tool-production/scripts/Invoke-BuildSealLaunch.ps1 -Project src/UI/SunEyeVision.UI.csproj

# 1 全量 Release 构建
dotnet build SunEyeVision.slnx -c Release --no-restore

# 2 统一静态与测试门禁（复用已构建载荷；不加 -SkipBuild 则内部重编）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verification/Invoke-ToolBatchReleaseVerification.ps1 -SkipBuild

# 3 Release 运行验证（12 个工具的隔离方案 + 窗口快照）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/agent-desktop/Invoke-ToolBatchAgentDesktopVerification.ps1
```

单工具循环（会写 `iteration-report.json`）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/tool-production/Invoke-ToolProductionIteration.ps1 -ToolId <ToolId> -SkipBuild
```

清单结构自检（本技能自带，先跑它过滤明显错误）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .opencode/skills/tool-production/scripts/Check-ToolManifest.ps1 -ToolId <ToolId>
```

门禁含义、失败归因和通过标准见 `references/gates.md`。

### Step 5 记录

每轮在 `Release/_validation/ToolProduction/<ToolId>-<时间戳>/` 留：构建日志、门禁日志、运行快照、差异列表、结果判定。证据结论写回 `docs/evidence/`，进度写回 `CURRENT_TASK.md`（阶段性汇总一条，不逐条流水账）。

报告用 `templates/iteration-report.md`。

### Step 6 判定

只有满足全部条件才能写 `Verified`：

- 原厂静态证据路径存在且覆盖本工具页签/字段/控件；
- 清单路径、页签顺序、专属面板挂载断言通过；
- Release 构建 0 错误；
- 探针与 Agent Desktop 该工具窗口打开/关闭通过；
- 未确认项已登记，且没有把未确认写成已实现。
- 结果页每个原厂显示对象都有本方字段或明确的 `unconfirmed` 记录。
- Release 封存时间晚于最后一个写入 Release 的程序集，且启动准入读取到同一份清单。

算法等价单独判定：没有黄金样本一律 `AlgorithmUnconfirmed`。

## 三、并行批次与单工具闭口

用户要求“一个工具闭口后再进下一个”时，闭口单位是工具，不是整个批次。多个智能体可以并行处理互不依赖的工具，但每个智能体必须独立完成本工具的证据、声明、测试、Release 和运行记录。

- 每个智能体只领取一个 `toolId`，不跨工具修改公共框架和其他工具目录。
- 公共框架变更由主线单独领取，完成后重新跑已闭口工具的回归门禁。
- 工具交付必须带清单状态、原厂证据路径、改动文件、未确认项、构建封存结果和窗口快照路径。
- 合并顺序按“最接近闭口的工具优先”，不按目录顺序批量跳过人工验收。

推荐的高效收口顺序是：先完成 PixelCount 的结果显示与动态区间闭口，再收口 FrameMean，随后收口 IntensityMeasure。ColorExtract 仍缺 RoiSelectControl、基本 ROI 组和文本显示组，不能与前面三个混在同一闭口结论中。

## 四、自动迭代协议

允许连续自动执行，但必须遵守：

1. 每轮只修一类差异（声明 / 资源 / 接缝 / 组装 / 执行 / 释放），同类改完统一验证。
2. 同一条失败连续两轮仍失败 → 停止，登记到差异队列，向用户报告需要补的证据或需要决策的合同。
3. 完成一个工具的全部可修差异后，输出：实际改动、门禁结果、剩余 `unconfirmed`、人工验收项。
4. 不自动扩大范围（不顺手改其他工具、不顺手重构公共框架、不顺手清理无关文件）。
5. 不自动提交、不自动推送、不自动修改原厂证据文件。

### 五点一 每个工具闭口后的有限经验沉淀

工具满足闭口条件后，只检查本轮是否发现了可复用于后续工具的流程或框架规则：

1. 只有同时具备真实证据、可重复复现、至少影响两个工具的经验，才写入本技能或 `references/`；单工具特例留在该工具证据文件。
2. 每个闭口最多新增一条规则，先写失败现象、证据位置、修复边界和回归命令，再决定是否推广；不得把一次性猜测升级为通用规则。
3. 技能更新后只重跑受影响的门禁，不扩大当前工具的代码范围；若没有新经验则不改技能文件。
4. 新规则必须明确停止条件，避免把经验沉淀成无限扩张的重构任务。

停止条件（满足任一即停）：只剩 `unconfirmed`；需要新原厂证据；需要改公共合同；需要用户决定口径；连续两轮同一失败。

## 五、常见坑

- `dotnet build` 与正在运行的 `Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe` 抢文件锁：先确认进程已退出。
- **静态初始化顺序**：`public static ToolSchema Schema { get; } = BuildSchema();` 必须在 `ImagePort`/`RoiPort` 等静态字段之后声明；否则首次加载工具时 `TypeInitializationException`，表现为启动准入失败。
- **结果页分区顺序由 `FieldPresentation.Order` 决定**：判据字段 10..50、显示字段从 100 起；不要靠控件插入顺序猜。
- **字段身份变更会打断已保存测试方案**：新增设置字段后，工具在 `Bind` 前补默认值；不要为了避开「缺少必需参数」把字段改成可选，清除按钮用 `FieldLayout.ShowClearButton=false`。
- **标准基本参数页先复用再写**：ROI/位置修正/Assist 先看 `NativeStandardBasicPage` 和已验收工具（PixelCount）；不要给单个工具另写一套装配。
- **自动封存不能被去重跳过**：任何写 Release 的构建结束后，最后一个工程必须强制重新封存；中间工程可以合并，最终封存不能跳过。
- Release 程序集被定向编译改写后必须重新封存载荷，否则启动校验失败。
- `runtime/` 类 `.gitignore` 规则会吞新工具源码：新目录先用 `git check-ignore -v <文件>` 验证，必要时补白名单。
- 页签顺序、空页、输入页归属由 `ParameterPresentationTree` 按工具提供的原厂序列重排；不要用伪造字段占位。
- 工具箱映射必须 `MappingVerified=true`，且 ToolId 与原厂模块名对应（如 PixelCount 用 `IMVSPixelCountModuVA`）。
- 构建通过 ≠ 行为通过；UI 烟测通过 ≠ 算法等价。
- Agent Desktop 物理点击会移动/最小化窗口：优先语义点击（ref），物理点击前先 `focus-window`，点击后检查 `list-windows` 的 bounds；窗口跑到 `-32000` 时先 `restore`。
- 测试二进制可能藏着旧 DLL：看起来全绿的专项测试要确认不是 `--no-build` 跑的旧产物；改了工具后至少完整跑一次该测试项目。
- 字段数量/端口集合变化会让声明测试失败：这是合同同步的一部分，别当成回归噪音。

## 六、参考文件

- `references/source-document.md`：用户原始文档（上位口径）
- `references/pipeline-policy.md`：状态机、三层职责、算法等价层、闭口标准的执行口径
- `references/gates.md`：每道门禁的命令、通过条件、失败归因
- `references/single-project-layout.md`：单项目目录、csproj、注册与迁移顺序
- `references/standard-basic-page.md`：标准基本参数页装配（ROI、位置修正、Assist）
- `references/algorithm-logic-alignment.md`：算法逻辑对齐（代码层）与黄金样本边界
- `references/failure-taxonomy.md`：六阶段失败分类与修复顺序
- `references/contract-change-checklist.md`：字段/持久化/编辑器/执行变更前的检查表
- `references/new-tool-fast-path.md`：从证据到可运行的最短实施顺序
- `scripts/Invoke-BuildSealLaunch.ps1`：构建 + 封存 + 启动一条命令
- `templates/tool-manifest-entry.json`：清单条目模板
- `templates/iteration-report.md`：迭代报告模板
- `scripts/Check-ToolManifest.ps1`：清单结构自检
