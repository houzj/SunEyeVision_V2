---
name: WPF视觉开发
description: WPF视觉开发技能。用于WPF/MVVM界面开发、工作流画布编辑器、节点系统、RoI编辑器、图像显示控件开发。触发场景：WPF开发、画布交互、节点渲染、连接绘制、RoI编辑、图像预览。
---

# WPF 视觉开发技能

## 触发场景

- WPF/MVVM 界面开发
- 工作流画布编辑器交互
- 节点渲染和样式
- 连接路径绘制
- RoI 编辑器开发
- 图像预览控件
- 参数面板绑定

## 核心架构

### 目录分层（2026-06 UI 大重构后）

```
src/UI/
  ├─ Core/          ← ViewModelBase、RelayCommand、ServiceLocator、IRootCoordinator
  ├─ Features/      ← 功能模块（WorkflowUI：画布、Tab VM、调试窗口工厂）
  ├─ Shell/         ← 主窗口（Windows/MainWindowViewModel、Controls/Panels/ImagePreviewControl）
  ├─ Services/      ← 服务层（Interaction/Connection/Canvas/Rendering/PathCalculators/...）
  ├─ Shared/        ← 共享模型（NodeStyleConfig、WorkflowNodeModel、MatExtensions 等）
  ├─ Converters/    ← 转换器（30 个）
  ├─ Themes/        ← 主题资源
  └─ App.xaml       ← 应用入口

src/Plugin.SDK/
  ├─ Models/        ← ObservableObject、SdkTypeVocabulary、Geometry（Point2d/Line2d/Circle2d）
  ├─ ViewModels/    ← ToolViewModelBase
  ├─ Execution/     ← Parameters/Results（ToolParameters、ToolResults）
  └─ UI/
     ├─ Controls/   ← ImageControl/、Roi/、ParamBinding、ToolDebugControlBase、ImageSourceSelector 等
     └─ Windows/    ← DefaultDebugWindow.xaml
```

### MVVM 分层

```
View (XAML) ← Binding → ViewModel ← 调用 → Service/Model
```

- **ViewModelBase** (`src/UI/Core/ViewModelBase.cs`) — UI层基类，继承 ObservableObject，提供 LogInfo/LogSuccess 等便捷日志 + 线程安全集合操作
- **ObservableObject** (`src/Plugin.SDK/Models/ObservableObject.cs`) — SDK层基类
- **ToolViewModelBase** (`src/Plugin.SDK/ViewModels/ToolViewModelBase.cs`) — 工具VM基类

属性通知规范：
```csharp
// 用户可见属性 - 带中文显示名
public int Threshold
{
    get => _threshold;
    set => SetProperty(ref _threshold, value, "阈值");
}

// 内部属性 - 不带显示名
public bool IsVisible
{
    get => _isVisible;
    set => SetProperty(ref _isVisible, value);
}
```

## 工作流画布系统

### 画布引擎（Canvas Engine 模式）

`src/UI/Services/Canvas/`：

| 组件 | 职责 |
|------|------|
| `ICanvasEngine.cs` | 画布引擎统一接口（EngineName/GetControl/SetDataContext/SetPathCalculator/Cleanup） |
| `CanvasEngineManager.cs` | 引擎管理器 |
| `CanvasStateManager.cs` | 画布状态管理 |
| `CanvasConfig.cs` / `CanvasHelper.cs` | 配置与坐标工具 |
| `Engines/WorkflowCanvasEngine.cs` | 工作流画布引擎 |
| `Engines/TestCanvasEngine.cs` | 测试画布引擎 |

### 画布控件

`src/UI/Features/WorkflowUI/Canvas/WorkflowCanvasControl.xaml(.cs)`

- 纯画布控件，负责节点和连线的显示、拖拽、连接等交互
- 通过 Adapters 解耦节点显示逻辑
- 配合 `CanvasTemplateSelector.cs`（模板选择）与 `CanvasType.cs`

### 节点模型

`WorkflowNodeBase`（`src/Workflow/WorkflowNode.cs`，继承 ObservableObject）同时服务 UI 和序列化：

- **序列化字段**：Id, Name, DispName, ToolType, Parameters, ParamSettings, PositionX/Y, Width/Height, IsEnabled, LocalIndex/GlobalIndex
- **UI 专有属性**（`[JsonIgnore]`）：IsSelected, IsVisible, Status, Position, OutputCache, InputSource, LastResult, StyleConfig

### 节点样式系统（Adapters 模式）

`src/UI/Features/WorkflowUI/Canvas/Adapters/`（20 个适配器）：

```
NodeDisplayAdapterFactory（静态工厂，自动扫描 ICategoryDisplayAdapter）
  ├── 按 Category 分类：AIAnalysis / ControlFlow / Data / ImageSource / Location /
  │                      Logic / Measurement / MultiCollection / Output / Processing /
  │                      Recognition / VideoSource
  ├── 按 algorithmType 手动注册
  └── DefaultNodeDisplayAdapter（兜底）
```

接口：`INodeDisplayAdapter`（GetDisplayText/GetIcon/GetBackgroundColor/GetBorderColor）
样式配置：`src/UI/Shared/NodeStyleConfig.cs`

### 连接系统

`src/UI/Services/Connection/`：

| 组件 | 职责 |
|------|------|
| `ConnectionService.cs` | 连接 CRUD（IConnectionService 接口） |
| `ConnectionPathService.cs` | 路径计算调度 |
| `ConnectionPathCache.cs` | 连接路径缓存 |
| `ConnectionBatchUpdateManager.cs` | 批量更新 |

路径计算：`src/UI/Services/PathCalculators/`（BezierPathCalculator + PathCalculatorFactory）
连接模型：`WorkflowConnection`（`src/Workflow/Workflow.cs`，SourceNodeId → TargetNodeId）

### 交互系统（Handler 拆分）

`src/UI/Services/Interaction/`（15 个 Handler，从原 WorkflowCanvasControl 拆出）：

| Handler | 职责 |
|---------|------|
| `NodeDragHandler.cs` | 节点拖拽 |
| `ConnectionDragHandler.cs` | 连接拖拽 |
| `ConnectionCreator.cs` | 连接创建 |
| `ConnectionManager.cs` | 连接管理 |
| `DragDropHandler.cs` | 工具箱拖放到画布 |
| `BoxSelectionHandler.cs` | 框选 |
| `SelectionHandler.cs` | 选择管理 |
| `PortInteractionHandler.cs` / `PortInteractionHandler2.cs` | 端口交互 |
| `PortPositionService.cs` | 端口位置查询 |
| `NodeInteractionHandler.cs` | 节点通用交互 |
| `DefaultInputProvider.cs` / `IInputProvider.cs` | 输入提供 |

## RoI 编辑器（Region → RoI 迁移后）

`src/Plugin.SDK/UI/Controls/Roi/`：

### 核心文件

| 文件 | 职责 |
|------|------|
| `Views/RoiEditorControl.xaml(.cs)` | RoI 编辑器主控件（纯 UI 组件，不持久化） |
| `Views/RoiEditorManagerControl.xaml(.cs)` | RoI 管理器 |
| `ViewModels/RoiEditorViewModel.cs` | RoI 编辑器 ViewModel |
| `Models/RoiDefinition.cs` | RoI 定义基类（[JsonPolymorphic] + ShapeParameters/FixedRoi/ComputedRoi） |
| `Models/RoiData.cs` / `Models/RoiEditorSettings.cs` / `Models/RoiType.cs` | 数据模型 |
| `Logic/RoiResolver.cs` | RoI 解析 |
| `Logic/RoiEditorIntegration.cs` | 编辑器集成 |
| `Rendering/IRoiRenderer.cs` / `Rendering/RoiHitTester.cs` | 渲染与命中测试 |
| `Converters/RoiConverters.cs` | 转换器 |

### 支持的 RoI 类型

`RoiDefinition` 抽象类，`[JsonPolymorphic]` 多态序列化：

| 类型 | 类 | 说明 |
|------|-----|------|
| Shape | `ShapeParameters` | 用户绘制形状（持有 SDK Geometry 实例，Circle2d/Line2d/Annulus2d/RotatedRect2d/Polygon2d/Arc2d/Point2D） |
| Fixed | `FixedRoi` | 固定 RoI |
| Computed | `ComputedRoi` | 计算 RoI（绑定上游输出） |

RoiSourceMode 枚举：Drawing / Fixed / Computed

### 区域参数系统

- `RegionParameters` — 带区域的参数基类
- `RegionCollectionParameter` — 区域集合（支持常量/绑定模式）
- `RegionData` — 区域数据模型
- `RegionParameterMode` — InspectionRegion / MaskRegion

## 图像显示控件

### ImageControl

路径：`src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml`

功能：
- 图像显示和缩放
- 鼠标交互事件
- 视图变换（平移/缩放）
- RoI 叠加显示（叠加层机制）

### ImagePreviewControl

路径：`src/UI/Shell/Controls/Panels/ImagePreviewControl.xaml`

主界面右侧图像预览面板，集成 ImageControl。

## 参数面板系统

### 参数绑定控件（SDK 层）

`src/Plugin.SDK/UI/Controls/`：

| 控件 | 用途 |
|------|------|
| `ParamBinding.cs` | 参数绑定控件（继承 ParamBindingBase，树形数据源选择 + 类型过滤） |
| `ParamBindingBase.cs` | 绑定基类 |
| `ParamComboBox.cs` / `ParamTextBox.cs` / `ParamGroup.cs` | 参数表单控件 |
| `ImageSourceSelector.cs` | 图像源选择器 |
| `ConfigSetting.cs` | 通用参数配置控件 |
| `RangeSlider.cs` / `RangeInputControl.cs` | 范围滑块/输入 |
| `NumericUpDown.cs` | 数值上下调 |
| `ToggleSwitch.cs` | 开关 |
| `ColorPicker/` | 颜色选择器 |
| `LabeledControl.cs` | 带标签的控件容器 |

### 参数绑定模式

`ParamValue<T>` 统一管理值和绑定：

- `BindingType.Constant` — 固定值
- `BindingType.Binding` — 从其他节点输出动态获取
- `BindingType.NodeOutput` — 从节点输出端口获取

## 调试窗口系统

| 组件 | 职责 |
|------|------|
| `src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml` | 标准调试窗口 |
| `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 调试控件基类（路由命令 Execute/Confirm/ContinuousExecute/ResetParameters + ToolExecutionCompletedEvent） |
| `src/UI/Features/WorkflowUI/ToolDebugWindowFactory.cs` | 调试窗口工厂 |
| `CustomDebugWindow.cs` | 自定义无边框窗口 |

`DebugWindowStyle` 枚举：None（无窗口）、Default（标准）、Custom（自定义）

## 性能优化

### 关键服务

`src/UI/Services/Rendering/`：

| 服务 | 职责 |
|------|------|
| `NodeRenderScheduler.cs` | 节点渲染调度 |
| `NodeUIPool.cs` | 节点 UI 对象池 |
| `SpatialIndex.cs` | 空间索引（命中测试） |
| `CanvasRenderer.cs` | 画布渲染 |
| `CachedNodeConverters.cs` / `VisualHelper.cs` / `GeometryOptimizer.cs` | 渲染辅助 |

### 虚拟化

- 节点虚拟化：`IsVisible` 属性控制渲染
- 空间索引：`SpatialIndex` 加速命中测试
- 渲染调度：`NodeRenderScheduler` 优先渲染可见节点

## 日志规范

ViewModel 层使用：
```csharp
LogInfo($"开始处理: {imagePath}, 尺寸: {width}x{height}");
LogSuccess("执行完成");
LogWarning("参数超出范围");
LogError("执行失败", exception);
```

禁止：`Debug.WriteLine()`, `Console.WriteLine()`, `Trace.WriteLine()`

## 命名规范速查

| 类型 | 格式 | 示例 |
|------|------|------|
| 类名 | PascalCase 名词 | `WorkflowEngine` |
| 方法 | PascalCase 动词 | `ProcessImage()` |
| 属性 | PascalCase | `IsEnabled` |
| 私有字段 | _camelCase | `_threshold` |
| 布尔属性 | Is/Has/Can 前缀 | `IsSelected` |
| 事件 | 过去式 | `Loaded`, `Completed` |
| 命令 | 动词+Command | `SaveSolutionCommand` |

## 代码锚点

### 工作流引擎与 UI（2026-06 重构后路径）

| 类 | 路径 | 用途 |
|---|---|---|
| `WorkflowCanvasControl` | `src/UI/Features/WorkflowUI/Canvas/WorkflowCanvasControl.xaml` | 画布主控件 |
| `NodeDisplayAdapterFactory` | `src/UI/Features/WorkflowUI/Canvas/Adapters/NodeDisplayAdapterFactory.cs` | 节点样式适配器工厂（Category 自动扫描） |
| `WorkflowNodeBase` | `src/Workflow/WorkflowNode.cs` | 节点模型（UI+序列化） |
| `WorkflowConnection` | `src/Workflow/Workflow.cs` | 连接模型 |
| `IConnectionService` | `src/UI/Services/Connection/ConnectionService.cs` | 连接 CRUD 接口 |
| `BezierPathCalculator` | `src/UI/Services/PathCalculators/BezierPathCalculator.cs` | 贝塞尔曲线路径计算 |
| `WorkflowTabViewModel` | `src/UI/Features/WorkflowUI/WorkflowTabViewModel.cs` | 工作流 Tab VM |
| `MainWindowViewModel` | `src/UI/Shell/Windows/MainWindowViewModel.cs` | 主窗口 VM |

### Plugin SDK 控件（UI 开发者用）

| 类 | 路径 | UI 开发者视角 |
|---|---|---|
| `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | 调试控件基类（继承后实现 ExecuteTool/CanExecuteTool） |
| `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | 图像预览 + RoI 叠加 |
| `ParamBinding` | `src/Plugin.SDK/UI/Controls/ParamBinding.cs` | 参数绑定（树形数据源 + 类型过滤） |
| `ConfigSetting` | `src/Plugin.SDK/UI/Controls/ConfigSetting.cs` | 参数面板核心控件 |
| `RoiEditorControl` | `src/Plugin.SDK/UI/Controls/Roi/Views/RoiEditorControl.xaml.cs` | RoI 编辑器架构 |
| `DefaultDebugWindow` | `src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml` | 调试窗口体系 |

### 画布引擎与交互（新增）

| 类 | 路径 | 用途 |
|---|---|---|
| `ICanvasEngine` | `src/UI/Services/Canvas/ICanvasEngine.cs` | 画布引擎接口 |
| `WorkflowCanvasEngine` | `src/UI/Services/Canvas/Engines/WorkflowCanvasEngine.cs` | 工作流画布引擎 |
| `NodeDragHandler` | `src/UI/Services/Interaction/NodeDragHandler.cs` | 节点拖拽 |
| `ConnectionDragHandler` | `src/UI/Services/Interaction/ConnectionDragHandler.cs` | 连接拖拽 |
