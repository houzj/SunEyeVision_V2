using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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
        Icon = "🔲",
        Category = "图像处理",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class MorphologyTool : IToolPlugin<MorphologyParameters, MorphologyResults>
    {
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

                var operation = parameters.Operation.Value;
                var kernelShape = parameters.KernelShape.Value;
                var kernelWidth = parameters.KernelWidth.Value;
                var kernelHeight = parameters.KernelHeight.Value;
                var iterations = parameters.Iterations.Value;
                var isRegionEnabled = parameters.IsRegionEnabled;

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

                    HRegion? roiRegion = null;
                    HRegion? inspHRegion = null;
                    HRegion? maskHRegion = null;

                    // ===== Step 2: 构建ROI区域 =====
                    stepSw.Restart();
                    if (isRegionEnabled)
                    {
                        var inspectionRegions = parameters.GetRoisByType(RoiType.Inspection);
                        var maskRegions = parameters.GetRoisByType(RoiType.Mask);
                        (roiRegion, inspHRegion, maskHRegion) = ToolRegionHelper.BuildHalconROI(
                            imgWidth, imgHeight, inspectionRegions, maskRegions, parameters.NodeResults);
                        if (roiRegion != null)
                        {
                            HOperatorSet.AreaCenter(roiRegion, out var whitePixels, out _, out _);
                            parameters.LogInfo($"ROI区域创建成功，白色像素数: {whitePixels.L}");
                        }
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
                            (roiRegion, inspHRegion, maskHRegion) = ToolRegionHelper.ApplyPositionCorrectionHalcon(
                                roiRegion, inspHRegion, maskHRegion, matrix, imgWidth, imgHeight);
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
                        // ===== Step 3: ReduceDomain（ROI掩码限制）=====
                        stepSw.Restart();
                        HImage reducedImage;
                        if (roiRegion != null)
                        {
                            HOperatorSet.ReduceDomain(grayImage, roiRegion, out var reducedObj);
                            reducedImage = new HImage(reducedObj);
                        }
                        else
                        {
                            reducedImage = grayImage.Clone();
                        }
                        stepSw.Stop();
                        stepTimings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));

                        using (reducedImage)
                        {
                            // ===== Step 4: 固定阈值二值化 =====
                            stepSw.Restart();
                            HOperatorSet.Threshold(reducedImage, out var binRegion,
                                128, 255);
                            stepSw.Stop();
                            stepTimings.Add(("二值化", stepSw.ElapsedMilliseconds));

                            using (binRegion)
                            {
                                // ===== Step 5: 形态学操作 =====
                                stepSw.Restart();
                                HRegion morphRegion;
                                using (var se = BuildStructuringElement(kernelShape, kernelWidth, kernelHeight))
                                {
                                    morphRegion = ApplyMorphology(new HRegion(binRegion), se, operation, iterations);
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
                    }
                    finally
                    {
                        roiRegion?.Dispose();
                        inspHRegion?.Dispose();
                        maskHRegion?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"形态学处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
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
            switch (shape)
            {
                case MorphKernelShape.Rect:
                    HOperatorSet.GenRectangle1(out var rect, 0, 0, height - 1, width - 1);
                    return new HRegion(rect);
                case MorphKernelShape.Cross:
                    // 十字核：中心 + 上下左右
                    int cx = width / 2;
                    int cy = height / 2;
                    HOperatorSet.GenRegionPoints(out var cross,
                        new HTuple(new int[] { cy, cy, cy, 0, height - 1 }),
                        new HTuple(new int[] { cx, 0, width - 1, cx, cx }));
                    return new HRegion(cross);
                case MorphKernelShape.Ellipse:
                default:
                    int radius = Math.Max(width, height) / 2;
                    radius = Math.Max(radius, 1);
                    HOperatorSet.GenCircle(out var circle, radius, radius, radius);
                    return new HRegion(circle);
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
                    HOperatorSet.Opening(inputRegion, se, out var openObj);
                    return new HRegion(openObj);
                case MorphOperation.Close:
                    HOperatorSet.Closing(inputRegion, se, out var closeObj);
                    return new HRegion(closeObj);
                case MorphOperation.Gradient:
                    HOperatorSet.Dilation1(inputRegion, se, out var gradDilate, 1);
                    HOperatorSet.Erosion1(inputRegion, se, out var gradErode, 1);
                    HOperatorSet.Difference(gradDilate, gradErode, out var gradientObj);
                    gradDilate.Dispose();
                    gradErode.Dispose();
                    return new HRegion(gradientObj);
                case MorphOperation.TopHat:
                    HOperatorSet.TopHat(inputRegion, se, out var topHatObj);
                    return new HRegion(topHatObj);
                case MorphOperation.BlackHat:
                    HOperatorSet.BottomHat(inputRegion, se, out var bottomHatObj);
                    return new HRegion(bottomHatObj);
                default:
                    throw new NotSupportedException($"不支持的形态学操作: {operation}");
            }
        }

        /// <summary>
        /// 执行结果判断
        /// </summary>
        private void EvaluateResult(MorphologyResults result, Models.MorphologyResultConfig config)
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

            if (config.IsAreaCheckEnabled)
            {
                if (result.TotalArea < config.AreaMin || result.TotalArea > config.AreaMax)
                {
                    allPassed = false;
                    failReasons.Add($"总面积 {result.TotalArea} 不在范围 [{config.AreaMin}-{config.AreaMax}]");
                }
            }

            if (config.IsBlobCountCheckEnabled)
            {
                if (result.BlobCount < config.BlobCountMin || result.BlobCount > config.BlobCountMax)
                {
                    allPassed = false;
                    failReasons.Add($"Blob数量 {result.BlobCount} 不在范围 [{config.BlobCountMin}-{config.BlobCountMax}]");
                }
            }

            if (config.IsMaxBlobAreaCheckEnabled)
            {
                if (result.MaxBlobArea < config.MaxBlobAreaMin || result.MaxBlobArea > config.MaxBlobAreaMax)
                {
                    allPassed = false;
                    failReasons.Add($"最大Blob面积 {result.MaxBlobArea:F1} 不在范围 [{config.MaxBlobAreaMin}-{config.MaxBlobAreaMax}]");
                }
            }

            result.IsPass = allPassed;
            result.FailReasons = failReasons;
        }

        #endregion
    }
}
