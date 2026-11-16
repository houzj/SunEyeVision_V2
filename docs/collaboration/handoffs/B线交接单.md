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
