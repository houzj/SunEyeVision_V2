using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageThreshold.Models;
using SunEyeVision.Tool.ImageThreshold.Views;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 图像二值化工具 - 支持5种二值化方法
    /// </summary>
    [Tool(
        id: "image-threshold",
        displayName: "图像二值化",
        Description = "对输入图像进行二值化处理，支持5种方法",
        Icon = "\U0001f52e",
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
                ToolId = _attribute?.Id ?? "image-threshold",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 验证参数
                var validationResult = ValidateParameters(parameters);
                if (!validationResult.IsValid)
                {
                    parameters.LogError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                // 验证输入图像
                if (image == null)
                {
                    parameters.LogWarning("未选择输入图像源，请先在基本参数中选择上游图像");
                    result.SetError("未选择输入图像源，请先在基本参数中选择上游图像");
                    return result;
                }
                parameters.LogInfo($"输入图像: Empty={image.Empty()}, Size={image.Width}x{image.Height}, Channels={image.Channels()}");
                if (image.Empty())
                {
                    parameters.LogWarning("上游图像内容为空，请检查上游节点是否已执行");
                    result.SetError("上游图像内容为空，请检查上游节点是否已执行");
                    return result;
                }

                parameters.LogInfo($"开始二值化处理: 图像尺寸 {image.Width}x{image.Height}, 方法: {parameters.Method.Value}");

                // 记录输入尺寸
                result.InputSize = image.Size();

                // 1. 彩色图转灰度
                Mat grayImage = image;
                bool grayImageOwned = false;
                if (image.Channels() > 1)
                {
                    grayImage = new Mat();
                    Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);
                    grayImageOwned = true;
                    parameters.LogInfo($"已将图像转换为灰度，原图通道数: {image.Channels()}");
                }

                // 2. 构建并应用区域掩码
                Mat workingImage = grayImage;
                CvRegion? roiRegion = null;
                if (parameters.IsRegionEnabled.Value)
                {
                    var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                    var maskRegions = parameters.GetRegionsByType(RegionType.Mask);

                    bool hasInspection = inspectionRegions.Any();
                    bool hasMask = maskRegions.Any();

                    if (hasInspection && hasMask)
                    {
                        using var inspRegion = CvRegion.CreateFromRegions(grayImage.Size(), inspectionRegions);
                        using var maskRegion = CvRegion.CreateFromRegions(grayImage.Size(), maskRegions);
                        CvOp.Difference(inspRegion, maskRegion, out roiRegion);
                        parameters.LogInfo($"差集ROI区域创建成功，白色像素数: {roiRegion.PixelCount}");
                    }
                    else if (hasInspection)
                    {
                        roiRegion = CvRegion.CreateFromRegions(grayImage.Size(), inspectionRegions);
                        parameters.LogInfo($"检测区域创建成功，白色像素数: {roiRegion.PixelCount}");
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域，将使用全图处理");
                    }

                    if (roiRegion != null && !roiRegion.IsEmpty && roiRegion.Mask != null)
                    {
                        var masked = new Mat();
                        grayImage.CopyTo(masked, roiRegion.Mask);
                        workingImage = masked;
                        parameters.LogInfo("已应用区域掩码，区域外像素置黑");
                    }
                }

                try
                {
                    // 3. 根据 Method 分支处理
                    Mat outputImage;
                    double thresholdUsed;

                    switch (parameters.Method.Value)
                    {
                        case ThresholdMethod.Global:
                            ExecuteGlobal(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;

                        case ThresholdMethod.OTSU:
                            ExecuteOTSU(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;

                        case ThresholdMethod.SmoothHisto:
                            ExecuteSmoothHisto(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;

                        case ThresholdMethod.Dynamic:
                            ExecuteDynamic(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;

                        case ThresholdMethod.LocalStats:
                            ExecuteLocalStats(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;

                        default:
                            ExecuteGlobal(workingImage, parameters, out outputImage, out thresholdUsed);
                            break;
                    }

                    // 4. 应用位置修正（预留）
                    if (parameters.IsPositionCorrectionEnabled.Value)
                    {
                        parameters.LogInfo("位置修正功能预留，当前未应用偏移");
                    }

                    // 5. 计算统计和轮廓提取
                    long whitePixelCount = 0;
                    double whitePixelRatio = 0;
                    int regionCount = 0;
                    double meanValue = 0;

                    if (outputImage != null && !outputImage.Empty())
                    {
                        whitePixelCount = Cv2.CountNonZero(outputImage);
                        var totalPixels = (long)outputImage.Width * outputImage.Height;
                        whitePixelRatio = totalPixels > 0 ? (double)whitePixelCount / totalPixels * 100.0 : 0;

                        var meanScalar = Cv2.Mean(outputImage);
                        meanValue = meanScalar.Val0;

                        if (whitePixelCount > 0)
                        {
                            var cvImage = new CvImage(outputImage.Clone(), ownsImage: true);
                            try
                            {
                                CvOp.Threshold(cvImage, null, 128, 255, out var binaryRegion, out _, CvThresholdType.Binary);
                                if (!binaryRegion.IsEmpty)
                                {
                                    CvOp.Connection(binaryRegion, out var connected);
                                    regionCount = connected.Count;
                                    CvOp.GetContour(connected, out var contourArray);
                                    connected.Dispose();
                                }
                                binaryRegion.Dispose();
                            }
                            finally
                            {
                                cvImage.Dispose();
                            }
                        }
                    }

                    // 6. 结果判断
                    bool isPass = EvaluateResult(outputImage, whitePixelCount, whitePixelRatio, parameters.ResultConfig);
                    if (isPass)
                    {
                        parameters.LogSuccess("结果判断通过");
                    }
                    else
                    {
                        parameters.LogWarning("结果判断未通过");
                    }

                    // 设置结果
                    result.OutputImage = outputImage;
                    result.MethodUsed = parameters.Method.Value;
                    result.ThresholdUsed = thresholdUsed;
                    result.MaxValueUsed = parameters.MaxValue.Value;
                    result.WhitePixelCount = whitePixelCount;
                    result.WhitePixelRatio = whitePixelRatio;
                    result.RegionCount = regionCount;
                    result.MeanValue = meanValue;
                    result.InvertUsed = parameters.Invert.Value;

                    stopwatch.Stop();
                    result.SetSuccess(stopwatch.ElapsedMilliseconds);

                    parameters.LogInfo($"二值化处理完成: 方法 {parameters.Method.Value}, 阈值 {thresholdUsed:F1}, " +
                                      $"白色像素 {whitePixelCount}/{whitePixelRatio:F1}%, 区域 {regionCount}个, " +
                                      $"耗时 {stopwatch.ElapsedMilliseconds}ms");
                }
                finally
                {
                    roiRegion?.Dispose();
                    if (grayImageOwned && grayImage != null)
                    {
                        grayImage.Dispose();
                    }
                    if (workingImage != grayImage && workingImage != null)
                    {
                        workingImage.Dispose();
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

        #region 各方法实现

        private void ExecuteGlobal(Mat grayImage, ImageThresholdParameters parameters, out Mat outputImage, out double thresholdUsed)
        {
            outputImage = new Mat();
            var threshType = parameters.Invert.Value ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            thresholdUsed = Cv2.Threshold(grayImage, outputImage, parameters.Threshold.Value, parameters.MaxValue.Value, threshType);
            parameters.LogInfo($"全局阈值: {thresholdUsed:F0}");
        }

        private void ExecuteOTSU(Mat grayImage, ImageThresholdParameters parameters, out Mat outputImage, out double thresholdUsed)
        {
            outputImage = new Mat();
            var threshType = parameters.Invert.Value ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            thresholdUsed = Cv2.Threshold(grayImage, outputImage, 0, parameters.MaxValue.Value, threshType | ThresholdTypes.Otsu);
            parameters.LogInfo($"OTSU自动阈值: {thresholdUsed:F0}");
        }

        private void ExecuteSmoothHisto(Mat grayImage, ImageThresholdParameters parameters, out Mat outputImage, out double thresholdUsed)
        {
            using var smoothed = new Mat();
            int kSize = parameters.SmoothKernel.Value;
            Cv2.Blur(grayImage, smoothed, new OpenCvSharp.Size(kSize, kSize));

            outputImage = new Mat();
            var threshType = parameters.Invert.Value ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            thresholdUsed = Cv2.Threshold(smoothed, outputImage, parameters.Threshold.Value, parameters.MaxValue.Value, threshType);
            parameters.LogInfo($"平滑直方图阈值: {thresholdUsed:F0}, 核大小: {kSize}");
        }

        private void ExecuteDynamic(Mat grayImage, ImageThresholdParameters parameters, out Mat outputImage, out double thresholdUsed)
        {
            using var smoothed = new Mat();
            int kSize = parameters.SmoothKernel.Value;
            Cv2.Blur(grayImage, smoothed, new OpenCvSharp.Size(kSize, kSize));

            var cvImage = new CvImage(grayImage, ownsImage: false);
            var smoothCvImage = new CvImage(smoothed, ownsImage: false);
            try
            {
                CvOp.DynamicThreshold(
                    cvImage, smoothCvImage, null,
                    parameters.Offset.Value, parameters.LightDark.Value,
                    out var regionResult);

                outputImage = regionResult.Mask?.Clone() ?? Mat.Zeros(grayImage.Size(), MatType.CV_8UC1);
                regionResult.Dispose();
            }
            finally
            {
                cvImage.Dispose();
                smoothCvImage.Dispose();
            }

            thresholdUsed = parameters.Offset.Value;

            if (parameters.Invert.Value)
            {
                var inverted = new Mat();
                Cv2.BitwiseNot(outputImage, inverted);
                outputImage.Dispose();
                outputImage = inverted;
            }

            parameters.LogInfo($"动态阈值: 偏移 {parameters.Offset.Value}, 核大小 {kSize}");
        }

        private void ExecuteLocalStats(Mat grayImage, ImageThresholdParameters parameters, out Mat outputImage, out double thresholdUsed)
        {
            var cvImage = new CvImage(grayImage, ownsImage: false);
            try
            {
                CvOp.AdaptiveThresholdVariance(
                    cvImage, null,
                    parameters.MaskWidth.Value, parameters.MaskHeight.Value,
                    parameters.StdDevScale.Value, parameters.AbsThreshold.Value,
                    parameters.LightDark.Value,
                    out var regionResult);

                outputImage = regionResult.Mask?.Clone() ?? Mat.Zeros(grayImage.Size(), MatType.CV_8UC1);
                regionResult.Dispose();
            }
            finally
            {
                cvImage.Dispose();
            }

            thresholdUsed = parameters.StdDevScale.Value;

            if (parameters.Invert.Value)
            {
                var inverted = new Mat();
                Cv2.BitwiseNot(outputImage, inverted);
                outputImage.Dispose();
                outputImage = inverted;
            }

            parameters.LogInfo($"局部统计阈值: 掩码 {parameters.MaskWidth.Value}x{parameters.MaskHeight.Value}, " +
                              $"缩放 {parameters.StdDevScale.Value:F2}, 偏移 {parameters.AbsThreshold.Value}");
        }

        #endregion

        #region 结果判断

        private bool EvaluateResult(Mat? outputImage, long whitePixelCount, double whitePixelRatio, Models.ImageThresholdResultConfig config)
        {
            if (config == null) return true;

            if (config.IsEmptyCheckEnabled)
            {
                if (outputImage == null || outputImage.Empty() || whitePixelCount == 0)
                    return false;
            }

            if (config.IsWhitePixelRatioCheckEnabled)
            {
                if (whitePixelRatio < config.WhitePixelRatioMin || whitePixelRatio > config.WhitePixelRatioMax)
                    return false;
            }

            return true;
        }

        #endregion

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
    }
}
