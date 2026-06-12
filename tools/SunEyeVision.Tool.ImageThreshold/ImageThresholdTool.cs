using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageThreshold.Views;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 图像二值化工具
    /// 算法流程：
    ///   1. Rgb1ToGray → 灰度转换
    ///   2. ReduceDomain → 缩小定义域（ROI限制）
    ///   3. 阈值分割（按 Method 选择策略）
    ///   4. 统计计算 → 白色像素/比例/均值/面积
    ///   5. 结果判断 → OK/NG判定
    ///   6. ComposeDisplay → 合成显示图像
    /// </summary>
    [Tool(
        id: "ImageThreshold",
        displayName: "图像二值化",
        Description = "将灰度图像通过阈值分割转换为二值图像，支持固定阈值/Otsu/Triangle/自适应/范围阈值",
        Icon = "📷",
        Category = "图像处理",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class ImageThresholdTool : IToolPlugin<ImageThresholdParameters, ImageThresholdResults>
    {
        private readonly ToolAttribute? _attribute;

        public ImageThresholdTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）
        /// </summary>
        public ImageThresholdResults Run(ImageThresholdParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            var result = new ImageThresholdResults
            {
                ToolName = _attribute?.DisplayName ?? "ImageThreshold",
                ToolId = _attribute?.Id ?? "ImageThreshold",
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

                parameters.LogInfo($"开始二值化处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();
                result.InputChannels = image.Channels();

                var method = parameters.Method.Value;
                int threshold = parameters.Threshold.Value;
                int maxValue = parameters.MaxValue.Value;
                int blockSize = parameters.BlockSize.Value;
                double c = parameters.C.Value;
                int minThreshold = parameters.MinThreshold.Value;
                int maxThreshold = parameters.MaxThreshold.Value;
                bool invert = parameters.Invert;
                bool isRegionEnabled = parameters.IsRegionEnabled;

                // ===== Step 1: 灰度转换 =====
                stepSw.Restart();
                CvOp.Rgb1ToGray(image, out var grayImage);
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                using (grayImage)
                {
                    result.GrayImage = grayImage.Image.Clone();

                    CvRegion? roiRegion = null;

                    // ===== Step 2: 构建ROI区域 =====
                    stepSw.Restart();
                    if (isRegionEnabled)
                    {
                        var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                        var maskRegions = parameters.GetRegionsByType(RegionType.Mask);
                        roiRegion = CvRegionHelper.BuildROI(grayImage.Size, inspectionRegions, maskRegions);
                        if (roiRegion != null)
                            parameters.LogInfo($"ROI区域创建成功，白色像素数: {roiRegion.PixelCount}");
                    }
                    stepSw.Stop();
                    stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

                    // ===== Step 2.5: 位置修正（正向变换 ROI 掩码） =====
                    if (parameters.IsPositionCorrectionEnabled)
                    {
                        stepSw.Restart();
                        var bindingSource = parameters.PositionCorrectionBindingSource;
                        if (bindingSource?.CurrentValue is AffineMatrix matrix)
                        {
                            roiRegion = CvRegionHelper.ApplyPositionCorrection(
                                roiRegion, matrix, grayImage.Size);
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
                        stepSw.Stop();
                        stepTimings.Add(("位置修正", stepSw.ElapsedMilliseconds));
                    }

                    try
                    {
                        // ===== Step 3: ReduceDomain =====
                        stepSw.Restart();
                        CvOp.ReduceDomain(grayImage, roiRegion, out var reducedImage);
                        stepSw.Stop();
                        stepTimings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));

                        using (reducedImage)
                        {
                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            using var binaryMat = new Mat();
                            double actualThreshold = 0;

                            var grayMat = reducedImage.Image;

                            switch (method)
                            {
                                case ThresholdMethod.Binary:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, threshold, maxValue, ThresholdTypes.Binary);
                                    break;
                                case ThresholdMethod.BinaryInv:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, threshold, maxValue, ThresholdTypes.BinaryInv);
                                    break;
                                case ThresholdMethod.Trunc:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, threshold, maxValue, ThresholdTypes.Trunc);
                                    break;
                                case ThresholdMethod.ToZero:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, threshold, maxValue, ThresholdTypes.Tozero);
                                    break;
                                case ThresholdMethod.ToZeroInv:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, threshold, maxValue, ThresholdTypes.TozeroInv);
                                    break;
                                case ThresholdMethod.Otsu:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, 0, maxValue, ThresholdTypes.Otsu | ThresholdTypes.Binary);
                                    break;
                                case ThresholdMethod.Triangle:
                                    actualThreshold = Cv2.Threshold(grayMat, binaryMat, 0, maxValue, ThresholdTypes.Triangle | ThresholdTypes.Binary);
                                    break;
                                case ThresholdMethod.AdaptiveMean:
                                    Cv2.AdaptiveThreshold(grayMat, binaryMat, maxValue, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, blockSize, c);
                                    break;
                                case ThresholdMethod.AdaptiveGaussian:
                                    Cv2.AdaptiveThreshold(grayMat, binaryMat, maxValue, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, blockSize, c);
                                    break;
                                case ThresholdMethod.InRange:
                                    Cv2.InRange(grayMat, new Scalar(minThreshold), new Scalar(maxThreshold), binaryMat);
                                    break;
                            }

                            // 应用 invert（对于非Trunc/ToZero/ToZeroInv模式）
                            if (invert && method is ThresholdMethod.Binary or ThresholdMethod.BinaryInv
                                                 or ThresholdMethod.Otsu or ThresholdMethod.Triangle
                                                 or ThresholdMethod.AdaptiveMean or ThresholdMethod.AdaptiveGaussian
                                                 or ThresholdMethod.InRange)
                            {
                                Cv2.BitwiseNot(binaryMat, binaryMat);
                            }

                            // ReduceDomain 已处理 ROI 掩码，直接构造二值区域
                            var binaryRegion = CvRegion.FromMask(binaryMat.Clone(), ownsData: true);

                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // 合成显示图像
                                CvOp.ComposeDisplay(reducedImage, null, binaryRegion, out var outputImage);
                                result.OutputImage = outputImage;

                                // 提取二值掩码
                                CvOp.RegionToMask(binaryRegion, out var binaryMask);
                                result.BinaryMask = binaryMask;

                                // 图形结果输出
                                result.OutputRegions = new IVisionRegion[] { binaryRegion.Clone() };

                                result.ThresholdUsed = actualThreshold;
                                result.MaxValueUsed = maxValue;
                                result.MethodUsed = method;

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
                                    $"===== 图像二值化性能报告 =====",
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
                                reportLines.Add($"结果: 方法 {method}, 阈值 {actualThreshold:F0}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
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
                parameters.LogError($"二值化处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(ImageThresholdParameters parameters)
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
            return new ImageThresholdToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new ImageThresholdDataAnalysisControl();
        }

        #region 私有方法

        /// <summary>
        /// 计算统计结果
        /// </summary>
        private void ComputeStatistics(ImageThresholdResults result, Mat outputImage, Mat inputImage)
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
            }

            using var tempCvImage = new CvImage(inputImage);
            CvOp.Intensity(tempCvImage, null,
                out var histogram, out _, out _);

            result.HistogramData = histogram;
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private void EvaluateResult(ImageThresholdResults result, Models.ImageThresholdResultConfig config)
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

            result.IsPass = allPassed;
            result.FailReasons = failReasons;
        }

        #endregion
    }
}
