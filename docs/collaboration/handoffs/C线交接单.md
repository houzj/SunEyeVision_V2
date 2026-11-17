# C 线交接单 · 查找检测类 + 标定夹具类工具

- 基线提交：`d2e746d27`（本工作树 `feature/tools-inspection` 起点；任务卡记 `131e4e772`）
- 当前分支：`feature/tools-inspection`
- 工作树：`D:\MyWork\line-tools-inspect`
- 当前执行者：CodeBuddy（C 线队员）
- 本轮任务：类③第二个工具 **标记查找（原件 `IMVSMarkFindModu`，目录显示名「图形定位」）**
- 允许修改目录：`tools/SunEyeVision.MarkFind/**`、`tests/SunEyeVision.Tools.Inspection.Tests/**`；追加式 `SunEyeVision.slnx`、`product/ToolComponents.props`、本交接单
- 硬门槛：先取证 → 完整链路 → 差异 → 实施设计 → 改代码 → 按原厂链路验收（用户 2026-11-17 指令）
- 独占资源登记：本轮**未**使用动态/native 独占资源（未运行原厂程序、未跑 Ghidra、未开 REA 会话），全部为静态反编译源码与既有清单证据

---

## 第一段 · 标记查找原厂取证

### 1.1 证据清单（来源可复查）

| # | 证据项 | 来源（文件 + 位置） | 结论 | 状态 |
|---|---|---|---|---|
| E1 | 工具身份与显示名 | `docs/07-工具逆向/tools/imvsmarkfindmodu/ui-preview.json`（`displayName`）；`src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json:66-70` | 原厂工具 `IMVSMarkFindModu`，目录名「图形定位」，类别 `Location`；Toolbox 条目当前 `ToolId=null` | 已证 |
| E2 | 页签结构 | 同目录 `resources.json`（`uiTabs`） | 四页签：`Tab_Basic Params` / `Tab_FeatureModelTemplate` / `Tab_Run Params` / `ResultShow` | 已证（资源声明级） |
| E3 | 运行/判断参数（47 项） | 同目录 `parameters.json` | 47 个 `ParamItem` 名称与默认值：`MinScore=0.5`、`MaxMatchNum=1`、`MaxOverlap=80`、`AngleStart=-45`、`AngleEnd=45`、`Polarity=1`、`SortType=2`、`OutLineEnable=True`、`NumLimitHigh=99999` 等 | 已证（清单级） |
| E4 | 参数页控件顺序 | 同目录 `ui-preview.json`（`uiControls` 51 项） | 运行参数含 `MinScore`/`MaxMatchNum`/`Polarity`/`RangAngle`/`RangScale`/`Expand`/`MarkLocationType`/`RangScaleX,Y`/`MaxOverlap`/`SortType`/`MatchThresholdFlag,High`/`SpotterFlag`/`MatchExtentRate`/`TimeOut`/`OutLineEnable`；判断段按数量/角度/缩放/分数/匹配点/Box点分组 | 已证（声明级） |
| E5 | 结果显示项（9 项） | 同上 `ImageViewConfig` | `SensorFiled`、`MatchResults`、`MatchLocation`、`LineFiled`、`MatchOutlinePoints`、`FixtureInitPoint`、`FixtureRunPoint`、`ResutlText`、`BlindZone` | 已证（声明级） |
| E6 | 模板参数默认值 | `docs/07-工具逆向/decompiled/ModuleControls/IMVSMarkFindModuControl/Apps.XmlParser.ParamTab/MarkFindDialogData.cs:56-76` | `_PyramidScaleMode=1`（自动）、`_RoughPyramidScale=3`、`_FinePyramidScale=3`、`_ThresholdMode=1`（自动）、`_HighThreshold=20`、`_FeatureMatchType=1`（高精度）、`_RotationAngle=10`、`_RotationStep=0.5`、`_ProjectionInterval=3`、`_MarkType=2`（实心十字）、`_MaskType=0`（十字标记） | 已证（反编译源码） |
| E7 | 模板参数范围与显隐 | `.../IMVSMarkFindModuControl/MarkFindDialogUI.xaml:566-600` | 压缩比 `1..20`、特征尺度 `1..压缩比`、对比度阈值 `1..255`、旋转角度 `5..45`、旋转步长 `0.1..1`、投影区间 `1..10`；压缩比/特征尺度仅「手动」可见（`:584-585`，绑定 `PyramidscaleEnable`），对比度阈值仅「手动」可见（`:590`），旋转角度/步长/投影区间/Mark类型受「高级参数」控制（`:591-595`） | 已证（资源 XAML） |
| E8 | 特征尺度 ≤ 压缩比 | `.../Apps.XmlParser.ParamTab/MarkFindDialogData.cs:206-252` | 压缩比变小时自动把特征尺度压到 `floor(压缩比)` | 已证（反编译源码） |
| E9 | 定位方式影响可见性 | `.../MarkFindDialogUI.xaml.cs:1367-1380` | 选「快速特征匹配」（index 0）时把「特征尺度」整行高度置 0 | 已证（反编译源码） |
| E10 | 标记类型 ↔ Mark类型联动 | `.../MarkFindDialogUI.xaml.cs:1288-1348` | `MaskType=0`（十字）→ 可选空心/实心十字并强制默认 `2`；`MaskType=1`（矩形）→ 只可选方框并强制 `3`；切换标记类型清空旧 ROI/掩膜（`:1350-1365`） | 已证（反编译源码） |
| E11 | 模板创建与结果形状 | `.../MarkFindDialogUI.xaml.cs:788-861`、`:903-933` | 模板由 `PatCreateCSharp.CreateMarkFindPatE` 生成；输出 `centerPoint`（十字点）+ `outputLine[4]`（边点最小外接框四边）+ `PointsData`（边点）；界面把边点画绿、中心画十字 | 已证（反编译源码） |
| E12 | 模型持久化形态 | `.../MarkFindConfigPanal.xaml.cs:517-563`、`:820-897` | 模型以**字节数据随模块保存**：`ClearCandidate` + `FeatureMatchType`（字符串）+ `AddSpareMatchModel`（`ModelData`）+ `AddSpareMarkModel`（`MarkModelData`）；导出为加密 `.mfxml` | 已证（反编译源码） |
| E13 | 模型面板行为 | `.../MarkFindConfigPanal.xaml:18-136`、`.xaml.cs:413-474` | 「新建模型/修改模型/清空/导出/导入」+ 模型缩略预览（`ModelImageData` 322×229 RGB、边点描绿，`MarkFindDialogData.cs:672-776`）；新建需先取到当前图像 | 已证（XAML + 反编译源码） |
| E14 | 匹配点语义 | `.../MarkFindDialogData.cs:423-447`、`MarkFindDialogUI.xaml.cs:903-933` | `centerPoint` 为标记中心，`outputLine` 为边点外接框；无逐点置信度输出证据 | 已证（源码结构） |
| E15 | 未取到算法语义的参数 | `parameters.json` 只给名称+默认值；`ui-preview.json` 只给控件类型 | `Polarity`、`ScaleStart/End`、`ScaleX/YStart/End`、`MatchThresholdFlag/High`、`SpotterFlag`、`MatchExtentRate`、`TimeOut`、`MarkLocationType`、`ProjectionInterval`、`MarkType`、`MaskType` 的运行时语义未取证 | 缺口（本轮「默认值可用 + 非默认显式报未支持」，不猜语义） |
| E16 | 原生算法桥 | 同目录 `resources.json` 只有 5 个 XML 的 SHA256；本库无 XML 内容与 `PatCreateCSharp` 反编译 | 原生模板创建/匹配函数体、错误码、`ModuStatus` 取值域未取证 | 缺口（冻结） |
| E17 | 同族样板（本仓） | `tools/SunEyeVision.BlobLabels/Runtime/*`（上一轮 C 线交付） | 契约全家桶、结果投影、显示需求、锚定面板结构（本工具沿用） | 已证（本仓源码） |

### 1.2 原厂完整链路

1. **入口**：工具箱「图形定位」→ 参数页四页签；模型面板在基本参数页顶部（`MarkFindConfigPanal`）。
2. **模板参数**：定位方式（快速/高精度）、金字塔比例（自动/手动 → 压缩比、特征尺度）、阈值模式（自动/手动 → 对比度阈值）、高级参数（旋转角度/步长、投影区间、Mark类型）；标记类型（十字/矩形）决定 ROI 形状与可选 Mark类型。
3. **建模型**：取当前图像（或选其它图像）→ 画十字/矩形 ROI → 橡皮擦掩膜 →「生成学习模板」→ `CreateMarkFindPatE` 产出模型字节 + 边点 + 中心点 + 外接框 → 写回模块（E12）。
4. **执行**：按运行参数（分数、个数、角度范围、重叠率…）与模型匹配 → 输出中心点、外接框、边点；限位组产出判定。
5. **结果与显示**：结果页 9 个显示组（E5）；判定与模块状态/耗时一并发布。
6. **关闭/释放**：对话框 `Deinit` → `imageView.Dispose`（`.../MarkFindDialogUI.xaml.cs:1246-1257`）。

链路缺口（冻结，不猜）：`PatCreateCSharp` 内部算法、限位组精确判定函数、`ModuStatus` 取值域、`Polarity/TimeOut/Spotter/缩放` 运行时语义。

### 1.3 差异对照（原厂 vs 本方框架）

| 项 | 原厂 | 本方框架 | 处理决策 |
|---|---|---|---|
| D1 模板存储 | 模块字节数据（`ModelData`+`MarkModelData`，E12） | 设置随方案持久化；无 `ResourceKind.Model` 宿主装载通道 | 模板存为中立文本 `ModelData`（HALCON 形状模板序列化项 + 外接框，格式串 `SunEyeVision.MarkFind.Model/1`）；不兼容原件 `.mfxml` |
| D2 模板创建 | 工具内对话框：选图像 + 画 ROI + 橡皮擦 + 生成 | 编辑器只能借宿主已发布输入（`NativeRoiFrameSource.CaptureInputs`） | 面板「新建模型」用**当前发布输入图像 + 搜索区域**建模板；缺图像/缺 ROI 明确报错 |
| D3 搜索区域（ROI） | 工具内自绘十字/矩形 ROI + 掩膜 | 区域以输入端口引用承载（与 BlobLabels 一致），本地绘制待公共控件 | ROI = `input.roi`（Region 引用），缺失报 `MARKFIND_ROI_REQUIRED`；掩膜/橡皮擦未迁移 |
| D4 模板缩略预览 | 322×229 RGB 缩略图 + 绿色边点 | 无缩略控件能力 | 用文本摘要（边点数/层数/外接框尺寸）替代，记未实现 |
| D5 导出/导入 | 加密 `.mfxml`（XML + 加密容器） | 架构边界禁止兼容旧格式 | 导出/导入 `.mfindmodel`（本工具载荷纯文本） |
| D6 显示组「直线域」 | `MatchResults` 与 `LineFiled` 两组独立配置 | `ResultDisplayAppearance` 只有一个匹配框槽位 | 合并为「匹配结果」；`LineFiled` 记未单独迁移 |
| D7 算法内核 | 原厂 `PatCreateCSharp` 原生模板匹配 | 走 HALCON `HOperatorSet` 形状模板（`create/find_shape_model`） | 映射见 §1.4；性能与逐点行为差异记已知限制 |
| D8 缩放搜索 | 有 `RangScale`/`RangScaleX,Y` 与缩放限位 | HALCON 形状模板不支持缩放 | 非默认值显式报 `MARKFIND_SCALE_NOT_READY`；缩放输出端口置空并标「不可用」 |
| D9 未取证参数 | 极性/时间/斑点/匹配阈值/匹配范围/标记定位方式/投影区间/Mark类型/标记类型 | 无对应已证语义 | 默认值按开放形状模板处理，**非默认值一律显式报错**（E15） |
| D10 页签组织 | 四页签（基本/模板/运行/结果） | 参数页 + 结果显示页由声明生成 | 本次为「模板参数」「运行参数」「结果显示」三页；ROI/掩膜并入输入与模板面板 |

### 1.4 实施设计（标记查找）

**身份**：ToolId `MarkFind`；组件 `SunEyeVision.Tool.MarkFind.Runtime` / `SunEyeVision.Tool.MarkFind.Wpf`；入口 `...Runtime.MarkFindTool` / `...Wpf.MarkFindEditorFactory`；目录名「图形定位」、类别「定位」、Order 30、IconKey `graph-locate`、`NodeDisplayType=DEFAULT`。

**端口**
- 输入：`input.image`（Image，引用，兼作 workflow 图像输入）、`input.roi`（Region，引用，可空）、`input.fixture`（位置修正，常量/引用，可空）。
- 输出：`output.status`、`output.run-time`、`output.count`、`output.scores`、`output.match-points`、`output.angles`、`output.scales`（恒空并标不可用）、`output.boxes`、`output.outline-points`（x/y/match-index/count）、`output.fixture`、`markfind.rows`（结果行）。

**字段（59 项 = 47 运行/判断 + 12 模板）**：名称与默认值逐条取自 E3/E6（`MarkFindFields.cs` 表保留原厂名供绑定排序）。模板页：`ModelData`、`PyramidscaleMode`、`PyramidScale`、`FinePyramidscale`、`ThresholdMode`、`HighThreshold`、`FeatureMatchType`、`RotationAngle`、`RotationStep`、`ProjectionInterval`、`MarkType`、`MaskType`。运行页：`MinScore`、`MaxMatchNum`、`Polarity`、`AngleStart/End`、`ScaleStart/End`、`ScaleXStart/End`、`ScaleYStart/End`、`MaxOverlap`、`SortType`、`MatchThresholdFlag/High`、`SpotterFlag`、`MatchExtentRate`、`TimeOut`、`OutLineEnable`、`MarkLocationType`；判断段：`NumLimit*`、`AngleLimit*`、`ScaleXLimit*`、`ScaleYLimit*`、`ScoreLimit*`、`MatchPointX/YLimit*`、`BoxPointX/YLimit*`。

**HALCON 映射（本方适配，标注未确认）**
- 压缩比 → `create_shape_model` 的 `NumLevels`（手动 `1..20`、自动 `auto`）。
- 特征尺度 → `find_shape_model` 的 `NumLevels`（手动 `min(Fine, 模板层数)`、自动 `0`）。
- 对比度阈值 → `create_shape_model` 的 `Contrast`（手动取阈值、自动 `auto`）；`MinContrast` 固定 5（HALCON 默认）。
- 旋转角度/步长 → `AngleStart=-RotationAngle`、`AngleExtent=2×RotationAngle`、`AngleStep=RotationStep`（原件是否正负对称未取证，暂按 ±）。
- 定位方式 → `Optimization`：快速 `point_reduction_high`、高精度 `none`。
- 极性固定 `use_polarity`（取值语义未取证）。

**执行语义**：图像 → 灰度 → 有效域 ∩ 搜索区域（有位置修正先按仿射位移）→ 形状模板匹配 → 每个匹配用 `vector_angle_to_rigid(0,0,0 → row,col,angle)` 把模板轮廓搬到图像坐标 → `smallest_rectangle2` 得匹配框与中心 → 生成框区域（输出资源）→ 排序（分数升/降/不排）→ 限位判定（个数为整体、其余逐匹配）→ 结果行/几何/输出。

**校验（直接报诊断，不兜底）**：`MARKFIND_SETTINGS_INVALID`、`MARKFIND_TEMPLATE_REQUIRED/INVALID/VERSION/LOAD/EMPTY`、`MARKFIND_IMAGE_REQUIRED`、`MARKFIND_ROI_REQUIRED/EMPTY`、`MARKFIND_POLARITY_NOT_READY`、`MARKFIND_SCALE_NOT_READY`、`MARKFIND_MATCH_THRESHOLD_NOT_READY`、`MARKFIND_SPOTTER_NOT_READY`、`MARKFIND_MATCH_EXTENT_NOT_READY`、`MARKFIND_TIMEOUT_NOT_READY`、`MARKFIND_LOCATION_TYPE_NOT_READY`、`MARKFIND_PROJECTION_NOT_READY`、`MARKFIND_MARK_TYPE_NOT_READY`、`MARKFIND_MASK_TYPE_NOT_READY`、`MARKFIND_NATIVE_RESULT`、`MARKFIND_EXECUTION_FAILED`、`MARKFIND_CANCELLED`。

**显示需求（`ResultDisplayDescriptor`）**：行 `markfind.rows`；身份 `element-id`；矩形 `box`；区域来源 `region`；数值 `score`；判定 `judgment-passed`；轮廓 `outline`；中心点 `match-point`；选中框色 (255,255,0)。显示组：匹配结果/匹配位置/传感器区域/匹配轮廓点/基准点/运行点/文本/盲区（`MarkFindResultDisplay.cs`）。

**实现文件**：`Runtime/{MarkFindTool,MarkFindFields,MarkFindSettings,MarkFindTemplate,MarkFindTemplateBuilder,MarkFindCapabilityException}.cs`、`Runtime/Algorithms/MarkFindSession.cs`、`Runtime/Results/{MarkFindSchema,MarkFindResult,MarkFindResultGeometry,MarkFindResultDisplay}.cs`、`Wpf/{MarkFindEditorFactory,Views/MarkFindParameterEditor*,Views/MarkFindResultView*,Views/MarkFindTemplatePanel,Design/MarkFindDesignData}.cs`。

---

## 第二段 · 实现进展（本轮完成）

### 2.1 已完成

- Runtime：6 必需契约 + `IToolResultDisplayProvider` + `IToolResultGeometryOutput` + `IToolResultRecordOutput` + `IToolResourceFieldDeclarations`（本工具无文件路径类资源，声明为空）+ `IToolParameterBindingNames` + `IToolKnownSettingsValidation` + `IToolRuntimeInitialization`。
- 模板链路：创建（图像+ROI→HALCON 形状模板）、载荷编解码（`SunEyeVision.MarkFind.Model/1`）、装载/释放、导出/导入 `.mfindmodel`、面板摘要。
- 执行链：完整「入口→参数→执行→结果→显示→释放」，输出资源随本次调用作用域释放，含 `[Perf] tool=MarkFind` 探针。
- Wpf：参数页（模板页顶部锚定模板面板、抑制 `ModelData` 行）、结果页（共享声明式结果表）、设计时示例数据。
- 登记：`SunEyeVision.slnx`（`/Tools/MarkFindComponents/`）、`product/ToolComponents.props`（Runtime+Editor 两条）。

### 2.2 构建与测试

- `dotnet build SunEyeVision.slnx -c Release --no-incremental`：**0 错误**（33.8s）。
- `dotnet build src/UI/SunEyeVision.UI.csproj -c Release`：**0 错误**；`release-payload.json` 已含 `SunEyeVision.Tool.MarkFind.Runtime/.Wpf` 条目（测试按 Release 载荷 SHA 校验加载成功）。
- `dotnet test tests/SunEyeVision.Tools.Inspection.Tests/... -c Release --no-build`：**51/51 通过**（新增 21 条：模板往返、单标记定位与位姿/分数/行/几何、空图判定、缺 ROI/缺模板诊断、个数限位、未支持参数拒绝、设置往返、参数契约、显示契约）。
- Release 程序启动：进程可启动进入宿主初始化，未见插件准入失败日志。**但**当日日志被并行工作树实例（`SunEyeVision_V2`、`SunEyeVision_V2-param-window-datatree`）共用同一 `%LOCALAPPDATA%\SunEyeVision\Logs\suneye.log2026-11-17.log`，无法归属本轮进程，故**不据此声明 UI 运行时通过与工具箱可见**。

### 2.3 未确认 / 未实现（如实记录）

| 项 | 状态 | 说明 |
|---|---|---|
| 工具箱可见（验收 1） | **需主线改 `src/**`** | `ToolboxDisplayCatalog.json` 中 `IMVSMarkFindModu` 仍为 `"ToolId": null`，需改为 `"ToolId": "MarkFind"` 并加 `"MappingVerified": true` |
| 参数页/结果页人工验收（验收 3/4） | 未做 | 需真机拖入节点对照；本轮只完成自动契约与执行链验证 |
| 模板缩略预览 | 未实现 | 无缩略控件，用文本摘要替代（D4） |
| 工具内 ROI/掩膜绘制、橡皮擦 | 未实现 | 走区域引用输入（D3），待公共控件 |
| 「直线域」独立显示组 | 合并 | 与「匹配结果」同槽位（D6） |
| 缩放搜索/极性/时间限制/斑点定位/匹配阈值/匹配范围比例/标记定位方式/投影区间/空心十字/方框/矩形标记 | 未支持 | 非默认值显式报错（E15/D8/D9），语义取证后补齐 |
| 原厂原生算法对齐 | 未对齐 | 按「不为对齐而对齐」既定原则；性能差异记已知限制 |
| 位置修正语义 | 本方定义 | 搜索区域按 `FixtureDeclarations.ToAffine2D` 位移后匹配；原件是否同法未取证 |

### 2.4 提交

- `feat(标记查找): 新增图形定位（MarkFind）Runtime/Wpf 全链与检查类验收用例`
- commit 号见本轮汇报；未 push、未 merge main。

### 2.5 需要的 src 改动（请主线处理）

1. `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json`：`IMVSMarkFindModu` → `"ToolId": "MarkFind"`、`"MappingVerified": true`（否则工具箱看不到新工具）。
2. 公共控件需求：暂无「必须补」项；如需补齐 D4（模板缩略预览）与 D3（工具内 ROI/掩膜绘制），建议在 `Plugin.Wpf` 提供通用缩略预览与本地 ROI 宿主接缝后再迁移。

### 2.6 下一步（建议）

1. 主线补 `src/**` 工具箱映射后，启动 Release 做验收 1/2/3/4（工具箱、拖入、参数页、结果页）。
2. 若需追平原厂语义：优先补证 E15 的极性/时间/缩放语义与 E16 的原生算法，再替换本轮「非默认即报错」的限制。
3. 类③后续工具（标记检测、轮廓匹配、灰度匹配、边缘缺陷）沿用本单结构与 §1.4 的契约全家桶模板。

---

# 第三段 · 轮廓匹配（原件 `IMVSContourMatchModu`，ToolId `ContourMatch`）

- 分支：`feature/tools-inspection`；工作树：`D:\MyWork\line-tools-inspect`
- 执行者：CodeBuddy（C 线队员）
- 起点基线：`d8498ee93`（合入本轮第一版实现）；随后按主线指示 `git merge main`（取得 `aa3045bcd` 工具箱映射修复与 `工具自测闭环规范-20261117.md`）
- 允许修改：`tools/SunEyeVision.ContourMatch/**`、`tests/SunEyeVision.Tools.Inspection.Tests/**`；追加式 `SunEyeVision.slnx`、`product/ToolComponents.props`、本交接单
- 独占资源登记（agent-desktop 冒烟占用）：**2026-11-17 09:52:45 – 09:56:20**；结论见 §3.6（被并行线实例干扰，未取得有效参数页证据）

## 3.1 取证清单（S1）

| # | 证据项 | 来源（文件 + 行为/行号） | 结论 | 归属 |
|---|---|---|---|---|
| C1 | 工具身份 | `docs/07-工具逆向/decompiled/Lang/zh-cn.xaml:10015-10016`（`IMVSContourMatchModu`＝轮廓匹配，Tip＝按形状相似查找目标） | 原名「轮廓匹配」，类别定位 | 本工具 |
| C2 | 本工具是「金字塔特征轮廓匹配」子类 | `ModuleControls/IMVSContourMatchModuControl/Apps.XmlParser.ParamTab/HPPyramidFeaturepatMatchDialogData.cs:5`（`: HPFeaturepatMatchDialogData`）、`:193-196`（`CreatePatE → CreateDipolePyramid`）、`:165-168`（`FeatureMatchName="IMVSContourMatchModu"`） | 训练产物是「双极金字塔」轮廓模型，与高精度匹配同族 | 本工具 |
| C3 | 模板参数默认值 | 同文件 `:7-23`：`_IncludeImageFlag=0`、`_pyramidScaleFlag=1`、`_pyramidScaleLevel=5`、`_pyramidScaleRLevel=1`、`_edgeThresholdFlag=1f`、`_edgeThreshold=15`、`_maskModelFlag=0`；基类 `.../IMVSHPFeatureMatchModuControl/Apps.XmlParser.ParamTab/HPFeaturepatMatchDialogData.cs:26-42`：`_chainFlag=1`、`_minChainLen=4` | 9 项模板参数默认值 | 本工具 |
| C4 | 模板对话框控件/范围/选项顺序 | `IMVSContourMatchModuControl/hppyramidfeaturepatmatchdialoguistyle.xaml:879-906`：`PyramidScaleFlag` 下拉 `:887-890`（0=手动,1=自动）、`PyramidScaleLevel` 滑条 1..10 `:891`、`PyramidScaleRLevel` 滑条 1..10 `:892`、`EdgeThresholdFlag` 下拉 `:893-896`、`EdgeThreshold` 滑条 1..255 `:897`、`ChainFlag` 下拉 `:898-901`、`MinChainLen` 滑条 2..1000 `:902`、`MaskModelFlag` 下拉 `:903-906`（0=全轮廓,1=外轮廓） | 控件类型/范围/枚举顺序 | 本工具 |
| C5 | 运行/判断参数（56 项） | `docs/07-工具逆向/tools/imvscontourmatchmodu/parameters.json`（`parameters` 数组 56 项，名称+默认值） | `MinScore=0.5`、`MaxMatchNum=1`、`MaxOverlap=50`、`MultiModelMaxOverlap=50`、`AngleStart=-45`、`AngleEnd=45`、`Polarity=1`、`SortType=2`、`OutLineEnable=True`、`OutLinePointEnable=False`、`NumLimitHigh=99999`、`ScaleStart/End=1`、`SkewXStart/End=0` 等 | 本工具 |
| C6 | 参数家族关系（交叉印证） | 与 `tools/imvshpfeaturematchmodu/parameters.json` 逐项比对：轮廓 56 项中 52 项与其同名同默认值，仅多出 `MultiModelMaxOverlap`、`OutLinePointEnable`、`SkewXStart`、`SkewXEnd` | 参数属于同族共享集 | 交叉印证 |
| C7 | 枚举/取值语义 | **同族工具托管 SDK 源码，非本工具二进制**：`docs/07-工具逆向/decompiled/Managed/FastFeatureMatch/IMVSFastFeatureMatchModuCs/FastFeatureMatchParam.cs:13-23`（Polarity：0=不考虑极性/1=考虑极性）、`:28-54`（SortType：2 分数降序/3 角度降序/4 X 升序/5 Y 升序/6 X→Y/7 Y→X）、`:59-73`（MatchThresholdFlag：0 自动/1 模板/2 手动）、`:187-395`（各参数取值范围 XML 注释）、`:820-866`（$Limit$ 族上下限） | 上述三条枚举与范围 | 同族源码（交叉印证，非本工具结论） |
| C8 | 运行参数取值（仅名称+默认值） | `parameters.json`（无值域与算法语义） | 默认值可用；非默认语义未取证 | 本工具（缺口） |
| C9 | 输入端口 | `parameters.json.inputs`：`InputImage`（InImage/Width/Height/PixelFormat，强制）、`InputROI`（ROIBOX）、`Position Correction Info`（FIXTURE）、`InputMask`（IMAGE，非强制）、`InputBlindPolygonString` | 图像+ROI+位置修正为必需链；掩膜/盲区为可选 | 本工具 |
| C10 | 输出端口（22 项） | `parameters.json.outputs`：`ModuRunTime/ModuStatus/MatchStatus/MatchNum/MatchModelIndex/MatchModelName/MatchRect/Match Point/MatchScale(X,Y)/MatchScore/SkewX/ROI/MatchOutline/MatchOutlineNum/ContourPoints/ContourIndex/BlindPolygon(Str)/OutputMask/Output Position Correction Info` | 匹配类输出 + 轮廓点 + 多模板字段 | 本工具 |
| C11 | 结果显示项（9 项） | `docs/07-工具逆向/tools/imvscontourmatchmodu/ui-preview.json`（`controls` 中 9 个 `ImageViewConfig`：SensorFiled/MatchResults/MatchLocation/MatchOutlinePoints/FixtureInitPoint/FixtureRunPoint/ResutlText/BlindZone/BoundingBox） | 显示需求来源 | 本工具（声明级） |
| C12 | 参数页控件顺序 | 同 `ui-preview.json`（58 控件）：掩膜图组、ROI 组、`HPPyramidMatchPat` 模型面板，运行参数与判断组顺序 | 页面结构 | 本工具（声明级） |
| C13 | 训练链路：ROI→掩膜→建模型 | `IMVSHPFeatureMatchModuControl/IMVSHPFeatureMatchModuControl/HPFeaturepatMatchDialogUI.cs:1499-1539`（匹配按钮→`FeaturePatCreateByEraser`→`ShowPoints`）、`:1664-1744`（`CreatePatE` 在线程中执行、`ret!=0` 报错、`-3`＝模型内存申请失败）、`:1746-1782`（边点画 `#66FF00` 微点）、`:1549-1609`（ROI 类型 8 种：框/多边形/圆/扇环/十字/椭圆/线/多线） | 训练语义 | 本工具（基类，被 C2 继承） |
| C14 | 模型与掩膜持久化 | `IMVSHPFeatureMatchModuControl/.../HPFeaturepatMatchDialogUI.cs:998-1054`（`CustomSave` 导加密 `.contourmxml`，含 BasicData/CurImageData/OtherMask/PointsData）、`IMVSContourMatchModuControl/HPPyramidFeaturepatMatchDialogUI.cs:10`（过滤器 `*.contourmxml`） | 原件模板＝模块内字节 + 加密导出 | 本工具 |
| C15 | 多模板存储结构 | `FeatureMatchCommonControl/FeatureMatchCommonControl/FeaturepatMatchConfigPanalDataBase.cs:152-209`（`SpareModelName`/`AddSpareModel`/`DeleteCandidateByIndex`）、`:211-237`（`SolModelFile_{i}_BasicData/_CurImageData/_OtherMask/_PointsData/_PointsNums`） | 原件支持多模板（→C5 的多模板参数） | 本工具 |
| C16 | 模板几何形态 | `FeaturepatMatchConfigPanalDataBase.cs:100-142`（`PointsData` 为 `MVBPATMATCH_EDGEPOINT_OUT{x,y,score_flag,weight_flag}`） | 模型＝一组带方向的边点 | 本工具 |
| C17 | 原生算法体 | `resources.json` 仅有 18 个 XML 的 SHA256，无内容；`evidence.json` 指向的 `docs/plugins/IMVSCONTOURMATCHMODU-*.md` 共 10 份**在仓库中不存在** | `Process/ProcessAlg` 函数体、`ModuStatus` 取值域、掩膜/盲区语义**未确认**；上述 md 属**悬空引用**，不作为证据 | 缺口 |
| C18 | 参照实现 | `tools/SunEyeVision.MarkFind/**`（本线上一交付）、`tools/SunEyeVision.BlobLabels/**` | 沿用其契约全家桶、结果投影/显示需求、锚定面板结构 | 本仓源码 |

## 3.2 实施结果（已完成）

| 层 | 文件 | 内容 |
|---|---|---|
| Runtime | `ContourMatchSettings.cs` | 65 项冻结设置（模板 9 + 运行/判断 56）+ 组合校验（含 16 种「未支持语义」显式诊断） |
| Runtime | `ContourMatchFields.cs` | 65 个字段声明（ID 由字段名哈希派生、默认值、范围、配对、显隐、引用能力、绑定名） |
| Runtime | `ContourMatchTemplate.cs` / `ContourMatchTemplateBuilder.cs` | HALCON 形状模板载荷（`SunEyeVision.ContourMatch.Model/1`，`.cmatchmodel`）；压缩比→建模板层数、边缘阈值→建模板对比度、角度覆盖 ±180° 并在执行前校验 |
| Runtime | `Algorithms/ContourMatchSession.cs` | 图像→灰度→ROI∩有效域（含位置修正）→`FindShapeModel`→排序（6 种）→判定→结果行/几何→输出 |
| Runtime | `Results/*` | 11 个输出端口、结果行 schema、结果投影、显示需求、几何快照、输出对象（幂等 Dispose） |
| Wpf | `ContourMatchParameterEditor` / `ContourMatchTemplatePanel` | 声明式参数页 + 模型面板（新建/清空/导出/导入/摘要） |
| Wpf | `ContourMatchResultView` / `ContourMatchDisplayUi` / `Design/ContourMatchDesignData` | 结果表；显示组合（图形 6 组 + 文本 7 行 + 可见性 2 项）；设计时示例数据 |
| 登记 | `SunEyeVision.slnx`、`product/ToolComponents.props` | 各追加 2 条（Runtime/Editor） |
| 测试 | `tests/SunEyeVision.Tools.Inspection.Tests/ContourMatch*Tests.cs` | 26 例（执行链 10、参数契约 7、显示契约 8 含 1 例文本表达式、呈现层组装 2 由独立文件 4 例中的 2 例） |

### 呈现层五项契约（S2 / P1–P5）

| # | 契约 | 落点 | 证据 |
|---|---|---|---|
| P1 | `IToolSettingsView` + `IToolSettingsViewReady`（`ReadyTask => Task.CompletedTask`） | `ContourMatchParameterEditor.xaml.cs:16`、`:69` | `ContourMatchPresentationViewTests.Parameter_view_binds_the_formal_draft_without_throwing` 断言 `ReadyTask.IsCompleted` |
| P2 | 绑定前设 `InputSection` | 同上 `:50`：`options.InputSection ?? ContourMatchFields.TemplateSection`（**默认值必须是已声明分组**） | 同测试：组装成功；未设时抛 `An input content section is required.`（今日日志 09:15:46 同类） |
| P3 | 图形分组 `DialogGroup`+`DialogTitle` 同给、`visible` 设 `DialogPrimary` | `Results/ContourMatchResultDisplay.cs`（`GraphicFields()` 五个角色同时带 dialog/title，`visible` 为 primary） | `Every_graphic_group_declares_visibility_primary_and_style_roles` |
| P4 | 分组字段被 `GraphicDisplays` 完整消费 | `Wpf/Views/ContourMatchDisplayUi.cs`（6 组全覆盖） | 组装测试通过（漏项会抛「图形角色必须完整消费分组」）；分组字段数固定为 5 的断言同上 |
| P5 | `DecimalPlaces:null` 坐标用 `TextDisplays` 注册；组合式 Dispose | `ContourMatchDisplayUi.cs`（`NativeTextDisplayOptions` 含 x/y）、`ContourMatchParameterEditor.Dispose`/`ContourMatchResultView.Dispose` | 组装测试 + `Appearance_capture_rejects_out_of_range_values`、`Default_appearance_is_fully_captured` |

> 注：本工具默认输入区是「模板参数」组（第一页首个已声明分组）。原件把输入/ROI 放在基本参数页顶部，本工具第一页即模板参数页，故以此为锚点，避免落到不存在的「基本参数」组而崩。

## 3.3 与原件的差异（已确认，非缺陷）

| 项 | 原件 | 本工具 | 理由 |
|---|---|---|---|
| D1 模板文件格式 | 加密 `.contourmxml`（模块字节 + 加密导出） | 中立文本 `.cmatchmodel`（HALCON 序列化项 base64 + 外接框 + 角度覆盖） | 架构边界：不兼容旧加密格式 |
| D2 模型面板 | 多模板列表 + 缩略预览 | 单模板 + 文本摘要 | 多模板未迁移（C15），缩略控件缺基础 |
| D3 训练交互 | 工具内 ROI 8 类绘制 + 橡皮擦掩膜 + 手动基准点 | 走「图像/区域/位置修正」引用输入 | 无本地 ROI 宿主接缝（同 MarkFind D3） |
| D4 轮廓输出打包 | `MatchOutline/MatchOutlineNum` 与 `ContourPoints/ContourIndex` 两套 | 统一 `output.outline-points{x,y,match-index,count}` | 两者是同一组点，避免双轨 |
| D5 显示项 BoundingBox | 与 MatchResults 分别绘制 | 合并入「匹配结果」（唯一矩形槽位） | 框架只有一个匹配框外观槽位 |
| D6 掩膜图像/盲区 | `InputMask` 图像、`InputBlindPolygonString` 输入与 `OutputMask`/`BlindPolygon*` 输出 | 未声明 | 语义未确认（C17），不臆造端口 |
| D7 位置修正 | 输入端口 `Position Correction Info` | 搜索区域按 `FixtureDeclarations.ToAffine2D` 位移 | 与 MarkFind 同法（原件是否同法未取证） |

## 3.4 未确认语义清单（请主线决定是否补动态取证）

| 参数/端口 | 已知 | 未确认 | 当前处置 |
|---|---|---|---|
| `Polarity`=0 | 同族源码：0=不考虑极性 | 本工具是否同为该枚举；HALCON 侧 metric 在创建期固定 | 仅允许 1（默认），非默认报 `CONTOURMATCH_POLARITY_NOT_READY` |
| `ScaleStart/End`、`ScaleX/YStart/End`、`RangeScaleType` | 同族源码：尺度范围 [0.1,10] | 本工具尺度搜索算法；`RangeScaleType` 取值含义 | 仅允许 1（默认）/0，非默认报 `CONTOURMATCH_SCALE_NOT_READY`、`CONTOURMATCH_RANGE_SCALE_TYPE_NOT_READY` |
| `SkewXStart/End` | 本地化：起始/终止横切角度 | 算法语义、与角度/尺度的耦合 | 仅允许 0，非默认报 `CONTOURMATCH_SKEW_NOT_READY` |
| `MatchThresholdFlag/High` | 同族源码：0 自动/1 模板/2 手动，边缘阈值 [1,255] | 运行期如何生效 | 仅允许 0，非默认报 `CONTOURMATCH_MATCH_THRESHOLD_NOT_READY` |
| `SpotterFlag` | 同族源码：是否考虑噪点 | 具体判定 | 仅允许 False |
| `MatchExtentRate` | 同族源码：延拓阈值 [0,90]，默认 0 | 算法语义 | 仅允许 0 |
| `TimeOut` | 同族源码：超时控制 [0,10000] | 是否可中断原生匹配 | 仅允许 0（HALCON 单次调用不可中断） |
| `UseMatchAllMode`、`MultiModelMaxOverlap` | 名称与默认值 | 全部搜索/多模板的判定细节 | 仅允许默认值（False / 50） |
| `OutLinePointEnable` | 名称与默认值 False | 与 `MatchOutline` 的区别 | 仅允许 False（轮廓已由 `OutLineEnable` 输出） |
| `MaskModelFlag`=1（外轮廓） | 下拉项名称 | 轮廓划分算法 | 仅允许 0（全轮廓） |
| `ChainFlag`=0（手动链长） | 下拉项 0=手动 + 滑条 2..1000 | HALCON 无链长语义 | 仅允许 1（自动）；自动时 `MinChainLen` 按原件禁用（不参与执行） |
| `IncludeImageFlag`、`HighThreshold`、`LowThreshold` | 字段默认值（0/0/0） | 用途：未出现在对话框 XAML 与控件清单 | 不暴露为设置项 |
| `ModuStatus`、`MatchStatus` 取值域 | 端口名 | 取值定义 | 本方 `status=1/0`（Pass/Fail） |

**已知限制**：原生算法体（`Process/ProcessAlg`）未取证，当前仅支持上述「默认值路径」；所有非默认值走显式诊断，不静默按标准语义实现。

## 3.5 构建与测试（S3 / S4）

```
dotnet build SunEyeVision.slnx -c Release --no-incremental -m:1
    6065 个警告
    0 个错误
已用时间 00:00:32.03
```

```
dotnet test tests\SunEyeVision.Tools.Inspection.Tests\SunEyeVision.Tools.Inspection.Tests.csproj -c Release --no-build
已通过! - 失败: 0，通过: 79，已跳过: 0，总计: 79，持续时间: 424 ms
```

（79 例 = 本轮轮廓匹配 26 例 + 既有 BlobLabels/MarkFind 53 例）

> 构建/测试顺序注意：`dotnet test` 触发依赖构建会使工具 DLL 变化而 `release-payload.json` 仍为旧 SHA，故必须「全量构建 → `--no-build` 跑测试」，否则全部用例因 SHA 不一致报错。

## 3.6 agent-desktop 冒烟（S2 补充；未取得有效证据）

- 占用时间：**09:52:45 – 09:56:20**
- 过程：启动本工作树 Release（`D:\MyWork\line-tools-inspect\Release\...\SunEyeVision.UI.exe`，pid 29260/25688）→ 起始页快照 → 点「＋ 新建方案」→ 空白工作区（流程1，nodes=0）与工具箱渲染成功；随后实例在无本线操作的情况下被关闭，同一日志文件 `%LOCALAPPDATA%\SunEyeVision\Logs\suneye.log2026-11-17.log` 出现**另一 worktree**（`D:\MyWork\SunEyeVision_V2-param-window-datatree`）实例的启动/退出记录。
- 结论：**桌面与日志目录被多条线共用**，两次尝试均被并行线实例打断（09:53:47 我的实例日志出现「保存方案」对话框被答 No 后退出；09:54:00 另一实例退出；09:55:24 我的第二实例被最小化后消失）。未取得参数页/页签切换的有效截图与日志证据。
- 本线未产生新 ERROR：今日 3 条 ERROR（05:27 P5、09:08 P4、09:15 P2）均早于本线占用时间。
- 处置：参数页/结果页的「组装不崩」由自动化 STA 用例覆盖（真实 `DeclaredSettingsView` + 真实声明 + 真实显示组合）；**工具箱可见性 → 拖入 → 切页签 → 关闭**的人工/程序冒烟请主线在独占时段执行。
- 建议：主线为 agent-desktop 冒烟设置互斥（文件锁或指定 `AGENT_DESKTOP_HOME` 与独立日志根），否则多线结果不可信。

## 3.7 自检结果（S1–S8）

| # | 项 | 结论 | 证据 |
|---|---|---|---|
| S1 | 取证 | ✅ | §3.1 C1–C18，逐条带文件+行号；C7 明确标注「同族工具托管 SDK 源码，非本工具二进制」；`IncludeImageFlag`/`HighThreshold`/`LowThreshold`/原生算法体/掩膜/盲区标「未确认」（§3.4）；`docs/plugins/IMVSCONTOURMATCHMODU-*.md` 标为悬空引用（C17） |
| S2 | 契约 | ✅ 6 必需 + P1–P5 | 6 必需：`IToolDefinition` 全套（`ContourMatchTool.cs:11-13`）、`IToolSettings`（`ContourMatchSettings.cs`）、`IToolSession`（`ContourMatchSession.cs:12`）、`IToolResultPresentationProvider/Descriptor`、`IToolResultRecordOutput/GeometryOutput`（`ContourMatchResult.cs:84`）、`IToolResourceFieldDeclarations`；P1–P5 见 §3.2 表 |
| S3 | 构建 | ✅ 0 错误 | §3.5 末 3 行（6065 警告 / 0 错误 / 32.03s） |
| S4 | 测试 | ✅ 79/79 | §3.5（失败 0，通过 79） |
| S5 | 登记 | ✅ 仅追加 | `git diff d8498ee93^ SunEyeVision.slnx product/ToolComponents.props` 仅新增 `ContourMatchComponents` 文件夹 2 行与 `ContourMatch` 2 个 `ProductToolComponent`；未改他人条目 |
| S6 | 边界 | ✅（附说明） | `git status --short` 仅 `tools/SunEyeVision.ContourMatch/`、`tests/SunEyeVision.Tools.Inspection.Tests/{ContourMatch*Tests.cs,*.csproj,packages.lock.json}`、`SunEyeVision.slnx`、`product/ToolComponents.props`、本交接单；未改 `src/**`（`ToolboxDisplayCatalog.json` 由主线 `aa3045bcd` 修改）、未改 `.gitignore`、未 push；`git merge main` 为**主线明确指示**的一次性同步（合入到本特性分支，非合入 main） |
| S7 | 提交 | ✅ | 见 §3.8 `git log --oneline`；中文 Conventional Commits，分阶段（实现+测试 / 交接单），无 `*-msg.txt` 等临时文件入库（提交信息经 `%TEMP%` 文件传入，不入库） |
| S8 | 交接 | ✅ | 本节 §3.1–§3.8：基线/分支/执行者/已完成/未完成/构建测试/已知风险/下一步/自检结果 |

## 3.8 提交

见本轮汇报与 `git log --oneline`（两笔：`feat(工具-轮廓匹配)` 实现+测试、`docs(交接单)` 本段更新）。未 push、未合入 main。

## 3.9 下一步

1. 主线补/已补 `ToolboxDisplayCatalog.json`（`aa3045bcd` 已将 `IMVSContourMatchModu → ContourMatch` + `MappingVerified`）：需在独占时段做工具箱可见性→拖入→参数页→切页签→关闭冒烟。
2. 若要追平原件语义：优先补 `Process/ProcessAlg` 反编译（尺度搜索、极性、横切、掩膜/盲区、`ModuStatus`），再放开 §3.4 中对应限制。
3. 多模板（C15）与掩膜图像（C6）建议作为独立批次，与公共控件（模板缩略预览、本地 ROI 宿主接缝）一并设计。

---

# 第四段 · 灰度匹配（原件 `IMVSGrayMatchModuVA`，ToolId `GrayMatch`）

- 分支：`feature/tools-inspection`；工作树：`D:\MyWork\line-tools-inspect`
- 执行者：CodeBuddy（C 线队员）
- 起点基线：`a8db07988`（主线集成合并后，含轮廓匹配并入 main 与工具箱映射）
- 允许修改：`tools/SunEyeVision.GrayMatch/**`、`tests/SunEyeVision.Tools.Inspection.Tests/**`；追加式 `SunEyeVision.slnx`、`product/ToolComponents.props`、本交接单
- 独占资源：**未**使用动态取证 / 原厂程序 / Ghidra / REA；**未**使用 agent-desktop（冒烟由主线在独占时段执行）

## 4.1 取证清单（S1）

| # | 证据项 | 来源（文件 + 行号） | 结论 | 归属 |
|---|---|---|---|---|
| G1 | 工具身份 | `decompiled/Lang/zh-cn.xaml:5401-5402`（`IMVSGrayMatchModu`/`IMVSGrayMatchModuVA`＝灰度匹配）、`:8691`（Tip＝查找灰度相似的目标） | 工具箱条目 `IMVSGrayMatchModuVA`，类别定位 | 本工具 |
| G2 | 与匹配家族的关系 | `IMVSGrayMatchModuVAControl/Apps.XmlParser.ParamTab/GrayMatchVADialogData.cs:5`（`: HPFeaturepatMatchDialogData`）、`:121-124`（`CreatePatE → CreateGrayPatEVA`）、`IMVSGrayMatchModuVAControl/GrayMatchVADialogUI.cs:37-40` | 训练＝灰度（NCC 型）模板，与轮廓/高精度匹配共用训练基类 | 本工具 |
| G3 | 模板参数默认值 | `GrayMatchVADialogData.cs:7-15` | `pyramidNumLevelFlag=1`、`pyramidNumLevel=5`、`modelAngleStep=2`、`modelAngleStart=-45`、`modelAngleEnd=45` | 本工具 |
| G4 | 训练对话框控件/范围/标签 | `IMVSGrayMatchModuVAControl/graymatchvadialoguistyle.xaml:860-872` | 5 行：金字塔层数标记（下拉）、金字塔层数（滑条 **1..8**，仅手动可用）、角度步长（**1..50**）、起始/终止角度（**-180..180** 互相夹取）；组标题 `Tab_Match Params`＝配置参数 | 本工具 |
| G5 | flag ↔ 自动/手动 | `GrayMatchVADialogDataCvt.cs:15-30` | `Index`：flag 0↔index 1、flag 1↔index 0；`Enable`＝`flag==0` ⇒ **1=自动、0=手动** | 本工具 |
| G6 | 中文显示名 | `zh-cn.xaml:6113-6114`（金字塔层数／金字塔层数标记）、`:6276`（角度步长）、`:3173`（起始角度）、`:3206`（终止角度） | 字段标签直接取自原件本地化 | 本工具 |
| G7 | 运行/判断参数（VA 33 项） | `docs/07-工具逆向/tools/imvsgraymatchmoduva/parameters.json` | `MinScore=0.5`、`MaxMatchNum=1`、`MaxOverlap=**40**`、`Polarity=1`、`MatchingPolarity=1`、`AngleStart=-45`、`AngleEnd=45`、`SortType=2`、`TimeOut=0`、`EndPyramidLevel=0`、`UseMatchAllMode=False`、`OKWhenNumIsZero=False` + 7 组限位 | 本工具（默认值唯一来源） |
| G8 | 与旧版差异（防串用） | 对比 `tools/imvsgraymatchmodu/parameters.json`（**31 项**） | 旧版有 `AngleStep=8`、`MaxOverlap=80`、无 `MatchingPolarity/EndPyramidLevel/UseMatchAllMode`；**VA 无 AngleStep** ⇒ 默认值必须逐工具取自本工具清单 | 交叉印证 |
| G9 | 枚举/排序语义 | **同族工具托管 SDK 源码，非本工具二进制**：`decompiled/Managed/FastFeatureMatch/IMVSFastFeatureMatchModuCs/FastFeatureMatchParam.cs:28-54`（SortType 2..7 语义）、`:13-23`（Polarity 0=不考虑/1=考虑）、`:59-73`（MatchThresholdFlag 枚举，本工具 VA 无此参数） | SortType 六种语义、Polarity 两位语义 | 同族源码（交叉印证） |
| G10 | 输入/输出（3/13） | `imvsgraymatchmoduva/parameters.json` | 输入：图像（InImage/Width/Height/PixelFormat 强制）、ROI（ROIBOX）、位置修正（FIXTURE）；输出：`ModuRunTime/ModuStatus/MatchStatus/MatchNum/MatchModelIndex/MatchModelName/MatchRect/Match Point/MatchScore/ROI/BlindPolygon/OutputMask/输出位置修正` | 本工具 |
| G11 | 结果显示项（6 项） | `imvsgraymatchmoduva/ui-preview.json`（`controls` 37 项，含 6 个 `ImageViewConfig`） | `CheckZone`(检测区域)、`MatchResults`、`MatchLocation`、`FixtureInitPoint`、`FixtureRunPoint`、`ResutlText` | 本工具（声明级） |
| G12 | `EndPyramidLevel` 含义 | `zh-cn.xaml:9868`（`RunParam_EndPyramidLevel Feature`＝**匹配结束层**）+ 本机 HALCON 反射签名 `find_ncc_model(..., NumLevels, ...)`（`NumLevels=0` 表示全部层） | **适配映射结论**：`EndPyramidLevel → NumLevels`（非原厂算法语义已证实） | 适配 |
| G13 | 页签结构 | `imvsgraymatchmoduva/resources.json`（`status=inventory-only`，`xmlEvidence`/`uiTabs` 为空串）；`evidence.json` 的 `note` 记 Basic Params/Gray Scale Template/Run Params/ResultShow | 页签名只有文字线索，无逐控件绑定 | 已证（受限） |
| G14 | 模型文件形态 | `GrayMatchVAConfigPanalData.cs:20`（`*.gmvaxml` 过滤）、`:22-25`（`GetModelData → new GrayMatchVADialogData()`）、`:32-34`（旧根节点名） | 原件模型＝模块内字节 + `.gmvaxml` 导出 | 本工具 |
| G15 | 未闭合项 | `imvsgraymatchmoduva/evidence.json`（4 条 `unknowns`）、`algorithm-semantics.md`（9 步全 `unknown`） | 原生 `Process/ProcessAlg/ProcessOutput` 函数体、排序/阈值/失败发布、释放顺序未取证 | 缺口 |
| G16 | **悬空引用** | `docs/plugins/GRAY-MATCH-IDENTITY-SOURCE-INVENTORY.md`、`…-MODULE-FIRST-CALLER-SOURCE.md`、`…-PARAMETER-MODEL-CORE-CHAIN.md`、`docs/plugins/inventory.json` | 仓内**均不存在**，不作证据 | 悬空 |
| G17 | HALCON NCC 接口 | 本机 `libs/halcon/net/halcondotnetxl.dll` 反射：`CreateNccModel(template, numLevels, angleStart, angleExtent, angleStep, metric, out modelId)`、`FindNccModel(..., minScore, numMatches, maxOverlap, subPixel, numLevels, out row/column/angle/score)`、`GetNccModelParams`、`GetNccModelOrigin`、`GetNccModelRegion`、`Serialize/DeserializeNccModel`、`ClearNccModel` | 算法实现基础（「灰度相似」→归一化互相关 NCC，与原件训练函数 `CreateGrayPatEVA` 对应） | 本仓依赖 |
| G18 | 参照实现 | `tools/SunEyeVision.MarkFind/**`、`tools/SunEyeVision.BlobLabels/**`、`tools/SunEyeVision.ContourMatch/**` | 沿用契约全家桶、显示组合、STA 组装用例 | 本仓源码 |

## 4.2 实施结果（已完成）

| 层 | 文件 | 内容 |
|---|---|---|
| Runtime | `GrayMatchSettings.cs` | 39 项冻结设置（模板 6 + 运行/判断 33）+ 组合校验（含 4 类「未支持语义」诊断）+ 模板角度/结束层覆盖校验 |
| Runtime | `GrayMatchFields.cs` | 39 个字段声明（ID 哈希派生、默认值、范围、配对、显隐、引用能力、绑定名） |
| Runtime | `GrayMatchTemplate.cs` / `GrayMatchTemplateBuilder.cs` | NCC 模板载荷（`SunEyeVision.GrayMatch.Model/1`，`.gmmodel`）；层数/角度范围/角度步长→建模板参数；存原点+外接框+角度覆盖；执行前校验角度范围与结束层 |
| Runtime | `Algorithms/GrayMatchSession.cs` | 图像→灰度→ROI∩有效域（含位置修正）→`FindNccModel`→刚体变换求匹配框→排序（6 种）→判定→结果行/几何→输出 |
| Runtime | `Results/*` | 9 个输出端口（原件的尺度/轮廓点本就不存在，故不设空端口）、结果行 schema、投影、显示需求、几何快照、输出对象（幂等 Dispose） |
| Wpf | `GrayMatchParameterEditor` / `GrayMatchTemplatePanel` | 声明式参数页（模板参数/运行参数/结果显示）+ 模型面板（新建/清空/导出/导入/摘要） |
| Wpf | `GrayMatchResultView` / `GrayMatchDisplayUi` / `Design/GrayMatchDesignData` | 结果表；显示组合（图形 5 组 + 文本 7 行 + 文本可见性）；设计时示例数据 |
| 登记 | `SunEyeVision.slnx`、`product/ToolComponents.props` | 各追加 2 条（Runtime/Editor），纯新增 |
| 测试 | `GrayMatch{Execution,ParameterContract,DisplayContract,PresentationView}Tests.cs` + `WpfPresentationHost.cs` | 新增 30 例 |

### 呈现层五项契约（S2 / P1–P5）

| # | 契约 | 落点 | 证据 |
|---|---|---|---|
| P1 | `IToolSettingsView` + `IToolSettingsViewReady` | `GrayMatchParameterEditor.xaml.cs`（类声明 + `ReadyTask => Task.CompletedTask`） | `GrayMatchPresentationViewTests.Parameter_view_binds_the_formal_draft_without_throwing` 断言 `ReadyTask.IsCompleted` |
| P2 | 绑定前设 `InputSection`，且必须是本工具 schema 真实存在的组 | 同文件：`InputSection = options.InputSection ?? GrayMatchFields.TemplateSection` | 同测试；设计数据 `InputPage = GrayMatchFields.TemplateSection` |
| P3 | 图形分组 `DialogGroup`+`DialogTitle` 同给、`visible` 为 `DialogPrimary` | `Results/GrayMatchResultDisplay.cs`（`GraphicFields()` 五角色同时带 dialog/title） | `Every_graphic_group_declares_visibility_primary_and_style_roles` |
| P4 | 分组字段被 `GraphicDisplays` 完整消费 | `Wpf/Views/GrayMatchDisplayUi.cs`（5 组全覆盖） | 组装测试通过；分组字段数固定 5 的断言同上 |
| P5 | `DecimalPlaces:null` 坐标用 `TextDisplays` 注册；组合式 Dispose | `GrayMatchDisplayUi.cs`；`GrayMatchParameterEditor.Dispose` / `GrayMatchResultView.Dispose` | 组装测试 + `Appearance_capture_rejects_out_of_range_values`、`Default_appearance_is_fully_captured` |

**额外**：`Parameter_view_composes_every_page_when_all_pages_are_requested` 用 `EagerlyComposePages = true` 强制构建**全部页签**（模板参数/运行参数/结果显示），满足主线「STA 用例覆盖页签全构建」要求。

## 4.3 与原件的差异

| 项 | 原件 | 本工具 | 理由 |
|---|---|---|---|
| D1 模型文件 | 模块字节 + `*.gmvaxml` 加密导出 | 中立文本 `.gmmodel`（NCC 序列化项 base64 + 原点 + 外接框 + 角度覆盖） | 架构边界，不兼容旧格式 |
| D2 多模板 | `MatchModelIndex/MatchModelName` + 模板列表 | 单模板 | 多模板未迁移（沿用轮廓匹配口径） |
| D3 盲区/掩膜端口 | `BlindPolygon`、`OutputMask` 输入输出 | 未声明 | 语义未取证（G15），不臆造端口 |
| D4 显示项 | `CheckZone/MatchResults/MatchLocation/FixtureInit/Run/ResutlText` | 5 个图形组 + 文本 7 行，一一对应，无合并 | 槽位足够 |
| D5 结束层 | `EndPyramidLevel`（匹配结束层） | → HALCON `NumLevels`（0＝全部） | **适配映射结论**（G12），非原厂语义已证实 |
| D6 训练交互 | 工具内 ROI 绘制 + 橡皮擦掩膜 | 走「图像/区域/位置修正」引用输入 | 无本地 ROI 宿主接缝 |

## 4.4 未确认语义清单（当前处置：仅默认值放行 + 非默认显式报错）

| 参数/端口 | 已知 | 未确认 | 处置与错误码 |
|---|---|---|---|
| `Polarity` | 同族源码：0=不考虑极性、1=考虑极性 | 本工具是否同枚举；HALCON metric 为**创建期**属性 | 仅 1；否则 `GRAYMATCH_POLARITY_NOT_READY` |
| `MatchingPolarity` | VA 新增，默认 1 | 与 `Polarity` 的分工 | 仅 1；否则 `GRAYMATCH_MATCHING_POLARITY_NOT_READY` |
| `TimeOut` | 本地化「超时时间(单位:毫秒)」，默认 0 | 是否可中断原生匹配 | 仅 0；否则 `GRAYMATCH_TIMEOUT_NOT_READY` |
| `UseMatchAllMode` | VA 新增，默认 False | 「全部搜索模式」判定细节 | 仅 False；否则 `GRAYMATCH_MATCH_ALL_MODE_NOT_READY` |
| `EndPyramidLevel` | 本地化「匹配结束层」，默认 0 | 与原厂金字塔编号是否同向 | 支持 0..8 并校验 ≤ 模板层数（`GRAYMATCH_END_LEVEL_INVALID`）；映射见 D5 |
| `BlindPolygon`/`OutputMask` | 端口名 | 生成与消费语义 | 不声明端口 |
| 多模板输出 | `MatchModelIndex/Name` | 选择与并发语义 | 单模板，不声明 |
| `ModuStatus`/`MatchStatus` 取值域 | 端口名 | 取值定义 | 本方 `status=1/0`（Pass/Fail） |
| 旧版 `AngleStep=8` | 旧版 31 项里有 | VA 版**没有**该参数 | 不声明（防串用，见 G8） |

**已知限制**：原生 `Process/ProcessAlg` 未取证，当前仅支持默认值路径；所有非默认值走显式诊断。

## 4.5 构建与测试（S3 / S4）

```
dotnet build SunEyeVision.slnx -c Release --no-incremental -m:1
    6067 个警告
    0 个错误
已用时间 00:00:34.51
```

```
dotnet test tests\SunEyeVision.Tools.Inspection.Tests\SunEyeVision.Tools.Inspection.Tests.csproj -c Release --no-build
已通过! - 失败: 0，通过: 109，已跳过: 0，总计: 109，持续时间: 1 s
```

（109 = 本轮灰度匹配 30 + 轮廓匹配 26 + 既有 BlobLabels/MarkFind 53）

**本轮遇到的三个环境/框架坑（供其他线避坑）**
1. **增量构建会产出混合 BAML**：`dotnet build` 增量后运行呈现层用例报 `BAML TypeId 509 未知`，`--no-incremental` 重建即消失 ⇒ 跑呈现层用例前必须全量构建。
2. **多 STA 线程争用 WPF 资源**：两个测试类各起 STA 线程会报「调用线程无法访问此对象，因为另一个线程拥有该对象」⇒ 新增 `WpfPresentationHost`（进程内单一 STA + `Dispatcher.Invoke`），轮廓匹配的呈现层用例一并切到该宿主。
3. **`dotnet test` 会重建工具 DLL 导致 payload SHA 不一致**（上轮已记）：必须「全量构建 → `--no-build` 跑测试」。

## 4.6 自检结果（S1–S8）

| # | 项 | 结论 | 证据 |
|---|---|---|---|
| S1 | 取证 | ✅ | §4.1 G1–G18 逐条带文件+行号；G9 标注「同族工具托管 SDK 源码，非本工具二进制」；G12 标为「适配映射结论」；未确认项单列 §4.4；G16 悬空引用不作证据 |
| S2 | 契约 | ✅ 6 必需 + P1–P5 | `GrayMatchTool.cs`（`IToolDefinition/IToolSettingsFields/IToolCatalogProvider/IToolResultPresentationProvider/IToolResultDisplayProvider/IToolParameterBindingNames/IToolResourceFieldDeclarations/IToolKnownSettingsValidation/IToolRuntimeInitialization`）、`GrayMatchSettings : IToolSettings<>`、`GrayMatchSession : IToolSession<,>, IReusableToolSession`、`GrayMatchOutput : IToolOutput, IToolResultRecordOutput, IToolResultGeometryOutput`；P1–P5 见 §4.2 表 |
| S3 | 构建 | ✅ 0 错误 | §4.5 末 3 行 |
| S4 | 测试 | ✅ 109/109 | §4.5 |
| S5 | 登记 | ✅ 仅追加 | `git diff` 仅新增 `GrayMatchComponents` 2 行 + 2 个 `ProductToolComponent`，无 `-` 行 |
| S6 | 边界 | ✅ | `git status --short` 仅 `tools/SunEyeVision.GrayMatch/`、`tests/.../GrayMatch*Tests.cs`、`WpfPresentationHost.cs`、测试项目 csproj+lock、`SunEyeVision.slnx`、`product/ToolComponents.props`、本交接单；未改 `src/**`、未改 `.gitignore`、未 push、未 merge main（本轮开始前按主线指示做过一次 `merge main` 同步） |
| S7 | 提交 | ✅ | 中文 Conventional Commits，分两笔（实现+测试 / 交接单），无临时文件入库 |
| S8 | 交接 | ✅ | 本节 §4.1–§4.7 |

## 4.7 提交与下一步

- 提交两笔（见汇报 `git log --oneline`），未 push、未 merge main。
- 下一步建议：
  1. 主线独占时段冒烟：工具箱可见「灰度匹配」→ 拖入 → 参数页三页签（模板参数/运行参数/结果显示）→ 关闭不崩 → 日志无 ERROR。
  2. 若需追平原厂算法：优先补 `Process/ProcessAlg`（阈值/排序/失败发布）与 `BlindPolygon/OutputMask`，再放开 §4.4 的限制。
  3. 类③剩余工具（边缘缺陷、标记检测等）沿用本单结构；多模板能力建议与模板列表控件一并设计后再统一。

