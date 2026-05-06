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
using SunEyeVision.Tool.ImageFilter.Views;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 图像滤波工具 - 支持5种滤波方式
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. 滤波处理       → 按 FilterType 选择对应滤波算子
    ///   3. 统计计算       → 均值/标准差/最值
    ///   4. 结果判断       → OK/NG判定
    /// </summary>
    [Tool(
        id: "image-filter",
        displayName: "图像滤波",
        Description = "对图像进行滤波处理，支持均值/高斯/中值/双边/自定义卷积",
        Icon = "🖼️",
        Category = "图像处理",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class ImageFilterTool : IToolPlugin<ImageFilterParameters, ImageFilterResults>
    {
        private readonly ToolAttribute? _attribute;

        public ImageFilterTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）
        /// </summary>
        public ImageFilterResults Run(ImageFilterParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            var result = new ImageFilterResults
            {
                ToolName = _attribute?.DisplayName ?? "ImageFilter",
                ToolId = _attribute?.Id ?? "image-filter",
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

                parameters.LogInfo($"开始滤波处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();

                FilterType filterType = parameters.FilterTypeValue.Value;
                int kernelWidth = parameters.KernelWidth.Value;
                int kernelHeight = parameters.KernelHeight.Value;
                double sigmaX = parameters.SigmaX.Value;
                double sigmaY = parameters.SigmaY.Value;
                double sigmaColor = parameters.SigmaColor.Value;
                double sigmaSpace = parameters.SigmaSpace.Value;
                var borderType = ImageFilterParameters.ToCvBorderType(parameters.BorderTypeValue.Value);
                bool isRegionEnabled = parameters.IsRegionEnabled.Value;

                // ===== Step 1: 灰度转换 (rgb1_to_gray) =====
                stepSw.Restart();
                CvOp.Rgb1ToGray(image, out var grayImage);
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                using (grayImage)
                {
                    // 保存灰度图像中间结果
                    result.GrayImage = grayImage.Image.Clone();
                    result.InputChannels = image.Channels();

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
                            // ===== Step 4: 滤波处理 =====
                            stepSw.Restart();
                            CvImage? filteredImage = null;

                            try
                            {
                                switch (filterType)
                                {
                                    case FilterType.Blur:
                                        CvOp.MeanImage(reducedImage, kernelWidth, kernelHeight,
                                            out filteredImage, borderType);
                                        break;
                                    case FilterType.Gaussian:
                                        CvOp.GaussImage(reducedImage, kernelWidth, kernelHeight,
                                            sigmaX, sigmaY, out filteredImage, borderType);
                                        break;
                                    case FilterType.MedianBlur:
                                        CvOp.MedianImage(reducedImage, kernelWidth,
                                            out filteredImage);
                                        break;
                                    case FilterType.Bilateral:
                                        CvOp.BilateralFilterImage(reducedImage, kernelWidth,
                                            sigmaColor, sigmaSpace, out filteredImage, borderType);
                                        break;
                                    case FilterType.Filter2D:
                                        // 默认使用 3x3 锐化核
                                        using (var sharpenKernel = new Mat(3, 3, MatType.CV_32FC1, Scalar.All(0)))
                                        {
                                            sharpenKernel.Set<float>(0, 1, -1);
                                            sharpenKernel.Set<float>(1, 0, -1); sharpenKernel.Set<float>(1, 1, 5); sharpenKernel.Set<float>(1, 2, -1);
                                            sharpenKernel.Set<float>(2, 1, -1);

                                            CvOp.ConvolImage(reducedImage, sharpenKernel,
                                                out filteredImage, borderType);
                                        }
                                        break;
                                }
                                stepSw.Stop();
                                stepTimings.Add(("滤波处理", stepSw.ElapsedMilliseconds));

                                if (filteredImage == null || filteredImage.Empty)
                                {
                                    parameters.LogWarning("滤波结果为空");
                                    result.SetError("滤波结果为空");
                                    return result;
                                }

                                using (filteredImage)
                                {
                                    result.OutputImage = filteredImage.Image.Clone();
                                    result.FilterTypeUsed = filterType;
                                    result.KernelWidthUsed = kernelWidth;
                                    result.KernelHeightUsed = kernelHeight;

                                    // ===== Step 5: 统计信息 =====
                                    stepSw.Restart();
                                    ComputeStatistics(result, filteredImage);
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
                                        $"===== 滤波性能报告 =====",
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
                                    reportLines.Add($"结果: 滤波 {filterType}, 内核 {kernelWidth}x{kernelHeight}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                    parameters.LogInfo(string.Join("\n", reportLines));
                                }
                            }
                            finally
                            {
                                filteredImage?.Dispose();
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
                parameters.LogError($"滤波处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(ImageFilterParameters parameters)
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
            return new ImageFilterToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new ImageFilterDataAnalysisControl();
        }

        #region 私有方法

        /// <summary>
        /// 计算统计结果
        /// </summary>
        private void ComputeStatistics(ImageFilterResults result, CvImage filteredImage)
        {
            // 使用 CvOp.Intensity 计算统计
            CvOp.Intensity(filteredImage, null,
                out var histogram, out var mean, out var stdDev);

            result.OutputMean = mean;
            result.OutputStd = stdDev;

            // 计算最小最大值
            if (result.OutputImage != null && !result.OutputImage.Empty())
            {
                Cv2.MinMaxLoc(result.OutputImage, out double minVal, out double maxVal);
                result.OutputMin = minVal;
                result.OutputMax = maxVal;

                // 通道均值
                var means = Cv2.Mean(result.OutputImage);
                result.ChannelMeans = new[]
                {
                    means.Val0, means.Val1, means.Val2, means.Val3
                }.Take(result.InputChannels > 0 ? result.InputChannels : 1)
                 .ToArray();
            }
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private void EvaluateResult(ImageFilterResults result, Models.ImageFilterResultConfig config)
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

            if (config.IsMeanCheckEnabled)
            {
                if (result.OutputMean < config.MeanMin || result.OutputMean > config.MeanMax)
                {
                    allPassed = false;
                    failReasons.Add($"输出均值 {result.OutputMean:F1} 不在范围 [{config.MeanMin}-{config.MeanMax}]");
                }
            }

            result.IsPass = allPassed;
            result.FailReasons = failReasons;
        }

        #endregion
    }
}
