# B 线交接单 · 图像统计类 + 逻辑数据类工具

- 基线提交：`461be5320`（本工作树 `feature/tools-logic-data` 起点；任务卡记 `131e4e772`）
- 当前分支：`feature/tools-logic-data`
- 工作树：`D:\MyWork\line-tools-logic`
- 当前执行者：CodeBuddy（B 线队员）
- 任务目标：先 If（条件分支，分支契约首个真实消费者），再像素计数等类②/类④工具
- 允许修改目录：`tools/SunEyeVision.If/**`、`tools/SunEyeVision.PixelCount/**`、`tests/SunEyeVision.Tools.LogicData.Tests/**`；追加式 `SunEyeVision.slnx`、`product/ToolComponents.props`
- 硬门槛：先取证 → 完整链路 → 差异 → 实施设计 → 改代码 → 按原厂链路验收（用户 2026-11-17 指令）

---

## 第一段 · If（条件分支）原厂取证

### 1.1 证据清单（来源可复查）

| # | 证据项 | 来源（文件 + 位置） | 结论 | 状态 |
|---|---|---|---|---|
| E1 | 工具身份与类别 | `docs/07-工具逆向/tools/ifbranchmodule/ifbranchmodule-reverse.md:3-9` | 原厂工具 `IfBranchModule`，类别 `Logic`，UI 组装类别 compound-control，原厂目录 `...\Logic\IfBranchModule` | 已证（清单登记） |
| E2 | 目录显示身份 | `docs/07-工具逆向/_reference-codex/site/工具/Logic-IfBranchModule-caf95b67.html`（`displayName` / `displayType` 字段） | `displayName=条件分支`、`displayType=IFBRANCH`、`categoryLabel=逻辑工具`；二进制 SHA 记录 `IfBranchModule.dll`=4c01db8b…、`IfBranchModuleCs.dll`=c1a2b07f…、`IfBranchModuleControl.dll`=5c7446d7… | 二手（Codex 记录，本库未复算哈希） |
| E3 | 端口清单（静态声明） | `docs/07-工具逆向/tools/ifbranchmodule/parameters.html:22-58` | `inputs: []`（**原厂无数据输入端口**）、`parameters: []`、`outputs`= `ModuRunTime`(float)、`ModuStatus`(int)、`BranchMatch`(string, StructName=BranchMatch, CanSubscribe=False) | 已证（资源声明级） |
| E4 | 条件行的数据模型 | `docs/07-工具逆向/decompiled/ModuleControls/IfBranchModuleControl/Apps.XmlParser.ParamTab/IfBranchDynamicIO.cs:28-81`（`Floatbetween`） | 每行一个 `FloatBettween` 区间；名含 `int` → 上下限 ±999999999，含 `float` → ±100000；起止默认 `1` / `1000` | 已证（反编译源码） |
| E5 | 每行判定状态 | 同上 `:83-114`（`State`） | `Enumeration` 枚举值 `OK=1`、`NG=2`，`CurValue`/`DefaultValue` 均为 1，`AccessMode="O"`（输出语义，非输入） | 已证（反编译源码） |
| E6 | 每行分支目标 | 同上 `:116-131`（`RunModuleId`）+ `:194-230`（`SetParamUnit`） | 每行带一个 `RunModuleId`（目标模块 ID，默认 -1）；整体序列化为 `名称:起点:终点:状态:目标模块ID;` 文本，写入模块参数 `IfBranchText`（UTF8 零填充 `StringUtil.UTF8GetBytesPadZero`） | 已证（反编译源码） |
| E7 | 动态行与类型选择 | `.../IfBranchModuleControl/Apps.XmlParser.ParamTab/IfBranchModuleDynamicIO.cs:66-125`、`:138-193` | 行可增删；`SelectTypeIndex` 0/1 决定新增行的类型 `int`/`float`；`GetParam` 从 `IfBranchText` 还原行（`array4.Length>4` 且首段等于行名，空目标模块 → -1） | 已证（反编译源码） |
| E8 | 参数页行布局 | `.../IfBranchModuleControl/datatemplate/paramusercontroldatatemplate.xaml:8-25`、`:36-44` | 行 = [名称 70] [变量选择器 `ButtonSelecter` 132] [区间 `FloatBettween` 300] [状态 `Enumeration`(OK/NG)] [目标模块下拉 80] [删除]；底部 = 类型下拉(int/float) + 添加按钮 | 已证（资源 XAML） |
| E9 | 运行语义（官方帮助文本） | `src/Plugin.Wpf/Themes/Native/Localization/zh-cn.xaml:9463-9473` | 「功能：根据输入条件是否在有效值区间的结果来控制分支流程的执行」；同组词条 `判断`、`执行模块ID` | 已证（本地化资源） |
| E10 | 判定函数体、端点包含性、多命中、`ModuStatus`/`BranchMatch` 取值 | `docs/07-工具逆向/tools/ifbranchmodule/ifbranchmodule-reverse.md:41-46`、`algorithm-semantics.md:38-46` 明确记录三份 `IfBranchModuleTool/IfBranchParam/IfBranchResult` 反编译源码**不在本库** | native 判定体未取得；端点是否含边界、是否短路、多命中行为、`BranchMatch` 字符串格式均**未确认** | 缺口（按任务卡不得猜语义，实施设计标注为「本方定义」） |
| E11 | 对照件：`IfModule`（条件检测）非分支 | `.../IfModuleControl/Apps.XmlParser.ParamTab/IfDynamicIO.cs:304-342`（`SetParamUnit` 写 `IFText`=`名称:起:止:备注;`）；`IfModuleDynamicIO.cs:125-141`（每行额外输出 `<名称>Out`(int)、`<名称>Min`、`<名称>Max`） | `IfModule` 是「结果视图/逐行输出」工具，`Describe` 无分支输出，**不能**作为分支契约消费者 | 已证（反编译源码） |

### 1.2 原厂完整链路

1. 参数页：类型下拉（int/float）→ 添加行 → 每行选前序变量（`ButtonSelecter`）+ 填区间 + 选目标模块；行内 `State` 为 OK/NG 显示位。
2. 保存：`SetParamUnit` 把全部可见行拼成 `名称:起:止:状态:目标模块ID;` 写入模块参数 `IfBranchText`；`GetParam` 反向还原（不完整字段直接跳过）。
3. 运行：按行取该行绑定的变量值 → 判断是否落在 `[起,止]` 区间 → 行 `State` 置 OK/NG → 状态为 OK 的行按其 `RunModuleId` 触发目标模块 → 模块输出 `ModuStatus` / `ModuRunTime` / `BranchMatch`。
4. 未命中行：原厂无专门错误码证据（E10），只能确认「不触发目标模块」。

链路缺口（冻结，不猜）：步骤 3 的判定函数体、端点是否包含、多命中是否并行、`BranchMatch` 文本格式、空行/未配置目标模块的运行时行为。

### 1.3 差异对照（原厂 vs 本方框架）

| 项 | 原厂 | 本方框架契约 | 处理决策 |
|---|---|---|---|
| 分支目标 | 行内 `RunModuleId`（节点身份） | `ToolOutcome.SelectedBranches` 返回序号，宿主映射出边（`WorkflowExecutionEngine` 分支段 `:65-96`、`:208-219`） | 工具**只报序号**，不接触节点身份与图结构 |
| 条件数量 | 可任意增删（动态行） | `Describe()` 是工具类型级静态声明，端口/控制输出不可按实例变化 | 固定 4 个条件槽（0..3），差异写入 1.4 决策 D3 |
| 取值来源 | 行内变量选择器（前序变量引用） | 字段 `FieldEditModes.Reference`（前序节点输出 / 全局变量引用），与 `E3 inputs:[]` 一致 | 用引用字段承接，不新造输入端口 |
| 数值类型 | 每行可选 int / float，限值不同 | `ScalarKind.Float64`（无 storage）可承接 Int64 源（`NumericStorageRules.IsWidening` Int64→Float64） | 统一 Float64，区间限值取原厂 int 档 ±999999999 |
| 行内 OK/NG 实时显示 | 参数页有「判断」列 | 参数页无运行态回显通道（宿主能力） | 不做；作为宿主缺口上报 |
| 越界分支序号 | 无此概念（目标为模块 ID） | 引擎判失败（`:212-218`） | 工具自身不产生越界序号；越界路径用契约测试取证 |
| 未命中目标 | 不触发目标模块 | 目标节点标 `Skipped`，整轮仍成功 | 空 `SelectedBranches` 即「无分支命中」 |
| 每行输出 `<名称>Out/Min/Max` | `IfModule` 才有 | 本工具不是 `IfModule` | 不实现（避免与 E11 混淆） |

### 1.4 实施设计（If 工具）

**身份**：ToolId `If`；组件 `SunEyeVision.Tool.If.Runtime`（Runtime）/ `SunEyeVision.Tool.If.Wpf`（Editor）；入口 `SunEyeVision.Tool.If.Runtime.IfTool` / `...Wpf.IfEditorFactory`；目录项 显示名「条件分支」、类别「逻辑工具」、IconKey `branch`（宿主 `tool-branch.png` 已存在）、`NodeDisplayType=IFBRANCH`。

**端口（E3：原厂无输入端口）**
- 输入：无。
- 输出：`output.module-status`(Int32) / `output.module-time-ms`(Float32) / `output.branch-match`(Text)。
- 控制输出（分支）：4 个，声明顺序即分支序号 → `branch.0`「条件1」、`branch.1`「条件2」、`branch.2`「条件3」、`branch.3`「条件4」。

**字段（每槽 i = 0..3，页面「基本参数」，段「条件 i+1」）**

| 字段 id | 类型 | 编辑方式 | 默认 | 说明 |
|---|---|---|---|---|
| `condition.{i}.enabled` | Boolean | Constant | `false` | 该条件是否参与本轮判定；默认全关以保证默认设置通过准入校验 |
| `condition.{i}.source` | Number(Float64) | Reference | 缺省（未绑定） | 取值来源（前序节点输出/全局变量），对应原厂行内变量选择器 |
| `condition.{i}.min` | Number(Float64) | Constant | `1` | 区间下限；与 `max` 组成 `PairWith` 区间行（标签「有效区间」），限值 ±999999999 |
| `condition.{i}.max` | Number(Float64) | Constant | `1000` | 区间上限；同上 |

**校验（`Validate`，不兜底、直接报诊断）**
1. `enabled` 且 `source` 缺失 → `IF_CONDITION_SOURCE`「条件 n 已启用，但没有选择取值来源。」
2. `min`/`max` 非有限或超出 ±999999999 → `IF_CONDITION_RANGE`「条件 n 的有效区间必须落在 ±999999999 之内。」
3. `min > max` → `IF_CONDITION_RANGE`「条件 n 的最小值不能大于最大值。」
4. 全部槽未启用不属于校验失败（默认节点必须可准入）；执行期以 `IToolExecutionFilter.ShouldExecute=false` 抑制为 `NotExecuted`，避免「配置未完成却静默成功」。

**执行语义（本项目定义，标注原厂未确认项）**
- 命中判定：`value >= min && value <= max`（**含端点**；原厂端点语义属 E10 未确认，本方明确取闭区间）。
- 选中分支：所有命中且已启用的槽，按槽序升序放入 `SelectedBranches`（**允许多命中**；原厂多命中行为属 E10 未确认）。
- 未命中/未启用槽：不产生选中序号；目标节点由宿主标 `Skipped`，不判失败。
- `output.branch-match`：命中分支显示名以 `;` 连接；无命中为空字符串（原厂 `BranchMatch` 格式未确认，本方定义）。
- `output.module-status`：成功执行 `1`；`output.module-time-ms`：本次模块耗时（ms）。
- 业务判定：`BusinessJudgment.NotEvaluated`（分支工具不给出产品合格判定）。

**已知声明层面限制（上报主线，非本工具可实现）**
1. `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json` 中 `IfBranchModule`（条件分支）现映射 `ToolId: "Condition"` 且无 `MappingVerified`；本工具 ToolId 为 `If`，故工具箱只能以「已部署，未匹配目录」出现在「逻辑工具」下。正确修法在 `src/**`（映射改为 `If` + `MappingVerified:true`），按文件边界不由 B 线修改。
2. 引擎按出边声明顺序编号分支，但宿主目前**不校验「分支源出边数 > 已声明控制输出数」**：第 5 条及以后的出边永远不会被选中（静默不执行）。建议主线在 `WorkflowExecutionEngine` 分支段补校验并报明确诊断。
3. 单出边可见性：画布不为分支源渲染多个分支出口，用户无法从画面区分「第 0 支 / 第 1 支」，只能依赖连线创建顺序。

---

## 第二段 · 实现与验证（If）

（实施完成后回填：修改文件、构建、测试、未决问题）

- 已修改文件：
- 已执行构建与测试：
- 人工验收结果：
- 下一步：

---

## 第三段 · 像素统计（类②首个工具）原厂取证

取证日期 2026-11-17；本机原厂安装根为 `C:\Program Files\VisionMaster4.4.0`（`D:\Program Files\...` 不存在，仅为历史文档记录路径）。以下全部为静态证据（读 XML / 读 DLL 元数据与字符串），**未启动原厂程序、未跑 Ghidra、未占用任何独占动态取证资源**。

### 3.1 证据清单（来源可复查）

| # | 证据项 | 来源（文件 + 位置） | 结论 | 状态 |
|---|---|---|---|---|
| E1 | 工具身份与分类 | `Applications\Module(sp)\x64\Measurement\IMVSPixelCountModuVA\ToolItemInfo.xml:2-6` | `name=IMVSPixelCountModuVA`、`priority=9`、`toolTip=IMVSPixelCountModu_Tip`、`helpUrl=GUID-60D8AD05-…`；原厂分类 Measurement，显示名「像素统计」 | 已证（资源声明） |
| E2 | 显示名与帮助语 | `src/Plugin.Wpf/Themes/Native/Localization/zh-cn.xaml:8716`、`:10239` | `IMVSPixelCountModu_Tip=统计图像中像素总数`、`IMVSPixelCountModuVA=像素统计` | 已证（本地化资源） |
| E3 | 参数页页签与控件树 | `IMVSPixelCountModuVAAlgorithmTab.xml:4-41`（基本参数）、`:42-53`（运行参数）、`:54-255`（结果显示） | 基本参数=ImageSourceGroup + ROI Area（`ROISelecter` ROISelection=`Box SectorRing Sector`、`RoiType`、掩膜/输出掩膜组、继承方式、辅助组）；运行参数=`PixelModuRangeConfigList`（AccessMode=O）；结果显示=`CountLimitEnable`/`RangCountLimit`/`RatioLimitEnable`/`RangRatioLimit` + 4 个 `ImageViewConfig` | 已证（资源 XML） |
| E4 | ROI 可选类型含全图 | `IMVSPixelCountModuVACs.dll` 字符串字面量 | `Full Box SectorRing Sector`（RoiType 取值集），与 E3 的 ROISelection 合并后为 Full/Box/SectorRing/Sector 四种 | 已证（程序集字符串） |
| E5 | 阈值区间列表模型 | `docs/07-工具逆向/decompiled/ModuleControls/IMVSPixelCountModuVA*Control/Apps.XmlParser.ParamTab/PixelModuRangeConfigList.cs:23-33`、`:31`、`:95`、`:122-156`、`:158-207` | 参数名 `Threshold_nums`/`min_Threshold_i`/`max_Threshold_i`/`add_Threshold`/`del_Threshold_i`；`MaxNum=32`；新增项默认 Min=0/Max=255/Inc=1；每项两个 Integer 参数范围 0..255；删除只能删末条（`DelEnable = Count>1`，故最少 1 条） | 已证（反编译源码） |
| E6 | 区间如何进入模块 | `IMVSPixelCountModuVACs.dll` 字符串 `min_Threshold_`/`max_Threshold_`/`DynamicParams`/`Threshold_nums`；`IMVSPixelCountModuVACs.xml:75-114` | 区间以动态参数承载：`GetThresholdRangeItemNum() = DynamicParams.Count / 2`（IL：`ldfld dynamicParams; callvirt Count; div 2`），每区间 2 项；`ThresholdRangeItemParam` 只有 `MinValue`/`MaxValue` | 已证（程序集元数据 + XML 文档） |
| E7 | 输入声明 | `IMVSPixelCountModuVA.xml:Input 段`（`:28-114`） | `InputImage`(IMAGE, InImage/W/H/PixelFormat 强制)、`InputROI`(ROIBOX, roicenterx/y/roiwidth/roiheight/roiangle)、`InputROIAnnulus`(ROIANNULUS, 中心/内外半径/起始角/角扩展)、`Position Correction Info`(FIXTURE)、`InputMask`(IMAGE, InMask/W/H/PixelFormat) | 已证（资源 XML） |
| E8 | 输出声明 | `IMVSPixelCountModuVA.xml:Output 段`（`:117-188`） | `ModuRunTime`(float)、`ModuStatus`(int)、`Count`(int, IsArray=true)、`Ratio`(float, IsArray=true)、`ROI`(ROIBOX 回显 DetectCenterX/Y、DetectWidth/Height、DetectAngle)、`ROIAnnulus`(回显)、`OutputMask`(IMAGE, OutMask/W/H/PixelFormat)、`Output Position Correction Info`(FIXTURE, `Visible=false`) | 已证（资源 XML） |
| E9 | 结果对象类型 | `IMVSPixelCountModuVACs.xml:143-186` + `IMVSPixelCountModuVACs.dll` 字段元数据 | `PixelCountVAResult.Count` = `List<int>`（`0x04000010` 类型 `class:129<i4>`）、`Ratio` = `List<float>`（`class:129<r4>`）；另有 `ROI`/`ROIAnnulus`/`OutputMask` 回显属性；含义是「每个阈值区间一个像素数与一个比率」 | 已证（元数据 + XML 文档） |
| E10 | 判定参数默认值与范围 | `IMVSPixelCountModuVAAlgorithm.xml:2-27`；`IMVSPixelCountModuVAAlgorithmTab.xml:79-173` | CountLimitEnable=False→`RangCountLimit` 隐藏（Trigger False + HiddenOperation）；IntegerBettween 0..999999999（low=0/high=999999999/inc=1）；RatioLimitEnable=False 同理；FloatBettween 0..1（low=0/high=1/inc=0.01） | 已证（资源 XML） |
| E11 | 显示项与颜色/文本模板 | `IMVSPixelCountModuVADisplay.xml:14-42`、`:24-34`、`:63-70` | 文本对象 `Show Text`：`Content="ModuleStatus:{0}PixelNum:{1} ,Rate{2}"`、FontSize=10、OK `#66ff00`/NG `#ff0000`；`ROI`(rect)：OK `#0000ff`、linewidth=2；`OutputMask` 为图像对象 | 已证（资源 XML） |
| E12 | 算法职责与原生边界 | `IMVSPixelCountModuVA.dll` 字符串 | `CIMVSPixelCountModuVA::Init/ProcessAlg/LimitJudge/ProcessOutput`、`GenerateMaskImage`、`VmModule_GetInputRoiBox/GetInputRoiAnnulus`、`CreatePixelCountV3ToolInstance`、依赖 `MVDPixelCountV3Cpp.dll`；模块 DLL 为原生（无 CLI 头，`BSJB` 不存在） | 已证（二进制字符串） |
| E13 | 算法库位置 | `MVDAlgorithmSDK\Runtime\x64\MVDPixelCountV3Cpp.dll`（47KB，导出仅实例创建/销毁） | 计数公式在原生库内，无符号、无可读字符串（仅 `No data.`）→ **公式取证不可得** | 已证（文件存在 + 字符串） |

### 3.2 原厂完整链路（按证据拼接）

1. 参数页「基本参数」：`ImageSourceGroup` 选输入图；`RoiSelecter` 选 ROI 形态（Full/Box/SectorRing/Sector）并在图像上给出 `InputROI`/`InputROIAnnulus` 几何；`InputMaskImageGroup` 选输入掩膜；`OutputMaskImageGroup` 决定是否输出掩膜。
2. 参数页「运行参数」：`PixelModuRangeConfigList` 增删灰度区间（1..32 条，每条 min/max 0..255），增删通过模块参数命令 `add_Threshold`/`del_Threshold_i` 落到动态参数 `min_Threshold_i`/`max_Threshold_i`。
3. 参数页「结果显示」：勾选数量判断/比率判断并设置两条范围；未勾选时对应范围行隐藏。
4. 运行：`ProcessAlg` 取输入图与 ROI/掩膜 → `GenerateMaskImage` 生成统计掩膜 → 原生 `PixelCountV3` 逐区间计数 → 结果写回 `Count[]`/`Ratio[]`/`ModuStatus`，`OutputMask` 输出掩膜图像。
5. 判定：`LimitJudge` 按数量/比率范围给出判定；文本对象按 `ModuleStatus/PixelNum/Rate` 模板显示。

链路缺口（冻结，不猜）：见 3.3 未确认项 U1–U5。

### 3.3 未确认项（本方定义 + 上报）

| # | 缺口 | 影响 | 本方定义（已在代码注释与测试中固化） |
|---|---|---|---|
| U1 | `Ratio` 的分母 | 比率数值 | 分母 = ROI∩掩膜后的参与统计像素数（不是全图像素数） |
| U2 | 阈值端点包含性 | 边界像素归哪个区间 | 闭区间 `[min,max]`（与 If 工具一致） |
| U3 | `InputMask` 语义 | 掩膜是有效区还是屏蔽区 | 非零像素参与统计（与 ROI 求交）；原厂可能相反，属未确认 |
| U4 | 判定作用于区间还是合计 | 判定口径 | 逐区间判定，任一区间越界即整体 NG |
| U5 | `ModuStatus` 取值含义 | 状态码 | `1` = 本次执行成功；判定单独走 `BusinessJudgment` 与逐行 `judgment-passed` |

### 3.4 未实现项（本次明确不做，入口不可误用）

1. `ROIAnnulus` 输出回显（原厂仅在扇环 ROI 下输出）：本方 `output.roi` 只给出检测区域外接旋转矩形。
2. `Position Correction Info`（位置修正，原厂输入 FIXTURE + 输出 `Visible=false`）：输入端口与输出端口均未声明；显示项 `Fixtured Point`/`Unfixtured Point` 也未实现。
3. 区间列表的增删按钮 UI：框架字段声明是类型级静态声明，改为单个文本字段（见 4.4）。

---

## 第四段 · 像素统计实现与验证

### 4.1 交付物

| 项 | 值 |
|---|---|
| ToolId | `PixelCount` |
| 组件 | `SunEyeVision.Tool.PixelCount.Runtime`（Runtime）/ `SunEyeVision.Tool.PixelCount.Wpf`（Editor） |
| 入口类型 | `SunEyeVision.Tool.PixelCount.Runtime.PixelCountTool` / `SunEyeVision.Tool.PixelCount.Wpf.PixelCountEditorFactory` |
| 代码位置 | `tools/SunEyeVision.PixelCount/Runtime/*`、`tools/SunEyeVision.PixelCount/Wpf/*` |
| 登记 | `SunEyeVision.slnx` 的 `/Tools/PixelCountComponents/`、`product/ToolComponents.props`（均只追加自身条目） |
| 目录声明 | 显示名「像素统计」、描述「统计图像中落在各灰度区间的像素数与占比，并按数量/比率判定」、分类「测量」、`Order=9`（原厂 priority 9）、`IconKey=blob`、`NodeDisplayType=DEFAULT` |

### 4.2 端口与契约

- 输入：`input.image`(Image, 必需, Reference)、`input.roi`(Region, 可选, Reference)、`input.mask`(Image, 可选, Reference)。
- 输出：`output.module-status`(Int32)、`output.module-time-ms`(Float32)、`output.count`(Int32 序列, 1..32)、`output.ratio`(Float32 序列, 1..32)、`output.roi`(外接矩形记录, 可空)、`output.mask`(图像记录, 可空)、`output.rows`(区间结果行, 1..32)。
- 实现契约：`IToolDefinition`、`IToolSettings`、`IToolOutput`、`IToolSettingsFields`、`IToolCatalogProvider`、`IToolResultPresentationProvider`（6 必需）+ `IToolResultDisplayProvider`（结果显示）+ `IToolRuntimeInitialization`（`HalconPlatform.Initialise()`）+ `IToolManualReferenceSources`（状态/耗时/像素数/比率可被手动引用）。
- 结果投影：`StandardTable`/`Many`，行身份 `range-index`，列 = 序号/灰度下限/灰度上限/像素数/比率/判定。
- 显示需求：`ResultDisplayDescriptor`（行=结果行、身份=range-index、矩形=rectangle、区域=region、文本值=ratio、判定=judgment-passed、选中矩形色 `#0000ff`）。

### 4.3 参数与校验（页面「运行参数」「结果显示」）

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `range.thresholds` | Text | `0-255` | 阈值区间列表，`下限-上限`，`;`/`,`/换行分隔，1..32 条，0..255，下限≤上限 |
| `limit.count.enabled` | Boolean | `false` | 原厂 `CountLimitEnable` |
| `limit.count.low` / `.high` | Integer(Int32) | `0` / `999999999` | 区间成对显示；勾选后可见 |
| `limit.ratio.enabled` | Boolean | `false` | 原厂 `RatioLimitEnable` |
| `limit.ratio.low` / `.high` | Number(Float32, 两位小数, 步长 0.01) | `0` / `1` | 同上 |

校验（直接报诊断，不兜底）：`PIXELCOUNT_THRESHOLD`（区间文本非法/条数越界/越界灰度/下限大于上限）、`PIXELCOUNT_COUNT_LIMIT`、`PIXELCOUNT_RATIO_LIMIT`。

执行期失败码：`PIXELCOUNT_SETTINGS_INVALID`、`PIXELCOUNT_IMAGE_REQUIRED`、`PIXELCOUNT_PIXEL_FORMAT`、`PIXELCOUNT_MASK_INVALID`（掩膜非 Gray8 或尺寸不同，不缩放兜底）、`PIXELCOUNT_ROI_EMPTY`（统计区域为空，**不输出 0**）、`PIXELCOUNT_CANCELLED`、`PIXELCOUNT_EXECUTION`。

### 4.4 与原厂的差异（逐项）

| 项 | 原厂 | 本方 | 决策依据 |
|---|---|---|---|
| ROI 承载 | `InputROI`/`InputROIAnnulus` 几何参数 + `RoiType` 四种形态 | 单一区域引用 `input.roi`（缺省整图） | 框架区域资源已覆盖全部形态；不新造 `RoiType` 参数（差异已记录） |
| 阈值区间 | 可增删列表控件（1..32 条） | 一个文本字段 + 严格解析 | 字段声明是类型级静态，无动态行编辑器；与 `BlobLabels` 阈值区域同一既有做法 |
| ROI 输出 | `ROI` + `ROIAnnulus` 两路回显 | 单路外接旋转矩形 | 扇环回显未实现（见 3.4） |
| 位置修正 | 输入 FIXTURE + 输出（默认隐藏） | 未实现 | 无回显语义证据 |
| 掩膜 | `InputMask` IMAGE | 同（引用图像，Gray8 同尺寸） | 语义见 U3 |
| 显示项 | ROI/文本/基准点/运行点 + 输出掩膜组 | `display.range.*`（区间像素区域）、`display.roi.*`（检测区域）、`display.text.*`（文本，含原厂模板默认值） | 基准点/运行点随位置修正未实现；掩膜图像作为辅助图像输出 |

显示字段共 18 个，全部随工具持久化、不进调用输入（`ResultDisplayFields.EditorSchema` 校验通过）。

### 4.5 构建与测试

| 项 | 命令 | 结果 |
|---|---|---|
| 全量 Release | `dotnet build SunEyeVision.slnx -c Release --no-incremental -m:1` | **0 错误** |
| 载荷登记 | `Release\SunEyeVision_v1.0.0\SunEyeVision\release-payload.json` | 含 `SunEyeVision.Tool.PixelCount.Runtime/Wpf`、`toolId=PixelCount` |
| 单元测试 | `dotnet test tests\SunEyeVision.Tools.LogicData.Tests\... -c Release --no-build` | **59 通过 / 0 失败**（像素统计新增 36 条 + If 既有 23 条） |

新增测试覆盖：端口与无分支声明、可选 ROI/掩膜、投影与显示需求一致性、显示字段逐一有布局行、默认值准入、显隐与成对区间、区间解析边界（0-255/多条/空格/32 条上限/33 条拒绝/8 类非法文本/往返）、设置校验、Materialize 拒绝非法区间对、宿主声明规则指纹稳定、逐区间计数与比率、输出掩膜 Gray8 全 255、ROI 裁切、掩膜裁切、数量判定逐区间、比率判定整体通过、空 ROI 明确失败、掩膜网格不符明确失败、必需图像端口在冻结期即拒绝、输出释放后拒绝读取且输入不受影响。

### 4.6 需要主线/宿主侧处理（`src/**` 不在本线边界）

1. `src/UI/Resources/Toolbox/ToolboxDisplayCatalog.json:264-269` 中 `IMVSPixelCountModuVA` 的 `ToolId` 为 `null`：需改为 `"ToolId": "PixelCount"` 并加 `MappingVerified: true`，否则工具箱只会以「像素统计（已部署，未匹配目录）」出现。**原件 Id 为 `IMVSPixelCountModuVA`**（同文件 `:258-263` 还有非 VA 变体 `IMVSPixelCountModu`，不要误映射）。
2. 图标：本线未新增图标资源，目录声明 `IconKey=blob`（宿主已有 `tool-blob.png`）。若要求与原件 `IMVSPixelCountModuVAImageEnable.png` 一致，需在 `src/UI/Resources/Toolbox/` 增加图标并改 IconKey。

### 4.7 未验证项（如实记录）

- 未在正式 Release 程序中做人工交互验收（工具箱拖入、参数页显隐、结果页叠加、保存重开）；本次仅完成构建与 Runtime/声明级自动化验证。
- 未取得原厂运行证据（未启动原厂程序），U1–U5 属本方定义。
- 结果页叠加依赖宿主对 `output.rows[].region` 与 `output.roi` 的绘制；本线未做 WPF 视觉核验。
