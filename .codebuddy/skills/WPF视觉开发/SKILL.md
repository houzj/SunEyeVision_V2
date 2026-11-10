---
name: WPF视觉开发
description: WPF视觉开发技能。用于WPF/MVVM界面开发、公共控件迁移与组装、声明式参数页、结果工作区与图像源条、工作流画布与RoI编辑、图像显示控件开发。触发场景：WPF开发、公共控件迁移、.pending原件迁移、参数页显隐绑定、结果工作区、图像源条、画布交互、节点渲染、RoI编辑、图像预览。
---

# WPF 视觉开发技能（2026-11 现行架构）

> 本文件 2026-11-10 重写。旧版描述的 `src/Plugin.SDK/**`、`src/Workflow/WorkflowNode.cs`、`ParamBinding/ConfigSetting/ParamValue<T>`、`DefaultDebugWindow/ToolDebugControlBase` **已随旧 SDK 退役，路径不存在**。按旧版开发会直接走错方向。

## 一、现行分层（唯一真相）

| 项目 | 职责 | 关键约束 |
|---|---|---|
| `src/Plugin.Contracts` | 工具与宿主的**唯一契约**：`IToolSession` / `ToolInvocation` / `ToolOutcome` / `Diagnostic` / `TypeRef` / `ParameterPresentationTree` / `RoiDeclarations` / `SpatialDeclarations` | 不含 WPF、不含业务服务 |
| `src/Plugin.Runtime` | 运行时：`ReleasePayloadAuthority`（发布准入）、`ToolRuntimeExecutor`、`ToolSettingsEditSession`、`FrozenOutputResources`、`ResultResourceOwner`、`RuntimeDirectorySnapshot` | payload 是唯一真源 |
| `src/Plugin.Wpf` | **公共控件库，全平台 UI 唯一归属**：`Controls/Native/**`、`Fields/**`、`Presenters/**`、`Themes/Native/**`、`Regions/**` | 工具 Wpf 只组装，不造公共控件 |
| `src/Plugin.Editing.Wpf` | 编辑期参数模型、`ObservableObject`（见下方硬约束 5） | 不含 WPF 窗口 |
| `src/Domain` | 领域定义：`Workflows/WorkflowDefinition.cs`、`Workflows/ToolConfiguration.cs`、`Workflows/ScopedNodeIdentity.cs`、`Workflows/CanvasPoint.cs`、`Solutions/SolutionDefinition.cs` | 纯数据，无 UI |
| `src/Execution` | 执行：`ExecutionRunBatch`、`ExecutionToolInvocation`、`ExecutionNodeSnapshot`、`ExecutionResourceLeaseManager` | 不访问 WPF 控件 |
| `src/Application` | `Editing`（文档编辑/提交/撤销）、`Queries`、`Communications` | 不依赖 UI |
| `src/UI` | 宿主界面：`Shell/Windows`（MainWindow*）、`Shell/Controls`、`Features/WorkflowUI/Canvas`、`Services/**`、`Shared/**`、`Themes`、`Core/ViewModelBase.cs` | 不承担工作流调度 |
| `tools/<Tool>/Runtime` | 工具算法与参数模型（如 `SunEyeVision.Tool.BlobAnalysis.Runtime`） | 不反依 UI/Application |
| `tools/<Tool>/Wpf` | 工具界面**只组装**（如 `BlobParameterEditor.xaml` 仅 842 字节） | 依赖 Runtime + Plugin.Wpf |

**已退役，禁止引用**：`src/Plugin.SDK`（目录已不存在）、`src/Workflow` 下的旧节点/连线模型（该目录已无 .cs）、`ParamBinding` / `ConfigSetting` / `ParamValue<T>` / `ImageSourceSelector` / `ToolDebugControlBase` / `DefaultDebugWindow`。

## 二、五条硬约束（都是踩过的坑）

### 1. `.pending` 文件不参与编译，不能当"控件缺失"的证据

`src/Plugin.Wpf/Controls/Native` 现有 **333 个 `.pending` / 1219 个文件**。`.pending` 后缀不被 MSBuild 的 `*.cs` / `*.xaml` 默认 glob 收集，因此**任何已编译代码都不可能引用它们**。

- 判断"某控件是否在用"必须**排除 `*.pending`** 再做全仓检索；
- 实测结论：`ImageListPanel` 三件套、`ParameterPresentation.ParamTab` 家族 55 个 pending，与 Blob 三条链**引用关系为 0**；
- `.pending` 只代表"原件尚未迁移"，不代表"功能缺失"；把 pending 迁成 `.cs` 才有消费者。

### 2. 平台大件（HALCON / OpenCV）必须靠 `Default.Resolving`

三处 csproj 以 `<Private>false</Private>` 引用 `halcondotnetxl`（`src/SunEyeVision.HalAdapter/*.csproj`、`src/Plugin.Tooling/*.csproj`），因此它**不进 deps.json、不进 TPA**。`PluginLoadContext.Load` 会把"宿主目录可解析"的大件交回 `AssemblyLoadContext.Default`。

- 默认上下文能解析它们，**只因为** `src/Plugin.Runtime/ReleasePayloadAuthority.cs` 在构造时安装了幂等 `Default.Resolving`（按 payload 精确 `Identity` + SHA256 校验）；
- 这段一旦被删或绕过，表现就是执行时 `Could not load file or assembly 'halcondotnetxl, Version=24.5.0.0…'`，流程直接失败、算法 0.00ms；
- 不要用"再写一个 Resolving 兜底"或"往 deps.json 登记"绕过 payload 校验——那会形成第二份清单。

### 3. 构建与测试的封签顺序

`tests/_shared/ProductRuntimeTestHost.cs` 会校验 `release-payload.json` 里每个程序集的 SHA256。

- 正确顺序：**先** `dotnet build SunEyeVision.slnx -c Release`（构建末尾重新封签）**再** `dotnet test --no-build`；
- `dotnet test` 自带的构建会重建产品程序集，使清单立即失效并抛 `Product assembly SHA mismatch`；
- 重新封签脚本 `installer/Seal-ReleasePayload.ps1` 需要 **PowerShell 7**（用了 Core 专有 API），PS 5.1 会报 `TrimEndingDirectorySeparator` 不存在。

### 4. 参数页是声明式的，不要在工具里逐控件搭

工具侧参数页只有一行 `parameters:DeclaredSettingsView`，三页 Tab 由 `ParameterPresentationTree` + 工具自己的 XML/定义驱动。

- 新增参数 = 改工具的参数声明（如 `BlobParameters.xml` + `BlobParameterDefinition`）+ 设置模型字段；**不要**在工具 XAML 里手写控件；
- 显隐联动、分组、分页由声明里的 `visibleWhen` / `page` / `section` / `advanced` 驱动；
- 改公共控件（`DeclaredFieldsView` 家族）会影响所有工具，必须先评估跨工具影响。

### 5. 属性通知与 MVVM 基类

- `ObservableObject`：`src/Plugin.Editing.Wpf/Models/ObservableObject.cs`
- 宿主 VM 基类：`src/UI/Core/ViewModelBase.cs`（提供 `LogInfo/LogSuccess/LogWarning/LogError`）
- 规则：继承上述基类 + `SetProperty`，**禁止**直接实现 `INotifyPropertyChanged`（原厂 `.pending` 控件常犯这条，迁移时必须改）。

## 三、参数页（声明式）

| 组件 | 路径 | 说明 |
|---|---|---|
| `DeclaredSettingsView` | `src/Plugin.Wpf/Presenters/Parameters/DeclaredSettingsView.cs` | 工具参数页入口，纯代码 ContentControl，按 schema 生成三页 |
| `DeclaredFieldsView` | `src/Plugin.Wpf/Fields/DeclaredFieldsView.cs` | 字段树主体；同目录 `DeclaredFieldsView.{Assist,Displays,Groups,NumericModels,Options}.cs` 分层 |
| 字段编辑器 | `src/Plugin.Wpf/Fields/DeclaredFieldEditor*.cs`、`DeclaredFieldTemplateRegistry.cs` | 按字段类型选模板 |
| 参数树模型 | `src/Plugin.Contracts/ParameterPresentationTree.cs`、`FieldPresentation.cs`、`FieldSettings.cs` | 声明侧唯一来源 |

改参数页显示问题，先确认是**声明（工具 XML/定义）**问题还是**公共控件渲染**问题，不要先改控件。

## 四、结果工作区与图像源条

| 组件 | 路径 | 说明 |
|---|---|---|
| `ResultWorkspaceShell` | `src/Plugin.Wpf/Controls/Native/Composition/Results/ResultShell/ResultWorkspaceShell.xaml` | 结果区容器（图像区 / 属性区 / 图像源条三块） |
| `PropertyListView` | `src/Plugin.Wpf/Controls/Native/PropertyList/PropertyListView.xaml` | 模块结果树表 |
| `ImageSourceStrip` | `src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml(.cs)` | 图像源缩略图条（**自研**，原厂 `ImageListPanel` 未迁） |
| 宿主承载 | `src/UI/Shell/Controls/Shell/ImageResultControl.xaml` | 用 `HasActiveImageSource` 控制整条可见性 |
| 可见性写入口 | `src/UI/Shell/Windows/MainWindowImageSourceEditing.cs` | `SetActiveImageSourceBinding` 是唯一写入口 |
| 投影绑定 | `src/UI/Services/Images/ImageSourceProjectionBinding.cs` | `Owns` 用 `ReferenceEquals` 判失效（增删后易误判） |
| 条会话 | `src/UI/Services/Images/ImageSourceStripSession.cs` | 增删/自动切换/运行全部的交互状态机 |
| 结果发布 | `src/UI/Services/Images/ResultWorkspacePresenter.cs`、`src/UI/Services/Workflow/INodeResultSink.cs`、`src/UI/Shared/NodeOutputCache.cs` | 执行结果 → 显示的唯一通道 |

**已知坑（实测）**：
- 增删文件后整条消失：一次 `CommitEdit` 后 `Owns` 引用判定失效 → 清空 → `HasActiveImageSource=false`；重算只挂在"选中节点变化"上；
- 画布不画结果图形：日志 `[UI.默认] 结果图文未显示：轮廓准备期间结果已失效。` —— 结果在轮廓准备期间被判失效，属发布时序，不是控件问题。

## 五、画布（本节旧版基本仍有效）

| 组件 | 路径 |
|---|---|
| 画布控件 | `src/UI/Features/WorkflowUI/Canvas/WorkflowCanvasControl.xaml(.cs)` |
| 节点显示适配 | `src/UI/Features/WorkflowUI/Canvas/Adapters/`（`NodeDisplayAdapterFactory`、`ImageSourceNodeDisplayAdapter` 等） |
| 画布引擎 | `src/UI/Services/Canvas/`（`ICanvasEngine`、`CanvasEngineManager`、`Engines/*`） |
| 交互拆分 | `src/UI/Services/Interaction/`（`NodeDragHandler`、`ConnectionDragHandler`、`BoxSelectionHandler` 等） |
| 连线 | `src/UI/Services/Connection/`、`src/UI/Services/PathCalculators/` |
| 渲染性能 | `src/UI/Services/Rendering/`（`NodeRenderScheduler`、`NodeUIPool`、`SpatialIndex`） |

节点/连线数据模型在 `src/Domain/Workflows/**`，**不在** `src/Workflow`（旧模型已不存在）。

## 六、图像与 RoI 控件

| 组件 | 路径 |
|---|---|
| `ImageControl`（含 `MultiImageControl` 等） | `src/Plugin.Wpf/Controls/Native/Input/WorkspaceRelease/WorkspaceRelease.Front/` |
| 图像结果显示 | `src/UI/Shared/Controls/Visualization/ImageVisualizationPanel.xaml.cs` |
| RoI 编辑器 | `src/Plugin.Wpf/Presenters/Regions/NativeRoiEditor.cs` + `src/Plugin.Wpf/Regions/` |
| RoI 声明 | `src/Plugin.Contracts/RoiDeclarations.cs`、`SpatialDeclarations.cs`、`GeometryValues.cs` |

## 七、主题与本地化

- 原件主题：`src/Plugin.Wpf/Themes/Native/OriginalTheme.xaml`（TabControl 等原厂样式键由它提供，已无 `clr-namespace` 依赖）
- 本地化：`src/Plugin.Wpf/Themes/Native/Localization/zh-cn.xaml`（原厂文案），`AppLocalization` 为访问入口
- 禁止：在公共控件里硬编码中文可见文案；新增文案先查本地化字典

## 八、日志与命名

- VM 层：`LogInfo/LogSuccess/LogWarning/LogError`（`ViewModelBase`）；Service 层用 `IAppLogger`
- 禁止 `Debug.WriteLine` / `Console.WriteLine` / `Trace.WriteLine`
- 失败必须可见：执行/发布失败要写日志（含节点身份 + 错误码），不能只在界面显示
- 命名：PascalCase 类/方法/属性、`_camelCase` 私有字段、`Is/Has/Can` 布尔、过去式事件、`动词+Command`

## 九、代码锚点速查

| 要做的事 | 从这里入手 |
|---|---|
| 改工具参数页字段/显隐 | 工具的参数 XML/定义 → `ParameterPresentationTree` |
| 改参数控件外观 | `src/Plugin.Wpf/Fields/DeclaredFieldsView*.cs` |
| 改结果区布局 | `ResultWorkspaceShell.xaml` |
| 改模块结果列 | `PropertyListView` + 工具的结果声明（`BlobSchema.Outputs` 等） |
| 改图像源条 | `ImageSourceStrip` + `ImageSourceStripSession` + `MainWindowImageSourceEditing` |
| 改执行→显示 | `ResultWorkspacePresenter` / `INodeResultSink` / `NodeOutputCache` |
| 改画布节点外观 | `Adapters/*NodeDisplayAdapter` + `NodeStyleConfig` |
| 排查"程序集加载失败" | `src/Plugin.Runtime/ReleasePayloadAuthority.cs` 的 `Default.Resolving` |

## 十、验收纪律

- UI 改动**必须真实启动程序目视核验**，不能用"测试通过"代替；
- 构件级验证顺序：`dotnet build SunEyeVision.slnx -c Release` → 确认真实启动 → 再 `dotnet test --no-build`；
- 未接入项（如历史结果页、结果图文叠层）要在界面与台账里显式标注"未接入"，不得用占位实现冒充完成；
- 迁移原厂 `.pending` 控件时，先剥离原厂依赖（`VM.Core`、`VM.PlatformSDKCS`、`PresentationControls.Interface`、`View.Utility`、`Apps.*`、`SharedStyles`、`Microsoft.WindowsAPICodePack`），再评估是否可直接用；无法剥离的按行为契约重写。
