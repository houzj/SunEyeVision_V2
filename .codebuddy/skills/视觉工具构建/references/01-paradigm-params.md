# 参数与判定范式（`ParamValue<T>` + `MeasureThreshold`/`MeasureJudge`）

> 适用于范式线 C（几何测量式）**强制**，范式线 A/B 新增参数同样推荐。
> 基座位置：`src/Plugin.SDK/Execution/Parameters/ParamValue.cs`、`.../MeasureThreshold.cs`

## 1. `ParamValue<T>` 模型（唯一参数载体）

一个参数槽 = **值** + **绑定配置**（常量 / 绑定上游字段两态），序列化一次同时保存两者。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Value` | `T` | 算法层直接读的值；**无论常量还是绑定模式都存具体值** |
| `BindingConfig` | `ParamSetting?` | `null` = 常量模式；非 null = 绑定模式（框架管理） |
| `IsBinding` | `bool` | 是否绑定模式（`[JsonIgnore]`） |
| `ValueSource` | `string` | 调试显示："常量" / "绑定: {节点}.{属性}" |
| `BindingConstantValue` | `object?` | 切到绑定前保存的常量值（切回时恢复） |
| `CreateConstant(T value, string parameterName)` | 静态 | 创建常量模式 |
| `CreateBinding(parameterName, sourceNodeId, sourceProperty, defaultValue)` | 静态 | 创建绑定模式 |

**关键结论**：`Value` 永远有值 → 算法层写 `parameters.Sigma.Value` 即可，**无需关心来源**。上游值由执行器 `ApplyBindings` 注入。

## 2. 参数类骨架（范式线 C，双元素）

```csharp
[JsonDerivedType(typeof(XxxMeasureParameters), "XxxMeasure")]
public class XxxMeasureParameters : RoiParameters   // 无区域需求时改继承 ToolParameters
{
    // ---- ① 判定规格列表（SDK 复用，六工具同构）----
    /// <summary>测量输出项判定规格；可写以便 JSON 反序列化回填</summary>
    public ObservableCollection<MeasureThreshold> Judgments { get; set; } = new()
    {
        new() { Name = "角度", Enabled = true,  Min = 0,     Max = 90,    Unit = "°"  },
        new() { Name = "距离", Enabled = false, Min = 0,     Max = 10,    Unit = "px" },
    };

    // ---- ② 元素1 引导几何（全部 ParamValue）----
    // ★ 必须是可写属性：JSON 反序列化只回填带 setter 的属性，
    //   get-only 会导致方案保存后再打开，引导/卡尺参数回默认值。
    public ParamValue<double> E1StartX { get; set; } = ParamValue<double>.CreateConstant(0, "E1StartX");
    public ParamValue<double> E1StartY { get; set; } = ParamValue<double>.CreateConstant(0, "E1StartY");
    public ParamValue<double> E1EndX   { get; set; } = ParamValue<double>.CreateConstant(0, "E1EndX");
    public ParamValue<double> E1EndY   { get; set; } = ParamValue<double>.CreateConstant(0, "E1EndY");

    // ---- ③ 元素1 卡尺参数（半长口径！）----
    public ParamValue<double> E1MeasureLength1 { get; set; } = ParamValue<double>.CreateConstant(20.0, "E1MeasureLength1");
    public ParamValue<double> E1MeasureLength2 { get; set; } = ParamValue<double>.CreateConstant(5.0,  "E1MeasureLength2");
    public ParamValue<int>    E1NumMeasures    { get; set; } = ParamValue<int>.CreateConstant(10, "E1NumMeasures");
    public ParamValue<double> E1Sigma          { get; set; } = ParamValue<double>.CreateConstant(1.0, "E1Sigma");
    public ParamValue<double> E1Threshold      { get; set; } = ParamValue<double>.CreateConstant(30.0, "E1Threshold");
    public ParamValue<EdgeTransition> E1Transition { get; set; } = ParamValue<EdgeTransition>.CreateConstant(EdgeTransition.All, "E1Transition");
    public ParamValue<EdgeSelect>     E1Select     { get; set; } = ParamValue<EdgeSelect>.CreateConstant(EdgeSelect.All, "E1Select");
    public ParamValue<double> E1DistanceThreshold { get; set; } = ParamValue<double>.CreateConstant(5.0, "E1DistanceThreshold");

    // ---- ④ 元素2：同构 E2* 字段 ----

    // ---- ⑤ 结果显示样式（Tab3 数据驱动；颜色不得自定义）----
    public GraphicsStyle FitLine1Style { get; set; } = new() { Name = "拟合结果1", IsVisible = true };
    public TextStyle     AngleTextStyle { get; set; } = new() { Name = "夹角", IsVisible = true };
}
```

**命名约定**：元素前缀 `E1`/`E2`，字段名与 `CreateConstant` 的 `parameterName` **必须一致**（parameterName 是绑定/调试显示用的标识）。

**参考实现**：`tools/SunEyeVision.Tool.LineLineMeasure/LineLineMeasureParameters.cs`

## 3. XAML 绑定铁律

```xml
<!-- ✅ 正确：绑 ParamValue.Value，外层 ConfigSetting 提供"常量↔绑定"两态切换 -->
<controls:ConfigSetting Label="阈值" ParamValue="{Binding Parameters.E1Threshold}">
    <controls:NumericUpDown Value="{Binding Parameters.E1Threshold.Value, Mode=TwoWay}"
                            Minimum="0" Maximum="255" />
</controls:ConfigSetting>

<!-- ❌ 错误：裸 double 属性 + ConfigSetting → 无效绑定（上游值注入不进来） -->
<controls:ConfigSetting Label="阈值" ParamValue="{Binding Parameters.Threshold}">
    <controls:NumericUpDown Value="{Binding Parameters.Threshold, Mode=TwoWay}" />
</controls:ConfigSetting>
```

| 铁律 | 说明 |
|---|---|
| 一律绑 `.Value` | 控件值绑 `Parameters.Xxx.Value`，`ConfigSetting.ParamValue` 绑整个 `Parameters.Xxx` |
| `Mode=TwoWay` | 输入控件必须双向 |
| 参数属性可写 | `{ get; set; }`，get-only 序列化不回填 |
| 卡尺几何参数单一入口 | 宽/长/数量只在 Tab1 元素面板编辑，**Tab2 禁止重复编辑**（避免两处改同一参数互相覆盖） |

## 4. 枚举参数：ComboBox + 索引翻译

枚举**不直绑**（易静默失配），走 `SelectedIndex` 翻译：

```csharp
// Code-Behind：参数 → UI（同步）
private void SyncEnumsFromParameters()
{
    if (Parameters == null) return;
    E1TransitionCombo.SelectedIndex = (int)Parameters.E1Transition.Value;
    E1SelectCombo.SelectedIndex     = (int)Parameters.E1Select.Value;
}

// Code-Behind：UI → 参数（SelectionChanged）
private void OnE1TransitionChanged(object sender, SelectionChangedEventArgs e)
{
    if (Parameters == null || E1TransitionCombo.SelectedIndex < 0) return;
    Parameters.E1Transition.Value = (EdgeTransition)E1TransitionCombo.SelectedIndex;
}
```

枚举定义顺序即 ComboBox 项顺序（`All, Positive, Negative` ↔ 全部/正极性/负极性）。

## 5. 判定范式（`MeasureThreshold` + `MeasureJudge`）

### 5.1 模型

| 类型 | 成员 |
|---|---|
| `MeasureThreshold`（`ObservableObject`） | `Name` / `Enabled` / `Min` / `Max` / `Unit` / `Clone()` |
| `MeasureJudgeResult`（sealed） | `Threshold` / `Value`（无值 = `double.NaN`）/ `IsPass` / `Message` |
| `MeasureJudge`（static） | `EvaluateAll(thresholds, valueResolver)` → `IReadOnlyList<MeasureJudgeResult>`；`IsAllPass(results)` → `bool` |

### 5.2 工具侧用法（Run 内）

```csharp
var judged = MeasureJudge.EvaluateAll(
    parameters.Judgments,
    t => t.Name switch
    {
        "角度"  => result.Angle,
        "距离"  => double.IsNaN(result.Distance) ? null : result.Distance,
        "交点X" => double.IsNaN(result.IntersectionX) ? null : result.IntersectionX,
        _ => null,      // 未识别项 → null → 若启用则判 NG（rule-015，不静默放过）
    });
result.IsPass = MeasureJudge.IsAllPass(judged);
result.JudgmentDetails = judged.Select(j => j.Message).ToArray();
```

**判定语义（不可改）**：
- `Enabled=false` → 恒 `true`（不参与判定）
- `Enabled=true` 但**取值为 null（无实测值）→ 判 NG**（rule-015，禁止静默当 OK）
- 通过条件：`Min <= value <= Max`（含端点）
- 无任何启用项 → `IsAllPass` 返回 `true`

### 5.3 Tab3 UI（数据驱动，禁止逐项手写）

判定列表用 `ItemsControl` 数据驱动：每行 = `ToggleSwitch`（绑 `Enabled`，控制范围输入可见）+ 名称 + `RangeInputControl`（绑 `Min`/`Max`）+ 单位 + 结果状态色。

> 禁止为每个判定项手写一段 `IsAngleCheckEnabled + AngleMin + AngleMax`——这正是被 `MeasureThreshold` 收敛掉的样板。

## 6. 卡尺口径换算（高频踩坑）

| 层 | 口径 |
|---|---|
| 参数存储 | **半长**（Halcon `measure_length1/measure_length2` 原生语义） |
| UI 显示（Tab1 元素面板） | **全宽 / 全长** = 半长 × 2 |
| 模块结果显示 | **全宽 / 全长** = 半长 × 2 |

```csharp
// Run：结果回显（半长 → 全长）
result.E1CaliberWidth  = parameters.E1MeasureLength1.Value * 2;
result.E1CaliberLength = parameters.E1MeasureLength2.Value * 2;
```

```csharp
// 元素面板：UI ↔ 参数 换算
param.E1MeasureLength1.Value = panelWidth / 2;   // UI 全宽 → 参数半长
panelWidth = param.E1MeasureLength1.Value * 2;   // 参数半长 → UI 全宽
```

> 参数与 UI 若都按"全长"存，则 measure 的实际卡尺长度会翻倍。**必须**在一处换算，禁止两边各存一套。

## 7. 错误对照表

| 症状 | 根因 | 正确做法 |
|---|---|---|
| 上游值注入不进来 | 裸 `double` + `ConfigSetting` | 改 `ParamValue<double>` + 绑 `.Value` |
| 方案保存后重开参数回默认 | 参数属性 get-only | 改 `{ get; set; }` |
| 参数改了结果不变 | Tab1/Tab2 两处编辑同一参数 | 收敛为单一入口 |
| 卡尺长度实际是 UI 显示值的 2 倍 | 口径未换算 | 按 §6 换算 |
| 启用项没测到却判 OK | 手写判定漏 rule-015 | 用 `MeasureJudge.EvaluateAll`，无值 → NG |
| 枚举选择与参数不一致 | 枚举直绑 | ComboBox + 索引翻译 |
