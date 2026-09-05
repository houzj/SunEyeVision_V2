using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.HalAdapter;
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
    id: "ShapeMatch",
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

    // ===== 模板模型句柄缓存 =====
    // ★ 修复：此前每次 Run 都写临时文件 + ReadShapeModel 重建句柄（磁盘 IO + 反序列化开销）。
    //   shape_model 句柄只依赖 ModelData（模板内容），不依赖搜索图像，进程内可安全复用。
    //   缓存键 = 模板 Id（同一模板重训后 ModelData 变化），再以内容 SHA256 校验是否需重建。
    //   同一 Id 重训时替换旧句柄并释放，避免缓存无界增长。
    //   Halcon 句柄非线程安全，用锁保护（Halcon 官方建议同一句柄勿并发访问）。
    private static readonly object _modelCacheLock = new();
    private static readonly Dictionary<string, (string ContentKey, HTuple Handle)> _modelCache = new();

    /// <summary>取得模板句柄（命中缓存且内容未变则复用，否则读模型并缓存）</summary>
    private static HTuple GetOrLoadModelHandle(TemplateModel template)
    {
        var contentKey = ComputeContentKey(template.ModelData);
        lock (_modelCacheLock)
        {
            if (_modelCache.TryGetValue(template.Id, out var cached))
            {
                if (cached.ContentKey == contentKey)
                    return cached.Handle;

                // 模板被重训：替换并释放旧句柄
                cached.Handle.Dispose();
                _modelCache.Remove(template.Id);
            }

            var handle = ReadModelHandle(template);
            _modelCache[template.Id] = (contentKey, handle);
            return handle;
        }
    }

    private static string ComputeContentKey(byte[]? modelData)
    {
        if (modelData == null || modelData.Length == 0)
            throw new InvalidOperationException("模板 ModelData 为空，无法创建模型句柄");

        // SHA256(ModelData) → 内容指纹
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(modelData);
        return Convert.ToHexString(hash);
    }

    private static HTuple ReadModelHandle(TemplateModel template)
    {
        // 临时文件是 halcon read_shape_model 的官方输入方式；模型数据来自序列化的 ModelData
        string tempFile = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllBytes(tempFile, template.ModelData!);
            HOperatorSet.ReadShapeModel(tempFile, out var handle);
            return handle;
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    public ShapeMatchTool()
    {
        _attribute = GetType().GetCustomAttribute<ToolAttribute>();
    }

    public ShapeMatchResults Run(ShapeMatchParameters parameters)
    {
        var result = new ShapeMatchResults
        {
            ToolName = _attribute?.DisplayName ?? "ShapeMatch",
            ToolId = _attribute?.Id ?? "ShapeMatch",
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
            result.GrayImage = new HalImage(grayImage.Clone());
            // OutputImage 在 ReduceDomain 后赋值
            stepSw.Stop();
            stepTimings.Add(("灰度转换", stepSw.ElapsedMilliseconds));

            using (grayImage)
            {
                // ===== ROI 构建 & ReduceDomain =====
                stepSw.Restart();
                var inspectionRegions = parameters.GetRoisByType(RoiType.Inspection);
                var maskRegions = parameters.GetRoisByType(RoiType.Mask);

                HRegion? roiRegion = null;
                HRegion? inspHRegion = null;
                HRegion? maskHRegion = null;

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
                        parameters.LogInfo("位置修正已启用，但绑定源当前无有效矩阵，跳过位置修正");
                    }
                    else
                    {
                        parameters.LogWarning("位置修正已启用但未配置绑定源，跳过位置修正");
                    }
                }

                // 位置修正按形状有向性并入 ROI 构建（无向剥转 / 有向完整，对齐 UI 语义）
                (roiRegion, inspHRegion, maskHRegion) = ToolRegionHelper.BuildHalconROI(
                    width, height, inspectionRegions, maskRegions,
                    parameters.NodeResults, correctionMatrix);

                // 保存搜索区域掩码（已含位置修正结果）
                if (roiRegion != null)
                {
                    HOperatorSet.RegionToBin(roiRegion!, out var maskObj, 255, 0, width, height);
                    result.SearchRegionMask = new HalImage(new HImage(maskObj));
                }
                stepSw.Stop();
                stepTimings.Add(("ROI构建", stepSw.ElapsedMilliseconds));

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

                    // 保存预处理后的图像作为输出图像
                    result.OutputImage = new HalImage(processingImage.Clone());

                    using (processingImage)
                    {
                        // ===== 获取可用模板 =====
                        stepSw.Restart();
                        var enabledTemplates = parameters.Templates
                            .Where(t => t.IsEnabled && t.ExtractionStatus == TemplateExtractionStatus.Success && t.ModelData != null)
                            .ToList();

                        if (enabledTemplates.Count == 0)
                        {
                            // ★ M9：无可用模板不再静默早退 —— 清空匹配结果后走下方统一判定，
                            //   判定消息口径与"模板存在但零匹配"一致（空结果统一强制 NG，见结果判断后）。
                            parameters.LogWarning("没有可用的模板（没有已提取成功的启用模板），按零匹配结果处理");
                            result.MatchedItems = new List<ShapeMatchResultItem>();
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
                                processingImage, template, parameters);
                            allMatches.AddRange(templateMatches);
                        }
                        stepSw.Stop();
                        stepTimings.Add(("模板匹配", stepSw.ElapsedMilliseconds));

                        // ===== 结果排序 =====
                        stepSw.Restart();
                        var sortedMatches = SortResults(allMatches, parameters.SortMode.Value);
                        stepSw.Stop();
                        stepTimings.Add(("结果排序", stepSw.ElapsedMilliseconds));

                        // ===== 跨模板空间去重 NMS（M8/M10） =====
                        // Halcon MaxOverlap 只在单模板内生效；多模板匹配同一目标时会输出重叠重复框，
                        // 需按空间重叠做一次跨模板抑制，再截取 MaxMatchCount。
                        stepSw.Restart();
                        var nonOverlapMatches = SuppressCrossTemplateOverlap(
                            sortedMatches, parameters.MaxOverlap.Value);
                        stepSw.Stop();
                        stepTimings.Add(("跨模板去重", stepSw.ElapsedMilliseconds));

                        // ===== 截取最大匹配数量 =====
                        var finalMatches = nonOverlapMatches.Take(parameters.MaxMatchCount.Value).ToList();

                        // ===== 设置结果 =====
                        result.MatchedItems = finalMatches;

                        // 汇总日志
                        if (finalMatches.Count > 0)
                        {
                            var resultSummary = new List<string>
                            {
                                $">> 最终匹配结果 ({finalMatches.Count}/{allMatches.Count} 个)"
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
                        // ★ M9：空结果强制 NG（无模板 / 模板存在但零匹配 共用此兜底），
                        //   避免"匹配数量/分数判断均未启用"时空结果被误判为通过。
                        if (result.MatchCount == 0)
                        {
                            result.JudgmentPassed = false;
                            if (!result.JudgmentMessages.Any(m => m.Contains("无模板")))
                                result.JudgmentMessages.Add("未找到任何匹配（数量=0）");
                        }
                        stepSw.Stop();
                        stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                        // ===== 构建显示项 =====
                        stepSw.Restart();
                        BuildDisplayItems(result, parameters, parameters.ResultConfig);
                        stepSw.Stop();
                        stepTimings.Add(("显示构建", stepSw.ElapsedMilliseconds));

                        // 提取检测区/屏蔽区几何输出（RoI 契约：结果树字段聚合 + 下游绑定）
                        ToolRegionHelper.ExtractInspectionGeometries(result, parameters.InspectionRegions?.Regions);
                        ToolRegionHelper.ExtractMaskGeometries(result, parameters.MaskRegions?.Regions);

                        stopwatch.Stop();
                        result.SetSuccess(stopwatch.Elapsed.TotalMilliseconds);

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
            result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 对单个模板执行匹配 — 直接调用 Halcon find_scaled_shape_model
    /// </summary>
    private static List<ShapeMatchResultItem> RunSingleTemplateMatch(
        HImage image, TemplateModel template, ShapeMatchParameters parameters)
    {
        // 从缓存取得模型句柄（首次按 ModelData 重建，之后复用；句柄生命周期由缓存管理，此处不 Dispose）
        HTuple modelHandle;
        try
        {
            modelHandle = GetOrLoadModelHandle(template);
        }
        catch (Exception ex)
        {
            parameters.LogError($"模板 {template.Name} 模型加载失败: {ex.Message}");
            throw;
        }

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

        // ★ M12/M13：删除 angleStep/scaleStep/minContrast 死代码 —— find_scaled_shape_model
        //   （本机官方文档签名）不含这些参数，它们是 create_scaled_shape_model 的创建期参数，
        //   此前在搜索路径计算了却从未传入 find 调用，属无效代码。

        // 搜索层级：使用 Parameters 中的搜索层级区间 [起始层(粗), 结束层(细)]
        // Halcon 官方语义：NumLevels=[4,2] = 从第 4 层开始、跟踪匹配到第 2 层（起始层≥结束层）。
        // 0 或未成对时使用 0 = 全层级（用 create 时的层数）。
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

        // 获取模型轮廓（模型坐标，Level 1）
        var modelContours = TemplateModel.GetShapeModelContours(modelHandle, 1);

        var results = new List<ShapeMatchResultItem>(matchCount);
        for (int i = 0; i < matchCount; i++)
        {
            double matchRow = row[i].D;
            double matchCol = col[i].D;
            double matchAngle = angle[i].D;
            double matchScale = scale[i].D;
            double matchScore = score[i].D;

            // ★ 唯一构造出口：由独立工厂从 Halcon 匹配姿态构造 AffineMatrix（仿 HalconArcAdapter 范式）
            var transformMatrix = ShapeMatchTransformFactory.FromMatchPose(
                matchRow, matchCol, matchAngle, matchScale);

            // 轮廓变换到图像坐标（M5：渲染不再依赖矩阵变换，Display 直接画像素点）
            var imageChains = new List<WpfPoint[]>(modelContours.Count);
            foreach (var chain in modelContours)
            {
                var mapped = new WpfPoint[chain.Length];
                for (int j = 0; j < chain.Length; j++)
                {
                    var (mx, my) = transformMatrix.TransformPoint(chain[j].X, chain[j].Y);
                    mapped[j] = new WpfPoint(mx, my);
                }
                imageChains.Add(mapped);
            }
            var flatImagePoints = imageChains.SelectMany(c => c).ToList();

            // 包围矩形：由已变换图像坐标直接求取（模型坐标无需再次经矩阵计算）
            var bbox = ComputeBoundingBoxFromPoints(flatImagePoints);

            results.Add(new ShapeMatchResultItem
            {
                Score = matchScore,
                CenterX = matchCol,
                CenterY = matchRow,
                Angle = -matchAngle / DEG2RAD,   // Halcon ccw+ → 屏幕角度系 cw+（方向契约测试 AC-2 定案）
                Scale = matchScale,
                TemplateName = template.Name,
                TemplateId = template.Id,
                TemplateOrderIndex = template.OrderIndex,
                BoundingBox = bbox,
                ContourPoints = flatImagePoints,   // 已变换图像坐标（供下游消费/展示）
                ContourChains = imageChains,       // 已变换图像坐标，渲染时逐链独立添加（不再经矩阵）
                TransformMatrix = transformMatrix, // 保留：仅作下游数值输出/位置修正（AC-3）
                ReferencePointX = template.ReferencePointX,
                ReferencePointY = template.ReferencePointY,
            });
        }

        // ★ 方向一致性诊断日志：矩阵旋转角 vs Angle 字段（AC-2 方向契约测试的人工观测口）
        LogDirectionConsistency(parameters, template, results);

        // 日志
        parameters.LogInfo(
            $"Halcon 匹配 [{template.Name}]: {matchCount}个 结果, " +
            $"角度=[{angleStart:F1}~{angleEnd:F1}]°, 缩放=[{scaleMin:F3}~{scaleMax:F3}]");

        return results;
    }

    /// <summary>
    /// 根据（已变换到图像坐标的）匹配轮廓点直接求包围矩形
    /// </summary>
    private static WpfRect ComputeBoundingBoxFromPoints(List<WpfPoint> points)
    {
        if (points.Count == 0) return WpfRect.Empty;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in points)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new WpfRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 方向一致性诊断日志：输出每个匹配的矩阵旋转角（atan2(M21,M11)）与 Angle 字段，
    /// 供"轮廓渲染方向 == 角度线方向 == Angle 文本"实测核对（方案 AC-2 方向契约）。
    /// </summary>
    private static void LogDirectionConsistency(
        ShapeMatchParameters parameters, TemplateModel template, List<ShapeMatchResultItem> results)
    {
        try
        {
            foreach (var m in results)
            {
                double matrixAngleDeg = Math.Atan2(m.TransformMatrix.M21, m.TransformMatrix.M11) / DEG2RAD;
                parameters.LogInfo(
                    $"[Diag] 方向一致性 [{template.Name}] Center=({m.CenterX:F2},{m.CenterY:F2}) " +
                    $"矩阵角(atan2(M21,M11))={matrixAngleDeg:F2}° | Angle字段={m.Angle:F2}° | " +
                    $"Halcon原角={m.Angle * -1.0:F2}° | Scale={m.Scale:F4}");
            }
        }
        catch (Exception ex)
        {
            parameters.LogInfo($"[Diag] 方向一致性日志异常: {ex.Message}");
        }
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
    /// 跨模板空间去重 NMS（M10）：抑制与已保留框重叠超过 MaxOverlap 的后续匹配。
    /// 输入应为已排序列表（建议按分数降序），第一个匹配总是保留。
    /// </summary>
    private static List<ShapeMatchResultItem> SuppressCrossTemplateOverlap(
        List<ShapeMatchResultItem> sortedMatches, double maxOverlap)
    {
        if (sortedMatches.Count <= 1) return sortedMatches;

        var kept = new List<ShapeMatchResultItem>();
        foreach (var candidate in sortedMatches)
        {
            if (!candidate.BoundingBox.HasValue)
            {
                kept.Add(candidate); // 无包围盒无法判重叠，保留
                continue;
            }

            bool overlapped = kept.Any(k =>
                k.BoundingBox.HasValue && BoundingBoxOverlapRatio(
                    candidate.BoundingBox.Value, k.BoundingBox.Value) > maxOverlap);
            if (!overlapped)
                kept.Add(candidate);
        }
        return kept;
    }

    /// <summary>
    /// 两个矩形交并比（IoU）：交集面积 / 并集面积；不相交返回 0。
    /// </summary>
    private static double BoundingBoxOverlapRatio(WpfRect a, WpfRect b)
    {
        var ia = WpfRect.Empty;
        ia.X = Math.Max(a.X, b.X);
        ia.Y = Math.Max(a.Y, b.Y);
        ia.Width = Math.Min(a.X + a.Width, b.X + b.Width) - ia.X;
        ia.Height = Math.Min(a.Y + a.Height, b.Y + b.Height) - ia.Y;
        if (ia.Width <= 0 || ia.Height <= 0) return 0;

        double intersection = ia.Width * ia.Height;
        double union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union > 0 ? intersection / union : 0;
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

        // ★ M7：逐项赋值 JudgmentPassed —— 此前 item.JudgmentPassed 从未赋值（恒 true），
        //   整体 NG（数量不足/分数越界）时显示层仍把框/中心画成 OK 色，视觉误导。
        //   整体判定 NG 则所有匹配项置 false（显示 NG 色）；整体 OK 保持各项默认 true。
        if (result.MatchedItems != null)
        {
            foreach (var item in result.MatchedItems)
            {
                item.JudgmentPassed = wholePassed;
            }
        }

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
        // ★ M5：ContourChains 已由工具层变换为图像坐标，Display 直接画点，不再做矩阵变换（WPF 不参与几何计算）
        foreach (var match in result.MatchedItems)
        {
            if (match.ContourChains == null) continue;
            foreach (var chain in match.ContourChains)
            {
                if (chain.Length < 2) continue;
                display.AddContour(config.TemplateContourStyle,
                    chain, match.JudgmentPassed, false);
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

    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
    {
        return new ShapeMatchToolDebugControl("ShapeMatch", tool, null);
    }

    public FrameworkElement? CreateDataAnalysisControl()
    {
        return new ShapeMatchDataAnalysisControl();
    }

    #endregion
}
