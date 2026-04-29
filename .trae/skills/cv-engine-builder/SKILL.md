---
name: "cv-engine-builder"
description: "CV引擎封装技能。用于将OpenCV Cv2调用封装到CvEngine层，对齐Halcon算子。触发场景：发现工具层重复Cv2调用、需封装新Halcon算子方法、优化CvRegion多区域数据模型。"
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
internal Mat? Mask => _mask;
internal bool HasLabels => _labels != null && !_labels.Empty();
internal Mat? Labels => _labels;
internal Mat? Stats => _stats;
internal Mat? Centroids => _centroids;
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

    var labels = new Mat();
    var stats = new Mat();
    var centroids = new Mat();

    int numLabels = Cv2.ConnectedComponentsWithStats(
        mask, labels, stats, centroids,
        CvConvert.ToOpenCvConnectivity(connectivity));

    if (numLabels <= 1)
    {
        labels.Dispose(); stats.Dispose(); centroids.Dispose();
        connectedRegions = new CvRegion(
            new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black),
            0, region.ImageSize);
        return;
    }

    using var derivedMask = new Mat();
    Cv2.Compare(labels, 0, derivedMask, CmpType.GT);

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
        int n = stats.Rows;
        areaCenters = new (double, double, double)[n - 1];
        for (int i = 1; i < n; i++)
            areaCenters[i - 1] = (
                stats.At<int>(i, (int)ConnectedComponentsTypes.Area),
                centroids.At<double>(i, 1),
                centroids.At<double>(i, 0));
        return;
    }

    // 慢速路径：无缓存时重新跑 ConnectedComponentsWithStats
}
```

### SelectShape — 复用缓存 + 重建 labels

**核心决策**：输出 **保留 labels 信息**（过滤后重编号），使下游继续受益。

### Split — 零开销拆分

快速路径：复用 labels（零 CC 开销），用 `Cv2.Compare(labels, i+1, blobMask, CmpType.EQ)` 提取子区域。

### SelectObj — 0-based 子区域索引访问

**对应 Halcon**：`select_obj(Regions, ObjectTrans, Index1, Index2)`
**索引约定**：**0-based**（对齐 C# 和 OpenCV，非 Halcon 的 1-based）

### 性能收益（典型 BlobAnalysis 流程）

| 算子 | 当前 CC 次数 | 优化后 | 节省 |
|------|------------|--------|------|
| Connection | 1 | 1 | — |
| AreaCenter | 1 | **0** | **-100%** |
| SelectShape | 1 | **0** | **-100%** |
| AreaCenter (二次) | 1 | **0** | **-100%** |
| Split | 1 | **0** | **-100%** |
| **合计** | **5** | **1** | **-80%** |

## 添加新方法到 CvImage

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

## 添加结果结构体

在 `Stats/ImageStats.cs` 中定义，使用 `readonly struct`。

## 工具层移除 Cv2 调用模式

封装完成后，按以下步骤更新工具：

1. **读取 CvImage.cs**，确认新方法已添加
2. **更新工具类**：移除重复的 Cv2 代码块，替换为 CvEngine 调用
3. **验证编译通过**
4. **验证功能一致**

## 常见 Halcon 算子封装参考

| Halcon 算子 | CvOp 方法 | 说明 |
|------------|-------------|------|
| `intensity` | `Intensity()` | 强度统计（均值、标准差、直方图） |
| `area_center` | `AreaCenter()` | 面积和质心（优先读缓存，零 CC 开销） |
| `gray_histogram` | 内含于 `Intensity()` | 直方图计算 |
| `read_image` | `FromFile()` | 从文件加载 |
| `threshold` | `Threshold()` | 阈值分割（产出单区域，无 labels） |
| `connection` | `Connection()` | 连通域分析（labels 基础产出者） |
