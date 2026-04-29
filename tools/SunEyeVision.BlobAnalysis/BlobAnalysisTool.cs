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
    /// 算法流程（v6 - CvOp 统一算子入口）：
    ///   1. reduce_domain → 构建ROI区域 (CvRegion)
    ///   2. difference     → ROI差集 (CvOp.Difference)
    ///   3. threshold      → 阈值分割，输出 CvRegion
    ///   4. connection     → 连通域分析 (CvOp.Connection)
    ///   5. area_center    → 特征提取 (CvOp.AreaCenter)
    ///   6. select_shape   → 形状筛选 (CvOp.SelectShape)
    /// </remarks>
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
                // ===== 参数验证 =====
                var validationResult = ValidateParameters(parameters);
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
                CvOp.Rgb1ToGray(image, out var grayImage);
                using (grayImage)
                {
                    CvRegion? roiRegion = null;

                    // ===== Step 2: 构建ROI区域 + 差集处理 (difference) =====
                    if (parameters.IsRegionEnabled.Value)
                    {
                        var inspectionRegions = parameters.InspectionRegions.GetEnabledRegions();
                        var maskRegions = parameters.MaskRegions.GetEnabledRegions();

                        bool hasInspection = inspectionRegions.Any();
                        bool hasMask = maskRegions.Any();

                        if (hasInspection && hasMask)
                        {
                            // 构造 CvRegion 并执行差集
                            using var inspRegion = CvRegion.CreateFromRegions(grayImage.Size, inspectionRegions);
                            using var maskRegion = CvRegion.CreateFromRegions(grayImage.Size, maskRegions);

                            CvOp.Difference(inspRegion, maskRegion, out roiRegion);

                            int whiteCount = roiRegion.PixelCount;
                            parameters.LogInfo($"差集ROI区域创建成功（检测区-遮罩），白色像素数: {whiteCount}");
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

                    try
                    {
                        // ===== Step 3: 阈值分割 (threshold → out CvRegion) =====
                        var cvThresholdType = (CvThresholdType)parameters.Type.Value;

                        CvOp.Threshold(roiRegion, grayImage,
                            parameters.Threshold.Value, parameters.MaxValue.Value,
                            out var binaryRegion, out double actualThreshold,
                            cvThresholdType, parameters.Invert.Value);

                        using (binaryRegion)
                        {
                            parameters.LogInfo($"阈值分割完成: 实际阈值={actualThreshold}");

                            // ===== Step 4: 连通域分析 (connection → CvRegion) =====
                            CvOp.Connection(binaryRegion,
                                out var connectedRegions,
                                (CvConnectivity)parameters.Connectivity.Value);
                            using (connectedRegions)
                            {
                                parameters.LogInfo($"连通域分析完成: 检测到 {connectedRegions.Count} 个子区域");

                                // ===== Step 5a: 提取全部Blob特征 (area_center → 组装 BlobInfo) =====
                                CvOp.AreaCenter(connectedRegions, out var rawCenters);
                                var allBlobs = AssembleBlobInfos(connectedRegions, rawCenters);
                                parameters.LogInfo($"Blob特征提取完成: {allBlobs.Length} 个");

                                // ===== Step 5b: 形状筛选 (select_shape → CvRegion) =====
                                CvOp.SelectShape(connectedRegions,
                                    out var selectedRegions,
                                    parameters.MinArea.Value, parameters.MaxArea.Value,
                                    parameters.MinCircularity.Value, parameters.MaxCircularity.Value,
                                    parameters.MinRectangularity.Value, parameters.MaxRectangularity.Value);
                                using (selectedRegions)
                                {
                                    CvOp.AreaCenter(selectedRegions, out var filteredRawCenters);
                                    var filteredBlobs = AssembleBlobInfos(selectedRegions, filteredRawCenters);

                                    parameters.LogInfo($"Blob筛选完成: 通过 {filteredBlobs.Length}/{allBlobs.Length} 个");

                                    // ===== Step 6: 组合显示图 (compose_display) =====
                                    CvOp.ComposeDisplay(roiRegion, grayImage, binaryRegion, out var displayImage);

                                    // ===== 设置结果 =====
                                    result.BinaryImage = displayImage;
                                    result.AllBlobs = allBlobs.ToList();
                                    result.FilteredBlobs = filteredBlobs.ToList();
                                    result.ThresholdUsed = (int)actualThreshold;
                                    result.ThresholdUsedDouble = actualThreshold;
                                    result.MaxValueUsed = parameters.MaxValue.Value;
                                    result.MaxValueUsedDouble = parameters.MaxValue.Value;
                                    result.TypeUsed = parameters.Type.Value;
                                    result.InvertUsed = parameters.Invert.Value;
                                    result.ConnectivityUsed = parameters.Connectivity.Value;

                                    // ===== 统计信息 (intensity) - 使用CvRegion限定范围 =====
                                    CvOp.Intensity(roiRegion, grayImage,
                                        out var histogram, out _, out _);
                                    result.HistogramData = histogram;
                                    // ChannelMeans 通过 intensity 单值输出，保持兼容
                                    double histMean = histogram.Length > 0 ? histogram.Zip(
                                        Enumerable.Range(0, 256), (h, i) => h * i).Sum() / histogram.Sum() : 0;
                                    result.ChannelMeans = new[] { histMean };

                                    result.ProcessedAt = DateTime.Now;

                                    // ===== 构建显示项（Style 引用驱动按需收集） =====
                                    var display = result.Display;
                                    display.Clear();

                                    // 图形项: Blob 外接矩形
                                    foreach (var blob in filteredBlobs)
                                    {
                                        var bbox = blob.BoundingBox;
                                        if (bbox.Width > 0 && bbox.Height > 0)
                                            display.AddRect(parameters.BlobsStyle, new Rect2d(bbox.X, bbox.Y, bbox.Width, bbox.Height));
                                    }

                                    // 图形项: 质心十字
                                    foreach (var blob in filteredBlobs)
                                    {
                                        if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                            display.AddCross(parameters.CentroidsStyle, new Point2d(blob.CentroidX, blob.CentroidY));
                                    }

                                    // 文本项: Blob 标签（坐标跟随质心，批量独立定位）
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

                                    // 文本项: 统计信息（锚点自动换行，位置从 Style 读取）
                                    var filteredTotalArea = filteredBlobs.Sum(b => b.Area);
                                    display.AddText(parameters.StatisticsStyle,
                                        parameters.StatisticsStyle.PositionX, parameters.StatisticsStyle.PositionY,
                                        new[]
                                        {
                                            $"Blob: {filteredBlobs.Length}/{allBlobs.Length}  阈值: {actualThreshold}",
                                            $"面积: {filteredTotalArea:F0}  类型: {parameters.Type.Value}"
                                        });

                                    // 文本项: 更多提示（条件性）
                                    if (filteredBlobs.Length > 20)
                                        display.AddText(parameters.MoreHintStyle, 10, result.InputSize.Height - 20, $"...+{filteredBlobs.Length - 20} more");

                                    stopwatch.Stop();
                                    result.SetSuccess(stopwatch.ElapsedMilliseconds);

                                    parameters.LogInfo($"Blob分析处理完成: 阈值 {actualThreshold}, " +
                                                      $"连通域 {allBlobs.Length}, 筛选后 {filteredBlobs.Length}, " +
                                                      $"耗时 {stopwatch.ElapsedMilliseconds}ms");
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

        /// <summary>
        /// 将 AreaCenter 基础数据组装为完整 BlobInfo（显示层 DTO）
        /// </summary>
        /// <remarks>
        /// 算法与显示解耦：
        ///   - CvRegion.AreaCenter 只返回 (Area, Row, Column)
        ///   - CvOp.Split() 拆分为单区域数组（out）
        ///   - CvOp.RegionFeatures 按需查询额外特征（out）
        ///   - Tool 层负责组装为 UI 可消费的 BlobInfo
        /// </remarks>
        private static BlobInfo[] AssembleBlobInfos(
            CvRegion region, (double Area, double Row, double Column)[] areaCenters)
        {
            if (areaCenters.Length == 0)
                return Array.Empty<BlobInfo>();

            var blobs = new List<BlobInfo>(areaCenters.Length);
            int idx = 0;
            CvOp.Split(region, out var splitRegions);
            foreach (var singleRegion in splitRegions)
            {
                if (idx >= areaCenters.Length) break;
                var (area, row, col) = areaCenters[idx];

                CvOp.RegionFeatures(singleRegion, out var features,
                    RegionFeature.Perimeter,
                    RegionFeature.Circularity,
                    RegionFeature.Rectangularity,
                    RegionFeature.BoundingBoxRow,
                    RegionFeature.BoundingBoxColumn,
                    RegionFeature.BoundingBoxWidth,
                    RegionFeature.BoundingBoxHeight);

                blobs.Add(BlobInfo.Create(
                    label: idx + 1, area: area, perimeter: features[0],
                    centroidX: col, centroidY: row,
                    boundingBox: new OpenCvSharp.Rect(
                        (int)features[4], (int)features[3],
                        (int)features[5], (int)features[6]),
                    circularity: features[1],
                    rectangularity: features[2]));
                idx++;
            }
            return blobs.ToArray();
        }
    }
}
