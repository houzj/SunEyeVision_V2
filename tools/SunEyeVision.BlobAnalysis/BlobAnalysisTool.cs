using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;
using SunEyeVision.Tool.BlobAnalysis.Views;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具 - 连通域分析与特征提取（按需计算版 v11）
    /// </summary>
    /// <remarks>
    /// 算法流程（v11 - 按需计算，消除冗余）：
    ///   1. rgb1_to_gray   → 灰度转换
    ///   2. reduce_domain  → 缩小定义域
    ///   3. 图像预处理      → 均值/高斯/中值/直方图均衡化（可选）
    ///   4. threshold       → 阈值分割
    ///   5. 形态学处理      → 腐蚀/膨胀/开闭/顶帽/黑帽（可选）
    ///   6. connection      → 连通域分析
    ///   7. select_shape    → Halcon原生面积筛选（替代C#全量特征提取）
    ///   8. 特征提取+统一判断 → 仅对剩余blob计算特征，统一判断通过才保留
    ///   9. get_region_contour → 仅对通过blob提取轮廓（延迟计算）
    ///  10. 整体结果判断    → OK/NG判定
    ///  11. 构建显示项
    /// 
    /// 优化要点：
    /// - 消除全量特征提取：仅对面积筛选后的blob计算
    /// - 消除双抢筛选：IsFiltered + JudgmentPassed → 统一的JudgmentPassed
    /// - 延迟轮廓提取：仅对最终通过的blob提取contour
    /// </remarks>
    [Tool(
        id: "BlobAnalysis",
        displayName: "Blob分析",
        Description = "连通域分析与特征提取",
        Icon = "🔬",
        Category = "图像处理",
        Version = "3.1.0",
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
        /// 执行工具（同步）
        /// </summary>
        public BlobAnalysisResults Run(BlobAnalysisParameters parameters)
        {
            // 从 IVisionImage 拆包为 HImage
            var hImage = parameters.GetHalconImage();
            var result = new BlobAnalysisResults
            {
                ToolName = _attribute?.DisplayName ?? "BlobAnalysis",
                ToolId = _attribute?.Id ?? "BlobAnalysis",
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



                // ===== 图像尺寸获取（图像已在 GetHalconImage 中验证） =====
                HOperatorSet.GetImageSize(hImage, out var imgW, out var imgH);
                int imgWidth = (int)imgW.L;
                int imgHeight = (int)imgH.L;
                parameters.LogInfo($"开始Blob分析处理: 图像尺寸 {imgWidth}x{imgHeight}");

                // ===== Step 1: 灰度转换 =====
                stepSw.Restart();
                int channels = hImage.CountChannels();
                HImage grayImage;
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
                    // ===== Step 2+3: 公共区域前置（HalconImageOpPipeline：区域/掩码构建 + ReduceDomain）=====
                    // helper 边界：只做图像源 + 区域管理 + 位置修正；图像预处理属算法首段，由本工具在取得处理图后执行
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
                            // 已配置绑定源但上游节点尚未返回有效矩阵（非错误，降级为信息日志）
                            parameters.LogInfo("位置修正已启用，但绑定源当前无有效矩阵，跳过位置修正");
                        }
                        else
                        {
                            // 未配置绑定源：发出警告但不阻断执行（用户可能忘记配置绑定源）
                            parameters.LogWarning("位置修正已启用但未配置绑定源，跳过位置修正");
                        }
                    }

                    var masked = HalconImageOpPipeline.Prepare(
                        grayImage, imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiType.Inspection),
                        parameters.GetRoisByType(RoiType.Mask),
                        parameters.NodeResults, correctionMatrix);
                    stepTimings.AddRange(masked.Timings);

                    try
                    {
                        // 接管核心处理图：预处理需就地替换 → Detach 取得所有权（由原 using(processingImage) 统一释放）
                        var processingImage = masked.DetachWorkingImage();

                        // ===== Step 3.5: 图像预处理（可选） =====
                        if (parameters.IsPreprocessingEnabled && parameters.PreprocessMethodValue.Value != PreprocessMethod.None)
                        {
                            stepSw.Restart();
                            var preprocessed = ApplyPreprocessing(processingImage, parameters);
                            processingImage.Dispose();
                            processingImage = preprocessed;
                            stepSw.Stop();
                            stepTimings.Add(("图像预处理", stepSw.ElapsedMilliseconds));
                        }

                        using (processingImage)
                        {
                            // ===== Step 4: 阈值分割 =====
                            stepSw.Restart();
                            int thresholdMin = parameters.Threshold.Value;
                            int thresholdMax = parameters.MaxValue.Value;

                            HOperatorSet.Threshold(processingImage, out var binaryRegion,
                                thresholdMin, thresholdMax);

                            if (parameters.Type.Value == ThresholdType.BinaryInv)
                            {
                                HOperatorSet.Complement(binaryRegion, out var complRegion);
                                binaryRegion.Dispose();
                                binaryRegion = complRegion;
                            }

                            double actualThreshold = (thresholdMin + thresholdMax) / 2.0;
                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            using (binaryRegion)
                            {
                                // ===== Step 4.5: 形态学处理（可选） =====
                                if (parameters.IsMorphologyEnabled)
                                {
                                    stepSw.Restart();
                                    var morphed = ApplyMorphology(new HRegion(binaryRegion), parameters);
                                    binaryRegion.Dispose();
                                    binaryRegion = morphed;
                                    stepSw.Stop();
                                    stepTimings.Add(("形态学处理", stepSw.ElapsedMilliseconds));
                                }

                                // ===== Step 5: 连通域分析 =====
                                stepSw.Restart();
                                HOperatorSet.Connection(binaryRegion, out var connectedRegions);
                                stepSw.Stop();
                                stepTimings.Add(("连通域分析", stepSw.ElapsedMilliseconds));

                                using (connectedRegions)
                                {
                                    HOperatorSet.CountObj(connectedRegions, out var numReg);
                                    int rawBlobCount = (int)numReg.L;

                                    // ===== Step 6~8: 按需特征提取 + Tab3筛选 + Tab4判断 =====
                                    stepSw.Restart();

                                    // Step 6: SelectShape - Halcon原生面积筛选（仅对 Tab3 面积筛选启用时执行）
                                    bool useAreaFilter = parameters.IsAreaFilterEnabled && rawBlobCount > 0;

                                    HRegion shapeFilteredRegions;
                                    if (useAreaFilter)
                                    {
                                        HOperatorSet.SelectShape(connectedRegions, out var selected,
                                            "area", "and", parameters.FilterAreaMin, parameters.FilterAreaMax);
                                        shapeFilteredRegions = new HRegion(selected);
                                    }
                                    else
                                    {
                                        shapeFilteredRegions = new HRegion(connectedRegions);
                                    }

                                    using (shapeFilteredRegions)
                                    {
                                        HOperatorSet.CountObj(shapeFilteredRegions, out var numSel);
                                        int candidateCount = (int)numSel.L;

                                        // Step 7: 对剩余 blob 按需计算特征 + Tab3 C#筛选 + Tab4判断
                                        var filteredBlobs = new List<BlobResult>(candidateCount);
                                        var config = parameters.ResultConfig;

                                        bool tab3FilterCircularity = parameters.IsCircularityFilterEnabled;
                                        bool tab3FilterRectangularity = parameters.IsRectangularityFilterEnabled;

                                        for (int i = 1; i <= candidateCount; i++)
                                        {
                                            HOperatorSet.SelectObj(shapeFilteredRegions, out var singleObj, i);
                                            using var singleRegion = new HRegion(singleObj);

                                            // 基础特征（面积、质心、BBox、矩形度、圆度、周长）
                                            HOperatorSet.AreaCenter(singleRegion,
                                                out var area, out var row, out var col);

                                            HOperatorSet.SmallestRectangle1(singleRegion,
                                                out var r1, out var c1, out var r2, out var c2);

                                            HOperatorSet.Rectangularity(singleRegion,
                                                out var rectangularity);

                                            HOperatorSet.Circularity(singleRegion,
                                                out var circularity);

                                            HOperatorSet.Contlength(singleRegion,
                                                out var contLength);

                                            var bbox = new System.Windows.Rect(
                                                c1.L, r1.L,
                                                c2.L - c1.L + 1, r2.L - r1.L + 1);

                                            var blob = new BlobResult
                                            {
                                                Area = area.L,
                                                Perimeter = contLength.D,
                                                CentroidX = col.D,
                                                CentroidY = row.D,
                                                Circularity = Math.Min(circularity.D, 1.0),
                                                Rectangularity = Math.Min(rectangularity.D, 1.0),
                                                BoundingBox = bbox
                                            };

                                            // ★ Phase A: Tab3 筛选（不通过则丢弃；面积已由 Halcon SelectShape 筛过，此处仅补圆度/矩形度）
                                            bool passesTab3Filter = true;
                                            if (passesTab3Filter && tab3FilterCircularity)
                                            {
                                                if (blob.Circularity < parameters.FilterCircularityMin || blob.Circularity > parameters.FilterCircularityMax)
                                                    passesTab3Filter = false;
                                            }
                                            if (passesTab3Filter && tab3FilterRectangularity)
                                            {
                                                if (blob.Rectangularity < parameters.FilterRectangularityMin || blob.Rectangularity > parameters.FilterRectangularityMax)
                                                    passesTab3Filter = false;
                                            }

                                            if (!passesTab3Filter)
                                                continue; // Tab3 丢弃

                                            // ★ Phase B: Tab4 独立判断（标记 JudgmentPassed 但不丢弃）
                                            blob.JudgmentPassed = EvaluateBlobJudgment(blob, config);

                                            // 提取轮廓（所有 blob 都有轮廓，颜色由 JudgmentPassed 通过 contourPassList 控制）
                                            var geom = ContourGeometryFactory.FromRegionSmooth(singleRegion);
                                            if (geom.Points.Length > 0)
                                                blob.Contour = geom.Points;

                                            blob.Label = filteredBlobs.Count + 1;
                                            filteredBlobs.Add(blob);
                                        }

                                        stepSw.Stop();
                                        stepTimings.Add(("特征提取+判断", stepSw.ElapsedMilliseconds));

                                        // ===== Step 8.5: 排序 =====
                                        if (parameters.SortMode.Value != BlobSortMode.None)
                                        {
                                            SortBlobs(filteredBlobs, parameters.SortMode.Value);
                                        }

                                        // ===== Step 9: 设置结果数据 =====
                                        stepSw.Restart();
                                        result.GrayImage = new HalImage(grayImage.Clone());
                                        result.FilteredBlobs = filteredBlobs;
                                        result.ThresholdUsed = (int)actualThreshold;
                                        result.MaxValueUsed = parameters.MaxValue.Value;
                                        result.TypeUsed = parameters.Type.Value.ToString();

                                        // 区域本体保留（供下游区域运算/图像叠加消费；不进 GetPropertyMap 契约）
                                        result.BlobRegions = new HalRegion(shapeFilteredRegions.Clone());

                                        // 二值掩码
                                        HOperatorSet.RegionToBin(binaryRegion, out var binObj,
                                            255, 0, imgWidth, imgHeight);
                                        result.BinaryMask = new HalImage(new HImage(binObj));

                                        // 合成显示：PaintRegion 在灰度图上绘制二值区域
                                        var overlayClone = processingImage.Clone();
                                        HOperatorSet.PaintRegion(binaryRegion, overlayClone,
                                            out var paintedObj, 255.0, "fill");
                                        result.OverlayImage = new HalImage(new HImage(paintedObj));
                                        overlayClone.Dispose();
                                        stepSw.Stop();
                                        stepTimings.Add(("结果赋值", stepSw.ElapsedMilliseconds));

                                        // ===== Step 10: 灰度直方图统计 =====
                                        stepSw.Restart();
                                        HOperatorSet.GrayHisto(binaryRegion, processingImage,
                                            out var absoluteHisto, out _);
                                        
                                        result.HistogramData = Array.ConvertAll(absoluteHisto.LArr, v => (double)v);

                                        HOperatorSet.Intensity(binaryRegion, processingImage,
                                            out var meanVal, out var devVal);
                                        result.ChannelMeans = new[] { meanVal.D };
                                        stepSw.Stop();
                                        stepTimings.Add(("灰度统计", stepSw.ElapsedMilliseconds));

                                        result.ProcessedAt = DateTime.Now;

                                        // ===== Step 11: 结果判断 =====
                                        stepSw.Restart();
                                        EvaluateJudgments(parameters.ResultConfig, result, new HRegion(binaryRegion), filteredBlobs, imgWidth, imgHeight);
                                        stepSw.Stop();
                                        stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                                        // ===== Step 12: 构建显示项 =====
                                        stepSw.Restart();
                                        var display = result.Display;
                                        display.Clear();

                                        // 轮廓：仅对有轮廓数据的 blob（通过 Tab4 判断的 blob 有轮廓）
                                        var contourBlobs = filteredBlobs
                                            .Where(b => b.HasContour)
                                            .ToList();
                                        if (contourBlobs.Count > 0)
                                        {
                                            var allContourPoints = contourBlobs.Select(b => b.Contour!).ToList();
                                            var contourPassList = contourBlobs.Select(b => b.JudgmentPassed).ToList();
                                            display.AddContours(parameters.ContoursStyle, allContourPoints, contourPassList);
                                        }

                                        // 外接矩形（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        foreach (var blob in filteredBlobs)
                                        {
                                            var bbox = blob.BoundingBox;
                                            if (bbox.Width > 0 && bbox.Height > 0)
                                                display.AddRect(parameters.BlobsStyle, bbox, blob.JudgmentPassed);
                                        }

                                        // 质心十字（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        foreach (var blob in filteredBlobs)
                                        {
                                            if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                                display.AddCross(parameters.CentroidsStyle,
                                                    new WpfPoint(blob.CentroidX, blob.CentroidY), blob.JudgmentPassed);
                                        }

                                        // 面积文本（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        var areaStyle = parameters.AreaDisplayStyle;
                                        foreach (var blob in filteredBlobs)
                                        {
                                            if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                                display.AddText(areaStyle, areaStyle.PositionX, areaStyle.PositionY,
                                                    $"{blob.Label}: {blob.Area:F0}px", blob.JudgmentPassed);
                                        }

                                        stepSw.Stop();
                                        stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

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
                                            $"===== Blob分析性能报告 =====",
                                            $"图像: {imgWidth}x{imgHeight}  Halcon原生  总耗时: {totalMs}ms",
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
                                        reportLines.Add($"结果: 原始连通域 {rawBlobCount}, 筛选后 {filteredBlobs.Count}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                                        parameters.LogInfo(string.Join("\n", reportLines));
                                    }
                                }
                            }
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
                parameters.LogError($"Blob分析处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(BlobAnalysisParameters parameters)
        {
            if (parameters == null)
                return ValidationResult.Failure("参数不能为空");
            return parameters.Validate();
        }

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new BlobAnalysisToolDebugControl("BlobAnalysis", tool, null);
        }

        public FrameworkElement? CreateDataAnalysisControl()
        {
            return new BlobDataAnalysisControl();
        }

        /// <summary>
        /// 评估整体结果判断（OK/NG判定）- 仅保留整体统计判断
        /// </summary>
        /// <remarks>
        /// Per-Blob 判断（Tab4）已分离到 EvaluateBlobJudgment，不在此处处理。
        /// Tab3 筛选在特征提取循环中处理。
        /// 此处只处理基于 FilteredBlobs 聚合统计的整体判断条件。
        /// </remarks>
        private static void EvaluateJudgments(
            BlobAnalysisResultConfig config,
            BlobAnalysisResults result,
            HRegion binaryRegion,
            List<BlobResult> filteredBlobs,
            int imageWidth,
            int imageHeight)
        {
            if (config == null) return;

            var messages = result.JudgmentMessages;
            messages.Clear();

            int filteredCount = filteredBlobs.Count;
            double filteredTotalArea = filteredBlobs.Sum(b => b.Area);
            double averageArea = filteredCount > 0 ? filteredBlobs.Average(b => b.Area) : 0;
            double maxArea = filteredCount > 0 ? filteredBlobs.Max(b => b.Area) : 0;
            double minArea = filteredCount > 0 ? filteredBlobs.Min(b => b.Area) : 0;
            double averageCircularity = filteredCount > 0 ? filteredBlobs.Average(b => b.Circularity) : 0;
            double averageRectangularity = filteredCount > 0 ? filteredBlobs.Average(b => b.Rectangularity) : 0;
            double totalPixels = imageWidth * imageHeight;

            HOperatorSet.AreaCenter(binaryRegion, out var whitePixelCount, out _, out _);
            double whitePixelRatio = totalPixels > 0 ? whitePixelCount.L / totalPixels * 100 : 0;
            result.WhitePixelRatio = whitePixelRatio;

            // 整体判断
            bool wholePassed = true;

            if (config.IsEmptyCheckEnabled)
            {
                bool isEmpty = filteredCount == 0;
                if (isEmpty) { wholePassed = false; messages.Add("[NG] 输出为空：未检测到符合条件的Blob"); }
                else { messages.Add($"[OK] 输出不为空：检测到 {filteredCount} 个Blob"); }
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

            result.JudgmentPassed = wholePassed;

            if (messages.Count > 0)
            {
                string summary = wholePassed ? "全部通过 ✓" : "存在未通过项 ✗";
                messages.Insert(0, $"===== 结果判断: {summary} =====");
            }
        }

        /// <summary>
        /// Tab4 独立判断：判断单个 Blob 是否通过（仅标记 JudgmentPassed，不丢弃 blob）
        /// </summary>
        /// <returns>
        /// true 表示通过判断（显示绿色）；false 表示未通过（显示红色）。
        /// 无论通过与否，blob 都会被保留在 FilteredBlobs 中（Tab3 已筛选过）。
        /// </returns>
        private static bool EvaluateBlobJudgment(BlobResult blob, BlobAnalysisResultConfig config)
        {
            if (!config.HasPerBlobJudgmentEnabled)
                return true;

            if (config.IsPerBlobAreaCheckEnabled)
            {
                if (blob.Area < config.PerBlobAreaMin || blob.Area > config.PerBlobAreaMax)
                    return false;
            }

            if (config.IsPerBlobCircularityCheckEnabled)
            {
                if (blob.Circularity < config.PerBlobCircularityMin || blob.Circularity > config.PerBlobCircularityMax)
                    return false;
            }

            if (config.IsPerBlobRectangularityCheckEnabled)
            {
                if (blob.Rectangularity < config.PerBlobRectangularityMin || blob.Rectangularity > config.PerBlobRectangularityMax)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 对筛选后的 Blob 列表按指定排序方式进行排序
        /// </summary>
        private static void SortBlobs(List<BlobResult> blobs, BlobSortMode mode)
        {
            blobs.Sort((a, b) => mode switch
            {
                BlobSortMode.AreaAsc => a.Area.CompareTo(b.Area),
                BlobSortMode.AreaDesc => b.Area.CompareTo(a.Area),
                BlobSortMode.CircularityAsc => a.Circularity.CompareTo(b.Circularity),
                BlobSortMode.CircularityDesc => b.Circularity.CompareTo(a.Circularity),
                BlobSortMode.RectangularityAsc => a.Rectangularity.CompareTo(b.Rectangularity),
                BlobSortMode.RectangularityDesc => b.Rectangularity.CompareTo(a.Rectangularity),
                BlobSortMode.XAsc => a.CentroidX.CompareTo(b.CentroidX),
                BlobSortMode.XDesc => b.CentroidX.CompareTo(a.CentroidX),
                BlobSortMode.YAsc => a.CentroidY.CompareTo(b.CentroidY),
                BlobSortMode.YDesc => b.CentroidY.CompareTo(a.CentroidY),
                _ => 0
            });

            // 排序后重新标注标签
            for (int i = 0; i < blobs.Count; i++)
            {
                blobs[i].Label = i + 1;
            }
        }

        #region 图像预处理辅助方法

        /// <summary>
        /// 应用图像预处理（均值/高斯/中值/直方图均衡化）
        /// </summary>
        private static HImage ApplyPreprocessing(HImage image, BlobAnalysisParameters parameters)
        {
            var method = parameters.PreprocessMethodValue.Value;
            int kernelSize = parameters.PreprocessKernelSize.Value;

            switch (method)
            {
                case PreprocessMethod.Mean:
                    HOperatorSet.MeanImage(image, out var meanImg, kernelSize, kernelSize);
                    return new HImage(meanImg);

                case PreprocessMethod.Gaussian:
                    HOperatorSet.SmoothImage(image, out var gaussImg, "gauss", kernelSize);
                    return new HImage(gaussImg);

                case PreprocessMethod.Median:
                    HOperatorSet.MedianImage(image, out var medianImg, "circle", kernelSize, "mirrored");
                    return new HImage(medianImg);

                case PreprocessMethod.HistogramEqualization:
                    HOperatorSet.EquHistoImage(image, out var eqImg);
                    return new HImage(eqImg);

                default:
                    return image.Clone();
            }
        }

        #endregion

        #region 形态学处理辅助方法

        /// <summary>
        /// 应用形态学处理（腐蚀/膨胀/开闭运算/顶帽/黑帽）
        /// </summary>
        private static HRegion ApplyMorphology(HRegion inputRegion, BlobAnalysisParameters parameters)
        {
            var operation = parameters.MorphOperationValue.Value;
            var elementShape = parameters.MorphElementShapeValue.Value;
            int width = parameters.MorphElementWidth.Value;
            int height = parameters.MorphElementHeight.Value;
            int iterations = parameters.MorphIterations.Value;

            // 顶帽/黑帽固定迭代1次
            if (operation is MorphOperationType.TopHat or MorphOperationType.BottomHat)
                iterations = 1;

            using var se = BuildStructuringElement(elementShape, width, height);

            return operation switch
            {
                MorphOperationType.Erosion => ApplyErosion(inputRegion, se, iterations),
                MorphOperationType.Dilation => ApplyDilation(inputRegion, se, iterations),
                MorphOperationType.Opening => ApplyOpening(inputRegion, se),
                MorphOperationType.Closing => ApplyClosing(inputRegion, se),
                MorphOperationType.TopHat => ApplyTopHat(inputRegion, se),
                MorphOperationType.BottomHat => ApplyBottomHat(inputRegion, se),
                _ => inputRegion.Clone()
            };
        }

        /// <summary>
        /// 构建结构元素
        /// </summary>
        private static HRegion BuildStructuringElement(MorphElementShapeType shape, int width, int height)
        {
            if (shape == MorphElementShapeType.Rectangle)
            {
                HOperatorSet.GenRectangle1(out var rect, 0, 0, height - 1, width - 1);
                return new HRegion(rect);
            }
            else
            {
                int radius = Math.Max(width, height) / 2;
                radius = Math.Max(radius, 1);
                HOperatorSet.GenCircle(out var circle, radius, radius, radius);
                return new HRegion(circle);
            }
        }

        private static HRegion ApplyErosion(HRegion region, HRegion se, int iterations)
        {
            HOperatorSet.Erosion1(region, se, out var result, iterations);
            return new HRegion(result);
        }

        private static HRegion ApplyDilation(HRegion region, HRegion se, int iterations)
        {
            HOperatorSet.Dilation1(region, se, out var result, iterations);
            return new HRegion(result);
        }

        private static HRegion ApplyOpening(HRegion region, HRegion se)
        {
            HOperatorSet.Opening(region, se, out var result);
            return new HRegion(result);
        }

        private static HRegion ApplyClosing(HRegion region, HRegion se)
        {
            HOperatorSet.Closing(region, se, out var result);
            return new HRegion(result);
        }

        private static HRegion ApplyTopHat(HRegion region, HRegion se)
        {
            HOperatorSet.TopHat(region, se, out var result);
            return new HRegion(result);
        }

        private static HRegion ApplyBottomHat(HRegion region, HRegion se)
        {
            HOperatorSet.BottomHat(region, se, out var result);
            return new HRegion(result);
        }

        #endregion
    }
}
