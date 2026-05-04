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
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.Threshold.Views;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 图像阈值化工具 - v3.0 Blob模式重构
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → 缩小定义域（ROI掩码限制，不改变图像尺寸）
    ///   3. threshold      → 阈值分割（固定/自适应）
    ///   4. 统计计算       → 白色像素/比例/均值/质心
    ///   5. 结果判断       → OK/NG判定（5种条件）
    /// </summary>
    [Tool(
        id: "threshold",
        displayName: "图像阈值化",
        Description = "将灰度图像转换为二值图像",
        Icon = "📷",
        Category = "图像处理",
        Version = "3.0.0",
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

                parameters.LogInfo($"开始阈值化处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();

                double threshold = parameters.Threshold.Value;
                double maxValue = parameters.MaxValue.Value;
                ThresholdType type = parameters.Type.Value;
                AdaptiveMethod adaptiveMethod = parameters.AdaptiveMethod.Value;
                int blockSize = parameters.BlockSize.Value;
                bool invert = parameters.Invert.Value;
                bool isRegionEnabled = parameters.IsRegionEnabled.Value;

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
                    if (isRegionEnabled)
                    {
                        var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                        var maskRegions = parameters.GetRegionsByType(RegionType.Mask);

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
                            var cvThresholdType = (CvThresholdType)type;
                            var cvAdaptiveMethod = (CvAdaptiveMethod)adaptiveMethod;

                            CvRegion? binaryRegion;
                            double actualThreshold;

                            if (type == ThresholdType.Binary || type == ThresholdType.BinaryInv)
                            {
                                CvOp.Threshold(reducedImage, null,
                                    (int)threshold, (int)maxValue,
                                    out binaryRegion, out actualThreshold,
                                    cvThresholdType, invert);
                            }
                            else
                            {
                                CvOp.AdaptiveThreshold(reducedImage, null,
                                    (int)maxValue, cvAdaptiveMethod, blockSize,
                                    out binaryRegion, c: 5,
                                    type: cvThresholdType, invert: invert);
                                actualThreshold = threshold;
                            }
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // 输出图像：通过 ComposeDisplay 将二值区域转为输出 Mat
                                CvOp.ComposeDisplay(reducedImage, null, binaryRegion, out var outputImage);

                                result.OutputImage = outputImage;
                                result.ThresholdUsed = actualThreshold;
                                result.MaxValueUsed = maxValue;
                                result.TypeUsed = type;
                                result.AdaptiveMethodUsed = adaptiveMethod;
                                result.BlockSizeUsed = blockSize;
                                result.InputChannels = image.Channels();

                                // ===== Step 5: 统计信息 =====
                                stepSw.Restart();
                                ComputeStatistics(result, outputImage, image);
                                stepSw.Stop();
                                stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                                // ===== Step 6: 结果判断 =====
                                stepSw.Restart();
                                EvaluateResult(result, parameters.ResultConfig);
                                stepSw.Stop();
                                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                                stopwatch.Stop();
                                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                                // ===== 性能报告 =====
                                var totalMs = stopwatch.ElapsedMilliseconds;
                                var maxStep = stepTimings.MaxBy(t => t.Ms);
                                var reportLines = new List<string>
                                {
                                    $"===== 阈值化性能报告 =====",
                                    $"图像: {image.Width}x{image.Height}  总耗时: {totalMs}ms",
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
                                reportLines.Add($"结果: 阈值 {actualThreshold:F0}, 类型 {type}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                parameters.LogInfo(string.Join("\n", reportLines));
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

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new ThresholdDataAnalysisControl();
        }

        #region 私有方法

        /// <summary>
        /// 计算统计结果
        /// </summary>
        private void ComputeStatistics(ThresholdResults result, Mat outputImage, Mat inputImage)
        {
            if (outputImage != null && !outputImage.Empty())
            {
                var whitePixelCount = Cv2.CountNonZero(outputImage);
                var totalPixels = outputImage.Rows * outputImage.Cols;
                result.WhitePixelCount = whitePixelCount;
                result.WhitePixelRatio = totalPixels > 0 ? (double)whitePixelCount / totalPixels * 100 : 0;
                result.OutputArea = whitePixelCount;

                var outputMean = Cv2.Mean(outputImage);
                result.OutputMean = outputMean.Val0;

                var moments = Cv2.Moments(outputImage);
                if (moments.M00 > 0)
                {
                    result.CentroidX = moments.M10 / moments.M00;
                    result.CentroidY = moments.M01 / moments.M00;
                }
            }

            using var tempCvImage = new CvImage(inputImage);
            CvOp.Intensity(tempCvImage, null,
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

            if (config.IsEmptyCheckEnabled)
            {
                if (result.OutputImage == null || result.OutputImage.Empty())
                {
                    allPassed = false;
                    failReasons.Add("输出图像为空");
                }
            }

            if (config.IsWhitePixelRatioCheckEnabled)
            {
                if (result.WhitePixelRatio < config.WhitePixelRatioMin || result.WhitePixelRatio > config.WhitePixelRatioMax)
                {
                    allPassed = false;
                    failReasons.Add($"白色像素比例 {result.WhitePixelRatio:F1}% 不在范围 [{config.WhitePixelRatioMin}-{config.WhitePixelRatioMax}]");
                }
            }

            if (config.IsMeanCheckEnabled)
            {
                if (result.OutputMean < config.MeanMin || result.OutputMean > config.MeanMax)
                {
                    allPassed = false;
                    failReasons.Add($"输出均值 {result.OutputMean:F1} 不在范围 [{config.MeanMin}-{config.MeanMax}]");
                }
            }

            if (config.IsAreaCheckEnabled)
            {
                if (result.OutputArea < config.AreaMin || result.OutputArea > config.AreaMax)
                {
                    allPassed = false;
                    failReasons.Add($"输出面积 {result.OutputArea} 不在范围 [{config.AreaMin}-{config.AreaMax}]");
                }
            }

            if (config.IsCentroidXCheckEnabled)
            {
                if (result.CentroidX < config.CentroidXMin || result.CentroidX > config.CentroidXMax)
                {
                    allPassed = false;
                    failReasons.Add($"质心X {result.CentroidX:F1} 不在范围 [{config.CentroidXMin}-{config.CentroidXMax}]");
                }
            }

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
