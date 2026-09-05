using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.ImageThreshold.Views;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 图像二值化工具（Halcon 实现）
    /// 算法流程：
    ///   1. Rgb1ToGray → 灰度转换
    ///   2. ReduceDomain → 缩小定义域（ROI限制）
    ///   3. 阈值分割（按 Method 选择策略）
    ///   4. 统计计算 → 白色像素/比例/均值/面积
    ///   5. 结果判断 → OK/NG判定
    ///   6. RegionToBin → 二值掩码输出
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
            var hImage = parameters.GetHalconImage();
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
                if (!hImage.IsInitialized())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                HOperatorSet.GetImageSize(hImage, out var imgW, out var imgH);
                int imgWidth = (int)imgW.L;
                int imgHeight = (int)imgH.L;
                parameters.LogInfo($"开始二值化处理: 图像尺寸 {imgWidth}x{imgHeight}");
                result.InputWidth = imgWidth;
                result.InputHeight = imgHeight;
                result.InputChannels = hImage.CountChannels();

                var method = parameters.Method.Value;
                int threshold = parameters.Threshold.Value;
                int maxValue = parameters.MaxValue.Value;
                int blockSize = parameters.BlockSize.Value;
                double c = parameters.C.Value;
                int minThreshold = parameters.MinThreshold.Value;
                int maxThreshold = parameters.MaxThreshold.Value;
                bool invert = parameters.Invert;

                // ===== Step 1: 灰度转换 =====
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
                    result.GrayImage = new HalImage(grayImage.Clone());

                    // ===== Step 2+3: 公共区域前置（HalconImageOpPipeline：ROI/掩码构建 + ReduceDomain）=====
                    // 区域语义统一：有检测/屏蔽区域即限制，无区域全图；中间图像与区域所有权随 masked 统一释放
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

                    var masked = HalconImageOpPipeline.Prepare(
                        grayImage, imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiType.Inspection),
                        parameters.GetRoisByType(RoiType.Mask),
                        parameters.NodeResults, correctionMatrix);
                    stepTimings.AddRange(masked.Timings);

                    if (masked.RoiRegion != null)
                    {
                        HOperatorSet.AreaCenter(masked.RoiRegion, out var whitePixels, out _, out _);
                        parameters.LogInfo($"ROI区域创建成功，白色像素数: {whitePixels.L}");
                    }
                    else
                    {
                        parameters.LogInfo("未配置检测区域，执行全图分析");
                    }

                    // 核心算子输入（已 ReduceDomain 或全图，只读使用；生命周期随 masked）
                    var reducedImage = masked.WorkingImage;

                    try
                    {
                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            HRegion binaryRegion;
                            double actualThreshold = 0;

                            switch (method)
                            {
                                case ThresholdMethod.Binary:
                                    HOperatorSet.Threshold(reducedImage, out var binObj,
                                        threshold, maxValue);
                                    binaryRegion = new HRegion(binObj);
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.BinaryInv:
                                    HOperatorSet.Threshold(reducedImage, out var binInvObj,
                                        threshold, maxValue);
                                    HOperatorSet.Complement(binInvObj, out var complInvObj);
                                    binaryRegion = new HRegion(complInvObj);
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.Trunc:
                                case ThresholdMethod.ToZero:
                                    // Halcon 无像素级截断算子，以 [threshold, maxValue] 区间区域二值化近似
                                    HOperatorSet.Threshold(reducedImage, out var truncObj,
                                        threshold, maxValue);
                                    binaryRegion = new HRegion(truncObj);
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.ToZeroInv:
                                    // 反向截断近似：取 [0, threshold] 区间
                                    HOperatorSet.Threshold(reducedImage, out var tozeroInvObj,
                                        0, threshold);
                                    binaryRegion = new HRegion(tozeroInvObj);
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.Otsu:
                                    HOperatorSet.BinaryThreshold(reducedImage, out var otsuObj,
                                        "max_separability", "dark", out var usedOtsu);
                                    binaryRegion = new HRegion(otsuObj);
                                    actualThreshold = usedOtsu.D;
                                    break;
                                case ThresholdMethod.Triangle:
                                    var triangleThreshold = ComputeTriangleThreshold(reducedImage, imgWidth, imgHeight);
                                    HOperatorSet.Threshold(reducedImage, out var triObj,
                                        triangleThreshold, maxValue);
                                    binaryRegion = new HRegion(triObj);
                                    actualThreshold = triangleThreshold;
                                    break;
                                case ThresholdMethod.AdaptiveMean:
                                    HOperatorSet.MeanImage(reducedImage, out var meanImg,
                                        blockSize, blockSize);
                                    using (var meanImage = new HImage(meanImg))
                                    {
                                        HOperatorSet.Threshold(meanImage, out var adMeanObj,
                                            (int)(threshold + c), maxValue);
                                        binaryRegion = new HRegion(adMeanObj);
                                    }
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.AdaptiveGaussian:
                                    HOperatorSet.SmoothImage(reducedImage, out var gaussImg,
                                        "gauss", blockSize);
                                    using (var gaussImage = new HImage(gaussImg))
                                    {
                                        HOperatorSet.Threshold(gaussImage, out var adGaussObj,
                                            (int)(threshold + c), maxValue);
                                        binaryRegion = new HRegion(adGaussObj);
                                    }
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.InRange:
                                    HOperatorSet.Threshold(reducedImage, out var inRangeObj,
                                        minThreshold, maxThreshold);
                                    binaryRegion = new HRegion(inRangeObj);
                                    actualThreshold = minThreshold;
                                    break;
                                default:
                                    throw new NotSupportedException($"不支持的阈值方法: {method}");
                            }

                            // 应用 invert（对可二值化的方法）
                            if (invert && method is ThresholdMethod.Binary or ThresholdMethod.BinaryInv
                                                     or ThresholdMethod.Otsu or ThresholdMethod.Triangle
                                                     or ThresholdMethod.AdaptiveMean or ThresholdMethod.AdaptiveGaussian
                                                     or ThresholdMethod.InRange)
                            {
                                HOperatorSet.Complement(binaryRegion, out var compObj);
                                binaryRegion.Dispose();
                                binaryRegion = new HRegion(compObj);
                            }
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // 二值掩码输出
                                HOperatorSet.RegionToBin(binaryRegion, out var binObj,
                                    255, 0, imgWidth, imgHeight);
                                result.OutputImage = new HalImage(new HImage(binObj.Clone()));
                                result.BinaryMask = new HalImage(new HImage(binObj));

                                result.ThresholdUsed = actualThreshold;
                                result.MaxValueUsed = maxValue;
                                result.MethodUsed = method.ToString();

                                // ===== Step 5: 统计信息 =====
                                stepSw.Restart();
                                ComputeStatistics(result, binaryRegion, reducedImage, imgWidth, imgHeight);
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
                                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                                // ===== 性能报告 =====
                                var totalMs = stopwatch.ElapsedMilliseconds;
                                var maxStep = stepTimings.MaxBy(t => t.Ms);
                                var reportLines = new List<string>
                                {
                                    $"===== 图像二值化性能报告 =====",
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
                                reportLines.Add($"结果: 方法 {method}, 阈值 {actualThreshold:F0}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                parameters.LogInfo(string.Join("\n", reportLines));
                            }
                    }
                    finally
                    {
                        masked.Dispose();
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
        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new ImageThresholdToolDebugControl("ImageThreshold", tool, null);
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
        /// 基于灰度直方图的三角形法阈值计算（Halcon 无对应算子，按业界标准算法自实现）
        /// 算法：连接直方图峰值与边界点形成三角形，取到直方图曲线距离最远的谷点作为阈值。
        /// </summary>
        private int ComputeTriangleThreshold(HImage image, int width, int height)
        {
            using var fullRegion = new HRegion(0.0, 0.0, (double)height - 1, (double)width - 1);
            HOperatorSet.GrayHisto(fullRegion, image, out var absoluteHisto, out _);
            var hist = absoluteHisto.LArr;

            // 直方图峰值（灰度级）
            int maxIndex = 0;
            long maxCount = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i] > maxCount)
                {
                    maxCount = hist[i];
                    maxIndex = i;
                }
            }

            // 以峰值一侧（低灰度侧）构建三角形
            int start = maxIndex;
            int end = hist.Length - 1;
            while (end > start && hist[end] == 0) end--;

            if (end <= start)
                return maxIndex;

            long h = hist[start];
            double bestThreshold = start;
            double maxDist = -1;
            for (int i = start; i <= end; i++)
            {
                // 点 (i, hist[i]) 到直线 (start, h)-(end, 0) 的距离
                double dx = end - start;
                double dy = -h;
                double dist = Math.Abs(dy * i - dx * hist[i] + dx * h) / Math.Sqrt(dx * dx + dy * dy);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    bestThreshold = i;
                }
            }

            return (int)Math.Clamp(bestThreshold, 0, 255);
        }

        /// <summary>
        /// 计算统计结果
        /// </summary>
        private void ComputeStatistics(ImageThresholdResults result, HRegion binaryRegion, HImage reducedImage, int width, int height)
        {
            HOperatorSet.AreaCenter(binaryRegion, out var area, out _, out _);
            var whitePixelCount = (int)area.L;
            var totalPixels = width * height;
            result.WhitePixelCount = whitePixelCount;
            result.WhitePixelRatio = totalPixels > 0 ? (double)whitePixelCount / totalPixels * 100 : 0;
            result.OutputArea = whitePixelCount;

            HOperatorSet.Intensity(binaryRegion, reducedImage, out var mean, out _);
            result.OutputMean = mean.D;

            HOperatorSet.GrayHisto(binaryRegion, reducedImage, out var absoluteHisto, out _);
            result.HistogramData = Array.ConvertAll(absoluteHisto.LArr, v => (double)v);
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
                if (result.OutputImage == null || result.OutputImage.IsEmpty)
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
