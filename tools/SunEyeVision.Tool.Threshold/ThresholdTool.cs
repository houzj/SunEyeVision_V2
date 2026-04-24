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
            // 从参数中获取输入图像
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

                parameters.LogInfo($"开始阈值化处理: 图像尺寸 {image.Width}x{image.Height}");

                // 记录输入尺寸
                result.InputSize = image.Size();

                // 获取参数值
                // 算法层使用 .Value，完全无感参数是常量还是绑定的
                double threshold = parameters.Threshold.Value;
                double maxValue = parameters.MaxValue.Value;
                ThresholdType type = parameters.Type.Value;
                AdaptiveMethod adaptiveMethod = parameters.AdaptiveMethod.Value;
                int blockSize = parameters.BlockSize.Value;
                bool invert = parameters.Invert.Value;
                bool isRegionEnabled = parameters.IsRegionEnabled.Value;

                // 执行阈值化处理
                Mat outputImage;
                double actualThreshold;

                // 使用SDK的CvImage进行灰度转换
                using var cvImage = image.Channels() == 1
                    ? new CvImage(image)
                    : image.Channels() == 3
                        ? new CvImage(image).ToGrayscale()
                        : throw new InvalidOperationException("不支持的图像通道数");

                // 构建ROI掩码
                Mat? mask = null;
                if (isRegionEnabled)
                {
                    var effectiveRegions = parameters.GetEffectiveRegions().ToList();
                    if (effectiveRegions.Count > 0)
                    {
                        parameters.LogInfo($"启用区域限制，共 {effectiveRegions.Count} 个有效区域");
                        mask = RegionMaskBuilder.CreateMask(cvImage.Size, effectiveRegions);

                        var whitePixelCount = Cv2.CountNonZero(mask);
                        parameters.LogInfo($"掩码创建成功，白色像素数: {whitePixelCount}");
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                    }
                }

                // 执行阈值化
                var cvThresholdType = (CvThresholdType)type;
                var cvAdaptiveMethod = (CvAdaptiveMethod)adaptiveMethod;

                if (type == ThresholdType.Binary || type == ThresholdType.BinaryInv)
                {
                    // 普通阈值化
                    var thresholdResult = cvImage.Threshold(
                        (int)threshold, (int)maxValue, cvThresholdType, mask, invert);

                    outputImage = cvImage.CreateDisplayImage(thresholdResult.RegionImage, mask);
                    actualThreshold = thresholdResult.ActualThreshold;

                    parameters.LogInfo($"使用固定阈值: {actualThreshold} (应用掩码)");
                    thresholdResult.Dispose();
                }
                else
                {
                    // 自适应阈值化
                    var thresholdResult = cvImage.AdaptiveThreshold(
                        (int)maxValue, cvAdaptiveMethod, blockSize, c: 5,
                        type: cvThresholdType, mask: mask, invert: invert);

                    outputImage = cvImage.CreateDisplayImage(thresholdResult.RegionImage, mask);
                    actualThreshold = threshold; // 自适应阈值无法直接获取

                    parameters.LogInfo($"使用自适应阈值，块大小: {blockSize}");
                    thresholdResult.Dispose();
                }

                // 释放掩码
                mask?.Dispose();

                // 设置参数回写
                result.OutputImage = outputImage;
                result.ThresholdUsed = actualThreshold;
                result.MaxValueUsed = maxValue;
                result.TypeUsed = type;
                result.AdaptiveMethodUsed = adaptiveMethod;
                result.BlockSizeUsed = blockSize;

                // 设置输入图像信息
                result.InputChannels = image.Channels();

                // 计算统计结果
                ComputeStatistics(result, outputImage, cvImage.Image, image);

                // 执行结果判断
                EvaluateResult(result, parameters.ResultConfig);

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                parameters.LogInfo($"阈值化处理完成: 使用阈值 {actualThreshold}, " +
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
        private void ComputeStatistics(ThresholdResults result, Mat outputImage, Mat grayImage, Mat inputImage)
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

            // 灰度直方图和通道均值
            var stats = new CvImage(grayImage).GetIntensity();
            result.HistogramData = stats.Histogram;
            result.ChannelMeans = stats.ChannelMeans;
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
