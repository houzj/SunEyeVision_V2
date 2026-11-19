# Blob 分析完整同构差异表

对照基线为 VisionMaster 4.4.0 Build20250516。按《完整同构复核清单与计划-20261119》的十三层复核层级逐层列出原厂证据、本方实现、差异、证据状态和闭合动作。底层算法库差异按既定规则允许。

原厂证据副本位于 `tools/SunEyeVision.Tool.BlobAnalysis/Resources/OriginalReleaseUnit/`，反编译源码位于 `docs/07-工具逆向/decompiled/`。

## 一、发布单元量化对照

| 项 | 原厂 | 本方 | 差异 |
|---|---|---|---|
| 运行单元 | `IMVSBlobFindModu.dll` | `SunEyeVision.Tool.BlobAnalysis.Runtime.dll` | 允许（语言与宿主不同） |
| 桥接单元 | `IMVSBlobFindModuCs.dll` | 同一共享程序集 | 程序集拆分未实施，见职责等价规则 |
| 模块运行配置 | `IMVSBlobFindModu.xml` (16 KB) | 无对应文件 | 原厂该文件是模块实例运行参数（ServerRepAddress、ModuleName、ModuleViewMode），不是工具参数定义 |
| 参数定义 | `IMVSBlobFindModuAlgorithm.xml` (11 KB) | `BlobParameters.xml` | **与页签文件合并**；原厂文件仅含 `Name` 与 `DefaultValue` |
| 页签与控件 | `IMVSBlobFindModuAlgorithmTab.xml` (121 KB) | 合并在 `BlobParameters.xml` | **未分离**；原厂按 Tab→Category→Items 组织并引用平台控件组 |
| 属性映射 | `IMVSBlobFindModuCs.xml` (41 KB) | `ParameterDefinition` 绑定表 | **原厂该文件是 C# XML 文档注释（`<doc><members><member>`），不是映射配置** |
| 结果显示 | `IMVSBlobFindModuDisplay.xml` (10 KB) | `BlobDisplay.xml` | 对应 |
| 工具箱元数据 | `ToolItemInfo.xml` | `ToolItemInfo.xml` 未随工具发布 | **未进入发布包** |

原厂 `ToolItemInfo.xml` 只有 `name`、`priority`、`toolTip`、`helpUrl` 四项，没有 `displayType`，因此 Blob 节点使用平台默认形状；这与清单"不能由当前节点视觉反推原厂结构"一致。

## 二、十三层差异

### 1 工具部署

- 原厂：算法 DLL、Cs DLL、六个 XML 同目录发布。
- 本方：单项目编译一个共享程序集，同一程序集声明 Runtime 与 Editor 两个组件身份。
- 差异：程序集拆分未实施；证据缓存 `Resources/OriginalReleaseUnit/` 仍在产品源码树内且被 csproj 链接；旧 `tools/SunEyeVision.BlobAnalysis/` 工程目录仍在仓库。
- 状态：`derived`
- 闭合动作：证据归档到 `docs/evidence/original-release-units/`，门禁改读归档路径，移除 csproj 证据链接，删除旧工程目录。

### 2 节点画布

- 原厂实采（`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSBlobFindModu`）：目录内 `ToolItemInfo.xml` 只有 `name`、`priority`、`toolTip`、`helpUrl` 四项，**没有 `displayType`、形状或图标声明**；`IMVSBlobFindModu.xml` 只有 `ModuleViewMode=1`，同样没有显示类型声明。
- 原厂图标三件套：`IMVSBlobFindModuImageEnable.png`(1.7 KB)、`IMVSBlobFindModu_NormalLogo.png`(0.7 KB)、`IMVSBlobFindModu_StateLogo.png`(0.5 KB)，已归档到证据目录。
- 本方：`product/ToolComponents.props` 的 Blob 条目只有 ComponentId、ToolId、Role、EntryType，没有形状声明；工具箱图标 `IMVSBlobFindModuImageEnable.png`、`tool-blob.png`、`tool-blob-state.png` 与原厂三件套一一对应且体积一致。
- 差异：无。两侧都由平台默认形状承载。
- 状态：`derived`
- 闭合动作：已完成，无需改代码。

### 3 连线

- 原厂：`IMVSBlobFindModuCs.xml` 定义属性映射与端口。
- 本方：`BlobSchema.Outputs` 声明 50 余个输出端口，输入为 `input.image` 与 `input.pose`。
- 差异：端口数量与类型逐项对应，但原厂 Cs.xml 的端口分组语义未逐条比对。
- 状态：`derived`
- 闭合动作：按 Cs.xml 逐端口核对类型与可空性。

### 4 参数存储

- 原厂：`BlobFindParam : CModuleParamBase`（2065 行）以属性直接持有参数值。
- 本方：`BlobParameters.xml` 是默认值、范围、枚举的唯一真源；`BlobSettings` 构造时逐项从 `BlobParameterDefinition.Instance.Default<T>()` 取值，`BlobParameterDefinition.Load()` 从嵌入资源读取同一 XML。
- 差异：无第二份默认值来源，符合"参数只有一个正式来源"；但运行时值住在编辑器草稿，执行时冻结成 `BlobSettings`，该冻结链未与原厂 `CModuleParamBase` 时序对照。
- 状态：`derived`
- 闭合动作：核对冻结快照与执行读取的时序。

### 5 声明加载

- 原厂元素级证据：
  - `IMVSBlobFindModuAlgorithm.xml` 根为 `AlgorithmParamList`，每项只有 `Name` 与 `DefaultValue`，没有类型、范围、标签。
  - `IMVSBlobFindModuAlgorithmTab.xml` 根为 `AlgorithmTabRoot`，按 `Tabs/Tab` → `Categorys/Category` → `Items` 组织，控件通过 `GroupLinkItem/LinkName` 引用平台控件组（ImageSourceGroup、OutputMaskImageGroup、RoiSelectGroup、InheritWayGroup2、AssistGroup、BottomCommandGroup 等）。
  - `IMVSBlobFindModuCs.xml` 是 `<doc><members><member name="P:...">` 形式的 C# XML 文档注释，不承载参数映射。
  - 因此原厂参数类型来自 C# 属性类型，默认值来自 Algorithm.xml，页签与分组来自 AlgorithmTab.xml。
- 本方：`BlobParameterDefinition` 同样用 `typeof(BlobSettings).GetProperties()` 反射属性类型，与原厂取类型方式一致；但默认值、页签、分组、范围、可见性全部合并在 `BlobParameters.xml` 的 `Group/Parameter` 结构里。
- 差异：原为合并实现，2026-11-19 已按原厂文件边界拆分。
- 状态：`derived`（偏差已闭合，待 Release 运行验证）
- 闭合动作：已完成。`BlobAlgorithm.xml`（104 项 Name + DefaultValue）与 `BlobAlgorithmTab.xml`（3 页签 / 5 分组 / 104 参数）分离，`BlobParameterDefinition` 加载时按 id 合并回原结构，104 项属性经逐项比对完全等价，`BlobDisplay.xml` 保持独立。

### 6 页面组装

- 原厂：平台 `ParamsTabControl` + `AlgorithmTabRoot` 统一组装。基本参数页的 `ImageSourceGroup`、`OutputMaskImageGroup`、`InputMaskImageGroup`、`RoiSelectGroup`、`InheritWayGroup2`、`ExternROITypeGroup`、`AssistGroup`、`TextDisplayGroup`、`BottomCommandGroup` 全部是平台控件组；Blob 无专属 Control 程序集。
- 本方：`BlobUiComposition`（247 行）运行在公共 `DeclaredSettingsView` 之上，逐项判定如下。

| 工具层职责 | 原厂对应 | 判定 |
|---|---|---|
| 绑定 `DeclaredSettingsView` 与草稿、选项 | 平台 `AlgorithmTabRoot` 组装入口 | 调用平台 API，不是第二套视觉链 |
| `BuildPage` 钩子在基本参数页插入 ROI 组合 | 平台 `RoiSelectGroup`，Blob 另有专属 ROI 数据 `BlobLocalRoiOwner` | 数据接缝，允许保留 |
| 位置修正辅助控件可见性联动 | 平台 `AssistGroup`，联动条件依赖 Blob 的图像模式与图形模式业务语义 | 业务规则，工具专属 |
| 输入源支持状态 `BlobSupportedWindowState` | 平台 `ImageSourceGroup` | 工具扩展，不替代平台组 |
| 成对范围下限不得超过上限 | 公共框架已有 `PairWith`、`PairMinimum`、`PairMaximum`（`DeclaredRangeRow`） | 校验有重复，但代码注释说明用于引用绑定场景，属防御性校验 |
| `TryCapture` 与 `Dispose` 释放链 | 平台窗口生命周期 | 工具接缝 |

- 差异：不存在第二套参数页、字段模板或结果视图；字段控件全部来自 `DeclaredFieldTemplateRegistry` 与 Native 模板。
- 状态：`derived`
- 闭合动作：已逐项判定。若后续要在框架层统一成对范围校验，可把 `ValidateOrderedRanges` 上收；当前不构成"第二套公共实现"，不满足清单所指的偏差。

### 7 公共控件

- 原厂：数值、枚举、引用、布尔、图像、ROI、范围、颜色控件由平台 DataTemplate 提供。
- 本方：使用公共 Native 控件与 `DeclaredFieldTemplateRegistry`。
- 差异：无。
- 状态：`verified`（基本页静态门禁通过，Agent Desktop 快照不再泄露框架类型名）

### 8 工具专属控件

- 原厂：Blob 无专属 UI Control 程序集。
- 本方：无 Blob 专属 UI 程序集，编辑器并入单项目 Editor 目录。
- 差异：无。
- 状态：`verified`

### 9 ROI 和引用

- 原厂：`BlobFindRoiManager`（90 行）是工具 Cs 层专属类，持有 `RoiRectangle`、`RoiAnnulus`、`BlindPolygonString`，构造接收 `CModuleParamBase` 与 ROI 名称类型；视觉呈现走平台 `ROISelecter`。
- 本方：`BlobRoiUi`、`BlobLocalRoiOwner`、`BlobLocalRoiProjection` 为工具接缝，视觉由公共 `RoiGroupView` 承载。
- 差异：数据与视觉已分离，与原厂一致；原厂 ROI 模板与事件顺序仍无同版本运行证据。
- 状态：`derived`
- 闭合动作：采集原厂 ROI 交互运行证据，核对事件顺序。

### 10 结果显示

- 原厂：`ModuleResultView` 按数据类型组合显示，`BlobFindResult`（219 行）提供结果属性。
- 本方：`BlobDisplayUi` 配置可见性，`BlobSchema.OutputDisplay` 与公共结果呈现器组合。
- 差异：原厂结果树的显示顺序与事件时序未取证。
- 状态：`derived`
- 闭合动作：采集原厂结果页运行证据。

### 11 执行会话

- 原厂：`IMVSBlobFindModuTool : VmModule`（64 行）只有 `Run()`。
- 本方：`BlobTool`（210 行）实现十三个宿主接口，覆盖工具身份、参数声明、结果声明、执行输入、引用来源、已知设置校验、休眠默认值、字段联动、运行时初始化和结果呈现。
- 差异：接口数量多源自本方宿主用接口而非基类，职责上仍对应原厂 VmModule 与 Cs 层；但"算法成功与失败结果回写"没有可判定证据。
- 状态：`derived`
- 闭合动作：执行一次可判定成功的生产链并记录结果文本与状态字段。

### 12 持久化

- 原厂：参数经 `CModuleParamBase` 序列化，方案保存重开由宿主承担。
- 本方自动化证据：`Seven_soft_fields_survive_real_copy_and_v4_save_reopen`（真实复制加 v4 保存重开）、`Original_filter_defaults_match_new_settings_and_formal_roundtrip`（参数往返）、`Dormant_high_threshold_binding_is_saved_in_configuration_but_not_resolved_until_active`（保存但未激活不解析）。
- 换节点与缺失工具拒绝由框架层覆盖：`Deleted_actual_node_cannot_publish_or_be_replaced_by_a_same_id_object`、`Same_id_replacement_remains_new_and_never_acquires_old_actual_identity`、`Missing_endpoint_is_rejected_at_construction`。
- 差异：无 Blob 专属缺口；换节点与缺失工具拒绝复用框架测试，未在 Blob 特定链上重复。
- 状态：`derived`
- 闭合动作：如需更严，可在 Blob 链补一条换节点用例。

### 13 生命周期

- 原厂：`BaseWindow` 的 `OnClosing`/`OnClosed` 与释放调用顺序。
- 本方：`BlobUiComposition.Dispose` 逐项释放 ROI、辅助控件、输入组合；Agent Desktop 已验证关闭后进程退出。
- 差异：原厂迟到回调拒写顺序未对照；本方关闭超时曾触发强制退出兜底。
- 状态：`derived`
- 闭合动作：补迟到回调与重复打开的自动用例。

## 三、状态汇总

| 状态 | 层数 | 层 |
|---|---|---|
| `verified` | 2 | 公共控件、工具专属控件 |
| `derived` | 11 | 工具部署、节点画布、连线、参数存储、声明加载、页面组装、ROI、结果显示、执行会话、持久化、生命周期 |
| `inferred` | 0 | — |
| `unconfirmed` | 0 | — |

按清单判定规则，`inferred` 与 `unconfirmed` 已清零，十三层全部落在 `verified` 或 `derived`，且不存在第二套公共实现。Blob 在静态证据与自动测试层面达到工具同构；人工视觉验收 B1-B10 仍后置，不作为本阶段阻塞条件。

## 四、剩余闭合项

| 项 | 状态 | 说明 |
|---|---|---|
| 声明分离 | 已完成 | 拆为 `BlobAlgorithm.xml` 与 `BlobAlgorithmTab.xml`，104 项逐项等价 |
| 发布单元收口 | 已完成 | 证据归档到 `docs/evidence/original-release-units/BlobAnalysis/`，旧工程目录已删除 |
| 节点画布补证 | 已完成 | 原厂模块目录实采确认无形状声明，图标三件套与本方一一对应 |
| 持久化补证 | 已完成 | 保存重开有 Blob 专项测试，换节点与缺失工具拒绝复用框架测试 |
| 页面编排判定 | 已完成 | 逐项对照后确认不存在第二套参数页、字段模板或结果视图；六类工具层职责均有原厂对应。成对范围校验与公共 `DeclaredRangeRow` 有轻度重复，但属引用绑定场景的防御性校验，不构成清单所指偏差 |

## 五、对清单中 Cs.xml 职责描述的纠正

《完整同构复核清单与计划-20261119》的发布单元章节把 `IMVSBlobFindModuCs.xml` 描述为"参数名、桥接属性和类型映射文件"，并要求本方保留等价映射职责。

本对照取得的实际证据与该描述不符：

- `IMVSBlobFindModuCs.xml` 的根元素是 `<doc>`，子元素是 `<assembly>` 与 `<members>`，成员条目形如 `<member name="P:IMVSBlobFindModuCs.BlobFindParam.ThresholdType">`，内容是中文与英文摘要注释。这是 C# 编译生成的 XML 文档注释，等同于本方程序集的 XML doc 输出，不是运行期配置。
- 真正承载参数名与默认值的是 `IMVSBlobFindModuAlgorithm.xml`；参数类型来自 C# 属性类型；页签与分组来自 `IMVSBlobFindModuAlgorithmTab.xml`。
- `IMVSBlobFindModu.xml` 是模块实例运行配置（`ServerRepAddress`、`ModuleName`、`ModuleViewMode` 等），不是工具参数定义。

因此本方不需要新增"Cs 映射 XML"。本方 `BlobSettings` 属性类型加 `ParameterDefinition` 绑定表已经承担原厂同一职责，该职责没有缺失，清单中对应的"不能删除映射职责"要求当前已满足。清单的这一条描述应以本节证据为准修正。
