using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具 - 连通域分析与特征提取
    /// </summary>
    /// <remarks>
    /// 算法流程由CVHalconWrapper封装，按Halcon算子风格实现：
    ///   1. reduce_domain → 构建ROI掩码
    ///   2. threshold → 阈值分割
    ///   3. connection → 连通域分析
    ///   4. select_shape → 形状筛选
    ///   5. CreateDisplayImage → 生成显示图像
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

                // ===== Step 1: 灰度转换 =====
                // Halcon: rgb1_to_gray(Image, GrayImage)
                using var cvImage = image.Channels() == 1
                    ? new CvImage(image)
                    : image.Channels() == 3
                        ? new CvImage(image).ToGrayscale()
                        : throw new InvalidOperationException("不支持的图像通道数");

                // ===== Step 2: 构建ROI掩码 =====
                Mat? domainMask = null;
                if (parameters.IsRegionEnabled.Value)
                {
                    var effectiveRegions = parameters.GetEffectiveRegions().ToList();
                    if (effectiveRegions.Count > 0)
                    {
                        parameters.LogInfo($"启用区域限制，共 {effectiveRegions.Count} 个有效区域");
                        domainMask = RegionMaskBuilder.CreateMask(cvImage.Size, effectiveRegions);

                        var whitePixelCount = Cv2.CountNonZero(domainMask);
                        parameters.LogInfo($"ROI掩码创建成功，白色像素数: {whitePixelCount}");
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                    }
                }

                // ===== Step 3: threshold - 阈值分割 =====
                var cvThresholdType = (CvThresholdType)parameters.Type.Value;
                var thresholdResult = cvImage.Threshold(
                    parameters.Threshold.Value,
                    parameters.MaxValue.Value,
                    cvThresholdType,
                    domainMask,
                    parameters.Invert.Value);

                parameters.LogInfo($"阈值分割完成: 实际阈值={thresholdResult.ActualThreshold}");

                // 释放掩码（threshold已完成掩码应用）
                domainMask?.Dispose();

                // ===== Step 4: connection - 连通域分析 =====
                using var cvRegion = CvRegion.FromRegion(thresholdResult.RegionImage);
                var cvConnectivity = (CvConnectivity)parameters.Connectivity.Value;
                var allBlobs = cvRegion.Connection(cvConnectivity);
                parameters.LogInfo($"连通域分析完成: 检测到 {allBlobs.Count} 个连通域");

                // ===== Step 5: select_shape - 形状筛选 =====
                var filteredBlobs = cvRegion.SelectShape(
                    allBlobs,
                    parameters.MinArea.Value,
                    parameters.MaxArea.Value,
                    parameters.MinCircularity.Value,
                    parameters.MaxCircularity.Value,
                    parameters.MinRectangularity.Value,
                    parameters.MaxRectangularity.Value);

                parameters.LogInfo($"Blob筛选完成: 通过 {filteredBlobs.Count}/{allBlobs.Count} 个");

                // ===== Step 6: CreateDisplayImage - 生成显示图像 =====
                Mat? displayMask = null;
                if (parameters.IsRegionEnabled.Value)
                {
                    var effectiveRegions = parameters.GetEffectiveRegions().ToList();
                    if (effectiveRegions.Count > 0)
                    {
                        displayMask = RegionMaskBuilder.CreateMask(cvImage.Size, effectiveRegions);
                    }
                }

                var displayImage = cvImage.CreateDisplayImage(thresholdResult.RegionImage, displayMask);
                displayMask?.Dispose();

                // 释放threshold结果
                thresholdResult.Dispose();

                // ===== 设置结果 =====
                result.BinaryImage = displayImage;
                result.AllBlobs = allBlobs;
                result.FilteredBlobs = filteredBlobs;
                result.ThresholdUsed = (int)thresholdResult.ActualThreshold;
                result.ThresholdUsedDouble = thresholdResult.ActualThreshold;
                result.MaxValueUsed = parameters.MaxValue.Value;
                result.MaxValueUsedDouble = parameters.MaxValue.Value;
                result.TypeUsed = parameters.Type.Value;
                result.InvertUsed = parameters.Invert.Value;
                result.ConnectivityUsed = parameters.Connectivity.Value;

                // ===== 计算统计信息 =====
                var stats = cvImage.GetIntensity();
                result.HistogramData = stats.Histogram;
                result.ChannelMeans = stats.ChannelMeans;

                result.ProcessedAt = DateTime.Now;

                // ===== 完成 =====
                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                parameters.LogInfo($"Blob分析处理完成: 阈值 {thresholdResult.ActualThreshold}, " +
                                  $"连通域 {allBlobs.Count}, 筛选后 {filteredBlobs.Count}, " +
                                  $"耗时 {stopwatch.ElapsedMilliseconds}ms");
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
