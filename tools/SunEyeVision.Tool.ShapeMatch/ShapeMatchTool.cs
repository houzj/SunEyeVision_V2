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
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ShapeMatch.Models;
using SunEyeVision.Tool.ShapeMatch.Views;

namespace SunEyeVision.Tool.ShapeMatch
{
    /// <summary>
    /// 轮廓匹配工具 - 基于形状的模板匹配
    /// </summary>
    /// <remarks>
    /// 算法流程：
    ///   1. 灰度转换
    ///   2. ReduceDomain（搜索区域限制）
    ///   3. 对每个启用模板执行匹配
    ///   4. 结果合并与NMS
    ///   5. 结果排序
    ///   6. 亚像素精定位（可选）
    ///   7. 结果判断（OK/NG）
    ///   8. 构建显示项
    /// </remarks>
    [Tool(
        id: "shape-match",
        displayName: "轮廓匹配",
        Description = "基于形状的轮廓模板匹配",
        Icon = "🔍",
        Category = "图像处理",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class ShapeMatchTool : IToolPlugin<ShapeMatchParameters, ShapeMatchResults>
    {
        private readonly ToolAttribute? _attribute;

        public ShapeMatchTool()
        {
            _attribute = GetType().GetCustomAttribute<ToolAttribute>();
        }

        /// <summary>
        /// 执行工具（同步）
        /// </summary>
        public ShapeMatchResults Run(ShapeMatchParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            var result = new ShapeMatchResults
            {
                ToolName = _attribute?.DisplayName ?? "ShapeMatch",
                ToolId = _attribute?.Id ?? "shape-match",
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
                if (image == null || image.Empty())
                {
                    parameters.LogWarning("输入图像为空");
                    result.SetError("输入图像为空");
                    return result;
                }

                parameters.LogInfo($"开始轮廓匹配处理: 图像尺寸 {image.Width}x{image.Height}");
                result.InputImage = image.Clone();
                result.InputSize = image.Size();

                // ===== Step 1: 灰度转换 =====
                stepSw.Restart();
                using var grayImage = image.Channels() > 1
                    ? image.CvtColor(ColorConversionCodes.BGR2GRAY)
                    : image.Clone();
                // 保存灰度调试图像
                result.DebugGrayImage = grayImage.Clone();
                stepSw.Stop();
                stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

                // ===== Step 1a: 搜索区域限制（ReduceDomain） =====
                stepSw.Restart();
                if (parameters.IsRegionEnabled.Value)
                {
                    var inspectionRegions = parameters.InspectionRegions.Regions
                        .Where(r => r.IsEnabled).ToList();
                    var maskRegions = parameters.MaskRegions.Regions
                        .Where(r => r.IsEnabled).ToList();

                    if (inspectionRegions.Count > 0)
                    {
                        using var inspMask = RegionMaskBuilder.CreateMask(grayImage.Size(), inspectionRegions);
                        // 保存搜索区域掩码调试图像
                        result.DebugSearchRegionMask = inspMask.Clone();
                        if (maskRegions.Count > 0)
                        {
                            using var maskMask = RegionMaskBuilder.CreateMaskMask(grayImage.Size(), maskRegions);
                            using var invMask = new Mat();
                            Cv2.BitwiseNot(maskMask, invMask);
                            using var finalMask = new Mat();
                            Cv2.BitwiseAnd(inspMask, invMask, finalMask);
                            Cv2.BitwiseAnd(grayImage, finalMask, grayImage);
                        }
                        else
                        {
                            Cv2.BitwiseAnd(grayImage, inspMask, grayImage);
                        }
                        parameters.LogInfo($"搜索区域限制已应用: {inspectionRegions.Count}个检测区, {maskRegions.Count}个屏蔽区");
                    }
                    else
                    {
                        parameters.LogWarning("区域限制已启用但无有效检测区域，将在全图匹配");
                    }
                }
                stepSw.Stop();
                stepTimings.Add(("搜索区域限制", stepSw.ElapsedMilliseconds));

                // ===== Step 2: 获取可用模板 =====
                stepSw.Restart();
                var enabledTemplates = parameters.Templates
                    .Where(t => t.IsEnabled && t.ExtractionStatus == TemplateExtractionStatus.Success && t.EdgePoints != null)
                    .ToList();

                if (enabledTemplates.Count == 0)
                {
                    parameters.LogWarning("没有可用的模板（没有已提取成功的启用模板）");
                    result.SetSuccess(stopwatch.ElapsedMilliseconds);
                    result.JudgmentPassed = false;
                    result.JudgmentMessages.Add("无可用模板");
                    return result;
                }

                parameters.LogInfo($"可用模板数量: {enabledTemplates.Count}");
                stepSw.Stop();
                stepTimings.Add(("模板筛选", stepSw.ElapsedMilliseconds));

                // ===== Step 3: 对每个模板执行匹配 =====
                stepSw.Restart();
                var allMatches = new List<ShapeMatchResultItem>();

                foreach (var template in enabledTemplates)
                {
                    var templateMatches = RunSingleTemplateMatch(grayImage, template, parameters);
                    allMatches.AddRange(templateMatches);
                }
                stepSw.Stop();
                stepTimings.Add(("模板匹配", stepSw.ElapsedMilliseconds));

                // ===== Step 2.5: 生成调试中间图像（仅用于可视化，不影响匹配流程） =====
                if (grayImage != null && !grayImage.Empty() && enabledTemplates.Count > 0)
                {
                    CvOp.ComputeShapeGradient(grayImage, out var dbgGx, out var dbgGy, out var dbgMag);

                    // 取第一个启用模板的对比度参数做 NMS
                    float debugContrast = enabledTemplates.First().Contrast > 0
                        ? enabledTemplates.First().Contrast : 30f;
                    using var dbgNms = CvOp.ApplyNms(dbgMag, dbgGx, dbgGy, debugContrast);

                    result.DebugMagnitude = NormalizeForDisplay(dbgMag);
                    result.DebugEdgeMap = NormalizeForDisplay(dbgNms);

                    dbgGx.Dispose(); dbgGy.Dispose(); dbgMag.Dispose();
                }

                // ===== Step 4: NMS合并 =====
                stepSw.Restart();
                var nmsMatches = ApplyNms(allMatches, 10); // MinMatchDistance 已删除，使用默认值 10px
                stepSw.Stop();
                stepTimings.Add(("NMS合并", stepSw.ElapsedMilliseconds));

                // ===== Step 5: 结果排序 =====
                stepSw.Restart();
                var sortedMatches = SortResults(nmsMatches, parameters.SortMode.Value);
                stepSw.Stop();
                stepTimings.Add(("结果排序", stepSw.ElapsedMilliseconds));

                // ===== Step 6: 截取最大匹配数量 =====
                stepSw.Restart();
                var finalMatches = sortedMatches.Take(parameters.MaxMatchCount.Value).ToList();
                stepSw.Stop();
                stepTimings.Add(("截取数量", stepSw.ElapsedMilliseconds));

                // ===== Step 7: 设置结果 =====
                result.MatchedItems = finalMatches;

                // ★ 汇总日志：所有模板的最终匹配结果
                if (finalMatches.Count > 0)
                {
                    var resultSummary = new List<string>
                    {
                        $">> 最终匹配结果 (NMS后 {finalMatches.Count}/{allMatches.Count} 个)"
                    };
                    foreach (var item in finalMatches)
                    {
                        var template = enabledTemplates.FirstOrDefault(t => t.Id == item.TemplateId);
                        double dx = item.CenterX - (template?.ReferencePointX ?? 0);
                        double dy = item.CenterY - (template?.ReferencePointY ?? 0);
                        resultSummary.Add(
                            $"  [{item.TemplateName}] " +
                            $"Center=({item.CenterX:F4},{item.CenterY:F4}), " +
                            $"Ang={item.Angle:F2}°, Scale={item.Scale:F4}, " +
                            $"Score={item.Score:F4}, 偏差=({dx:F4},{dy:F4})px");
                    }
                    parameters.LogInfo(string.Join("\n", resultSummary));
                }

                // ===== Step 8: 结果判断 =====
                stepSw.Restart();
                EvaluateJudgments(parameters.ResultConfig, result);
                stepSw.Stop();
                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                // ===== Step 9: 构建显示项 =====
                stepSw.Restart();
                BuildDisplayItems(result, parameters, parameters.ResultConfig);
                stepSw.Stop();
                stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);

                // ===== 性能报告 =====
                var totalMs = stopwatch.ElapsedMilliseconds;
                var maxStep = stepTimings.MaxBy(t => t.Ms);
                var reportLines = new List<string>
                {
                    $"===== 轮廓匹配性能报告 =====",
                    $"图像: {image.Width}x{image.Height}  总耗时: {totalMs}ms",
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
                reportLines.Add($"结果: 匹配 {finalMatches.Count}/{allMatches.Count}, 瓶颈 [{maxStep.Step}]={maxStep.Ms}ms");
                parameters.LogInfo(string.Join("\n", reportLines));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"轮廓匹配处理异常: {ex.Message}", ex);
                result.SetError($"处理失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 对单个模板执行匹配 - 调用 CvOp.FindShape 算子进行实际形状匹配
        /// 使用模板的 EdgePoints（v3.0 梯度引擎统一）
        /// </summary>
        private static List<ShapeMatchResultItem> RunSingleTemplateMatch(
            Mat grayImage, TemplateModel template, ShapeMatchParameters parameters)
        {
            if (template.EdgePoints == null || template.EdgePoints.Count < 3)
                return new List<ShapeMatchResultItem>();

            // ===== 解析有效参数（支持每模型覆盖） =====
            double minScore = parameters.MinMatchScore.Value;
            double angleStart = parameters.AngleMin.Value;
            double angleEnd = parameters.AngleMax.Value;
            double angleStep = parameters.AngleStep.Value;
            double scaleMin = parameters.ScaleMin.Value;
            double scaleMax = parameters.ScaleMax.Value;
            double scaleStep = parameters.ScaleStep.Value;
            int timeoutMs = parameters.MatchTimeoutMs.Value;

            // ★ 从模板读取金字塔搜索范围（PyramidStart=粗层, PyramidEnd=细层）
            int maxLevel = template.PyramidEdgePoints?.Keys.Max() ?? 0;
            int pyramidStart = template.PyramidStart;   // 起始层（最粗）
            int pyramidEnd = template.PyramidEnd;       // 结束层（最细）

            // 约束: 起始(粗) ≥ 结束(细)
            if (pyramidStart < pyramidEnd)
                throw new InvalidOperationException(
                    $"模板 {template.Name}: 起始层(粗L{pyramidStart}) < 结束层(细L{pyramidEnd}) — 金字塔搜索范围无效");

            // 截断到可用最大层级
            pyramidStart = Math.Min(pyramidStart, maxLevel);
            pyramidEnd = Math.Min(pyramidEnd, pyramidStart);

            // 始终使用每层独立边缘点金字塔搜索
            var pyramidPoints = template.PyramidEdgePoints
                ?? throw new InvalidOperationException(
                    $"模板 {template.Name} 无金字塔数据 — 请重新提取模板");

            // 确保所有需要的层级都有数据
            for (int lvl = pyramidEnd; lvl <= pyramidStart; lvl++)
                if (!pyramidPoints.ContainsKey(lvl))
                    throw new InvalidOperationException(
                        $"金字塔 Level {lvl} 数据缺失 — 请重新提取模板");

            // 调用 CvEngine 形状匹配算子（Greediness + MaxOverlap NMS + NumMatches）
            var findSw = System.Diagnostics.Stopwatch.StartNew();

            // ★ 计算有效 MinContrast
            MinContrastMode effectiveMode = parameters.MinContrastMode.Value;
            float effectiveMinContrast = parameters.MinContrast.Value;
            if (effectiveMode == MinContrastMode.FromTemplate && template.Contrast > 0)
                effectiveMinContrast = template.Contrast;

            // ★ 读取 Greediness / MaxOverlap / NumMatches
            int numMatches = parameters.NumMatches?.Value ?? 1;
            double maxOverlap = parameters.MaxOverlap?.Value ?? 0.5;
            double greediness = parameters.Greediness?.Value ?? 0.9;

            CvOp.FindShape(
                grayImage, pyramidPoints,
                minScore, numMatches, maxOverlap, greediness,
                angleStart, angleEnd, angleStep,
                scaleMin, scaleMax, scaleStep,
                pyramidStart, pyramidEnd, timeoutMs,
                effectiveMode, effectiveMinContrast,
                out var matchResults,
                out var perfStats);
            findSw.Stop();

            // ===== ★ 汇总日志：每个模板输出一个完整监控块 ★ =====
            bool isTimeout = findSw.ElapsedMilliseconds > timeoutMs;
            int angleSteps = (int)((angleEnd - angleStart) / angleStep);
            int scaleSteps = (int)((scaleMax - scaleMin) / scaleStep);
            int totalCycles = angleSteps * scaleSteps;

            var summary = new List<string>
            {
                $"========== 轮廓匹配精度监控 [{template.Name}] ==========",
                $"【模板基准】参考点=({template.ReferencePointX:F4},{template.ReferencePointY:F4}), " +
                $"金字塔=LSB(1)~{pyramidPoints.Keys.Max()}层, L1={pyramidPoints[1].Count}pt(全分辨率)",
                $"【匹配参数】搜索=L{pyramidStart}(粗)~L{pyramidEnd}(细), " +
                $"角度=[{angleStart:F1}~{angleEnd:F1}]/{angleStep:F1}°, " +
                $"缩放=[{scaleMin:F3}~{scaleMax:F3}]/{scaleStep:F3}, " +
                $"timeout={timeoutMs}ms",
                $"【耗时】FindShape={findSw.ElapsedMilliseconds}ms, " +
                (isTimeout ? "⚠️超时 " : "") +
                $"候选数={matchResults.Count}"
            };

            // —— 坐标链路 ——
            if (perfStats.CoordinateChainLog.Count > 0)
            {
                summary.Add("【坐标链路】");
                summary.AddRange(perfStats.CoordinateChainLog);
            }

            // —— 结果精度 ——
            if (matchResults.Count > 0)
            {
                var bestMatch = matchResults.OrderByDescending(r => r.Score).First();
                double diffX = bestMatch.CenterX - template.ReferencePointX;
                double diffY = bestMatch.CenterY - template.ReferencePointY;
                summary.Add("【结果变换精度】" +
                    $"bestScore={bestMatch.Score:F4}, " +
                    $"Center=({bestMatch.CenterX:F4},{bestMatch.CenterY:F4}), " +
                    $"Angle={bestMatch.Angle:F2}°, Scale={bestMatch.Scale:F4}, " +
                    $"与模板参考点偏差=({diffX:F4},{diffY:F4})px");

                var finestLvl = perfStats.Levels.FirstOrDefault(l => l.FinalCenterX.HasValue);
                if (finestLvl != null && finestLvl.ScaleX > 0)
                {
                    double pixelStepOrig = finestLvl.ScaleX;
                    summary.Add($"  —— 精度上限: 最终搜索层步长={pixelStepOrig:F4}px(原图), " +
                        $"整数网格舍入误差上限=±{pixelStepOrig * 0.5:F4}px ——");
                }
            }
            else if (isTimeout)
            {
                summary.Add("—— ★ 匹配超时, pipeline 中断未到达最细层 ——");
                summary.Add($"  — 建议: 增大MatchTimeoutMs(当前={timeoutMs}ms), 缩小角度/缩放范围 —");
            }
            else
            {
                summary.Add("—— 匹配结果为 0（minScore过高或图像无匹配特征）——");
            }

            // —— 逐层性能 ——
            if (perfStats.Levels.Count > 0)
            {
                summary.Add("【逐层性能】" +
                    $"建图={perfStats.BuildPyramidMs}ms, 总={perfStats.TotalMs}ms");
                summary.Add($"  Lv  图像   点数  模式        候选  时间ms   内部耗时(准/全搜/回退/精搜)  角度×缩放   搜索像素    模板检查    提前终止  终止均点");
                summary.Add($"  ──  ────  ────  ─────────  ───  ─────  ──────────────────────────────  ─────────  ─────────  ─────────  ─────────  ───────");
                foreach (var ls in perfStats.Levels)
                {
                    string mode = ls.IsFullSearch ? "FullSearch" : "RefineSrch";
                    string innerTiming = ls.IsFullSearch
                        ? $"准={ls.PointPrepMs,3} 全搜={ls.FullSearchFirstMs,4}{(ls.FullSearchFallbackMs > 0 ? $"+回退{ls.FullSearchFallbackMs}" : "")}"
                        : $"准={ls.PointPrepMs,3} 精搜={ls.RefineSearchMs,4}";
                    string earlyInfo = ls.EarlyTerminations > 0
                        ? $"{ls.EarlyTerminations,8}  {ls.EarlyTerminationAvgPoint,5:F1}"
                        : $"{0,8}  {0,5:F1}";
                    summary.Add(
                        $"  {ls.Level,2}  " +
                        $"{ls.ImageW,4}×{ls.ImageH,-3}  " +
                        $"{ls.TemplatePointsUsed,4}  " +
                        $"{mode,-9}  " +
                        $"{ls.Candidates,3}  " +
                        $"{ls.SearchMs,5}  " +
                        $"{innerTiming,-32}  " +
                        $"{ls.AngleCombosTried,4}×{ls.ScaleCombosTried,-3}  " +
                        $"{ls.SpatialPixelsSearched,9}  " +
                        $"{ls.TotalTemplateChecks,9}  " +
                        earlyInfo);
                }
            }

            if (matchResults.Count == 0)
                parameters.LogWarning(string.Join("\n", summary));
            else
                parameters.LogInfo(string.Join("\n", summary));

            // ★ 使用预归一化的模板轮廓点（所有层级已统一归一化到参考点，坐标为相对参考点的偏移）
            var normalizedContour = pyramidPoints[1]
                .Select(p => new OpenCvSharp.Point2f((float)p.X, (float)p.Y))
                .ToList();

            // 将 CvEngine 结果映射为 ShapeMatchResultItem
            var resultItems = new List<ShapeMatchResultItem>(matchResults.Count);
            foreach (var match in matchResults)
            {
                double cosA = Math.Cos(match.Angle * Math.PI / 180.0);
                double sinA = Math.Sin(match.Angle * Math.PI / 180.0);

                // Scale 参与轮廓位置计算 — 匹配到的目标可能缩放，轮廓需等比变换
                var transformedContour = normalizedContour.Select(p =>
                {
                    float sx = (float)(p.X * match.Scale);
                    float sy = (float)(p.Y * match.Scale);
                    return new OpenCvSharp.Point2f(
                        (float)(sx * cosA - sy * sinA + match.CenterX),
                        (float)(sx * sinA + sy * cosA + match.CenterY));
                }).ToList();

                float minX = transformedContour.Min(p => p.X);
                float maxX = transformedContour.Max(p => p.X);
                float minY = transformedContour.Min(p => p.Y);
                float maxY = transformedContour.Max(p => p.Y);

                resultItems.Add(new ShapeMatchResultItem
                {
                    Score = match.Score,
                    MatchRatio = match.MatchRatio,
                    CenterX = match.CenterX,
                    CenterY = match.CenterY,
                    Angle = match.Angle,
                    Scale = match.Scale,
                    TemplateName = template.Name,
                    TemplateId = template.Id,
                    BoundingBox = new OpenCvSharp.Rect2d(minX, minY, maxX - minX, maxY - minY),
                    ContourPoints = transformedContour,
                    OriginalTemplateContour = normalizedContour,
                    ReferencePointX = template.ReferencePointX,
                    ReferencePointY = template.ReferencePointY
                });
            }

            return resultItems;
        }

        /// <summary>
        /// NMS非极大值抑制 - 合并邻近匹配结果
        /// </summary>
        private static List<ShapeMatchResultItem> ApplyNms(
            List<ShapeMatchResultItem> matches, int minDistance)
        {
            if (matches.Count <= 1 || minDistance <= 0)
                return matches;

            // 按得分降序排列
            var sorted = matches.OrderByDescending(m => m.Score).ToList();
            var result = new List<ShapeMatchResultItem>();

            foreach (var match in sorted)
            {
                bool isDuplicate = result.Any(r =>
                {
                    double dx = r.CenterX - match.CenterX;
                    double dy = r.CenterY - match.CenterY;
                    return Math.Sqrt(dx * dx + dy * dy) < minDistance;
                });

                if (!isDuplicate)
                    result.Add(match);
            }

            return result;
        }

        /// <summary>
        /// 对结果进行排序
        /// </summary>
        private static List<ShapeMatchResultItem> SortResults(
            List<ShapeMatchResultItem> matches, SortMode sortMode)
        {
            return sortMode switch
            {
                SortMode.ScoreDescending => matches.OrderByDescending(m => m.Score).ToList(),
                SortMode.AngleDescending => matches.OrderByDescending(m => m.Angle).ToList(),
                SortMode.XAscending => matches.OrderBy(m => m.CenterX).ToList(),
                SortMode.YAscending => matches.OrderBy(m => m.CenterY).ToList(),
                SortMode.XThenYAscending => matches.OrderBy(m => m.CenterX).ThenBy(m => m.CenterY).ToList(),
                SortMode.YThenXAscending => matches.OrderBy(m => m.CenterY).ThenBy(m => m.CenterX).ToList(),
                _ => matches // None = 保持原始顺序
            };
        }

        /// <summary>
        /// 评估结果判断（OK/NG判定）
        /// </summary>
        private static void EvaluateJudgments(
            Models.ShapeMatchResultConfig config,
            ShapeMatchResults result)
        {
            if (config == null) return;

            var messages = result.JudgmentMessages;
            messages.Clear();

            bool wholePassed = true;

            if (config.IsMatchCountCheckEnabled)
            {
                bool pass = result.MatchCount >= config.MatchCountMin &&
                            result.MatchCount <= config.MatchCountMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 匹配数量: {result.MatchCount} (范围 {config.MatchCountMin}~{config.MatchCountMax})");
                if (!pass) wholePassed = false;
            }

            if (config.IsMatchScoreCheckEnabled)
            {
                bool pass = result.BestScore >= config.MatchScoreMin &&
                            result.BestScore <= config.MatchScoreMax;
                string status = pass ? "OK" : "NG";
                messages.Add($"[{status}] 最佳匹配得分: {result.BestScore:F3} (范围 {config.MatchScoreMin:F2}~{config.MatchScoreMax:F2})");
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
        /// 构建显示项
        /// </summary>
        private static void BuildDisplayItems(ShapeMatchResults result, ShapeMatchParameters parameters, Models.ShapeMatchResultConfig config)
        {
            if (config == null) return;

            var display = result.Display;
            display.Clear();

            int matchCount = result.MatchCount;
            bool wholePassed = result.JudgmentPassed;

            // 匹配框
            foreach (var match in result.MatchedItems)
            {
                var bbox = match.BoundingBox;
                if (bbox.HasValue && bbox.Value.Width > 0 && bbox.Value.Height > 0)
                {
                    display.AddRect(config.MatchBoxStyle,
                        new System.Windows.Rect(bbox.Value.X, bbox.Value.Y, bbox.Value.Width, bbox.Value.Height),
                        match.JudgmentPassed);
                }
            }

            // 匹配中心 + 角度指示线
            foreach (var match in result.MatchedItems)
            {
                var center = new System.Windows.Point(match.CenterX, match.CenterY);
                display.AddCross(config.MatchCenterStyle, center, match.JudgmentPassed);

                // 角度指示线：用轮廓线段替代（DisplayCollector 无 AddLine 方法）
                double angleRad = match.Angle * Math.PI / 180.0;
                double lineLen = 20.0;
                var lineEnd = new System.Windows.Point(
                    match.CenterX + lineLen * Math.Cos(angleRad),
                    match.CenterY + lineLen * Math.Sin(angleRad));
                display.AddContour(config.AngleLineStyle,
                    new System.Windows.Point[] { center, lineEnd },
                    match.JudgmentPassed);
            }

            // 轮廓叠加（匹配到的轮廓 → 显示为点）
            foreach (var match in result.MatchedItems)
            {
                if (match.ContourPoints != null && match.ContourPoints.Count > 0)
                {
                    display.AddPoints(config.TemplateContourStyle,
                        match.ContourPoints.Select(p => new System.Windows.Point(p.X, p.Y)).ToList(),
                        Enumerable.Range(0, match.ContourPoints.Count)
                            .Select(_ => match.JudgmentPassed).ToArray());
                }
            }

            // 模板参考轮廓（原始模板轮廓还原到模板创建时的位置，与匹配轮廓叠加对比）
            foreach (var match in result.MatchedItems)
            {
                if (match.OriginalTemplateContour != null && match.OriginalTemplateContour.Count > 0)
                {
                    var refPoints = match.OriginalTemplateContour
                        .Select(p => new System.Windows.Point(
                            p.X + match.ReferencePointX,
                            p.Y + match.ReferencePointY))
                        .ToList();

                    // 诊断：参考轮廓与匹配轮廓的坐标范围对比
                    double refMinX = refPoints.Min(p => p.X), refMaxX = refPoints.Max(p => p.X);
                    double refMinY = refPoints.Min(p => p.Y), refMaxY = refPoints.Max(p => p.Y);
                    double matchMinX = match.ContourPoints?.Min(p => p.X) ?? 0, matchMaxX = match.ContourPoints?.Max(p => p.X) ?? 0;
                    double matchMinY = match.ContourPoints?.Min(p => p.Y) ?? 0, matchMaxY = match.ContourPoints?.Max(p => p.Y) ?? 0;
                    parameters.LogInfo(
                        $"[参考轮廓诊断] RefPoint=({match.ReferencePointX:F2},{match.ReferencePointY:F2}), " +
                        $"蓝色BBox=({refMinX:F1},{refMinY:F1})~({refMaxX:F1},{refMaxY:F1}), " +
                        $"绿色BBox=({matchMinX:F1},{matchMinY:F1})~({matchMaxX:F1},{matchMaxY:F1}), " +
                        $"中心差=({match.CenterX - match.ReferencePointX:F2},{match.CenterY - match.ReferencePointY:F2})px");

                    display.AddPoints(config.TemplateRefContourStyle, refPoints,
                        Enumerable.Range(0, match.OriginalTemplateContour.Count)
                            .Select(_ => match.JudgmentPassed).ToArray());
                }
            }

            // 文本标注
            foreach (var match in result.MatchedItems)
            {
                float baseY = (float)match.CenterY;
                float baseX = (float)(match.CenterX + 10);

                display.AddText(config.ScoreStyle, baseX, baseY,
                    $"Score={match.Score:F3}", wholePassed);

                display.AddText(config.PositionStyle, baseX, baseY + 14,
                    $"({match.CenterX:F1}, {match.CenterY:F1})", wholePassed);

                display.AddText(config.AngleStyle, baseX, baseY + 28,
                    $"Ang={match.Angle:F1}°", wholePassed);

                display.AddText(config.ScaleStyle, baseX, baseY + 42,
                    $"Scale={match.Scale:F3}", wholePassed);
            }
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        public ValidationResult ValidateParameters(ShapeMatchParameters parameters)
        {
            if (parameters == null)
                return ValidationResult.Failure("参数不能为空");
            return parameters.Validate();
        }

        /// <summary>
        /// 将图像归一化到 [0, 255] 范围并转为 CV_8U（用于 WPF 显示）
        /// </summary>
        private static Mat NormalizeForDisplay(Mat src)
        {
            var dst = new Mat();
            Cv2.Normalize(src, dst, 0, 255, NormTypes.MinMax);
            dst.ConvertTo(dst, MatType.CV_8U);
            return dst;
        }

        /// <summary>
        /// 创建调试控件
        /// </summary>
        public FrameworkElement? CreateDebugControl()
        {
            return new ShapeMatchToolDebugControl();
        }

        /// <summary>
        /// 创建数据分析控件
        /// </summary>
        public FrameworkElement? CreateDataAnalysisControl()
        {
            return null; // 暂不实现数据分析控件
        }
    }
}
