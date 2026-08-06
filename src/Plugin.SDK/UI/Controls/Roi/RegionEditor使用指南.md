# Region Editor 使用指南

## 概述

Region Editor是一个数据驱动的区域编辑控件，支持绘制模式和订阅模式两种输入方式。该控件采用分层架构设计，职责分离清晰。

## 架构设计

### 1. 数据层 (Models)

#### RegionData - 顶层容器
```csharp
public class RegionData
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public RegionDefinition Definition { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsVisible { get; set; }
    public bool IsEditable { get; set; }
    public uint DisplayColor { get; set; }
    public double DisplayOpacity { get; set; }
}
```

#### RegionDefinition - 区域定义

**ShapeDefinition** - 绘制形状
- 支持形状：Point, Line, Circle, Rectangle, RotatedRectangle, Polygon, Annulus, Arc
- 几何参数：CenterX, CenterY, Width, Height, Angle, Radius等

**FixedRegion** - 区域订阅
- 订阅其他节点的区域输出
- 属性：SourceNodeId, OutputName, RegionIndex

**ComputedRegion** - 参数订阅
- 每个参数可单独绑定
- 支持两种参数源：Constant, NodeOutput

#### ParameterSource - 参数源

1. **ConstantSource** - 常量值
2. **NodeOutputSource** - 节点输出
   ```csharp
   new NodeOutputSource("nodeId", "OutputName", index: 0, propertyPath: "Center.X")
   ```

### 2. 逻辑层 (Logic)

#### RegionResolver - 区域解析器
将抽象区域数据转换为几何参数：
```csharp
var resolver = new RegionResolver(context);
var resolved = resolver.Resolve(regionData);

if (resolved.IsValid)
{
    // 使用 resolved.CenterX, resolved.CenterY 等几何参数
}
```

### 3. ViewModel层 (ViewModels)

#### RegionEditorViewModel
提供MVVM绑定支持：
- Regions: 区域列表
- SelectedRegion: 当前选中区域
- CurrentMode: 当前模式（Drawing/SubscribeByRegion/SubscribeByParameter）
- 命令：AddRegionCommand, RemoveRegionCommand等

### 4. UI层 (Views)

#### RegionEditorControl
完整的区域编辑控件，包含：
- 工具栏：模式切换、绘制工具、操作按钮
- 图像显示：继承自ImageControl
- 参数面板：RegionInfoPanel

## 使用示例

### 1. 基本使用

```xaml
<ctrl:RegionEditorControl x:Name="RegionEditor"/>
```

```csharp
// 设置区域数据
var regions = new List<RegionData>
{
    RegionData.CreateDrawingRegion("矩形", ShapeType.Rectangle)
};
RegionEditor.SetRegions(regions);

// 获取结果
var selected = RegionEditor.GetSelectedRegion();
var allResolved = RegionEditor.ResolveAllRegions();
```

### 2. 创建绘制模式区域

```csharp
var region = RegionData.CreateDrawingRegion("检测区域", ShapeType.RotatedRectangle);
if (region.Definition is ShapeDefinition def)
{
    def.CenterX = 100;
    def.CenterY = 100;
    def.Width = 200;
    def.Height = 100;
    def.Angle = 45; // 数学角度系统：逆时针为正
}
```

### 3. 创建区域订阅

```csharp
// 订阅节点输出
var region = RegionData.CreateSubscribedRegion(
    name: "订阅区域",
    nodeId: "FindCircle_001",
    outputName: "OutputRegion",
    index: 0
);
```

### 4. 创建参数订阅

```csharp
var region = RegionData.CreateComputedRegion("计算区域", ShapeType.Rectangle);
if (region.Definition is ComputedRegion def)
{
    // 中心点订阅到节点输出
    def.SetParameterBinding("CenterX", 
        new NodeOutputSource("node_001", "CenterX"));
    def.SetParameterBinding("CenterY", 
        new NodeOutputSource("node_001", "CenterY"));
    
    // 尺寸使用常量
    def.SetParameterBinding("Width", 
        new ConstantSource(150.0));
    def.SetParameterBinding("Height", 
        new ConstantSource(100.0));
}
```

### 5. 实现IParameterContext

```csharp
public class MyParameterContext : IParameterContext
{
    private readonly Dictionary<string, Dictionary<string, object>> _nodeOutputs = new();

    public object? GetNodeOutputValue(string nodeId, string outputName, int? index, string? propertyPath)
    {
        if (!_nodeOutputs.TryGetValue(nodeId, out var outputs))
            return null;
        
        if (!outputs.TryGetValue(outputName, out var value))
            return null;
        
        // 处理索引和属性路径
        // ...
        return value;
    }
}
```

## 特性

### 1. 数学角度系统
- 角度范围：[-180°, 180°]
- 逆时针为正
- 0°表示宽度方向与X轴平行

### 2. 三种定义模式
- **Drawing**: 用户手动绘制
- **SubscribeByRegion**: 按区域类型订阅
- **SubscribeByParameter**: 按参数订阅

### 3. 数据驱动
- 纯POCO模型，易于序列化
- 支持撤销/重做
- 支持验证

### 4. 扩展性
- 可添加新的形状类型
- 可扩展参数源类型
- 可自定义解析逻辑

## 与ROI编辑器的关系

Region Editor是新设计的区域编辑系统，与现有的ROI编辑器的关系：

1. **独立系统**：Region Editor是完全独立的实现，不依赖ROI编辑器
2. **可共存**：两个系统可以并存使用
3. **迁移路径**：后期可以考虑将ROI编辑器迁移到Region Editor架构

## 后续优化

1. **绘制交互**：实现鼠标绘制各种形状
2. **可视化绑定**：图形化显示参数绑定关系
3. **性能优化**：大量区域时的渲染优化
4. **更多形状**：支持多边形、圆环、弧形等
5. **表达式编辑器**：可视化的表达式编辑和调试
