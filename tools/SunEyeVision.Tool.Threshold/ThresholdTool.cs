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
using SunEyeVision.Tool.Threshold.Views;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 图像阈值化工具 - 使用 [Tool] 特性作为元数据单一数据源
    /// </summary>
    [Tool(
        id: "threshold",
        displayName: "图像阈值化",
        Description = "将灰度图像转换为二值图像",
        Icon = "📷",
        Category = "图像处理",
        Version = "2.0.0",
        HasDebugWindow = true
    )]
    public class ThresholdTool : IToolPlugin<ThresholdParameters, ThresholdResults>
    {
        private readonly ToolAttribute? _attribute;

        public ThresholdTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）
        /// </summary>
        public ThresholdResults Run(ThresholdParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            var result = new ThresholdResults
            {
                ToolName = _attribute?.DisplayName ?? "Threshold",
                ToolId = _attribute?.Id ?? "threshold",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var validationResult = ValidateParameters(parameters);
                if (!validationResult.IsValid)
                {
                    parameters.LogError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                if (image == null || image.Empty())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                parameters.LogInfo($"开始阈值化处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();

                double threshold = parameters.Threshold.Value;
                double maxValue = parameters.MaxValue.Value;
                ThresholdType type = parameters.Type.Value;
                AdaptiveMethod adaptiveMethod = parameters.AdaptiveMethod.Value;
                int blockSize = parameters.BlockSize.Value;
                bool invert = parameters.Invert.Value;
                bool isRegionEnabled = parameters.IsRegionEnabled.Value;

                Mat outputImage;
                double actualThreshold;

                // Step 1: 灰度转换 (rgb1_to_gray)
                CvOp.Rgb1ToGray(image, out var grayImage);
                using (grayImage)
                {
                    // Step 2: 构建ROI区域 (CvRegion 替代 Mat? roiMask)
                    CvRegion? roiRegion = null;
                    if (isRegionEnabled)
                    {
                        var effectiveRegions = parameters.GetEffectiveRegions().ToList();
                        if (effectiveRegions.Count > 0)
                        {
                            parameters.LogInfo($"启用区域限制，共 {effectiveRegions.Count} 个有效区域");
                            var roiMaskMat = RegionMaskBuilder.CreateMask(grayImage.Size, effectiveRegions);
                            roiRegion = CvRegion.FromMask(roiMaskMat, ownsData: true);
                            parameters.LogInfo($"ROI区域创建成功，白色像素数: {roiRegion.PixelCount}");
                        }
                        else
                        {
                            parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                        }
                    }

                    try
                    {
                        // Step 3: 阈值分割 (threshold / adaptive_threshold → out CvRegion)
                        var cvThresholdType = (CvThresholdType)type;
                        var cvAdaptiveMethod = (CvAdaptiveMethod)adaptiveMethod;

                        if (type == ThresholdType.Binary || type == ThresholdType.BinaryInv)
                        {
                            CvOp.Threshold(roiRegion, grayImage,
                                (int)threshold, (int)maxValue,
                                out var binaryRegion, out actualThreshold,
                                cvThresholdType, invert);

                            using (binaryRegion)
                                CvOp.ComposeDisplay(roiRegion, grayImage, binaryRegion, out outputImage);

                            parameters.LogInfo($"使用固定阈值: {actualThreshold}");
                        }
                        else
                        {
                            CvOp.AdaptiveThreshold(roiRegion, grayImage,
                                (int)maxValue, cvAdaptiveMethod, blockSize,
                                out var binaryRegion, c: 5,
                                type: cvThresholdType, invert: invert);

                            using (binaryRegion)
                                CvOp.ComposeDisplay(roiRegion, grayImage, binaryRegion, out outputImage);

                            actualThreshold = threshold;
                            parameters.LogInfo($"使用自适应阈值，块大小: {blockSize}");
                        }
                    }
                    finally
                    {
                        roiRegion?.Dispose();
                    }
                }

                // 设置参数回写
                result.OutputImage = outputImage;
                result.ThresholdUsed = actualThreshold;
                result.MaxValueUsed = maxValue;
                result.TypeUsed = type;
                result.AdaptiveMethodUsed = adaptiveMethod;
                result.BlockSizeUsed = blockSize;
                result.InputChannels = image.Channels();

                // 计算统计结果
                ComputeStatistics(result, outputImage, image);

                // 执行结果判断
                EvaluateResult(result, parameters.ResultConfig);

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                parameters.LogInfo($"阈值化处理完成: 阈值 {actualThreshold}, " +
                                  $"类型 {type}, 判断 {(result.IsPass ? "OK" : "NG")}, 耗时 {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"阈值化处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(ThresholdParameters parameters)
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
            return new ThresholdToolDebugControl();
        }

        #region 私有方法

        /// <summary>
        /// 计算统计结果
        /// </summary>
        private void ComputeStatistics(ThresholdResults result, Mat outputImage, Mat inputImage)
        {
            // 白色像素数和比例
            if (outputImage != null && !outputImage.Empty())
            {
                var whitePixelCount = Cv2.CountNonZero(outputImage);
                var totalPixels = outputImage.Rows * outputImage.Cols;
                result.WhitePixelCount = whitePixelCount;
                result.WhitePixelRatio = totalPixels > 0 ? (double)whitePixelCount / totalPixels * 100 : 0;
                result.OutputArea = whitePixelCount;

                // 输出图像均值
                var outputMean = Cv2.Mean(outputImage);
                result.OutputMean = outputMean.Val0;

                // 质心坐标
                var moments = Cv2.Moments(outputImage);
                if (moments.M00 > 0)
                {
                    result.CentroidX = moments.M10 / moments.M00;
                    result.CentroidY = moments.M01 / moments.M00;
                }
            }

            // 灰度直方图和通道均值 - 使用CvOp直接统计整图灰度信息（null region = 整图）
            using var tempCvImage = new CvImage(inputImage);
            CvOp.Intensity(null, tempCvImage,
                out var histogram, out _, out _);

            result.HistogramData = histogram;
            double histMean = histogram.Length > 0
                ? histogram.Zip(Enumerable.Range(0, 256), (h, i) => h * i).Sum() / histogram.Sum()
                : 0;
            result.ChannelMeans = new[] { histMean };
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private void EvaluateResult(ThresholdResults result, Models.ThresholdResultConfig config)
        {
            if (config == null) return;

            var failReasons = new List<string>();
            var allPassed = true;

            // 输出为空判断
            if (config.IsEmptyCheckEnabled)
            {
                if (result.OutputImage == null || result.OutputImage.Empty())
                {
                    allPassed = false;
                    failReasons.Add("输出图像为空");
                }
            }

            // 白色像素比例判断
            if (config.IsWhitePixelRatioCheckEnabled)
            {
                if (result.WhitePixelRatio < config.WhitePixelRatioMin || result.WhitePixelRatio > config.WhitePixelRatioMax)
                {
                    allPassed = false;
                    failReasons.Add($"白色像素比例 {result.WhitePixelRatio:F1}% 不在范围 [{config.WhitePixelRatioMin}-{config.WhitePixelRatioMax}]");
                }
            }

            // 输出均值判断
            if (config.IsMeanCheckEnabled)
            {
                if (result.OutputMean < config.MeanMin || result.OutputMean > config.MeanMax)
                {
                    allPassed = false;
                    failReasons.Add($"输出均值 {result.OutputMean:F1} 不在范围 [{config.MeanMin}-{config.MeanMax}]");
                }
            }

            // 输出面积判断
            if (config.IsAreaCheckEnabled)
            {
                if (result.OutputArea < config.AreaMin || result.OutputArea > config.AreaMax)
                {
                    allPassed = false;
                    failReasons.Add($"输出面积 {result.OutputArea} 不在范围 [{config.AreaMin}-{config.AreaMax}]");
                }
            }

            // 质心X判断
            if (config.IsCentroidXCheckEnabled)
            {
                if (result.CentroidX < config.CentroidXMin || result.CentroidX > config.CentroidXMax)
                {
                    allPassed = false;
                    failReasons.Add($"质心X {result.CentroidX:F1} 不在范围 [{config.CentroidXMin}-{config.CentroidXMax}]");
                }
            }

            // 质心Y判断
            if (config.IsCentroidYCheckEnabled)
            {
                if (result.CentroidY < config.CentroidYMin || result.CentroidY > config.CentroidYMax)
                {
                    allPassed = false;
                    failReasons.Add($"质心Y {result.CentroidY:F1} 不在范围 [{config.CentroidYMin}-{config.CentroidYMax}]");
                }
            }

            result.IsPass = allPassed;
            result.FailReasons = failReasons;
        }

        #endregion
    }
}
