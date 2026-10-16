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
        Icon = "sliders", Order = 10,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Preprocessing,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class ImageFilterTool : IToolPlugin<ImageFilterParameters, ImageFilterResults>, IToolEditorFactory, SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider
    {
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider.ImageFramePolicy => SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy.PreserveCoordinates;
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

                FilterType filterType = parameters.FilterTypeValue;
                int kernelWidth = parameters.KernelWidth;
                int kernelHeight = parameters.KernelHeight;
                double sigmaX = parameters.SigmaX;
                double sigmaY = parameters.SigmaY;
                double sigmaColor = parameters.SigmaColor;
                double sigmaSpace = parameters.SigmaSpace;

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
                    parameters.LogInfo($"滤波灰度类型：输入={hImage.GetImageType()}，灰度={grayImage.GetImageType()}；线性卷积使用real工作类型后恢复原类型，不做8位归一化");
                    result.InputWidth = imgWidth;
                    result.InputHeight = imgHeight;
                    result.InputChannels = channels;

                    // ===== Step 2: 区域前置（HalconRegionPipeline：订阅解析 + 位置修正 + 差集）=====
                    // 纯几何：区域 + 位置修正；全图/按域执行由算法层自决（见下 HalImageDomain.Restrict）
                    var correctionMatrix = PositionCorrectionResolver.Resolve(parameters);

                    var scope = HalconRegionPipeline.Resolve(
                        imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiParameterMode.InspectionRoi),
                        parameters.GetRoisByType(RoiParameterMode.MaskRoi),
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

                            using var filterInput = grayImage.FullDomain();
                            switch (filterType)
                            {
                                case FilterType.Blur:
                                    filteredImage = HalconLinearFilters.Apply(filterInput, kernelWidth, kernelHeight,
                                        0, 0, false, (int)parameters.BorderTypeValue);
                                    break;
                                case FilterType.Gaussian:
                                    filteredImage = HalconLinearFilters.Apply(filterInput, kernelWidth, kernelHeight,
                                        sigmaX, sigmaY, true, (int)parameters.BorderTypeValue);
                                    parameters.LogInfo("高斯滤波：按核宽高截断；Sigma=0按对应核尺寸的±3Sigma估算（最小0.5像素），非零Sigma直接生效");
                                    break;
                                case FilterType.MedianBlur:
                                    HOperatorSet.MedianRect(filterInput, out var medianImg, kernelWidth, kernelHeight);
                                    filteredImage = new HImage(medianImg);
                                    break;
                                case FilterType.Bilateral:
                                    if (parameters.BorderTypeValue != BorderType.Reflect)
                                        throw new NotSupportedException("HALCON双边滤波仅使用原生边界处理，当前仅支持Reflect；其他边界模式不能静默替代");
                                    if (!double.IsFinite(sigmaSpace) || !double.IsFinite(sigmaColor) || sigmaSpace <= 0 || sigmaColor <= 0)
                                        throw new ArgumentException("双边滤波的空间Sigma和颜色Sigma必须为有限正数");
                                    HOperatorSet.BilateralFilter(filterInput, filterInput,
                                        out var bilateralImg, sigmaSpace, sigmaColor,
                                        new HTuple(), new HTuple());
                                    filteredImage = new HImage(bilateralImg);
                                    kernelWidth = kernelHeight = 0; // Native support is sigma-driven, not the old ignored diameter.
                                    parameters.LogInfo("双边滤波使用原生Sigma控制邻域，核宽高结果0表示自动邻域；旧直径字段保留但不参与计算");
                                    break;
                                case FilterType.Filter2D:
                                    if (parameters.UseCustomKernel)
                                    {
                                        var custom = CustomConvolutionKernel.Parse(parameters.CustomKernelText);
                                        kernelWidth = custom.Width;
                                        kernelHeight = custom.Height;
                                        filteredImage = HalconLinearFilters.ApplyKernel(filterInput, kernelWidth, kernelHeight,
                                            custom.Coefficients, parameters.CustomKernelDivisor, (int)parameters.BorderTypeValue);
                                        parameters.LogInfo($"Filter2D：HALCON自定义核相关运算，核={kernelWidth}x{kernelHeight}，除数={parameters.CustomKernelDivisor}；不翻核、不自动归一化，参考点由原生非零核位置决定");
                                    }
                                    else
                                    {
                                        filteredImage = HalconLinearFilters.Sharpen(filterInput, (int)parameters.BorderTypeValue);
                                        kernelWidth = kernelHeight = 3;
                                        parameters.LogInfo("Filter2D：兼容固定3x3锐化核，自定义核未启用");
                                    }
                                    break;
                                default:
                                    throw new NotSupportedException($"不支持的滤波类型: {filterType}");
                            }
                            stepSw.Stop();
                            stepTimings.Add(("滤波处理", stepSw.ElapsedMilliseconds));

                            using (filteredImage)
                            {
                                // Neighborhood context is full image; output/statistics remain confined to the effective ROI-minus-mask.
                                using var effectiveDomain = reducedImage.GetDomain();
                                using var limitedOutput = filteredImage.ReduceDomain(effectiveDomain);
                                result.OutputImage = new HalImage(limitedOutput.Clone());
                                result.FilterTypeUsed = filterType.ToString();
                                result.KernelWidthUsed = kernelWidth;
                                result.KernelHeightUsed = kernelHeight;

                                // ===== Step 5: 统计信息 =====
                                stepSw.Restart();
                                ComputeStatistics(result, limitedOutput, imgWidth, imgHeight);
                                stepSw.Stop();
                                stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                                // ===== Step 6: 结果判断 =====
                                stepSw.Restart();
                                EvaluateResult(result, parameters.ResultPresentation);
                                stepSw.Stop();
                                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                                // 提取检测区/屏蔽区几何输出（RoI 契约：结果树字段聚合 + 下游绑定）
                                // 统一经 SDK ResolveRuntime 出口，传入上游结果上下文支持订阅/拼装区域；
                                // ★ 传入位置修正矩阵使契约几何为修正后坐标（下游订阅区与源显示一致）
                                ToolRegionHelper.ExtractInspectionGeometries(result, parameters.InspectionRois?.Rois,
                                    parameters.NodeResults, correctionMatrix);
                                ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRois?.Rois,
                                    parameters.NodeResults, correctionMatrix);

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
            using var fullRegion = filteredImage.GetDomain();
            using var area = fullRegion.AreaCenter(out HTuple centerRow, out HTuple centerColumn);
            using var realArea = area.TupleReal();
            using (centerRow)
            using (centerColumn)
                if (realArea.Length == 0 || realArea.D == 0)
                {
                    result.OutputMean = result.OutputStd = result.OutputMin = result.OutputMax = 0;
                    result.ChannelMeans = new[] { 0.0 };
                    return;
                }

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
        private static void EvaluateResult(ImageFilterResults result, ResultPresentationConfig config)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(config.Judgments, key => key switch
            {
                "output-not-empty" => result.OutputImage is { IsEmpty: false } ? 1 : 0,
                "mean" => result.OutputMean,
                _ => null,
            });
            result.IsPass = ResultJudgmentEvaluator.IsAllPass(outcomes);
            result.FailReasons = outcomes.Where(item => !item.IsPass).Select(item => item.Message).ToList();
        }
        #endregion
    }
}
