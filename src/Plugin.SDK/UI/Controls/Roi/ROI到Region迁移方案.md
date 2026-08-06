# ROI编辑器到Region编辑器迁移方案

## 一、迁移目标

将SDK下ROI编辑器的核心功能（形状样式、编辑手柄、渲染逻辑）迁移到Region编辑器，保持Region编辑器的UI框架和事件控制架构。

## 二、核心原则

1. **保留Region编辑器的MVVM架构**
2. **保留Region编辑器的UI布局和事件控制**
3. **以ROI编辑器的形状渲染和编辑手柄为准**
4. **保持Region编辑器的数据模型（RegionData）**

## 三、已完成的迁移

### 3.1 形状渲染器
- **文件**: `Rendering/ShapeRenderer.cs`
- **功能**:
  - 创建矩形、圆形、旋转矩形、直线形状
  - 参考ROI编辑器的`CreateROIShape`方法（1820-1896行）
  - 统一的颜色和样式系统
  - 支持预览和选中状态

### 3.2 手柄渲染器
- **文件**: `Rendering/HandleRenderer.cs`
- **功能**:
  - 定义`EditHandle`类和`HandleType`枚举
  - 创建各类形状的编辑手柄（矩形8个、圆形4个、旋转矩形9个、直线2个）
  - 参考ROI编辑器的手柄创建方法（1071-1237行）
  - 绘制手柄到Canvas
  - 命中测试逻辑

### 3.3 旋转矩形辅助类
- **文件**: `Rendering/RotatedRectangleHelper.cs`
- **功能**:
  - 角度规范化（数学角度系统）
  - 计算旋转矩形四个角点
  - 计算方向箭头
  - 计算旋转手柄位置
  - 绘制方向箭头和连接线
  - 参考ROI.cs中的几何计算方法（317-476行）

## 四、待完成的迁移步骤

### 阶段二：集成到RegionEditorControl

#### 4.1 修改RegionEditorControl.xaml.cs

**需要添加的内容**：

1. **引用新的渲染器**
```csharp
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering;
```

2. **替换形状绘制逻辑**
```csharp
// 原有代码可能需要替换
// 使用ShapeRenderer.CreateShape创建形状
var shape = ShapeRenderer.CreateShape(shapeDefinition, isSelected, isPreview);
```

3. **替换手柄绘制逻辑**
```csharp
// 根据形状类型创建手柄
EditHandle[] handles = null;
switch (shape.ShapeType)
{
    case ShapeType.Rectangle:
        var bounds = GetBounds(shape);
        handles = HandleRenderer.CreateRectangleHandles(bounds);
        break;
    case ShapeType.Circle:
        var center = GetCenter(shape);
        var radius = shape.Radius ?? 50;
        handles = HandleRenderer.CreateCircleHandles(center, radius);
        break;
    case ShapeType.RotatedRectangle:
        var corners = RotatedRectangleHelper.GetCorners(...);
        var bottomCenter = ...;
        handles = HandleRenderer.CreateRotatedRectangleHandles(...);
        break;
    case ShapeType.Line:
        handles = HandleRenderer.CreateLineHandles(...);
        break;
}

// 绘制手柄
HandleRenderer.DrawHandles(overlayCanvas, handles, shape.ShapeType);
```

4. **添加旋转矩形的方向箭头绘制**
```csharp
if (shape.ShapeType == ShapeType.RotatedRectangle)
{
    var center = GetCenter(shape);
    var arrow = RotatedRectangleHelper.GetDirectionArrow(center, height, rotation);
    RotatedRectangleHelper.DrawDirectionArrow(overlayCanvas, arrow.Start, arrow.End, strokeColor);
}
```

#### 4.2 修改ShapeDefinition模型

**需要确保ShapeDefinition包含以下属性**：
- `X, Y`: 中心位置
- `Width, Height`: 尺寸
- `Radius`: 半径（圆形）
- `Rotation`: 旋转角度（旋转矩形）
- `EndX, EndY`: 直线终点
- `FillColor, StrokeColor`: 颜色
- `StrokeThickness`: 边框厚度
- `Opacity`: 透明度

#### 4.3 更新编辑操作逻辑

**参考ROI编辑器的编辑操作**：

1. **手柄拖动处理**（参考ROI编辑器1461-1816行）
   - `HandleResize`: 处理大小调整
   - `HandleRotate`: 处理旋转操作
   - `HandleCircleResize`: 处理圆形半径调整
   - `HandleRotatedRectangleResize`: 处理旋转矩形调整
   - `HandleLineResize`: 处理直线端点调整

2. **命中测试**
   - 使用`HandleRenderer.HitTestHandle`检测手柄点击
   - 使用形状的`Contains`方法检测区域点击

### 阶段三：UI样式对齐

#### 4.4 形状样式配置

**在RegionEditorSettings中添加**：
```csharp
// 参考ROIEditorSettings.cs
public Color DefaultFillColor => Color.FromArgb(40, 255, 0, 0);
public Color DefaultStrokeColor => Colors.Red;
public double DefaultOpacity => 0.3;
public Color SelectedFillColor => Color.FromArgb(40, 0, 0, 255);
public Color SelectedStrokeColor => Colors.Blue;
```

#### 4.5 手柄样式配置

```csharp
// 手柄大小（参考ROI编辑器的_handleSize = 12）
public double HandleSize => 12;

// 手柄命中容差（参考ROI编辑器的容差8）
public double HitTolerance => 8;
```

### 阶段四：功能验证

#### 4.6 测试清单

1. **形状创建**
   - [ ] 矩形创建和编辑
   - [ ] 圆形创建和编辑
   - [ ] 旋转矩形创建和编辑
   - [ ] 直线创建和编辑

2. **编辑手柄**
   - [ ] 矩形8个手柄功能正常
   - [ ] 圆形4个手柄功能正常
   - [ ] 旋转矩形9个手柄功能正常（8个缩放+1个旋转）
   - [ ] 直线2个端点手柄功能正常

3. **旋转矩形特有功能**
   - [ ] 方向箭头显示正确
   - [ ] 旋转手柄和连接线显示正确
   - [ ] 旋转操作流畅无跳变

4. **视觉效果**
   - [ ] 颜色与ROI编辑器一致
   - [ ] 线宽与ROI编辑器一致
   - [ ] 透明度与ROI编辑器一致
   - [ ] 预览状态显示正确

## 五、关键代码参考

### 5.1 ROI编辑器关键方法索引

| 方法名 | 行号 | 功能 |
|--------|------|------|
| `CreateROIShape` | 1820-1896 | 创建形状 |
| `DrawROILabel` | 937-978 | 绘制标签 |
| `UpdateROIOverlay` | 880-932 | 更新叠加层 |
| `CreateRectangleHandles` | 1071-1097 | 创建矩形手柄 |
| `CreateCircleHandles` | 1102-1126 | 创建圆形手柄 |
| `CreateRotatedRectangleHandles` | 1131-1213 | 创建旋转矩形手柄 |
| `CreateLineHandles` | 1218-1237 | 创建直线手柄 |
| `DrawEditHandles` | 1281-1347 | 绘制手柄 |
| `DrawDirectionArrow` | 1354-1425 | 绘制方向箭头 |
| `HandleResize` | 1461-1700 | 处理调整大小 |
| `HandleRotate` | 1792-1816 | 处理旋转 |

### 5.2 ROI数据模型关键方法索引（ROI.cs）

| 方法名 | 行号 | 功能 |
|--------|------|------|
| `GetBounds()` | 171-203 | 获取边界矩形 |
| `Contains()` | 208-243 | 点包含检测 |
| `GetCorners()` | 345-381 | 获取旋转矩形角点 |
| `GetDirectionArrow()` | 390-411 | 获取方向箭头 |
| `GetRotationHandlePosition()` | 420-446 | 获取旋转手柄位置 |
| `GetRotatedBoundingBox()` | 452-474 | 获取包围盒 |
| `NormalizeAngle()` | 325-336 | 角度规范化 |

## 六、注意事项

1. **角度系统**：ROI编辑器使用图像坐标系角度（顺时针为正，0°向下），需要确保Region编辑器也使用相同的角度系统。

2. **坐标系转换**：WPF的RotateTransform使用逆时针为正，0°向右，需要进行转换：`wpfAngle = -imageAngle`。

3. **手柄大小**：ROI编辑器使用12像素手柄，Region编辑器应保持一致。

4. **颜色值**：ROI编辑器默认填充色为红色半透明`Color.FromArgb(40, 255, 0, 0)`，选中时为蓝色半透明`Color.FromArgb(40, 0, 0, 255)`。

5. **旋转矩形方向箭头**：旋转矩形必须显示方向箭头，指示矩形的"下方"方向，这是用户体验的关键。

## 七、实施优先级

**P0（核心功能）**：
- 集成ShapeRenderer
- 集成HandleRenderer
- 修改形状绘制逻辑

**P1（关键功能）**：
- 集成RotatedRectangleHelper
- 添加方向箭头绘制
- 添加旋转手柄和连接线

**P2（增强功能）**：
- 优化编辑操作逻辑
- 添加命中测试
- 完善视觉样式

**P3（优化项）**：
- 性能优化
- 边界处理
- 辅助功能

## 八、后续扩展

迁移完成后，可以考虑：
1. 支持多边形（ROI编辑器已预留Points集合）
2. 支持撤销/重做（ROI编辑器有EditHistory）
3. 支持快捷键（ROI编辑器支持R/C/O/L/V键）
4. 支持对齐和吸附功能
5. 支持图层顺序调整
