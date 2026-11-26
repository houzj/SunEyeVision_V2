# AI 协作交接单

- 基线提交：`1716fe79f`（Release 包 `2.0.0.8+1716fe79f`）；本交接的全部改动**尚未提交**，工作树基于该提交。
- 当前分支：`main`
- 当前执行者：CodeBuddy（本轮）→ 交给 Codex
- 任务目标：
  1. 修复「结果层检测区域（`ResultGeometryRole.Inspection`）不显示轮廓」；
  2. 处理「程序启动不了」（双击无窗口 / 进程 exit=2）——已修复并验证；
  3. 已停做的方向：结果层 ROI 可拖动编辑（用户已明确否定，原厂关窗隐藏参数 ROI）。
- 允许修改目录：`src/`、`tools/`、`tests/`、`docs/`；禁止改动 `Release/` 目录产物。

## 已修改文件（未提交）

### 结果层 ROI 显示通道（第一批，已实现并通过测试）
- `src/Plugin.Contracts/Results/Presentation/ResultRoiSnapshot.cs`（新增）：`IToolResultRoiOutput` / `ResultRoiSnapshot` / `ResultRoiItem`，`ResultRoiSnapshot.From(ROI 文档, frame)`。
- `src/Plugin.Wpf/Presenters/Display/NativeResultViewport.RoiEditing.cs`：`PresentResultRoi` / `RetireResultRoi` / `ApplyResultRoi`，与交互层 `_roiAttachment` 仲裁。
- `src/Plugin.Wpf/Presenters/Display/NativeResultViewport.cs`：`ResultImageSize`；帧替换时 `RetireResultRoi`；视口清空时退休。
- `src/Plugin.Wpf/Adapters/Regions/NativeRoiProjection.cs` + `NativeInheritedAnnulusProjection.cs`：`CreateResultRoi` 只读投影（`_isAvoid` 支持）。
- `src/UI/Services/Images/ResultWorkspacePresenter.Display.cs`：`PresentResultRoi` / `EffectiveRoi`（本地项取当前配置文档，继承项取执行快照）。
- `src/UI/Shell/Windows/MainWindowResultDisplay.cs`：`ResultDisplayState.Roi` / `RoiDocument`。
- `src/UI/App.xaml.cs`：引导页隐藏时显式显示主窗口（启动失败修复）。
- `tools/SunEyeVision.ContourMatch/Results/ContourMatchResult.cs`、`tools/SunEyeVision.Tool.BlobAnalysis/Results/BlobResult.cs`、`tools/SunEyeVision.BlobLabels/Results/BlobLabelsResult.cs`：实现 `IToolResultRoiOutput` + `LocalRoiDocumentFieldId`。

### BlobLabels 本地 ROI 执行链（已实现并通过测试）
- `tools/SunEyeVision.BlobLabels/Algorithms/BlobLabelsSession.cs`：`ReadLocalRoi`（Local 模式、16/8 容量校验）+ 栅格化 inspection/mask 参与搜索域。
- `src/SunEyeVision.HalAdapter/Runtime/RoiDocumentRasterizer.cs`（新增）：由 Blob 工具内 `BlobLocalRoiRasterizer` 上移，Blob/BlobLabels 共用。
- `tools/SunEyeVision.Tool.BlobAnalysis/Algorithms/BlobAlgorithm.cs`：删除私有栅格化类，改用平台实现。

### ROI 编辑声明（第一批基础，未接线）
- `src/Plugin.Contracts/Results/Presentation/RoiEditDeclaration.cs`（新增）：`RoiEditDeclaration` / `RoiEditGroupDeclaration` / `RoiMaskInheritanceDeclaration`。
- `src/Plugin.Contracts/RuntimeEntry.cs`：`IToolDefinition.RoiEdit` 默认 null。
- Blob / ContourMatch / BlobLabels 三个工具返回各自声明。
- `src/Plugin.Wpf/Adapters/Regions/RoiDocumentStore.cs`：`From(declaration, inputs)` 公共工厂。
- `src/Plugin.Runtime/RuntimeDirectorySnapshot.cs`：`TryCreateDefinition(toolId)` 宿主侧入口。

### 诊断
- `ResultWorkspacePresenter.Display.cs`：结果几何存在但没有可绘制组时，打印一次性日志
  `结果几何未显示：组=Role/Coverage/Shapes；检测区域样式=visible/width/fill`。

### 文档
- `docs/collaboration/evidence/VM结果层ROI与交互层ROI分层-20261125.json`（新增，已含 `SetRoiVisible` 全分支更正）。
- `docs/collaboration/CURRENT_TASK.md`：顶部新增两条记录。

## 已完成

- 全量 Release 构建：**0 错误**。
- `ContourMatchExecutionTests` + `BlobLabels*`：**87/87**。
- `BlobAnalysis`：**152/152**。
- `ResultRoiLayerTests` + `ResultImagePaneTests`：**34/34**。
- BlobLabels 新增用例：`Local_roi_document_limits_the_search_domain`（本地 ROI 参与搜索域）。
- ContourMatch 新增用例：结果 ROI 快照携带运行时本地文档；Upstream 模式无名义 ROI。

## 未完成（按优先级）

### 1. 启动不了（已修复并验证）
根因：`StartPreferences.HideStartPage=true` 时，`HandleStartupDecisionAsync` 跳过 `ShowStartPageAsync`，而主窗口只在引导页选择后才 `Show()`（`App.xaml.cs:744`）。结果是进程正常启动到 `HandleStartupDecision returned`、持有单实例锁，但**没有任何可见窗口**；后续启动走到单实例分支，`HandleInstanceActivationAsync` 因 `visible==null` 返回 `InstanceActivationResult(false, "SunEyeVision 正在启动，请稍后重试。")`，新进程 `Shutdown(2)`，表现为双击无反应。
修复：`src/UI/App.xaml.cs` 在 `ApplyToolboxPreset` 后增加 `else if (!mainWindow.IsVisible) ShowMainWindow(mainWindow, "start page hidden or solution request")`；引导页隐藏或方案请求未显示工作区时显式显示主窗口。
验证：
- 结束旧无窗口实例后重新构建 Release，**0 错误**，Release DLL 已更新；
- 启动新进程：`MainWindowHandle != 0`，枚举到可见窗口 `SunEyeVision · 起始页`、`太阳眼视觉 - 机器视觉平台`；
- 再启动第二个实例：`exit=0`（转发激活成功），原实例保持运行。
注：`ApplicationLifetimeGate.TryRegister()` 不是根因，保持原样。

### 2. 结果层检测区域不显示轮廓（用户原始问题，未定位）
- 用户已澄清：**交互层参数 ROI**（`ROIItemsPage` 绑 `ROIList`，受 `ROIVisible` 控制）与**结果图形**（`ShapeItemsPage` 绑 `ShapeList`）是两回事；用户看不到的是**结果图形里的检测区域**；
- 原厂证据：`ModuleResultView.xaml.cs:2250-2273` `SetRoiVisible`，`IsOpenParams=false` 时 `ROIVisible=false`——关窗隐藏的是参数 ROI，不影响结果图形；
- 检测区域显示条件（`ResultWorkspacePresenter.Display.cs`）：
  `Coverage == Explicit` 且 `Shapes.Count != 0` 且 `RoiStyle(appearance, Inspection)?.Visible == true`；
- 已加诊断日志（见上），请让用户复现一次并提供该行日志：
  - `visible=False` → 显示开关被关（历史方案），勾选即可；
  - `组=Inspection/None/0` 或 `Unavailable` → 工具侧几何没产出，查 `ContourMatchResultGeometry.Raster`（128 轮廓 / 40 万顶点预算）或 `BlobResultGeometry.Capture/Nominal`；
  - 无日志 → 几何组未进入绘制链，查快照 `Output as IToolResultGeometryOutput` 与 `state.Geometry`。
- 不要再用「结果层 ROI 可编辑」方向修这个问题，用户已明确否定。

### 3. 结果层 ROI 只读通道（已按原厂移除）
- 用户结论：原厂关窗后 `ROIVisible=false`，ROI 隐藏不可编辑，不存在「关窗后仍可拖动/增删/锁定解锁/立即生效」；
- 已删除：`ResultDisplayState.Roi/RoiDocument`、`PresentResultRoi`/`EffectiveRoi`、视口 `PresentResultRoi`/`RetireResultRoi`/`ApplyResultRoi` 及其字段、`ResultRoiLayerTests.cs`；
- 原厂显隐已落地：结果视口构造即 `ROIVisible=false`，`BeginRoiEditing` 成功后置 `true`（`Initialize` 读取它计算命中测试），`RetireRoi` 清 `ROIList` 并恢复 `ROIVisible=false` + `ROIHitTestVisible=false`；参数 ROI 数据仍留在参数节点，不销毁；
- 结果图形（`ShapeList`：匹配框/检测区域）完全未动，与 `ROIVisible` 无关；
- 仅保留数据层 `ResultRoiSnapshot` 契约/工具输出与 `RoiEditDeclaration` 声明/工厂，当前无 UI 消费者，是否清理由用户决定。

## 已执行构建与测试

```
dotnet build SunEyeVision.slnx -c Release --no-restore        → 0 错误
dotnet test tests/SunEyeVision.Tools.Inspection.Tests -c Release --filter "ContourMatch|BlobLabels" → 87/87
dotnet test tests/SunEyeVision.Tool.BlobAnalysis.Tests -c Release → 152/152
dotnet test tests/SunEyeVision.UI.Tests -c Release --filter "ResultRoiLayerTests|ResultImagePaneTests" → 34/34
```
另：`ResultPresentationPresenterTests` 有 3 项既存失败（租约断言，未触碰该模块）。

## 人工验收结果

未做。Release 包（`2.0.0.8+1716fe79f`）未用本批源码重建，不能用它验收本批改动；本轮所有验证均为工程构建与自动化测试。

## 已知风险 / 禁止改动

- 不要把 `ROIVisible`（交互层参数 ROI）与 `ShapeList`（结果图形）混用；改任一侧都要先对照 `ModuleResultView.xaml:126/142/153`。
- 不要在结果层开放「可拖动但写不回」的假编辑。
- 不要用 `Release/` 旧包冒充本批验证产物。
- 本轮改动全部未提交，Codex 接手前先确认工作树状态，必要时先做临时提交固定基线。

## 下一步

1. 启动已修复并重建 Release，可直接人工验收；不要再排查 `ApplicationLifetimeGate`。
2. 让用户复现「检测区域不显示」并给出诊断日志，按日志定位是显示开关还是几何产出。
3. 结果层只读 ROI 通道已移除，行为按原厂；剩 `ResultRoiSnapshot`/`RoiEditDeclaration` 数据层无 UI 消费者，是否清理由用户决定。

## Codex 追加交接（2026-11-25）

### 本轮已完成

- `App.OnStartup` 已加入固定启动标记，覆盖 `ResetStartupTrace` 后、参数解析前后、单实例注册前后以及单实例转发前后。可据 `%LOCALAPPDATA%\SunEyeVision\Logs\startup-trace.txt` 判断 exit=2 的实际路径。
- `ResultRoiItem` 增加明确的 `ResultRoiSource` 来源标识。结果层合并逻辑按 `IsInherited` 合并继承项，不再用 `Enabled=false` 推断来源。
- ContourMatch 的 `RoiEditDeclaration` 已补齐形状继承声明，参数编辑器改用 `RoiDocumentStore.From`。
- BlobLabels 的 `RoiEditDeclaration` 已补齐，参数继承模式按原工具单值字段物化为结果 ROI 文档。
- 新增 ContourMatch 按区域、按参数和 BlobLabels 矩形参数、圆环参数的继承结果 ROI 自动测试。
- 清理残留 `SunEyeVision.UI` 进程后，新增定向测试 **3/3 通过**。上一轮既有 ContourMatch/BlobLabels 定向测试 **35/35 通过**。

### 可以进入人工验收的范围

工程自动验证已具备人工验收候选的功能链包括：

1. ContourMatch 本地 ROI、矩形按区域、矩形按参数的执行与结果 ROI 只读显示。
2. BlobLabels 本地 ROI、矩形参数、圆环参数的执行与结果 ROI 只读显示。
3. 结果 ROI 与交互编辑 ROI 的生命周期切换，包括执行后显示、帧替换退休和编辑会话占用期间让位。
4. 启动固定标记写入链，需用 Release 程序实际启动一次确认日志标记顺序和 exit=2 分支。

### 尚未达到人工验收闭口的范围

- `ShapeList` 检测区域实际显示问题尚未根据真实诊断日志定位，当前只能确认结果几何自动链存在并有一次性缺失日志。
- BlobLabels 按区域输入来自外部 Region 时没有可逆的名义矩形/圆环形状，结果 ROI 仍应以结果几何为准，不能把 Region 猜成参数 ROI。
- 尚未用本批源码生成 Release 包并进行宿主人工启动和画布验收。
- `RoiDocumentStore` 的 BlobLabels 外部 Region 继承预览仍需按原厂输入类型补一条专用适配链。

### 交给 CodeBuddy 的下一步

1. 使用本工作树源码构建 Release，启动一次并读取 `startup-trace.txt`，记录 exit=2 的固定标记位置。
2. 复现 ShapeList 不显示，回收包含 `结果几何未显示` 的完整日志行，按 `Inspection/Coverage/Shapes` 和样式开关定位。
3. 以 ContourMatch 和 BlobLabels 各一套方案执行人工验收，记录本地 ROI、继承 ROI、结果几何和帧替换显示。
4. 不提交当前批次改动，保留工作树和本交接记录作为继续工作的基线。

### 最新现场证据

- 当前机器最新 `startup-trace.txt` 已记录 `OnStartup.Enter`、插件加载、服务初始化和主窗口工作区初始化，说明该次 Release 启动已越过原先的 exit=2 区间并进入主窗口。
- 该日志的 `RuntimeIdentity` 指向 `E:\SunEyeVision_V2\Release\...` 的旧 Release 包，不能作为本工作树改动的 Release 验收证据。
- 当前源码中 `ShapeList` 结果几何仍由 `ResultWorkspacePresenter.Display.cs` 的 `state.Geometry -> roiGroups -> DisplaySceneLayer` 链路提供。只有 `Explicit` 且存在形状的组才进入绘制，`FullDomain`、`None`、`Unavailable` 不会产生 ShapeList 图形。下一步应以用户复现日志中的组状态决定是工具几何产出问题还是显示开关问题。
