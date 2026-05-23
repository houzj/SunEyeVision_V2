using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ShapeMatch.Models;
using SunEyeVision.Tool.ShapeMatch.Views;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace SunEyeVision.Tool.ShapeMatch;

/// <summary>
/// 轮廓匹配工具 — 基于 Halcon 形状模板匹配
/// </summary>
[Tool(
    id: "shape-match",
    displayName: "轮廓匹配",
    Description = "基于形状的轮廓模板匹配 (Halcon)",
    Icon = "🔍",
    Category = "图像处理",
    Version = "2.0.0",
    HasDebugWindow = true
)]
public class ShapeMatchTool : IToolPlugin<ShapeMatchParameters, ShapeMatchResults>
{
    private readonly ToolAttribute? _attribute;

    private const double DEG2RAD = Math.PI / 180.0;

    public ShapeMatchTool()
    {
        _attribute = GetType().GetCustomAttribute<ToolAttribute>();
    }

    public ShapeMatchResults Run(ShapeMatchParameters parameters)
    {
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

            // ===== 获取 Halcon HImage =====
            stepSw.Restart();
            var hImage = parameters.GetHalconImage();
            HOperatorSet.GetImageSize(hImage, out var imgW, out var imgH);
            int width = (int)imgW.L;
            int height = (int)imgH.L;
            result.InputSize = new System.Drawing.Size(width, height);
            parameters.LogInfo($"开始轮廓匹配处理: 图像尺寸 {width}x{height}");
            stepSw.Stop();
            stepTimings.Add(("图像获取", stepSw.ElapsedMilliseconds));

            // ===== 灰度转换 =====
            stepSw.Restart();
            int channels = hImage.CountChannels();
            HImage grayImage;
            if (channels > 1)
            {
                HOperatorSet.Rgb1ToGray(hImage, out var grayObj);
                grayImage = new HImage(grayObj);
            }
            else
            {
                grayImage = hImage.Clone();
            }
            result.DebugGrayImage = grayImage.Clone();
            result.OutputImage = grayImage.Clone();
            stepSw.Stop();
            stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

            using (grayImage)
            {
                // ===== ROI 构建 & ReduceDomain =====
                stepSw.Restart();
                var inspectionRegions = parameters.GetRegionsByType(RegionType.Inspection);
                var maskRegions = parameters.GetRegionsByType(RegionType.Mask);

                HRegion? roiRegion = null;
                HRegion? inspHRegion = null;
                HRegion? maskHRegion = null;

                (roiRegion, inspHRegion, maskHRegion) = ToolRegionHelper.BuildHalconROI(
                    width, height, inspectionRegions, maskRegions);

                // 保存搜索区域掩码
                if (roiRegion != null)
                {
                    HOperatorSet.RegionToBin(roiRegion!, out var maskObj, 255, 0, width, height);
                    result.DebugSearchRegionMask = new HImage(maskObj);
                }
                stepSw.Stop();
                stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

                // ===== Step 2.5: 位置修正（正向变换 ROI） =====
                if (parameters.IsPositionCorrectionEnabled?.Value == true)
                {
                    stepSw.Restart();
                    var bindingSource = parameters.PositionCorrectionBindingSource;
                    if (bindingSource?.CurrentValue is AffineMatrix matrix)
                    {
                        parameters.LogInfo($"位置修正已启用, 矩阵: {matrix}");
                        (roiRegion, inspHRegion, maskHRegion) = ToolRegionHelper.ApplyPositionCorrectionHalcon(
                            roiRegion, inspHRegion, maskHRegion, matrix, width, height);

                        // 更新搜索区域掩码
                        if (result.DebugSearchRegionMask != null)
                        {
                            result.DebugSearchRegionMask.Dispose();
                        }
                        if (roiRegion != null)
                        {
                            HOperatorSet.RegionToBin(roiRegion!, out var maskObj, 255, 0, width, height);
                            result.DebugSearchRegionMask = new HImage(maskObj);
                        }
                    }
                    else
                    {
                        parameters.LogError("位置修正已启用但绑定源无有效矩阵，无法继续处理");
                        result.SetError("位置修正已启用但绑定源无有效矩阵");
                        return result;
                    }
                    stepSw.Stop();
                    stepTimings.Add(("位置修正", stepSw.ElapsedMilliseconds));
                }

                try
                {
                    HImage processingImage;
                    if (roiRegion != null)
                    {
                        HOperatorSet.ReduceDomain(grayImage, roiRegion, out var reducedObj);
                        processingImage = new HImage(reducedObj);
                    }
                    else
                    {
                        processingImage = grayImage.Clone();
                    }

                    using (processingImage)
                    {
                        // ===== 获取可用模板 =====
                        stepSw.Restart();
                        var enabledTemplates = parameters.Templates
                            .Where(t => t.IsEnabled && t.ExtractionStatus == TemplateExtractionStatus.Success && t.ModelData != null)
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

                        // ===== 对每个模板执行匹配 =====
                        stepSw.Restart();
                        var allMatches = new List<ShapeMatchResultItem>();

                        foreach (var template in enabledTemplates)
                        {
                            var templateMatches = RunSingleTemplateMatch(
                                processingImage, template, parameters, width, height);
                            allMatches.AddRange(templateMatches);
                        }
                        stepSw.Stop();
                        stepTimings.Add(("模板匹配", stepSw.ElapsedMilliseconds));

                        // ===== NMS 合并（Halcon 内部通过 MaxOverlap 处理，此处做二次合并） =====
                        stepSw.Restart();
                        var nmsMatches = ApplyNms(allMatches, 5);
                        stepSw.Stop();
                        stepTimings.Add(("NMS合并", stepSw.ElapsedMilliseconds));

                        // ===== 结果排序 =====
                        stepSw.Restart();
                        var sortedMatches = SortResults(nmsMatches, parameters.SortMode.Value);
                        stepSw.Stop();
                        stepTimings.Add(("结果排序", stepSw.ElapsedMilliseconds));

                        // ===== 截取最大匹配数量 =====
                        var finalMatches = sortedMatches.Take(parameters.MaxMatchCount.Value).ToList();

                        // ===== 设置结果 =====
                        result.MatchedItems = finalMatches;

                        // 汇总日志
                        if (finalMatches.Count > 0)
                        {
                            var resultSummary = new List<string>
                            {
                                $">> 最终匹配结果 (NMS后 {finalMatches.Count}/{allMatches.Count} 个)"
                            };
                            foreach (var item in finalMatches)
                            {
                                resultSummary.Add(
                                    $"  [{item.TemplateName}] " +
                                    $"Center=({item.CenterX:F4},{item.CenterY:F4}), " +
                                    $"Ang={item.Angle:F2}°, Scale={item.Scale:F4}, " +
                                    $"Score={item.Score:F4}");
                            }
                            parameters.LogInfo(string.Join("\n", resultSummary));
                        }
                        else
                        {
                            parameters.LogWarning($"未匹配到任何模板: 搜索了 {enabledTemplates.Count} 个启用模板, " +
                                $"MinScore={parameters.MinMatchScore.Value:F2}, NumMatches={parameters.NumMatches.Value}");
                        }

                        // ===== 结果判断 =====
                        stepSw.Restart();
                        EvaluateJudgments(parameters.ResultConfig, result);
                        stepSw.Stop();
                        stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                        // ===== 构建显示项 =====
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
                            $"===== 轮廓匹配性能报告 (Halcon) =====",
                            $"图像: {width}x{height}  总耗时: {totalMs}ms",
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
            parameters.LogError($"轮廓匹配处理异常: {ex.Message}", ex);
            result.SetError($"处理失败: {ex.Message}", ex);
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 对单个模板执行匹配 — 直接调用 Halcon find_scaled_shape_model
    /// </summary>
    private static List<ShapeMatchResultItem> RunSingleTemplateMatch(
        HImage image, TemplateModel template, ShapeMatchParameters parameters,
        int imgWidth, int imgHeight)
    {
        // 从 byte[] 反序列化模型（使用临时文件 + read_shape_model）
        string tempFile = System.IO.Path.GetTempFileName();
        HTuple modelHandle = new HTuple();
        try
        {
            System.IO.File.WriteAllBytes(tempFile, template.ModelData!);
            HOperatorSet.ReadShapeModel(tempFile, out modelHandle);
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
            parameters.LogWarning($"模板 {template.Name} 反序列化失败: {ex.Message}");
            return new List<ShapeMatchResultItem>();
        }

        if (System.IO.File.Exists(tempFile))
            System.IO.File.Delete(tempFile);

        try
        {
            // 解析搜索参数（支持每模型覆盖）
            double angleStart = parameters.AngleStart.Value;
            double angleEnd = parameters.AngleEnd.Value;
            double scaleMin = parameters.ScaleMin.Value;
            double scaleMax = parameters.ScaleMax.Value;
            double minScore = parameters.MinMatchScore.Value;
            int numMatches = parameters.NumMatches.Value;
            double maxOverlap = parameters.MaxOverlap.Value;
            double greediness = parameters.Greediness.Value;
            int timeoutMs = parameters.MatchTimeoutMs.Value;

            // 角度步长/缩放步长：0=auto
            HTuple angleStep = parameters.AngleStep.Value > 0
                ? parameters.AngleStep.Value * DEG2RAD
                : new HTuple("auto");
            HTuple scaleStep = parameters.ScaleStep.Value > 0
                ? parameters.ScaleStep.Value
                : new HTuple("auto");

            // 最小对比度：auto / float
            HTuple minContrast = parameters.IsMinContrastAuto.Value
                ? new HTuple("auto")
                : new HTuple(parameters.MinContrast.Value);

            // 搜索层级：使用 Parameters 中的搜索层级区间
            int searchLevelStart = parameters.SearchLevelStart.Value;
            int searchLevelEnd = parameters.SearchLevelEnd.Value;
            HTuple numLevelsTuple;
            if (searchLevelStart > 0 && searchLevelEnd > 0 && searchLevelStart >= searchLevelEnd)
            {
                int clampedStart = Math.Min(searchLevelStart, template.NumLevels);
                int clampedEnd = Math.Min(searchLevelEnd, template.NumLevels);
                numLevelsTuple = new HTuple(new int[] { clampedStart, clampedEnd });
            }
            else
            {
                numLevelsTuple = 0; // 全层级
            }

            // 设置超时
            HOperatorSet.SetShapeModelParam(modelHandle, "timeout", timeoutMs);

            // 计算 AngleExtent
            double angleExtent = angleEnd - angleStart;

            // 执行匹配
            HOperatorSet.FindScaledShapeModel(
                image, modelHandle,
                angleStart * DEG2RAD, angleExtent * DEG2RAD,
                scaleMin, scaleMax, minScore, numMatches, maxOverlap,
                MapSubPixel(parameters.SubPixelMode.Value),
                numLevelsTuple, greediness,
                out HTuple row, out HTuple col,
                out HTuple angle, out HTuple scale, out HTuple score);

            int matchCount = row.Length;
            if (matchCount == 0)
                return new List<ShapeMatchResultItem>();

            // 获取模型轮廓
            var modelContours = TemplateModel.GetShapeModelContours(modelHandle, 1);

            // 将轮廓打平为单层点列表（用于显示）
            var flatModelPoints = modelContours
                .SelectMany(chain => chain.Select(p => new WpfPoint(p.X, p.Y)))
                .ToList();

            var results = new List<ShapeMatchResultItem>(matchCount);
            for (int i = 0; i < matchCount; i++)
            {
                double matchRow = row[i].D;
                double matchCol = col[i].D;
                double matchAngle = angle[i].D;
                double matchScale = scale[i].D;
                double matchScore = score[i].D;

                // 构建仿射变换矩阵：M = scale * R(θ) + translation
                double cosA = Math.Cos(matchAngle);
                double sinA = Math.Sin(matchAngle);
                var transformMatrix = new Matrix(
                    matchScale * cosA,  matchScale * sinA,
                   -matchScale * sinA,  matchScale * cosA,
                    matchCol,           matchRow);

                // 计算包围矩形（应用变换矩阵到原始轮廓点）
                var bbox = ComputeBoundingBox(flatModelPoints, transformMatrix);

                results.Add(new ShapeMatchResultItem
                {
                    Score = matchScore,
                    CenterX = matchCol,
                    CenterY = matchRow,
                    Angle = matchAngle / DEG2RAD,   // 弧度→度
                    Scale = matchScale,
                    TemplateName = template.Name,
                    TemplateId = template.Id,
                    TemplateOrderIndex = template.OrderIndex,
                    BoundingBox = bbox,
                    ContourPoints = flatModelPoints,
                    ContourChains = modelContours,  // 保持链结构，渲染时逐链独立添加
                    TransformMatrix = transformMatrix,
                    ReferencePointX = template.ReferencePointX,
                    ReferencePointY = template.ReferencePointY,
                });
            }

            // 日志
            parameters.LogInfo(
                $"Halcon 匹配 [{template.Name}]: {matchCount}个 结果, " +
                $"角度=[{angleStart:F1}~{angleEnd:F1}]°, 缩放=[{scaleMin:F3}~{scaleMax:F3}]");

            return results;
        }
        finally
        {
            modelHandle.Dispose();
        }
    }

    /// <summary>
    /// 根据匹配轮廓点和变换矩阵计算包围矩形
    /// </summary>
    private static WpfRect ComputeBoundingBox(List<WpfPoint> points, Matrix transform)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            var tp = transform.Transform(p);
            if (tp.X < minX) minX = tp.X;
            if (tp.X > maxX) maxX = tp.X;
            if (tp.Y < minY) minY = tp.Y;
            if (tp.Y > maxY) maxY = tp.Y;
        }
        return new WpfRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// NMS 非极大值抑制 — 基于距离的中心点合并
    /// </summary>
    private static List<ShapeMatchResultItem> ApplyNms(
        List<ShapeMatchResultItem> matches, int minDistance)
    {
        if (matches.Count <= 1 || minDistance <= 0)
            return matches;

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
    /// 结果排序
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
            _ => matches
        };
    }

    /// <summary>
    /// 结果判断（OK/NG）
    /// </summary>
    private static void EvaluateJudgments(
        Models.ShapeMatchResultConfig config, ShapeMatchResults result)
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
    /// 构建显示项 — 使用 AddContour 替代 AddPoints
    /// </summary>
    private static void BuildDisplayItems(
        ShapeMatchResults result, ShapeMatchParameters parameters,
        Models.ShapeMatchResultConfig config)
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
                    bbox.Value, match.JudgmentPassed);
            }
        }

        // 匹配中心 + 角度指示线
        foreach (var match in result.MatchedItems)
        {
            var center = new WpfPoint(match.CenterX, match.CenterY);
            display.AddCross(config.MatchCenterStyle, center, match.JudgmentPassed);

            double angleRad = match.Angle * DEG2RAD;
            double lineLen = 20.0;
            var lineEnd = new WpfPoint(
                match.CenterX + lineLen * Math.Cos(angleRad),
                match.CenterY + lineLen * Math.Sin(angleRad));
            display.AddContour(config.AngleLineStyle,
                new[] { center, lineEnd }, match.JudgmentPassed);
        }

        // 匹配轮廓 → AddContour 折线（匹配到的轮廓，不封闭显示）
        // 每条轮廓链独立添加，避免链间多余连接线
        foreach (var match in result.MatchedItems)
        {
            if (match.ContourChains == null) continue;
            foreach (var chain in match.ContourChains)
            {
                if (chain.Length < 2) continue;
                display.AddContour(config.TemplateContourStyle,
                    chain, match.TransformMatrix, match.JudgmentPassed, false);
            }
        }

        // 文本标注（字符串数组 + 共享锚点自动换行）
        foreach (var match in result.MatchedItems)
        {
            float baseY = (float)match.CenterY;
            float baseX = (float)(match.CenterX + 10);

            display.AddText(config.ScoreStyle, baseX, baseY, new[]
            {
                $"{match.TemplateName}",
                $"S={match.Score:F3}",
                $"A={match.Angle:F3}°"
            }, wholePassed);
        }
    }


    #region Halcon 参数映射

    private static string MapSubPixel(SubPixelMode mode) => mode switch
    {
        Models.SubPixelMode.None => "none",
        Models.SubPixelMode.Interpolation => "interpolation",
        Models.SubPixelMode.LeastSquares => "least_squares",
        Models.SubPixelMode.LeastSquaresHigh => "least_squares_high",
        Models.SubPixelMode.LeastSquaresVeryHigh => "least_squares_very_high",
        _ => "least_squares"
    };

    #endregion

    #region 验证 & 调试

    public ValidationResult ValidateParameters(ShapeMatchParameters parameters)
    {
        if (parameters == null)
            return ValidationResult.Failure("参数不能为空");
        return parameters.Validate();
    }

    public FrameworkElement? CreateDebugControl()
    {
        return new ShapeMatchToolDebugControl();
    }

    public FrameworkElement? CreateDataAnalysisControl()
    {
        return null;
    }

    #endregion
}
