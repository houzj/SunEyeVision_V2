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
using SunEyeVision.Tool.Morphology.Views;

namespace SunEyeVision.Tool.Morphology
{
    /// <summary>
    /// 形态学处理工具 - v1.0（Halcon 实现）
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → ROI掩码限制
    ///   3. threshold      → 固定阈值二值化 (128)
    ///   4. 形态学操作     → Erosion1/Dilation1/Opening/Closing/TopHat/BottomHat
    ///   5. 区域统计       → Blob计数/面积统计
    ///   6. 结果判断       → OK/NG判定
    /// </summary>
[Tool(
        id: "Morphology",
        displayName: "形态学处理",
        Description = "对二值图像执行形态学操作（腐蚀/膨胀/开闭运算等）",
        Icon = "morphology", Order = 40,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Preprocessing,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class MorphologyTool : IToolPlugin<MorphologyParameters, MorphologyResults>, IToolEditorFactory, SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider
    {
        SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy SunEyeVision.Plugin.SDK.Execution.Helpers.IImageFramePolicyProvider.ImageFramePolicy => SunEyeVision.Plugin.SDK.Execution.Helpers.ImageFramePolicy.PreserveCoordinates;
        private readonly ToolAttribute? _attribute;

        public MorphologyTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具
        /// </summary>
        public MorphologyResults Run(MorphologyParameters parameters)
        {
            var hImage = parameters.GetHalconImage();
            var result = new MorphologyResults
            {
                ToolName = _attribute?.DisplayName ?? "Morphology",
                ToolId = _attribute?.Id ?? "Morphology",
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
                parameters.LogInfo($"开始形态学处理: 图像尺寸 {imgWidth}x{imgHeight}");

                var operation = parameters.Operation;
                var kernelShape = parameters.KernelShape;
                var kernelWidth = parameters.KernelWidth;
                var kernelHeight = parameters.KernelHeight;
                var iterations = parameters.Iterations;

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
                            // ===== Step 4: 固定阈值二值化 =====
                            stepSw.Restart();
                            HOperatorSet.Threshold(reducedImage, out var binRegion,
                                128, "max");
                            stepSw.Stop();
                            stepTimings.Add(("二值化", stepSw.ElapsedMilliseconds));

                            using (binRegion)
                            {
                                // ===== Step 5: 形态学操作 =====
                                stepSw.Restart();
                                HRegion morphRegion;
                                using (var se = BuildStructuringElement(kernelShape, kernelWidth, kernelHeight))
                                using (var inputRegion = new HRegion(binRegion))
                                using (var effectiveDomain = reducedImage.GetDomain())
                                using (var expanded = ApplyMorphology(inputRegion, se, operation, iterations))
                                {
                                    // Dilation/closing must not resurrect excluded mask pixels or leave the ROI.
                                    morphRegion = expanded.Intersection(effectiveDomain);
                                }
                                stepSw.Stop();
                                stepTimings.Add(("形态学操作", stepSw.ElapsedMilliseconds));

                                using (morphRegion)
                                {
                                    // ===== Step 6: 区域转换与统计 =====
                                    stepSw.Restart();

                                    // 输出图像：RegionToBin
                                    HOperatorSet.RegionToBin(morphRegion, out var binObj,
                                        255, 0, imgWidth, imgHeight);
                                    result.OutputImage = new HalImage(new HImage(binObj.Clone()));
                                    result.BinaryMask = new HalImage(new HImage(binObj));

                                    // 参数回写
                                    result.OperationUsed = operation.ToString();
                                    result.KernelShapeUsed = kernelShape.ToString();
                                    result.KernelWidthUsed = kernelWidth;
                                    result.KernelHeightUsed = kernelHeight;
                                    result.InputWidth = imgWidth;
                                    result.InputHeight = imgHeight;
                                    result.InputChannels = channels;
                                    result.IterationsUsed = iterations;

                                    // 连通域分析 → Blob 统计
                                    HOperatorSet.Connection(morphRegion, out var connectedObj);
                                    using var connectedRegion = new HRegion(connectedObj);
                                    HOperatorSet.CountObj(connectedRegion, out var numObj);
                                    int blobCount = (int)numObj.L;
                                    result.BlobCount = blobCount;

                                    if (blobCount > 0)
                                    {
                                        HOperatorSet.AreaCenter(connectedRegion, out var areas, out _, out _);
                                        long totalArea = 0;
                                        long maxArea = 0;
                                        for (int i = 0; i < areas.Length; i++)
                                        {
                                            long a = areas.LArr[i];
                                            totalArea += a;
                                            if (a > maxArea) maxArea = a;
                                        }
                                        result.TotalArea = totalArea;
                                        result.MaxBlobArea = maxArea;
                                    }
                                    else
                                    {
                                        result.TotalArea = 0;
                                        result.MaxBlobArea = 0;
                                    }

                                    stepSw.Stop();
                                    stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                                    // ===== Step 7: 结果判断 =====
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
                                        $"===== 形态学性能报告 =====",
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
                                    reportLines.Add($"结果: 操作 {operation}, 形状 {kernelShape}, 核 {kernelWidth}x{kernelHeight}, 判断 {(result.IsPass ? "OK" : "NG")}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                    parameters.LogInfo(string.Join("\n", reportLines));
                                }
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
                parameters.LogError($"形态学处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(MorphologyParameters parameters)
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
            return new MorphologyToolDebugControl("Morphology", tool, null);
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new MorphologyDataAnalysisControl();
        }

        #region 私有方法

        /// <summary>
        /// 构建结构元素 HRegion
        /// </summary>
        private static HRegion BuildStructuringElement(MorphKernelShape shape, int width, int height)
        {
            int rx = width / 2, ry = height / 2;
            switch (shape)
            {
                case MorphKernelShape.Rect:
                    // HALCON centers the structuring element by its centroid; keep coordinates nonnegative.
                    HOperatorSet.GenRectangle1(out var rect, 0, 0, height - 1, width - 1);
                    return new HRegion(rect);
                case MorphKernelShape.Cross:
                    using (var horizontal = new HRegion((double)ry, 0.0, (double)ry, width - 1.0))
                    using (var vertical = new HRegion(0.0, (double)rx, height - 1.0, (double)rx))
                        return horizontal.Union2(vertical);
                case MorphKernelShape.Ellipse:
                    // A one-pixel axis is a line/point, not an inflated circle.
                    if (rx == 0 || ry == 0) return new HRegion(0.0, 0.0, height - 1.0, width - 1.0);
                    HOperatorSet.GenEllipse(out var ellipse, ry, rx,
                        rx >= ry ? 0 : Math.PI / 2, Math.Max(rx, ry), Math.Min(rx, ry));
                    return new HRegion(ellipse);
                default:
                    throw new NotSupportedException($"不支持的结构元素: {shape}");
            }
        }

        /// <summary>
        /// 应用形态学操作
        /// </summary>
        private static HRegion ApplyMorphology(HRegion inputRegion, HRegion se, MorphOperation operation, int iterations)
        {
            switch (operation)
            {
                case MorphOperation.Erode:
                    HOperatorSet.Erosion1(inputRegion, se, out var erodeObj, iterations);
                    return new HRegion(erodeObj);
                case MorphOperation.Dilate:
                    HOperatorSet.Dilation1(inputRegion, se, out var dilateObj, iterations);
                    return new HRegion(dilateObj);
                case MorphOperation.Open:
                    using (var eroded = ApplyMorphology(inputRegion, se, MorphOperation.Erode, iterations))
                    {
                        HOperatorSet.Dilation1(eroded, se, out var openObj, iterations);
                        return new HRegion(openObj);
                    }
                case MorphOperation.Close:
                    using (var dilated = ApplyMorphology(inputRegion, se, MorphOperation.Dilate, iterations))
                    {
                        HOperatorSet.Erosion1(dilated, se, out var closeObj, iterations);
                        return new HRegion(closeObj);
                    }
                case MorphOperation.Gradient:
                    using (var dilated = ApplyMorphology(inputRegion, se, MorphOperation.Dilate, iterations))
                    using (var eroded = ApplyMorphology(inputRegion, se, MorphOperation.Erode, iterations))
                        return dilated.Difference(eroded);
                case MorphOperation.TopHat:
                    using (var opened = ApplyMorphology(inputRegion, se, MorphOperation.Open, iterations))
                        return inputRegion.Difference(opened);
                case MorphOperation.BlackHat:
                    using (var closed = ApplyMorphology(inputRegion, se, MorphOperation.Close, iterations))
                        return closed.Difference(inputRegion);
                default:
                    throw new NotSupportedException($"不支持的形态学操作: {operation}");
            }
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private static void EvaluateResult(MorphologyResults result, ResultPresentationConfig config)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(config.Judgments, key => key switch
            {
                "output-not-empty" => result.OutputImage is { IsEmpty: false } ? 1 : 0,
                "area" => result.TotalArea,
                "blob-count" => result.BlobCount,
                "max-blob-area" => result.MaxBlobArea,
                _ => null,
            });
            result.IsPass = ResultJudgmentEvaluator.IsAllPass(outcomes);
            result.FailReasons = outcomes.Where(item => !item.IsPass).Select(item => item.Message).ToList();
        }
        #endregion
    }
}
