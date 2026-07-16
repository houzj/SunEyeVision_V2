# 几何类型三层分离 + 工具层全链路优化方案

## 🔄 改前 → 改后 → 依据

### 改前（用户视角）
1. **ROI 编辑器画的形状无法被下游绑定**：画个圆 → 存成 `ShapeParameters{ShapeType=Circle, 14个double字段}` → 下游只认 `SdkTypeVocabulary` 词目 → `Circle2d` 是 "Circle" 但存的是 `ShapeParameters` → 类型不匹配，下游使用被阻滞
2. **子程序/脚本工具实现臃肿**：`SubroutineTool.Run()` 160 行内嵌 4 个逻辑块，3 遍 `new ParameterResolver()` + 4 遍 `Split('.')`；`ScriptLifecycleManager` 反射读所有 public 字段当输出
3. **Condition 工具静默吞异常**：`TryConvertToDouble` 的 `catch { return false; }` 让用户完全不知道条件评估出错
4. **ScriptTypeMapper 和 SdkTypeVocabulary 打架**：脚本层 `Circle2d` → `ShapeParameters`，全局层 `Circle2d` → `"Circle"`，同一类型两个映射指向不同 CLR 类型

### 改后（用户视角）
1. **ROI 区域按形状类型输出 SDK 几何对象**：画圆 → `Geometry = Circle2d` → 变量类型 `"Circle"`；画旋转矩形 → `Geometry = RotatedRect2d` → 变量类型 `"RotatedRect"`。下游绑定系统直接识别、展开字段
2. **新增形状类型只需加 1 个 SDK 类型 + 3 行 if**：不需要改枚举、改字段、改 10 处 switch
3. **子程序/脚本工具简洁、fail-fast**：重复解析收归单一服务；3 套 Coerce 收敛为 1 套；类型不匹配直接报错
4. **一个类型映射权威**：`SdkTypeVocabulary` 唯一，`ScriptTypeMapper` 退化为"类型→C# 源码字符串"

### 依据（业界参考）
- **Halcon**：`gen_circle`/`gen_rectangle2`/`gen_region_polygon_filled` 各有独立算子；计量模型 `add_metrology_object(MeasureHandle, 'circle', ...)` 按类型创建
- **VisionPro**：`CogCircle`/`CogRectangle`/`CogRectangleAffine` 各自继承 `CogGraphic`，容器 `CogGraphicCollection` 统一管理
- **OpenCV**：`RotatedRect` 独立 struct，`cv::circle()`/`cv::rectangle()` 各为独立 API
- **MVVM 标准**：UI 模型（ViewModel）不混入领域逻辑，`ShapeParameters` 当前违反此原则

---

## 方案全景

```
┌─────────────────────────────────────────────────────────────┐
│           当前（ShapeParameters 大 union + 10处switch）       │
│                                                             │
│  ShapeParameters 一个类管3件事：                              │
│  ├─ 🎨 UI样式: FillColor/StrokeColor/Opacity               │
│  ├─ 📐 形状定义: ShapeType + 14个double的大union             │
│  └─ 🔗 结果输出: GlobalVarTypeMapper 映射为"Rectangle"       │
│                                                             │
│  新增形状 = 改10处文件 ─── 每次必有遗漏风险                     │
└─────────────────────────────────────────────────────────────┘

                             ↓ 重构

┌─────────────────────────────────────────────────────────────┐
│           改后（3层分离 + SDK类型独立）                        │
│                                                             │
│  ShapeParameters 瘦身为UI包装器：                              │
│  ├─ 🎨 UI样式: FillColor/StrokeColor/Opacity (保留)          │
│  └─ 📐 形状定义 → 委托给 SDK 几何类型: Geometry               │
│       ├── Circle2d     (已有 readonly struct)                │
│       ├── Line2d       (已有 readonly struct)                │
│       ├── Annulus2d    (已有 readonly struct)                │
│       ├── RotatedRect2d (新增 readonly struct)               │
│       ├── Polygon2d    (新增 class, 顶点数不定)               │
│       └── Arc2d        (新增 readonly struct)                │
│                                                             │
│  3条转换通道: Geometry is Xxx → 干净模式匹配                   │
│  ├─ CvRegion.CreateFromRegions → OpenCV/算法层               │
│  ├─ ShapeRenderer → WPF渲染层                                │
│  └─ RegionHitTester + HandleManager → 命中/编辑层             │
│                                                             │
│  输出: Geometry.GetType() → ToTypeVocabulary() → "Circle"     │
│                                                             │
│  新增形状 = SDK加1类型 + 3条 if分支 ─── 零影响已有              │
└─────────────────────────────────────────────────────────────┘
```

---

## 调研记录

### 文件分布

| 文件 | 行号 | 角色 |
|------|------|------|
| `src/Plugin.SDK/Models/Geometry/Circle2d.cs` | — | SDK 几何类型（已有） |
| `src/Plugin.SDK/Models/Geometry/Line2d.cs` | — | SDK 几何类型（已有） |
| `src/Plugin.SDK/Models/Geometry/Annulus2d.cs` | — | SDK 几何类型（已有） |
| `src/Plugin.SDK/UI/Controls/Region/Models/RegionDefinition.cs` | 55-460 | ShapeParameters 定义（大 union 目标） |
| `src/Plugin.SDK/UI/Controls/Region/Models/ShapeType.cs` | 1-58 | ShapeType 枚举 |
| `src/Plugin.SDK/UI/Controls/Region/Models/RegionData.cs` | 1-290 | RegionData 顶层容器 |
| `src/Plugin.SDK/VisionAdapters/CvRegion.cs` | 69-111 | 算法通道 CreateFromRegions |
| `src/Plugin.SDK/UI/Controls/Region/Rendering/ShapeRenderer.cs` | 29-191 | WPF 渲染通道 |
| `src/Plugin.SDK/UI/Controls/Region/Rendering/RegionHitTester.cs` | 117-259 | 命中测试 + 手柄创建 |
| `src/Plugin.SDK/UI/Controls/Region/Logic/HandleManager.cs` | 138-468 | 编辑手柄管理器 |
| `src/Plugin.SDK/Models/SdkTypeVocabulary.cs` | 1-134 | 类型映射权威 |
| `src/Plugin.SDK/Execution/Helpers/ToolRegionHelper.cs` | 25,83,113 | Halcon 区域构建（含 ShapeParameters→HRegion） |
| `tools/SunEyeVision.Tool.Script/Models/ScriptTypeMapper.cs` | 34-74 | 脚本层类型映射（与全局打架） |
| `tools/SunEyeVision.Tool.Subroutine/SubroutineTool.cs` | 全文件 | 子程序工具（160 行内联） |
| `tools/SunEyeVision.Tool.Script/ScriptTool.cs` | 全文件 | 脚本工具 |
| `tools/SunEyeVision.Tool.Script/ScriptEngine/ScriptLifecycleManager.cs` | 213-225 | 反射所有 public 字段 |
| `tools/SunEyeVision.Tool.Script/ScriptEngine/ScriptBindingResolver.cs` | 全文件 | 3 套 Coerce |
| `tools/SunEyeVision.Tool.Condition/ConditionTool.cs` | 121-137 | TryConvertToDouble catch-all |
| `tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs` | 全文件 | Blob 结果类 |
| `tools/SunEyeVision.Tool.ShapeMatch/ShapeMatchResults.cs` | 全文件 | ShapeMatch 结果类 |
| `tools/SunEyeVision.Tool.Threshold/ThresholdResults.cs` | 全文件 | 阈值结果类 |

### SDK 几何类型现状

- 已有：`Circle2d`（readonly struct）、`Line2d`（readonly struct）、`Annulus2d`（readonly struct）
- 缺失：`RotatedRect2d`（readonly struct）、`Polygon2d`（class）、`Arc2d`（readonly struct）
- SDK `Point2D` 是 UI 层模型（`INotifyPropertyChanged`），与 `Circle2d.Center` 的 `System.Windows.Point` 不同

### 序列化现状

- `RegionDefinition.cs:14-17` — 已使用 `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]` + `[JsonDerivedType(typeof(ShapeParameters), "Shape")]`
- 旧 ShapeParameters 序列化含 14 个 double 字段 + ShapeType 枚举 → 删除后直接反序列化失败（零兼容符合 rule-008）

### ScriptTypeMapper 与 SdkTypeVocabulary 冲突

- SdkTypeVocabulary `StringToType["Circle"] = typeof(Circle2d)`
- ScriptTypeMapper `[typeof(Circle2d)] = "...ShapeParameters"` — 同类型两个映射指向不同 CLR 类型

---

## 问题分析（Phase 0）

### 需求拆解

- **表面需求**：整合对话上下文中所有优化诉求，输出最终全链路优化方案
- **根本问题**：`ShapeParameters` 三个职责（UI 样式/形状大 union/结果输出）混在一个类中，导致序列化冗余、类型映射打架、渲染/命中/算法三通道全部 `switch(ShapeType)`。工具层缺乏几何类型输出，下游绑定链断裂
- **问题分类**：
  1. **设计缺陷**（`ShapeParameters` 大 union 设计）→ 需重构
  2. **功能缺失**（工具无几何类型输出）→ 需增量
  3. **实现偏差**（子程序/脚本工具重复解析、Condition 静默吞异常）→ 需重构
  4. **架构冗余**（3 套 Coerce + `ScriptTypeMapper`/`SdkTypeVocabulary` 打架）→ 需收敛

### 架构分配检查

- 几何类型定义最自然的归属：**SDK 层** `src/Plugin.SDK/Models/Geometry/`
- ShapeParameters 当前归属：**SDK UI 层** `RegionDefinition.cs`（含 UI 样式）
- 已有类似机制：`Circle2d`/`Line2d`/`Annulus2d` 已在 SDK Geometry 目录下 — [来源] 文件系统
- 不改是否可覆盖：不可覆盖 — 不重构 ShapeParameters 则永远大 union
- 是否改变分层边界：不改变 — ShapeParameters 仍留在 UI 层，Geometry 类型委托给 SDK 类型

### 方法对比

| 维度 | 方案 A（推荐：三层分离+SDK 类型补齐） | 方案 B（仅加 ToCircle2d 补丁） | 方案 C（不动 ShapeParameters，仅工具层加几何输出） |
|------|:---:|:---:|:---:|
| 实现方式 | ShapeParameters 剥离形状字段→持有 SDK 几何对象；SDK 补 RotatedRect2d/Polygon2d/Arc2d；3 通道改为 `is` 模式匹配 | ShapeParameters 加 ToCircle2d()/ToLine2d() 等转换方法 | 工具 Run() 末尾手动构造几何对象 |
| 架构一致性 | ✅ 类型权归 SDK，UI 层只做包装 | ❌ 类型权仍在 ShapeParameters | ❌ 工具层绕过 SDK 直接构造几何 |
| 复杂度 | 🔴 10+ 文件，30+ 处 switch 修改 | 🟢 1 文件，5 个转换方法 | 🟡 各工具各自加，总量 15+ 行 |
| 维护成本 | ✅ 新增形状只需 SDK 加类型 + 3 条 if | ❌ 每次新增都要改 ShapeParameters 字段 + 所有 switch | ❌ 每次新增都要改所有工具 |
| 扩展性 | ✅ 新增形状独立类，零影响已有 | ❌ 永远大 union，永远 switch 漏改 | ❌ 工具层需要感知所有形状 |
| 长期 ROI | 🏆 3 年持久方案，对标 Halcon/VisionPro | ❌ 6 个月后变成更难维护的大 union | ❌ 12 个月后工具层膨胀 |

---

## Bug 模式分析（Phase 0.5）

### 当前 bug

`ShapeParameters` 大 union 设计 + 3 条转换通道各写一套 `switch(ShapeType)` + `ScriptTypeMapper`/`SdkTypeVocabulary` 对同一类型映射打架。

### 真因分类

**设计缺陷** — `ShapeParameters` 天生就是 `ShapeType` + 14 个 double 字段的大 union，新增形状类型必然需要：

1. `ShapeType` 枚举加值
2. `ShapeParameters` 加字段（可能已有同名字段，语义冲突）
3. `CvRegion.CreateFromRegions` 加 case
4. `ShapeRenderer.CreateShape` 加 case + 创建方法
5. `RegionHitTester.IsPointInRegion` 加 case + 命中测试方法
6. `RegionHitTester.CreateHandles` 加 case + 手柄创建方法
7. `HandleManager.CreateHandles` 加 case + 手柄创建方法
8. `ShapeParameters.Clone` 加字段复制
9. `ShapeParameters.GetMonitorFields` 加 case
10. `SdkTypeVocabulary` 加注册

**每次新增形状类型，至少 10 处修改。** 这就是 bug 模式。

### 同类潜伏点

- `ShapeRenderer.cs:56-77` — switch(shape.ShapeType) 分发
- `RegionHitTester.cs:117-128` — switch 创建手柄
- `RegionHitTester.cs:132-143` — switch 命中测试
- `HandleManager.cs:138-162` — switch 创建手柄
- `CvRegion.cs:81-107` — switch 算法绘制
- `RegionDefinition.cs:382-458` — switch GetMonitorFields
- `ScriptTypeMapper.cs:34-74` — 与 SdkTypeVocabulary 不一致的第二套映射

### 治本点

**修复 `ShapeParameters` 的设计模式**，而非给它的每个新形状加补丁。消除 10 处 switch，改为 SDK 类型 `is` 模式匹配。

---

## 业界对标（Phase 0.7）

| 框架/产品 | 关键特征 | 文档引用 | 借鉴/不借鉴 |
|-----------|---------|---------|------------|
| **Halcon** | 每个形状独立算子（`gen_circle`/`gen_rectangle2`/`gen_region_polygon_filled`），测量模型 `add_metrology_object` 按类型创建 | `create_metrology_model` → `add_metrology_object(MeasureHandle, 'circle', ...)` | 借鉴：形状类型独立 + SDK 层定义类型 |
| **Cognex VisionPro** | `CogCircle`/`CogRectangle`/`CogRectangleAffine`/`CogPolygon` 各自继承 `CogGraphic`；`CogGraphicCollection` 统一容器管理 | `CogFindCircleTool`/`CogFitCircleTool` 等，每个工具有独立结果类 | 借鉴：每个形状独立类的继承体系 |
| **OpenCV** | `RotatedRect` 独立 struct；`cv::circle()`/`cv::rectangle()`/`cv::fillPoly()` 各为独立 API | OpenCV docs: `cv::RotatedRect` class | 借鉴：旋转矩形作为独立几何类型 |
| **CommunityToolkit.Mvvm** | `ObservableObject` + `SetProperty` 单一职责，不在 ViewModel 中混入领域逻辑 | MVVM Toolkit docs | 借鉴：UI 模型与领域模型分离 |
| **VisionMaster** | 工具结果树按类型输出独立结果项，支持 `Point`/`Circle`/`Line`/`Rectangle` | VisionMaster 输出变量面板 | 借鉴：几何类型作为独立输出变量类型 |

---

## 解决方案

### 方案概述

分 6 个模块（Module A~G），按 Phase A→E 分阶段实施，零兼容（rule-008）。

### 技术方案

#### Module A — SDK 几何类型补齐

在 `src/Plugin.SDK/Models/Geometry/` 下新增 3 个类型，与已有 `Circle2d`/`Line2d`/`Annulus2d` 同级。

```csharp
// RotatedRect2d.cs — readonly struct
public readonly struct RotatedRect2d
{
    public double CenterX { get; }
    public double CenterY { get; }
    public double Width { get; }
    public double Height { get; }
    public double Angle { get; }       // 度
}

// Polygon2d.cs — class，顶点数不定
public class Polygon2d : IMonitorable
{
    public Point2D[] Vertices { get; }
    public bool IsClosed { get; }
}

// Arc2d.cs — readonly struct
public readonly struct Arc2d
{
    public double CenterX { get; }
    public double CenterY { get; }
    public double Radius { get; }
    public double StartAngle { get; }  // 弧度
    public double EndAngle { get; }    // 弧度
}
```

#### Module B — ShapeParameters 重构

```csharp
// 改后：只持 UI 样式 + SDK 几何类型
public class ShapeParameters : RegionDefinition, IMonitorable
{
    // 【删除】14 个形状字段
    // 【新增】Geometry
    private object? _geometry;
    public object? Geometry    // Circle2d / Line2d / RotatedRect2d / Polygon2d / Arc2d / Point2D / System.Windows.Rect
    {
        get => _geometry;
        set => SetProperty(ref _geometry, value);
    }

    // 【保留】UI 样式
    public uint FillColorArgb { get; set; }
    public uint StrokeColorArgb { get; set; }
    public double StrokeThickness { get; set; }
    public double Opacity { get; set; }

    // 【新增】类型转换
    public ShapeType ShapeType => GeometryToShapeType(Geometry);

    // 图形编辑操作委托给 SDK 类型
    public Point2D GetCenter() => Geometry switch {
        Circle2d c        => c.Center,
        Line2d l          => l.Point,
        RotatedRect2d rr  => new Point2D(rr.CenterX, rr.CenterY),
        ...
    };
}

// GetMonitorFields 改为 Geometry switch
public IReadOnlyList<(string, object?)> GetMonitorFields() => Geometry switch {
    Circle2d c     => [("中心X", c.Center.X), ("中心Y", c.Center.Y), ("半径", c.Radius)],
    RotatedRect2d r => [("中心X", r.CenterX), ("中心Y", r.CenterY), ("宽", r.Width), ("高", r.Height), ("角度", r.Angle)],
    ...
};
```

序列化变化：`{$type:"Shape", Geometry:{$type:"Circle2d", Center:{X:100,Y:200}, Radius:50}, FillColorArgb:0}`

#### Module C — 3 条转换通道适配

**C1：CvRegion.CreateFromRegions** — switch → `is` 模式匹配

```csharp
// 改前（L81-107）
switch (shape.ShapeType) {
    case ShapeType.Circle: Cv2.Circle(mask, (int)shape.CenterX, (int)shape.CenterY, (int)shape.Radius, ...); break;
    ...
}

// 改后
if (sp.Geometry is Circle2d c)
    Cv2.Circle(mask, (int)c.Center.X, (int)c.Center.Y, (int)c.Radius, ...);
else if (sp.Geometry is RotatedRect2d rr)
    Cv2.RotatedRectangle(mask, new RotatedRect(...), ...);
else if (sp.Geometry is Polygon2d poly && poly.Vertices.Length >= 3)
    Cv2.FillPoly(mask, poly.Vertices.Select(v => new Point((int)v.X, (int)v.Y))...);
```

**C2：ShapeRenderer** — switch → when 分发

```csharp
// 改前（L56-77）
switch (shape.ShapeType) {
    case ShapeType.Rectangle: result = CreateRectangle(shape, ...); break;
    case ShapeType.Circle: result = CreateCircle(shape, ...); break;
    ...
}

// 改后
if (sp.Geometry is Circle2d c)
    result = CreateEllipse(c.Center, c.Radius, fillColorBrush, strokeColor, strokeThickness, isPreview);
else if (sp.Geometry is RotatedRect2d rr)
    result = CreateRotatedRect(rr.CenterX, rr.CenterY, rr.Width, rr.Height, rr.Angle, ...);
```

每个 `CreateXxx(ShapeParameters)` 方法改为接受具体 SDK 类型参数。

**C3：RegionHitTester + HandleManager**

- `CreateHandles(L119)` — `region.ShapeType switch` → `region switch { when sp.Geometry is Circle2d => ... }`
- `IsPointInRegion(L134)` — 同上
- `HandleManager.CreateHandles(L144)` — 同上
- `HandleManager.GetBounds(L358)` — 同上

#### Module D — SdkTypeVocabulary

```csharp
// TypeToString 新增
[typeof(RotatedRect2d)] = "RotatedRect",
[typeof(Polygon2d)]     = "Polygon",
[typeof(Arc2d)]         = "Arc",
[typeof(RotatedRect2d[])] = "RotatedRect",
[typeof(Polygon2d[])]      = "Polygon",

// StringToType 修正
["RotatedRect"] = typeof(RotatedRect2d),   // ← 之前是 ShapeParameters
["Polygon"]     = typeof(Polygon2d),        // ← 之前是 ShapeParameters
["Arc"]         = typeof(Arc2d),            // ← 新增

// ChineseNames 新增
["Arc"] = "弧形",
```

#### Module E — 工具层几何结果输出

每个工具的 `Results` 类新增 `InspectionGeometries` 集合：

```csharp
public List<object?> InspectionGeometries { get; set; } = new();
```

在工具 `Run()` 末尾，遍历 `parameters.InspectionRegions.Regions.Where(r => r.IsEnabled)`：

```csharp
foreach (var rd in parameters.InspectionRegions.Regions.Where(r => r.IsEnabled))
{
    if (rd.Parameters is ShapeParameters sp && sp.Geometry != null)
        result.InspectionGeometries.Add(sp.Geometry);
    // FixedRegion/ComputedRegion — 从运行时 resolved 区域取
}
```

#### Module F — 脚本/子程序/条件工具优化

**F1：SubroutineTool** — 提取 `SubroutineBindingService`
- 消除 3 处 `new ParameterResolver()`
- 消除 4 处 `Split('.')` → 统一 `PropertyPathParser.Parse()`
- `EvaluateBreakCondition` 提取为 `LoopBreakEvaluator`

**F2：ScriptLifecycleManager**
- L213-225 改为按 `OutputConfigs` 白名单反射字段（只读声明的 Name 对应字段）
- 值类型 `is` 校验：`if (value is not TargetType) → 抛异常`

**F3：ScriptBindingResolver** — 3 套 Coerce 收敛
- `CoerceNodeValue` / `CoerceValue` / `ConvertType` 全部委托 `SdkTypeVocabulary` + 单一 `TypeCoercer`

**F4：ConditionTool** — 修复 + 扩展
- 删除 `TryConvertToDouble` 的 `catch { return false; }`
- 支持 string 精确匹配、bool true/false、enum 精确匹配

#### Module G — ScriptTypeMapper 收敛

```csharp
// 改前：独立自建映射表
[typeof(Circle2d)] = "...ShapeParameters"  // ← 与全局矛盾

// 改后：退化为"Type → C# 源码字符串"的辅助
// 类型映射委派给 SdkTypeVocabulary.ToTypeVocabulary(type)
```

---

### 实施步骤

| Phase | 模块 | 内容 | 前置 |
|:-----:|------|------|:----:|
| Phase A | A1-A3 + D1-D4 | SDK 新增 3 个几何类型 + Mapper 注册 | 无 |
| Phase B | B1-B6 + C1-C4 | ShapeParameters 重构 + 3 通道适配 | Phase A |
| Phase C | F4 | Condition 修复 | 无 |
| Phase D | F1-F3 + G1 | 脚本/子程序优化 | 无 |
| Phase E | E1-E6 | 工具层几何输出 | Phase B |

> Phase A/C/D 可并行实施。

### 清理清单

| 文件 | 清理内容 | 原因 |
|------|---------|------|
| `RegionDefinition.cs` | 删除 `_centerX/_centerY/_width/_height/_angle/_radius/_outerRadius/_startX/_startY/_endX/_endY/_startAngle/_endAngle` + `_points` + 对应属性 | 重构到 SDK 几何类型 |
| `RegionDefinition.cs` | `Clone()` 删除 14 个字段的行 | 改为 clone `Geometry` |
| `RegionDefinition.cs` | `GetMonitorFields()` switch 重写 | SDK 类型模式匹配 |
| `ScriptTypeMapper.cs` | 删除几何类型独立映射 | 委托 SdkTypeVocabulary |

### 防御机制

| 防御机制 | 说明 | 验证方式 |
|---------|------|---------|
| 契约测试 | ShapeParameters.Geometry 序列化/反序列化往返一致 | unit test |
| 端到端冒烟 | ROI 编辑器画圆 → 渲染正确 / 命中正确 / 算法绘制正确 / 结果类型正确 | manual |
| Fail-Forward | Geometry 类型不匹配直接报错，rule-015 无静默 | code review |
| 零兼容 | 旧字段删除后序列化立即失败（符合 rule-008），无迁移层 | dotnet build |

---

## 验收标准

### Phase 1：SDK 类型 + ShapeParameters 重构

| # | 验收项 | 验收方法 | 对应防御机制 |
|---|--------|---------|------------|
| 1 | 3 个新 SDK 类型编译通过 | dotnet build | 契约测试 |
| 2 | ShapeParameters.Geometry 存 Circle2d → JSON 序列化 → 反序列化为 Circle2d | unit test | 契约测试 |
| 3 | ShapeParameters.Geometry 存 RotatedRect2d → JSON 序列化 → 反序列化为 RotatedRect2d | unit test | 契约测试 |
| 4 | CvRegion 用 Circle2d 绘制掩码 → 像素级 mask 正确 | unit test | 契约测试 |
| 5 | ShapeRenderer 渲染 RotatedRect2d → 正确 WPF 形状 | manual | 端到端冒烟 |
| 6 | RegionHitTester 命中 Circle2d → 圆内命中、圆外不命中 | unit test | 契约测试 |
| 7 | HandleManager 为 RotatedRect2d 创建 8+2+1 个手柄 | manual | 端到端冒烟 |
| 8 | SdkTypeVocabulary 新增/修正 3 个类型正确映射 | unit test | 契约测试 |

### Phase 2：工具层适配 + 代码优化

| # | 验收项 | 验收方法 | 对应防御机制 |
|---|--------|---------|------------|
| 9 | BlobAnalysis 输出 InspectionGeometries 含有效区域 Geometry | unit test | 契约测试 |
| 10 | ConditionTool 数值转换失败直接抛异常，不静默返回 false | unit test | Fail-Forward |
| 11 | SubroutineTool 无手动 Split('.') 残留 | code review | 契约测试 |
| 12 | ScriptLifecycleManager 按 OutputConfigs 白名单反射字段，非所有 public 字段 | unit test | 契约测试 |
| 13 | ScriptTypeMapper 无独立几何类型映射 | code review | 契约测试 |
| 14 | 几何类型 Geometry 变量在绑定下拉中显示正确类型名（如 "Circle"） | manual | 端到端冒烟 |
| 15 | 3 套 Coerce 已收敛为 1 套 TypeCoercer | code review | 契约测试 |

### 数据一致性验收

| # | 验收项 | 验收方法 | 对应防御机制 |
|---|--------|---------|------------|
| 16 | ShapeParameters 旧字段不可访问 → 编译报错 | dotnet build | 零兼容 |
| 17 | 旧序列化数据（含 ShapeType 枚举）加载直接失败——零兼容预期 | manual | 零兼容 |

---

## 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| RegionEditorControl 大量取 shape.CenterX/Y | 编译错误 | 统一改为 `sp.Geometry` → SDK 属性，编译即验证 |
| ShapeType 枚举在 UI 下拉中使用 | UI 不显示形状列表 | ShapeType 枚举保留作为 UI 辅助，`GeometryToShapeType()` 转换 |
| ToolRegionHelper 含 ShapeParameters 引用 | 编译错误或遗漏 | 代码搜索 + 编译验证 |
| ScriptTypeMapper 使用者尚未清理 | 运行时映射错误 | 删除几何映射后编译报错，强制清理 |

---

## 测试方法

### 单元测试

| 测试文件 | 覆盖模块 | 用例数 | 自动化 |
|---------|---------|:------:|:------:|
| `GeometryTypesTests.cs` | A1-A3 新增类型序列化 | 6 | ✅ |
| `ShapeParametersTests.cs` | B1-B6 Geometry 往返 | 4 | ✅ |
| `CvRegionConversionTests.cs` | C1 算法通道 | 5 | ✅ |
| `RegionHitTests.cs` | C3 命中测试 | 6 | ✅ |
| `MapperRegistrationTests.cs` | D1-D4 映射 | 4 | ✅ |
| `ConditionTests.cs` | F4 | 4 | ✅ |
| `ScriptBindingTests.cs` | F2-F3 | 4 | ✅ |

### 手动验证

1. 打开 ROI 编辑器 → 画圆 → 确认渲染正确 → 确认命中测试正确 → 确认手柄正确
2. 打开 ROI 编辑器 → 画旋转矩形 → 同上
3. 打开工具调试面板 → 查看 InspectionGeometries 输出 → 确认类型名显示正确
4. 下游工具绑定 → 绑定下拉出现 "检测区域1 (Circle)" → 确认字段展开
