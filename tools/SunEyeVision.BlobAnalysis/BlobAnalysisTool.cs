using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具 - 连通域分析与特征提取
    /// </summary>
    [Tool(
        id: "blob-analysis",
        displayName: "Blob分析",
        Description = "连通域分析与特征提取",
        Icon = "🔬",
        Category = "图像处理",
        Version = "2.0.0",
        HasDebugWindow = true
    )]
    public class BlobAnalysisTool : IToolPlugin<BlobAnalysisParameters, BlobAnalysisResults>
    {
        private readonly ToolAttribute? _attribute;

        public BlobAnalysisTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）
        /// </summary>
        public BlobAnalysisResults Run(BlobAnalysisParameters parameters)
        {
            // 从参数中获取输入图像
            var image = parameters.ResolvedInputImage;
            var result = new BlobAnalysisResults
            {
                ToolName = _attribute?.DisplayName ?? "BlobAnalysis",
                ToolId = _attribute?.Id ?? "blob-analysis",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 验证参数
                var validationResult = ValidateParameters(parameters);
                if (!validationResult.IsValid)
                {
                    parameters.LogError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                // 验证输入图像
                if (image == null || image.Empty())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                parameters.LogInfo($"开始Blob分析处理: 图像尺寸 {image.Width}x{image.Height}");

                // 记录输入尺寸
                result.InputSize = image.Size();

                // 获取预处理参数
                double threshold = parameters.Threshold.Value;
                double maxValue = parameters.MaxValue.Value;
                ThresholdType type = parameters.Type.Value;
                bool invert = parameters.Invert.Value;
                bool isRegionEnabled = parameters.IsRegionEnabled.Value;

                // 获取Blob筛选参数
                int minArea = parameters.MinArea.Value;
                int maxArea = parameters.MaxArea.Value;
                double minCircularity = parameters.MinCircularity.Value;
                double maxCircularity = parameters.MaxCircularity.Value;
                double minRectangularity = parameters.MinRectangularity.Value;
                double maxRectangularity = parameters.MaxRectangularity.Value;
                ConnectivityType connectivity = parameters.Connectivity.Value;

                // 确保输入是灰度图
                Mat grayImage = image;
                if (image.Channels() > 1)
                {
                    grayImage = new Mat();
                    Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);
                    parameters.LogInfo($"已将图像转换为灰度，原图通道数: {image.Channels()}");
                }

                // 转换阈值类型
                var thresholdType = ConvertThresholdType(type);

                // 区域处理逻辑
                Mat? mask = null;
                if (isRegionEnabled)
                {
                    var effectiveRegions = parameters.GetEffectiveRegions();
                    var regionList = effectiveRegions.ToList();

                    if (regionList.Any())
                    {
                        parameters.LogInfo($"启用区域限制，共 {regionList.Count} 个有效区域");

                        var maskBuilder = new RegionMaskBuilder(grayImage.Size());
                        try
                        {
                            maskBuilder.AddRegions(regionList);
                            mask = maskBuilder.GetMask();

                            var whitePixelCount = Cv2.CountNonZero(mask);
                            parameters.LogInfo($"掩码创建成功，白色像素数: {whitePixelCount}");
                        }
                        finally
                        {
                            maskBuilder.Dispose();
                        }
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                    }
                }

                // ===== 预处理：阈值化 =====
                Mat binaryImage = new Mat();
                double actualThreshold;

                if (mask != null && !mask.Empty())
                {
                    var tempOutput = new Mat();
                    actualThreshold = Cv2.Threshold(grayImage, tempOutput, threshold, maxValue, thresholdType);
                    Cv2.CopyTo(tempOutput, binaryImage, mask);
                    tempOutput.Dispose();
                    parameters.LogInfo($"使用固定阈值: {actualThreshold} (应用掩码)");
                }
                else
                {
                    actualThreshold = Cv2.Threshold(grayImage, binaryImage, threshold, maxValue, thresholdType);
                    parameters.LogInfo($"使用固定阈值: {actualThreshold}");
                }

                // 释放掩码
                if (mask != null)
                {
                    mask.Dispose();
                }

                // 如果需要反转
                if (invert && binaryImage != null)
                {
                    var inverted = new Mat();
                    Cv2.BitwiseNot(binaryImage, inverted);
                    binaryImage.Dispose();
                    binaryImage = inverted;
                    parameters.LogInfo("已反转二值图像");
                }

                // 设置预处理结果
                result.BinaryImage = binaryImage;
                result.ThresholdUsed = (int)actualThreshold;
                result.MaxValueUsed = (int)maxValue;
                result.ThresholdUsedDouble = actualThreshold;
                result.MaxValueUsedDouble = maxValue;
                result.TypeUsed = type;
                result.InvertUsed = invert;
                result.ConnectivityUsed = connectivity;

                // 计算灰度直方图
                var histogram = new Mat();
                Cv2.CalcHist(
                    new[] { grayImage },
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
                histogram.Dispose();
                result.HistogramData = histData;

                // 计算通道均值
                if (image.Channels() >= 3)
                {
                    Cv2.MeanStdDev(image, out var means, out _);
                    result.ChannelMeans = new double[] { means.Val0, means.Val1, means.Val2 };
                }
                else
                {
                    var mean = Cv2.Mean(grayImage);
                    result.ChannelMeans = new double[] { mean.Val0 };
                }

                result.ProcessedAt = DateTime.Now;

                // ===== 核心：Blob 连通域分析 =====
                var pixelConnectivity = connectivity == ConnectivityType.Connect4
                    ? PixelConnectivity.Connectivity4
                    : PixelConnectivity.Connectivity8;

                var allBlobs = AnalyzeBlobs(binaryImage!, pixelConnectivity);
                result.AllBlobs = allBlobs;
                parameters.LogInfo($"连通域分析完成: 检测到 {allBlobs.Count} 个连通域");

                // ===== Blob 筛选 =====
                var filteredBlobs = FilterBlobs(allBlobs, minArea, maxArea, minCircularity, maxCircularity, minRectangularity, maxRectangularity);
                result.FilteredBlobs = filteredBlobs;
                parameters.LogInfo($"Blob筛选完成: 通过 {filteredBlobs.Count}/{allBlobs.Count} 个");

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                parameters.LogInfo($"Blob分析处理完成: 阈值 {actualThreshold}, " +
                                  $"连通域 {allBlobs.Count}, 筛选后 {filteredBlobs.Count}, " +
                                  $"耗时 {stopwatch.ElapsedMilliseconds}ms");

                // 释放临时灰度图
                if (grayImage != image)
                {
                    grayImage.Dispose();
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"Blob分析处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(BlobAnalysisParameters parameters)
        {
            if (parameters == null)
            {
                return ValidationResult.Failure("参数不能为空");
            }
            return parameters.Validate();
        }

        /// <summary>
        /// 创建调试控件
        /// </summary>
        public FrameworkElement? CreateDebugControl()
        {
            return new BlobAnalysisToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new BlobDataAnalysisControl();
        }

        #region 私有方法

        /// <summary>
        /// 转换阈值类型到OpenCvSharp类型
        /// </summary>
        private ThresholdTypes ConvertThresholdType(ThresholdType type)
        {
            return type switch
            {
                ThresholdType.Binary => ThresholdTypes.Binary,
                ThresholdType.BinaryInv => ThresholdTypes.BinaryInv,
                _ => ThresholdTypes.Binary
            };
        }

        /// <summary>
        /// 分析二值化图像中的连通域高级特征
        /// </summary>
        private List<BlobInfo> AnalyzeBlobs(Mat binaryImage, PixelConnectivity connectivity)
        {
            var blobs = new List<BlobInfo>();

            if (binaryImage == null || binaryImage.Empty())
                return blobs;

            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            int numLabels = Cv2.ConnectedComponentsWithStats(
                binaryImage, labels, stats, centroids, connectivity);

            for (int i = 1; i < numLabels; i++) // 跳过背景(0)
            {
                double area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                double centroidX = centroids.At<double>(i, 0);
                double centroidY = centroids.At<double>(i, 1);

                var bbox = new OpenCvSharp.Rect(
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Left),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Top),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Height));

                double perimeter = 0;
                double circularity = 0;
                double rectangularity = 0;

                using var blobMask = new Mat();
                Cv2.Compare(labels, i, blobMask, CmpType.EQ);
                Cv2.FindContours(blobMask, out var contours, out _,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple);

                if (contours.Length > 0)
                {
                    var contour = contours[0];
                    perimeter = Cv2.ArcLength(contour, true);

                    // 圆度 = 4π × 面积 / 周长²
                    circularity = perimeter > 0
                        ? (4 * Math.PI * area) / (perimeter * perimeter)
                        : 0;
                }

                // 矩形度 = 面积 / 外接矩形面积
                double bboxArea = bbox.Width * bbox.Height;
                rectangularity = bboxArea > 0 ? area / bboxArea : 0;

                // 裁剪数值误差
                circularity = Math.Min(circularity, 1.0);
                rectangularity = Math.Min(rectangularity, 1.0);

                blobs.Add(new BlobInfo
                {
                    Label = i,
                    Area = area,
                    Perimeter = perimeter,
                    CentroidX = centroidX,
                    CentroidY = centroidY,
                    Circularity = circularity,
                    Rectangularity = rectangularity,
                    BoundingBox = bbox
                });
            }

            return blobs;
        }

        /// <summary>
        /// 根据筛选条件过滤Blob
        /// </summary>
        private List<BlobInfo> FilterBlobs(
            List<BlobInfo> allBlobs,
            int minArea, int maxArea,
            double minCircularity, double maxCircularity,
            double minRectangularity, double maxRectangularity)
        {
            return allBlobs.Where(b =>
                b.Area >= minArea && b.Area <= maxArea &&
                b.Circularity >= minCircularity && b.Circularity <= maxCircularity &&
                b.Rectangularity >= minRectangularity && b.Rectangularity <= maxRectangularity
            ).ToList();
        }

        #endregion
    }
}
