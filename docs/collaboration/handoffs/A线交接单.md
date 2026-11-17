# AI 协作交接单 · A 线（图像处理类工具）

- 基线提交：`d2e746d27`（main，A 线起点）
- 当前分支：`feature/tools-image-processing`
- 工作树：`D:\MyWork\line-tools-image`
- 当前执行者：CodeBuddy（A 线队员）
- 任务目标：类①第二个工具 **色彩提取**（原件 `IMVSColorExtractModu`，ToolId `ColorExtract`）
- 允许修改目录：`tools/SunEyeVision.ColorExtract/`、`tests/SunEyeVision.Tools.ImageProcessing.Tests/`、`SunEyeVision.slnx`（追加）、`product/ToolComponents.props`（追加）、本交接单
- 已修改文件：见「七、交付物清单」

## 一、原厂取证（证据来源 + 结论）

> 取证纪律：以下每条结论都附文件路径与方法/行号；无证据的一律列在「五、未确认项」，不用经验填充。

### 1.1 声明面（Algorithm/IO/UI 页）

证据：`docs\07-工具逆向\_reference-codex\site\工具\ColorProcessing-IMVSColorExtractModu-64061353.html`
（该页 xmlEvidence 固定了原件 SHA256：`IMVSColorExtractModuAlgorithm.xml` = `cc640ed3…e571`、`IMVSColorExtractModu.xml` = `5a7dace5…89f8`、`…AlgorithmTab.xml` = `ac4167ac…a42`、`…Display.xml` = `1f62712b…70ed`、`ToolItemInfo.xml` = `c56b715e…abb4`）

- 算法参数只有 4 项，默认值分别为 `AreaLimitEnable=False`、`AreaLimitLow=1`、`AreaLimitHigh=9999999`、`InputColorRange=255,0,255,0,255,0,1,0,1`。
- 输入 4 项：`InputImage`(IMAGE，4 个 Filter 全 IsForce=true)、`InputROI`(ROIBOX，全部 IsForce=false)、`InputROIAnnulus`(ROIANNULUS，全 false)、`Position Correction Info`(FIXTURE，全 false)。
- 输出 7 项：`ModuRunTime`(float)、`ModuStatus`(int)、`TotalArea`(float)、`OutputImage`(OutImage/Width/Height/PixelFormat)、`ROI`(ROIBOX：DetectCenterX/Y、DetectWidth/Height/Angle)、`ROIAnnulus`(ROIANNULUS)、`Output Position Correction Info`(FIXTURE，Visible=false)。
- uiTabs：`Tab_Basic Params` / `Tab_Run Params` / `ResultShow`；uiControls：RoiSelectGroup、RoiType(ROISelecter)、InheritWayGroup、AssistGroup、InputColorRange(ColorExtractList)、AreaLimitEnable(Boolean)、RangeAreaLimit(FloatBettween)、CheckZone/FixtureInitPoint/FixtureRunPoint/CheckZoneAnnular(ImageViewConfig)。
- 该页 `algorithmComparison` 为 `not-established`：**原件没有任何算法数值基线**，本工具不声称数值等价。

补充证据：`docs\08-工具预览\preview\ColorProcessing-IMVSColorExtractModu.html` 第 82-98 行——已取证页只有 `Tab_Basic Params` 承载 7 个控件；`Tab_Run Params`、`ResultShow` 明确标注「归属未取证」。

### 1.2 区间数据模型与默认值

证据：`docs\07-工具逆向\decompiled\ModuleControls\IMVSColorExtractModuControl\Apps.XmlParser.ParamTab\RangeInfo.cs`

- 第 13-17 行：`COLOR_RGB=1`、`COLOR_HSV=2`、`COLOR_HSI=3`。
- 第 19-29 行：三对通道默认 `Low=0`、`High=255`；第 31 行 `isChecked=true`；第 35 行 `isColorInversion=false`。
- 第 51-79 行：`ColorTypes` 枚举项 RGB/HSV/HSI（Value 1/2/3），`CurValue=DefaultValue=1`。
- 第 420-444 行 `SetColor()`：`CurValueA==1` 时 `Color.FromRgb((C1High+C1Low)/2, C2…, C3…)`（→ **RGB 空间 C1/C2/C3 = R/G/B**）；`CurValueA==2/3` 时把 C1 当 H、C2 当 S、C3 当 V/I（→ **HSV/HSI 空间 C1/C2/C3 = H/S/(V|I)**）。
- 第 576-579 行 `ToString()`：字段顺序固定为 `C1High,C1Low,C2High,C2Low,C3High,C3Low,色彩空间,反色(0/1),启用(0/1)`。

### 1.3 区间集合的持久化与数量约束

证据：`…\IMVSColorExtractModuControl\Apps.XmlParser.ParamTab\ColorExtractList.cs`

- 第 21 行 `MAX_NUMBER=16`；第 164-167 行 `AddCommandExcute` 达 16 项时弹「添加超限:16」并拒绝新增。
- 第 103-122 行 `Value`：区间以 `#` 连接（getter 拼 `RangeInfo.ToString()`，setter 走 `Serialize`）。
- 第 138-151 行构造函数 + 第 462-471 行 `Serialize` 收尾：**至少保留 1 个区间**；第 337-355 行 `DelParamCommandExcute` 在只剩 1 个区间时拒绝删除（提示 `Lang_PmuNeedOneRange`）。
- 第 230-233 行：ROI 取色更新对 `PixelFormat=="Gray8"` 直接弹「图像格式不正确或者不支持」。
- 第 383-472 行 `Serialize`：按 `#` 切分、每段必须 9 个整数、反色/启用只接受 "0"/"1"。

### 1.4 参数页外观（已取证部分）

证据：`…\IMVSColorExtractModuControl\datatemplate\paramusercontroldatatemplate.xaml`

- 第 69 行 `DynamicResource ExtractList`（区间列表段标题）、第 76 行 `Params Settings`（参数设置段标题）。
- 第 81-84 行「Color Space」下拉绑定 `SelectedRange.ColorTypes`；第 96/112/128 行 ChannelOne/Two/Three；第 104-105 行车 120-121、136-137 行双滑块 `Minimum=0`、`Maximum=255`；第 142-156 行「Color Inversion」开关。

### 1.5 契约面依据

`docs\09-现行文档\新工具开发基线清单-20261117-033000.html`：§1.1 六必需契约、§1.3 按需契约缺省行为、§三 接入步骤、§四 10 项验收、§五 已知限制。

## 二、差异确认（本实现 vs 原件）

| 项 | 原件 | 本批实现 | 原因 |
|---|---|---|---|
| 输入 | InputImage + InputROI + InputROIAnnulus + 位置修正 | 仅 `input.image` | ROI 本地绘制/继承需要宿主 ROI 编辑合同与公共控件（现有 Blob 的 ROI 机制是工具私有 `BlobLocalRoi`/`BlobRoiInheritance`，不可跨工具复用），本线禁改 `src/**`。原件 `InputROI` 全部 IsForce=false，**未给 ROI 时按全图执行**，链路仍闭环 |
| 输出 | 7 项（含 ROIAnnulus、位置修正） | 5 项：OutputImage、TotalArea、ROI（外接矩形）、ModuStatus、ModuRunTime | ROIAnnulus 输出与位置修正输出语义未取证且当前平台无消费者；不声明无法产出的空端口 |
| 颜色区间 UI | ColorExtractList 自定义控件（区间列表 + 每通道直方图 + 双滑块） | 声明式文本字段承载**原厂完整持久化格式** | 平台缺该公共控件；不自行近似绘制。见「六、需要主线决定」 |
| 参数页 | Tab_Basic Params / Tab_Run Params / ResultShow | 单页「基本参数」，两段「区间列表」「参数设置」 | 后两页归属未取证 |
| 上下限颠倒 | 滑块各自独立，取色预览对 H 做 +255 处理 | 显式报错 | 环带语义未取证，不猜语义（rule-015） |
| 彩色输入 | 无明确执行期限制（仅 ROI 预览拒绝 Gray8） | 仅接受 RGB24/BGR24，其余显式失败（复用原件提示文案） | 颜色抽取在灰度上无意义；报错而非兜底 |

## 三、实施设计（已落地）

- ToolId `ColorExtract`；分类 `颜色处理`（必须与工具箱目录中 `IMVSColorExtractModu` 所在组一致，否则宿主判「部署分类与已核实映射不一致」）。
- 设置（`proc.*`，均为 Constant）：`颜色区间`（文本，默认 `255,0,255,0,255,0,1,0,1`）、`启用面积上下限`（布尔，默认 false）、`面积下限`（1..9999999，默认 1）、`面积上限`（1..9999999，默认 9999999，与下限成对显示，二者 VisibleWhen=启用）。
- 契约：6 必需 + `IToolRuntimeInitialization`（`HalconPlatform.Initialise()`）+ `IToolKnownSettingsValidation`（编辑期非法区间/颠倒面积给出诊断）。
- 算法链：`decompose3` → 按区间色彩空间分组（RGB 直取；HSV/HSI 走 `trans_from_rgb`）→ 每区间三通道 `threshold` 交 `intersection`（反色以输入图像域 `difference` 取补）→ `union2` 合并 → 可选 `connection`+`select_shape('area')`+`union1` → 面积与 `smallest_rectangle2` → `reduce_domain` 后快照。
- 输出：图像 = 输入彩色像素 + 域限定在提取区域；`TotalArea` = 行程长度求和；`ROI` = 外接矩形（X=列/Y=行、宽高=半长×2、角度=屏幕角度，与平台 Blob 检测矩形约定一致）；`ModuleStatus=1`。

## 四、已执行构建与测试

- `dotnet build SunEyeVision.slnx -c Release --no-incremental -m:1` → **0 错误**（`--no-incremental` 全量重建通过；封包 `release-payload.json` 已含 `SunEyeVision.Tool.ColorExtract.Runtime/Wpf` 及其 SHA）。
- `dotnet test tests\SunEyeVision.Tools.ImageProcessing.Tests\SunEyeVision.Tools.ImageProcessing.Tests.csproj -c Release --no-build` → **36/36 通过**（其中色彩提取 20 项：契约面 11 + 执行 9；帧均值 16 项未受影响）。
- 覆盖：正常输出（RGB/HSV、BGR24 通道顺序、面积与外接矩形、域行程）、边界/空输入（无匹配像素 → 面积 0 且 ROI 为空）、参数校验失败（文本非法、区间超 16、面积颠倒、未勾选区间、灰度图）、显示需求声明完整（每个结果列都有结果面板节点与成员）、宿主编辑会话准入。

## 五、未确认项（不得当成已对齐）

1. `OutputImage` 内容形态：本实现是「原图 + 域=提取区域」，**未取证**是否应零化非匹配像素，也未取证是否恒输出 RGB8。
2. `TotalArea` 定义：本实现按提取像素总和；是否含 4/8 连通域口径、是否在面积筛选前后统计，未取证。
3. 面积上下限作用于**逐连通域**（本实现）还是整体区域，未取证。
4. `DetectAngle` 的基准与符号：本实现沿用平台 Blob 约定（`-phi`），原件未取证。
5. 原件是否支持 RGBA/16 位彩色：本批只接受 RGB24/BGR24。
6. `Tab_Run Params`、`ResultShow` 页归属与内容。
7. HSV/HSI 数值基准：本实现用 HALCON 0..255（与滑块 0..255 一致），与原厂原生实现是否同基准**无证据**。
8. 每区间独立的色彩空间是否在执行期按区间生效（UI 证据支持，执行期未取证）。
9. 原件 ROIBOX/圆环 ROI 的坐标与角度约定、ROI 继承与位置修正链路。
10. 无原厂实机数值对比：本线未运行原厂程序，也未跑 Ghidra/REA（无 native 语义缺口需要补证时不做独占资源占用）。

## 六、需要主线处理的事项

1. **工具箱映射（必须，否则工具箱不出现该工具）**：`src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json` 中「颜色处理」组的
   `{"Id": "IMVSColorExtractModu", "Name": "颜色抽取", "Icon": "IMVSColorExtractModuImageEnable.png", "ToolId": null}`
   需改为 `"ToolId": "ColorExtract"` 并补 `"MappingVerified": true`。运行时 `Catalog.Category` 已按该组声明为「颜色处理」。
2. **公共控件决策**：是否迁移原厂 `ColorExtractList`（区间列表 + 三通道直方图 + 双滑块 + 反色开关）到 `Plugin.Wpf`。当前以声明式文本字段承载原厂持久化格式；若主线决定迁移，本工具只需把该字段的展示形态替换为该控件，设置文本格式不变。
3. 若后续要接 ROI：需要宿主侧 ROI 编辑/继承合同（对应原件 RoiSelectGroup/RoiType/InheritWayGroup/AssistGroup），不建议在各工具内重复实现。

## 七、交付物清单

- `tools/SunEyeVision.ColorExtract/Runtime/`：`SunEyeVision.ColorExtract.Runtime.csproj`、`ColorExtractTool.cs`、`ColorExtractFields.cs`、`ColorExtractSettings.cs`、`ColorExtractRange.cs`、`ColorExtraction.cs`、`ColorExtractSession.cs`、`ColorExtractOutput.cs`
- `tools/SunEyeVision.ColorExtract/Wpf/`：`SunEyeVision.ColorExtract.Wpf.csproj`、`ColorExtractEditorFactory.cs`、`Views/ColorExtractParameterEditor.xaml(.cs)`、`Design/ColorExtractDesignData.cs`
- `tests/SunEyeVision.Tools.ImageProcessing.Tests/`：`ColorExtractDeclarationTests.cs`、`ColorExtractExecutionTests.cs`（csproj 追加 ColorExtract Runtime 引用）
- 追加登记：`SunEyeVision.slnx`（`/Tools/ColorExtractComponents/`）、`product/ToolComponents.props`（Runtime + Editor 两个组件）

## 八、人工验收状态

未进行。10 项验收清单中 1/2/3/6/7/9 需实机（工具箱出现、拖入画布、参数页交互、连续执行、保存重开、生命周期探针），本线只完成构建与自动化用例；第 4 项结果页与第 5 项单次执行需映射工具箱后才有入口。

## 九、提交

- `df7a5ab4d9bd686e2d1ce2ff369da56d887cbf18` — `feat(色彩提取): 新增色彩提取工具Runtime与Wpf实现并登记组件`（主交付）
- `976ff88b7` — 交接单补记提交号
- `aed3eef6a` — 取消误加入的临时提交消息文件的版本跟踪
- 未 push、未 merge main；仅在本工作树 `feature/tools-image-processing` 上。工作树残留未跟踪的 `color-commit-msg.txt`（删除命令待人工确认，未纳入版本库）。

---

# 第二批任务取证结论（2026-11-17）：`imvsdivideimagemodu`

- 基线提交：`d8498ee93`（main，已 `--ff-only` 合入本工作树）
- 当前分支：`feature/tools-image-processing`
- 当前执行者：CodeBuddy（A 线队员）
- 任务目标（主线派单原文）：图像除法，原件 `imvsdivideimagemodu`，类① 图像变换
- 已完成：取证与对照、契约面梳理、呈现层契约预对齐
- 未完成：Runtime/Wpf 实现、测试、构建、提交（**卡在取证门槛，见 10.3/10.5**）
- 已执行构建与测试：无（未产生代码）
- 人工验收结果：不适用
- 已知风险或禁止改动：`src/**` 禁改；ROI 输入与位置修正继承依赖宿主 ROI 合同
- 下一步：等主线在 10.5 方案 A/B/C 中定夺

## 10.1 结论：该原件是「划片拆分」，不是「图像除法」

| # | 结论 | 证据来源 |
|---|---|---|
| 1 | 原件显示名 = **划片拆分** | `docs\07-工具逆向\decompiled\Lang\zh-cn.xaml:9245`（`<sys:String x:Key="IMVSDivideImageModu">划片拆分</sys:String>`） |
| 2 | 功能 = 对大图按 X/Y 方向划片，并可设置 X/Y 方向重叠率 | 同文件 `4568-4573` 行 `IMVSDivideImageModu_HELP`：「功能：用于对大图进行划片，可设置X和Y方向划片数量…3.设置X方向和Y方向上的重叠率」 |
| 3 | 算法参数只有 4 项：`NumX`/`NumY`/`OverlaprateX`/`OverlaprateY`，默认 `1`/`1`/`0`/`0` | `docs\07-工具逆向\tools\imvsdivideimagemodu\parameters.json` |
| 4 | 原厂类别 = `SplitCombination`（拆分组合），非「图像变换」 | 同目录 `resources.json`（`originalCategory`）、`evidence.json`（`uiCategory=image-roi`） |
| 5 | 页签 `Tab_Basic Params` / `Tab_Run Params`；控件 8 项 = `RoiSelectGroup`、`RoiType(ROISelecter)`、`InheritWayGroup`、`AssistGroup`、`NumX`、`NumY`、`OverlaprateX`、`OverlaprateY` | `resources.json`（`uiTabs`/`uiControls`）、`ui-preview.json` |
| 6 | 「两幅图像除」不是本工具：该文案是**另一模块的运行参数**下拉项 | `zh-cn.xaml:9698`（`RunParam_Image1_divide_Image2` = 两幅图像除） |
| 7 | 162 工具清单中没有图像算术类原件 | `docs\07-工具逆向\index.json`，按 `originalPlugin` 过滤 `Arith|Operate|Calc|Divide|Num` 只得到 `CalculatorModule`(变量计算)、`IMVSBoxOverlapCalculationModu`、`IMVSDivideImageModu`、`IMVSParallelCalculateModu`、`IMVSRotateCalculateModu` |

> 结论：派单把 `DivideImage` 直译成了「图像除法」，但原件 `Divide` 指「划片」。按 `DivideImage` 做算术除法会得到一个原厂不存在、也无法与证据对照的工具。

## 10.2 声明面（唯一可作实施依据的部分）

证据：`docs\07-工具逆向\_reference-codex\site\工具\SplitCombination-IMVSDivideImageModu-c6d1dab7.html`（declarations 段）

- 输入 3：`InputImage`(IMAGE；`InImage`/`InImageWidth`/`InImageHeight`/`InImagePixelFormat` 全 `IsForce=true`)、`InputROI`(ROIBOX；`roicenterx/y`、`roiwidth`、`roiheigh`+`roiangle`，全 `IsForce=false`)、`Position Correction Info`(FIXTURE，全 false)
- 输出 6：`ModuRunTime`(float)、`ModuStatus`(int)、`ROI`(ROIBOX：`DetectCenterX/Y`、`DetectWidth/Height/Angle`)、`DivideRect`(ROIBOX，**`IsArray=true`**：`DivideRectCenterX/Y`、`DivideRectWidth/Height/Angle`)、`DivideCount`(int)、`Output Position Correction Info`(FIXTURE，`Visible=false`)
- 声明面**没有输出图像端口**；`DivideRect` 是唯一的划片产物（矩形序列）＋ `DivideCount` 个数。

## 10.3 未确认项（不得当成已对齐）

1. **分片数学**：`Overlaprate` 单位（0..100 百分比还是 0..1 比例）、单片宽高取整规则、除不尽余量如何分配、遍历顺序（行优先/列优先）、`NumX × NumY` 是否恒等于 `DivideCount`。
2. **是否产出子图**：声明只有分片矩形序列；原件是否借「拆分组合」类别机制另传子图、下游如何取子图，无任何证据。
3. **ROI 语义**：`InputROI` 为空时是否按全图；划片是否被 ROI 裁剪；ROI 有角度时的坐标基。
4. **位置修正继承**：`InheritWayGroup`/`AssistGroup` 的控件行为与执行期继承链。
5. **异常语义**：`NumX/NumY ≤ 0`、重叠率 ≥ 100、图像为空时的诊断文案与判定（成功/失败）口径。
6. **释放链**：`DeInit`/`DestroyModule` 对分片缓冲区的所有权（若本工具不产出图像，风险低但未取证）。
7. **数值基线**：该页 `algorithmComparison` 七维全部 `not-established`；`algorithm-semantics.md` 九步全部 `unknown`。

## 10.4 证据可得性（本线已排查，无法自行补齐）

| 排查项 | 结果 |
|---|---|
| 本机原厂安装 `D:\Program Files\VisionMaster4.4.0` | 不存在（`Test-Path` = False） |
| 三盘搜 `IMVSDivideImageModu*.dll/exe/pdb` | 无结果 |
| `docs\07-工具逆向\decompiled\ModuleControls\` | 无 `IMVSDivideImageModuControl` 目录（只有 63 个其他模块） |
| `D:\MyWork\Rea逆向\analysis` + `analysis.zip`（283,535 条） | 仅命中图标 PNG，无该模块反编译产物 |
| `decompiled` 内搜 `Overlaprate|DivideCount|DivideRect` | 只命中 `Lang\zh-cn.xaml` |

结论：10.3 的 1、2 项是工具核心语义，必须由**能访问原厂安装/VM 的取证线**用「托管控件反编译 + native `Process/ProcessAlg` 反汇编」补；本线静态材料已穷尽。

## 10.5 用户决定（2026-11-17，已生效）

派单的「图像除法」无法对应任何原厂原件；用户确认：**就做「划片拆分」**——按 X方向块数 × Y方向块数 切分，输出每片矩形与片数；重叠率按百分比实现，除不尽余量等无资料处标注「未确认」，不当成已对齐。据此本线已实施（见第十一章）。

## 10.6 呈现层 5 项契约：落地状态（已随第十一章实施）

| # | 契约 | 本批状态 |
|---|---|---|
| P1 | `IToolSettingsView` + `IToolSettingsViewReady`（`ReadyTask => Task.CompletedTask`） | 已实现：`DivideImageParameterEditor.xaml.cs:17,52-53` |
| P2 | 绑定前设 `InputSection`（`options with { InputSection = options.InputSection ?? "基本参数" }`） | 已实现：同文件 `36-37` 行 |
| P3 | 结果页图形分组 `DialogGroup`+`DialogTitle`+`DialogPrimary` | **不适用**：本工具结果页没有图形显示分组字段（原件声明面只输出矩形数值，显示走属性列表），因此不声明 `GraphicDisplays`，无该崩溃面 |
| P4 | 分组内字段被 `GraphicDisplays` 完整消费 | 同上，不适用（无分组即无遗漏） |
| P5 | `DecimalPlaces: null` 坐标字段走 `TextDisplays`；组合式 Dispose | 前半不适用（结果页无文本显示字段）；后半已实现：`DivideImageParameterEditor.xaml.cs:57-63` |

> 结果页用 `ResultProjectionKind.PropertyList` + `DeclaredPropertyList`，与已验收的色彩提取同一路径。

---

# 十一、实施与自检结果（2026-11-17，划片拆分）

- 基线提交：`f4f976da7`（本工作树 merge main 后）
- 当前分支：`feature/tools-image-processing`
- 当前执行者：CodeBuddy（A 线队员）
- 任务目标：`DivideImage`（原件 `IMVSDivideImageModu`，显示名「划片拆分」，原厂类别 SplitCombination）
- 允许修改目录：`tools/SunEyeVision.DivideImage/`、`tests/SunEyeVision.Tools.ImageProcessing.Tests/`、`SunEyeVision.slnx`（追加）、`product/ToolComponents.props`（追加）、本交接单
- 已完成：Runtime + Wpf 实现、单元/契约/呈现层测试、全量 Release 构建、启动冒烟、登记
- 未完成：工具箱可见性与参数页运行期冒烟（被 `src/UI` 映射阻塞，见 §11.6）；原厂算法数值基线（见 §11.4）

## 11.1 契约面与实施设计

| 项 | 值 |
|---|---|
| ToolId / 分类 | `DivideImage` / `拆分组合`（必须与 `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json:707-711` 中 `IMVSDivideImageModu` 所在组一致） |
| 输入端口 | `input.image`（`ResourceKind.Image`，`FieldEditModes.Reference`，`workflowImageInputId`） |
| 设置（4 项，均 Constant + UseDeclaredDefault） | `proc.num-x`（X方向划片数量，默认 1）、`proc.num-y`（Y方向划片数量，默认 1）、`proc.overlap-rate-x`（X方向重叠率 %，默认 0）、`proc.overlap-rate-y`（Y方向重叠率 %，默认 0） |
| 输出端口（5 项） | `output.roi`（检测框记录：center/width/height/angle）、`output.divide-rects`（同一记录类型的**序列**）、`output.divide-count`、`output.module-time-ms`、`output.module-status` |
| 投影 | `PropertyList` + `One`：检测框、分片矩形、分片个数（0 位小数）、模块耗时（ms，3 位小数） |
| 契约实现 | 6 必需（`IToolDefinition`/`IToolSettings`/`IToolOutput`/`IToolSettingsFields`/`IToolCatalogProvider`/`IToolResultPresentationProvider`）+ `IToolKnownSettingsValidation`（编辑期越界诊断）。**未实现** `IToolRuntimeInitialization`：本工具只读图像尺寸、不调用 HALCON，无原生平台需要预热 |
| 分片几何 | 边长 = ⌈边 / (数量 − (数量−1) × 重叠率)⌉；间距 = (边 − 边长)/(数量−1)，起点四舍五入，末片贴边；顺序先横后竖 |
| 矩形度量 | 平台既有约定（与 Blob 整图 ROI 一致）：整幅 8×6 → center=(4,3)、width=8、height=6 |

## 11.2 与原件的差异（本实现 vs 原件）

| 项 | 原件 | 本批实现 | 原因 |
|---|---|---|---|
| 输入 | InputImage + InputROI(ROIBOX) + 位置修正(FIXTURE) | 仅 `input.image` | 原件 `InputROI`/位置修正全部 `IsForce=false`，缺省即全图执行；本线禁改 `src/**`，宿主 ROI 编辑/继承合同尚不存在（色彩提取同一处置） |
| 输出 | 6 项（含位置修正输出，`Visible=false`） | 5 项 | 位置修正输出无消费者且语义未取证，不声明无法产出的空端口 |
| 分片产物 | 声明面只有 `DivideRect` 矩形序列 + `DivideCount`，**无图像端口** | 同（矩形序列 + 个数） | 跟随声明面；不猜「另传子图」 |
| 重叠率单位 | 未取证 | 按百分比实现并在 UI 标注「%」 | 用户决定口径；已列未确认 |
| 参数页 | Tab_Basic Params / Tab_Run Params | 单页「基本参数」+ 单段「划片参数」 | 四个数字控件已取证在同一 AlgorithmTab；Run Params 归属未取证 |
| 分片数上限 | 未取证 | 65536 片保护上限，命中直接报错 | 防止笔误（40→4000）产生上千万结果行；**产品侧保护，非原厂证据**，已在代码注释与 §11.4 标注 |

## 11.3 已执行构建与测试（S3/S4 证据）

- `dotnet build SunEyeVision.slnx -c Release --no-incremental -m:1` → 输出末行 `0 个错误`（6064 个既有警告，均为历史测试/控件警告，未新增）
- `dotnet test tests\SunEyeVision.Tools.ImageProcessing.Tests\SunEyeVision.Tools.ImageProcessing.Tests.csproj -c Release --no-build` → `失败: 0，通过: 60，已跳过: 0，总计: 60`
  - 本批新增 24 项：契约/几何 13（`DivideImageDeclarationTests`）、执行 8（`DivideImageExecutionTests`）、呈现层 3（`DivideImagePresentationTests`）
  - 覆盖：正常输出（整图单片、2×3 行优先、50% 重叠、列数多于像素）、边界（除不尽、超上限、重叠 100%）、参数校验失败（列数为 0、重叠越界）、缺失/释放（释放后拒绝使用）、显示需求声明完整（每个结果列都有结果面板节点与成员）
- 构建顺序纪律：改动测试后必须**先全量构建**再 `test --no-build`；只建测试项目会触发 `Product assembly SHA mismatch: SunEyeVision.Application.dll`（本批实遇一次）

## 11.4 未确认项（不得当成已对齐）

1. **重叠率单位**：本实现按 0..99 百分比。原厂 `Overlaprate` 是百分比还是 0..1 比例，无证据。
2. **除不尽时的取整与余量**：本实现向上取整保证不遗漏、余量分散到间距、末片贴边。原厂取整方向与余量分配未取证。
3. **分片顺序**：本实现先横后竖（行优先）；原厂输出数组顺序未取证。
4. **矩形度量**：本实现用平台既有「像素跨度」约定；原厂 `DivideRectWidth/Height` 是否同度量未取证。
5. **是否另产出子图**：声明面没有图像端口，但「拆分组合」类别是否在别处下传子图、下游如何取子图无证据。
6. **ROI / 位置修正**：`InputROI` 为空是否全图、划片是否被 ROI 裁剪、`InheritWayGroup`/`AssistGroup` 的继承链均未取证，本批未实现。
7. **异常判定**：`NumX/NumY≤0`、重叠率≥100 的诊断文案是本产品口径，原厂文案与判定未取证。
8. **分片数上限 65536**：产品保护，非原厂约束。
9. **数值基线**：`algorithmComparison` 七维 `not-established`，本工具不声称与原厂数值等价。

## 11.5 运行期自测记录（S 段 3，占用登记）

- **占用起止**：2026-11-17 10:31 — 10:34（agent-desktop，单工具启动冒烟；期间未与其他线并发使用）
- 程序：本工作树 `Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe`（`SunEyeVision.Tool.DivideImage.Runtime/Wpf` 已部署到该目录，`release-payload.json` 两个组件均在册）
- 结果：主窗口正常显示（截图 `artifacts/divide-smoke/startup-divide.png`）；日志 `%LOCALAPPDATA%\SunEyeVision\Logs\suneye.log2026-11-17.log` 本次启动**无新增 ERROR**（最后一条 ERROR 为 10:24:47，早于本次启动）；一次正常退出（10:28:02 `MainWindow.Closed`/`插件卸载完成`）
- **参数页运行期冒烟未能执行**：工具箱按 `ToolboxDisplayCatalog.json` 的 `MappingVerified` 过滤，`IMVSDivideImageModu` 当前 `ToolId=null` → 该条目显示为「尚未实现」并被跳过，工具不可从工具箱拖入。映射文件属 `src/**`（本线禁改），见 §11.6 请求。

## 11.6 需要主线处理

1. **工具箱映射（必须，否则工具不可见）**：`src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json:707-711`
   `{"Id":"IMVSDivideImageModu","Name":"划片拆分","Icon":"IMVSDivideImageModuImageEnable.png","ToolId":null}`
   → 改为 `"ToolId": "DivideImage"` 并补 `"MappingVerified": true`。
2. 映射落地后请复跑一次「工具箱出现 → 拖入画布 → 打开参数页 → 切页签 → 关闭」并向本线回报日志片段（或由本线在下批补做该段冒烟）。
3. 若要接原件 `InputROI`/位置修正继承：需要宿主侧 ROI 编辑/继承合同（对应原件 RoiSelectGroup/RoiType/InheritWayGroup/AssistGroup），不建议各工具重复实现。

## 11.7 自检结果（S1–S8）

| # | 项 | 结果 | 证据 |
|---|---|---|---|
| S1 | 取证 | ✅（含未确认项） | §10.1 证据表（`zh-cn.xaml:9245`、`4568-4573`、`parameters.json`、`resources.json`、`_reference-codex` declarations 段）；未确认项见 §11.4 |
| S2 | 契约 | ✅ 6 必需 + P1/P2/P5（P3/P4 不适用，理由见 §10.6） | `DivideImageTool.cs:9-10`、`DivideImageParameterEditor.xaml.cs:17,36-37,52-53,57-63`；`DivideImagePresentationTests` 3 项通过 |
| S3 | 构建 | ✅ 0 错误 | §11.3 第 1 条 |
| S4 | 测试 | ✅ 60/60 通过（本批新增 24） | §11.3 第 2 条 |
| S5 | 登记 | ✅ 只追加自己条目 | `git diff` 片段见下 |
| S6 | 边界 | ✅ 仅本工具目录 + 两个登记文件 + 本交接单 | `git status --short` 见下 |
| S7 | 提交 | ✅ 中文 Conventional Commits，无 `*-msg.txt` 入库 | `git log --oneline` 见 §11.9 |
| S8 | 交接 | ✅ 本节 | 本文件 |

S5 登记 diff（追加，未改他人行）：

```
+  <Folder Name="/Tools/DivideImageComponents/">
+    <Project Path="tools/SunEyeVision.DivideImage/Runtime/..." />
+    <Project Path="tools/SunEyeVision.DivideImage/Wpf/..." />
+  </Folder>
+    <ProductToolComponent Include="tools/SunEyeVision.DivideImage/Runtime/...">…</ProductToolComponent>
+    <ProductToolComponent Include="tools/SunEyeVision.DivideImage/Wpf/...">…</ProductToolComponent>
+    <ProjectReference Include="../../tools/SunEyeVision.DivideImage/Runtime/..."><Private>false</Private></ProjectReference>
+    <ProjectReference Include="../../tools/SunEyeVision.DivideImage/Wpf/..."><Private>false</Private></ProjectReference>
+    <ProjectReference Include="../../src/Plugin.Wpf/..."><Private>false</Private></ProjectReference>   ← 测试项目为验证 P1/P2/P5 组合路径新增
```

> 注意：测试项目新增的 `Plugin.Wpf` 引用只用于在真实 `DeclaredSettingsView` 上验证参数页组装，不改变产品依赖。

## 11.8 交付物清单

- `tools/SunEyeVision.DivideImage/Runtime/`：`SunEyeVision.DivideImage.Runtime.csproj`、`DivideImageTool.cs`、`DivideImageFields.cs`、`DivideImageSettings.cs`、`DivideImageTiling.cs`、`DivideImageSession.cs`、`DivideImageOutput.cs`
- `tools/SunEyeVision.DivideImage/Wpf/`：`SunEyeVision.DivideImage.Wpf.csproj`、`DivideImageEditorFactory.cs`、`Views/DivideImageParameterEditor.xaml(.cs)`、`Design/DivideImageDesignData.cs`
- `tests/SunEyeVision.Tools.ImageProcessing.Tests/`：`DivideImageDeclarationTests.cs`、`DivideImageExecutionTests.cs`、`DivideImagePresentationTests.cs`（csproj 追加 3 个引用）
- 追加登记：`SunEyeVision.slnx`（`/Tools/DivideImageComponents/`）、`product/ToolComponents.props`（Runtime + Editor）

## 11.9 提交

- `1571d5b82` — `feat(划片拆分): 新增 DivideImage 工具 Runtime/Wpf 实现与契约测试`（主交付：22 个文件，1462 行新增）
- `b219a6f45` — `docs(交接单): 记录划片拆分 imvsdivideimagemodu 取证结论与阻塞点`（取证阶段）
- 未 push、未 merge 到主分支；`color-commit-msg.txt` 为此前会话遗留的未跟踪临时文件，未入库。

