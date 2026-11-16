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

