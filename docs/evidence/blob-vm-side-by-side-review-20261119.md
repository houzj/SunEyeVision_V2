# Blob 分析：VM 原厂与本方一一对应复核

对照基线：VisionMaster 4.4.0 Build20250516。

原厂证据全部来自本机安装目录与反编译产物，路径固定为：

- 安装目录：`C:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSBlobFindModu\`
- 证据副本：`docs/evidence/original-release-units/BlobAnalysis/`
- 反编译源码：`docs/07-工具逆向/decompiled/`

状态用词沿用清单：`已证实` / `推断` / `未确认`。

## 一、框架设计层

### 1.1 四层结构

| 原厂层 | 证据 | 本方对应 | 判定 |
|---|---|---|---|
| 平台 UI（XmlUI / VMControls.WPF / VM.PcUI） | `decompiled/XmlParser`、`WpfControls`、`PcUi` 三个目录 | `src/Plugin.Wpf` | 对应 |
| 托管桥接 `XxxCs.dll`（全库仅 8 个工具具备） | `decompiled/Managed/` 只有 8 个目录，Blob 在列 | 同一共享程序集 | 职责对应，物理合并属允许差异 |
| 原生算法 `XxxModu.dll` | `IMVSBlobFindModu.dll` 243 KB | `BlobAlgorithm.cs` 1290 行 | 语言不同，职责对应 |
| 专属 UI `XxxControl.dll`（全库 72 个） | `decompiled/ModuleControls/` 72 个目录，**Blob 不在其中** | 无专属 UI 程序集 | 对应（Blob 本就不该有） |

### 1.2 发布单元

| 原厂文件 | 大小 | 实际内容（已证实） | 本方对应 | 判定 |
|---|---|---|---|---|
| `IMVSBlobFindModu.dll` | 243 KB | 原生算法 | `BlobAlgorithm.cs` | 允许差异 |
| `IMVSBlobFindModuCs.dll` | 36.5 KB | 参数/结果/ROI 桥接 | 同一程序集 | 允许差异 |
| `IMVSBlobFindModu.xml` | 15.5 KB | **模块实例运行配置**（`ServerRepAddress`、`ModuleName`、`ModuleViewMode`），不是工具参数 | 无对应文件 | 由宿主承担，属允许差异 |
| `IMVSBlobFindModuAlgorithm.xml` | 10.6 KB | **只有 `Name` + `DefaultValue`**，无类型无范围 | `BlobParameters.xml` | 合并，见 1.3 |
| `IMVSBlobFindModuAlgorithmTab.xml` | 121 KB | 页签 → 类别 → 控件 → 平台控件组引用 | `BlobParameters.xml` | 合并，见 1.3 |
| `IMVSBlobFindModuCs.xml` | 41.2 KB | **C# XML 文档注释**（`<doc><members><member>`），不是映射配置 | 无 | 不需要，清单原描述有误 |
| `IMVSBlobFindModuDisplay.xml` | 10.4 KB | **16 个显示对象声明**（图像、形状、文本、结果列表） | `BlobDisplay.xml` 是**表格列**，显示对象在代码里 | **职责错位** |
| `ToolItemInfo.xml` | 0.2 KB | 仅 `name`/`priority`/`toolTip`/`helpUrl` | 平台工具箱目录 | 平台承担 |
| `IMVSBlobFindModuImageEnable.png` | 1.7 KB | 工具箱图标 | `src/UI/Resources/Toolbox/IMVSBlobFindModuImageEnable.png` | 对应 |
| `IMVSBlobFindModu_NormalLogo.png` | 0.7 KB | 节点常态图标 | `tool-blob.png` | 对应 |
| `IMVSBlobFindModu_StateLogo.png` | 0.5 KB | 节点状态图标 | `tool-blob-state.png` | 对应 |

### 1.3 声明文件分工

原厂把「参数定义」与「页面布局」分成两个文件，本方按方案 6.2 合并成一个，理由已写入方案：拆分必要性未验证，无同一批参数多套布局的需求。

| 原厂职责 | 本方落点 | 判定 |
|---|---|---|
| 参数名 + 默认值 | `BlobParameters.xml` 的 `Parameter@id/@default` | 对应 |
| 页签 / 类别 / 控件 / 顺序 | 同文件 `Group@page/@section/@vmCategory` + 参数顺序 | 对应，文件合并 |
| 参数类型 | C# 属性类型反射（`typeof(BlobSettings).GetProperties()`） | 与原厂取类型方式一致 |
| 结果显示对象 | `BlobDisplay.xml` 只声明表格列；图形显示在 `BlobDisplayUi` 代码里 | **偏差** |

### 1.4 平台组件

| 原厂组件 | 本方对应 | 判定 |
|---|---|---|
| `BaseWindow` + `ParamsTabControl` | `RuntimeToolEditorWindow` + `RuntimeNodeConfigurationControl` | 对应 |
| `AlgorithmTabRoot` / `ParamsConfigControlBase` / `ParamListView` | `DeclaredParameterSchema` + `DeclaredSettingsView` + `DeclaredFieldsView` | 对应（声明加载器本轮补齐） |
| `ROISelecter : NameValueItem` | `NativeRoiEditor` + `NativeRoiSession` + `IRoiDocument` + `INativeRoiEditDocument` + `NativeRoiGroupComposer` | 对应（本轮补齐契约） |
| `ModuleResultView`（按数据类型分区） | `ResultRendererRegistry` + 声明投影 | **部分**，见 2.4 |
| DataTemplate 资源字典 | `DeclaredFieldTemplateRegistry` | 对应 |
| 参数表（在 C++ 引擎） | `ParameterStore`（本轮新增，待接入） | 方向一致，未接入 |

## 二、功能实现层

### 2.1 页签与分组

原厂 Tab 三个：`Tab_Basic Params`、`Tab_Run Params`、`ResultShow`。

原厂 Category 四个（已证实）：

| 原厂 Category | 属性 | 本方 | 判定 |
|---|---|---|---|
| `Tab_ROI Area` | `CanTriggerRun="TRUE"` | 基本参数 / ROI区域 | 对应 |
| `Tab_Run Params` | `CanTriggerRun="TRUE"` | 运行参数 / 运行参数 + 高级 | 本方拆两个 section |
| `Tab_Check Result` | `SubscribeVisible="False"` `CanTriggerRun="TRUE"` | 结果显示 / 结果判定 + 高级判定 | 本方拆两个 section |
| `Tab_Image Display` | 无 | **无对应** | **缺** |

`Tab_Image Display` 缺失意味着：原厂在结果页有一个独立的图像显示配置区，本方没有对应声明。

### 2.2 参数

| 项 | 原厂 | 本方 | 说明 |
|---|---|---|---|
| C# 参数属性 | `BlobFindParam.cs` 2065 行 / ≈112 属性 | `BlobSettings` 138 属性（含非字段标记） | 本方多出 ROI、输入、位置修正等宿主字段 |
| XML 参数项 | `Algorithm.xml` 112 项 `Name`+`DefaultValue` | `BlobParameters.xml` 104 项 | 逐条差异见 `docs/collaboration/acceptance/D2v2-对照-BlobAnalysis.md`（原厂 129 / 本方 104） |
| 参数值存放 | C++ 引擎，C# 无副本 | 编辑器草稿 → 执行时冻结 `BlobSettings` | 方向一致，`ParameterStore` 已建但未接入 |

### 2.3 ROI

| 项 | 原厂 | 本方 | 判定 |
|---|---|---|---|
| ROI 数据类 | `BlobFindRoiManager.cs` 90 行，4 个成员：`RoiRectangle`、`RoiAnnulus`、`BlindPolygonString`、构造函数 | `BlobRoiInheritance` + `BlobMaskInheritance` + `BlobLocalRoi` 约 300 行 | 本方更细（含形状校验、规范化、序列化） |
| ROI 视觉 | 平台 `ROISelecter` | `NativeRoiEditor` + `NativeRoiSession` | 对应 |
| ROI 编辑编排 | 平台内部 | `BlobLocalRoiOwner` 约 570 行，混合了会话编排与 Blob 业务 | **结构差异**：本方把业务与会话混在一个类 |
| ROI 组声明 | `AlgorithmTab` 的 `RoiSelectGroup` / `InheritWayGroup2` / `ExternROITypeGroup` | `BlobParameters.xml` 的 ROI 组 + `BlobRoi` 的字段定义 | 对应 |

### 2.4 结果显示

原厂 `Display.xml` 有 16 个显示对象（已证实）：

| # | 原厂对象 | 本方对应 | 判定 |
|---|---|---|---|
| 1 | Display Input Image | 平台图像视口 | 平台承担 |
| 2 | Display Blob Image | **无** | **缺** |
| 3 | Display Binary Image | **无** | **缺** |
| 4 | Blob | `GraphicDisplays` 的 Blob 组 | 对应 |
| 5 | Blob Max Area Inner Rect | `InnerRectangle` 组 | 对应 |
| 6 | ROI | `Inspection` 组 | 对应 |
| 7 | Contour Point | 仅有 `outline` 可见性开关 | **部分** |
| 8 | Fixtured Point | `ReferencePoint` 组 | 对应 |
| 9 | Unfixtured Point | `RunPoint` 组 | 对应 |
| 10 | Show Text | `TextDisplays` | 对应 |
| 11 | Data Record | **无** | **缺** |
| 12 | Data Record | **无** | **缺** |
| 13 | Result List | `BlobDisplay.xml` 的表格列（10 列） | 职责近似，格式不同 |
| 14 | ExternROI | **无** | **缺** |
| 15 | OutputMask | **无** | **缺** |
| 16 | Annular | `Annulus` 组 | 对应 |

本方 `BlobDisplay.xml` 实际内容是结果表格列（`Table/Column`），与原厂 `Display.xml` 的「显示对象 + 映射」不是同一职责。

### 2.5 执行

| 项 | 原厂 | 本方 | 判定 |
|---|---|---|---|
| 工具入口 | `IMVSBlobFindModuTool.cs` 64 行，继承 `VmModule`，只有 `Run()` | `BlobTool.cs` 228 行，实现 13 个宿主接口 | 接口数量差异来自宿主用接口而非基类，职责对应 |
| 算法调用 | 原生 DLL | `BlobAlgorithm.cs` + HALCON 经 `HalAdapter` | 允许差异 |
| 结果契约 | `BlobFindResult.cs` 219 行 / ≈33 属性 | `BlobResult.cs` 63 个输出端口 | 本方端口更细，需按端口逐项对应 |

### 2.6 保存与释放

| 项 | 原厂 | 本方 | 判定 |
|---|---|---|---|
| 保存 | `CModuleParamBase` 序列化 | `BlobSettings` 记录 + 方案 JSON | 对应，有 `Seven_soft_fields_survive_real_copy_and_v4_save_reopen` 覆盖 |
| 释放 | `BaseWindow.OnClosed` | `BlobUiComposition.Dispose` + 窗口释放链 | 对应，Agent Desktop 验证过进程退出 |
| 迟到回调 | 未取证 | 有代次/投影版本控制 | **未确认** |

## 三、差异清单

### 3.1 已证实的差异（需处理）

| # | 差异 | 原厂证据 | 本方现状 | 影响 |
|---|---|---|---|---|
| D1 | 缺 `Tab_Image Display` 分类 | `IMVSBlobFindModuAlgorithmTab.xml` 的 `<Category Name="Tab_Image Display">` | 无 | 结果页图像显示配置无处声明 |
| D2 | 显示声明职责错位 | `Display.xml` 16 个 `Object` + `Features/Mapping` | `BlobDisplay.xml` 是表格列；显示对象写在 `BlobDisplayUi` 代码里 | 声明驱动被绕开 |
| D3 | 缺 7 个显示对象 | `Display.xml`：Blob Image、Binary Image、Contour Point、Data Record ×2、ExternROI、OutputMask | 无对应 | 叠加显示不完整 |
| D4 | ROI 编辑编排未分层 | `BlobFindRoiManager` 90 行 vs 平台 `ROISelecter` 分离 | `BlobLocalRoiOwner` 570 行混合两者，内部仍有 38 个 Blob 静态引用 | 职责边界不清 |

### 3.2 已证实的对齐项

| # | 项 | 证据 |
|---|---|---|
| A1 | 四层结构对应，Blob 无专属 UI 程序集 | `decompiled/ModuleControls/` 72 目录不含 Blob |
| A2 | 页签三个、类别四个中三个已对应 | `AlgorithmTab.xml` 的 `Tab` 与 `Category` |
| A3 | ROI 视觉与绘制在平台 | `NativeRoiEditor`/`NativeRoiSession` |
| A4 | 参数类型由 C# 属性反射决定 | 双方一致 |
| A5 | 工具箱元数据由平台承担 | `ToolItemInfo.xml` 仅 4 项 |
| A6 | 图标三件套一一对应且体积一致 | 安装目录 PNG 与本方 Toolbox 资源 |

### 3.3 未确认

| # | 项 | 缺什么 |
|---|---|---|
| U1 | 迟到回调拒写顺序 | 原厂 `BaseWindow` 关闭时序的运行证据 |
| U2 | `Tab_Image Display` 的完整控件布局 | 该 Category 下的 `Items` 未逐项展开 |
| U3 | `Display.xml` 每个 `Object` 的完整映射语义 | 16 个对象只取了 `Name`，`Features/Mapping` 未逐条读 |

## 四、下一步

按差异编号推进，顺序固定：

1. **D1 + D2 + D3**：把 `Display.xml` 的 16 个对象读全，补齐 `Tab_Image Display` 与显示对象声明，让显示回到声明驱动。
2. **D4**：按 38 个成员逐个判断归属，把纯会话编排移出 `BlobLocalRoiOwner`，Blob 业务留在工具侧。
3. **U1**：补原厂关闭时序证据后再动生命周期。
