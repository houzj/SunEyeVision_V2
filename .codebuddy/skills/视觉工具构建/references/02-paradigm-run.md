# Run 范式（图像处理式 / 引导检测式 / 几何测量式）

## 1. 通用骨架（所有范式线共用）

```csharp
public XxxResults Run(XxxParameters parameters)
{
    var result = new XxxResults
    {
        ToolName = _attribute?.DisplayName ?? "默认名称",
        ToolId   = _attribute?.Id ?? "XxxTool",
        Timestamp = DateTime.Now,
    };

    try
    {
        // ===== 参数校验（Fail-Fast，缺参数直接抛）=====
        // ===== 算法主体 =====
        // ===== 结果落位 =====
        // ===== 判定 =====
        result.FillDisplay(result.IsPass, parameters);   // 可视化项按参数样式装配
        result.SetSuccess();                              // 或 SetSuccess(durationMs)
    }
    catch (Exception ex)
    {
        parameters.LogError($"XXX 失败: {ex.Message}", ex);
        result.SetError($"XXX 失败: {ex.Message}", ex);
    }
    return result;
}
```

**铁律**：
- `[Tool(..., HasDebugWindow = true)]` + `IToolPlugin<TParameters, TResults>`
- 构造函数缓存 `ToolAttribute`（`GetType().GetCustomAttribute<ToolAttribute>()`）供 `ToolName`/`ToolId` 回填
- `CreateDebugControl(IToolPlugin tool) => new Views.XxxToolDebugControl();`；`CreateDataAnalysisControl() => null;`
- 异常：`LogError` + `SetError`（**禁止**静默吞异常，rule-008/rule-015）
- 结果落位顺序固定：先数值 → 再输入回显 → 再判定 → 最后 `FillDisplay`

---

## 2. 范式线 A：图像处理式（算子链 + 区域前置两段式）

```csharp
// ① 输入图像
var hImage = parameters.GetHalconImage();                     // 需 using SunEyeVision.HalAdapter;
if (hImage == null || !hImage.IsInitialized())
    throw new InvalidOperationException("输入图像为空或无效");

// ② 区域解析（纯几何，不接收图像对象）
using var scope = HalconRegionPipeline.Resolve(
    imageWidth, imageHeight,
    parameters.DetectionRegion, parameters.MaskRegion,
    nodeResults, correctionMatrix);

// ③ 执行方式自决：按域 → Restrict（谁 Restrict 谁释放）；全图 → 直接用原图
using var domainImage = HalImageDomain.Restrict(grayImage, scope.RoiRegion);

// ④ 算子链（HOperatorSet 静态方法，禁止 HRegion 实例方法）
HOperatorSet.Threshold(domainImage, out var region, minGray, maxGray);
HOperatorSet.Connection(region, out var connected);
using var regions = new HRegion(connected);

// ⑤ 统计/判定 → 结果
```

**要点**：
- `HalconRegionPipeline.Resolve` 签名**不含图像对象**；`RoiScope` 用后 `Dispose()`（只释放 region，防重入）
- 位置修正矩阵：交互层与 Run **同源**（`PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource)`）
- 拓扑变更算子一律 `HOperatorSet` 静态方法（rule-014）
- 区域启用开关可选（`IsRegionEnabled`，参考 Threshold）

---

## 3. 范式线 C：几何测量式（Metrology 卡尺，v4 定稿）

```csharp
// ① 参数校验（引导几何有效性）
var e1Start = new Point(parameters.E1StartX.Value, parameters.E1StartY.Value);
var e1End   = new Point(parameters.E1EndX.Value,   parameters.E1EndY.Value);
if ((e1End - e1Start).Length < 1e-6)
    throw new InvalidOperationException("卡尺1 引导直线未设置或长度无效");
// 元素2 同理

// ② 输入图像
var hImage = parameters.GetHalconImage();
if (hImage == null || !hImage.IsInitialized())
    throw new InvalidOperationException("输入图像为空或无效");

// ③ 位置修正（交互层与 Run 同源）
AffineMatrix? matrix = null;
if (parameters.IsPositionCorrectionEnabled)
    matrix = PositionCorrectionResolver.ResolveFirstMatrix(parameters.PositionCorrectionBindingSource);

// ④ 单 metrology 模型承载两个元素
using var metrologyModel = new HMetrologyModel();
metrologyModel.CreateMetrologyModel();
HOperatorSet.GetImageSize(hImage, out var width, out var height);
metrologyModel.SetMetrologyModelImageSize((int)width.L, (int)height.L);
result.ImageWidth  = (int)width.L;      // 供结果显示裁剪"无限直线延长段"
result.ImageHeight = (int)height.L;

var e1StartT = Transform(e1Start, matrix);   // 引导先做位置修正
var e1EndT   = Transform(e1End, matrix);
var e1Index = metrologyModel.AddMetrologyObjectLineMeasure(
    e1StartT.Y, e1StartT.X, e1EndT.Y, e1EndT.X,
    parameters.E1MeasureLength1.Value, parameters.E1MeasureLength2.Value,   // 半长口径
    parameters.E1Sigma.Value, parameters.E1Threshold.Value,
    BuildGenParamName(), BuildGenParamValue(...));
// 元素2 同理

// ⑤ 一次 apply 执行全部元素
metrologyModel.ApplyMetrologyModel(hImage);

// ⑥ 逐元素取测点 + 鲁棒拟合
var e1Points = ExtractDiagnosticPoints(metrologyModel, e1Index, transition);
var (e1FitStart, e1FitEnd, e1Inliers, e1Rmse) = FitLineRobust(e1Points, distanceThreshold);

// ⑦ 几何量（HALCON 算子优先，见 §4）
// ⑧ 判定（MeasureJudge，见 references/01 §5）
result.IsPass = MeasureJudge.IsAllPass(judged);
result.FillDisplay(result.IsPass, parameters);
result.SetSuccess();
```

**元素几何类型对应算子**：

| 元素 | 添加方法 | 结果提取 |
|---|---|---|
| 直线 | `AddMetrologyObjectLineMeasure` | `GetMetrologyObjectResult` / 测点 → `FitLineContourXld` |
| 圆 | `AddMetrologyObjectCircleMeasure` | `GetMetrologyObjectResult`（圆拟合） |
| 椭圆/弧 | 对应 `AddMetrologyObjectEllipseMeasure` 等 | — |

**鲁棒拟合两段式（推荐）**：先用 `tukey` 拟合 → 按 `distanceThreshold` 剔除外点 → 若剔除了点则用 `regression` 对内点重拟合；内点 < 2 直接抛"拟合内点不足"（不静默降级）。

**gen_param 翻译（枚举 → Halcon 字符串）**：

```csharp
private static HTuple BuildGenParamName() => new(new[]
{
    "num_measures", "measure_transition", "measure_select", "distance_threshold",
});

private static string TransitionToHalcon(EdgeTransition t) => t switch
{
    EdgeTransition.Positive => "positive",   // 亮到暗
    EdgeTransition.Negative => "negative",   // 暗到亮
    _ => "all",
};

private static string SelectToHalcon(EdgeSelect s) => s switch
{
    EdgeSelect.First => "first", EdgeSelect.Last => "last", _ => "all",
};
```

> `measure_transition` 的极性语义以本机 HALCON 官方文档为准（可用 `[skill:halcon-docs]` 核对），避免社区二手资料矛盾说法。

---

## 4. 几何量铁律：HALCON 算子优先（rule-004）

| 几何量 | HALCON 算子 | 说明 |
|---|---|---|
| 两线夹角 | `AngleLl` | 有向角 -π~π，需锐角化（`abs > π/2 → π - abs`） |
| 点到线 / 点到线集 | `DistancePl` | **支持参数广播**：N 个点 + 1 条直线 → N 个正交距离 |
| 线到线 / 线到点集 | `DistanceSl` / `DistanceSs` | — |
| 两线交点 + 平行判定 | `IntersectionLl` | **自带 `isParallel` 输出**，替代自研角度硬阈值 |
| 点-点距离 | `DistancePp` | — |
| 直线拟合 | `FitLineContourXld` | 输出含各点残差 `Dist`，可算 RMSE |

**自研例外条款**（必须命中其一，且在差异点清单注明理由）：
- (a) HALCON 无对应算子
- (b) 纯显示层计算（不参与测量与绑定）
- (c) 性能热点（且已实测证明）

---

## 5. 结果落位与可视化约定

| 项 | 约定 |
|---|---|
| 输入回显 | 引导几何 → `RoiShape[]`（`BuildGuideGeometry`）；卡尺 → 数量/全宽/全长/极性字符串 |
| 中间量 | 拟合内点 `Point2d[]`、RMSE、测点数量 → 供"拟合结果/元素N"分组 |
| 判定 | `IsPass` + `JudgmentDetails`（每条 Message） |
| 可视化 | `result.FillDisplay(result.IsPass, parameters)`（样式取自参数类，颜色不得自定义） |
| 成功标记 | `result.SetSuccess()` / `SetSuccess(durationMs)` |

> `FillDisplay(isPass, parameters)` 是本项目统一入口：按参数类中的 `GraphicsStyle`/`TextStyle` 装配图形/文本项，避免 Run 内散落绘制代码。
