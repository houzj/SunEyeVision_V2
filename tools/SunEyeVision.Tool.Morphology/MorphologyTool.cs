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
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Tool.Morphology.Views;

namespace SunEyeVision.Tool.Morphology
{
    /// <summary>
    /// 形态学处理工具 - v1.0
    /// 算法流程：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → ROI掩码限制
    ///   3. threshold      → 固定阈值二值化 (128)
    ///   4. morphology_ex  → 形态学操作
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
            var image = parameters.GetCvImage();
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
                if (image.Empty)
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                parameters.LogInfo($"开始形态学处理: 图像尺寸 {image.Size.Width}x{image.Size.Height}");
                result.InputSize = image.Size;

                var operation = parameters.Operation.Value;
                var kernelShape = parameters.KernelShape.Value;
                var kernelWidth = parameters.KernelWidth.Value;
                var kernelHeight = parameters.KernelHeight.Value;
                var iterations = parameters.Iterations.Value;
                var borderType = parameters.BorderType.Value;
                var isRegionEnabled = parameters.IsRegionEnabled;

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
                        // ===== Step 3: ReduceDomain（ROI掩码限制）=====
                        stepSw.Restart();
                        CvOp.ReduceDomain(grayImage, roiRegion, out var reducedImage);
                        stepSw.Stop();
                        stepTimings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));

                        using (reducedImage)
                        {
                            // ===== Step 4: 固定阈值二值化 =====
                            stepSw.Restart();
                            var binaryMat = new Mat();
                            Cv2.Threshold(reducedImage.Image, binaryMat, 128, 255, ThresholdTypes.Binary);
                            stepSw.Stop();
                            stepTimings.Add(("二值化", stepSw.ElapsedMilliseconds));

                            using (binaryMat)
                            {
                                // ===== Step 5: 形态学操作（二值图背景为黑，边框用黑色填充防止白边）=====
                                stepSw.Restart();
                                CvOp.MorphologyEx(binaryMat, out var resultMat,
                                    (MorphTypes)operation, kernelShape,
                                    kernelWidth, kernelHeight,
                                    iterations, borderType,
                                    borderValue: Scalar.All(0));
                                stepSw.Stop();
                                stepTimings.Add(("形态学操作", stepSw.ElapsedMilliseconds));

                                using (resultMat)
                                {
                                    // ===== Step 6: 区域转换与统计 =====
                                    stepSw.Restart();
                                    using var morphRegion = CvRegion.FromMask(resultMat);

                                    // 输出图像：通过 ComposeDisplay 合成
                                    CvOp.ComposeDisplay(reducedImage, null, morphRegion, out var outputImage);

                                    // 保存二值掩码
                                    CvOp.RegionToMask(morphRegion, out var binaryMask);
                                    result.BinaryMask = binaryMask;

                                    // 图形结果输出
                                    result.OutputRegions = new IVisionRegion[] { morphRegion.CopyObj() };

                                    result.OutputImage = outputImage;

                                    // 参数回写
                                    result.OperationUsed = operation.ToString();
                                    result.KernelShapeUsed = kernelShape.ToString();
                                    result.KernelWidthUsed = kernelWidth;
                                    result.KernelHeightUsed = kernelHeight;
                                    result.InputChannels = image.Channels;
                                    result.IterationsUsed = iterations;

                                    // 统计：输出均值
                                    var outputMean = Cv2.Mean(resultMat);
                                    result.OutputMean = outputMean.Val0;

                                    // 连通域分析 → Blob 统计
                                    CvOp.Connection(morphRegion, out var connectedRegion);
                                    using (connectedRegion)
                                    {
                                        if (!connectedRegion.IsEmpty)
                                        {
                                            CvOp.AreaCenter(connectedRegion, out var areaCenters);
                                            result.BlobCount = areaCenters.Length;
                                            result.TotalArea = (long)areaCenters.Sum(ac => ac.Area);
                                            result.MaxBlobArea = areaCenters.Length > 0
                                                ? areaCenters.Max(ac => ac.Area) : 0;
                                        }
                                    }

                                    stepSw.Stop();
                                    stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                                    // ===== Step 7: 结果判断 =====
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
                                        $"===== 形态学性能报告 =====",
                                        $"图像: {image.Size.Width}x{image.Size.Height}  总耗时: {totalMs}ms",
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
        /// 执行结果判断
        /// </summary>
        private void EvaluateResult(MorphologyResults result, Models.MorphologyResultConfig config)
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
