using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.GrayMatch.Runtime;

/// <summary>
/// 一次同步执行：图像 → 搜索区域（含位置修正）→ 灰度（NCC）模板匹配 → 排序 → 判定 → 结果行/几何 → 输出。
/// 工作流调度、输入准备与资源生命周期属于宿主。
/// </summary>
public sealed class GrayMatchSession : IToolSession<GrayMatchSettings, GrayMatchOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<GrayMatchOutput>> ExecuteAsync(ToolInvocation<GrayMatchSettings> invocation)
    {
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(invocation);
            var output = Execute(invocation);
            return ValueTask.FromResult(new ToolOutcome<GrayMatchOutput>(true, output,
                output.IsPass ? BusinessJudgment.Pass : BusinessJudgment.Fail, Array.Empty<Diagnostic>()));
        }
        catch (GrayMatchCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<GrayMatchOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<GrayMatchOutput>.Failed("GRAYMATCH_CANCELLED", "灰度匹配已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<GrayMatchOutput>.Failed("GRAYMATCH_EXECUTION_FAILED", error.Message));
        }
    }

    private sealed record Candidate(long ElementId, double CenterX, double CenterY, double AngleDegrees, double Score,
        HRegion Region, GrayMatchBox Box, bool Passed, IReadOnlyList<string> FailedLimits);

    private static GrayMatchOutput Execute(ToolInvocation<GrayMatchSettings> invocation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new GrayMatchCapabilityException("GRAYMATCH_SETTINGS_INVALID", string.Join("；", errors.Select(error => error.Message)));
        var template = GrayMatchTemplate.Decode(settings.ModelData);
        template.RequireAngleCoverage(settings);
        template.RequireEndLevelCoverage(settings);
        var cancellation = invocation.Context.Cancellation;
        var inputs = invocation.Inputs;
        var image = inputs.Get<IReadOnlyImage>(GrayMatchSchema.ImageInput)
            ?? throw new GrayMatchCapabilityException("GRAYMATCH_IMAGE_REQUIRED", "缺少输入图像。");
        var roi = OptionalRegion(inputs, GrayMatchSchema.RoiInput)
            ?? throw new GrayMatchCapabilityException("GRAYMATCH_ROI_REQUIRED", "请为搜索区域连接区域输入（原件必须框选搜索区域）。");
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
        using var posedRoi = fixture is null ? roiRegion.Clone() : HalconResourceBridge.TransformRegion(roiRegion, FixtureDeclarations.ToAffine2D(fixture, GrayMatchSchema.ImageFrame));
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
                var measurements = new List<GrayMatchMeasurement>(judged.Items.Length);
                foreach (var candidate in judged.Items)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var captured = HalconResourceBridge.CaptureRegion(candidate.Region, GrayMatchSchema.ImageFrame, frame, outputResources);
                    measurements.Add(new(candidate.ElementId, candidate.CenterX, candidate.CenterY, candidate.AngleDegrees,
                        candidate.Score, candidate.Box, captured, candidate.Passed, candidate.FailedLimits));
                }
                var modulePassed = judged.Overall && (measurements.Count > 0 || settings.OkWhenCountIsZero) && measurements.All(item => item.Passed);
                var projection = GrayMatchResultProjection.Create(measurements, modulePassed,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds, fixture);
                var geometry = GrayMatchResultGeometry.Capture(image, modulePassed, search, fixture, cancellation);
                var output = new GrayMatchOutput(settings, projection, measurements.AsReadOnly(),
                    judged.Messages, geometry, new IDisposable[] { outputResources });
                invocation.Context.Log($"[Perf] tool=GrayMatch count={projection.Count} elapsed={projection.ModuleRunTime:0.###}ms");
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
            GrayMatchTemplate.Clear(modelId);
        }
    }

    private static IReadOnlyRegion? OptionalRegion(InputValues values, string id)
        => values.Contains(id) ? values.Get<IReadOnlyRegion?>(id) : null;

    private static RecordValue? OptionalFixture(InputValues values)
        => values.Contains(GrayMatchSchema.FixtureInput) ? values.Get<RecordValue?>(GrayMatchSchema.FixtureInput) : null;

    private static void RequireSearchArea(HRegion search)
    {
        HOperatorSet.AreaCenter(search, out var area, out var row, out var column);
        using (area) using (row) using (column)
            if (area.Length == 0 || area.LArr.All(value => value == 0))
                throw new GrayMatchCapabilityException("GRAYMATCH_ROI_EMPTY", "搜索区域与图像有效域没有交集：请检查区域与位置修正。");
    }

    /// <summary>NCC 模板匹配；返回按设置排序后的候选（仍未判定）。</summary>
    private static Candidate[] Find(HImage reduced, HTuple modelId, GrayMatchTemplate template, GrayMatchSettings settings,
        CancellationToken cancellation)
    {
        using var angleStart = new HTuple(settings.AngleStart * Math.PI / 180d);
        using var angleExtent = new HTuple(Math.Max(0.0001, (settings.AngleEnd - settings.AngleStart) * Math.PI / 180d));
        using var minScore = new HTuple(settings.MinScore);
        using var numMatches = new HTuple(settings.MaxMatchCount);
        using var maxOverlap = new HTuple(Math.Clamp(settings.MaxOverlap, 0, 100) / 100d);
        using var subPixel = new HTuple("true");
        // 「匹配结束层」→ find_ncc_model 的 NumLevels（0＝使用全部层）；超出模板层数已在入口校验。
        using var numLevels = new HTuple(settings.EndPyramidLevel);

        HOperatorSet.FindNccModel(reduced, modelId, angleStart, angleExtent, minScore, numMatches, maxOverlap,
            subPixel, numLevels, out var rows, out var columns, out var angles, out var scores);
        using (rows) using (columns) using (angles) using (scores)
        {
            if (rows.Length != columns.Length || rows.Length != angles.Length || rows.Length != scores.Length)
                throw new GrayMatchCapabilityException("GRAYMATCH_NATIVE_RESULT", "HALCON 返回的匹配元组长度不一致。");
            if (rows.Length == 0) return Array.Empty<Candidate>();
            cancellation.ThrowIfCancellationRequested();
            var candidates = new List<Candidate>(rows.Length);
            try
            {
                for (var index = 0; index < rows.Length; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using var homMat = HomMat(template.OriginRow, template.OriginColumn, rows[index].D, columns[index].D, angles[index].D);
                    using var boxColumn = new HTuple(template.Box.Column);
                    using var boxRow = new HTuple(template.Box.Row);
                    HOperatorSet.AffineTransPoint2d(homMat, boxColumn, boxRow, out var centerColumn, out var centerRow);
                    using (centerColumn) using (centerRow)
                    {
                        var phi = template.Box.Phi + angles[index].D;
                        HOperatorSet.GenRectangle2(out var boxRegion, centerRow.D, centerColumn.D, phi,
                            template.Box.Length1, template.Box.Length2);
                        using (boxRegion)
                        {
                            var box = new GrayMatchBox(centerColumn.D, centerRow.D, template.Box.Length1 * 2, template.Box.Length2 * 2,
                                NormalizeDegrees(-phi * 180d / Math.PI));
                            candidates.Add(new(0, columns[index].D, rows[index].D,
                                NormalizeDegrees(-angles[index].D * 180d / Math.PI), scores[index].D,
                                new HRegion(boxRegion), box, true, Array.Empty<string>()));
                        }
                    }
                }
            }
            catch
            {
                foreach (var candidate in candidates) candidate.Region.Dispose();
                throw;
            }
            return Number(Order(candidates, settings));
        }
    }

    /// <summary>把模板坐标系映射到图像坐标系：原点 (originRow, originColumn)/0° → 匹配位置 (row, column)/angle。</summary>
    private static HTuple HomMat(double originRow, double originColumn, double row, double column, double angle)
    {
        using var originRowTuple = new HTuple(originRow);
        using var originColumnTuple = new HTuple(originColumn);
        using var zero = new HTuple(0d);
        using var rowTuple = new HTuple(row);
        using var columnTuple = new HTuple(column);
        using var angleTuple = new HTuple(angle);
        HOperatorSet.VectorAngleToRigid(originRowTuple, originColumnTuple, zero, rowTuple, columnTuple, angleTuple, out var homMat);
        return homMat;
    }

    /// <summary>原件排序方式（同族 SDK 枚举语义）：分数降序/角度降序/X升序/Y升序/X升序·Y升序/Y升序·X升序。</summary>
    private static Candidate[] Order(IReadOnlyList<Candidate> candidates, GrayMatchSettings settings)
        => settings.SortType switch
        {
            GrayMatchSortType.Score => candidates.OrderByDescending(item => item.Score).ToArray(),
            GrayMatchSortType.Angle => candidates.OrderByDescending(item => item.AngleDegrees).ToArray(),
            GrayMatchSortType.X => candidates.OrderBy(item => item.CenterX).ToArray(),
            GrayMatchSortType.Y => candidates.OrderBy(item => item.CenterY).ToArray(),
            GrayMatchSortType.XY => candidates.OrderBy(item => item.CenterX).ThenBy(item => item.CenterY).ToArray(),
            GrayMatchSortType.YX => candidates.OrderBy(item => item.CenterY).ThenBy(item => item.CenterX).ToArray(),
            _ => throw new GrayMatchCapabilityException("GRAYMATCH_SETTINGS_INVALID", "排序方式取值非法。")
        };

    /// <summary>排序完成后按顺序编号，序号即结果表/输出数组的下标。</summary>
    private static Candidate[] Number(IReadOnlyList<Candidate> ordered)
    {
        var result = new Candidate[ordered.Count];
        for (var index = 0; index < ordered.Count; index++) result[index] = ordered[index] with { ElementId = index + 1 };
        return result;
    }

    /// <summary>匹配个数为整体判定，其余限位组逐匹配给出单项结果。</summary>
    private static (Candidate[] Items, bool Overall, IReadOnlyList<string> Messages) Judge(GrayMatchSettings settings, Candidate[] candidates)
    {
        var messages = new List<string>();
        var overall = true;
        void Overall(string name, bool passed) { overall &= passed; if (!passed) messages.Add("[NG] " + name); }
        if (settings.CountLimitEnabled)
            Overall("匹配个数判断", candidates.Length >= settings.CountLimitLow && candidates.Length <= settings.CountLimitHigh);
        if (candidates.Length == 0 && !settings.OkWhenCountIsZero) overall = false;

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
    private static Point2[] Corners(GrayMatchBox box)
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
