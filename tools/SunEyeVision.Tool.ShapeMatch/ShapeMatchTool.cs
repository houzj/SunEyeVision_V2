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
                    .Where(t => t.IsEnabled && t.ExtractionStatus == TemplateExtractionStatus.Success && t.ContourGroups != null)
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

                // ===== Step 4: NMS合并 =====
                stepSw.Restart();
                var nmsMatches = ApplyNms(allMatches, parameters.MinMatchDistance.Value);
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

                // ===== Step 8: 结果判断 =====
                stepSw.Restart();
                EvaluateJudgments(parameters.ResultConfig, result);
                stepSw.Stop();
                stepTimings.Add(("结果判断", stepSw.ElapsedMilliseconds));

                // ===== Step 9: 构建显示项 =====
                stepSw.Restart();
                BuildDisplayItems(result, parameters.ResultConfig);
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
        /// </summary>
        private static List<ShapeMatchResultItem> RunSingleTemplateMatch(
            Mat grayImage, TemplateModel template, ShapeMatchParameters parameters)
        {
            if (template.ContourGroups == null || template.ContourGroups.Count < 1)
                return new List<ShapeMatchResultItem>();

            // 将所有轮廓组拍平为单个点列表用于匹配
            var validPoints = template.ContourGroups.SelectMany(g => g).ToList();
            if (validPoints.Count < 3)
                return new List<ShapeMatchResultItem>();

            // 将 Point2D 转换为 OpenCV Point2f
            var contourPoints = validPoints
                .Select(p => new OpenCvSharp.Point2f((float)p.X, (float)p.Y))
                .ToList();

            // 解析有效参数（支持每模型覆盖）
            double minScore = parameters.MinMatchScore.Value;
            double angleStart = parameters.AngleMin.Value;
            double angleEnd = parameters.AngleMax.Value;
            double angleStep = parameters.AngleStep.Value;
            double scaleMin = parameters.ScaleMin.Value;
            double scaleMax = parameters.ScaleMax.Value;
            double scaleStep = parameters.ScaleStep.Value;
            int pyramidLevels = parameters.PyramidLevels.Value;
            int timeoutMs = parameters.MatchTimeoutMs.Value;
            int speedScale = parameters.SpeedScale.Value;

            // 调用 CvEngine 形状匹配算子
            var matchResults = CvOp.FindShape(
                grayImage, contourPoints,
                minScore, angleStart, angleEnd, angleStep,
                scaleMin, scaleMax, scaleStep,
                pyramidLevels, timeoutMs, speedScale);

            // 将 CvEngine 结果映射为 ShapeMatchResultItem 列表
            var resultItems = new List<ShapeMatchResultItem>(matchResults.Count);
            foreach (var match in matchResults)
            {
                // 根据匹配位置和缩放变换模板轮廓点
                double cosA = Math.Cos(match.Angle * Math.PI / 180.0);
                double sinA = Math.Sin(match.Angle * Math.PI / 180.0);

                var transformedContour = contourPoints.Select(p =>
                {
                    // 相对模板中心的坐标
                    float rx = (float)(p.X - template.CenterX) * (float)match.Scale;
                    float ry = (float)(p.Y - template.CenterY) * (float)match.Scale;
                    // 旋转 + 平移到匹配位置
                    return new OpenCvSharp.Point2f(
                        (float)(rx * cosA - ry * sinA + match.CenterX),
                        (float)(rx * sinA + ry * cosA + match.CenterY));
                }).ToList();

                // 计算变换后的包围矩形
                float minX = transformedContour.Min(p => p.X);
                float maxX = transformedContour.Max(p => p.X);
                float minY = transformedContour.Min(p => p.Y);
                float maxY = transformedContour.Max(p => p.Y);

                resultItems.Add(new ShapeMatchResultItem
                {
                    Score = match.Score,
                    CenterX = match.CenterX,
                    CenterY = match.CenterY,
                    Angle = match.Angle,
                    Scale = match.Scale,
                    TemplateName = template.Name,
                    TemplateId = template.Id,
                    BoundingBox = new OpenCvSharp.Rect2d(minX, minY, maxX - minX, maxY - minY),
                    ContourPoints = transformedContour
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
        private static void BuildDisplayItems(ShapeMatchResults result, Models.ShapeMatchResultConfig config)
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
                        new OpenCvSharp.Rect2d(bbox.Value.X, bbox.Value.Y, bbox.Value.Width, bbox.Value.Height),
                        match.JudgmentPassed);
                }
            }

            // 匹配中心 + 角度指示线
            foreach (var match in result.MatchedItems)
            {
                var center = new OpenCvSharp.Point2d(match.CenterX, match.CenterY);
                display.AddCross(config.MatchCenterStyle, center, match.JudgmentPassed);

                // 角度指示线：用轮廓线段替代（DisplayCollector 无 AddLine 方法）
                double angleRad = match.Angle * Math.PI / 180.0;
                double lineLen = 20.0;
                var lineEnd = new OpenCvSharp.Point2d(
                    match.CenterX + lineLen * Math.Cos(angleRad),
                    match.CenterY + lineLen * Math.Sin(angleRad));
                display.AddContour(config.AngleLineStyle,
                    new OpenCvSharp.Point[] { new OpenCvSharp.Point((int)center.X, (int)center.Y), new OpenCvSharp.Point((int)lineEnd.X, (int)lineEnd.Y) },
                    match.JudgmentPassed);
            }

            // 轮廓叠加
            foreach (var match in result.MatchedItems)
            {
                if (match.ContourPoints != null && match.ContourPoints.Count > 0)
                {
                    display.AddContour(config.TemplateContourStyle,
                        match.ContourPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray(),
                        match.JudgmentPassed);
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
