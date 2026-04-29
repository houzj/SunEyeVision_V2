using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.CvEngine;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具 - 连通域分析与特征提取
    /// </summary>
    /// <remarks>
    /// 算法流程（v7 - ReduceDomain + Stats直读 + C#过滤）：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → 缩小定义域（ROI掩码限制，不改变图像尺寸）
    ///   3. threshold       → 阈值分割
    ///   4. connection      → 连通域分析（缓存 labels/stats/centroids）
    ///   5. area_center     → 从缓存直读面积/质心（零CC开销）
    ///   6. smallest_rectangle1 → 从缓存直读BBox（零CC开销）
    ///   7. rectangularity  → 从缓存直读矩形度（零CC开销）
    ///   8. get_contour     → 一次FindContours获取轮廓
    ///   9. circularity(XLD) → 从轮廓计算圆度
    ///  10. cont_length     → 从轮廓计算周长
    ///  11. C#内存过滤      → 替代 SelectShape 的 OpenCV 调用
    /// </remarks>
    [Tool(
        id: "blob-analysis",
        displayName: "Blob分析",
        Description = "连通域分析与特征提取",
        Icon = "🔬",
        Category = "图像处理",
        Version = "3.0.0",
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
            var image = parameters.ResolvedInputImage;
            var result = new BlobAnalysisResults
            {
                ToolName = _attribute?.DisplayName ?? "BlobAnalysis",
                ToolId = _attribute?.Id ?? "blob-analysis",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();
            var stepTimings = new List<(string Step, long Ms)>();
            var stepSw = new Stopwatch();

            try
            {
                // ===== 参数验证 =====
                stepSw.Restart();
                var validationResult = ValidateParameters(parameters);
                stepSw.Stop();
                stepTimings.Add(("参数验证", stepSw.ElapsedMilliseconds));

                if (!validationResult.IsValid)
                {
                    parameters.LogError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                // ===== 输入图像验证 =====
                if (image == null || image.Empty())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                parameters.LogInfo($"开始Blob分析处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();

                // ===== Step 1: 灰度转换 (rgb1_to_gray) =====
                stepSw.Restart();
                CvOp.Rgb1ToGray(image, out var grayImage);
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                using (grayImage)
                {
                    CvRegion? roiRegion = null;

                    // ===== Step 2: 构建ROI区域 + 差集处理 =====
                    stepSw.Restart();
                    if (parameters.IsRegionEnabled.Value)
                    {
                        var inspectionRegions = parameters.InspectionRegions.GetEnabledRegions();
                        var maskRegions = parameters.MaskRegions.GetEnabledRegions();

                        bool hasInspection = inspectionRegions.Any();
                        bool hasMask = maskRegions.Any();

                        if (hasInspection && hasMask)
                        {
                            using var inspRegion = CvRegion.CreateFromRegions(grayImage.Size, inspectionRegions);
                            using var maskRegion = CvRegion.CreateFromRegions(grayImage.Size, maskRegions);

                            CvOp.Difference(inspRegion, maskRegion, out roiRegion);

                            parameters.LogInfo($"差集ROI区域创建成功（检测区-遮罩），白色像素数: {roiRegion.PixelCount}");
                        }
                        else if (hasInspection)
                        {
                            roiRegion = CvRegion.CreateFromRegions(grayImage.Size, inspectionRegions);
                            parameters.LogInfo($"检测区域创建成功，白色像素数: {roiRegion.PixelCount}");
                        }
                        else
                        {
                            parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                        }
                    }
                    stepSw.Stop();
                    stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

                    try
                    {
                        // ===== Step 3: ReduceDomain（缩小定义域，ROI掩码限制）=====
                        stepSw.Restart();
                        CvOp.ReduceDomain(grayImage, roiRegion, out var reducedImage);
                        stepSw.Stop();
                        stepTimings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));

                        using (reducedImage)
                        {
                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            var cvThresholdType = (CvThresholdType)parameters.Type.Value;

                            CvOp.Threshold(reducedImage, null,
                                parameters.Threshold.Value, parameters.MaxValue.Value,
                                out var binaryRegion, out double actualThreshold,
                                cvThresholdType, parameters.Invert.Value);
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // ===== Step 5: 连通域分析（缓存 labels/stats/centroids）=====
                                stepSw.Restart();
                                CvOp.Connection(binaryRegion,
                                    out var connectedRegions,
                                    (CvConnectivity)parameters.Connectivity.Value);
                                stepSw.Stop();
                                stepTimings.Add(("连通域分析", stepSw.ElapsedMilliseconds));

                                using (connectedRegions)
                                {
                                    // ===== Step 6: 从缓存直读面积/质心（零CC开销）=====
                                    stepSw.Restart();
                                    CvOp.AreaCenter(connectedRegions, out var areaCenters);
                                    stepSw.Stop();
                                    stepTimings.Add(("AreaCenter", stepSw.ElapsedMilliseconds));

                                    // ===== Step 7: 从缓存直读BBox（零CC开销）=====
                                    stepSw.Restart();
                                    CvOp.SmallestRectangle1(connectedRegions,
                                        out var row1, out var col1, out var row2, out var col2);
                                    stepSw.Stop();
                                    stepTimings.Add(("SmallestRectangle1", stepSw.ElapsedMilliseconds));

                                    // ===== Step 8: 从缓存直读矩形度（零CC开销）=====
                                    stepSw.Restart();
                                    CvOp.Rectangularity(connectedRegions, out var rectangularities);
                                    stepSw.Stop();
                                    stepTimings.Add(("Rectangularity", stepSw.ElapsedMilliseconds));

                                    // ===== Step 9: 一次FindContours获取轮廓 =====
                                    stepSw.Restart();
                                    CvOp.GetContour(connectedRegions, out var contours);
                                    stepSw.Stop();
                                    stepTimings.Add(("GetContour", stepSw.ElapsedMilliseconds));

                                    // ===== Step 10: 从轮廓计算圆度 =====
                                    stepSw.Restart();
                                    CvOp.Circularity(contours, out var circularities);
                                    stepSw.Stop();
                                    stepTimings.Add(("Circularity(XLD)", stepSw.ElapsedMilliseconds));

                                    // ===== Step 11: 从轮廓计算周长 =====
                                    stepSw.Restart();
                                    CvOp.ContLength(contours, out var perimeters);
                                    stepSw.Stop();
                                    stepTimings.Add(("ContLength", stepSw.ElapsedMilliseconds));

                                    // ===== Step 12: 组装 BlobInfo（坐标已在原图坐标系）=====
                                    stepSw.Restart();
                                    int blobCount = areaCenters.Length;
                                    var allBlobs = new List<BlobInfo>(blobCount);

                                    for (int i = 0; i < blobCount; i++)
                                    {
                                        // ReduceDomain 不改变图像尺寸，坐标直接对应原图
                                        double centroidX = areaCenters[i].Column;
                                        double centroidY = areaCenters[i].Row;

                                        var bbox = new OpenCvSharp.Rect(
                                            (int)col1[i],
                                            (int)row1[i],
                                            (int)(col2[i] - col1[i] + 1),
                                            (int)(row2[i] - row1[i] + 1));

                                        // 轮廓坐标已在原图坐标系，无需偏移
                                        CvContour? offsetContour = contours[i] != null && !contours[i].IsEmpty
                                            ? contours[i]
                                            : null;

                                        allBlobs.Add(BlobInfo.Create(
                                            label: i + 1,
                                            area: areaCenters[i].Area,
                                            perimeter: i < perimeters.Length ? perimeters[i] : 0,
                                            centroidX: centroidX,
                                            centroidY: centroidY,
                                            boundingBox: bbox,
                                            circularity: i < circularities.Length ? circularities[i] : 0,
                                            rectangularity: i < rectangularities.Length ? rectangularities[i] : 0,
                                            contour: offsetContour));
                                    }
                                    stepSw.Stop();
                                    stepTimings.Add(("组装BlobInfo", stepSw.ElapsedMilliseconds));

                                    // ===== Step 13: C#内存过滤（替代 SelectShape 的 OpenCV 调用）=====
                                    stepSw.Restart();
                                    int minArea = parameters.MinArea.Value;
                                    int maxArea = parameters.MaxArea.Value;
                                    double minCircularity = parameters.MinCircularity.Value;
                                    double maxCircularity = parameters.MaxCircularity.Value;
                                    double minRectangularity = parameters.MinRectangularity.Value;
                                    double maxRectangularity = parameters.MaxRectangularity.Value;

                                    var filteredBlobs = allBlobs.Where(b =>
                                        b.Area >= minArea && b.Area <= maxArea &&
                                        b.Circularity >= minCircularity && b.Circularity <= maxCircularity &&
                                        b.Rectangularity >= minRectangularity && b.Rectangularity <= maxRectangularity
                                    ).ToList();

                                    // 标记过滤结果
                                    foreach (var blob in allBlobs)
                                        blob.IsFiltered = filteredBlobs.Contains(blob);
                                    stepSw.Stop();
                                    stepTimings.Add(("C#过滤", stepSw.ElapsedMilliseconds));

                                    // ===== Step 14: 设置结果数据 =====
                                    stepSw.Restart();
                                    result.BinaryImage = grayImage.Image.Clone();
                                    result.AllBlobs = allBlobs;
                                    result.FilteredBlobs = filteredBlobs;
                                    result.ThresholdUsed = (int)actualThreshold;
                                    result.ThresholdUsedDouble = actualThreshold;
                                    result.MaxValueUsed = parameters.MaxValue.Value;
                                    result.MaxValueUsedDouble = parameters.MaxValue.Value;
                                    result.TypeUsed = parameters.Type.Value;
                                    result.InvertUsed = parameters.Invert.Value;
                                    result.ConnectivityUsed = parameters.Connectivity.Value;
                                    stepSw.Stop();
                                    stepTimings.Add(("结果赋值", stepSw.ElapsedMilliseconds));

                                    // ===== Step 15: 统计信息 =====
                                    stepSw.Restart();
                                    CvOp.Intensity(reducedImage, null,
                                        out var histogram, out _, out _);
                                    result.HistogramData = histogram;
                                    double histMean = histogram.Length > 0 ? histogram.Zip(
                                        Enumerable.Range(0, 256), (h, i) => h * i).Sum() / histogram.Sum() : 0;
                                    result.ChannelMeans = new[] { histMean };
                                    stepSw.Stop();
                                    stepTimings.Add(("灰度统计", stepSw.ElapsedMilliseconds));

                                    result.ProcessedAt = DateTime.Now;

                                    // ===== Step 16: 构建显示项 =====
                                    stepSw.Restart();
                                    var display = result.Display;
                                    display.Clear();

                                    // 轮廓显示
                                    var filteredContours = filteredBlobs
                                        .Where(b => b.Contour != null && !b.Contour.IsEmpty && b.Contour.Points != null)
                                        .Select(b => b.Contour.Points!)
                                        .ToList();
                                    if (filteredContours.Count > 0)
                                        display.AddContours(parameters.ContoursStyle, filteredContours);

                                    // 外接矩形
                                    foreach (var blob in filteredBlobs)
                                    {
                                        var bbox = blob.BoundingBox;
                                        if (bbox.Width > 0 && bbox.Height > 0)
                                            display.AddRect(parameters.BlobsStyle, new Rect2d(bbox.X, bbox.Y, bbox.Width, bbox.Height));
                                    }

                                    // 质心十字
                                    foreach (var blob in filteredBlobs)
                                    {
                                        if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                            display.AddCross(parameters.CentroidsStyle, new Point2d(blob.CentroidX, blob.CentroidY));
                                    }

                                    // 标签
                                    var labelBlobs = filteredBlobs
                                        .Where(b => b.CentroidX > 0 || b.CentroidY > 0).ToArray();
                                    if (labelBlobs.Length > 0)
                                    {
                                        var xs = new double[labelBlobs.Length];
                                        var ys = new double[labelBlobs.Length];
                                        var labels = new string[labelBlobs.Length];
                                        for (int i = 0; i < labelBlobs.Length; i++)
                                        {
                                            xs[i] = labelBlobs[i].CentroidX + 5;
                                            ys[i] = labelBlobs[i].CentroidY - 5;
                                            labels[i] = $"{i}:{labelBlobs[i].Area:F0}";
                                        }
                                        display.AddText(parameters.LabelsStyle, xs, ys, labels);
                                    }
                                    

                                    // 统计文本
                                    var filteredTotalArea = filteredBlobs.Sum(b => b.Area);
                                    display.AddText(parameters.StatisticsStyle,
                                        parameters.StatisticsStyle.PositionX, parameters.StatisticsStyle.PositionY,
                                        new[]
                                        {
                                            $"Blob: {filteredBlobs.Count}/{allBlobs.Count}  阈值: {actualThreshold}",
                                            $"面积: {filteredTotalArea:F0}  类型: {parameters.Type.Value}"
                                        });

                                    if (filteredBlobs.Count > 20)
                                        display.AddText(parameters.MoreHintStyle, 10, result.InputSize.Height - 20, $"...+{filteredBlobs.Count - 20} more");
                                    stepSw.Stop();
                                    stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                                    // 释放轮廓资源（已被 BlobInfo 引用的轮廓不释放，由 BlobInfo 管理生命周期）

                                    stopwatch.Stop();
                                    result.SetSuccess(stopwatch.ElapsedMilliseconds);

                                    // ===== 性能报告 =====
                                    var totalMs = stopwatch.ElapsedMilliseconds;
                                    var maxStep = stepTimings.MaxBy(t => t.Ms);
                                    var reportLines = new List<string>
                                    {
                                        $"===== Blob分析性能报告 =====",
                                        $"图像: {image.Width}x{image.Height}  ReduceDomain  总耗时: {totalMs}ms",
                                        $"步骤  名称              耗时(ms)    占比",
                                        $"────  ──────────────── ────────  ──────"
                                    };
                                    foreach (var (step, ms) in stepTimings)
                                    {
                                        var pct = totalMs > 0 ? (double)ms / totalMs * 100 : 0;
                                        var marker = step == maxStep.Step ? " ◄瓶颈" : "";
                                        reportLines.Add($"  {stepTimings.IndexOf((step, ms)) + 1,2}.  {step,-18} {ms,8}  {pct,5:F1}%{marker}");
                                    }
                                    reportLines.Add($"──────────────────────────────────");
                                    reportLines.Add($"结果: 连通域 {allBlobs.Count}, 筛选 {filteredBlobs.Count}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                    parameters.LogInfo(string.Join("\n", reportLines));
                                }
                            }
                        }
                    }
                    finally
                    {
                        roiRegion?.Dispose();
                    }
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
    }
}
