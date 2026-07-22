# ROIEditor 技术分析文档 - 新版本迁移指南

> 文档创建时间: 2026-03-08
> 目标版本: 新版本迁移参考

## 目录

1. [坐标系统](#1-坐标系统)
2. [命中测试逻辑](#2-命中测试逻辑)
3. [旋转角度计算](#3-旋转角度计算)
4. [参数显示系统](#4-参数显示系统)
5. [核心文件结构](#5-核心文件结构)
6. [关键算法详解](#6-关键算法详解)
7. [常见问题与解决方案](#7-常见问题与解决方案)
8. [迁移注意事项](#8-迁移注意事项)

---

## 1. 坐标系统

### 1.1 双坐标系统架构

ROIEditor 使用**双坐标系统**设计，实现了 UI 显示与数据存储的分离：

```
┌─────────────────────────────────────────────────────────┐
│                    ROIEditor 架构                        │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  屏幕坐标系          图像坐标系                            │
│  (Screen Coords)    (Image Coords)                      │
│                                                          │
│  • WPF UI 位置       • 数据存储位置                        │
│  • 屏幕像素值        • 图像像素坐标                        │
│  • 用于交互显示      • 用于数据持久化                      │
│                                                          │
│  转换关系:                                             │
│  Image = Screen / ScaleFactor                           │
│  Screen = Image × ScaleFactor                           │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 1.2 屏幕坐标系 (Screen Coordinates)

**定义**: WPF UI 层使用的坐标系统

**特点**:
- 原点 (0,0) 位于 Canvas 左上角
- X 轴向右为正
- Y 轴向下为正
- 单位: 像素 (pixel)

**使用场景**:
```csharp
// ROIImageEditor.xaml.cs
// 所有交互操作都在屏幕坐标系中
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    var position = e.GetPosition(_canvas);  // 屏幕坐标
    // ...
}

private Point ImageToScreen(Point imagePoint)
{
    return new Point(
        imagePoint.X * _scaleFactor,
        imagePoint.Y * _scaleFactor
    );
}

private Point ScreenToImage(Point screenPoint)
{
    return new Point(
        screenPoint.X / _scaleFactor,
        screenPoint.Y / _scaleFactor
    );
}
```

### 1.3 图像坐标系 (Image Coordinates)

**定义**: 实际数据存储使用的坐标系统

**特点**:
- 原点 (0,0) 位于图像左上角
- X 轴向右为正
- Y 轴向下为正
- 单位: 像素 (pixel)

**使用场景**:
```csharp
// ROI.cs
// ROI 核心数据存储在图像坐标系
public class ROI
{
    public Point Center { get; set; }        // 中心点（图像坐标）
    public double Width { get; set; }        // 宽度（图像像素）
    public double Height { get; set; }       // 高度（图像像素）
    public double Rotation { get; set; }     // 旋转角度（数学角度）

    // 所有计算和存储都基于图像坐标
    public Point[] GetCorners()
    {
        // 返回四个角的图像坐标
    }
}
```

### 1.4 坐标转换规则

#### 转换公式

```csharp
// 图像坐标 → 屏幕坐标
Point ImageToScreen(Point imagePoint, double scaleFactor)
{
    return new Point(
        imagePoint.X * scaleFactor,
        imagePoint.Y * scaleFactor
    );
}

// 屏幕坐标 → 图像坐标
Point ScreenToImage(Point screenPoint, double scaleFactor)
{
    return new Point(
        screenPoint.X / scaleFactor,
        screenPoint.Y / scaleFactor
    );
}

// ROI 转换（包含旋转）
ROIScreenData ConvertToScreenData(ROI roi, double scaleFactor)
{
    return new ROIScreenData
    {
        Center = ImageToScreen(roi.Center, scaleFactor),
        Width = roi.Width * scaleFactor,
        Height = roi.Height * scaleFactor,
        Rotation = roi.Rotation  // 角度不变
    };
}
```

#### 转换时机

| 操作 | 使用的坐标系 | 说明 |
|------|------------|------|
| UI 渲染 | 屏幕坐标 | Canvas 上的矩形和控件位置 |
| 鼠标交互 | 屏幕坐标 | 点击、拖拽、旋转操作 |
| 数据存储 | 图像坐标 | ROI 数据持久化到文件 |
| 算法处理 | 图像坐标 | 图像处理算法输入 |
| 导出结果 | 图像坐标 | 输出到其他系统的数据 |

---

## 2. 命中测试逻辑

### 2.1 命中测试概述

ROIEditor 实现了**三层命中测试系统**：

```
┌─────────────────────────────────────────────────────────┐
│                    命中测试层次                          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  第一层: 区域命中 (Region Hit Test)                      │
│  - 判断鼠标是否在 ROI 内部                               │
│  - 支持旋转后的矩形                                      │
│  - 用于选中、拖拽 ROI                                    │
│                                                          │
│  第二层: 编辑点命中 (Handle Hit Test)                    │
│  - 判断鼠标是否在编辑控制点上                            │
│  - 8个编辑点（4个角 + 4边中点）                          │
│  - 用于调整 ROI 大小和形状                               │
│                                                          │
│  第三层: 旋转手柄命中 (Rotation Handle Hit Test)         │
│  - 判断鼠标是否在旋转手柄上                              │
│  - 一个圆形旋转控制点                                    │
│  - 用于旋转 ROI                                          │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 2.2 区域命中测试 (Region Hit Test)

#### 算法原理

使用**局部坐标变换法**判断点是否在旋转矩形内：

```
1. 将鼠标点转换到 ROI 的局部坐标系
2. 在局部坐标系中，ROI 变成轴对齐矩形
3. 简单判断点是否在 [ -Width/2, Width/2 ] × [ -Height/2, Height/2 ] 范围内
```

#### 代码实现

```csharp
// ROIImageEditor.xaml.cs
private bool IsPointInROI(Point mousePoint, ROI roi)
{
    // 1. 将屏幕坐标转换为图像坐标
    var imagePoint = ScreenToImage(mousePoint);

    // 2. 计算局部坐标（相对于 ROI 中心）
    var dx = imagePoint.X - roi.Center.X;
    var dy = imagePoint.Y - roi.Center.Y;

    // 3. 逆旋转（将点转换到 ROI 的局部坐标系）
    var angleRad = -roi.Rotation * Math.PI / 180;  // 负号表示逆变换
    var localX = dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad);
    var localY = dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad);

    // 4. 判断是否在矩形范围内
    var halfWidth = roi.Width / 2;
    var halfHeight = roi.Height / 2;

    return localX >= -halfWidth && localX <= halfWidth &&
           localY >= -halfHeight && localY <= halfHeight;
}
```

#### 容差处理

```csharp
// 为命中测试添加容差（避免边缘难以选中）
private bool IsPointInROI(Point mousePoint, ROI roi, double tolerance = 2)
{
    // ... 前面的转换代码 ...

    // 添加容差判断
    var hitWidth = halfWidth + tolerance;
    var hitHeight = halfHeight + tolerance;

    return localX >= -hitWidth && localX <= hitWidth &&
           localY >= -hitHeight && localY <= hitHeight;
}
```

### 2.3 编辑点命中测试 (Handle Hit Test)

#### 编辑点布局

ROI 有 8 个编辑点，编号如下：

```
        TopLeft (0) ─── TopCenter (1) ─── TopRight (2)
              │                                    │
        MidLeft (3)                          MidRight (4)
              │                                    │
       BottomLeft (5) ── BottomCenter (6) ── BottomRight (7)
```

#### 编辑点数据结构

```csharp
// ROIImageEditor.xaml.cs
private enum HandleType
{
    TopLeft,       // 左上角
    TopCenter,     // 上边中点
    TopRight,      // 右上角
    MidLeft,       // 左边中点
    MidRight,      // 右边中点
    BottomLeft,    // 左下角
    BottomCenter,  // 下边中点
    BottomRight,   // 右下角
    Rotation       // 旋转手柄
}

private class HitInfo
{
    public ROI ROI { get; set; }
    public HandleType Handle { get; set; }
    public Point HitPoint { get; set; }
}
```

#### 编辑点位置计算

```csharp
// ROIImageEditor.xaml.cs
private Dictionary<HandleType, Point> GetHandlePositions(ROI roi)
{
    var positions = new Dictionary<HandleType, Point>();
    var corners = roi.GetCorners();  // 获取四个角点（屏幕坐标）

    positions[HandleType.TopLeft] = corners[0];
    positions[HandleType.TopRight] = corners[1];
    positions[HandleType.BottomRight] = corners[2];
    positions[HandleType.BottomLeft] = corners[3];

    // 计算边中点
    positions[HandleType.TopCenter] = new Point(
        (corners[0].X + corners[1].X) / 2,
        (corners[0].Y + corners[1].Y) / 2
    );
    positions[HandleType.MidRight] = new Point(
        (corners[1].X + corners[2].X) / 2,
        (corners[1].Y + corners[2].Y) / 2
    );
    positions[HandleType.BottomCenter] = new Point(
        (corners[2].X + corners[3].X) / 2,
        (corners[2].Y + corners[3].Y) / 2
    );
    positions[HandleType.MidLeft] = new Point(
        (corners[3].X + corners[0].X) / 2,
        (corners[3].Y + corners[0].Y) / 2
    );

    return positions;
}
```

#### 编辑点命中判定

```csharp
// ROIImageEditor.xaml.cs
private const double HANDLE_SIZE = 12;  // 编辑点大小（像素）
private const double HANDLE_HIT_TOLERANCE = 8;  // 命中容差（像素）
private const double TOTAL_HIT_SIZE = HANDLE_SIZE + HANDLE_HIT_TOLERANCE;  // 20

private HitInfo TestHandleHit(Point mousePoint, ROI roi)
{
    var handlePositions = GetHandlePositions(roi);
    var halfSize = TOTAL_HIT_SIZE / 2;

    // 检查每个编辑点
    foreach (var kvp in handlePositions)
    {
        var handleType = kvp.Key;
        var handlePos = kvp.Value;

        // 计算鼠标到编辑点的距离
        var dx = Math.Abs(mousePoint.X - handlePos.X);
        var dy = Math.Abs(mousePoint.Y - handlePos.Y);

        // 判断是否在命中范围内（正方形区域）
        if (dx <= halfSize && dy <= halfSize)
        {
            return new HitInfo
            {
                ROI = roi,
                Handle = handleType,
                HitPoint = handlePos
            };
        }
    }

    return null;
}
```

### 2.4 旋转手柄命中测试

#### 旋转手柄位置

```csharp
// ROIImageEditor.xaml.cs
private Point GetRotationHandlePosition(ROI roi)
{
    // 旋转手柄位于 ROI 顶部中心上方一定距离
    var screenCenter = ImageToScreen(roi.Center, _scaleFactor);
    var angleRad = roi.Rotation * Math.PI / 180;

    // 计算顶部中心点
    var topCenterOffset = roi.Height / 2 * _scaleFactor;
    var topCenter = new Point(
        screenCenter.X - topCenterOffset * Math.Sin(angleRad),
        screenCenter.Y - topCenterOffset * Math.Cos(angleRad)
    );

    // 旋转手柄在顶部中心上方 40 像素
    const double handleOffset = 40;
    return new Point(
        topCenter.X - handleOffset * Math.Sin(angleRad),
        topCenter.Y - handleOffset * Math.Cos(angleRad)
    );
}
```

#### 旋转手柄命中

```csharp
// ROIImageEditor.xaml.cs
private const double ROTATION_HANDLE_RADIUS = 10;  // 旋转手柄半径

private HitInfo TestRotationHandleHit(Point mousePoint, ROI roi)
{
    var handlePos = GetRotationHandlePosition(roi);

    // 计算鼠标到旋转手柄的距离
    var dx = mousePoint.X - handlePos.X;
    var dy = mousePoint.Y - handlePos.Y;
    var distance = Math.Sqrt(dx * dx + dy * dy);

    // 判断是否在命中范围内（圆形区域）
    if (distance <= ROTATION_HANDLE_RADIUS + HANDLE_HIT_TOLERANCE)
    {
        return new HitInfo
        {
            ROI = roi,
            Handle = HandleType.Rotation,
            HitPoint = handlePos
        };
    }

    return null;
}
```

### 2.5 综合命中测试流程

```csharp
// ROIImageEditor.xaml.cs
private HitInfo HitTest(Point mousePoint)
{
    // 1. 检查所有 ROI 的编辑点（从后往前，后创建的在上层）
    for (int i = _rois.Count - 1; i >= 0; i--)
    {
        var roi = _rois[i];

        // 检查旋转手柄
        var rotationHit = TestRotationHandleHit(mousePoint, roi);
        if (rotationHit != null)
            return rotationHit;

        // 检查编辑点
        var handleHit = TestHandleHit(mousePoint, roi);
        if (handleHit != null)
            return handleHit;
    }

    // 2. 检查所有 ROI 的区域命中
    for (int i = _rois.Count - 1; i >= 0; i--)
    {
        var roi = _rois[i];
        if (IsPointInROI(mousePoint, roi))
        {
            return new HitInfo
            {
                ROI = roi,
                Handle = HandleType.TopLeft,  // 区域命中不指定具体编辑点
                HitPoint = mousePoint
            };
        }
    }

    return null;  // 未命中任何 ROI
}
```

---

## 3. 旋转角度计算

### 3.1 数学角度系统

ROIEditor 使用**数学角度系统**，定义如下：

| 特性 | 值 |
|------|-----|
| 角度范围 | [-180°, 180°] |
| 正方向 | 逆时针 (Counter-Clockwise) |
| 负方向 | 顺时针 (Clockwise) |
| 零度 | 指向正上方 (12 点钟方向) |

#### 角度示例

```
        0° (正上方)
           ↑
           |
-90° ←----+----→ 90°
  (左)     |     (右)
           |
           ↓
       180°/-180° (正下方)
```

### 3.2 角度规范化

#### 规范化算法

```csharp
// ROI.cs
private double NormalizeAngle(double angle)
{
    // 1. 使用模运算将角度映射到 [0, 360)
    angle = angle % 360;

    // 2. 将角度转换到 [-180, 180]
    if (angle > 180)
        angle -= 360;
    else if (angle < -180)
        angle += 360;

    return angle;
}
```

#### 规范化示例

```csharp
NormalizeAngle(450°)   → 90°   // 450 - 360 = 90
NormalizeAngle(270°)   → -90°  // 270 - 360 = -90
NormalizeAngle(-270°)  → 90°   // -270 + 360 = 90
NormalizeAngle(540°)   → 180°  // 540 - 360 = 180
```

### 3.3 初始角度计算

#### 直线角度计算

```csharp
// ROIImageEditor.xaml.cs
private double CalculateAngle(Point start, Point end)
{
    var dx = end.X - start.X;
    var dy = end.Y - start.Y;

    // 使用 -Atan2 计算角度（因为屏幕坐标系 Y 轴向下）
    var angleRad = -Math.Atan2(dy, dx);
    var angleDeg = angleRad * 180 / Math.PI;

    return NormalizeAngle(angleDeg - 90);  // 减 90 度使 0° 指向上方
}
```

#### 为什么使用 -Atan2

在标准数学坐标系中：
- Y 轴向上
- Atan2(dy, dx) 给出的是从 X 轴正方向的角度，逆时针为正

在屏幕坐标系中：
- Y 轴向下
- 使用 -Atan2(dy, dx) 相当于对 Y 轴翻转
- 这样计算出的角度符合数学角度系统

### 3.4 Delta 角度计算

#### 问题：角度跳变

当 ROI 旋转接近 ±180° 边界时，会出现角度跳变：

```
例如：从 -179° 逆时针旋转 2°

直接计算: -179° + 2° = -177° ✓

但是：从 179° 顺时针旋转 2°

直接计算: 179° - 2° = 177° ✗ (应该是 -177°)
```

#### Delta 角度计算算法

```csharp
// ROIImageEditor.xaml.cs
private double CalculateDeltaAngle(double fromAngle, double toAngle)
{
    var delta = toAngle - fromAngle;

    // 规范化到 [-180, 180]
    if (delta > 180)
        delta -= 360;
    else if (delta < -180)
        delta += 360;

    return delta;
}
```

#### Delta 计算示例

```csharp
// 情况 1: 正常旋转
CalculateDeltaAngle(0, 30)    → 30    // 从 0° 到 30°，顺时针 30°
CalculateDeltaAngle(0, -30)   → -30   // 从 0° 到 -30°，逆时针 30°

// 情况 2: 跨越 180° 边界
CalculateDeltaAngle(179, -179)  → 2    // 从 179° 到 -179°，继续旋转 2°
CalculateDeltaAngle(-179, 179)  → -2   // 从 -179° 到 179°，继续旋转 -2°

// 情况 3: 大角度跨越
CalculateDeltaAngle(170, -170)  → 20   // 从 170° 到 -170°，继续旋转 20°
CalculateDeltaAngle(-170, 170)  → -20  // 从 -170° 到 170°，继续旋转 -20°
```

### 3.5 旋转交互实现

#### 旋转事件处理

```csharp
// ROIImageEditor.xaml.cs
private Point _rotationStartPoint;
private double _rotationStartAngle;
private ROI _rotatingROI;

private void OnRotationStarted(Point mousePoint, ROI roi)
{
    _rotatingROI = roi;
    _rotationStartPoint = mousePoint;
    _rotationStartAngle = roi.Rotation;

    CaptureMouse();  // 捕获鼠标，防止事件丢失
}

private void OnRotating(Point mousePoint)
{
    if (_rotatingROI == null)
        return;

    var screenCenter = ImageToScreen(_rotatingROI.Center, _scaleFactor);

    // 1. 计算鼠标相对于中心的角度
    var currentAngle = CalculateAngle(screenCenter, mousePoint);

    // 2. 计算起始角度
    var startAngle = CalculateAngle(screenCenter, _rotationStartPoint);

    // 3. 计算角度变化（使用 Delta 角度避免跳变）
    var delta = CalculateDeltaAngle(startAngle, currentAngle);

    // 4. 更新 ROI 角度（规范化）
    _rotatingROI.Rotation = NormalizeAngle(_rotationStartAngle + delta);

    // 5. 重绘
    InvalidateVisual();
}

private void OnRotationEnded()
{
    _rotatingROI = null;
    ReleaseMouseCapture();  // 释放鼠标捕获
}
```

#### 角度吸附功能（可选）

```csharp
// ROIImageEditor.xaml.cs
private const double SNAP_ANGLE = 15;  // 每 15° 吸附一次

private double SnapAngle(double angle)
{
    var snapped = Math.Round(angle / SNAP_ANGLE) * SNAP_ANGLE;

    // 吸附阈值：接近吸附点时才吸附
    if (Math.Abs(angle - snapped) <= SNAP_ANGLE / 4)
        return NormalizeAngle(snapped);

    return NormalizeAngle(angle);
}

// 在 OnRotating 中使用
_rotatingROI.Rotation = SnapAngle(_rotationStartAngle + delta);
```

### 3.6 角度与方向箭头

#### 方向箭头计算

```csharp
// ROI.cs
public Point GetDirectionArrow()
{
    // 方向箭头位于 ROI 上边中心，指向旋转方向
    var halfHeight = Height / 2;

    // 计算顶部中心点（局部坐标）
    var arrowOffset = halfHeight * 0.8;  // 距离顶部中心 80% 的位置

    // 转换为全局坐标
    var angleRad = Rotation * Math.PI / 180;

    return new Point(
        Center.X + arrowOffset * Math.Sin(angleRad),
        Center.Y - arrowOffset * Math.Cos(angleRad)
    );
}
```

#### 方向箭头绘制

```csharp
// ROIImageEditor.xaml.cs
private void DrawDirectionArrow(DrawingContext dc, ROI roi)
{
    var arrowPos = GetDirectionArrow(roi);
    var screenPos = ImageToScreen(arrowPos, _scaleFactor);

    // 绘制箭头标记
    var arrowSize = 10 * _scaleFactor;
    var angleRad = roi.Rotation * Math.PI / 180;

    // 箭头几何形状
    var arrowGeometry = new StreamGeometry();
    using (var context = arrowGeometry.Open())
    {
        var tip = screenPos;
        var baseOffset = arrowSize * 0.6;
        var widthOffset = arrowSize * 0.4;

        var leftBase = new Point(
            tip.X - baseOffset * Math.Sin(angleRad) - widthOffset * Math.Cos(angleRad),
            tip.Y + baseOffset * Math.Cos(angleRad) - widthOffset * Math.Sin(angleRad)
        );

        var rightBase = new Point(
            tip.X - baseOffset * Math.Sin(angleRad) + widthOffset * Math.Cos(angleRad),
            tip.Y + baseOffset * Math.Cos(angleRad) + widthOffset * Math.Sin(angleRad)
        );

        context.BeginFigure(tip, true, true);
        context.LineTo(leftBase, true, false);
        context.LineTo(rightBase, true, false);
        context.Close();
    }

    // 绘制箭头
    dc.DrawGeometry(Brushes.Red, new Pen(Brushes.Red, 2), arrowGeometry);
}
```

### 3.7 角度转换工具

#### 数学角度 ↔ OpenCV 角度

```csharp
// src/Plugin.SDK/Extensions/AngleConverter.cs
public static class AngleConverter
{
    /// <summary>
    /// 数学角度转 OpenCV 角度
    /// 数学角度: 0° 向上，逆时针为正，范围 [-180, 180]
    /// OpenCV 角度: 0° 向右，顺时针为正，范围 [0, 360]
    /// </summary>
    public static double MathToOpenCV(double mathAngle)
    {
        // 1. 转换为 [0, 360) 范围
        var angle = mathAngle % 360;
        if (angle < 0)
            angle += 360;

        // 2. 调整零度方向（从向上到向右）
        angle = (angle + 90) % 360;

        // 3. 调整旋转方向（逆时针到顺时针）
        return 360 - angle;
    }

    /// <summary>
    /// OpenCV 角度转数学角度
    /// </summary>
    public static double OpenCVToMath(double openCVAngle)
    {
        // 1. 调整旋转方向（顺时针到逆时针）
        var angle = 360 - openCVAngle;

        // 2. 调整零度方向（从向右到向上）
        angle = (angle - 90) % 360;

        // 3. 转换为 [-180, 180] 范围
        if (angle > 180)
            angle -= 360;

        return angle;
    }
}
```

#### 转换示例

```csharp
// 数学角度
MathToOpenCV(0)    → 90    // 向上 → 向右
MathToOpenCV(90)   → 0     // 向右 → 向右
MathToOpenCV(180)  → 270   // 向下 → 向左
MathToOpenCV(-90)  → 180   // 向左 → 向左

// OpenCV 角度
OpenCVToMath(0)    → 90    // 向右 → 向右
OpenCVToMath(90)   → 0     // 向上 → 向上
OpenCVToMath(180)  → -90   // 向左 → 向左
OpenCVToMath(270)  → 180   // 向下 → 向下
```

---

## 4. 参数显示系统

### 4.1 参数显示架构

```
┌─────────────────────────────────────────────────────────┐
│                    参数显示架构                          │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  UI Layer (ROIInfoPanel.xaml)                           │
│    ├── 数据绑定 (DataBinding)                           │
│    ├── 类型转换 (ValueConverter)                        │
│    └── 实时更新 (UpdateSourceTrigger)                   │
│                        ↕                                 │
│  ViewModel Layer (ROIInfoViewModel)                     │
│    ├── 显示模型 (ROIDisplayInfo)                        │
│    ├── 多选管理 (MultiSelect)                           │
│    └── 批量操作 (BatchOperation)                        │
│                        ↕                                 │
│  Data Layer (ROIDisplayInfo)                            │
│    ├── 属性包装 (Property Wrapper)                      │
│    ├── 变化通知 (PropertyChanged)                      │
│    └── 验证逻辑 (Validation)                             │
│                        ↕                                 │
│  Model Layer (ROI)                                      │
│    ├── 核心数据 (Core Data)                             │
│    └── 几何计算 (Geometry Calculation)                   │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 4.2 UI 层 (ROIInfoPanel.xaml)

#### 布局结构

```xml
<!-- ROIInfoPanel.xaml -->
<UserControl x:Class="SunEyeVision.Tool.ROIEditor.ROIInfoPanel">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <!-- ROI 类型选择 -->
            <ComboBox ItemsSource="{Binding ROITypes}"
                      SelectedItem="{Binding SelectedROIType}"
                      DisplayMemberPath="DisplayName" />

            <!-- ROI 名称 -->
            <TextBlock Text="名称:" />
            <TextBox Text="{Binding SelectedDisplayInfo.Name, UpdateSourceTrigger=PropertyChanged}" />

            <!-- ROI 位置 X -->
            <TextBlock Text="X:" />
            <TextBox Text="{Binding SelectedDisplayInfo.X, UpdateSourceTrigger=PropertyChanged}" />

            <!-- ROI 位置 Y -->
            <TextBlock Text="Y:" />
            <TextBox Text="{Binding SelectedDisplayInfo.Y, UpdateSourceTrigger=PropertyChanged}" />

            <!-- ROI 宽度 -->
            <TextBlock Text="宽度:" />
            <TextBox Text="{Binding SelectedDisplayInfo.Width, UpdateSourceTrigger=PropertyChanged}" />

            <!-- ROI 高度 -->
            <TextBlock Text="高度:" />
            <TextBox Text="{Binding SelectedDisplayInfo.Height, UpdateSourceTrigger=PropertyChanged}" />

            <!-- ROI 角度 -->
            <TextBlock Text="角度:" />
            <TextBox Text="{Binding SelectedDisplayInfo.Angle, UpdateSourceTrigger=PropertyChanged}" />

            <!-- 多选提示 -->
            <TextBlock Text="{Binding MultiSelectHint}"
                       Visibility="{Binding IsMultiSelect, Converter={StaticResource BooleanToVisibilityConverter}}" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

#### 数据绑定特点

1. **UpdateSourceTrigger=PropertyChanged**
   - 实时更新：输入时立即同步到模型
   - 即时反馈：画布上的 ROI 立即变化

2. **双向绑定**
   - UI → Model: 用户输入更新 ROI
   - Model → UI: 画布操作更新 UI

3. **值转换**
   - 类型转换: double ↔ string
   - 格式化: 显示固定小数位数
   - 可见性: 根据条件显示/隐藏控件

### 4.3 ViewModel 层 (ROIInfoViewModel)

#### 核心属性

```csharp
// ROIInfoViewModel.cs
public class ROIInfoViewModel : ObservableObject
{
    // 当前选中的显示信息
    private ROIDisplayInfo _selectedDisplayInfo;
    public ROIDisplayInfo SelectedDisplayInfo
    {
        get => _selectedDisplayInfo;
        set
        {
            SetProperty(ref _selectedDisplayInfo, value);
            OnPropertyChanged(nameof(IsMultiSelect));
            OnPropertyChanged(nameof(MultiSelectHint));
        }
    }

    // ROI 类型列表
    public ObservableCollection<ROITypeItem> ROITypes { get; } = new ObservableCollection<ROITypeItem>();

    // 当前选中的 ROI 类型
    private ROITypeItem _selectedROIType;
    public ROITypeItem SelectedROIType
    {
        get => _selectedROIType;
        set
        {
            SetProperty(ref _selectedROIType, value);
            UpdateSelectedROIType();
        }
    }

    // 是否多选
    public bool IsMultiSelect => _selectedROIs.Count > 1;

    // 多选提示文本
    public string MultiSelectHint =>
        _selectedROIs.Count > 1 ? $"已选择 {_selectedROIs.Count} 个 ROI，修改将批量应用" : string.Empty;

    // 当前选中的 ROI 列表
    private ObservableCollection<ROI> _selectedROIs;
    public ObservableCollection<ROI> SelectedROIs
    {
        get => _selectedROIs;
        set
        {
            SetProperty(ref _selectedROIs, value);
            UpdateSelectedDisplayInfo();
        }
    }
}
```

#### 更新选中显示信息

```csharp
// ROIInfoViewModel.cs
private void UpdateSelectedDisplayInfo()
{
    if (_selectedROIs == null || _selectedROIs.Count == 0)
    {
        SelectedDisplayInfo = null;
        return;
    }

    if (_selectedROIs.Count == 1)
    {
        // 单选: 直接创建显示信息
        SelectedDisplayInfo = new ROIDisplayInfo(_selectedROIs[0], this);
    }
    else
    {
        // 多选: 创建合并显示信息
        SelectedDisplayInfo = new ROIDisplayInfo(_selectedROIs, this);
    }
}
```

#### 批量应用修改

```csharp
// ROIInfoViewModel.cs
public void ApplyNameChange(string newName)
{
    if (IsMultiSelect)
    {
        // 多选: 批量修改名称（添加后缀）
        foreach (var roi in _selectedROIs)
        {
            roi.Name = $"{newName}_{_selectedROIs.IndexOf(roi) + 1}";
        }
    }
    else if (_selectedROIs.Count == 1)
    {
        // 单选: 直接修改
        _selectedROIs[0].Name = newName;
    }
}

public void ApplyTypeChange(ROIType newType)
{
    // 批量修改类型（类型相同的才修改）
    foreach (var roi in _selectedROIs)
    {
        if (roi.Type == newType || !TypeFilterEnabled)
        {
            roi.Type = newType;
        }
    }
}
```

### 4.4 数据层 (ROIDisplayInfo)

#### ROIDisplayInfo 类

```csharp
// ROIDisplayInfo.cs
public class ROIDisplayInfo : ObservableObject
{
    private readonly ROI _roi;
    private readonly List<ROI> _multiROIs;
    private readonly ROIInfoViewModel _viewModel;

    // 单选构造函数
    public ROIDisplayInfo(ROI roi, ROIInfoViewModel viewModel)
    {
        _roi = roi ?? throw new ArgumentNullException(nameof(roi));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _multiROIs = null;

        InitializeFromROI(roi);
    }

    // 多选构造函数
    public ROIDisplayInfo(List<ROI> rois, ROIInfoViewModel viewModel)
    {
        _roi = null;
        _multiROIs = rois ?? throw new ArgumentNullException(nameof(rois));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeFromROIs(rois);
    }

    // ROI 名称
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                _viewModel?.ApplyNameChange(value);
            }
        }
    }

    // ROI 位置 X
    private double _x;
    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                _viewModel?.ApplyGeometryChange("X", value);
            }
        }
    }

    // ROI 位置 Y
    private double _y;
    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
            {
                _viewModel?.ApplyGeometryChange("Y", value);
            }
        }
    }

    // ROI 宽度
    private double _width;
    public double Width
    {
        get => _width;
        set
        {
            if (SetProperty(ref _width, value))
            {
                _viewModel?.ApplyGeometryChange("Width", value);
            }
        }
    }

    // ROI 高度
    private double _height;
    public double Height
    {
        get => _height;
        set
        {
            if (SetProperty(ref _height, value))
            {
                _viewModel?.ApplyGeometryChange("Height", value);
            }
        }
    }

    // ROI 角度
    private double _angle;
    public double Angle
    {
        get => _angle;
        set
        {
            if (SetProperty(ref _angle, value))
            {
                _viewModel?.ApplyGeometryChange("Angle", value);
            }
        }
    }
}
```

#### 初始化显示信息

```csharp
// ROIDisplayInfo.cs
private void InitializeFromROI(ROI roi)
{
    _name = roi.Name;
    _x = Math.Round(roi.Center.X, 2);
    _y = Math.Round(roi.Center.Y, 2);
    _width = Math.Round(roi.Width, 2);
    _height = Math.Round(roi.Height, 2);
    _angle = Math.Round(roi.Rotation, 2);
}

private void InitializeFromROIs(List<ROI> rois)
{
    // 多选时显示第一个 ROI 的信息
    if (rois.Count > 0)
    {
        InitializeFromROI(rois[0]);
    }
}

// 从 ROI 更新显示信息（当画布操作导致 ROI 变化时调用）
public void UpdateFromROI(ROI roi)
{
    _name = roi.Name;
    _x = Math.Round(roi.Center.X, 2);
    _y = Math.Round(roi.Center.Y, 2);
    _width = Math.Round(roi.Width, 2);
    _height = Math.Round(roi.Height, 2);
    _angle = Math.Round(roi.Rotation, 2);

    OnPropertyChanged(nameof(Name));
    OnPropertyChanged(nameof(X));
    OnPropertyChanged(nameof(Y));
    OnPropertyChanged(nameof(Width));
    OnPropertyChanged(nameof(Height));
    OnPropertyChanged(nameof(Angle));
}
```

### 4.5 类型转换器

#### ROITypeToVisibilityConverter

```csharp
// ROIInfoPanel.xaml.cs
public class ROITypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ROIType roiType && parameter is string targetTypeStr)
        {
            ROIType targetROIType;
            if (Enum.TryParse(targetTypeStr, out targetROIType))
            {
                return roiType == targetROIType ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

#### BooleanToVisibilityConverter

```csharp
// ROIInfoPanel.xaml.cs
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
```

#### DoubleToStringConverter

```csharp
// ROIInfoPanel.xaml.cs
public class DoubleToStringConverter : IValueConverter
{
    private const string Format = "F2";  // 两位小数

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue.ToString(Format, culture);
        }
        return "0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Float, culture, out double result))
        {
            return result;
        }
        return 0.0;
    }
}
```

### 4.6 实时同步机制

#### 画布 → UI 同步

```csharp
// ROIImageEditor.xaml.cs
private void OnROIMoved(ROI roi)
{
    // 1. 获取 ROIInfoViewModel
    var viewModel = DataContext as ROIInfoViewModel;
    if (viewModel?.SelectedDisplayInfo != null)
    {
        // 2. 更新显示信息
        viewModel.SelectedDisplayInfo.UpdateFromROI(roi);
    }
}

private void OnROIRotated(ROI roi)
{
    // 同步角度变化
    OnROIMoved(roi);
}

private void OnROIResized(ROI roi)
{
    // 同步尺寸变化
    OnROIMoved(roi);
}
```

#### UI → 画布同步

```csharp
// ROIDisplayInfo.cs
public double X
{
    get => _x;
    set
    {
        if (SetProperty(ref _x, value))
        {
            // 触发 ViewModel 批量应用
            _viewModel?.ApplyGeometryChange("X", value);

            // 单选时直接更新 ROI
            if (_roi != null)
            {
                _roi.Center = new Point(value, _roi.Center.Y);
            }
        }
    }
}
```

### 4.7 多选批量操作

#### 批量操作策略

```csharp
// ROIInfoViewModel.cs
public void ApplyGeometryChange(string property, double value)
{
    if (_selectedROIs == null || _selectedROIs.Count == 0)
        return;

    switch (property)
    {
        case "X":
            // 选项 1: 绝对位置（所有 ROI 移动到相同的 X）
            foreach (var roi in _selectedROIs)
            {
                roi.Center = new Point(value, roi.Center.Y);
            }
            break;

        case "Y":
            // 选项 2: 相对偏移（保持 ROI 之间的相对位置）
            if (_selectedROIs.Count > 1)
            {
                var firstROI = _selectedROIs[0];
                var delta = value - firstROI.Center.Y;

                foreach (var roi in _selectedROIs)
                {
                    roi.Center = new Point(roi.Center.X, roi.Center.Y + delta);
                }
            }
            break;

        case "Width":
        case "Height":
            // 尺寸: 按比例缩放（保持纵横比）
            if (_selectedROIs.Count > 1)
            {
                var firstROI = _selectedROIs[0];
                var scale = property == "Width"
                    ? value / firstROI.Width
                    : value / firstROI.Height;

                foreach (var roi in _selectedROIs)
                {
                    roi.Width *= scale;
                    roi.Height *= scale;
                }
            }
            break;

        case "Angle":
            // 角度: 绝对角度（所有 ROI 旋转到相同角度）
            foreach (var roi in _selectedROIs)
            {
                roi.Rotation = value;
            }
            break;
    }
}
```

#### 类型过滤

```csharp
// ROIInfoViewModel.cs
private bool _typeFilterEnabled = true;
public bool TypeFilterEnabled
{
    get => _typeFilterEnabled;
    set => SetProperty(ref _typeFilterEnabled, value);
}

private ROIType _filterType = ROIType.Rectangle;
public ROIType FilterType
{
    get => _filterType;
    set
    {
        SetProperty(ref _filterType, value);
        // 筛选选中的 ROI 列表
        FilterSelectedROIs();
    }
}

private void FilterSelectedROIs()
{
    if (TypeFilterEnabled)
    {
        var filtered = _selectedROIs.Where(roi => roi.Type == FilterType).ToList();
        SelectedROIs = new ObservableCollection<ROI>(filtered);
    }
}
```

---

## 5. 核心文件结构

### 5.1 文件列表

| 文件路径 | 说明 | 行数 |
|---------|------|------|
| `ROI.cs` | ROI 数据模型 | ~300 |
| `ROIImageEditor.xaml.cs` | 画布编辑器核心 | ~800 |
| `ROIEditorParameters.cs` | 参数定义和数据序列化 | ~200 |
| `ROIEditorToolViewModel.cs` | 工具 ViewModel | ~300 |
| `ROIInfoViewModel.cs` | 信息面板 ViewModel | ~250 |
| `ROIDisplayInfo.cs` | 显示信息模型 | ~200 |
| `ROIInfoPanel.xaml` | 信息面板 UI | ~150 |
| `ROIInfoPanel.xaml.cs` | 信息面板代码后置 | ~100 |

### 5.2 依赖关系图

```
┌─────────────────────────────────────────────────────────┐
│                    依赖关系图                            │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ROI.cs (Model)                                          │
│    ├── 无依赖                                            │
│    └── 被 ROIImageEditor, ROIEditorToolViewModel 引用   │
│                                                          │
│  ROIImageEditor.xaml.cs (View)                          │
│    ├── 依赖 ROI.cs                                       │
│    └── 被 ROIEditorToolViewModel 引用                    │
│                                                          │
│  ROIEditorParameters.cs (Data)                          │
│    ├── 依赖 ROI.cs                                       │
│    └── 被 ROIEditorToolViewModel 引用                    │
│                                                          │
│  ROIEditorToolViewModel.cs (ViewModel)                 │
│    ├── 依赖 ROI.cs, ROIImageEditor, ROIEditorParameters│
│    └── 被 UI 层引用                                      │
│                                                          │
│  ROIInfoViewModel.cs (ViewModel)                        │
│    ├── 依赖 ROIDisplayInfo                               │
│    └── 被 ROIInfoPanel 引用                              │
│                                                          │
│  ROIDisplayInfo.cs (Model)                               │
│    ├── 依赖 ROI.cs, ROIInfoViewModel                    │
│    └── 被 ROIInfoViewModel 引用                          │
│                                                          │
│  ROIInfoPanel.xaml / .xaml.cs (View)                    │
│    ├── 依赖 ROIInfoViewModel                             │
│    └── 被 UI 层引用                                      │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

---

## 6. 关键算法详解

### 6.1 旋转矩形角点计算

#### 算法原理

```
给定: 中心点 (cx, cy), 宽度 w, 高度 h, 旋转角度 θ

步骤:
1. 计算未旋转时的四个角点（相对于中心）
   TL = (-w/2, -h/2)
   TR = ( w/2, -h/2)
   BR = ( w/2,  h/2)
   BL = (-w/2,  h/2)

2. 对每个角点应用旋转矩阵
   x' = x * cos(θ) - y * sin(θ)
   y' = x * sin(θ) + y * cos(θ)

3. 加上中心点偏移
   final_x = cx + x'
   final_y = cy + y'
```

#### 代码实现

```csharp
// ROI.cs
public Point[] GetCorners()
{
    var halfWidth = Width / 2;
    var halfHeight = Height / 2;
    var angleRad = Rotation * Math.PI / 180;

    var cos = Math.Cos(angleRad);
    var sin = Math.Sin(angleRad);

    // 未旋转时的四个角点（相对于中心）
    var corners = new[]
    {
        new Point(-halfWidth, -halfHeight),  // TL
        new Point( halfWidth, -halfHeight),  // TR
        new Point( halfWidth,  halfHeight),  // BR
        new Point(-halfWidth,  halfHeight)   // BL
    };

    // 旋转并转换到全局坐标
    var result = new Point[4];
    for (int i = 0; i < 4; i++)
    {
        var x = corners[i].X * cos - corners[i].Y * sin;
        var y = corners[i].X * sin + corners[i].Y * cos;

        result[i] = new Point(Center.X + x, Center.Y + y);
    }

    return result;
}
```

### 6.2 点到旋转矩形的最短距离

#### 算法原理

```
步骤:
1. 将点转换到矩形的局部坐标系
2. 将局部坐标限制在矩形范围内
3. 计算限制后的点与原点的距离
```

#### 代码实现

```csharp
// ROIImageEditor.xaml.cs
private double DistanceToROI(Point point, ROI roi)
{
    // 1. 转换到局部坐标系
    var dx = point.X - roi.Center.X;
    var dy = point.Y - roi.Center.Y;
    var angleRad = -roi.Rotation * Math.PI / 180;

    var localX = dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad);
    var localY = dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad);

    // 2. 限制在矩形范围内
    var halfWidth = roi.Width / 2;
    var halfHeight = roi.Height / 2;

    var clampedX = Math.Max(-halfWidth, Math.Min(halfWidth, localX));
    var clampedY = Math.Max(-halfHeight, Math.Min(halfHeight, localY));

    // 3. 计算距离
    var distX = localX - clampedX;
    var distY = localY - clampedY;

    return Math.Sqrt(distX * distX + distY * distY);
}
```

### 6.3 矩形交集检测

#### 算法原理

使用 SAT (Separating Axis Theorem) 分离轴定理检测两个旋转矩形是否相交。

```csharp
// ROIImageEditor.xaml.cs
private bool Intersects(ROI roi1, ROI roi2)
{
    var corners1 = roi1.GetCorners();
    var corners2 = roi2.GetCorners();

    // 矩形 1 的两个轴
    var axes1 = new[]
    {
        new Point(corners1[1].X - corners1[0].X, corners1[1].Y - corners1[0].Y),
        new Point(corners1[3].X - corners1[0].X, corners1[3].Y - corners1[0].Y)
    };

    // 矩形 2 的两个轴
    var axes2 = new[]
    {
        new Point(corners2[1].X - corners2[0].X, corners2[1].Y - corners2[0].Y),
        new Point(corners2[3].X - corners2[0].X, corners2[3].Y - corners2[0].Y)
    };

    // 所有分离轴
    var axes = axes1.Concat(axes2).ToArray();

    // 对每个轴进行投影测试
    foreach (var axis in axes)
    {
        var projection1 = Project(corners1, axis);
        var projection2 = Project(corners2, axis);

        // 如果投影不重叠，则不相交
        if (projection1.Max < projection2.Min || projection2.Max < projection1.Min)
            return false;
    }

    return true;  // 所有轴都有重叠，相交
}

private (double Min, double Max) Project(Point[] corners, Point axis)
{
    var min = double.MaxValue;
    var max = double.MinValue;

    // 归一化轴
    var length = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y);
    var normalizedAxis = new Point(axis.X / length, axis.Y / length);

    // 计算点积
    foreach (var corner in corners)
    {
        var dot = corner.X * normalizedAxis.X + corner.Y * normalizedAxis.Y;
        min = Math.Min(min, dot);
        max = Math.Max(max, dot);
    }

    return (min, max);
}
```

---

## 7. 常见问题与解决方案

### 7.1 角度跳变问题

#### 问题描述

当 ROI 旋转接近 ±180° 边界时，角度会从 180° 跳变到 -180°，导致旋转方向错误。

#### 解决方案

使用 Delta 角度计算：

```csharp
private double CalculateDeltaAngle(double fromAngle, double toAngle)
{
    var delta = toAngle - fromAngle;

    if (delta > 180)
        delta -= 360;
    else if (delta < -180)
        delta += 360;

    return delta;
}

// 使用
var delta = CalculateDeltaAngle(startAngle, currentAngle);
roi.Rotation = NormalizeAngle(startRotation + delta);
```

### 7.2 坐标混淆问题

#### 问题描述

在屏幕坐标和图像坐标之间转换时，容易出现混淆，导致位置错误。

#### 解决方案

明确区分坐标系，并使用命名规范：

```csharp
// 坐标变量命名规范
Point screenPoint;   // 屏幕坐标
Point imagePoint;    // 图像坐标

// 坐标转换函数命名明确
Point ImageToScreen(Point imagePoint, double scaleFactor)
Point ScreenToImage(Point screenPoint, double scaleFactor)

// 在函数注释中明确说明
/// <summary>
/// 将图像坐标转换为屏幕坐标
/// </summary>
/// <param name="imagePoint">图像坐标点</param>
/// <param name="scaleFactor">缩放因子</param>
/// <returns>屏幕坐标点</returns>
```

### 7.3 旋转矩形命中不准确

#### 问题描述

旋转后的矩形命中测试不准确，特别是在高 DPI 显示器上。

#### 解决方案

1. 使用 DPI 缩放
2. 添加适当的容差

```csharp
private double GetDpiScaleFactor()
{
    var presentationSource = PresentationSource.FromVisual(this);
    if (presentationSource != null)
    {
        return presentationSource.CompositionTarget.TransformToDevice.M11;
    }
    return 1.0;
}

private bool IsPointInROI(Point mousePoint, ROI roi)
{
    var scaleFactor = _scaleFactor * GetDpiScaleFactor();
    var tolerance = 2 * scaleFactor;  // DPI 感知容差

    // ... 命中测试逻辑
}
```

### 7.4 编辑点命中困难

#### 问题描述

编辑点太小，难以精确命中。

#### 解决方案

扩大命中区域，而非扩大显示区域：

```csharp
private const double HANDLE_SIZE = 12;           // 显示大小
private const double HANDLE_HIT_TOLERANCE = 8;   // 命中容差
private const double TOTAL_HIT_SIZE = HANDLE_SIZE + HANDLE_HIT_TOLERANCE;

private HitInfo TestHandleHit(Point mousePoint, ROI roi)
{
    var handlePositions = GetHandlePositions(roi);
    var halfSize = TOTAL_HIT_SIZE / 2;  // 使用扩大后的区域

    foreach (var kvp in handlePositions)
    {
        var dx = Math.Abs(mousePoint.X - kvp.Value.X);
        var dy = Math.Abs(mousePoint.Y - kvp.Value.Y);

        if (dx <= halfSize && dy <= halfSize)
        {
            return new HitInfo { ROI = roi, Handle = kvp.Key };
        }
    }

    return null;
}
```

### 7.5 鼠标事件丢失

#### 问题描述

拖拽时鼠标移出控件范围，导致事件丢失。

#### 解决方案

使用 CaptureMouse 捕获鼠标：

```csharp
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    var hitInfo = HitTest(e.GetPosition(_canvas));
    if (hitInfo != null)
    {
        _isDragging = true;
        _dragStartPoint = e.GetPosition(_canvas);
        _dragROI = hitInfo.ROI;
        _dragHandle = hitInfo.Handle;

        CaptureMouse();  // 捕获鼠标

        e.Handled = true;
    }
}

private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (_isDragging)
    {
        _isDragging = false;
        ReleaseMouseCapture();  // 释放捕获

        e.Handled = true;
    }
}
```

### 7.6 内存泄漏

#### 问题描述

长时间使用后内存占用不断增加。

#### 解决方案

及时释放事件处理器：

```csharp
// 在 ViewModel 中实现 IDisposable
public class ROIInfoViewModel : ObservableObject, IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 释放托管资源
                SelectedROIs.CollectionChanged -= OnSelectedROIsChanged;
            }

            _disposed = true;
        }
    }
}

// 在 View 的 Unloaded 事件中释放
private void OnUnloaded(object sender, RoutedEventArgs e)
{
    var viewModel = DataContext as ROIInfoViewModel;
    viewModel?.Dispose();
}
```

### 7.7 方向箭头显示错误

#### 问题描述

ROI 旋转后，方向箭头的方向不正确。

#### 解决方案

确保使用正确的角度系统（数学角度）：

```csharp
// ROI.cs
public Point GetDirectionArrow()
{
    // 方向箭头位于 ROI 上边中心
    var halfHeight = Height / 2;
    var arrowOffset = halfHeight * 0.8;

    // 使用数学角度（逆时针为正）
    var angleRad = Rotation * Math.PI / 180;

    // 计算箭头位置
    return new Point(
        Center.X + arrowOffset * Math.Sin(angleRad),
        Center.Y - arrowOffset * Math.Cos(angleRad)
    );
}
```

---

## 8. 迁移注意事项

### 8.1 坐标系统迁移

#### 必须保持一致性

1. **数据存储**: 始终使用图像坐标
2. **UI 显示**: 始终使用屏幕坐标
3. **转换点**: 明确标记转换函数的使用位置

```csharp
// ✓ 正确：明确区分
var imagePoint = ScreenToImage(mousePoint, _scaleFactor);
roi.Center = imagePoint;  // 存储到图像坐标

// ✗ 错误：混淆坐标
roi.Center = mousePoint;  // 错误地将屏幕坐标存储
```

#### 转换验证

```csharp
// 添加单元测试验证转换正确性
[TestMethod]
public void TestCoordinateConversion()
{
    var scaleFactor = 2.0;
    var imagePoint = new Point(100, 100);
    var screenPoint = ImageToScreen(imagePoint, scaleFactor);

    Assert.AreEqual(200, screenPoint.X);
    Assert.AreEqual(200, screenPoint.Y);

    var backToImage = ScreenToImage(screenPoint, scaleFactor);
    Assert.AreEqual(imagePoint.X, backToImage.X);
    Assert.AreEqual(imagePoint.Y, backToImage.Y);
}
```

### 8.2 角度系统迁移

#### 必须使用数学角度

1. **定义**: 逆时针为正，范围 [-180°, 180°]
2. **计算**: 使用 -Atan2(dy, dx) - 90
3. **存储**: 始终规范化到 [-180°, 180°]

```csharp
// ✓ 正确：使用数学角度
var angle = -Math.Atan2(dy, dx) * 180 / Math.PI - 90;
roi.Rotation = NormalizeAngle(angle);

// ✗ 错误：使用屏幕角度
var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
roi.Rotation = angle;  // 没有规范化，可能导致问题
```

#### OpenCV 交互

```csharp
// 调用 OpenCV 前转换
var openCVAngle = AngleConverter.MathToOpenCV(roi.Rotation);
var rotatedImage = OpenCV.Rotate(image, openCVAngle);

// 处理结果后转回
var resultAngle = AngleConverter.OpenCVToMath(extractedAngle);
roi.Rotation = resultAngle;
```

### 8.3 命中测试迁移

#### 保持三层结构

1. **区域命中**: 使用局部坐标变换
2. **编辑点命中**: 使用扩大后的命中区域
3. **旋转手柄命中**: 使用圆形命中区域

#### 性能优化

```csharp
// 使用空间索引加速命中测试（当 ROI 数量多时）
private QuadTree<ROI> _roiQuadTree;

private void BuildQuadTree()
{
    var bounds = GetCanvasBounds();
    _roiQuadTree = new QuadTree<ROI>(bounds);

    foreach (var roi in _rois)
    {
        var roiBounds = GetROIBounds(roi);
        _roiQuadTree.Insert(roi, roiBounds);
    }
}

private HitInfo HitTest(Point mousePoint)
{
    // 先查询空间索引
    var candidateROIs = _roiQuadTree.Query(mousePoint);

    // 只测试候选 ROI
    foreach (var roi in candidateROIs)
    {
        var hit = TestHandleHit(mousePoint, roi) ??
                  TestRotationHandleHit(mousePoint, roi) ??
                  (IsPointInROI(mousePoint, roi) ? new HitInfo { ROI = roi } : null);

        if (hit != null)
            return hit;
    }

    return null;
}
```

### 8.4 参数显示迁移

#### 保持三层架构

1. **UI 层**: 使用数据绑定和 UpdateSourceTrigger=PropertyChanged
2. **ViewModel 层**: 实现批量操作和类型过滤
3. **Model 层**: 提供属性变化通知

#### 避免性能问题

```csharp
// ✓ 正确：使用延迟更新
private DispatcherTimer _updateTimer;

public double X
{
    get => _x;
    set
    {
        SetProperty(ref _x, value);
        ScheduleUpdate();  // 延迟更新，避免频繁触发
    }
}

private void ScheduleUpdate()
{
    if (_updateTimer == null)
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _updateTimer.Tick += (s, e) =>
        {
            ApplyChanges();
            _updateTimer.Stop();
        };
    }

    _updateTimer.Stop();
    _updateTimer.Start();
}

// ✗ 错误：每次属性变化都立即应用
public double X
{
    get => _x;
    set
    {
        SetProperty(ref _x, value);
        ApplyChanges();  // 频繁调用，性能差
    }
}
```

### 8.5 兼容性迁移

#### 数据格式兼容

```csharp
// 支持旧版本数据格式
public class ROIEditorParameters
{
    public List<ROIData> ROIs { get; set; } = new List<ROIData>();

    // 兼容旧版本的序列化方法
    public static ROIEditorParameters FromV1Format(string json)
    {
        var v1Data = JsonConvert.DeserializeObject<V1ROIData>(json);

        var parameters = new ROIEditorParameters();
        foreach (var v1Roi in v1Data.Regions)
        {
            parameters.ROIs.Add(ConvertFromV1(v1Roi));
        }

        return parameters;
    }

    private static ROIData ConvertFromV1(V1Region v1Region)
    {
        // 转换逻辑
        return new ROIData
        {
            X = v1Region.X,
            Y = v1Region.Y,
            Width = v1Region.W,
            Height = v1Region.H,
            Rotation = AngleConverter.ScreenAngleToMathAngle(v1Region.Angle)
        };
    }
}
```

#### API 兼容

```csharp
// 提供兼容旧版本的 API
public class ROIImageEditor : Control
{
    // 新版 API
    public void AddROI(ROI roi) { /* ... */ }

    // 兼容旧版 API
    [Obsolete("使用 AddROI(ROI roi) 代替")]
    public void AddRegion(string id, double x, double y, double w, double h, double angle)
    {
        var roi = new ROI
        {
            Id = id,
            Center = new Point(x + w / 2, y + h / 2),
            Width = w,
            Height = h,
            Rotation = AngleConverter.ScreenAngleToMathAngle(angle)
        };
        AddROI(roi);
    }
}
```

---

## 附录

### A. 术语表

| 术语 | 英文 | 说明 |
|------|------|------|
| 感兴趣区域 | ROI (Region of Interest) | 图像中需要处理的特定区域 |
| 屏幕坐标 | Screen Coordinates | WPF UI 使用的坐标系统，原点在左上角 |
| 图像坐标 | Image Coordinates | 数据存储使用的坐标系统，原点在左上角 |
| 数学角度 | Mathematical Angle | 逆时针为正，范围 [-180°, 180°] |
| Delta 角度 | Delta Angle | 角度变化量，用于避免边界跳变 |
| 命中测试 | Hit Test | 判断鼠标是否在某个对象上的过程 |
| 编辑点 | Handle | 用于调整 ROI 大小和形状的控制点 |
| 旋转手柄 | Rotation Handle | 用于旋转 ROI 的圆形控制点 |
| 缩放因子 | Scale Factor | 图像坐标到屏幕坐标的转换比例 |
| 规范化 | Normalize | 将角度转换到指定范围的过程 |

### B. 参考代码文件

```
tools/SunEyeVision.Tool.ROIEditor/
├── ROI.cs                          # ROI 数据模型
├── ROIImageEditor.xaml             # 画布编辑器 UI
├── ROIImageEditor.xaml.cs          # 画布编辑器逻辑
├── ROIEditorParameters.cs          # 参数定义
├── ROIEditorToolViewModel.cs       # 工具 ViewModel
├── ROIInfoViewModel.cs             # 信息面板 ViewModel
├── ROIDisplayInfo.cs               # 显示信息模型
├── ROIInfoPanel.xaml               # 信息面板 UI
└── ROIInfoPanel.xaml.cs            # 信息面板代码后置

src/Plugin.SDK/Extensions/
└── AngleConverter.cs               # 角度转换工具
```

### C. 测试用例示例

```csharp
// ROIEditorTests.cs
[TestClass]
public class ROIEditorTests
{
    [TestMethod]
    public void TestAngleNormalization()
    {
        var roi = new ROI();

        // 测试边界值
        roi.Rotation = 180;
        Assert.AreEqual(180, roi.Rotation);

        roi.Rotation = 181;
        Assert.AreEqual(-179, roi.Rotation);

        roi.Rotation = -181;
        Assert.AreEqual(179, roi.Rotation);
    }

    [TestMethod]
    public void TestDeltaAngle()
    {
        // 测试跨越 180° 边界
        var delta = CalculateDeltaAngle(179, -179);
        Assert.AreEqual(2, delta);
    }

    [TestMethod]
    public void TestHitTest()
    {
        var roi = new ROI
        {
            Center = new Point(100, 100),
            Width = 50,
            Height = 50,
            Rotation = 45
        };

        var hitPoint = new Point(100, 100);
        Assert.IsTrue(IsPointInROI(hitPoint, roi));

        var missPoint = new Point(200, 200);
        Assert.IsFalse(IsPointInROI(missPoint, roi));
    }

    [TestMethod]
    public void TestCoordinateConversion()
    {
        var scaleFactor = 2.0;
        var imagePoint = new Point(50, 50);
        var screenPoint = ImageToScreen(imagePoint, scaleFactor);

        Assert.AreEqual(100, screenPoint.X);
        Assert.AreEqual(100, screenPoint.Y);

        var backToImage = ScreenToImage(screenPoint, scaleFactor);
        Assert.AreEqual(imagePoint, backToImage);
    }
}
```

---

**文档版本**: 1.0
**最后更新**: 2026-03-08
**适用版本**: SunEyeVision ROIEditor
**维护者**: SunEyeVision 开发团队
