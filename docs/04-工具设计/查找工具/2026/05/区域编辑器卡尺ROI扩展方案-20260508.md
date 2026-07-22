# RegionEditorManagerControl 卡尺 ROI 扩展方案

> 版本：v1.0（全新）
> 日期：2026-05-07
> 作者：region-editor-designer agent
> 状态：待审核

---

## 一、现状分析（基于实际代码调查结果）

### 1.1 RoiType 枚举完整清单及实现状态

**文件**: `src/Plugin.SDK/Models/Roi/IRoi.cs` (第 11-62 行)

| 枚举值 | 值 | ROI模型类 | IRoi实现状态 |
|--------|-----|----------|-------------|
| FullImage | 0 | FullImageRoi.cs | ✅ 完整实现 |
| Rectangle | 1 | RectangleRoi.cs | ✅ 完整实现 |
| RotatedRectangle | 2 | (内嵌于Rectangle) | ⚠️ 无独立类，但枚举存在 |
| Annulus | 3 | AnnulusRoi.cs | ✅ 完整实现 |
| Polygon | 4 | PolygonRoi.cs | ✅ 完整实现 |
| **LineCaliper** | **5** | ❌ 不存在 | ❌ 仅枚举预留 |
| **ArcCaliper** | **6** | ❌ 不存在 | ❌ 仅枚举预留 |
| Circle | 7 | CircleRoi.cs | ✅ 完整实现 |
| Ellipse | 8 | (无独立类) | ❌ 仅枚举预留 |
| Custom | 99 | - | 占位 |

**关键发现**：
- `LineCaliper=5` 和 `ArcCaliper=6` 已在枚举中预留
- 但 Circle 已占用值 7，所以 **CircleCaliper 需要使用新值（建议 9 或调整现有枚举）**
- 整条实现链路（ROI模型 → UI层ShapeType → 渲染器 → 手柄 → 交互逻辑）对 Caliper 类型完全为空

### 1.2 RegionEditorManagerControl 当前支持的形状

**文件**: `src/Plugin.SDK/UI/Controls/Region/Views/RegionEditorControl.xaml`

当前工具栏按钮布局（XAML 第 64-145 行）：

```
┌─────────────────────────────────────────────────┐
│ [▽选择] [○圆形] [□旋转矩形] [—直线(隐藏)] [⬠多边形] │
│   V        C           O            L          P    │
└─────────────────────────────────────────────────┘
```

注意：**矩形(Rectangle)** 按钮未出现在 XAML 工具栏中，但在代码中有快捷键支持(R键)。实际可见的绘制工具有 4 个：圆形、旋转矩形、多边形、直线(隐藏) + 选择模式。

### 1.3 UI层 ShapeType 枚举现状

**文件**: `src/Plugin.SDK/UI/Controls/Region/Models/ShapeType.cs` (第 10-56 行)

| ShapeType 值 | 名称 | 渲染器支持 | 手柄支持 | 交互逻辑支持 |
|-------------|------|-----------|---------|------------|
| None = 0 | 空闲/选择 | - | - | ✅ |
| Point = 1 | 点 | - | - | ✅ 选择模式标识 |
| Line = 2 | 直线 | ✅ CreateLine | ✅ CreateLineHandles | ✅ 拖拽+预览 |
| Circle = 3 | 圆形 | ✅ CreateCircle | ✅ CreateCircleHandles | ✅ 拖拽+预览 |
| Rectangle = 4 | 矩形 | ✅ CreateRectangle | ✅ CreateRectangleHandles | ✅ 拖拽+预览 |
| RotatedRectangle = 5 | 旋转矩形 | ✅ CreateRotatedRect | ✅ CreateRotatedRectHandles | ✅ 拖拽+旋转+预览 |
| Polygon = 6 | 多边形 | ✅ CreatePolygon | ✅ CreatePolygonHandles | ✅ 多点绘制+顶点拖拽 |
| Annulus = 7 | 圆环 | ❌ 无CreateXxx方法 | ❌ 无CreateXxxHandles | ❌ 无分支处理 |
| Arc = 8 | 弧形 | ❌ 无CreateXxx方法 | ❌ 无CreateXxxHandles | ❌ 无分支处理 |

**关键发现**：
- **Annulus 和 Arc 虽然在枚举中定义，但在渲染器和交互层面完全未实现**
- 需要新增的三个卡尺类型：`LineCaliper`, `ArcCaliper`, `CircleCaliper`
- ShapeType 是 UI 层的形状类型，与 RoiType（SDK 层的 ROI 类型）是两个独立枚举

### 1.4 ShapeParameters 数据结构现状

**文件**: `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs` (第 54-380 行)

当前字段清单：

| 字段组 | 字段名 | 类型 | 用途形状 |
|-------|--------|------|---------|
| **通用参数** | ShapeType | enum | 全部 |
| | CenterX, CenterY | double | 圆形/矩形/旋转矩形 |
| | Width, Height | double | 矩形/旋转矩形 |
| | Angle | double | 旋转矩形 |
| | Radius | double | 圆形 |
| | OuterRadius | double | 圆环(Annulus,未实现) |
| **直线参数** | StartX, StartY | double | 直线(Line) |
| | EndX, EndY | double | 直线(Line) |
| **多边形参数** | Points | ObservableCollection\<Point2D\> | 多边形(Polygon) |
| **弧形参数** | StartAngle | double | 弧形(Arc,未实现) |
| | EndAngle | double | 弧形(Arc,未实现) |
| **样式参数** | FillColorArgb, StrokeColorArgb, Opacity, StrokeThickness | - | 全部 |

**缺失字段**（卡尺所需）：
- LineCaliper: RakeAngle, MeasureLength, SampleWidth, NormalDirection
- ArcCaliper/CircleCaliper: SampleCount, ProjectLength

### 1.5 渲染链路完整调用链

```
用户操作 → RegionEditorControl.ProcessMouseDown/Move/Up
                ↓
         更新 _currentDrawingRegion.Parameters (ShapeParameters)
                ↓
         RenderRequested?.Invoke()
                ↓
         Manager.RenderAll()
                ↓
         inspectionsEditor.GetRenderContexts()
                ↓
         CreateRenderContextInternal(region, shape)
            → new RegionRenderContext { ShapeType, Center, Width, ... }
                ↓
         _renderer.Render(allContexts)
                ↓
         WpfShapeRenderer.UpdateOrCreateRegion(context)
                ↓
         switch(region.ShapeType):
             Rectangle  → CreateRectangle()      → WPF Rectangle
             Circle     → CreateCircle()         → WPF Ellipse
             RotatedRect→ CreateRotatedRect()    → WPF Rectangle+RotateTransform
             Line       → CreateLine()          → WPF Line
             Polygon    → CreatePolygon()       → WPF Polygon
             (其他      → null / 无处理)
                ↓
         UpdateHelpers(region)
             RotatedRect → CreateOrUpdateDirectionArrow() → 方向箭头辅助线
                ↓
         Canvas.Children.Add(shape)
```

**涉及文件**:
- `Rendering/WpfShapeRenderer.cs` (800行) — 主渲染器
- `Rendering/IRegionRenderer.cs` — RegionRenderContext 定义
- `Rendering/RotatedRectangleHelper.cs` — 旋转矩形辅助计算

### 1.6 交互链路鼠标事件分发流程

```
ImageControl 鼠标事件
       ↓
Manager.OnCanvasMouseLeftButtonDown(e)
       ↓
┌── 场景1: GetInteractingEditor()?.ProcessMouseDown(pos) ─── 有编辑器正在交互
├── 场景2: HitTestRegion(pos) → ProcessMouseDown(pos) ──── 选择模式命中区域
├── 场景3: ViewModel.IsDrawing → ProcessMouseDown(pos) ─── 绘制模式开始绘制
│       ↓
│   switch(_currentTool):
│     Rectangle/RotatedRect: 初始化 CenterX/Y, Width=0, Height=0
│     Circle: 初始化 CenterX/Y, Radius=0
│     Line: 初始化 StartX/Y = EndX/Y = pos
│     Polygon: 进入多点采集模式
│
├── 场景4: GetEditorWithSelection()?.ProcessMouseDown(pos) ── 手柄拖拽
│       ↓
│   HandleRenderer.HitTestHandle(position, _currentHandles)
│     → None: 尝试 HitTestRegion → 命中则拖动整体
│     → Center: 设置 _isDragging=true (整体平移)
│     → Rotate: 设置 _isDraggingHandle=true + 记录角度初值
│     → TopLeft/TopRight/...: 设置 _isDraggingHandle=true + 记录尺寸初值
│     → LineStart/LineEnd: 直线端点拖拽
│     → PolygonVertex: 多边形顶点拖拽
│
└── 场景5: DeselectAll() × 2 ──────────────────────── 空白点击
```

**ProcessMouseMove 分发**:

```
Manager.OnImageMouseMove(e)
       ↓
GetInteractingEditor()?.ProcessMouseMove(pos)  ← 只路由给正在交互的编辑器
       ↓
if (_isDrawing): UpdateDrawingPreview(currentPosition)
    switch(_currentTool):
        Rect/RotatedRect: 更新 Width=|dx|, Height=|dy|
        Circle: Radius = sqrt(dx²+dy²)
        Line: EndX/Y = currentPosition
elif (_isDragging): MoveRegion(region, offset)
elif (_isDraggingHandle): UpdateHandleDrag(currentPosition)
    Rotate: 计算新角度 = 初值 + (mouseAngle - startAngle)
    Corners: 根据手柄类型调整 Width/Height/Angle
    LineStart/End: 更新起点或终点坐标
```

**涉及文件**:
- `Views/RegionEditorManagerControl.xaml.cs` (602行) — 统一事件路由
- `Views/RegionEditorControl.xaml.cs` (~2896行) — 核心交互状态机

### 1.7 Caliper 相关代码引用汇总

| 文件 | 引用内容 | 性质 |
|------|---------|------|
| `IRoi.cs:41` | `LineCaliper = 5` 枚举定义 | 枚举预留 |
| `IRoi.cs:46` | `ArcCaliper = 6` 枚举定义 | 枚举预留 |
| 其他所有文件 | **无任何 Caliper 引用** | 完全空白 |

**结论**: 除了 IRoi.cs 中的两行枚举声明外，卡尺功能在整个代码库中是零实现的。

---

## 二、三种新卡尺 ROI 详细设计

### 2.1 LineCaliperROI（线卡尺）

#### 数据模型

```csharp
// ShapeType 枚举新增
LineCaliper = 9,  // 或使用下一个可用值

// ShapeParameters 新增字段（UI层）
private double _rakeAngle = 90;           // 搜索方向角度（默认90°=垂直于直线）
private double _measureLength = 50;        // 测量长度（搜索距离）
private double _sampleWidth = 20;          // 采样宽度
private int _sampleCount = 10;             // 采样点数量
private double _normalDirection = 1;       // 法向方向（1=正向, -1=反向）

// RegionRenderContext 新增字段（渲染上下文）
public double RakeAngle { get; set; }       // 搜索角度
public double MeasureLength { get; set; }    // 测量长度
public double SampleWidth { get; set; }      // 采样宽度
public int SampleCount { get; set; }         // 采样数量
public bool IsNormalDirection { get; set; } // 法向方向
```

#### 可视化设计

```
                    法向箭头（表示搜索方向）
                         ↑
                         │ measureLength
                         │
    ═════════════════════╪═══════════════════════  ← 主轴直线段
    |← sampleWidth →|                          |← sampleWidth →|
    ┌──────────────┐                          ┌──────────────┐
    │  采样虚线框   │                          │  采样虚线框   │
    └──────────────┘                          └──────────────┘
    ■                                          ■              ← 端点方形手柄
    ┌──────────────────────────────────────────────┐
    │                  ●                            │  ← 中点旋转手柄
    └──────────────────────────────────────────────┘

图例:
  ■ 端点手柄（方形, HandleType.LineStart/LineEnd）
  ● 旋转手柄（圆形红色, HandleType.Rotate）
  □ 宽度调节手柄（两侧边中心）
  ══ 主轴（实线, StrokeColor）
  --- 采样范围（虚线框, 半透明填充）
  → 法向箭头（辅助元素, 类似RotatedRectangleHelper的方向箭头）
```

#### 交互行为

| 操作 | 触发条件 | 行为 |
|------|---------|------|
| 拖端点 | MouseDown命中 LineStart/LineEnd 手柄 | 改变起点/终点位置，自动更新长度和中心 |
| 拖旋转手柄 | MouseDown命中 Rotate 手柄 | 绕中心点旋转整条直线（复用RotatedRectangle的角度计算逻辑） |
| 拖宽度调节 | MouseDown命中侧边手柄 | 调整 SampleWidth（两侧独立或同步） |
| 整体拖动 | MouseDown命中 Center 手柄或区域内部 | 平移整个ROI（保持相对几何关系不变） |
| 创建绘制 | 选择LineCaliper工具后拖拽 | 第一次点击确定起点，拖拽确定终点和初始宽度 |

### 2.2 ArcCaliperROI（弧形卡尺）

#### 数据模型

```csharp
// ShapeType 枚举新增
ArcCaliper = 10,

// ShapeParameters 新增字段（复用已有 CenterX/Y, Radius, StartAngle, EndAngle）
private int _arcSampleCount = 12;       // 弧上均匀采样数
private double _projectLength = 30;      // 投射长度（沿法向）

// RegionRenderContext 新增字段
public int ArcSampleCount { get; set; }  // 采样数
public double ProjectLength { get; set; } // 投射长度
```

**注意**: 复用已有的 `Radius`, `CenterX/Y`, `StartAngle`, `EndAngle` 字段，仅新增弧形特有参数。

#### 可视化设计

```
                        StartAngle标记
                           ↗
                      ╭────╮
                   ╭─╯    ╰─╮  ← 弧形主体（实线圆弧）
                 ╭─╰  采样射线  ╰─╮
               ╱   ↑           ↑   ╲
             ╱     │projectLen │     ╲
           ╱       │           │       ╲
         ○─────────●───────────●─────────○  EndAngle标记
       圆心        ↑           ↑
      十字标记    采样点      采样点


图例:
  ○ 圆心手柄（HandleType.Center, 十字标记）
  ● 半径调节手柄（HandleType.Right, 在弧的中点外侧）
  △ 起止角手柄（HandleType.ArcStart/ArcEnd, 在起止角位置）
  ╭─╮ 圆弧轮廓（PathGeometry ArcSegment）
  ↑  采样射线（从弧上的采样点向外发射，均匀分布）
  StartAngle/EndAngle 文字标注
```

#### 交互行为

| 操作 | 触发条件 | 行为 |
|------|---------|------|
| 拖圆心 | MouseDown命中 Center 手柄 | 平移整个弧形 |
| 拖半径 | MouseDown命中半径手柄 | 调整半径大小 |
| 拖起始角 | MouseDown命中 ArcStart 手柄 | 顺时针/逆时针调整 StartAngle |
| 拖终止角 | MouseDown命中 ArcEnd 手柄 | 顺时针/逆时针调整 EndAngle |
| 整体拖动 | MouseDown命中弧形内部 | 平移整个ROI |
| 创建绘制 | 选择ArcCaliper工具后拖拽 | 第一次点击确定圆心，拖拽确定半径，再拖拽确定起止角范围 |

### 2.3 CircleCaliperROI（圆环卡尺）

#### 数据模型

```csharp
// ShapeType 枚举新增
CircleCaliper = 11,

// ShapeParameters 新增字段（复用已有 CenterX/Y, Radius）
private int _circleSampleCount = 16;     // 圆周均匀采样数
private double _circleProjectLength = 30; // 投射长度

// 与 ArcCaliper 的区别：无 StartAngle/EndAngle，固定360°
```

#### 可视化设计

```
                    12个采样射线示例（像时钟刻度）

                     ↑  0°
                     │
               ╭─────┼─────╮
              ╱      │      ╲
             │  ↑    │    ↑  │
             │  │    │    │  │
        ←───╯──┼────●────┼──╱──→  采样射线（projectLength长度的短线段）
             │  │    │    │  │
             │↑ │    │    │↑ │   ↑ = projectLength
             ╲  │    │    ╱  ╲
               ╰─────┼─────╯
                     │
                     ↓
                   180°

              ● = 圆心（Center手柄，十字标记）
              ○ = 半径手柄（在0°方向的圆周上）

              外围完整圆形轮廓（Ellipse, Radius*2）
```

#### 交互行为

| 操作 | 触发条件 | 行为 |
|------|---------|------|
| 拖圆心 | MouseDown命中 Center 手柄 | 平移整个圆形 |
| 拖半径 | MouseDown命中半径手柄 | 调整半径大小（同时更新所有采样射线位置） |
| 整体拖动 | MouseDown命中圆形内部 | 平移整个ROI |
| 创建绘制 | 选择CircleCaliper工具后拖拽 | 点击确定圆心，拖拽确定半径（类似Circle的创建方式，但显示采样射线） |

**与 ArcCaliper 的对比**：

| 特性 | CircleCaliper | ArcCaliper |
|------|--------------|------------|
| 角度范围 | 固定360° | 用户可调 StartAngle~EndAngle |
| 起止角手柄 | 无 | 有 ArcStart, ArcEnd |
| 采样分布 | 完整圆周均匀 | 仅在弧段范围内均匀 |
| 交互复杂度 | 低（类似Circle+采样射线） | 高（需维护角度约束） |
| 适用场景 | 圆度测量、全方位边缘检测 | 扇区边缘检测、局部弧形特征 |

---

## 三、扩展实施计划（分阶段）

### P0: 数据模型层（预估工作量: 0.5天）

| 序号 | 文件 | 改动类型 | 具体改动 |
|------|------|---------|---------|
| 1 | `Models/ShapeType.cs` | **修改** | 新增 `LineCaliper=9, ArcCaliper=10, CircleCaliper=11` 枚举值 |
| 2 | `Models/RegionDefinition.cs` (ShapeParameters类) | **修改** | 新增卡尺字段：RakeAngle, MeasureLength, SampleWidth, SampleCount, ProjectLength 等；Clone() 方法增加新字段复制；getter/setter 辅助方法 |
| 3 | `Rendering/IRegionRenderer.cs` (RegionRenderContext类) | **修改** | 新增卡尺渲染属性：RakeAngle, MeasureLength, SampleWidth, SampleCount, ProjectLength；CalculateBounds() 增加 case 分支 |

**复杂度**: 🟢 低 — 纯数据结构扩展，不影响现有代码路径

**依赖**: 无

---

### P1: 渲染器层（预估工作量: 1.5天）

| 序号 | 文件 | 改动类型 | 具体改动 |
|------|------|---------|---------|
| 4 | `Rendering/WpfShapeRenderer.cs` | **修改** | CreateShape() switch 增加3个case；新增 CreateLineCaliper(), CreateArcCaliper(), CreateCircleCaliper() 方法；UpdateShapeDimensions() 增加对应case；UpdateHelpers() 增加卡尺辅助元素（法向箭头、采样射线）；GetShapePosition() 增加定位策略 |
| 5 | `Rendering/HandleRenderer.cs` | **修改** | 新增 CreateLineCaliperHandles(), CreateArcCaliperHandles(), CreateCircleCaliperHandles() 方法；可能需要新增 HandleType 枚举值（CaliperWidth, ArcStart, ArcEnd, CaliperRadius）；HitTestHandle() 无需改动（通用距离检测已足够） |
| 6 | **新建** `Rendering/CaliperHelper.cs` | **新建** | 卡尺专用辅助计算类（参考 RotatedRectangleHelper 模式）：法向量计算、采样射线端点计算、圆弧几何参数化等 |

**复杂度**: 🟡 中 — 需要精确的几何计算和WPF图形组合

**依赖**: P0

---

### P2: 交互逻辑层（预估工作量: 2-3天）

| 序号 | 文件 | 改动类型 | 具体改动 |
|------|------|---------|---------|
| 7 | `Views/RegionEditorControl.xaml` | **修改** | 工具栏新增3个按钮（LineCaliper, ArcCaliper, CircleCaliper），含图标和ToolTip；新增 Click 事件处理器 |
| 8 | `Views/RegionEditorControl.xaml.cs` | **修改** | **这是最大的改动文件**，需在多处增加分支： |
| | | | a) 快捷键处理（OnPreviewKeyDown）：新增3个快捷键（如 A/D/S 或 I/U/Y） |
| | | | b) SelectShapeTool()：switch 增加中文名称映射 |
| | | | c) ProcessMouseDown()：初始化分支增加3种卡尺类型的默认参数设置 |
| | | | d) UpdateDrawingPreview()：switch 增加卡尺实时预览逻辑 |
| | | | e) FinishInteraction()：验证逻辑增加卡尺尺寸校验规则 |
| | | | f) CreateHandlesForShape()：switch 增加3种卡尺的手柄创建调用 |
| | | | g) CreateRenderContextInternal()：增加卡尺属性到 context 映射 |
| | | | h) ProcessMouseMove() 中的手柄拖拽逻辑：增加卡尺特有手柄的处理（如宽度调节、角度调节） |
| 9 | `ViewModels/ParameterPanelViewModel.cs` | **修改** | 可能需要在参数面板中暴露卡尺特有属性供用户精细调整 |

**复杂度**: 🔴 高 — RegionEditorControl.xaml.cs 已有~2896行，需谨慎添加分支避免引入回归

**依赖**: P1

**风险缓解策略**:
- 所有新增代码使用 `#region 卡尺ROI扩展` 包裹，便于定位
- 每个分支保持与现有形状相同的代码风格和日志格式
- 先实现一种卡尺（推荐 LineCaliper 最简单），验证通过后再做其余两种

---

### P3: ROI模型层（SDK层）（预估工作量: 1天）

| 序号 | 文件 | 改动类型 | 具体改动 |
|------|------|---------|---------|
| 10 | **新建** `Models/Roi/LineCaliperRoi.cs` | **新建** | 实现 IRoi 接口；字段：StartPoint, EndPoint, RakeAngle, MeasureLength, SampleWidth；核心方法：Contains(), GetBoundaryPoints(), CreateMask() |
| 11 | **新建** `Models/Roi/ArcCaliperRoi.cs` | **新建** | 实现 IRoi 接口；字段：Center, Radius, StartAngle, EndAngle, SampleCount, ProjectLength；CreateMask() 使用 OpenCvSharp 的 ellipse/polylines |
| 12 | **新建** `Models/Roi/CircleCaliperRoi.cs` | **新建** | 实现 IRoi 接口；字段：Center, Radius, SampleCount, ProjectLength；CreateMask() 使用 circle() + 射线 mask |
| 13 | `Models/Roi/IRoi.cs` | **修改** | RoiType 枚举确认/调整：考虑将 CircleCaliper 加入（当前值7被Circle占用，需用9或重新编号） |

**复杂度**: 🟢 低 — 标准的 IRoi 实现，有6个现成参考（FullImage/Rectangle/Annulus/Polygon/Circle）

**依赖**: P0（需要确认最终的字段定义一致）

---

### P4: 参数面板绑定（工具集成准备）（预估工作量: 1天）

| 序号 | 文件 | 改动类型 | 具体改动 |
|------|------|---------|---------|
| 14 | `Views/DrawingParameterPanel.xaml(.cs)` | **修改** | 当 ShapeType 为卡尺类型时，显示卡尺特有的参数控件（滑块/输入框）：搜索角度、测量长度、采样宽度、采样数、投射长度 |
| 15 | 工具层参数绑定（LineCaliperTool等） | **待定** | 将工具参数 ↔ ROI属性双向绑定；此阶段属于工具集成本身，不在本次区域编辑器范围内 |

**复杂度**: 🟡 中 — DrawingParameterPanel 已有动态参数切换机制

**依赖**: P3

---

## 四、复用分析

### 4.1 从 RotatedRectangle 可复用的部分

| 可复用项 | 来源位置 | 如何复用 |
|---------|---------|---------|
| 角度系统（图像坐标系：顺时针为正，0°向下） | `RotatedRectangleHelper.NormalizeAngle()` | 直接调用 |
| 旋转手柄位置计算 | `HandleRenderer.CalculateRotationHandlePosition()` | LineCaliper 的旋转手柄直接使用 |
| 方向箭头绘制 | `RotatedRectangleHelper.DrawDirectionArrow()` | LineCaliper 的法向箭头可复用此方法 |
| 旋转光标适配 | `HandleRenderer.GetRotatedCursor()` | 卡尺旋转时的光标变化 |
| 角点计算 | `RotatedRectangleHelper.GetCorners()` | 如卡尺需要包围盒时使用 |

### 4.2 从 Circle 可复用的部分

| 可复用项 | 来源位置 | 如何复用 |
|---------|---------|---------|
| 圆形手柄（4方位+中心） | `HandleRenderer.CreateCircleHandles()` | CircleCaliper 的基础手柄集合 |
| 圆形渲染（Ellipse） | `WpfShapeRenderer.CreateCircle()` | CircleCaliper 的主体轮廓 |
| 半径拖拽逻辑 | RegionEditorControl 中的 Circle 手柄拖拽 | CircleCaliper 的半径调节 |
| CalculateBounds 圆形边界 | `RegionRenderContext.CalculateBounds()` Circle 分支 | CircleCaliper 边界计算 |

### 4.3 从 Line 可复用的部分

| 可复用项 | 来源位置 | 如何复用 |
|---------|---------|---------|
| 端点手柄（LineStart/LineEnd） | `HandleRenderer.CreateLineHandles()` | LineCaliper 的主轴端点 |
| 直线渲染（Line Shape） | `WpfShapeRenderer.CreateLine()` | LineCaliper 的主轴线段 |
| 直线拖拽预览 | UpdateDrawingPreview() Line 分支 | LineCaliper 的绘制预览基础 |
| 直线长度验证 | FinishInteraction() Line 验证 | LineCaliper 的最小尺寸校验 |

### 4.4 必须全新实现的特有逻辑

| 功能 | 说明 | 复杂度 |
|------|------|--------|
| **采样射线渲染** | 从主轴向法向发射的等间距短线段（LineCaliper）/ 从圆弧/圆周向外发射的射线 | 🟡 中 |
| **法向量计算** | 垂直于直线的单位向量，用于确定采样射线方向 | 🟢 低 |
| **圆弧参数化** | StartAngle/EndAngle 到圆弧上采样点的映射 | 🟡 中 |
| **宽度调节手柄** | LineCaliper 特有，位于主轴两侧的宽度控制手柄（需新增 HandleType） | 🟢 低 |
| **起止角手柄** | ArcCaliper 特有，在弧的两端的角度调节手柄（需新增 HandleType） | 🟡 中 |
| **卡尺辅助元素管理** | 法向箭头+采样射线+虚线框的组合渲染和清理（helperPool 管理） | 🟡 中 |
| **CaliperHelper 几何工具类** | 所有卡尺共用的几何计算函数 | 🟡 中 |

---

## 五、风险与缓解

### 5.1 风险矩阵

| # | 风险 | 影响 | 概率 | 缓解措施 |
|---|------|------|------|---------|
| R1 | **RegionEditorControl 2896行改动风险大** | 回归已有6种形状的功能 | 中 | (a) 只加新分支不改原有逻辑 (b) 每个分支用 #region 包裹 (c) 完整回归测试 |
| R2 | **卡尺采样射线实时预览性能** | 大量射线(如36根)时每帧重绘导致帧率下降 | 中 | (a) 限制最大采样显示数为24 (b) 采样射线使用轻量级Line而非Path (c) 缩放自适应：低zoom时不显示射线 |
| R3 | **三种卡尺间切换的数据迁移丢失** | 用户从 ArcCaliper 切换到 CircleCaliper 时丢掉起止角信息 | 低 | (a) 统一基类字段(Center,Radius,SampleCount)自动保留 (b) 特有字段在切换时给合理默认值 (c) 本次不实现自动转换，切形状视为"新建" |
| R4 | **ShapeType 枚举值冲突** | 未来可能与其它功能需求的枚举值碰撞 | 低 | (a) 卡尺类型使用连续区间 9-11 (b) 在枚举注释中明确标注用途范围 |
| R5 | **HandleType 枚举扩展影响命中测试** | 新增手柄类型可能导致误判 | 低 | (a) HitTestHandle() 使用通用距离检测，不依赖具体类型 (b) 新手柄只需保证 Position 和 HandleSize 正确即可 |
| R6 | **RegionRenderContext 字段膨胀** | 添加过多卡尺特有字段使上下文对象臃肿 | 低 | (a) 卡尺特有字段仅在对应 ShapeType 时有效 (b) 考虑未来提取 IShapeExtendedData 子接口（YAGNI，暂不做） |

### 5.2 回归测试检查清单

- [ ] 矩形绘制/选中/拖拽/缩放/删除 — 完整流程
- [ ] 圆形绘制/半径调节/删除
- [ ] 旋转矩形绘制/旋转/方向箭头正确性
- [ ] 直线绘制/端点拖拽
- [ ] 多边形绘制/顶点增删/双击完成
- [ ] 选择模式(V键)/空白处平移
- [ ] 键盘快捷键 Delete/Ctrl+Z/Ctrl+Y/Escape
- [ ] 检测区域/屏蔽区域互斥和颜色区分
- [ ] Zoom 缩放手柄/线宽/标签的自适应
- [ ] Manager 的事件路由正确性（双编辑器场景）

---

## 六、不需要实现的功能（YAGNI）

以下是明确不在本次实现范围内的功能，避免过度设计：

| # | 功能 | 原因 | 未来触发条件 |
|---|------|------|-------------|
| Y1 | **卡尺ROI的序列化/反序列化** | 当前区域编辑器的 RegionDefinition 已有多态 JSON 支持（JsonPolymorphic+JsonDerivedType），卡尺字段自然纳入序列化。但 IRoi 层（SDK）的序列化在工具集成时再做 | LineCaliperTool/ArcCaliperTool/CircleCaliperTool 工具开发时 |
| Y2 | **卡尺ROI之间的自动转换** | 如 ArcCaliper → CircleCaliper 自动锁定 360°。这类智能转换增加复杂度且用户预期不一致 | 用户明确需求 |
| Y3 | **卡尺采样结果的实时叠加显示** | 在编辑器中实时显示边缘检测采样结果需要 CvEngine 集成，超出 UI 层范围 | 工具运行时预览 |
| Y4 | **高级交互（多选批量调整）** | 当前编辑器不支持任何形状的多选 | 区域编辑器重构为多选架构时 |
| Y5 | **卡尺模板/预设管理** | 保存和加载常用卡尺配置参数 | 用户反馈需求 |
| Y6 | **Annulus/Arc 形状的补全实现** | 这两个 ShapeType 已存在于枚举但未实现渲染/交互。它们不是卡尺功能，不应混入本次开发 | 独立需求 |

---

## 七、附录

### 附录 A：文件修改清单

| Phase | 文件路径 | 改动类型 | 预估新增行数 |
|-------|---------|---------|------------|
| **P0** | `src/Plugin.SDK/UI/Controls/Region/Models/ShapeType.cs` | 修改(+3行) | ~3 |
| | `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs` | 修改(+60行) | ~60 |
| | `src/Plugin.SDK/UI/Controls/Region/Rendering/IRegionRenderer.cs` | 修改(+20行) | ~20 |
| **P1** | `src/Plugin.SDK/UI/Controls/Region/Rendering/WpfShapeRenderer.cs` | 修改(+200行) | ~200 |
| | `src/Plugin.SDK/UI/Controls/Region/Rendering/HandleRenderer.cs` | 修改(+150行) | ~150 |
| | `src/Plugin.SDK/UI/Controls/Region/Rendering/CaliperHelper.cs` | **新建**(~250行) | ~250 |
| **P2** | `src/Plugin.SDK/UI/Controls/Region/Views/RegionEditorControl.xaml` | 修改(+40行) | ~40 |
| | `src/Plugin.SDK/UI/Controls/Region/Views/RegionEditorControl.xaml.cs` | 修改(+300行) | ~300 |
| **P3** | `src/Plugin.SDK/Models/Roi/LineCaliperRoi.cs` | **新建**(~150行) | ~150 |
| | `src/Plugin.SDK/Models/Roi/ArcCaliperRoi.cs` | **新建**(~180行) | ~180 |
| | `src/Plugin.SDK/Models/Roi/CircleCaliperRoi.cs` | **新建**(~140行) | ~140 |
| | `src/Plugin.SDK/Models/Roi/IRoi.cs` | 修改(+3行) | ~3 |
| **P4** | `src/Plugin.SDK/UI/Controls/Region/Views/DrawingParameterPanel.xaml(.cs)` | 修改(+80行) | ~80 |
| | **合计** | | **~1576行(新增)** |

### 附录 B：新增文件清单

| 文件路径 | 所属Phase | 用途 |
|---------|----------|------|
| `Rendering/CaliperHelper.cs` | P1 | 卡尺几何计算辅助类 |
| `Models/Roi/LineCaliperRoi.cs` | P3 | 线卡尺 ROI SDK 模型 |
| `Models/Roi/ArcCaliperRoi.cs` | P3 | 弧卡尺 ROI SDK 模型 |
| `Models/Roi/CircleCaliperRoi.cs` | P3 | 圆环卡尺 ROI SDK 模型 |

### 附录 C：关键接口签名预览

```csharp
// ========== CaliperHelper.cs（新建）==========
namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Rendering
{
    public static class CaliperHelper
    {
        // LineCaliper 几何计算
        public static Vector CalcNormalVector(Point start, Point end);
        public static Point[] CalcSamplingRays(Point start, Point end,
            double rakeAngle, double measureLength, double sampleWidth, int count);
        public static Rect CalcLineCaliperBounds(Point start, Point end,
            double measureLength, double sampleWidth);

        // ArcCaliper/CircleCaliper 几何计算
        public static Point[] CalcArcPoints(Point center, double radius,
            double startAngle, double endAngle, int segmentCount);
        public static Point[] CalcCircleSamplingRays(Point center, double radius,
            double projectLength, int sampleCount);

        // 辅助元素绘制
        public static void DrawSamplingRays(Canvas canvas, Point[] rayPairs,
            Brush color, double scale);
        public static void DrawNormalArrow(Canvas canvas, Point origin,
            Vector normal, double length, Brush color, double scale);

        // 常量
        public const string SamplingRayTag = "CaliperSamplingRay";
        public const int MaxDisplaySampleCount = 24;
    }
}

// ========== HandleType 枚举扩展 ==========
// 在现有 HandleType 枚举中新增：
CaliperWidthStart,   // LineCaliper 左侧宽度手柄
CaliperWidthEnd,     // LineCaliper 右侧宽度手柄
ArcStart,            // ArcCaliper 起始角手柄
ArcEnd,              // ArcCaliper 终止角手柄
CaliperRadius        // CircleCaliper/ArcCaliper 半径手柄

// ========== WpfShapeRenderer 新增方法 ==========
private Shape CreateLineCaliper(RegionRenderContext region, ...);
private Shape CreateArcCaliper(RegionRenderContext region, ...);
private Shape CreateCircleCaliper(RegionRenderContext region, ...);

// ========== HandleRenderer 新增方法 ==========
public static EditHandle[] CreateLineCaliperHandles(
    Point startPoint, Point endPoint, double sampleWidth, double scale);
public static EditHandle[] CreateArcCaliperHandles(
    Point center, double radius, double startAngle, double endAngle, double scale);
public static EditHandle[] CreateCircleCaliperHandles(
    Point center, double radius, double scale);

// ========== IRoi 新增类签名 ==========
public class LineCaliperRoi : RoiBase { /* StartPoint, EndPoint, RakeAngle, ... */ }
public class ArcCaliperRoi : RoiBase   { /* Center, Radius, StartAngle, EndAngle, ... */ }
public class CircleCaliperRoi : RoiBase { /* Center, Radius, ... */ }
```

### 附录 D：与 Halcon ROI 系统的对标参考

| Halcon 概念 | 本方案对应 | 差异说明 |
|------------|-----------|---------|
| `gen_measure_pos` (线测量ROI) | **LineCaliperROI** | Halcon 用 Row1/Col1~Row2/Col2 定义轴线和测量长度；本方案增加了 SampleWidth 和 RakeAngle 作为可视化参数 |
| `gen_measure_arc` (弧测量ROI) | **ArcCaliperROI** | Halcon 用 CenterRow/CenterCol/Radius/Extent/StartAngle/AngleRange；基本一一对应 |
| `gen_measure_rectangle2` (旋转矩形测量) | ~~RotatedRectangle~~ | 已有，非本次范围 |
| Halcon 无原生圆环测量 | **CircleCaliperROI** | 本方案创新，Halcon 需用 gen_measure_arc + AngleRange=2π 模拟 |
| `set_draw()` 样式控制 | ShapeParameters 样式属性 | FillColorArgb/StrokeColorArgb/Opacity 对应 Halcon 的 Color/LineStyle |
| ROI 句柄交互 | HandleRenderer 系统 | Halcon 用鼠标拖拽改变 ROI 参数，本方案完全对标该交互范式 |

**Halcon 交互特点对照**:
- Halcon 的测量ROI在 HDevelopWindow 中显示为：轴线 + 法向箭头 + 采样点标记
- 本方案的 LineCaliper 可视化设计与 Halcon 高度一致（轴线+法向+采样范围）
- Halcon 的 arc 测量 ROI 显示为：圆弧 + 起止角标记 + 径向采样线
- 本方案 ArcCaliper 设计与其完全对齐

---

> **文档结束** — 本方案基于 `d:\MyWork\SunEyeVision_Dev\src` 目录下的实际代码调查编写。
