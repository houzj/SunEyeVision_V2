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
        Icon = "binarize", Order = 30,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Preprocessing,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class ImageThresholdTool : IToolPlugin<ImageThresholdParameters, ImageThresholdResults>, IToolEditorFactory, SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider
    {
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider.ImageFramePolicy => SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy.PreserveCoordinates;
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

                var method = parameters.Method;
                int threshold = parameters.Threshold;
                int maxValue = parameters.MaxValue;
                int blockSize = parameters.BlockSize;
                double c = parameters.C;
                int minThreshold = parameters.MinThreshold;
                int maxThreshold = parameters.MaxThreshold;
                bool invert = parameters.Invert;

                // ===== Step 1: 灰度转换 =====
                stepSw.Restart();
                int channels = hImage.CountChannels();
                var grayImage = HalconImageAdmission.Gray(hImage);
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                using (grayImage)
                {
                    result.GrayImage = new HalImage(grayImage.Clone());

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
                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            HRegion binaryRegion;
                            double actualThreshold = 0;

                            switch (method)
                            {
                                case ThresholdMethod.Binary:
                                case ThresholdMethod.BinaryInv:
                                case ThresholdMethod.Trunc:
                                case ThresholdMethod.ToZero:
                                case ThresholdMethod.ToZeroInv:
                                    binaryRegion = HalconThresholdOperations.SelectBasic(reducedImage, (int)method, threshold);
                                    actualThreshold = threshold;
                                    break;
                                case ThresholdMethod.Otsu:
                                    HOperatorSet.BinaryThreshold(reducedImage, out var otsuObj,
                                        "max_separability", "dark", out var usedOtsu);
                                    binaryRegion = new HRegion(otsuObj);
                                    actualThreshold = usedOtsu.D;
                                    break;
                                case ThresholdMethod.Triangle:
                                    var triangleThreshold = ComputeTriangleThreshold(reducedImage);
                                    binaryRegion = HalconThresholdOperations.SelectBasic(reducedImage, 0, triangleThreshold);
                                    actualThreshold = triangleThreshold;
                                    break;
                                case ThresholdMethod.AdaptiveMean:
                                case ThresholdMethod.AdaptiveGaussian:
                                    binaryRegion = HalconThresholdOperations.SelectAdaptive(reducedImage, blockSize, c,
                                        method == ThresholdMethod.AdaptiveGaussian);
                                    actualThreshold = -c; // Local threshold offset, not a global gray threshold.
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
                                var inverse = HalconThresholdOperations.InvertWithinDomain(reducedImage, binaryRegion);
                                binaryRegion.Dispose();
                                binaryRegion = inverse;
                            }
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // 二值掩码输出
                                HOperatorSet.RegionToBin(binaryRegion, out var binObj,
                                    255, 0, imgWidth, imgHeight);
                                using var binaryObject = binObj;
                                result.OutputImage = new HalImage(HalconThresholdOperations.Render(reducedImage, binaryRegion,
                                    (int)method, threshold, maxValue, imgWidth, imgHeight, invert));
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
                        reducedImage.Dispose();
                        scope.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"二值化处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
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
        /// 旧方案兼容的三角形法；HALCON生成有效域直方图，兼容计算选择较长尾部。
        /// 算法：连接直方图峰值与边界点形成三角形，取到直方图曲线距离最远的谷点作为阈值。
        /// </summary>
        private int ComputeTriangleThreshold(HImage image)
        {
            if (image.GetImageType() != "byte")
                throw new NotSupportedException("Triangle兼容模式仅支持byte灰度，未隐式压缩位深；请显式选择输入转换或其他阈值模式");
            using var fullRegion = image.GetDomain();
            HOperatorSet.GrayHisto(fullRegion, image, out var absoluteHisto, out var relativeHisto);
            using var ownedAbsolute = absoluteHisto;
            using var ownedRelative = relativeHisto;
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

            int left = 0, right = hist.Length - 1;
            while (left < right && hist[left] == 0) left++;
            while (right > left && hist[right] == 0) right--;
            if (left == right || maxCount == 0) return maxIndex;
            // Include one zero bin beyond each occupied tail where possible.
            if (left > 0) left--;
            if (right < hist.Length - 1) right++;
            int start = maxIndex;
            int end = maxIndex - left > right - maxIndex ? left : right;

            if (end == start || maxCount == 0)
                return maxIndex;

            int bestThreshold = start;
            double maxDist = -1;
            double dx = end - start;
            double dy = (double)hist[end] - hist[start];
            double denominator = Math.Sqrt(dx * dx + dy * dy);
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
            {
                double dist = Math.Abs(dy * (i - start) - dx * ((double)hist[i] - hist[start])) / denominator;
                if (dist > maxDist)
                {
                    maxDist = dist;
                    bestThreshold = i;
                }
            }

            return bestThreshold;
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

            if (whitePixelCount == 0)
            {
                result.OutputMean = 0;
                result.HistogramData = new double[256];
                return;
            }

            HOperatorSet.Intensity(binaryRegion, reducedImage, out var mean, out _);
            result.OutputMean = mean.D;

            HOperatorSet.GrayHisto(binaryRegion, reducedImage, out var absoluteHisto, out _);
            result.HistogramData = Array.ConvertAll(absoluteHisto.LArr, v => (double)v);
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private static void EvaluateResult(ImageThresholdResults result, ResultPresentationConfig config)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(config.Judgments, key => key switch
            {
                "output-not-empty" => result.OutputImage is { IsEmpty: false } ? 1 : 0,
                "white-ratio" => result.WhitePixelRatio,
                "mean" => result.OutputMean,
                "area" => result.OutputArea,
                _ => null,
            });
            result.IsPass = ResultJudgmentEvaluator.IsAllPass(outcomes);
            result.FailReasons = outcomes.Where(item => !item.IsPass).Select(item => item.Message).ToList();
        }
        #endregion
    }
}
