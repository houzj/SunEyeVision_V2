using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.MarkFind.Runtime;

/// <summary>
/// 一次同步执行：图像 → 搜索区域（含位置修正）→ 形状模板匹配 → 结果行/几何 → 输出。
/// 工作流调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class MarkFindSession : IToolSession<MarkFindSettings, MarkFindOutput>, IReusableToolSession
{
    private const double Greediness = 0.9;
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<MarkFindOutput>> ExecuteAsync(ToolInvocation<MarkFindSettings> invocation)
    {
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(invocation);
            var output = Execute(invocation);
            return ValueTask.FromResult(new ToolOutcome<MarkFindOutput>(true, output,
                output.IsPass ? BusinessJudgment.Pass : BusinessJudgment.Fail, Array.Empty<Diagnostic>()));
        }
        catch (MarkFindCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<MarkFindOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<MarkFindOutput>.Failed("MARKFIND_CANCELLED", "标记查找已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<MarkFindOutput>.Failed("MARKFIND_EXECUTION_FAILED", error.Message));
        }
    }

    private sealed record Candidate(long ElementId, double CenterX, double CenterY, double AngleDegrees, double Score,
        HRegion Region, MarkFindBox Box, IReadOnlyList<Point2> Outline, bool Passed, IReadOnlyList<string> FailedLimits);

    private static MarkFindOutput Execute(ToolInvocation<MarkFindSettings> invocation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new MarkFindCapabilityException("MARKFIND_SETTINGS_INVALID", string.Join("；", errors.Select(error => error.Message)));
        var template = MarkFindTemplate.Decode(settings.ModelData);
        var cancellation = invocation.Context.Cancellation;
        var inputs = invocation.Inputs;
        var image = inputs.Get<IReadOnlyImage>(MarkFindSchema.ImageInput)
            ?? throw new MarkFindCapabilityException("MARKFIND_IMAGE_REQUIRED", "缺少输入图像。");
        var roi = OptionalRegion(inputs, MarkFindSchema.RoiInput)
            ?? throw new MarkFindCapabilityException("MARKFIND_ROI_REQUIRED", "请为搜索区域连接区域输入（原件必须框选搜索区域）。");
        var fixture = OptionalFixture(inputs);

        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        using var widthOwner = widthTuple;
        using var heightOwner = heightTuple;
        var width = widthTuple.I;
        var height = heightTuple.I;
        using var gray = HalconImageAdmission.Gray(native);
        using var domain = gray.GetDomain();
        var frame = image.Frame;

        using var roiRegion = HalconResourceBridge.CopyRegion(roi, frame, width, height);
        using var posedRoi = fixture is null ? roiRegion.Clone() : HalconResourceBridge.TransformRegion(roiRegion, FixtureDeclarations.ToAffine2D(fixture, MarkFindSchema.ImageFrame));
        using var search = domain.Intersection(posedRoi);
        RequireSearchArea(search);

        using var reduced = gray.ReduceDomain(search);
        var modelId = template.LoadModel();
        var outputResources = invocation.Context.Resources.CreateScope();
        try
        {
            var matches = Find(reduced, modelId, template, settings, cancellation);
            var judged = Judge(settings, matches);
            try
            {
                var measurements = new List<MarkFindMeasurement>(judged.Items.Length);
                foreach (var candidate in judged.Items)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var captured = HalconResourceBridge.CaptureRegion(candidate.Region, MarkFindSchema.ImageFrame, frame, outputResources);
                    measurements.Add(new(candidate.ElementId, candidate.CenterX, candidate.CenterY, candidate.AngleDegrees,
                        candidate.Score, candidate.Box, candidate.Outline, captured, candidate.Passed, candidate.FailedLimits));
                }
                var modulePassed = judged.Overall && measurements.Count > 0 && measurements.All(item => item.Passed);
                var projection = MarkFindResultProjection.Create(measurements, modulePassed,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds, fixture);
                var geometry = MarkFindResultGeometry.Capture(image, modulePassed, search, fixture, cancellation);
                var output = new MarkFindOutput(settings, projection, measurements.AsReadOnly(),
                    judged.Messages, geometry, new IDisposable[] { outputResources });
                invocation.Context.Log($"[Perf] tool=MarkFind count={projection.Count} elapsed={projection.ModuleRunTime:0.###}ms");
                return output;
            }
            finally
            {
                foreach (var candidate in judged.Items) candidate.Region.Dispose();
            }
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
        finally
        {
            MarkFindTemplate.Clear(modelId);
        }
    }

    private static IReadOnlyRegion? OptionalRegion(InputValues values, string id)
        => values.Contains(id) ? values.Get<IReadOnlyRegion?>(id) : null;

    private static RecordValue? OptionalFixture(InputValues values)
        => values.Contains(MarkFindSchema.FixtureInput) ? values.Get<RecordValue?>(MarkFindSchema.FixtureInput) : null;

    private static void RequireSearchArea(HRegion search)
    {
        HOperatorSet.AreaCenter(search, out var area, out var row, out var column);
        using (area) using (row) using (column)
            if (area.Length == 0 || area.LArr.All(value => value == 0))
                throw new MarkFindCapabilityException("MARKFIND_ROI_EMPTY", "搜索区域与图像有效域没有交集：请检查区域与位置修正。");
    }

    /// <summary>形状模板匹配；返回按设置排序后的候选（仍未判定）。</summary>
    private static Candidate[] Find(HImage reduced, HTuple modelId, MarkFindTemplate template, MarkFindSettings settings,
        CancellationToken cancellation)
    {
        using var angleStart = new HTuple(settings.AngleStart * Math.PI / 180d);
        using var angleExtent = new HTuple(Math.Max(0.0001, (settings.AngleEnd - settings.AngleStart) * Math.PI / 180d));
        using var minScore = new HTuple(settings.MinScore);
        using var numMatches = new HTuple(settings.MaxMatchCount);
        using var maxOverlap = new HTuple(Math.Clamp(settings.MaxOverlap, 0, 100) / 100d);
        using var subPixel = new HTuple("least_squares");
        using var numLevels = new HTuple(settings.PyramidScaleMode == MarkFindPyramidMode.Manual
            ? Math.Clamp(settings.FinePyramidScale, 0, Math.Max(1, template.LevelCount))
            : 0);
        using var greediness = new HTuple(Greediness);

        HOperatorSet.FindShapeModel(reduced, modelId, angleStart, angleExtent, minScore, numMatches, maxOverlap,
            subPixel, numLevels, greediness, out var rows, out var columns, out var angles, out var scores);
        using (rows) using (columns) using (angles) using (scores)
        {
            if (rows.Length != columns.Length || rows.Length != angles.Length || rows.Length != scores.Length)
                throw new MarkFindCapabilityException("MARKFIND_NATIVE_RESULT", "HALCON 返回的匹配元组长度不一致。");
            if (rows.Length == 0) return Array.Empty<Candidate>();
            cancellation.ThrowIfCancellationRequested();
            using var modelContours = ModelContours(modelId);
            var candidates = new List<Candidate>(rows.Length);
            try
            {
                for (var index = 0; index < rows.Length; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using var homMat = HomMat(rows[index].D, columns[index].D, angles[index].D);
                    HOperatorSet.AffineTransContourXld(modelContours, out var moved, homMat);
                    using (moved)
                    {
                        HOperatorSet.SmallestRectangle2Xld(moved, out var boxRow, out var boxColumn, out var boxPhi, out var length1, out var length2);
                        using (boxRow) using (boxColumn) using (boxPhi) using (length1) using (length2)
                        {
                            if (length1.D <= 0 || length2.D <= 0)
                                throw new MarkFindCapabilityException("MARKFIND_NATIVE_RESULT", "匹配结果外接矩形退化。");
                            HOperatorSet.GenRectangle2(out var boxRegion, boxRow, boxColumn, boxPhi, length1, length2);
                            using (boxRegion)
                            {
                                var outline = settings.OutlineEnabled ? ReadPoints(moved, cancellation) : Array.Empty<Point2>();
                                var box = new MarkFindBox(boxColumn.D, boxRow.D, length1.D * 2, length2.D * 2,
                                    NormalizeDegrees(-boxPhi.D * 180d / Math.PI));
                                candidates.Add(new(index + 1, boxColumn.D, boxRow.D, NormalizeDegrees(angles[index].D * 180d / Math.PI),
                                    scores[index].D, new HRegion(boxRegion), box, outline, true, Array.Empty<string>()));
                            }
                        }
                    }
                }
            }
            catch
            {
                foreach (var candidate in candidates) candidate.Region.Dispose();
                throw;
            }
            return Order(candidates.ToArray(), settings);
        }
    }

    private static HObject ModelContours(HTuple modelId)
    {
        using var level = new HTuple(1);
        HOperatorSet.GetShapeModelContours(out var contours, modelId, level);
        if (contours is null) throw new MarkFindCapabilityException("MARKFIND_TEMPLATE_EMPTY", "模板没有可用轮廓。");
        return contours;
    }

    private static HTuple HomMat(double row, double column, double angle)
    {
        using var zero = new HTuple(0d);
        using var rowTuple = new HTuple(row);
        using var columnTuple = new HTuple(column);
        using var angleTuple = new HTuple(angle);
        HOperatorSet.VectorAngleToRigid(zero, zero, zero, rowTuple, columnTuple, angleTuple, out var homMat);
        return homMat;
    }

    private static IReadOnlyList<Point2> ReadPoints(HObject contours, CancellationToken cancellation)
    {
        HOperatorSet.CountObj(contours, out var count);
        using (count)
        {
            var points = new List<Point2>(256);
            for (var index = 1; index <= count.I; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(contours, out var selected, index);
                using (selected)
                {
                    HOperatorSet.GetContourXld(selected, out var rows, out var columns);
                    using (rows) using (columns)
                        for (var point = 0; point < rows.Length; point++)
                        {
                            if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                            points.Add(new Point2(columns[point].D, rows[point].D));
                        }
                }
            }
            return points.AsReadOnly();
        }
    }

    private static Candidate[] Order(Candidate[] candidates, MarkFindSettings settings) => settings.SortType switch
    {
        MarkFindSortType.Ascend => candidates.OrderBy(item => item.Score).ThenBy(item => item.ElementId).ToArray(),
        MarkFindSortType.Descend => candidates.OrderByDescending(item => item.Score).ThenBy(item => item.ElementId).ToArray(),
        MarkFindSortType.Unsorted => candidates,
        _ => throw new MarkFindCapabilityException("MARKFIND_SETTINGS_INVALID", "排序方式取值非法。")
    };

    /// <summary>匹配个数为整体判定，其余限位组逐匹配给出单项结果。</summary>
    private static (Candidate[] Items, bool Overall, IReadOnlyList<string> Messages) Judge(MarkFindSettings settings, Candidate[] candidates)
    {
        var messages = new List<string>();
        var overall = true;
        void Overall(string name, bool passed) { overall &= passed; if (!passed) messages.Add("[NG] " + name); }
        if (settings.CountLimitEnabled)
            Overall("匹配个数判断", candidates.Length >= settings.CountLimitLow && candidates.Length <= settings.CountLimitHigh);
        if (candidates.Length == 0) overall = false;

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var failures = new List<string>();
            void PerMatch(string name, bool passed)
            {
                if (passed) return;
                failures.Add(name);
                messages.Add("[NG] " + name);
            }
            if (settings.AngleLimitEnabled) PerMatch("角度判断", InRange(candidate.AngleDegrees, settings.AngleLimitLow, settings.AngleLimitHigh));
            if (settings.ScoreLimitEnabled) PerMatch("分数判断", InRange(candidate.Score, settings.ScoreLimitLow, settings.ScoreLimitHigh));
            if (settings.MatchPointXLimitEnabled) PerMatch("匹配点X判断", InRange(candidate.CenterX, settings.MatchPointXLimitLow, settings.MatchPointXLimitHigh));
            if (settings.MatchPointYLimitEnabled) PerMatch("匹配点Y判断", InRange(candidate.CenterY, settings.MatchPointYLimitLow, settings.MatchPointYLimitHigh));
            var corners = Corners(candidate.Box);
            if (settings.BoxPointXLimitEnabled)
                PerMatch("Box点X判断", corners.All(point => InRange(point.X, settings.BoxPointXLimitLow, settings.BoxPointXLimitHigh)));
            if (settings.BoxPointYLimitEnabled)
                PerMatch("Box点Y判断", corners.All(point => InRange(point.Y, settings.BoxPointYLimitLow, settings.BoxPointYLimitHigh)));
            candidates[index] = candidate with { Passed = failures.Count == 0, FailedLimits = failures.AsReadOnly() };
        }
        return (candidates, overall, messages.AsReadOnly());
    }

    /// <summary>匹配框四个角点（图像像素坐标，X 向右、Y 向下）。</summary>
    private static Point2[] Corners(MarkFindBox box)
    {
        var halfWidth = box.Width / 2;
        var halfHeight = box.Height / 2;
        // Box.AngleDegrees 已按产品约定取反（HALCON phi 以行轴为基准），这里换回屏幕角度。
        var radians = -box.AngleDegrees * Math.PI / 180d;
        var (sin, cos) = Math.SinCos(radians);
        Point2 Corner(double alongWidth, double alongHeight) => new(
            box.CenterX + alongWidth * cos - alongHeight * sin,
            box.CenterY + alongWidth * sin + alongHeight * cos);
        return new[]
        {
            Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight),
            Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight)
        };
    }

    private static bool InRange(double value, double minimum, double maximum)
        => minimum <= maximum ? value >= minimum && value <= maximum : value >= minimum || value <= maximum;

    internal static double NormalizeDegrees(double angle) => Math.IEEERemainder(angle, 360d);
}
