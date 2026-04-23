---
name: wpf-vision-dev
description: WPF视觉开发技能。用于WPF/MVVM界面开发、工作流画布编辑器、节点系统、ROI编辑器、图像显示控件开发。触发场景：WPF开发、画布交互、节点渲染、连接绘制、ROI编辑、图像预览。
---

# WPF 视觉开发技能

## 触发场景

- WPF/MVVM 界面开发
- 工作流画布编辑器交互
- 节点渲染和样式
- 连接路径绘制
- ROI 编辑器开发
- 图像预览控件
- 参数面板绑定

## 核心架构

### MVVM 分层

```
View (XAML) ← Binding → ViewModel ← 调用 → Service/Model
```

- **ViewModelBase** (`src/UI/ViewModels/ViewModelBase.cs`) — UI层基类
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

### 核心文件

| 文件 | 职责 |
|------|------|
| `WorkflowCanvasControl.xaml` | 画布主控件（140KB，核心交互） |
| `CanvasHelper.cs` | 边界限制、坐标转换 |
| `CanvasStateManager.cs` | 画布状态管理 |
| `BezierPathCalculator.cs` | 贝塞尔曲线路径计算 |

### 节点模型

`WorkflowNodeBase`（`src/Workflow/WorkflowNode.cs`）同时服务UI和序列化：

- **序列化字段**：Id, Name, DispName, ToolType, Parameters, PositionX/Y, Width/Height
- **UI专有属性**（`[JsonIgnore]`）：IsSelected, IsVisible, Status, Position, OutputCache

### 节点样式系统

节点显示通过 `NodeDisplayAdapter` 模式实现：

```
NodeDisplayAdapterFactory → 根据NodeType选择Adapter
  ├── DefaultNodeDisplayAdapter     — 标准算法节点
  ├── ImageSourceNodeDisplayAdapter — 图像源节点
  ├── ProcessingNodeDisplayAdapter  — 处理节点
  └── ...（按NodeType扩展）
```

样式配置：`NodeStyleConfig.cs` / `NodeStyleResources.xaml`

### 连接系统

| 组件 | 职责 |
|------|------|
| `ConnectionService.cs` | 连接CRUD |
| `ConnectionPathService.cs` | 路径计算调度 |
| `BezierPathCalculator.cs` | 贝塞尔曲线算法 |
| `ConnectionDragHandler.cs` | 拖拽创建连接 |
| `PortInteractionHandler.cs` | 端口交互 |

连接模型：`WorkflowConnection`（SourceNodeId → TargetNodeId）

### 交互系统

| Handler | 职责 |
|---------|------|
| `NodeDragHandler.cs` | 节点拖拽 |
| `DragDropHandler.cs` | 工具箱拖放到画布 |
| `BoxSelectionHandler.cs` | 框选 |
| `SelectionHandler.cs` | 选择管理 |
| `PortInteractionHandler.cs` | 端口悬停/点击 |

## ROI 编辑器

### 核心文件

| 文件 | 职责 |
|------|------|
| `RegionEditorControl.xaml(.cs)` | ROI编辑器主控件 |
| `RegionEditorViewModel.cs` | ROI编辑器ViewModel |
| `WpfShapeRenderer.cs` | 形状渲染（虚线支持） |
| `HandleRenderer.cs` | 控制柄渲染 |
| `ShapeType.cs` | 支持的形状类型 |

### 支持的ROI类型

`IRoi` 接口 → `RoiBase` 抽象类：

| 类型 | 类 | 说明 |
|------|-----|------|
| FullImage | `FullImageRoi` | 全图 |
| Rectangle | `RectangleRoi` | 矩形 |
| RotatedRectangle | `RectangleRoi` | 旋转矩形 |
| Circle | `CircleRoi` | 圆形 |
| Annulus | `AnnulusRoi` | 圆环 |
| Polygon | `PolygonRoi` | 多边形 |

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
- ROI叠加显示

### ImagePreviewControl

路径：`src/UI/Views/Controls/Panels/ImagePreviewControl.xaml`

主界面右侧图像预览面板，集成 ImageControl。

## 参数面板系统

### 参数绑定控件（SDK层）

| 控件 | 用途 |
|------|------|
| `ParamBinding.cs` | 参数绑定容器（常量/绑定切换） |
| `ImageSourceSelector.cs` | 图像源选择器 |
| `ConfigSetting.cs` | 通用参数配置控件 |
| `RangeSlider.cs` | 范围滑块 |
| `NumericUpDown.cs` | 数值上下调 |
| `ToggleSwitch.cs` | 开关 |
| `ColorPicker.cs` | 颜色选择器 |
| `LabeledControl.cs` | 带标签的控件容器 |

### 参数绑定模式

`ParamValue<T>` 统一管理值和绑定：

- `BindingType.Constant` — 固定值
- `BindingType.Binding` — 从其他节点输出动态获取
- `BindingType.NodeOutput` — 从节点输出端口获取

## 调试窗口系统

| 组件 | 职责 |
|------|------|
| `DefaultDebugWindow.xaml` | 标准调试窗口 |
| `CustomDebugWindow.cs` | 自定义无边框窗口 |
| `ToolDebugControlBase.cs` | 调试控件基类 |
| `ToolDebugWindowFactory.cs` | 调试窗口工厂 |

`DebugWindowStyle` 枚举：None（无窗口）、Default（标准）、Custom（自定义）

## 性能优化

### 关键服务

| 服务 | 职责 |
|------|------|
| `BatchUIUpdater.cs` | 批量UI更新 |
| `NodeRenderScheduler.cs` | 节点渲染调度 |
| `NodeUIPool.cs` | 节点UI对象池 |
| `SpatialIndex.cs` | 空间索引（命中测试） |
| `ImageConverterPool.cs` | Mat→BitmapSource转换池 |
| `ConnectionPathCache.cs` | 连接路径缓存 |

### 虚拟化

- 节点虚拟化：`IsVisible` 属性控制渲染
- 空间索引：`SpatialIndex` 加速命中测试
- 渲染调度：`NodeRenderScheduler` 优先渲染可见节点

## 日志规范

ViewModel层使用：
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

### 工作流引擎

| 类 | 路径 | 用途 |
|---|---|---|
| `WorkflowCanvasControl` | `src/UI/Views/Controls/WorkflowCanvasControl.xaml` | 画布主控件 |
| `WorkflowNodeBase` | `src/Workflow/WorkflowNode.cs` | 节点模型（UI+序列化） |
| `NodeDisplayAdapterFactory` | `src/UI/Views/Controls/Nodes/NodeDisplayAdapterFactory.cs` | 节点样式适配器工厂 |
| `ConnectionService` | `src/Workflow/Services/ConnectionService.cs` | 连接 CRUD |
| `BezierPathCalculator` | `src/Workflow/Services/BezierPathCalculator.cs` | 贝塞尔曲线路径计算 |

### Plugin SDK 控件（UI 开发者用）

| 类 | 路径 | 行号 | UI 开发者视角 |
|---|---|---|---|
| `ToolDebugControlBase` | `src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs` | L122 | 理解调试窗口体系 |
| `ImageControl` | `src/Plugin.SDK/UI/Controls/ImageControl/ImageControl.xaml.cs` | L20 | 画布图像预览集成 |
| `ParamBinding` | `src/Plugin.SDK/UI/Controls/ParamBinding.cs` | L24 | 参数面板绑定机制 |
| `ConfigSetting` | `src/Plugin.SDK/UI/Controls/ConfigSetting.cs` | L35 | 参数面板核心控件 |
| `RegionEditorControl` | `src/Plugin.SDK/UI/Controls/Region/Views/RegionEditorControl.xaml.cs` | L27 | ROI 编辑器架构 |
| `DefaultDebugWindow` | `src/Plugin.SDK/UI/Windows/DefaultDebugWindow.xaml.cs` | L35 | 调试窗口体系 |
