# ROI编辑器功能集成实施指南

## 一、准备工作

### 1.1 验证新组件已创建

确保以下文件已创建：
- ✅ `Rendering/ShapeRenderer.cs` - 形状渲染器
- ✅ `Rendering/HandleRenderer.cs` - 手柄渲染器
- ✅ `Rendering/RotatedRectangleHelper.cs` - 旋转矩形辅助类
- ✅ `Models/RegionDefinition.cs` - 已更新，添加了样式属性

### 1.2 添加引用

在 `RegionEditorControl.xaml.cs` 中添加引用：

```csharp
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering;
using System.Windows.Media;
```

## 二、集成ShapeRenderer

### 2.1 替换形状创建逻辑

找到 `RegionEditorControl.xaml.cs` 中创建形状的代码，替换为使用 `ShapeRenderer`：

```csharp
// 原有代码（示例）
// var rect = new Rectangle { ... };

// 新代码
var shape = ShapeRenderer.CreateShape(shapeDefinition, isSelected, isPreview);
if (shape != null)
{
    // 根据shape.Tag获取位置信息
    if (shape.Tag is dynamic tag && tag.X is double x && tag.Y is double y)
    {
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
    }
    else if (shapeDefinition.ShapeType == ShapeType.Line)
    {
        // 直线不需要Canvas定位
        // 直接添加即可
    }

    overlayCanvas.Children.Add(shape);
}
```

### 2.2 颜色转换辅助方法（可选）

如果需要在其他地方使用颜色转换，可以在 `RegionEditorControl.xaml.cs` 中添加：

```csharp
private static Color ConvertArgbToColor(uint argb)
{
    var a = (byte)((argb >> 24) & 0xFF);
    var r = (byte)((argb >> 16) & 0xFF);
    var g = (byte)((argb >> 8) & 0xFF);
    var b = (byte)(argb & 0xFF);
    return Color.FromArgb(a, r, g, b);
}
```

## 三、集成HandleRenderer

### 3.1 添加手柄管理字段

```csharp
private EditHandle[] _currentHandles = null;
private HandleType _activeHandleType = HandleType.None;
```

### 3.2 创建手柄

在绘制选中区域时创建手柄：

```csharp
private void CreateHandlesForShape(ShapeDefinition shape)
{
    if (shape == null) return;

    switch (shape.ShapeType)
    {
        case ShapeType.Rectangle:
            var bounds = new Rect(
                shape.CenterX - shape.Width / 2,
                shape.CenterY - shape.Height / 2,
                shape.Width,
                shape.Height
            );
            _currentHandles = HandleRenderer.CreateRectangleHandles(bounds);
            break;

        case ShapeType.Circle:
            var center = new Point(shape.CenterX, shape.CenterY);
            _currentHandles = HandleRenderer.CreateCircleHandles(center, shape.Radius);
            break;

        case ShapeType.RotatedRectangle:
            var rotatedCenter = new Point(shape.CenterX, shape.CenterY);
            var corners = RotatedRectangleHelper.GetCorners(
                rotatedCenter,
                shape.Width,
                shape.Height,
                shape.Angle
            );
            var bottomCenter = CalculateRotatedRectBottomCenter(rotatedCenter, shape.Height, shape.Angle);
            _currentHandles = HandleRenderer.CreateRotatedRectangleHandles(
                corners,
                shape.Angle,
                bottomCenter
            );
            break;

        case ShapeType.Line:
            var startPoint = new Point(shape.StartX, shape.StartY);
            var endPoint = new Point(shape.EndX, shape.EndY);
            _currentHandles = HandleRenderer.CreateLineHandles(startPoint, endPoint);
            break;
    }
}

private Point CalculateRotatedRectBottomCenter(Point center, double height, double angle)
{
    var angleRad = angle * Math.PI / 180;
    var sin = Math.Sin(angleRad);
    var cos = Math.Cos(angleRad);

    return new Point(
        center.X + (height / 2) * sin,
        center.Y + (height / 2) * cos
    );
}
```

### 3.3 绘制手柄

在更新OverlayCanvas时绘制手柄：

```csharp
private void UpdateOverlayCanvas()
{
    // ... 清除和绘制形状 ...

    // 绘制选中区域的手柄
    if (_selectedRegion?.Definition is ShapeDefinition shape && !isPreview)
    {
        CreateHandlesForShape(shape);

        if (_currentHandles != null && _currentHandles.Length > 0)
        {
            HandleRenderer.DrawHandles(overlayCanvas, _currentHandles, shape.ShapeType);

            // 旋转矩形的特殊处理（绘制方向箭头和连接线）
            if (shape.ShapeType == ShapeType.RotatedRectangle)
            {
                DrawRotatedRectangleHelpers(shape);
            }
        }
    }
}

private void DrawRotatedRectangleHelpers(ShapeDefinition shape)
{
    if (shape.ShapeType != ShapeType.RotatedRectangle) return;

    var center = new Point(shape.CenterX, shape.CenterY);
    var height = shape.Height;
    var rotation = shape.Angle ?? 0;

    // 计算旋转手柄位置
    var rotateHandlePos = RotatedRectangleHelper.GetRotationHandlePosition(center, height, rotation);

    // 绘制方向箭头和连接线
    RotatedRectangleHelper.DrawRotateHandleLine(
        overlayCanvas,
        center,
        height,
        rotation,
        rotateHandlePos
    );
}
```

## 四、事件处理

### 4.1 鼠标命中测试

```csharp
private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
{
    var mousePos = e.GetPosition(overlayCanvas);

    // 1. 先测试手柄
    if (_currentHandles != null)
    {
        var hitHandle = HandleRenderer.HitTestHandle(mousePos, _currentHandles, 8);
        if (hitHandle != HandleType.None)
        {
            _activeHandleType = hitHandle;
            BeginHandleDrag(e);
            return;
        }
    }

    // 2. 测试形状命中
    // ... 现有的形状命中逻辑 ...
}
```

### 4.2 手柄拖动处理

```csharp
private void BeginHandleDrag(MouseButtonEventArgs e)
{
    _isDraggingHandle = true;
    _handleDragStartPoint = e.GetPosition(overlayCanvas);

    // 保存原始状态
    _originalShapeDefinition = (_selectedRegion?.Definition as ShapeDefinition)?.Clone();

    CaptureMouse();
}

private void OnOverlayMouseMove(object sender, MouseEventArgs e)
{
    if (!_isDraggingHandle || _activeHandleType == HandleType.None) return;

    var currentPos = e.GetPosition(overlayCanvas);

    if (_selectedRegion?.Definition is ShapeDefinition shape)
    {
        switch (_activeHandleType)
        {
            case HandleType.Rotate:
                HandleRotate(shape, currentPos);
                break;
            case HandleType.LineStart:
            case HandleType.LineEnd:
                HandleLineEndpoint(shape, currentPos);
                break;
            case HandleType.Top:
            case HandleType.Bottom:
            case HandleType.Left:
            case HandleType.Right:
            case HandleType.TopLeft:
            case HandleType.TopRight:
            case HandleType.BottomLeft:
            case HandleType.BottomRight:
                HandleResize(shape, currentPos);
                break;
            case HandleType.Top: // 圆形
            case HandleType.Bottom: // 圆形
            case HandleType.Left: // 圆形
            case HandleType.Right: // 圆形
                if (shape.ShapeType == ShapeType.Circle)
                {
                    HandleCircleResize(shape, currentPos);
                }
                else
                {
                    HandleResize(shape, currentPos);
                }
                break;
        }

        UpdateOverlayCanvas();
    }
}

private void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
{
    if (_isDraggingHandle)
    {
        EndHandleDrag();
    }
}

private void EndHandleDrag()
{
    _isDraggingHandle = false;
    _activeHandleType = HandleType.None;
    _originalShapeDefinition = null;
    ReleaseMouseCapture();

    // 触发区域变更事件
    RegionDataChanged?.Invoke(this, _selectedRegion);
}
```

## 五、编辑操作实现

### 5.1 旋转操作（参考ROI编辑器1792-1816行）

```csharp
private void HandleRotate(ShapeDefinition shape, Point currentPos)
{
    if (shape.ShapeType != ShapeType.RotatedRectangle) return;

    var center = new Point(shape.CenterX, shape.CenterY);

    // 保存旋转开始时的状态（第一次调用时）
    if (_rotateStartAngle == null)
    {
        _rotateStartAngle = shape.Angle ?? 0;
        _rotateStartMouseAngle = CalculateMouseAngle(center, _handleDragStartPoint);
    }

    // 计算当前鼠标角度
    var currentMouseAngle = CalculateMouseAngle(center, currentPos);

    // 计算角度增量
    var deltaAngle = currentMouseAngle - _rotateStartMouseAngle.Value;

    // 应用角度增量
    shape.Angle = RotatedRectangleHelper.NormalizeAngle(_rotateStartAngle.Value + deltaAngle);
}

private double CalculateMouseAngle(Point center, Point mousePos)
{
    // 计算鼠标相对于中心的数学角度
    var mathAngle = Math.Atan2(mousePos.Y - center.Y, mousePos.X - center.X) * 180 / Math.PI;

    // 转换为图像坐标系角度（顺时针为正，0°向下）
    // 公式：imageAngle = -mathAngle + 90
    return -mathAngle + 90;
}
```

### 5.2 矩形/旋转矩形调整（参考ROI编辑器1461-1700行）

```csharp
private void HandleResize(ShapeDefinition shape, Point currentPos)
{
    var delta = currentPos - _handleDragStartPoint;

    if (shape.ShapeType == ShapeType.Rectangle)
    {
        HandleRectangleResize(shape, delta);
    }
    else if (shape.ShapeType == ShapeType.RotatedRectangle)
    {
        HandleRotatedRectangleResize(shape, currentPos, delta);
    }
}

private void HandleRectangleResize(ShapeDefinition shape, Vector delta)
{
    // 恢复到原始状态
    var original = _originalShapeDefinition;
    if (original == null) return;

    switch (_activeHandleType)
    {
        case HandleType.TopLeft:
            ResizeRectangleFromCorner(shape, original, delta, true, true);
            break;
        case HandleType.TopRight:
            ResizeRectangleFromCorner(shape, original, delta, false, true);
            break;
        case HandleType.BottomLeft:
            ResizeRectangleFromCorner(shape, original, delta, true, false);
            break;
        case HandleType.BottomRight:
            ResizeRectangleFromCorner(shape, original, delta, false, false);
            break;
        case HandleType.Top:
            ResizeRectangleFromEdge(shape, original, delta, true, true);
            break;
        case HandleType.Bottom:
            ResizeRectangleFromEdge(shape, original, delta, true, false);
            break;
        case HandleType.Left:
            ResizeRectangleFromEdge(shape, original, delta, false, true);
            break;
        case HandleType.Right:
            ResizeRectangleFromEdge(shape, original, delta, false, false);
            break;
    }
}

private void ResizeRectangleFromCorner(ShapeDefinition shape, ShapeDefinition original, Vector delta, bool isTopOrLeft, bool changeWidth)
{
    // 参考ROI编辑器的实现
    var newWidth = original.Width;
    var newHeight = original.Height;
    var centerX = original.CenterX;
    var centerY = original.CenterY;

    if (changeWidth)
    {
        newWidth = isTopOrLeft
            ? original.Width - delta.X
            : original.Width + delta.X;
        if (newWidth < 10) newWidth = 10;

        centerX = isTopOrLeft
            ? original.CenterX - original.Width / 2 + newWidth / 2 + delta.X / 2
            : original.CenterX - original.Width / 2 + newWidth / 2 + delta.X / 2;
    }

    if (isTopOrLeft) // Top或Bottom
    {
        newHeight = original.Height - delta.Y;
        if (newHeight < 10) newHeight = 10;
        centerY = original.CenterY - original.Height / 2 + newHeight / 2 + delta.Y / 2;
    }

    shape.Width = newWidth;
    shape.Height = newHeight;
    shape.CenterX = centerX;
    shape.CenterY = centerY;
}

private void ResizeRectangleFromEdge(ShapeDefinition shape, ShapeDefinition original, Vector delta, bool isVertical, bool isTopOrLeft)
{
    // 参考ROI编辑器1760-1790行
    if (isVertical)
    {
        var newHeight = isTopOrLeft
            ? original.Height - delta.Y
            : original.Height + delta.Y;
        if (newHeight < 10) newHeight = 10;

        var centerY = isTopOrLeft
            ? original.CenterY - original.Height / 2 + newHeight / 2 + delta.Y / 2
            : original.CenterY - original.Height / 2 + newHeight / 2 + delta.Y / 2;

        shape.Height = newHeight;
        shape.CenterY = centerY;
    }
    else
    {
        var newWidth = isTopOrLeft
            ? original.Width - delta.X
            : original.Width + delta.X;
        if (newWidth < 10) newWidth = 10;

        var centerX = isTopOrLeft
            ? original.CenterX - original.Width / 2 + newWidth / 2 + delta.X / 2
            : original.CenterX - original.Width / 2 + newWidth / 2 + delta.X / 2;

        shape.Width = newWidth;
        shape.CenterX = centerX;
    }
}
```

### 5.3 圆形半径调整（参考ROI编辑器1473-1477行）

```csharp
private void HandleCircleResize(ShapeDefinition shape, Point currentPos)
{
    var center = new Point(shape.CenterX, shape.CenterY);

    // 计算从中心到鼠标的距离作为新半径
    var dx = currentPos.X - center.X;
    var dy = currentPos.Y - center.Y;
    var newRadius = Math.Sqrt(dx * dx + dy * dy);

    if (newRadius < 5) newRadius = 5;

    shape.Radius = newRadius;
}
```

### 5.4 直线端点调整（参考ROI编辑器1466-1470行）

```csharp
private void HandleLineEndpoint(ShapeDefinition shape, Point currentPos)
{
    if (_activeHandleType == HandleType.LineStart)
    {
        shape.StartX = currentPos.X;
        shape.StartY = currentPos.Y;
    }
    else if (_activeHandleType == HandleType.LineEnd)
    {
        shape.EndX = currentPos.X;
        shape.EndY = currentPos.Y;
    }
}
```

## 六、清理状态

在适当的位置清理旋转状态：

```csharp
private void DeselectRegion()
{
    // ... 现有逻辑 ...

    _currentHandles = null;
    _activeHandleType = HandleType.None;
    _rotateStartAngle = null;
    _rotateStartMouseAngle = null;
}

private void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
{
    if (!_isDraggingHandle) return;

    EndHandleDrag();

    // 重置旋转状态
    _rotateStartAngle = null;
    _rotateStartMouseAngle = null;
}
```

## 七、测试检查清单

集成完成后，按以下清单测试：

### 7.1 形状创建
- [ ] 矩形形状正常显示
- [ ] 圆形形状正常显示
- [ ] 旋转矩形形状正常显示
- [ ] 直线形状正常显示

### 7.2 形状样式
- [ ] 填充颜色与ROI编辑器一致
- [ ] 边框颜色与ROI编辑器一致
- [ ] 边框厚度与ROI编辑器一致
- [ ] 透明度与ROI编辑器一致
- [ ] 选中状态颜色正确

### 7.3 编辑手柄
- [ ] 矩形显示8个手柄
- [ ] 圆形显示4个手柄
- [ ] 旋转矩形显示9个手柄（8个缩放+1个旋转）
- [ ] 直线显示2个端点手柄

### 7.4 编辑操作
- [ ] 矩形手柄调整大小正常
- [ ] 圆形手柄调整半径正常
- [ ] 旋转矩形手柄调整大小正常
- [ ] 旋转手柄旋转操作正常，无跳变
- [ ] 直线端点调整正常

### 7.5 旋转矩形特有功能
- [ ] 方向箭头显示正确
- [ ] 旋转手柄连接线显示正确
- [ ] 方向箭头指示正确的"下方"方向

## 八、注意事项

1. **角度系统**：确保使用图像坐标系角度（顺时针为正，0°向下）
2. **鼠标捕获**：开始拖动时调用`CaptureMouse()`，结束时调用`ReleaseMouseCapture()`
3. **状态重置**：每次编辑操作后重置临时状态（`_rotateStartAngle`等）
4. **最小尺寸限制**：确保形状不会被调整得太小（最小10像素）
5. **事件通知**：编辑完成后触发`RegionDataChanged`事件

## 九、问题排查

### 问题1：手柄不显示
- 检查`_currentHandles`是否为null
- 检查`HandleRenderer.DrawHandles`是否被调用
- 检查overlayCanvas是否正确

### 问题2：旋转跳变
- 检查是否正确保存了`_rotateStartAngle`和`_rotateStartMouseAngle`
- 检查角度计算是否使用了正确的坐标系
- 检查是否每次鼠标移动都正确计算delta

### 问题3：形状定位不正确
- 检查Canvas.SetLeft和Canvas.SetTop的值
- 检查是否正确处理了shape.Tag中的位置信息
- 检查中心点和左上角的转换是否正确
