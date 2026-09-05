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
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.Threshold.Views;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 图像阈值化工具 - v3.0 Blob模式重构（Halcon 实现）
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → 缩小定义域（ROI掩码限制，不改变图像尺寸）
    ///   3. threshold      → 阈值分割（固定/自适应）
    ///   4. 统计计算       → 白色像素/比例/均值/质心
    ///   5. 结果判断       → OK/NG判定（5种条件）
    /// </summary>
    [Tool(
        id: "Threshold",
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
            var hImage = parameters.GetHalconImage();
            var result = new ThresholdResults
            {
                ToolName = _attribute?.DisplayName ?? "Threshold",
                ToolId = _attribute?.Id ?? "Threshold",
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
                parameters.LogInfo($"开始阈值化处理: 图像尺寸 {imgWidth}x{imgHeight}");

                double threshold = parameters.Threshold.Value;
                double maxValue = parameters.MaxValue.Value;
                ThresholdType type = parameters.Type.Value;
                AdaptiveMethod adaptiveMethod = parameters.AdaptiveMethod.Value;
                int blockSize = parameters.BlockSize.Value;
                bool invert = parameters.Invert;

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

                    // ===== Step 2: 区域前置（HalconRegionPipeline：订阅解析 + 差集）=====
                    // 纯几何：检测/屏蔽区域订阅解析 + 差集；全图/按域执行由算法层自决（见下 HalImageDomain.Restrict）
                    var scope = HalconRegionPipeline.Resolve(
                        imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiType.Inspection),
                        parameters.GetRoisByType(RoiType.Mask),
                        parameters.NodeResults);
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
                    result.ReducedImage = new HalImage(reducedImage.Clone());

                    try
                    {

                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            HRegion binaryRegion;
                            double actualThreshold;

                            if (type == ThresholdType.Binary || type == ThresholdType.BinaryInv)
                            {
                                HOperatorSet.Threshold(reducedImage, out var binObj,
                                    (int)threshold, (int)maxValue);
                                binaryRegion = new HRegion(binObj);
                                actualThreshold = threshold;
                            }
                            else
                            {
                                // 自适应阈值：Halcon 以 MeanImage 求局部均值后 Threshold（块大小=均值窗口）
                                HOperatorSet.MeanImage(reducedImage, out var meanObj,
                                    blockSize, blockSize);
                                using var meanImage = new HImage(meanObj);
                                HOperatorSet.Threshold(meanImage, out var adBinObj,
                                    (int)threshold, (int)maxValue);
                                binaryRegion = new HRegion(adBinObj);
                                actualThreshold = threshold;
                            }

                            if (invert)
                            {
                                HOperatorSet.Complement(binaryRegion, out var complObj);
                                binaryRegion.Dispose();
                                binaryRegion = new HRegion(complObj);
                            }
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // 输出图像：RegionToBin 将二值区域转为输出图
                                HOperatorSet.RegionToBin(binaryRegion, out var binObj,
                                    255, 0, imgWidth, imgHeight);
                                result.BinaryMask = new HalImage(new HImage(binObj.Clone()));
                                result.OutputImage = new HalImage(new HImage(binObj));

                                result.ThresholdUsed = actualThreshold;
                                result.MaxValueUsed = maxValue;
                                result.TypeUsed = type.ToString();
                                result.AdaptiveMethodUsed = adaptiveMethod.ToString();
                                result.BlockSizeUsed = blockSize;
                                result.InputWidth = imgWidth;
                                result.InputHeight = imgHeight;
                                result.InputChannels = channels;

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
                                ToolRegionHelper.ExtractInspectionGeometries(result,
                                    parameters.InspectionRegions?.Regions);
                                ToolRegionHelper.ExtractMaskGeometries(result,
                                    parameters.MaskRegions?.Regions);

                                stopwatch.Stop();
                                result.SetSuccess(stopwatch.Elapsed.TotalMilliseconds);

                                // ===== 性能报告 =====
                                var totalMs = stopwatch.ElapsedMilliseconds;
                                var maxStep = stepTimings.MaxBy(t => t.Ms);
                                var reportLines = new List<string>
                                {
                                    $"===== 阈值化性能报告 =====",
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
                                reportLines.Add($"结果: 阈值 {actualThreshold:F0}, 类型 {type}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
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
                parameters.LogError($"阈值化处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
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
        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new ThresholdToolDebugControl("Threshold", tool, null);
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
        private void ComputeStatistics(ThresholdResults result, HRegion binaryRegion, HImage reducedImage, int width, int height)
        {
            HOperatorSet.AreaCenter(binaryRegion, out var area, out var row, out var col);
            var whitePixelCount = (int)area.L;
            var totalPixels = width * height;
            result.WhitePixelCount = whitePixelCount;
            result.WhitePixelRatio = totalPixels > 0 ? (double)whitePixelCount / totalPixels * 100 : 0;
            result.OutputArea = whitePixelCount;

            if (whitePixelCount > 0)
            {
                result.CentroidX = col.D;
                result.CentroidY = row.D;
            }

            // 二值区域灰度均值
            HOperatorSet.Intensity(binaryRegion, reducedImage, out var mean, out _);
            result.OutputMean = mean.D;

            // 灰度直方图
            HOperatorSet.GrayHisto(binaryRegion, reducedImage, out var absoluteHisto, out _);
            var histArr = absoluteHisto.LArr;
            result.HistogramData = Array.ConvertAll(histArr, v => (double)v);

            double histMean = histArr.Length > 0
                ? histArr.Select((h, i) => h * i).Sum() / (double)histArr.Sum()
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
