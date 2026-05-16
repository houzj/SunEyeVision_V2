using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具 - 连通域分析与特征提取
    /// </summary>
    /// <remarks>
    /// 算法流程（v9 - 完全 Halcon 原生实现）：
    ///   1. rgb1_to_gray       → 灰度转换
    ///   2. reduce_domain      → 缩小定义域（ROI掩码限制，不改变图像尺寸）
    ///   3. threshold           → 阈值分割
    ///   4. connection          → 连通域分析（8连通，4连通通过erosion模拟）
    ///   5. area_center         → 提取面积/质心
    ///   6. smallest_rectangle1 → 提取外接矩形
    ///   7. rectangularity      → 提取矩形度
    ///   8. select_shape        → Halcon原生特征筛选（面积+矩形度+圆度）
    ///   9. gen_contour_region_xld → 一次生成筛选后轮廓
    ///  10. contlength          → 轮廓周长
    ///  11. circularity_xld     → XLD轮廓圆度（更精确）
    ///  12. 结果判断            → OK/NG判定（9种条件）
    ///  13. 构建显示项          → 轮廓/矩形/质心/标签/统计文本
    /// </remarks>
    [Tool(
        id: "blob-analysis",
        displayName: "Blob分析",
        Description = "连通域分析与特征提取",
        Icon = "🔬",
        Category = "图像处理",
        Version = "3.0.0",
        HasDebugWindow = true
    )]
    public class BlobAnalysisTool : IToolPlugin<BlobAnalysisParameters, BlobAnalysisResults>
    {
        private readonly ToolAttribute? _attribute;

        public BlobAnalysisTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）- Halcon 原生实现
        /// </summary>
        public BlobAnalysisResults Run(BlobAnalysisParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            var result = new BlobAnalysisResults
            {
                ToolName = _attribute?.DisplayName ?? "BlobAnalysis",
                ToolId = _attribute?.Id ?? "blob-analysis",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();
            var stepTimings = new List<(string Step, long Ms)>();
            var stepSw = new Stopwatch();

            // Halcon 资源声明（finally 中统一清理）
            HObject grayImage = null!, reducedImage = null!, binaryRegion = null!;
            HObject connectedRegions = null!, shapeFiltered = null!, xldContours = null!;
            HObject? roiRegion = null;

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
                if (image == null || !image.IsInitialized())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                HOperatorSet.GetImageSize(image, out HTuple imgSizeW, out HTuple imgSizeH);
                int imgWidth = imgSizeW.I;
                int imgHeight = imgSizeH.I;
                parameters.LogInfo($"开始Blob分析处理: 图像尺寸 {imgWidth}x{imgHeight}");
                result.ImageWidth = imgWidth;
                result.ImageHeight = imgHeight;

                // ===== Step 1: 灰度转换（兼容单通道输入） =====
                stepSw.Restart();
                HOperatorSet.CountChannels(image, out HTuple channels);
                if (channels.I > 1)
                    HOperatorSet.Rgb1ToGray(image, out grayImage);
                else
                    HOperatorSet.CopyImage(image, out grayImage);
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                // ===== Step 2: 构建ROI区域 + 差集处理 =====
                stepSw.Restart();
                if (parameters.IsRegionEnabled.Value)
                {
                    var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                    var maskRegions = parameters.GetRegionsByType(RegionType.Mask);

                    bool hasInspection = inspectionRegions.Any();
                    bool hasMask = maskRegions.Any();

                    if (hasInspection && hasMask)
                    {
                        using var inspRegion = BuildHalconRegion(inspectionRegions, imgWidth, imgHeight);
                        using var maskRegion = BuildHalconRegion(maskRegions, imgWidth, imgHeight);
                        HOperatorSet.Difference(inspRegion, maskRegion, out HObject roiObj);
                        roiRegion = roiObj;

                        HOperatorSet.AreaCenter(roiRegion, out HTuple roiArea, out _, out _);
                        parameters.LogInfo($"差集ROI区域创建成功（检测区-遮罩），像素数: {roiArea.D}");
                    }
                    else if (hasInspection)
                    {
                        roiRegion = BuildHalconRegion(inspectionRegions, imgWidth, imgHeight);
                        HOperatorSet.AreaCenter(roiRegion, out HTuple roiArea, out _, out _);
                        parameters.LogInfo($"检测区域创建成功，像素数: {roiArea.D}");
                    }
                    else
                    {
                        parameters.LogWarning("启用区域限制，但没有有效的检测区域");
                    }
                }
                stepSw.Stop();
                stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

                // ===== Step 3: ReduceDomain（ROI掩码限制，不改变图像尺寸）=====
                stepSw.Restart();
                if (roiRegion != null)
                    HOperatorSet.ReduceDomain(grayImage, roiRegion, out reducedImage);
                else
                    HOperatorSet.CopyImage(grayImage, out reducedImage);
                stepSw.Stop();
                stepTimings.Add(("ReduceDomain", stepSw.ElapsedMilliseconds));

                // ===== Step 3.5: 图像预处理（在阈值分割前对灰度图滤波）=====
                stepSw.Restart();
                if (parameters.IsImagePreprocessingEnabled.Value)
                {
                    var preprocessed = ApplyImagePreprocessing(reducedImage, parameters);
                    reducedImage.Dispose();
                    reducedImage = preprocessed;
                }
                stepSw.Stop();
                stepTimings.Add(("图像预处理", stepSw.ElapsedMilliseconds));



                // ===== Step 4: 阈值分割 =====
                stepSw.Restart();
                double thresh = parameters.Threshold.Value;
                double maxVal = parameters.MaxValue.Value;
                bool invert = parameters.Invert.Value;
                ThresholdType type = parameters.Type.Value;
                double actualThreshold = thresh;

                bool selectDark = (type == ThresholdType.Binary && invert)
                               || (type == ThresholdType.BinaryInv && !invert);
                if (selectDark)
                    HOperatorSet.Threshold(reducedImage, out binaryRegion, 0, thresh);
                else
                    HOperatorSet.Threshold(reducedImage, out binaryRegion, thresh, maxVal);
                reducedImage.Dispose();
                reducedImage = null!;
                stepSw.Stop();
                stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));



                // ===== Step 4.5: 形态学预处理（在连通域分析前对二值区域操作）=====
                stepSw.Restart();
                if (parameters.IsMorphologyEnabled.Value)
                {
                    var morphRegion = ApplyMorphologyPreprocessing(binaryRegion, parameters);
                    binaryRegion.Dispose();
                    binaryRegion = morphRegion;
                }
                stepSw.Stop();
                stepTimings.Add(("形态学预处理", stepSw.ElapsedMilliseconds));

                // ===== Step 5: 连通域分析（Halcon 默认 8-连通）=====
                stepSw.Restart();
                HOperatorSet.Connection(binaryRegion, out connectedRegions);
                int totalConnectedCount = connectedRegions.CountObj();
                stepSw.Stop();
                stepTimings.Add(("连通域分析", stepSw.ElapsedMilliseconds));

                // ===== Step 7: select_shape 原生筛选（面积+矩形度+圆度）=====
                stepSw.Restart();
                double minArea = parameters.MinArea.Value;
                double maxArea = parameters.MaxArea.Value;
                double minCirc = parameters.MinCircularity.Value;
                double maxCirc = parameters.MaxCircularity.Value;
                double minRect = parameters.MinRectangularity.Value;
                double maxRect = parameters.MaxRectangularity.Value;

                HOperatorSet.SelectShape(connectedRegions, out shapeFiltered,
                    new HTuple(new string[] { "area", "rectangularity", "circularity" }), "and",
                    new HTuple(new double[] { minArea, minRect, minCirc }),
                    new HTuple(new double[] { maxArea, maxRect, maxCirc }));
                connectedRegions.Dispose();
                connectedRegions = null!;
                stepSw.Stop();
                stepTimings.Add(("SelectShape筛选", stepSw.ElapsedMilliseconds));

                int filteredCount = shapeFiltered.CountObj();

                // ===== Step 8: 提取筛选后特征 =====
                stepSw.Restart();
                HTuple areas, rows, cols, r1, c1, r2, c2, rectangularities;
                areas = rows = cols = r1 = c1 = r2 = c2 = rectangularities = new HTuple();
                if (filteredCount > 0)
                {
                    HOperatorSet.AreaCenter(shapeFiltered, out areas, out rows, out cols);
                    HOperatorSet.SmallestRectangle1(shapeFiltered, out r1, out c1, out r2, out c2);
                    HOperatorSet.Rectangularity(shapeFiltered, out rectangularities);
                }
                stepSw.Stop();
                stepTimings.Add(("筛选后特征提取", stepSw.ElapsedMilliseconds));

                // ===== Step 9: 轮廓 + 圆度 + 周长（仅筛选后）=====
                stepSw.Restart();
                HTuple circularities = new();
                HTuple perimeters = new();
                if (filteredCount > 0)
                {
                    HOperatorSet.GenContourRegionXld(shapeFiltered, out xldContours, "border");
                    HOperatorSet.CircularityXld(xldContours, out circularities);
                    HOperatorSet.Contlength(shapeFiltered, out perimeters);
                }
                stepSw.Stop();
                stepTimings.Add(("轮廓+圆度+周长", stepSw.ElapsedMilliseconds));

                // ===== Step 10: 组装筛选后的 BlobData =====
                stepSw.Restart();
                var filteredBlobs = new List<BlobData>(filteredCount);
                for (int i = 0; i < filteredCount; i++)
                {
                    var blob = new BlobData
                    {
                        Label = i + 1,
                        Area = areas[i].D,
                        CentroidX = cols[i].D,
                        CentroidY = rows[i].D,
                        BoundingBox = new Rect2d(c1[i].D, r1[i].D, c2[i].D - c1[i].D + 1, r2[i].D - r1[i].D + 1),
                        Rectangularity = i < rectangularities.Length ? rectangularities[i].D : 0,
                        Perimeter = i < perimeters.Length ? perimeters[i].D : 0,
                        Circularity = i < circularities.Length ? circularities[i].D : 0
                    };

                    using var singleContour = xldContours?.SelectObj(i + 1);
                    blob.ContourPoints = singleContour != null ? ExtractContourPoints(singleContour) : null;

                    filteredBlobs.Add(blob);
                }
                xldContours?.Dispose();
                xldContours = null!;
                shapeFiltered.Dispose();
                shapeFiltered = null!;
                stepSw.Stop();
                stepTimings.Add(("组装BlobData", stepSw.ElapsedMilliseconds));

                // ===== Step 11: 设置结果数据 =====
                stepSw.Restart();
                HOperatorSet.CopyImage(grayImage, out HObject grayClone);
                result.GrayImage = new HImage(grayClone);
                result.FilteredBlobs = filteredBlobs;
                result.ThresholdUsed = actualThreshold;
                result.MaxValueUsed = maxVal;
                result.TypeUsed = type;
                result.InvertUsed = invert;

                // 二值掩码
                HOperatorSet.RegionToBin(binaryRegion, out HObject binMaskObj, 255, 0, imgWidth, imgHeight);
                using (binMaskObj)
                {
                    HOperatorSet.CopyObj(binMaskObj, out HObject binMaskClone, 1, 1);
                    result.BinaryMask = new HImage(binMaskClone);
                }

                // 叠加显示图像
                HObject overlayObj = BuildOverlayImage(grayImage, binaryRegion);
                using (overlayObj)
                {
                    HOperatorSet.CopyObj(overlayObj, out HObject overlayClone, 1, 1);
                    result.OverlayImage = new HImage(overlayClone);
                }
                stepSw.Stop();
                stepTimings.Add(("结果赋值", stepSw.ElapsedMilliseconds));

                result.ProcessedAt = DateTime.Now;

                // ===== Step 12: 结果判断 =====
                stepSw.Restart();
                EvaluateJudgments(parameters.ResultConfig, result, binaryRegion, filteredBlobs);
                stepSw.Stop();
                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                // ===== 统一序号 =====
                for (int fi = 0; fi < filteredBlobs.Count; fi++)
                    filteredBlobs[fi].Label = fi + 1;

                // ===== Step 13: 构建显示项 =====
                stepSw.Restart();
                var display = result.Display;
                display.Clear();

                // 轮廓
                var contourBlobs = filteredBlobs
                    .Where(b => b.ContourPoints != null && b.ContourPoints.Length > 0)
                    .ToList();
                if (contourBlobs.Count > 0)
                {
                    var contourPointList = contourBlobs.Select(b => b.ContourPoints!).ToList();
                    var isPassList = contourBlobs.Select(b => b.JudgmentPassed).ToList();
                    display.AddContours(parameters.ContoursStyle, contourPointList, isPassList);
                }

                var areaStyle = parameters.AreaDisplayStyle;
                var blobsStyle = parameters.BlobsStyle;
                var centroidsStyle = parameters.CentroidsStyle;

                foreach (var blob in filteredBlobs)
                {
                    if (blob.BoundingBox.Width > 0 && blob.BoundingBox.Height > 0)
                        display.AddRect(blobsStyle, blob.BoundingBox, blob.JudgmentPassed);

                    if (blob.CentroidX > 0 || blob.CentroidY > 0)
                    {
                        display.AddCross(centroidsStyle, new Point2d(blob.CentroidX, blob.CentroidY), blob.JudgmentPassed);
                        display.AddText(areaStyle, areaStyle.PositionX, areaStyle.PositionY,
                            $"#{blob.Label}: {blob.Area:F0}px", blob.JudgmentPassed);
                    }
                }

                stepSw.Stop();
                stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                // ===== 性能报告 =====
                var totalMs = stopwatch.ElapsedMilliseconds;
                var maxStep = stepTimings.MaxBy(t => t.Ms);
                var reportLines = new List<string>
                {
                    $"===== Blob分析性能报告 =====",
                    $"图像: {imgWidth}x{imgHeight}  ReduceDomain  总耗时: {totalMs}ms",
                    $"步骤  名称              耗时(ms)    占比",
                    $"────  ──────────────── ────────  ──────"
                };
                for (int si = 0; si < stepTimings.Count; si++)
                {
                    var (step, ms) = stepTimings[si];
                    var pct = totalMs > 0 ? (double)ms / totalMs * 100 : 0;
                    var marker = step == maxStep.Step ? " ◄瓶颈" : "";
                    reportLines.Add($"  {si + 1,2}.  {step,-18} {ms,8}  {pct,5:F1}%{marker}");
                }
                reportLines.Add($"──────────────────────────────────");
                reportLines.Add($"结果: 连通域 {totalConnectedCount}, 筛选 {filteredCount}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                parameters.LogInfo(string.Join("\n", reportLines));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"Blob分析处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }
            finally
            {
                grayImage?.Dispose();
                reducedImage?.Dispose();
                binaryRegion?.Dispose();
                connectedRegions?.Dispose();
                shapeFiltered?.Dispose();
                xldContours?.Dispose();
                roiRegion?.Dispose();
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(BlobAnalysisParameters parameters)
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
            return new BlobAnalysisToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new BlobDataAnalysisControl();
        }

        /// <summary>
        /// 评估结果判断（OK/NG判定）
        /// </summary>
        /// <remarks>
        /// 两阶段判断：
        ///   Phase 1 - Per-Blob判断（控制单个blob显示颜色，参与整体判断）：
        ///     a. 每个Blob面积 (perBlobArea)
        ///     b. 每个Blob圆度 (perBlobCircularity)
        ///     c. 每个Blob矩形度 (perBlobRectangularity)
        ///
        ///   Phase 2 - 整体判断（仅控制整体JudgmentPassed，不控制blob颜色）：
        ///     1. 输出为空 (isEmpty)
        ///     2. 白色像素比例 (whitePixelRatio)
        ///     3. Blob数量 (blobCount)
        ///     4. 总面积 (totalArea)
        ///     5. 平均面积 (averageArea)
        ///     6. 最大面积 (maxArea)
        ///     7. 最小面积 (minArea)
        ///     8. 平均圆度 (averageCircularity)
        ///     9. 平均矩形度 (averageRectangularity)
        ///
        /// 颜色控制规则：
        ///   - Per-blob元素（轮廓/矩形/十字/标签/面积文本）→ blob.JudgmentPassed
        ///   - 聚合元素（统计文本/更多提示）→ result.JudgmentPassed
        ///   - 整体判断条件 NG 不影响已通过 per-blob 判断的 blob 颜色
        /// </remarks>
        private static void EvaluateJudgments(
            BlobAnalysisResultConfig config,
            BlobAnalysisResults result,
            HObject binaryRegion,
            List<BlobData> filteredBlobs)
        {
            if (config == null) return;

            var messages = result.JudgmentMessages;
            messages.Clear();

            // 统计值
            int filteredCount = filteredBlobs.Count;
            double filteredTotalArea = filteredBlobs.Sum(b => b.Area);
            double averageArea = filteredCount > 0 ? filteredBlobs.Average(b => b.Area) : 0;
            double maxArea = filteredCount > 0 ? filteredBlobs.Max(b => b.Area) : 0;
            double minArea = filteredCount > 0 ? filteredBlobs.Min(b => b.Area) : 0;
            double averageCircularity = filteredCount > 0 ? filteredBlobs.Average(b => b.Circularity) : 0;
            double averageRectangularity = filteredCount > 0 ? filteredBlobs.Average(b => b.Rectangularity) : 0;
            double totalPixels = result.ImageWidth * result.ImageHeight;
            HOperatorSet.AreaCenter(binaryRegion, out HTuple binArea, out _, out _);
            double whitePixelRatio = totalPixels > 0 ? binArea.D / totalPixels * 100 : 0;
            result.WhitePixelRatio = whitePixelRatio;

            // ===== Phase 1: Per-Blob判断（控制blob颜色）=====
            if (config.HasPerBlobJudgmentEnabled)
            {
                messages.Add("--- Per-Blob判断 ---");
            }

            foreach (var blob in filteredBlobs)
            {
                bool blobPassed = true;

                if (config.IsPerBlobAreaCheckEnabled)
                {
                    bool pass = blob.Area >= config.PerBlobAreaMin && blob.Area <= config.PerBlobAreaMax;
                    if (!pass) blobPassed = false;
                }

                if (config.IsPerBlobCircularityCheckEnabled)
                {
                    bool pass = blob.Circularity >= config.PerBlobCircularityMin && blob.Circularity <= config.PerBlobCircularityMax;
                    if (!pass) blobPassed = false;
                }

                if (config.IsPerBlobRectangularityCheckEnabled)
                {
                    bool pass = blob.Rectangularity >= config.PerBlobRectangularityMin && blob.Rectangularity <= config.PerBlobRectangularityMax;
                    if (!pass) blobPassed = false;
                }

                blob.JudgmentPassed = blobPassed;
            }

            if (config.HasPerBlobJudgmentEnabled)
            {
                int perBlobPassedCount = filteredBlobs.Count(b => b.JudgmentPassed);
                int perBlobFailedCount = filteredBlobs.Count - perBlobPassedCount;
                messages.Add(perBlobFailedCount == 0
                    ? $"[OK] Per-Blob判断: {filteredCount}个Blob全部通过"
                    : $"[NG] Per-Blob判断: {perBlobPassedCount}/{filteredCount}通过, {perBlobFailedCount}未通过");
            }

            // ===== Phase 2: 整体判断（不控制blob颜色）=====
            bool wholePassed = true;

            if (config.IsEmptyCheckEnabled)
            {
                bool isEmpty = filteredCount == 0;
                if (isEmpty)
                {
                    wholePassed = false;
                    messages.Add("[NG] 输出为空：未检测到符合条件的Blob");
                }
                else
                {
                    messages.Add($"[OK] 输出不为空：检测到 {filteredCount} 个Blob");
                }
            }

            if (config.IsWhitePixelRatioCheckEnabled)
            {
                bool pass = whitePixelRatio >= config.WhitePixelRatioMin && whitePixelRatio <= config.WhitePixelRatioMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 白色像素比例：{whitePixelRatio:F1}% (范围 {config.WhitePixelRatioMin:F1}~{config.WhitePixelRatioMax:F1}%)");
                if (!pass) wholePassed = false;
            }

            if (config.IsBlobCountCheckEnabled)
            {
                bool pass = filteredCount >= config.BlobCountMin && filteredCount <= config.BlobCountMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] Blob数量：{filteredCount} (范围 {config.BlobCountMin}~{config.BlobCountMax})");
                if (!pass) wholePassed = false;
            }

            if (config.IsTotalAreaCheckEnabled)
            {
                bool pass = filteredTotalArea >= config.TotalAreaMin && filteredTotalArea <= config.TotalAreaMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 总面积：{filteredTotalArea:F0}px² (范围 {config.TotalAreaMin:F0}~{config.TotalAreaMax:F0}px²)");
                if (!pass) wholePassed = false;
            }

            if (config.IsAverageAreaCheckEnabled)
            {
                bool pass = averageArea >= config.AverageAreaMin && averageArea <= config.AverageAreaMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 平均面积：{averageArea:F0}px² (范围 {config.AverageAreaMin:F0}~{config.AverageAreaMax:F0}px²)");
                if (!pass) wholePassed = false;
            }

            if (config.IsMaxAreaCheckEnabled)
            {
                bool pass = maxArea >= config.MaxAreaMin && maxArea <= config.MaxAreaMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 最大面积：{maxArea:F0}px² (范围 {config.MaxAreaMin:F0}~{config.MaxAreaMax:F0}px²)");
                if (!pass) wholePassed = false;
            }

            if (config.IsMinAreaCheckEnabled)
            {
                bool pass = minArea >= config.MinAreaMin && minArea <= config.MinAreaMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 最小面积：{minArea:F0}px² (范围 {config.MinAreaMin:F0}~{config.MinAreaMax:F0}px²)");
                if (!pass) wholePassed = false;
            }

            if (config.IsAverageCircularityCheckEnabled)
            {
                bool pass = averageCircularity >= config.AverageCircularityMin && averageCircularity <= config.AverageCircularityMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 平均圆度：{averageCircularity:F3} (范围 {config.AverageCircularityMin:F3}~{config.AverageCircularityMax:F3})");
                if (!pass) wholePassed = false;
            }

            if (config.IsAverageRectangularityCheckEnabled)
            {
                bool pass = averageRectangularity >= config.AverageRectangularityMin && averageRectangularity <= config.AverageRectangularityMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 平均矩形度：{averageRectangularity:F3} (范围 {config.AverageRectangularityMin:F3}~{config.AverageRectangularityMax:F3})");
                if (!pass) wholePassed = false;
            }

            // Phase 2 per-blob判断也计入整体结果
            bool perBlobAllPassed = !config.HasPerBlobJudgmentEnabled || filteredBlobs.All(b => b.JudgmentPassed);
            bool allPassed = perBlobAllPassed && wholePassed;

            result.JudgmentPassed = allPassed;

            if (messages.Count > 0)
            {
                string summary = allPassed ? "全部通过 ✓" : "存在未通过项 ✗";
                messages.Insert(0, $"===== 结果判断: {summary} =====");
            }
        }

        #region Halcon 辅助方法

        /// <summary>
        /// 将 UI 区域数据（RegionData）转为 Halcon HRegion
        /// </summary>
        private static HObject BuildHalconRegion(IEnumerable<RegionData> regions, int imageWidth, int imageHeight)
        {
            HObject? resultRegion = null;

            foreach (var regionData in regions)
            {
                if (regionData?.Parameters is not ShapeParameters shape)
                    continue;

                HObject? current = null;
                try
                {
                    switch (shape.ShapeType)
                    {
                        case ShapeType.Rectangle:
                            // CenterX/Y, Width/Height → row1, col1, row2, col2
                            double r1 = shape.CenterY - shape.Height / 2.0;
                            double c1 = shape.CenterX - shape.Width / 2.0;
                            double r2 = shape.CenterY + shape.Height / 2.0;
                            double c2 = shape.CenterX + shape.Width / 2.0;
                            HOperatorSet.GenRectangle1(out HObject rectObj, r1, c1, r2, c2);
                            current = rectObj;
                            break;

                        case ShapeType.Circle:
                            HOperatorSet.GenCircle(out HObject circObj, shape.CenterY, shape.CenterX, shape.Radius);
                            current = circObj;
                            break;

                        case ShapeType.Polygon:
                            var rows = new double[shape.Points.Count];
                            var cols = new double[shape.Points.Count];
                            for (int i = 0; i < shape.Points.Count; i++)
                            {
                                rows[i] = shape.Points[i].Y;
                                cols[i] = shape.Points[i].X;
                            }
                            HOperatorSet.GenRegionPolygon(out HObject polyObj, rows, cols);
                            current = polyObj;
                            break;

                        default:
                            // 不支持的类型：忽略
                            continue;
                    }

                    if (current != null)
                    {
                        if (resultRegion == null)
                            resultRegion = current;
                        else
                        {
                            HOperatorSet.Union2(resultRegion, current, out HObject mergedObj);
                            resultRegion.Dispose();
                            resultRegion = mergedObj;
                            current.Dispose();
                        }
                        current = null;
                    }
                }
                finally
                {
                    current?.Dispose();
                }
            }

            if (resultRegion != null)
                return resultRegion;

            HOperatorSet.GenEmptyRegion(out HObject empty);
            return empty;
        }

        /// <summary>
        /// 从 Halcon HXLDCont 提取轮廓点集
        /// </summary>
        private static Point2d[]? ExtractContourPoints(HObject contour)
        {
            if (contour == null || !contour.IsInitialized())
                return null;

            try
            {
                HOperatorSet.GetContourXld(contour, out HTuple rows, out HTuple cols);
                int count = Math.Min(rows.Length, cols.Length);
                if (count == 0) return null;

                var points = new Point2d[count];
                for (int i = 0; i < count; i++)
                    points[i] = new Point2d(cols[i].D, rows[i].D);
                return points;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 构建叠加显示图像（灰度 + 二值区域轮廓叠加）
        /// </summary>
        private static HObject BuildOverlayImage(HObject grayImage, HObject binaryRegion)
        {
            HOperatorSet.CopyImage(grayImage, out HObject overlay);
            try
            {
                HOperatorSet.PaintRegion(binaryRegion, overlay, out HObject paintObj,
                    "fill", 200);
                overlay.Dispose();
                return paintObj;
            }
            catch
            {
                return overlay;
            }
        }

        /// <summary>
        /// 应用图像预处理滤波
        /// </summary>
        private static HObject ApplyImagePreprocessing(HObject image, BlobAnalysisParameters p)
        {
            int kernel = p.PreprocessKernelSize.Value;
            switch (p.PreprocessFilterType.Value)
            {
                case PreprocessFilterType.Gaussian:
                    HOperatorSet.GaussFilter(image, out HObject gaussed, kernel);
                    return gaussed;
                case PreprocessFilterType.Median:
                    HOperatorSet.MedianImage(image, out HObject medianed, "circle", kernel, "mirrored");
                    return medianed;
                case PreprocessFilterType.Smooth:
                    HOperatorSet.MeanImage(image, out HObject smoothed, kernel, kernel);
                    return smoothed;
                case PreprocessFilterType.Emphasize:
                    double factor = p.PreprocessEmphasizeFactor.Value;
                    HOperatorSet.Emphasize(image, out HObject emphasized, kernel, kernel, factor);
                    return emphasized;
                default:
                    HOperatorSet.CopyImage(image, out HObject copy);
                    return copy;
            }
        }

        /// <summary>
        /// 应用形态学预处理（对二值区域操作）
        /// </summary>
        private static HObject ApplyMorphologyPreprocessing(HObject region, BlobAnalysisParameters p)
        {
            int kernelSize = p.MorphKernelSize.Value;
            int iterations = p.MorphIterations.Value;

            HOperatorSet.GenRectangle1(out HObject se, -kernelSize / 2, -kernelSize / 2,
                kernelSize / 2, kernelSize / 2);

            HObject result;
            switch (p.MorphOperation.Value)
            {
                case MorphOperation.Erode:
                    HOperatorSet.Erosion1(region, se, out result, iterations);
                    break;
                case MorphOperation.Dilate:
                    HOperatorSet.Dilation1(region, se, out result, iterations);
                    break;
                case MorphOperation.Open:
                    HOperatorSet.Opening(region, se, out result);
                    break;
                case MorphOperation.Close:
                    HOperatorSet.Closing(region, se, out result);
                    break;
                case MorphOperation.Gradient:
                    HOperatorSet.Erosion1(region, se, out HObject eroded, 1);
                    HOperatorSet.Dilation1(region, se, out HObject dilated, 1);
                    HOperatorSet.Difference(dilated, eroded, out result);
                    eroded.Dispose(); dilated.Dispose();
                    break;
                case MorphOperation.TopHat:
                    HOperatorSet.Opening(region, se, out HObject opened);
                    HOperatorSet.Difference(region, opened, out result);
                    opened.Dispose();
                    break;
                case MorphOperation.BlackHat:
                    HOperatorSet.Closing(region, se, out HObject closed);
                    HOperatorSet.Difference(closed, region, out result);
                    closed.Dispose();
                    break;
                default:
                    HOperatorSet.CopyObj(region, out result, 1, 1);
                    break;
            }
            se.Dispose();
            return result;
        }

        #endregion
    }
}
