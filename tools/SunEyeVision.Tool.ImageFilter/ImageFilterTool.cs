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
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageFilter.Models;
using SunEyeVision.Tool.ImageFilter.Views;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 图像滤波工具 - 对输入图像进行平滑/去噪滤波处理，支持5种方法
    /// </summary>
    /// <remarks>
    /// 算法流程：
    ///   1. 获取输入图像 Mat
    ///   2. 应用区域限制（ReduceDomain）
    ///   3. 根据 FilterType 分支调用 CvOp 对应滤波方法
    ///   4. 应用位置修正
    ///   5. 计算统计（均值、方差、与原图 PSNR）
    ///   6. 结果判断（OK/NG判定）
    ///   7. 构建显示项
    /// </remarks>
    [Tool(
        id: "image-filter",
        displayName: "图像滤波",
        Description = "对输入图像进行平滑/去噪滤波处理，支持5种方法",
        Icon = "🌀",
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

            // 需要 finally 块中释放的资源
            CvImage? cvImage = null;
            bool cvImageOwned = false;
            CvRegion? roiRegion = null;

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
                if (image == null)
                {
                    parameters.LogWarning("未选择输入图像源，请先在基本参数中选择上游图像");
                    result.SetError("未选择输入图像源，请先在基本参数中选择上游图像");
                    return result;
                }
                if (image.Empty())
                {
                    parameters.LogWarning("上游图像为空，请检查上游节点是否已执行");
                    result.SetError("上游图像为空，请检查上游节点是否已执行");
                    return result;
                }

                parameters.LogInfo($"开始图像滤波处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputSize = image.Size();

                // 保存原图副本用于 PSNR 计算
                using var originalImage = image.Clone();

                // ===== 构建 CvImage 包装 =====
                cvImage = new CvImage(image, ownsImage: false);
                cvImageOwned = true;

                // ===== 构建ROI区域 =====
                stepSw.Restart();
                if (parameters.IsRegionEnabled.Value)
                {
                    var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                    var maskRegions = parameters.GetRegionsByType(RegionType.Mask);

                    bool hasInspection = inspectionRegions.Any();
                    bool hasMask = maskRegions.Any();

                    if (hasInspection && hasMask)
                    {
                        using var inspRegion = CvRegion.CreateFromRegions(cvImage.Size, inspectionRegions);
                        using var maskRegion = CvRegion.CreateFromRegions(cvImage.Size, maskRegions);
                        CvOp.Difference(inspRegion, maskRegion, out roiRegion);
                        parameters.LogInfo($"差集ROI区域创建成功，白色像素数: {roiRegion.PixelCount}");
                    }
                    else if (hasInspection)
                    {
                        roiRegion = CvRegion.CreateFromRegions(cvImage.Size, inspectionRegions);
                        parameters.LogInfo($"检测区域创建成功，白色像素数: {roiRegion.PixelCount}");
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域，将使用全图处理");
                    }

                    // 掩码：只保留区域内像素，区域外设为黑色
                    if (roiRegion != null && !roiRegion.IsEmpty && roiRegion.Mask != null)
                    {
                        var masked = new Mat();
                        cvImage.Image.CopyTo(masked, roiRegion.Mask);
                        cvImage.Dispose();
                        cvImage = new CvImage(masked, ownsImage: true);
                    }
                }
                stepSw.Stop();
                stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

                // ===== 根据 FilterType 分支调用滤波方法 =====
                stepSw.Restart();
                var filterType = parameters.FilterType.Value;
                Mat filteredImage;

                switch (filterType)
                {
                    case FilterType.Gaussian:
                    {
                        int kSize = parameters.KernelSize.Value;
                        if (kSize % 2 == 0) kSize += 1;
                        CvOp.GaussianBlur(cvImage, kSize, parameters.Sigma.Value, out var resultImageG);
                        using (resultImageG)
                        {
                            filteredImage = resultImageG.Image.Clone();
                        }
                        result.KernelSizeUsed = kSize;
                        result.SigmaUsed = parameters.Sigma.Value;
                        result.ProcessingMode = $"高斯滤波 (kernel={kSize}, sigma={parameters.Sigma.Value})";
                        parameters.LogInfo($"高斯滤波完成: kernel={kSize}, sigma={parameters.Sigma.Value}");
                        break;
                    }
                    case FilterType.Median:
                    {
                        int kSize = parameters.KernelSize.Value;
                        if (kSize % 2 == 0) kSize += 1;
                        CvOp.MedianBlur(cvImage, kSize, out var resultImageM);
                        using (resultImageM)
                        {
                            filteredImage = resultImageM.Image.Clone();
                        }
                        result.KernelSizeUsed = kSize;
                        result.ProcessingMode = $"中值滤波 (kernel={kSize})";
                        parameters.LogInfo($"中值滤波完成: kernel={kSize}");
                        break;
                    }
                    case FilterType.Mean:
                    {
                        int kSize = parameters.KernelSize.Value;
                        CvOp.BoxBlur(cvImage, kSize, out var resultImageBox);
                        using (resultImageBox)
                        {
                            filteredImage = resultImageBox.Image.Clone();
                        }
                        result.KernelSizeUsed = kSize;
                        result.ProcessingMode = $"均值滤波 (kernel={kSize})";
                        parameters.LogInfo($"均值滤波完成: kernel={kSize}");
                        break;
                    }
                    case FilterType.Bilateral:
                    {
                        CvOp.BilateralFilter(cvImage, parameters.SigmaSpatial.Value, parameters.SigmaRange.Value, out var resultImageB);
                        using (resultImageB)
                        {
                            filteredImage = resultImageB.Image.Clone();
                        }
                        result.KernelSizeUsed = 0;
                        result.ProcessingMode = $"双边滤波 (sigmaSpatial={parameters.SigmaSpatial.Value}, sigmaRange={parameters.SigmaRange.Value})";
                        parameters.LogInfo($"双边滤波完成: sigmaSpatial={parameters.SigmaSpatial.Value}, sigmaRange={parameters.SigmaRange.Value}");
                        break;
                    }
                    case FilterType.Guided:
                    {
                        CvOp.GuidedFilter(cvImage, parameters.GuidedRadius.Value, parameters.GuidedAmplitude.Value, out var resultImageGd);
                        using (resultImageGd)
                        {
                            filteredImage = resultImageGd.Image.Clone();
                        }
                        result.KernelSizeUsed = parameters.GuidedRadius.Value;
                        result.ProcessingMode = $"导向滤波 (radius={parameters.GuidedRadius.Value}, amp={parameters.GuidedAmplitude.Value})";
                        parameters.LogInfo($"导向滤波完成: radius={parameters.GuidedRadius.Value}, amp={parameters.GuidedAmplitude.Value}");
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(filterType), $"不支持的滤波类型: {filterType}");
                }
                stepSw.Stop();
                stepTimings.Add(("滤波处理", stepSw.ElapsedMilliseconds));

                // ===== 设置结果 =====
                result.OutputImage = filteredImage.Clone();
                result.FilterTypeUsed = filterType;

                // ===== 计算统计（均值、方差） =====
                stepSw.Restart();
                using var meanMat = new Mat();
                using var stdDevMat = new Mat();
                Cv2.MeanStdDev(filteredImage, meanMat, stdDevMat);
                result.MeanValue = meanMat.Get<double>(0);
                result.StdDevValue = stdDevMat.Get<double>(0);
                stepSw.Stop();
                stepTimings.Add(("统计计算", stepSw.ElapsedMilliseconds));

                // ===== 计算与原图的 PSNR =====
                stepSw.Restart();
                result.PSNR = Cv2.PSNR(originalImage, filteredImage);
                stepSw.Stop();
                stepTimings.Add(("PSNR计算", stepSw.ElapsedMilliseconds));

                // ===== 结果判断 =====
                stepSw.Restart();
                EvaluateJudgments(parameters.ResultConfig, result);
                stepSw.Stop();
                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                // ===== 构建显示项 =====
                stepSw.Restart();
                var display = result.Display;
                display.Clear();

                // 叠加信息文本
                var wholePassed = result.JudgmentPassed;
                display.AddText(parameters.StatisticsStyle,
                    parameters.StatisticsStyle.PositionX, parameters.StatisticsStyle.PositionY,
                    new[]
                    {
                        $"滤波: {result.ProcessingMode}  {result.InputSize.Width}x{result.InputSize.Height}",
                        $"均值: {result.MeanValue:F2}  标准差: {result.StdDevValue:F2}  PSNR: {result.PSNR:F2}dB"
                    },
                    wholePassed);
                stepSw.Stop();
                stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                // 性能报告
                var totalMs = stopwatch.ElapsedMilliseconds;
                var maxStep = stepTimings.MaxBy(t => t.Ms);
                var reportLines = new List<string>
                {
                    $"===== 图像滤波性能报告 =====",
                    $"图像: {image.Width}x{image.Height}  滤波: {filterType}  总耗时: {totalMs}ms",
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
                reportLines.Add($"结果: {result.ProcessingMode}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                parameters.LogInfo(string.Join("\n", reportLines));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"图像滤波处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }
            finally
            {
                if (cvImage != null && cvImageOwned)
                {
                    cvImage.Dispose();
                }
                roiRegion?.Dispose();
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
        /// 评估结果判断（OK/NG判定）
        /// </summary>
        private static void EvaluateJudgments(
            ImageFilterResultConfig config,
            ImageFilterResults result)
        {
            if (config == null) return;

            var messages = result.JudgmentMessages;
            messages.Clear();

            bool wholePassed = true;

            // 输出为空判断
            if (config.IsEmptyCheckEnabled)
            {
                bool isEmpty = result.OutputImage == null || result.OutputImage.Empty();
                if (isEmpty)
                {
                    wholePassed = false;
                    messages.Add("[NG] 输出为空：滤波结果图像为空");
                }
                else
                {
                    messages.Add("[OK] 输出不为空");
                }
            }

            // 均值判断
            if (config.IsMeanValueCheckEnabled)
            {
                bool pass = result.MeanValue >= config.MeanValueMin && result.MeanValue <= config.MeanValueMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 均值：{result.MeanValue:F2} (范围 {config.MeanValueMin:F2}~{config.MeanValueMax:F2})");
                if (!pass) wholePassed = false;
            }

            // 标准差判断
            if (config.IsStdDevCheckEnabled)
            {
                bool pass = result.StdDevValue >= config.StdDevMin && result.StdDevValue <= config.StdDevMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 标准差：{result.StdDevValue:F2} (范围 {config.StdDevMin:F2}~{config.StdDevMax:F2})");
                if (!pass) wholePassed = false;
            }

            // PSNR 判断
            if (config.IsPSNRCheckEnabled)
            {
                bool pass = result.PSNR >= config.PSNRMin && result.PSNR <= config.PSNRMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] PSNR：{result.PSNR:F2}dB (范围 {config.PSNRMin:F2}~{config.PSNRMax:F2}dB)");
                if (!pass) wholePassed = false;
            }

            result.JudgmentPassed = wholePassed;

            if (messages.Count > 0)
            {
                string summary = wholePassed ? "全部通过 ✓" : "存在未通过项 ✗";
                messages.Insert(0, $"===== 结果判断: {summary} =====");
            }
        }
    }
}
