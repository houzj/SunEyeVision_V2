---
name: cv-engine-builder
description: CV引擎封装技能。用于将OpenCV Cv2调用封装到CvEngine层，对齐Halcon算子。触发场景：发现工具层重复的Cv2调用、需要封装新的Halcon算子对应方法。
---

# CV引擎封装技能

## 触发场景

- 发现工具层有重复的 `Cv2` 调用
- 需要封装新的 Halcon 算子对应方法
- 消除跨工具的重复代码

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
├── CvRegion.cs         # 区域封装，对应 Halcon Region
├── CvConvert.cs        # 类型转换工具
└── Stats/
    └── ImageStats.cs   # 图像统计结果结构
```

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

| Halcon 算子 | CvImage 方法 | 说明 |
|------------|-------------|------|
| `intensity` | `GetIntensity()` | 强度统计（均值、标准差、直方图） |
| `area_center` | `GetAreaCenter()` | 面积和质心 |
| `gray_histogram` | 内含于 `GetIntensity()` | 直方图计算 |
| `read_image` | `FromFile()` | 从文件加载 |
| `threshold` | `Threshold()` | 阈值分割（已实现） |
| `connection` | `CvRegion.Connection()` | 连通域分析 |
| `select_shape` | `CvRegion.SelectShape()` | 形状筛选 |
| `rgb1_to_gray` | `ToGrayscale()` | 灰度转换 |
| `reduce_domain` | 由 `Threshold()` 的 mask 参数处理 | ROI 限定 |

## 约束

- 必须添加 Halcon 算子对应的 XML 注释
- 必须使用 `using` 管理 Mat 生命周期
- 必须提供有领域语义的返回值结构
- 禁止创建无增值的薄包装方法
