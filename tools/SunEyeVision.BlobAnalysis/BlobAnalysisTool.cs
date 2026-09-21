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
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Validation;
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
    public class BlobAnalysisTool : IToolPlugin<BlobAnalysisParameters, BlobAnalysisResults>, IToolEditorFactory
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
                    // ===== Step 2: 区域前置（HalconRegionPipeline：订阅解析 + 位置修正 + 差集）=====
                    // 纯几何：区域 + 位置修正；图像预处理属算法首段（本工具在取得处理图后执行），全图/按域由算法自决
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

                    var scope = HalconRegionPipeline.Resolve(
                        imgWidth, imgHeight,
                        parameters.GetRoisByType(RoiParameterMode.InspectionRoi),
                        parameters.GetRoisByType(RoiParameterMode.MaskRoi),
                        parameters.NodeResults, correctionMatrix);
                    stepTimings.AddRange(scope.Timings);

                    try
                    {
                        // 算法决策：按域执行；图像预处理需就地替换 → 处理图由本工具自管（原 using(processingImage) 统一释放）
                        var processingImage = HalImageDomain.Restrict(grayImage, scope.RoiRegion);

                        // ===== Step 3.5: 图像预处理（可选） =====
                        if (parameters.IsPreprocessingEnabled && parameters.PreprocessMethodValue != PreprocessMethod.None)
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
                            int thresholdMin = parameters.Threshold;
                            int thresholdMax = parameters.MaxValue;

                            HObject thresholdObject;
                            HOperatorSet.Threshold(processingImage, out thresholdObject,
                                thresholdMin, thresholdMax);

                            stepSw.Stop();
                            stepTimings.Add(("阈值分割", stepSw.ElapsedMilliseconds));

                            // 不要在 using 变量上重新赋值。原实现把 BinaryInv/形态学结果
                            // 写回 using 变量，编译器已明确警告其 Dispose 目标不再是当前对象，
                            // 这会造成区域泄漏或错误释放。现在明确区分阈值原始对象和当前分析区域。
                            using (thresholdObject)
                            {
                                HRegion binaryRegion;
                                if (parameters.Type == ThresholdType.BinaryInv)
                                {
                                    HOperatorSet.Complement(thresholdObject, out var complementObject);
                                    binaryRegion = new HRegion(complementObject);
                                }
                                else
                                {
                                    binaryRegion = new HRegion(thresholdObject.Clone());
                                }

                                try
                                {
                                    // ===== Step 4.5: 形态学处理（可选） =====
                                    if (parameters.IsMorphologyEnabled)
                                    {
                                        stepSw.Restart();
                                        var morphed = ApplyMorphology(binaryRegion.Clone(), parameters);
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
                                        var finalRegionIndices = new List<int>();
                                        var okRegionIndices = new List<int>();
                                        var ngRegionIndices = new List<int>();
                                        var regionIndexByBlob = new Dictionary<BlobResult, int>();
                                        var config = parameters.ResultPresentation;

                                        bool tab3FilterCircularity = parameters.IsCircularityFilterEnabled;
                                        bool tab3FilterRectangularity = parameters.IsRectangularityFilterEnabled;

                                        HOperatorSet.AreaCenter(shapeFilteredRegions, out var areas, out var rows, out var cols);
                                        HOperatorSet.SmallestRectangle1(shapeFilteredRegions, out var r1s, out var c1s, out var r2s, out var c2s);
                                        HOperatorSet.Rectangularity(shapeFilteredRegions, out var rectangularities);
                                        HOperatorSet.Circularity(shapeFilteredRegions, out var circularities);
                                        HOperatorSet.Contlength(shapeFilteredRegions, out var contLengths);
                                        HOperatorSet.SmallestRectangle2(shapeFilteredRegions, out var rectRows, out var rectCols,
                                            out var rectPhis, out var rectLengths1, out var rectLengths2);

                                        for (int i = 1; i <= candidateCount; i++)
                                        {
                                            var tupleIndex = i - 1;
                                            var area = areas[tupleIndex];
                                            var row = rows[tupleIndex];
                                            var col = cols[tupleIndex];
                                            var r1 = r1s[tupleIndex];
                                            var c1 = c1s[tupleIndex];
                                            var r2 = r2s[tupleIndex];
                                            var c2 = c2s[tupleIndex];
                                            var rectangularity = rectangularities[tupleIndex];
                                            var circularity = circularities[tupleIndex];
                                            var contLength = contLengths[tupleIndex];

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
                                                BoundingBox = bbox,
                                                RotatedBoundingBox = BuildRotatedRectangle(rectRows[tupleIndex].D, rectCols[tupleIndex].D,
                                                    rectPhis[tupleIndex].D, rectLengths1[tupleIndex].D, rectLengths2[tupleIndex].D)
                                            };

                                            // ★ Phase A: Tab3 筛选（不通过则丢弃；面积已由 Halcon SelectShape 筛过，此处仅补圆度/矩形度）
                                            bool passesTab3Filter = true;
                                            if (passesTab3Filter && parameters.IsAreaFilterEnabled)
                                            {
                                                if (blob.Area < parameters.FilterAreaMin || blob.Area > parameters.FilterAreaMax)
                                                    passesTab3Filter = false;
                                            }
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

                                            // BlobRegions 必须与 FilteredBlobs 使用同一套 Tab3 筛选结果。
                                            // 旧实现直接复制 shapeFilteredRegions，导致区域输出仍包含被圆度/矩形度
                                            // 淘汰的候选，图形结果与数值结果不一致。
                                            // ★ Phase B: Tab4 独立判断（标记 JudgmentPassed 但不丢弃）
                                            blob.JudgmentPassed = EvaluateBlobJudgment(blob, config);

                                            // 只记录通过筛选的对象索引，循环结束后一次性 SelectObj。
                                            // 逐 Blob Clone + ConcatObj 会在数万区域时产生 O(n²) 的句柄和复制开销。
                                            finalRegionIndices.Add(i);
                                            if (blob.JudgmentPassed)
                                                okRegionIndices.Add(i);
                                            else
                                                ngRegionIndices.Add(i);

                                            // 轮廓提取推迟到最终集合确定后统一执行（见下方“轮廓提取”步骤），
                                            // 避免在候选循环中逐 Blob 调用 GenContourRegionXld 把执行线程拖死。
                                            blob.Label = filteredBlobs.Count + 1;
                                            filteredBlobs.Add(blob);
                                            regionIndexByBlob[blob] = i;
                                        }

                                        stepSw.Stop();
                                        stepTimings.Add(("特征提取+判断", stepSw.ElapsedMilliseconds));

                                        // ===== Step 8.5: 排序 =====
                                        if (parameters.SortMode != BlobSortMode.None)
                                        {
                                            SortBlobs(filteredBlobs, parameters.SortMode);
                                        }

                                        if (parameters.IsBlobCountFilterEnabled && filteredBlobs.Count > parameters.BlobCountLimit)
                                            filteredBlobs.RemoveRange(parameters.BlobCountLimit, filteredBlobs.Count - parameters.BlobCountLimit);

                                        finalRegionIndices = filteredBlobs.Select(blob => regionIndexByBlob[blob]).ToList();
                                        okRegionIndices = filteredBlobs.Where(blob => blob.JudgmentPassed).Select(blob => regionIndexByBlob[blob]).ToList();
                                        ngRegionIndices = filteredBlobs.Where(blob => !blob.JudgmentPassed).Select(blob => regionIndexByBlob[blob]).ToList();
                                        var contourSw = Stopwatch.StartNew();
                                        for (var index = 0; index < filteredBlobs.Count && parameters.OutputContours; index++)
                                        {
                                            var blob = filteredBlobs[index];
                                            blob.Label = index + 1;
                                            HOperatorSet.SelectObj(shapeFilteredRegions, out var singleObj, regionIndexByBlob[blob]);
                                            using var singleRegion = new HRegion(singleObj);
                                            var geom = ContourGeometryFactory.FromRegionSmooth(singleRegion);
                                            if (geom.Points.Length > 0)
                                                blob.Contour = geom.Points;
                                        }
                                        contourSw.Stop();
                                        stepTimings.Add(("轮廓提取", contourSw.ElapsedMilliseconds));

                                        // ===== Step 9: 设置结果数据 =====
                                        stepSw.Restart();
                                        result.GrayImage = parameters.OutputBlobImage
                                            ? new HalImage(grayImage.Clone())
                                            : null;
                                        result.FilteredBlobs = filteredBlobs;
                                        result.ThresholdMinUsed = thresholdMin;
                                        result.ThresholdMaxUsed = thresholdMax;
                                        result.TypeUsed = parameters.Type.ToString();

                                        // 区域本体保留（供下游区域运算/图像叠加消费；不进 GetPropertyMap 契约）。
                                        // 使用 Halcon 一次性 SelectObj，避免逐区域聚合导致执行时间随区域数平方增长。
                                        // 区域本体与最终 FilteredBlobs 一一对应；Tab4 判断为 NG 的
                                        // Blob 仍保留，颜色由 JudgmentPassed 决定。
                                        result.BlobRegions = new HalRegion(SelectRegions(shapeFilteredRegions, finalRegionIndices));
                                        result.OkBlobRegions = new HalRegion(SelectRegions(shapeFilteredRegions, okRegionIndices));
                                        result.NgBlobRegions = new HalRegion(SelectRegions(shapeFilteredRegions, ngRegionIndices));

                                        // 二值图按输出开关生成；OverlayImage 不再作为无条件中间图输出。
                                        if (parameters.OutputBinaryImage)
                                        {
                                            HOperatorSet.RegionToBin(binaryRegion, out var binObj,
                                                255, 0, imgWidth, imgHeight);
                                            result.BinaryMask = new HalImage(new HImage(binObj));
                                        }
                                        else
                                        {
                                            result.BinaryMask = null;
                                        }
                                        result.OverlayImage = null;
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
                                        EvaluateJudgments(parameters.ResultPresentation, result, new HRegion(binaryRegion), filteredBlobs, imgWidth, imgHeight);
                                        stepSw.Stop();
                                        stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                                        // ===== Step 12: 构建显示项 =====
                                        stepSw.Restart();
                                        var display = result.Display;
                                        display.Clear();

                                        // 轮廓：最终集合中已提取到轮廓数据的 blob
                                        var displayBlobs = filteredBlobs;
                                        var contourBlobs = displayBlobs
                                            .Where(b => b.HasContour)
                                            .ToList();
                                        // 轮廓的显示与计算统一由「轮廓输出」开关管理（轮廓样式只提供颜色/线宽）。
                                        var contourStyle = parameters.ResultPresentation.GetGraphicsStyle(BlobAnalysisParameters.ContourGraphicsKey);
                                        if (parameters.OutputContours && contourBlobs.Count > 0)
                                        {
                                            var okContours = contourBlobs
                                                .Where(b => b.JudgmentPassed)
                                                .Select(b => b.Contour!)
                                                .ToList();
                                            var ngContours = contourBlobs
                                                .Where(b => !b.JudgmentPassed)
                                                .Select(b => b.Contour!)
                                                .ToList();
                                            display.AddContourBatch(contourStyle, okContours, true);
                                            display.AddContourBatch(contourStyle, ngContours, false);
                                        }

                                        // 外接矩形（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        // 图元一律产出：是否显示由样式可见性在渲染阶段决定，否则
                                        // “先隐藏、再执行、后打开”会因执行时未产出图元而永远无法显示。
                                        var blobStyle = parameters.ResultPresentation.GetGraphicsStyle("blobs");
                                        var okBoxes = displayBlobs.Where(b => b.JudgmentPassed && b.RotatedBoundingBox is { Length: >= 4 })
                                            .Select(b => b.RotatedBoundingBox!).ToList();
                                        var ngBoxes = displayBlobs.Where(b => !b.JudgmentPassed && b.RotatedBoundingBox is { Length: >= 4 })
                                            .Select(b => b.RotatedBoundingBox!).ToList();
                                        display.AddContourBatch(blobStyle, okBoxes, true);
                                        display.AddContourBatch(blobStyle, ngBoxes, false);

                                        // 质心十字（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        // 同上：无条件产出，可见性由样式在渲染阶段决定。
                                        var centroidStyle = parameters.ResultPresentation.GetGraphicsStyle("centroids");
                                        foreach (var blob in displayBlobs)
                                        {
                                            if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                                display.AddCross(centroidStyle,
                                                    new WpfPoint(blob.CentroidX, blob.CentroidY), blob.JudgmentPassed);
                                        }

                                        // 面积文本（根据 JudgmentPassed 显示 OK/NG 颜色）
                                        var areaStyle = parameters.ResultPresentation.GetTextStyle("area");
                                        foreach (var blob in displayBlobs)
                                        {
                                            if (blob.CentroidX > 0 || blob.CentroidY > 0)
                                                display.AddText(areaStyle, areaStyle.PositionX, areaStyle.PositionY,
                                                    $"{blob.Label}: {blob.Area:F0}px", blob.JudgmentPassed);
                                        }

                                        stepSw.Stop();
                                        stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                                        // 提取检测区/屏蔽区几何输出（RoI 契约：结果树字段聚合 + 下游绑定）
                                        // 统一经 SDK ResolveRuntime 出口，传入上游结果上下文以支持订阅/拼装区域；
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
                                finally
                                {
                                    binaryRegion.Dispose();
                                }
                            }
                        }
                    }
                    finally
                    {
                        scope.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"Blob分析处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
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
        /// 评估整体结果判断（OK/NG判定）。
        /// </summary>
        /// <remarks>
        /// Per-Blob 判断的单项计算由 EvaluateBlobJudgment 完成；本方法负责把单项结果
        /// 合并到工具总体判定。否则会出现单项均显示 NG、模块判定结果却显示 OK 的矛盾。
        /// Tab3 筛选在特征提取循环中处理。
        /// 此处只处理基于 FilteredBlobs 聚合统计的整体判断条件。
        /// </remarks>
        private static void EvaluateJudgments(
            ResultPresentationConfig config,
            BlobAnalysisResults result,
            HRegion binaryRegion,
            List<BlobResult> filteredBlobs,
            int imageWidth,
            int imageHeight)
        {
            var messages = result.JudgmentMessages;
            messages.Clear();

            int count = filteredBlobs.Count;
            double totalArea = filteredBlobs.Sum(blob => blob.Area);
            double averageArea = count > 0 ? filteredBlobs.Average(blob => blob.Area) : 0;
            double maxArea = count > 0 ? filteredBlobs.Max(blob => blob.Area) : 0;
            double minArea = count > 0 ? filteredBlobs.Min(blob => blob.Area) : 0;
            double averageCircularity = count > 0 ? filteredBlobs.Average(blob => blob.Circularity) : 0;
            double averageRectangularity = count > 0 ? filteredBlobs.Average(blob => blob.Rectangularity) : 0;
            HOperatorSet.AreaCenter(binaryRegion, out var whitePixelCount, out _, out _);
            double totalPixels = imageWidth * imageHeight;
            result.WhitePixelRatio = totalPixels > 0 ? whitePixelCount.L / totalPixels * 100 : 0;

            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(
                config.Judgments.Where(item => item.Scope == JudgmentScope.Overall),
                key => key switch
                {
                    "output-not-empty" => count > 0 ? 1 : 0,
                    "white-ratio" => result.WhitePixelRatio,
                    "blob-count" => count,
                    "total-area" => totalArea,
                    "average-area" => averageArea,
                    "max-area" => maxArea,
                    "min-area" => minArea,
                    "average-circularity" => averageCircularity,
                    "average-rectangularity" => averageRectangularity,
                    _ => null,
                });

            var overallPassed = ResultJudgmentEvaluator.IsAllPass(outcomes);
            var enabledPerBlobJudgments = config.Judgments
                .Where(item => item.Scope == JudgmentScope.PerItem && item.Enabled)
                .ToList();
            var failedBlobCount = enabledPerBlobJudgments.Count == 0
                ? 0
                : filteredBlobs.Count(blob => !blob.JudgmentPassed);
            var perBlobPassed = failedBlobCount == 0;

            // 启用“每个 Blob …”判断时，任何一个保留 Blob 未通过都应使总体结果为 NG。
            result.IsPass = overallPassed && perBlobPassed;
            foreach (var outcome in outcomes.Where(item => config.GetJudgment(item.Key).Enabled))
                messages.Add($"[{(outcome.IsPass ? "OK" : "NG")}] {outcome.Message}");
            if (enabledPerBlobJudgments.Count > 0)
            {
                messages.Add(failedBlobCount == 0
                    ? $"[OK] 每个 Blob 判断全部通过（{filteredBlobs.Count} 个）"
                    : $"[NG] 每个 Blob 判断未通过：{failedBlobCount}/{filteredBlobs.Count} 个");
            }
            if (messages.Count > 0)
                messages.Insert(0, $"===== 结果判断: {(result.IsPass ? "全部通过 ✓" : "存在未通过项 ✗")} =====");
        }
        /// <summary>
        /// Tab4 独立判断：判断单个 Blob 是否通过（仅标记 JudgmentPassed，不丢弃 blob）
        /// </summary>
        /// <returns>
        /// true 表示通过判断（显示绿色）；false 表示未通过（显示红色）。
        /// 无论通过与否，blob 都会被保留在 FilteredBlobs 中（Tab3 已筛选过）。
        /// </returns>
        private static bool EvaluateBlobJudgment(BlobResult blob, ResultPresentationConfig config)
        {
            var outcomes = ResultJudgmentEvaluator.EvaluateRanges(
                config.Judgments.Where(item => item.Scope == JudgmentScope.PerItem),
                key => key switch
                {
                    "per-blob-area" => blob.Area,
                    "per-blob-circularity" => blob.Circularity,
                    "per-blob-rectangularity" => blob.Rectangularity,
                    _ => null,
                });
            return ResultJudgmentEvaluator.IsAllPass(outcomes);
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
            var method = parameters.PreprocessMethodValue;
            int kernelSize = parameters.PreprocessKernelSize;

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
            var operation = parameters.MorphOperationValue;
            var elementShape = parameters.MorphElementShapeValue;
            int width = parameters.MorphElementWidth;
            int height = parameters.MorphElementHeight;
            int iterations = parameters.MorphIterations;

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

        private static HRegion SelectRegions(HRegion source, IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return new HRegion();
            var tuple = new HTuple(indices.ToArray());
            HOperatorSet.SelectObj(source, out var selected, tuple);
            tuple.Dispose();
            return new HRegion(selected);
        }

        private static WpfPoint[] BuildRotatedRectangle(double row, double col, double phi, double length1, double length2)
        {
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            var axis = new WpfPoint(cos, sin);
            var normal = new WpfPoint(-sin, cos);
            return new[]
            {
                new WpfPoint(col - axis.X * length1 - normal.X * length2, row - axis.Y * length1 - normal.Y * length2),
                new WpfPoint(col + axis.X * length1 - normal.X * length2, row + axis.Y * length1 - normal.Y * length2),
                new WpfPoint(col + axis.X * length1 + normal.X * length2, row + axis.Y * length1 + normal.Y * length2),
                new WpfPoint(col - axis.X * length1 + normal.X * length2, row - axis.Y * length1 + normal.Y * length2)
            };
        }

        #endregion
    }
}
