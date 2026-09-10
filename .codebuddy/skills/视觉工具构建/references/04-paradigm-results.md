# 结果范式（结果类 + 分组 + 词目 + 可视化）

## 1. 结果类骨架

```csharp
public class XxxMeasureResults : ToolResults
{
    // ---- ① 拟合结果（按元素分组）----
    public Line2d[]? Element1Line { get; set; }
    public double Element1RMSE { get; set; }
    public int Element1EdgePointCount { get; set; }
    public Point2d[] Element1FitPoints { get; set; } = Array.Empty<Point2d>();
    public Point Element1Center { get; set; }
    public double Element1Angle { get; set; }

    // ---- ② 测量结果（元素间几何量）----
    public double Angle { get; set; }
    public double Distance { get; set; }
    public bool IsParallel { get; set; }
    public double IntersectionX { get; set; } = double.NaN;
    public double IntersectionY { get; set; } = double.NaN;

    // ---- ③ 判定 ----
    public bool IsPass { get; set; }
    public string[] JudgmentDetails { get; set; } = Array.Empty<string>();

    // ---- ④ 输入回显 ----
    public RoiShape[]? GuideGeometry1 { get; set; }
    public int E1CaliberCount { get; set; }
    public double E1CaliberWidth { get; set; }      // 全宽（= 参数半长 × 2）
    public string E1CaliberTransition { get; set; } = string.Empty;

    /// <summary>属性映射（结果树/输出契约 SSOT；必须合并基类获得执行耗时）</summary>
    public override List<PropertyMapEntry> GetPropertyMap() => 
    [
        .. base.GetPropertyMap(),   // 执行耗时 + 节点状态 → 执行信息

        // ===== 输入回显（对齐规范：输入区域/卡尺）=====
        new("GuideGeometry1", "引导几何1", "输入区域/卡尺/引导线"),
        new("E1CaliberCount", "卡尺1测量带数量", "输入区域/卡尺/测量带"),
        new("E1CaliberWidth", "卡尺1测量带宽度", "输入区域/卡尺/测量带"),
        new("Element1FitPoints", "卡尺1检测点", "输入区域/卡尺/测量带"),

        // ===== 拟合结果（按元素分组）=====
        new("Element1Line", "拟合直线1", "拟合结果/元素1"),
        new("Element1RMSE", "拟合残差1", "拟合结果/元素1"),
        new("Element1Angle", "元素1角度(°)", "拟合结果/元素1"),

        // ===== 测量结果 =====
        new("Angle", "两线夹角(°)", "测量结果"),
        new("Distance", "法向投影距离(px)", "测量结果"),
        new("IsParallel", "是否平行", "测量结果"),
    ];
}
```

**铁律**：
- 继承 `ToolResults`；`GetPropertyMap()` **必须** `.. base.GetPropertyMap()` 合并基类（否则丢"执行耗时"）
- 属性名 = 输出契约名（上游绑定的 `SourceProperty`）
- 无值几何量用 `double.NaN` 表示（`MeasureJudge` 判 NG，不静默）

## 2. 模块结果分组顺序（SSOT）

分组顺序由 `src/UI/Shell/Windows/MainWindowViewModel.cs` 的 `ModuleResultGroupOrder` 决定（未知分组排最后）：

```
执行信息            ← base.GetPropertyMap()：执行耗时 + 节点状态(判定 OK/NG)
拟合结果            ← 算法产出（紧跟执行信息）
拟合结果/元素1
拟合结果/元素2
测量结果
基本信息
输入信息
输入区域/卡尺/引导线  ← 输入回显向后排（引导线 → 测量带）
输入区域/卡尺/测量带
输入区域
参数回写            ← 最后
```

**新工具特有分组必须先登记 `ModuleResultGroupOrder`**，否则排到最末（视觉上错位）。

### 推荐分组约定（测量类工具）

| 内容 | 分组 |
|---|---|
| 引导几何（输入回显） | `输入区域/卡尺/引导线` |
| 卡尺参数回显 + 检测点 | `输入区域/卡尺/测量带` |
| 每元素拟合结果 | `拟合结果/元素1`、`拟合结果/元素2` |
| 元素间几何量 | `测量结果` |

## 3. 词目表硬约束

- `GetOutputVariables` **由 `GetPropertyMap` 派生**，不单独手写
- 暴露的属性类型**必须命中 `SdkTypeVocabulary` 已登记词目**；未映射类型 → **直接报错**（rule-015，禁止静默回退）
- 几何结果以 `Point2d` / `Line2d` / `Circle2d` / `RoiShape[]` 等契约类型暴露；显示层再做 WPF 转换
- 数值单位不写进属性名语义（除必要的 `(px)`/`(°)` 显示后缀），单位归显示层

## 4. 可视化（`FillDisplay` + 样式）

`FillDisplay(bool isOk, XxxParameters parameters)` 是**工具结果类的自定义方法**（非基类 API），Run 末尾统一调用：

```csharp
result.FillDisplay(result.IsPass, parameters);   // Run 末尾统一装配
```

> 参考实现：`LineLineMeasureResults.cs:148`、`LineDetectResults.cs:110`、`CircleDetectResults.cs:102`

- 可视化项样式取自**参数类**的 `GraphicsStyle` / `TextStyle`（`IsVisible` 由 Tab3 开关控制）
- **颜色铁律**：`OkColor`/`NgColor` 必须保留 SDK 默认（OK 绿 / NG 红），工具层**不得**自定义颜色；确需特定色须在差异点清单注明理由并与评审讨论
- **文本锚点铁律**：同一几何点的多条文本传**同一锚点**，由渲染层按锚点分组自动排版；需要微偏移一律用 `AddText(style, anchorX, anchorY, content, screenOffsetX, screenOffsetY)` 的**屏幕像素偏移**，禁止用图像坐标手写 ±N

### 常见可视化项（测量类）

| 项 | 类型 | 说明 |
|---|---|---|
| 引导线 | `GraphicsStyle` | 实线，表示卡尺走向 |
| 测量带 | `GraphicsStyle` | 由 `.BuildLineBands(start, end, halfWidth*2, halfLength*2, count)` 生成 |
| 拟合几何 | `GraphicsStyle` | 拟合直线/圆（实线） |
| 无限直线延长段 | `GraphicsStyle` | 虚线，表示"交点取自无限直线"；需按 `ImageWidth/ImageHeight` 裁剪 |
| 交点 | `GraphicsStyle`（`CrossSize`） | 角点十字 |
| 夹角弧 / 距离连线 | `GraphicsStyle` | 角标弧 / 公垂段 |
| 数值文本 | `TextStyle` | 夹角/距离/中心/角度 |

## 5. 结果树显示格式（`FormatTypedValue`，框架侧）

| 类型 | 显示 |
|---|---|
| `double` / `float` | `F3` |
| `bool` | 是 / 否 |
| `null` | `--` |
| `T[1]` | 解包显示单值 |
| `double[]` / `int[]` | 分号连接实际值 |

> 因此 `IsParallel` 等 `bool[1]` 语义项按单值显示，无需工具侧特殊处理。
