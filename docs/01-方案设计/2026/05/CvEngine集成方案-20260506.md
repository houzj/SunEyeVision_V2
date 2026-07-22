# CvEngine 集成方案确认

## 一、现状分析

CvOp 已有算子覆盖情况：

| 分组 | 已有方法 | 工具需求 | 差距 |
|------|---------|---------|------|
| 📦 图像转换 | `Rgb1ToGray`, `ReduceDomain`, `CropDomain` | 灰度转换 | ✅ 完全覆盖 |
| ✂️ 阈值分割 | `Threshold`, `AdaptiveThreshold`, `ThresholdRange` | 二值化全部方法 | ✅ 完全覆盖 |
| 📊 统计 | `Intensity` (含直方图/均值/标准差) | 结果统计 | ✅ 完全覆盖 |
| ⭕ 形态学 | `DilationCircle`, `ErosionCircle`, `OpeningCircle`, `ClosingCircle`, `DilationRectangle1`, `ErosionRectangle1` | 7种操作+3种核形状+迭代次数 | ❌ 需扩展 |
| 🎛️ 滤波 | 无 | 5种滤波方法 | ❌ 需新增 |

---

## 二、各工具集成方案

### 2.1 图像二值化 → 完全复用 CvOp

**图像类型流：** `Mat` → `CvImage` → `CvRegion` → `Mat`

```
输入 Mat (BGR/Gray)
  │
  ▼
CvOp.Rgb1ToGray(mat, out CvImage grayImage)      // 灰度转换（彩色时）
  │
  ▼
CvOp.ReduceDomain(grayImage, region, out CvImage reduced)  // 区域限制（可选）
  │
  ▼
┌─ 根据 Method 选择算子 ─────────────────────┐
│                                            │
│ 固定/Otsu/Triangle:                        │
│   CvOp.Threshold(reduced, region,          │
│     thresh, maxVal, out CvRegion result,   │
│     out double actualThreshold, type)      │
│                                            │
│ 自适应:                                    │
│   CvOp.AdaptiveThreshold(reduced, region,  │
│     maxVal, method, blockSize,             │
│     out CvRegion result, c)               │
│                                            │
│ 范围阈值:                                  │
│   CvOp.ThresholdRange(reduced, region,     │
│     minGray, maxGray, out CvRegion result) │
└────────────────────────────────────────────┘
  │
  ▼
CvOp.Intensity(reduced, result,              // 统计：直方图+均值+标准差
  out double[] histogram, out double mean,
  out double stdDev)
  │
  ▼
CvOp.AreaCenter(result, out var areaCenters) // 面积+质心统计
  │
  ▼
CvOp.ComposeDisplay(reduced, region, result, // 合成显示图像
  out Mat displayImage)
```

**算子调用确认：** 全部使用 CvOp 现有方法，**无需新增任何 CvOp 算子**。

---

### 2.2 图像滤波 → 需新增 CvOp 方法

**图像类型流：** `Mat` → `CvImage` → `CvImage` → `Mat`

#### 待新增 CvOp 算子（5个）

```csharp
// 命名规范：对齐 Halcon 风格，动词+名词
// 位置：CvOp.cs → 新增 #region 🎛️ 图像滤波

/// <summary>均值滤波 - 对应 Halcon mean_image(Image : ImageMean : MaskWidth, MaskHeight)</summary>
public static void MeanImage(CvImage image, int kernelWidth, int kernelHeight,
    out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
{
    using var result = new Mat();
    Cv2.Blur(image.Image, result, new Size(kernelWidth, kernelHeight),
        new Point(-1, -1), borderType);
    filteredImage = new CvImage(result.Clone(), ownsImage: true);
}

/// <summary>高斯滤波 - 对应 Halcon gauss_image(Image : ImageGauss : Size)</summary>
public static void GaussImage(CvImage image, int kernelWidth, int kernelHeight,
    double sigmaX, double sigmaY,
    out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
{
    using var result = new Mat();
    Cv2.GaussianBlur(image.Image, result,
        new Size(kernelWidth, kernelHeight), sigmaX, sigmaY, borderType);
    filteredImage = new CvImage(result.Clone(), ownsImage: true);
}

/// <summary>中值滤波 - 对应 Halcon median_image(Image : ImageMedian : MaskSize)</summary>
public static void MedianImage(CvImage image, int kernelSize,
    out CvImage filteredImage)
{
    using var result = new Mat();
    Cv2.MedianBlur(image.Image, result, kernelSize);
    filteredImage = new CvImage(result.Clone(), ownsImage: true);
}

/// <summary>双边滤波 - 对应 Halcon bilateral_filter</summary>
public static void BilateralFilterImage(CvImage image, int diameter,
    double sigmaColor, double sigmaSpace,
    out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
{
    using var result = new Mat();
    Cv2.BilateralFilter(image.Image, result, diameter, sigmaColor, sigmaSpace, borderType);
    filteredImage = new CvImage(result.Clone(), ownsImage: true);
}

/// <summary>自定义卷积滤波 - 对应 Halcon convol_image(Image : : : Filter)</summary>
public static void ConvolImage(CvImage image, Mat kernel,
    out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
{
    using var result = new Mat();
    Cv2.Filter2D(image.Image, result, -1, kernel, new Point(-1, -1), 0, borderType);
    filteredImage = new CvImage(result.Clone(), ownsImage: true);
}
```

**工具内调用流：**
```
输入 Mat
  │
  ▼
CvOp.Rgb1ToGray(input, out CvImage grayImage)   // 灰度转换
  │
  ▼
CvOp.MeanImage|GaussImage|MedianImage|          // 按 FilterType 选择
  BilateralFilterImage|ConvolImage(grayImage,    //  对应滤波方法
    ..., out CvImage filtered)
  │
  ▼
CvOp.Intensity(filtered, null,                  // 统计
  out histogram, out mean, out stdDev)
  │
  ▼
filtered.Image → Mat → 输出图像                  // 直接取 CvImage 的 Mat 作为显示
```

---

### 2.3 形态学处理 → 需扩展/新增 CvOp 方法

**图像类型流：** `Mat` → `CvRegion` → `CvRegion` → `Mat`

#### 待新增 CvOp 算子（1个通用方法）

现有 `DilationCircle`/`ErosionCircle`/`OpeningCircle` 等只能处理 `CvRegion` 且仅支持圆形/矩形核。形态学工具需要：

**方案：新增通用 MorphologyEx 算子（操作 Mat 级别）**

```csharp
// 命名规范：对齐 Halcon 的 morphology 家族
// 位置：CvOp.cs → ⭕ 形态学操作

/// <summary>
/// 形态学操作 - 对应 Halcon morphology_xxx 系列
/// 直接操作 Mat，输出处理后的二值图像
/// </summary>
public static void MorphologyEx(
    Mat input, out Mat result,
    MorphTypes op,                       // Erode/Dilate/Open/Close/Gradient/TopHat/BlackHat
    MorphShapes kernelShape,             // Rect/Cross/Ellipse
    int kernelWidth, int kernelHeight,
    int iterations = 1,
    BorderTypes borderType = BorderTypes.Constant,
    Scalar? borderValue = null)
{
    var kernel = Cv2.GetStructuringElement(kernelShape,
        new Size(kernelWidth, kernelHeight));
    using (kernel)
    {
        result = new Mat();
        if (op is MorphTypes.Erode or MorphTypes.Dilate)
        {
            if (op == MorphTypes.Erode)
                Cv2.Erode(input, result, kernel,
                    new Point(-1, -1), iterations, borderType,
                    borderValue ?? Scalar.All(255));
            else
                Cv2.Dilate(input, result, kernel,
                    new Point(-1, -1), iterations, borderType,
                    borderValue ?? Scalar.All(255));
        }
        else
        {
            Cv2.MorphologyEx(input, result, op, kernel,
                new Point(-1, -1), iterations, borderType,
                borderValue ?? Scalar.All(255));
        }
    }
}

/// <summary>形态学操作（CvImage 版）</summary>
public static void MorphologyEx(
    CvImage image, out CvImage resultImage,
    MorphTypes op, MorphShapes kernelShape,
    int kernelWidth, int kernelHeight,
    int iterations = 1,
    BorderTypes borderType = BorderTypes.Constant,
    Scalar? borderValue = null)
{
    MorphologyEx(image.Image, out var result,
        op, kernelShape, kernelWidth, kernelHeight,
        iterations, borderType, borderValue);
    resultImage = new CvImage(result, ownsImage: true);
}
```

**工具内调用流：**
```
输入 Mat
  │
  ▼
CvOp.Rgb1ToGray(input, out CvImage grayImage)   // 灰度转换
  │
  ▼
CvOp.Threshold(grayImage, null, 128, 255,        // 二值化（形态学在二值图上操作）
  out CvRegion binaryRegion, out _)
  │
  ▼
var binaryMask = binaryRegion.Mask               // 提取二值掩码
  │
  ▼
CvOp.MorphologyEx(binaryMask, out Mat morphed,   // 形态学操作
  op, kernelShape, kernelWidth, kernelHeight,
  iterations, borderType)
  │
  ▼
// 重新构造 CvRegion 用于统计
var resultRegion = CvRegion.FromMask(morphed, ownsData: true);
CvOp.Connection(resultRegion, out var connected); // Blob计数
CvOp.AreaCenter(resultRegion, out var areaCenters); // 总面积
  │
  ▼
morphed → 输出图像
```

**注意点：** 形态学工具与 Blob 分析不同，不直接输出 Blob 列表，而是输出处理后的二值图像（可以看到形态学效果），同时统计 Blob 数量和面积作为结果判断依据。

---

## 三、CvOp 新增算子汇总

| 工具 | 算子 | 签名 | 优先级 |
|------|------|------|--------|
| 图像滤波 | `MeanImage` | `(CvImage, int, int, out CvImage, BorderTypes)` | P0 |
| 图像滤波 | `GaussImage` | `(CvImage, int, int, double, double, out CvImage, BorderTypes)` | P0 |
| 图像滤波 | `MedianImage` | `(CvImage, int, out CvImage)` | P0 |
| 图像滤波 | `BilateralFilterImage` | `(CvImage, int, double, double, out CvImage, BorderTypes)` | P0 |
| 图像滤波 | `ConvolImage` | `(CvImage, Mat, out CvImage, BorderTypes)` | P1 |
| 形态学 | `MorphologyEx` (Mat版) | `(Mat, out Mat, MorphTypes, MorphShapes, int, int, int, BorderTypes)` | P0 |
| 形态学 | `MorphologyEx` (CvImage版) | `(CvImage, out CvImage, MorphTypes, MorphShapes, int, int, int, BorderTypes)` | P0 |

**总共需新增：** 7 个 CvOp 算子（其中 MorphologyEx 提供 Mat/CvImage 两个重载版本）

---

## 四、工具内部 CvOp 调用对照表

### 4.1 图像二值化

```csharp
// 灰度转换
CvOp.Rgb1ToGray(input, out var grayImage);

// 区域限制（可选）
CvOp.ReduceDomain(grayImage, region, out var reduced);

// 阈值分割（按 Method 选择）
CvOp.Threshold(reduced, region, threshold, maxValue, out var threshRegion, out var actualThreshold, type);
// 或 CvOp.AdaptiveThreshold(reduced, region, maxValue, method, blockSize, out var threshRegion, c);
// 或 CvOp.ThresholdRange(reduced, region, minGray, maxGray, out var threshRegion);

// 统计
CvOp.Intensity(reduced, threshRegion, out var histogram, out var mean, out var stdDev);
CvOp.AreaCenter(threshRegion, out var areaCenters);

// 合成显示
CvOp.ComposeDisplay(reduced, region, threshRegion, out var displayImage);
```

**依赖的现有 CvOp 算子：** `Rgb1ToGray`, `ReduceDomain`, `Threshold`, `AdaptiveThreshold`, `ThresholdRange`, `Intensity`, `AreaCenter`, `ComposeDisplay`
**新增需求：** 无

### 4.2 图像滤波

```csharp
// 灰度转换
CvOp.Rgb1ToGray(input, out var grayImage);

// 区域限制（可选）— 滤波通常在原图上操作，区域在后续统计时限制
// 简单实现：直接对 grayImage.Image (Mat) 操作

// 滤波（按 FilterType 选择）
switch (filterType)
{
    case FilterType.Blur:
        CvOp.MeanImage(grayImage, kw, kh, out var filtered, borderType);
        break;
    case FilterType.Gaussian:
        CvOp.GaussImage(grayImage, kw, kh, sigmaX, sigmaY, out var filtered, borderType);
        break;
    case FilterType.MedianBlur:
        CvOp.MedianImage(grayImage, kw, out var filtered);
        break;
    case FilterType.Bilateral:
        CvOp.BilateralFilterImage(grayImage, kw, sigmaColor, sigmaSpace, out var filtered, borderType);
        break;
    case FilterType.Filter2D:
        CvOp.ConvolImage(grayImage, kernel, out var filtered, borderType);
        break;
}

// 统计（无区域限制时对整个图像统计）
CvOp.Intensity(filtered, null, out var histogram, out var mean, out var stdDev);

// 输出图像
filtered.Image → OutputImage
```

**依赖的现有 CvOp 算子：** `Rgb1ToGray`, `Intensity`
**新增需求：** 5 个滤波算子

### 4.3 形态学处理

```csharp
// 灰度转换
CvOp.Rgb1ToGray(input, out var grayImage);

// 区域限制 + 二值化
// 需要先将输入二值化才能做形态学操作
// 形态学工具的"基本参数"中需要提供二值化参数，或者直接使用灰度阈值
var binary = grayImage.Image; // 或者 Cv2.Threshold(grayImage.Image, ...)

// 形态学操作
CvOp.MorphologyEx(binary, out var morphed,
    op, kernelShape, kernelWidth, kernelHeight, iterations, borderType);

// Blob 统计
var resultRegion = CvRegion.FromMask(morphed, ownsData: true);
CvOp.Connection(resultRegion, out var connected);
CvOp.AreaCenter(resultRegion, out var areaCenters);

// 输出
morphed → OutputImage
```

**依赖的现有 CvOp 算子：** `Rgb1ToGray`, `Connection`, `AreaCenter`
**新增需求：** `MorphologyEx` (Mat版 + CvImage版)

---

## 五、实施方案

### Phase 1: 新增 CvOp 算子（由 CvEngine 维护者完成）

| 步骤 | 内容 | 文件 |
|------|------|------|
| 1 | `CvOp.cs` 新增 `#region 🎛️ 图像滤波` | `src/SunEyeVision.CvEngine/CvOp.cs` |
| 2 | 实现 5 个滤波算子 | 同上 |
| 3 | `CvOp.cs` 扩展 `#region ⭕ 形态学操作` | 同上 |
| 4 | 实现 `MorphologyEx` 通用算子(Mat/CvImage重载) | 同上 |
| 5 | 编译验证 | - |

### Phase 2: 三个工具实现（由工具开发者完成）

| 工具 | 成员 | 依赖 CvOp 情况 |
|------|------|----------------|
| 图像二值化 | member1 | ✅ 零新增，全部使用已有算子 |
| 图像滤波 | member2 | ⏳ 依赖 Phase 1 的 5 个滤波算子 |
| 形态学处理 | member3 | ⏳ 依赖 Phase 1 的 MorphologyEx 算子 |

### 依赖关系

```
Phase 1: CvEngine 新增算子
    │
    ├──→ member2: 图像滤波工具开发
    │
    └──→ member3: 形态学工具开发
    │
member1: 图像二值化 ← 独立开发，无阻塞
```
