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

## 九、临时提交

`feat(色彩提取): ...`，见提交记录（未 push，未 merge main）。
