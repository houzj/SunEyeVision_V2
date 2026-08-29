---
name: CV引擎封装
description: CV引擎封装技能。用于将OpenCV Cv2调用封装到CvEngine层，对齐Halcon算子。触发场景：发现工具层重复的Cv2调用、需要封装新的Halcon算子对应方法、优化CvRegion多区域数据模型。
---

# CV引擎封装技能

## 触发场景

- 发现工具层有重复的 `Cv2` 调用
- 需要封装新的 Halcon 算子对应方法
- 消除跨工具的重复代码
- **优化 CvRegion 多区域数据模型（labels 缓存 + 子区域索引访问）**

## 封装判断标准

### ✅ 值得封装

| 条件 | 说明 |
|------|------|
| 有 Halcon 算子对应 | CvEngine 的设计目标就是对齐 Halcon |
| 消除跨工具重复 | DRY 原则，多处使用同一模式 |
| 领域语义增值 | 封装后比直接调用 `Cv2.Xxx()` 语义更清晰 |

### ❌ 不值得封装

| 模式 | 说明 |
|------|------|
| 1:1 薄包装 | `CvImage.CountNonZero()` 不比 `Cv2.CountNonZero(img)` 更好 |
| 简单单行调用 | 语义已经足够清晰，无需额外抽象 |
| UI/调试代码 | Debug 控件中的 Cv2 调用不属于核心算法 |

## CvEngine 架构

```
SunEyeVision.CvEngine/
├── CvImage.cs          # 图像封装，对应 Halcon HImage
├── CvRegion.cs         # 区域封装，对应 Halcon HObject（支持 0~N 子区域）
├── CvOp.cs             # 算子集合（所有区域操作的唯一入口）
├── CvConvert.cs        # 类型转换工具
└── Stats/
    └── ImageStats.cs   # 图像统计结果结构
```

## CvRegion 多区域数据模型（Labels-Centric 设计）

### 设计原则

1. **方法名**：严格对齐 Halcon 算子命名（Connection / AreaCenter / SelectShape / Split / SelectObj）
2. **参数签名**：以 OpenCV 最优实践为主，不机械照搬 Halcon 参数布局
3. **索引约定**：统一使用 **0-based**（对齐 C# 和 OpenCV CC 输出结构），不跟随 Halcon 的 1-based
4. **数据模型**：以 `ConnectedComponentsWithStats` 产出的 labels 为核心，stats/centroids 与 labels 天然对齐
5. **性能目标**：一次 CC 分析，下游所有算子零开销复用

### 为什么 Labels 是 OpenCV 的核心优势

```
OpenCV ConnectedComponentsWithStats 一次性产出三个天然对齐的 Mat:

labels (CV_32SC1)    — 每像素标签值: labels[y,x] = k 表示属于第 k 个子区域
stats (CV_32SC1)     — 每行对应一个子区域的统计: stats[k, :] = {CC_STAT_LEFT, TOP, WIDTH, HEIGHT, AREA}
centroids (CV_64FC1) — 每行对应一个子区域的质心: centroids[k, :] = {cx, cy}

天然对齐关系:
  取第 i 个子区域掩码 → Compare(labels, i+1, mask, EQ)      ← O(像素数)
  取第 i 个子区域面积 → stats.At<int>(i+1, CC_STAT_AREA)     ← O(1)
  取第 i 个外接矩形   → Rect(stats[i+1,0], ..., stats[i+1,3])← O(1)
  取第 i 个质心       → centroids.At<double>(i+1, 0/1)        ← O(1)

Halcon 无法直接做到这一点——每次 area_center/select_shape 都需要重新遍历内部数据。
CvRegion 缓存这组 Mat 后，下游算子的特征查询成本从 O(N*像素) 降到 O(N) 或 O(1)。
```

### 数据字段

| 字段 | 类型 | 说明 | 生命周期 |
|------|------|------|---------|
| `_mask` | Mat? (CV_8UC1) | 合并二值掩码，始终可用 | 始终存在 |
| `_labels` | Mat? (CV_32SC1) | 标签图，0=背景, 1..N=子区域 | **仅多区域结果** |
| `_stats` | Mat? (CV_32SC1) | CC 统计数据（与 labels 行对齐） | **仅多区域结果** |
| `_centroids` | Mat? (CV_64FC1) | CC 质心数据（与 labels 行对齐） | **仅多区域结果** |
| `_count` | int | 子区域数量 (= stats.Rows - 1) | 始终存在 |
| `_imageSize` | Size | 图像尺寸 | 始终存在 |

### 存储策略矩阵

| 场景 | _mask | _labels | _stats | _centroids | _count |
|------|-------|---------|--------|-----------|--------|
| `GenEmptyObj()` | null | null | null | null | 0 |
| `GenRectangle1()` / `FromMask()` / `Threshold()` 结果 | ✅ | null | null | null | 1 |
| **`Connection()` 结果** | ✅ 从 labels 派生 | ✅ 直接存储 | ✅ 直接存储 | ✅ 直接存储 | N |
| **`SelectShape()` 结果** | ✅ 过滤重建 | ✅ 过滤+重编号 | ✅ 过滤复制行 | ✅ 过滤复制行 | M |
| 形态学操作 (`Dilation/Erosion/Opening/Closing`) | ✅ 新建 | **null** | **null** | **null** | 1 |
| 集合运算 (`Union1/Difference/Intersection`) | ✅ 计算 | **null** | **null** | **null** | 1 |
| `Concat()` 结果 | ✅ OR 合并 | **null** | **null** | **null** | count累加 |

> **为什么形态学/集合运算丢弃 labels？**
> 这些操作的输出不再是原连通域的子集——Dilation 可能合并相邻区域，
> Union1 将多区域合为一体。此时 labels 编号已失效，重新跑 CC 才有意义。
> 下游如需子区域访问，调用 Connection 即可。

### 双构造函数

```csharp
// 单区域构造 — FromMask/Threshold/形态学/集合运算 使用
internal CvRegion(Mat? mask, int count, Size imageSize);

// 多区域构造 — Connection/SelectShape 使用（含完整 CC 分析数据，所有权转移）
internal CvRegion(Mat? mask, Mat labels, Mat stats, Mat centroids, int count, Size imageSize);
```

### 内部属性（仅 CvEngine 内部访问）

```csharp
internal Mat? Mask => _mask;                                    // 掩码（始终可用）
internal bool HasLabels => _labels != null && !_labels.Empty(); // 是否有缓存可复用
internal Mat? Labels => _labels;                                // 标签图（与 stats/centroids 行对齐）
internal Mat? Stats => _stats;                                  // CC 统计（[0]=背景, [1..N]=子区域）
internal Mat? Centroids => _centroids;                          // CC 质心（同上索引）
```

### 生命周期管理

- **CopyObj()**: 深拷贝全部 4 个 Mat（`_mask` + `_labels` + `_stats` + `_centroids`）
- **Dispose()**: 释放全部 4 个 Mat 并置 null
- **Finalizer**: 释放全部 4 个 Mat（安全网）

### Labels 生命周期规则

```
┌──────────────┐   产出 labels   ┌───────────────┐
│ Connection()  │ ──────────────→ │ 多区域 CvRegion │
│ (基础产出者)   │                │ 含 labels+stats │
└──────────────┘                │ +centroids     │
        ↑                        └───────┬───────┘
        │ 复用 + 重建 labels              │ 消费者（零 CC 开销）
        │                                ├──→ AreaCenter()  读 stats+centroids
        │                                ├──→ Split()       读 labels
┌───────┴────────┐                     ├──→ SelectObj()    读 labels
│ SelectShape()   │ ── 重建 labels ──→  └──→ (未来新算子)  读任意组合
│ (筛选后重建)    │
└────────────────┘

破坏者（输出清空 labels）:
  DilationCircle, ErosionCircle, OpeningCircle, ClosingCircle
  Union1, Difference, Intersection, Complement, Concat
```

## CvOp 算子实现规范（Labels-Centric 优化版）

> **命名规则**：方法名对齐 Halcon 算子，参数签名以 C#/OpenCV 惯例为准（0-based index、out 参数等）

### Connection — 连通域分析（labels 基础产出者）

**职责**：调用 `ConnectedComponentsWithStats` 一次，产出完整 CC 数据并转移所有权给 CvRegion。

```csharp
/// <summary>
/// 连通域分析 - 对应 Halcon connection(Region : : : ConnectedRegions)
/// </summary>
public static void Connection(CvRegion region, out CvRegion connectedRegions,
    CvConnectivity connectivity = CvConnectivity.Connect8)
{
    var mask = region.Mask;
    if (mask == null || mask.Empty()) { connectedRegions = GenEmptyObj(); return; }

    // CC 分析 — OpenCV 一次性产出 labels + stats + centroids
    var labels = new Mat();      // CV_32SC1, [H x W]
    var stats = new Mat();       // CV_32SC1, [numLabels x 5]
    var centroids = new Mat();   // CV_64FC1, [numLabels x 2]

    int numLabels = Cv2.ConnectedComponentsWithStats(
        mask, labels, stats, centroids,
        CvConvert.ToOpenCvConnectivity(connectivity));

    if (numLabels <= 1)  // 仅背景或无连通域
    {
        labels.Dispose(); stats.Dispose(); centroids.Dispose();
        connectedRegions = new CvRegion(
            new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black),
            0, region.ImageSize);
        return;
    }

    // 从 labels 派生 mask（labels > 0 的像素 = 有效区域）
    // 保证 mask 与 labels 天然一致性
    using var derivedMask = new Mat();
    Cv2.Compare(labels, 0, derivedMask, CmpType.GT);

    // 所有权转移给 CvRegion（不使用 using）
    connectedRegions = new CvRegion(
        derivedMask.Clone(), labels, stats, centroids,
        numLabels - 1, region.ImageSize);
}
```

### AreaCenter — 零开销读取缓存

**快速路径**：直接读 `stats` + `centroids`（O(N)，零 CC 调用）。

```csharp
/// <summary>
/// 面积和质心查询 - 对应 Halcon area_center(Regions : : : Area, Row, Column)
/// </summary>
public static void AreaCenter(CvRegion region,
    out (double Area, double Row, double Column)[] areaCenters)
{
    if (region.IsEmpty)
    {
        areaCenters = Array.Empty<(double, double, double)>();
        return;
    }

    // 快速路径：复用已缓存的 stats/centroids（零 CC 开销）
    if (region.Stats != null && !region.Stats.Empty()
        && region.Centroids != null && !region.Centroids.Empty())
    {
        var stats = region.Stats;
        var centroids = region.Centroids;
        int n = stats.Rows;           // 含背景行: n-1 个子区域
        areaCenters = new (double, double, double)[n - 1];
        for (int i = 1; i < n; i++)   // 跳过 i=0（背景标签）
            areaCenters[i - 1] = (
                stats.At<int>(i, (int)ConnectedComponentsTypes.Area),   // O(1)
                centroids.At<double>(i, 1),                               // cy (Row)
                centroids.At<double>(i, 0));                              // cx (Column)
        return;
    }

    // 慢速路径：无缓存时重新跑 ConnectedComponentsWithStats（与当前实现相同）
    // ...
}
```

### SelectShape — 复用缓存 + 重建 labels

**核心决策**：输出 **保留 labels 信息**（过滤后重编号），使下游继续受益。

```csharp
/// <summary>
/// 形状筛选 - 对应 Halcon select_shape(Regions : Selected : Feature, Min, Max)
/// </summary>
public static void SelectShape(CvRegion region, out CvRegion selected,
    int minArea, int maxArea,
    double minCircularity = 0, double maxCircularity = 1,
    double minRectangularity = 0, double maxRectangularity = 1)
{
    var mask = region.Mask;
    if (mask == null || mask.Empty()) { selected = GenEmptyObj(); return; }

    bool hasCached = region.HasLabels && region.Stats != null;

    // 快速路径：复用已有 labels+stats（零 CC 开销）
    Mat labels, stats;
    int numLabels;

    if (hasCached)
    {
        labels = region.Labels!;     // 只读引用，不拷贝
        stats = region.Stats!;
        numLabels = stats.Rows;
    }
    else
    {
        // 慢速路径：重新计算 CC
        labels = new Mat(); stats = new Mat();
        using var tmpCentroids = new Mat();
        numLabels = Cv2.ConnectedComponentsWithStats(
            mask, labels, stats, tmpCentroids, PixelConnectivity.Connectivity8);
    }

    // ... 过滤逻辑（从 stats 读面积 O(1)，形状特征按需计算）...
    // 收集 filteredIndices (基于 1-based CC 标签值)

    // 构建结果：重建 labels/stats/centroids（过滤 + 重编号 1..M）
    int M = filteredIndices.Count;
    var resultMask = BuildMergedMask(labels, filteredIndices, region.ImageSize);
    var resultLabels = BuildFilteredLabels(labels, filteredIndices, region.ImageSize);  // 重编号
    var resultStats = FilterRows(stats, filteredIndices);       // 复制对应行
    var resultCentroids = FilterRows(region.Centroids!, filteredIndices);

    selected = new CvRegion(resultMask, resultLabels, resultStats, resultCentroids,
        M, region.ImageSize);
}
```

### Split — 零开销拆分

```csharp
/// <summary>
/// 将多区域拆分为单区域数组 - 对应 Halcon connection + 遍历子区域
/// </summary>
public static void Split(CvRegion region, out CvRegion[] regions)
{
    if (region.IsEmpty) { regions = Array.Empty<CvRegion>(); return; }

    // 快速路径：复用 labels（零 CC 开销）
    if (region.HasLabels)
    {
        int n = region.Count;          // 子区域数量
        var labels = region.Labels!;
        regions = new CvRegion[n];
        for (int i = 0; i < n; i++)
        {
            // CC 标签值 = i + 1（0 是背景）
            using var blobMask = new Mat();
            Cv2.Compare(labels, i + 1, blobMask, CmpType.EQ);
            regions[i] = CvRegion.FromMask(blobMask);
        }
        return;
    }

    // 慢速路径：重新计算 ...
}
```

### SelectObj — 新增（0-based 子区域索引访问）

**对应 Halcon**：`select_obj(Regions, ObjectTrans, Index1, Index2)`
**索引约定**：**0-based**（对齐 C# 和 OpenCV，非 Halcon 的 1-based）

```csharp
/// <summary>
/// 选取指定索引的子区域 - 对应 Halcon select_obj
/// </summary>
/// <param name="region">多区域对象（需有 labels 缓存）</param>
/// <param name="index">子区域索引（0-based，范围 0..Count-1）</param>
/// <param name="singleRegion">选中的单个子区域</param>
public static void SelectObj(CvRegion region, int index, out CvRegion singleRegion)
{
    if (index < 0 || index >= region.Count) { singleRegion = GenEmptyObj(); return; }
    if (!region.HasLabels) { singleRegion = GenEmptyObj(); return; }

    // CC 内部标签值 = index + 1
    using var blobMask = new Mat();
    Cv2.Compare(region.Labels!, index + 1, blobMask, CmpType.EQ);
    singleRegion = CvRegion.FromMask(blobMask);
}
```

### 性能收益（典型 BlobAnalysis 流程）

```
Threshold → binaryRegion (Count=1, 无labels)
  → Connection → connRegs (Count=N, 含完整 CC 数据)     ← 唯一一次 CC
    → AreaCenter(connRegs)     ← 优化: 读 stats+centroids (0次CC)
    → SelectShape(connRegs)    ← 优化: 复用 labels+stats (0次CC)
      → AreaCenter(selected)   ← 优化: 读缓存 (0次CC)
      → Split(selected)        ← 优化: 复用 labels (0次CC)
      → SelectObj(selected, i) ← 优化: Compare(labels, i+1, EQ) (0次CC)
```

| 算子 | 当前 CC 次数 | 优化后 | 节省 |
|------|------------|--------|------|
| Connection | 1 | 1 | — |
| AreaCenter | 1 | **0** | **-100%** |
| SelectShape | 1 | **0** | **-100%** |
| AreaCenter (二次) | 1 | **0** | **-100%** |
| Split | 1 | **0** | **-100%** |
| **合计** | **5** | **1** | **-80%** |

## 1. 添加新方法到 CvImage

### 方法设计模板

```csharp
/// <summary>
/// {Halcon算子名} - {功能描述}
/// </summary>
/// <param name="mask">定义域掩码（可为null表示整图有效）</param>
/// <returns>{返回值描述}</returns>
/// <remarks>
/// Halcon等价:
///   {halcon_operator} ({input_params})
/// </remarks>
public {ReturnType} {MethodName}(Mat? mask = null)
{
    // 1. 构建参数
    // 2. 调用 OpenCV
    // 3. 返回结果
}
```

### 示例：GetIntensity (intensity 算子)

```csharp
/// <summary>
/// 获取图像强度统计 - 等价于Halcon的intensity
/// </summary>
/// <param name="mask">定义域掩码（可为null表示整图有效）</param>
/// <returns>强度统计结果</returns>
/// <remarks>
/// Halcon等价:
///   intensity(Region, Image, Mean, Deviation)
/// 返回值包含：灰度直方图、通道均值、标准差
/// </remarks>
public ImageStats GetIntensity(Mat? mask = null)
{
    var stats = new ImageStats();

    // 灰度直方图
    using var histogram = new Mat();
    Cv2.CalcHist(
        new[] { _image },
        new[] { 0 },
        mask,
        histogram,
        1,
        new[] { 256 },
        new Rangef[] { new Rangef(0f, 256f) },
        true);

    stats.Histogram = new double[256];
    for (int i = 0; i < 256; i++)
        stats.Histogram[i] = histogram.At<float>(i);

    // 通道均值
    if (_image.Channels() >= 3)
    {
        Cv2.MeanStdDev(_image, mask, out var means, out var stddev);
        stats.ChannelMeans = new double[] { means.Val0, means.Val1, means.Val2 };
        stats.StdDev = stddev.Val0;
        stats.Mean = means.Val0;
    }
    else
    {
        var mean = Cv2.Mean(_image, mask);
        stats.ChannelMeans = new double[] { mean.Val0 };
        stats.Mean = mean.Val0;
    }

    return stats;
}
```

### 示例：GetAreaCenter (area_center 算子)

```csharp
/// <summary>
/// 获取区域面积和质心 - 等价于Halcon的area_center
/// </summary>
/// <param name="regionImage">二值区域图</param>
/// <param name="mask">定义域掩码（可为null表示整图有效）</param>
/// <returns>面积和质心结果</returns>
public static AreaCenterResult GetAreaCenter(Mat regionImage, Mat? mask = null)
{
    var moments = Cv2.Moments(regionImage, false);

    double area = moments.M00;
    double centerX = area > 0 ? moments.M10 / area : 0;
    double centerY = area > 0 ? moments.M01 / area : 0;

    return new AreaCenterResult(area, centerX, centerY);
}
```

## 2. 添加结果结构体

在 `Stats/ImageStats.cs` 中定义：

```csharp
namespace SunEyeVision.CvEngine;

/// <summary>
/// 图像统计结果
/// </summary>
public readonly struct ImageStats
{
    /// <summary>灰度直方图（256 bins）</summary>
    public double[] Histogram { get; init; } = Array.Empty<double>();

    /// <summary>各通道均值</summary>
    public double[] ChannelMeans { get; init; } = Array.Empty<double>();

    /// <summary>灰度均值</summary>
    public double Mean { get; init; }

    /// <summary>灰度标准差</summary>
    public double StdDev { get; init; }
}

/// <summary>
/// 面积和质心结果
/// </summary>
public readonly struct AreaCenterResult
{
    public double Area { get; }
    public double CenterX { get; }
    public double CenterY { get; }

    public AreaCenterResult(double area, double centerX, double centerY)
    {
        Area = area;
        CenterX = centerX;
        CenterY = centerY;
    }
}
```

## 3. 工具层使用方式

### 封装前（重复代码）

```csharp
// BlobAnalysisTool.cs 和 ThresholdTool.cs 中完全相同的代码
using var histogram = new Mat();
Cv2.CalcHist(
    new[] { cvImage.Image },
    new[] { 0 },
    null,
    histogram,
    1,
    new[] { 256 },
    new Rangef[] { new Rangef(0f, 256f) },
    true);

var histData = new double[256];
for (int i = 0; i < 256; i++)
    histData[i] = histogram.At<float>(i);
result.HistogramData = histData;

if (image.Channels() >= 3)
{
    Cv2.MeanStdDev(image, out var means, out _);
    result.ChannelMeans = new double[] { means.Val0, means.Val1, means.Val2 };
}
else
{
    var mean = Cv2.Mean(cvImage.Image);
    result.ChannelMeans = new double[] { mean.Val0 };
}
```

### 封装后（单行调用）

```csharp
var stats = cvImage.GetIntensity();
result.HistogramData = stats.Histogram;
result.ChannelMeans = stats.ChannelMeans;
```

## 4. 工具层移除 Cv2 调用模式

封装完成后，按以下步骤更新工具：

1. **读取 CvImage.cs**，确认新方法已添加
2. **更新工具类**：
   - 移除重复的 `Cv2.CalcHist` / `Cv2.Mean` / `Cv2.MeanStdDev` 代码块
   - 替换为 `cvImage.GetIntensity()` 调用
3. **验证编译通过**
4. **验证功能一致**

## 常见 Halcon 算子封装参考

| Halcon 算子 | CvOp 方法 | 说明 |
|------------|-------------|------|
| `intensity` | `Intensity()` | 强度统计（均值、标准差、直方图） |
| `area_center` | `AreaCenter()` | 面积和质心（**优先读缓存，零 CC 开销**） |
| `gray_histogram` | 内含于 `Intensity()` | 直方图计算 |
| `read_image` | `FromFile()` | 从文件加载 |
| `threshold` | `Threshold()` | 阈值分割（产出单区域，无 labels） |
| `connection` | `Connection()` | 连通域分析（**labels 基础产出者**） |
| `select_shape` | `SelectShape()` | 形状筛选（**复用缓存 + 重建 labels**） |
| `select_obj` | `SelectObj(region, index, out)` | **新增：子区域索引访问 (0-based)** |
| `connection + 遍历` | `Split()` | 拆分为单区域数组（**复用 labels**） |
| `rgb1_to_gray` | `Rgb1ToGray()` | 灰度转换 |
| `reduce_domain` | `ReduceDomain()` | ROI 限定 |
| `union1/difference/intersection/concat` | 集合运算组 | **输出丢弃 labels** |

## 约束

- **方法命名**：必须对齐 Halcon 算子名（Connection / AreaCenter / SelectShape 等）
- **参数签名**：以 OpenCV 最优实践为主（0-based index、out 参数），不机械照搬 Halcon
- 必须添加 Halcon 算子对应的 XML 注释
- 必须使用 `using` 管理 Mat 生命周期（**Connection/SelectShape 产出的 CC 数据除外——所有权转移给 CvRegion**）
- 必须提供有领域语义的返回值结构
- 禁止创建无增值的薄包装方法
- **CvRegion 多区域优化约束**：
  - 单区域结果（FromMask/Threshold/形态学/集合运算）**禁止创建** `_labels`
  - Connection 是唯一的基础 labels 产出者，必须存储完整 CC 数据并转移所有权
  - SelectShape 输出必须重建 labels（重编号 1..M），保持下游可复用
  - 形态学操作 / 集合运算输出必须清空 labels
  - AreaCenter / Split / SelectObj **必须优先走快速路径**（读缓存），慢速路径作为降级
  - SelectObj 使用 **0-based index**（对齐 C#/OpenCV 惯例）
