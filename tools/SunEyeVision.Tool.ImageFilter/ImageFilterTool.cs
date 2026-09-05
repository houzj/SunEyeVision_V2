using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.ImageFilter.Views;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 图像滤波工具 - 支持5种滤波方式（Halcon 实现）
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. 滤波处理       → 按 FilterType 选择对应滤波算子
    ///   3. 统计计算       → 均值/标准差/最值
    ///   4. 结果判断       → OK/NG判定
    /// </summary>
    [Tool(
        id: "ImageFilter",
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
            var hImage = parameters.GetHalconImage();
            var result = new ImageFilterResults
            {
                ToolName = _attribute?.DisplayName ?? "ImageFilter",
                ToolId = _attribute?.Id ?? "ImageFilter",
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
                if (!hImage.IsInitialized())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                HOperatorSet.GetImageSize(hImage, out var imgW, out var imgH);
                int imgWidth = (int)imgW.L;
                int imgHeight = (int)imgH.L;
                parameters.LogInfo($"开始滤波处理: 图像尺寸 {imgWidth}x{imgHeight}");

                FilterType filterType = parameters.FilterTypeValue.Value;
                int kernelWidth = parameters.KernelWidth.Value;
                int kernelHeight = parameters.KernelHeight.Value;
                double sigmaX = parameters.SigmaX.Value;
                double sigmaY = parameters.SigmaY.Value;
                double sigmaColor = parameters.SigmaColor.Value;
                double sigmaSpace = parameters.SigmaSpace.Value;

                // ===== Step 1: 灰度转换 (rgb1_to_gray) =====
                stepSw.Restart();
                HImage grayImage;
                int channels = hImage.CountChannels();
                if (channels == 3 || channels == 4)
                {
                    HOperatorSet.Rgb1ToGray(hImage, out var grayObj);
                    grayImage = new HImage(grayObj);
                }
                else
                {
                    grayImage = hImage.Clone();
                }
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                using (grayImage)
                {
                    // 保存灰度图像中间结果
                    result.GrayImage = new HalImage(grayImage.Clone());
                    result.InputWidth = imgWidth;
                    result.InputHeight = imgHeight;
                    result.InputChannels = channels;

                    // ===== Step 2: 区域前置（HalconRegionPipeline：订阅解析 + 位置修正 + 差集）=====
                    // 纯几何：区域 + 位置修正；全图/按域执行由算法层自决（见下 HalImageDomain.Restrict）
                    AffineMatrix? correctionMatrix = null;
                    if (parameters.IsPositionCorrectionEnabled)
                    {
                        var bindingSource = parameters.PositionCorrectionBindingSource;
                        if (PositionCorrectionResolver.ResolveFirstMatrix(bindingSource) is { } matrix)
                        {
                            correctionMatrix = matrix;
                            parameters.LogInfo($"位置修正已启用, 矩阵: {matrix}");
                        }
                        else if (bindingSource != null &&
                                 !string.IsNullOrEmpty(bindingSource.SourceNodeId) &&
                                 !string.IsNullOrEmpty(bindingSource.PropertyName))
                        {
                            parameters.LogInfo("位置修正已启用，但绑定源当前无有效矩阵，跳过位置修正");
                        }
                        else
                        {
                            parameters.LogWarning("位置修正已启用但未配置绑定源，跳过位置修正");
                        }
                    }

                    var scope = HalconRegionPipeline.Resolve(
                        imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiType.Inspection),
                        parameters.GetRoisByType(RoiType.Mask),
                        parameters.NodeResults, correctionMatrix);
                    stepTimings.AddRange(scope.Timings);

                    if (scope.RoiRegion != null)
                    {
                        HOperatorSet.AreaCenter(scope.RoiRegion, out var whitePixels, out _, out _);
                        parameters.LogInfo($"ROI区域创建成功，白色像素数: {whitePixels.L}");
                    }
                    else
                    {
                        parameters.LogInfo("未配置检测区域，执行全图分析");
                    }

                    // 算法决策：按域执行（区域限制落地到图像 Domain；所有权随 reducedImage，用后 finally 释放）
                    var reducedImage = HalImageDomain.Restrict(grayImage, scope.RoiRegion);

                    try
                    {
                            // ===== Step 4: 滤波处理 =====
                            stepSw.Restart();
                            HImage filteredImage;

                            switch (filterType)
                            {
                                case FilterType.Blur:
                                    HOperatorSet.MeanImage(reducedImage, out var meanImg,
                                        kernelWidth, kernelHeight);
                                    filteredImage = new HImage(meanImg);
                                    break;
                                case FilterType.Gaussian:
                                    // Halcon SmoothImage "gauss" 以核尺寸为参数，sigma 仅作保留参数
                                    HOperatorSet.SmoothImage(reducedImage, out var gaussImg,
                                        "gauss", Math.Max(kernelWidth, kernelHeight));
                                    filteredImage = new HImage(gaussImg);
                                    break;
                                case FilterType.MedianBlur:
                                    HOperatorSet.MedianImage(reducedImage, out var medianImg,
                                        "circle", kernelWidth / 2, "mirrored");
                                    filteredImage = new HImage(medianImg);
                                    break;
                                case FilterType.Bilateral:
                                    HOperatorSet.BilateralFilter(reducedImage, reducedImage,
                                        out var bilateralImg, sigmaSpace, sigmaColor,
                                        new HTuple(), new HTuple());
                                    filteredImage = new HImage(bilateralImg);
                                    break;
                                case FilterType.Filter2D:
                                    // 默认使用 3x3 锐化核
                                    var sharpenKernel = new HTuple(
                                        new double[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 });
                                    HOperatorSet.ConvolImage(reducedImage, out var convolImg,
                                        sharpenKernel, "mirrored");
                                    filteredImage = new HImage(convolImg);
                                    break;
                                default:
                                    throw new NotSupportedException($"不支持的滤波类型: {filterType}");
                            }
                            stepSw.Stop();
                            stepTimings.Add(("滤波处理", stepSw.ElapsedMilliseconds));

                            using (filteredImage)
                            {
                                result.OutputImage = new HalImage(filteredImage.Clone());
                                result.FilterTypeUsed = filterType.ToString();
                                result.KernelWidthUsed = kernelWidth;
                                result.KernelHeightUsed = kernelHeight;

                                // ===== Step 5: 统计信息 =====
                                stepSw.Restart();
                                ComputeStatistics(result, filteredImage, imgWidth, imgHeight);
                                stepSw.Stop();
                                stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                                // ===== Step 6: 结果判断 =====
                                stepSw.Restart();
                                EvaluateResult(result, parameters.ResultConfig);
                                stepSw.Stop();
                                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                                // 提取检测区/屏蔽区几何输出（RoI 契约：结果树字段聚合 + 下游绑定）
                                ToolRegionHelper.ExtractInspectionGeometries(result, parameters.InspectionRegions?.Regions);
                                ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRegions?.Regions);

                                stopwatch.Stop();
                                result.SetSuccess(stopwatch.Elapsed.TotalMilliseconds);

                                // ===== 性能报告 =====
                                var totalMs = stopwatch.ElapsedMilliseconds;
                                var maxStep = stepTimings.MaxBy(t => t.Ms);
                                var reportLines = new List<string>
                                {
                                    $"===== 滤波性能报告 =====",
                                    $"图像: {imgWidth}x{imgHeight}  总耗时: {totalMs}ms",
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
                        reducedImage.Dispose();
                        scope.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"滤波处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
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
        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new ImageFilterToolDebugControl("ImageFilter", tool, null);
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
        private void ComputeStatistics(ImageFilterResults result, HImage filteredImage, int width, int height)
        {
            using var fullRegion = new HRegion(0.0, 0.0, (double)height - 1, (double)width - 1);

            HOperatorSet.Intensity(fullRegion, filteredImage, out var mean, out var stdDev);
            result.OutputMean = mean.D;
            result.OutputStd = stdDev.D;

            HOperatorSet.MinMaxGray(fullRegion, filteredImage, 0.0, out var minVal, out var maxVal, out _);
            result.OutputMin = minVal.D;
            result.OutputMax = maxVal.D;

            // 通道均值（Halcon 灰度图像为单通道）
            result.ChannelMeans = new[] { mean.D };
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
                if (result.OutputImage == null || result.OutputImage.IsEmpty)
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
