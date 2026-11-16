using System.Diagnostics;
using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>一次同步执行；工作流调度、输入准备与资源生命周期属于宿主。</summary>
public sealed class BlobLabelsSession : IToolSession<BlobLabelsSettings, BlobLabelsOutput>, IReusableToolSession
{
    private int _disposed;

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolOutcome<BlobLabelsOutput>> ExecuteAsync(ToolInvocation<BlobLabelsSettings> invocation)
    {
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(invocation);
            var started = Stopwatch.GetTimestamp();
            var output = Execute(invocation);
            return ValueTask.FromResult(new ToolOutcome<BlobLabelsOutput>(true, output,
                output.IsPass ? BusinessJudgment.Pass : BusinessJudgment.Fail, Array.Empty<Diagnostic>()));
        }
        catch (BlobLabelsCapabilityException error)
        {
            return ValueTask.FromResult(ToolOutcome<BlobLabelsOutput>.Failed(error.Code, error.Message));
        }
        catch (OperationCanceledException)
        {
            return ValueTask.FromResult(ToolOutcome<BlobLabelsOutput>.Failed("BLOBLABEL_CANCELLED", "Blob标签分析已取消。"));
        }
        catch (Exception error)
        {
            return ValueTask.FromResult(ToolOutcome<BlobLabelsOutput>.Failed("BLOBLABEL_EXECUTION_FAILED", error.Message));
        }
    }

    private sealed record Candidate(int RegionIndex, int ComponentLabel, double Area, double Perimeter, double X, double Y,
        double Circularity, double Rectangularity, BlobLabelsBounds Bounds, BlobLabelsOrientedBounds OrientedBounds,
        double AxisAngleDegrees, bool Passed, IReadOnlyList<string> FailedLimits);

    private static BlobLabelsOutput Execute(ToolInvocation<BlobLabelsSettings> invocation)
    {
        var started = Stopwatch.GetTimestamp();
        var settings = invocation.Settings.Freeze();
        var errors = settings.Validate();
        if (errors.Count != 0)
            throw new BlobLabelsCapabilityException("BLOBLABEL_SETTINGS_INVALID", string.Join("；", errors.Select(error => error.Message)));
        var cancellation = invocation.Context.Cancellation;
        var thresholds = settings.ReadThresholdAreas();
        if (thresholds.Count == 0)
            throw new BlobLabelsCapabilityException("BLOBLABEL_THRESHOLD_REQUIRED", "请先在“类别信息”中设置阈值区域（灰度值与面积范围）。");
        var inputs = invocation.Inputs;
        var image = inputs.Get<IReadOnlyImage>(BlobLabelsSchema.ImageInput)
            ?? throw new BlobLabelsCapabilityException("BLOBLABEL_IMAGE_REQUIRED", "缺少输入图像。");
        var fixture = OptionalFixture(inputs);

        using var neighbourhood = new HalconNeighborhoodScope(settings.Connectivity);
        using var native = HalconResourceBridge.CopyImage(image);
        HOperatorSet.GetImageSize(native, out var widthTuple, out var heightTuple);
        using var widthOwner = widthTuple;
        using var heightOwner = heightTuple;
        var width = widthTuple.I;
        var height = heightTuple.I;
        using var gray = HalconImageAdmission.Gray(native);
        using var domain = gray.GetDomain();
        var frame = image.Frame;

        var imageMask = OptionalImage(inputs, BlobLabelsSchema.MaskInput);
        using var maskFromImage = imageMask is null ? null : ThresholdImageMask(imageMask, width, height);
        using var maskRegion = OptionalRegion(inputs, BlobLabelsSchema.MaskRegionInput, frame, width, height);
        using var inspectionRegion = OptionalRegion(inputs, BlobLabelsSchema.InspectionRoiInput, frame, width, height);
        using var annulusRegion = OptionalRegion(inputs, BlobLabelsSchema.AnnulusRoiInput, frame, width, height);
        using var combinedMask = Combine(maskFromImage, maskRegion);
        using var effective = EffectiveDomain(domain, inspectionRegion, annulusRegion, combinedMask);

        using var reduced = gray.ReduceDomain(effective);
        using var foreground = Segment(reduced, thresholds, settings.LabelWiseAreaSelectEnabled, cancellation);
        HalconNeighborhoodScope.RequireUnlimitedConnection();
        using var connected = foreground.Connection();
        var candidates = Filter(ReadCandidates(connected, cancellation), settings, width, height, cancellation);
        var ordered = Order(candidates, settings);
        var limited = ordered.Take(settings.FindCount).ToArray();
        var judged = Judge(settings, limited);
        var overallPassed = judged.Overall;
        var limitMessages = judged.Messages;

        var outputResources = invocation.Context.Resources.CreateScope();
        try
        {
            var measurements = new List<BlobLabelsMeasurement>(judged.Items.Length);
            foreach (var candidate in judged.Items)
            {
                cancellation.ThrowIfCancellationRequested();
                using var region = connected.SelectObj(candidate.RegionIndex);
                var contour = ReadContour(region, cancellation);
                var captured = HalconResourceBridge.CaptureRegion(region, BlobLabelsSchema.ImageFrame, frame, outputResources);
                measurements.Add(new(candidate.ComponentLabel, candidate.ComponentLabel, candidate.Area, candidate.Perimeter,
                    candidate.X, candidate.Y, candidate.Circularity, candidate.Rectangularity, candidate.Bounds,
                    candidate.OrientedBounds, candidate.AxisAngleDegrees, contour, captured, candidate.Passed,
                    candidate.Passed ? Array.Empty<string>() : candidate.FailedLimits));
            }

            using var selectedRegions = SelectRegions(connected, judged.Items.Select(item => item.RegionIndex).ToArray());
            using var binaryNative = RegionImage(foreground, width, height);
            var binaryImage = HalImageSnapshotSource.Capture(binaryNative, frame, BlobLabelsSchema.ImageFrame, outputResources);
            IReadOnlyImage? blobImage = null;
            if (settings.BlobImageOutputEnabled)
            {
                using var blobNative = RegionImage(selectedRegions, width, height);
                blobImage = HalImageSnapshotSource.Capture(blobNative, frame, BlobLabelsSchema.ImageFrame, outputResources);
            }

            var modulePassed = overallPassed && measurements.All(item => item.Passed);
            var projection = BlobLabelsResultProjection.Create(measurements, modulePassed,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, fixture);
            var geometry = BlobLabelsResultGeometry.Capture(image, modulePassed, domain, inspectionRegion, annulusRegion,
                combinedMask, fixture, cancellation);
            var output = new BlobLabelsOutput(settings, projection, measurements.AsReadOnly(),
                limitMessages, geometry, binaryImage, blobImage, new IDisposable[] { outputResources });
            invocation.Context.Log($"[Perf] tool=BlobLabels count={projection.Count} elapsed={projection.ModuleRunTime:0.###}ms");
            return output;
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private static IReadOnlyImage? OptionalImage(InputValues values, string id)
        => values.Contains(id) ? values.Get<IReadOnlyImage?>(id) : null;

    private static RecordValue? OptionalFixture(InputValues values)
        => values.Contains(BlobLabelsSchema.FixtureInput) ? values.Get<RecordValue?>(BlobLabelsSchema.FixtureInput) : null;

    private static HRegion? OptionalRegion(InputValues values, string id, ImageFrameAddress frame, int width, int height)
    {
        if (!values.Contains(id)) return null;
        var region = values.Get<IReadOnlyRegion?>(id);
        return region is null ? null : HalconResourceBridge.CopyRegion(region, frame, width, height);
    }

    private static HRegion ThresholdImageMask(IReadOnlyImage mask, int width, int height)
    {
        if (mask.Width != width || mask.Height != height || mask.Layout.Format != PixelLayout.Gray8)
            throw new BlobLabelsCapabilityException("BLOBLABEL_MASK_INVALID", "掩膜图像必须与输入同尺寸且为 Gray8。");
        using var image = HalconResourceBridge.CopyImage(mask);
        return image.Threshold(1d, 255d);
    }

    private static HRegion EmptyRegion()
    {
        HOperatorSet.GenEmptyRegion(out var empty);
        using (empty) return new HRegion(empty);
    }

    private static HRegion? Combine(HRegion? first, HRegion? second)
    {
        if (first is null) return second?.Clone();
        if (second is null) return first.Clone();
        return first.Union2(second);
    }

    /// <summary>有效检测域：全图 − 屏蔽区，与检测区域/扇环求交（有则用，无则用全图）。</summary>
    private static HRegion EffectiveDomain(HRegion domain, HRegion? inspection, HRegion? annulus, HRegion? mask)
    {
        HRegion current = inspection is not null ? domain.Intersection(inspection) : domain.Clone();
        if (annulus is not null)
        {
            using var annulusClipped = domain.Intersection(annulus);
            using var union = current.Union2(annulusClipped);
            current.Dispose();
            current = union.Clone();
        }
        if (mask is not null)
        {
            using var subtracted = current.Difference(mask);
            current.Dispose();
            current = subtracted.Clone();
        }
        return current;
    }

    /// <summary>
    /// 按原件阈值区域逐条分割：每条给一个灰度值与面积范围；反选表示取该灰度以外的像素；
    /// 新增/减少决定与已累计区域的并/差。标签面积使能时，条内连通域先按该条面积范围筛选。
    /// </summary>
    private static HRegion Segment(HImage reduced, IReadOnlyList<BlobLabelsThresholdArea> thresholds, bool labelWiseArea,
        CancellationToken cancellation)
    {
        HRegion? accumulated = null;
        foreach (var threshold in thresholds)
        {
            cancellation.ThrowIfCancellationRequested();
            using var band = Band(reduced, threshold);
            using var prepared = labelWiseArea ? FilterByArea(band, threshold.MinimumArea, threshold.MaximumArea) : band.Clone();
            if (accumulated is null)
            {
                accumulated = threshold.IsAdd ? prepared.Clone() : EmptyRegion();
                continue;
            }
            var next = threshold.IsAdd ? accumulated.Union2(prepared) : accumulated.Difference(prepared);
            accumulated.Dispose();
            accumulated = next;
        }
        return accumulated ?? throw new BlobLabelsCapabilityException("BLOBLABEL_THRESHOLD_REQUIRED", "请先设置阈值区域。");
    }

    private static HRegion Band(HImage reduced, BlobLabelsThresholdArea threshold)
    {
        var gray = (double)threshold.GrayValue;
        if (!threshold.IsInverse) return reduced.Threshold(gray, gray);
        using var all = reduced.Threshold(0d, 255d);
        using var single = reduced.Threshold(gray, gray);
        return all.Difference(single);
    }

    private static HRegion FilterByArea(HRegion region, int minimum, int maximum)
    {
        using var connected = region.Connection();
        HOperatorSet.SelectShape(connected, out var selected, "area", "and", minimum, maximum);
        using (selected) return new HRegion(selected);
    }

    private static HRegion SelectRegions(HRegion source, IReadOnlyList<int> indexes)
    {
        if (indexes.Count == 0) return EmptyRegion();
        using var tuple = new HTuple(indexes.ToArray());
        HOperatorSet.SelectObj(source, out var selected, tuple);
        using (selected) return new HRegion(selected);
    }

    private static HImage RegionImage(HRegion region, int width, int height)
    {
        HOperatorSet.RegionToBin(region, out var binary, 255, 0, width, height);
        using (binary) return new HImage(binary);
    }

    private static Candidate[] ReadCandidates(HRegion regions, CancellationToken cancellation)
    {
        HOperatorSet.AreaCenter(regions, out var areas, out var rows, out var columns);
        using var areaOwner = areas;
        using var rowOwner = rows;
        using var columnOwner = columns;
        HOperatorSet.SmallestRectangle1(regions, out var top, out var left, out var bottom, out var right);
        using var topOwner = top;
        using var leftOwner = left;
        using var bottomOwner = bottom;
        using var rightOwner = right;
        using var circularity = regions.Circularity();
        using var rectangularity = regions.Rectangularity();
        using var perimeter = regions.Contlength();
        HOperatorSet.SmallestRectangle2(regions, out var centerRow, out var centerColumn, out var phi, out var halfLength1, out var halfLength2);
        using var centerRowOwner = centerRow;
        using var centerColumnOwner = centerColumn;
        using var phiOwner = phi;
        using var halfLength1Owner = halfLength1;
        using var halfLength2Owner = halfLength2;
        HOperatorSet.OrientationRegion(regions, out var orientation);
        using var orientationOwner = orientation;
        var candidates = new List<Candidate>(areas.Length);
        for (var index = 0; index < areas.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (areas[index].D <= 0) continue;
            var circular = Math.Min(1, circularity[index].D);
            var rectangular = Math.Min(1, rectangularity[index].D);
            var width = 2 * halfLength1[index].D;
            var height = 2 * halfLength2[index].D;
            var angle = halfLength1[index].D == 0 && halfLength2[index].D == 0 ? 0d : -phi[index].D * 180d / Math.PI;
            candidates.Add(new(index + 1, index + 1, areas[index].D, perimeter[index].D, columns[index].D, rows[index].D,
                circular, rectangular,
                new(left[index].D, top[index].D, right[index].D - left[index].D + 1, bottom[index].D - top[index].D + 1),
                new(centerColumn[index].D, centerRow[index].D, width, height, angle),
                orientation[index].D * 180d / Math.PI, true, Array.Empty<string>()));
        }
        return candidates.ToArray();
    }

    private static Candidate[] Filter(Candidate[] candidates, BlobLabelsSettings settings, int width, int height,
        CancellationToken cancellation)
    {
        var accepted = new List<Candidate>(candidates.Length);
        foreach (var candidate in candidates)
        {
            cancellation.ThrowIfCancellationRequested();
            var bounds = candidate.OrientedBounds;
            var centerBias = Math.Sqrt(Math.Pow(candidate.X - width / 2d, 2) + Math.Pow(candidate.Y - height / 2d, 2));
            if (settings.AreaFilterEnabled && !InRange((float)candidate.Area, settings.AreaFilterMin, settings.AreaFilterMax)) continue;
            if (settings.PerimeterFilterEnabled && !InRange((float)candidate.Perimeter, settings.PerimeterFilterMin, settings.PerimeterFilterMax)) continue;
            if (settings.ShortAxisFilterEnabled && !InRange((float)bounds.ShortAxis, settings.ShortAxisFilterMin, settings.ShortAxisFilterMax)) continue;
            if (settings.LongAxisFilterEnabled && !InRange((float)bounds.LongAxis, settings.LongAxisFilterMin, settings.LongAxisFilterMax)) continue;
            if (settings.CircularityFilterEnabled && !InRange(candidate.Circularity, settings.CircularityFilterMin, settings.CircularityFilterMax)) continue;
            if (settings.RectangularityFilterEnabled && !InRange(candidate.Rectangularity, settings.RectangularityFilterMin, settings.RectangularityFilterMax)) continue;
            if (settings.CenterBiasFilterEnabled && !InRange(centerBias, settings.CenterBiasFilterMin, settings.CenterBiasFilterMax)) continue;
            if (settings.AxisRatioFilterEnabled && !InRange(bounds.AxisRatio, settings.AxisRatioFilterMin, settings.AxisRatioFilterMax)) continue;
            accepted.Add(candidate);
        }
        return accepted.ToArray();
    }

    private static IReadOnlyList<Candidate> Order(Candidate[] candidates, BlobLabelsSettings settings)
    {
        if (settings.SortMode == BlobLabelsSortMode.SortModeNotSort) return candidates;
        var ordered = settings.SortMode == BlobLabelsSortMode.SortModeAscend
            ? candidates.OrderBy(item => SortKey(item, settings.SortFeature)).ThenBy(item => item.ComponentLabel)
            : candidates.OrderByDescending(item => SortKey(item, settings.SortFeature)).ThenBy(item => item.ComponentLabel);
        return ordered.ToArray();
    }

    private static double SortKey(Candidate candidate, BlobLabelsSortFeature feature) => feature switch
    {
        BlobLabelsSortFeature.SortFeatureArea => candidate.Area,
        BlobLabelsSortFeature.SortFeaturePerimeter => candidate.Perimeter,
        BlobLabelsSortFeature.SortFeatureCircularity => candidate.Circularity,
        BlobLabelsSortFeature.SortFeatureRect => candidate.Rectangularity,
        BlobLabelsSortFeature.SortFeatureCentroidX => candidate.X,
        BlobLabelsSortFeature.SortFeatureCentroidY => candidate.Y,
        BlobLabelsSortFeature.SortFeatureBoxAngle => candidate.OrientedBounds.AngleDegrees,
        BlobLabelsSortFeature.SortFeatureBoxWidth => candidate.OrientedBounds.Width,
        BlobLabelsSortFeature.SortFeatureBoxHeight => candidate.OrientedBounds.Height,
        BlobLabelsSortFeature.SortFeatureRectX => candidate.Bounds.X,
        BlobLabelsSortFeature.SortFeatureRectY => candidate.Bounds.Y,
        BlobLabelsSortFeature.SortFeatureAxisAngle => candidate.AxisAngleDegrees,
        BlobLabelsSortFeature.SortFeatureAxisRatio => candidate.OrientedBounds.AxisRatio,
        _ => throw new BlobLabelsCapabilityException("BLOBLABEL_SETTINGS_INVALID", "排序特征取值非法。")
    };

    /// <summary>判定：数量/总面积为整体判定，其余判定组逐 Blob 给出单项结果（与同族 Blob 工具一致）。</summary>
    private static (Candidate[] Items, bool Overall, IReadOnlyList<string> Messages) Judge(BlobLabelsSettings settings, Candidate[] candidates)
    {
        var messages = new List<string>();
        var perBlob = new bool[candidates.Length];
        Array.Fill(perBlob, true);
        var overall = true;
        void Overall(string name, bool passed) { overall &= passed; if (!passed) messages.Add("[NG] " + name); }
        if (settings.BlobCountJudgeEnabled)
            Overall("Blob个数判断", candidates.Length >= settings.BlobCountJudgeMin && candidates.Length <= settings.BlobCountJudgeMax);
        var totalArea = (float)candidates.Sum(item => item.Area);
        if (settings.TotalAreaJudgeEnabled)
            Overall("Blob总面积判断", totalArea >= settings.TotalAreaJudgeMin && totalArea <= settings.TotalAreaJudgeMax);
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            var bounds = candidate.OrientedBounds;
            var failures = new List<string>();
            void PerBlob(string name, bool passed)
            {
                if (passed) return;
                perBlob[index] = false;
                failures.Add(name);
                messages.Add("[NG] " + name);
            }
            if (settings.BlobAreaJudgeEnabled) PerBlob("Blob面积判断", InRange((float)candidate.Area, settings.BlobAreaJudgeMin, settings.BlobAreaJudgeMax));
            if (settings.CentroidXJudgeEnabled) PerBlob("质心X判断", InRange(candidate.X, settings.CentroidXJudgeMin, settings.CentroidXJudgeMax));
            if (settings.CentroidYJudgeEnabled) PerBlob("质心Y判断", InRange(candidate.Y, settings.CentroidYJudgeMin, settings.CentroidYJudgeMax));
            if (settings.BoxAngleJudgeEnabled) PerBlob("Box角度判断", InRange(bounds.AngleDegrees, settings.BoxAngleJudgeMin, settings.BoxAngleJudgeMax));
            if (settings.CenterXJudgeEnabled) PerBlob("中心X判断", InRange(bounds.CenterX, settings.CenterXJudgeMin, settings.CenterXJudgeMax));
            if (settings.CenterYJudgeEnabled) PerBlob("中心Y判断", InRange(bounds.CenterY, settings.CenterYJudgeMin, settings.CenterYJudgeMax));
            if (settings.BoxHeightJudgeEnabled) PerBlob("Box高度判断", InRange(bounds.Height, settings.BoxHeightJudgeMin, settings.BoxHeightJudgeMax));
            if (settings.BoxWidthJudgeEnabled) PerBlob("Box宽度判断", InRange(bounds.Width, settings.BoxWidthJudgeMin, settings.BoxWidthJudgeMax));
            if (settings.PerimeterJudgeEnabled) PerBlob("周长判断", InRange((float)candidate.Perimeter, settings.PerimeterJudgeMin, settings.PerimeterJudgeMax));
            if (settings.LongAxisJudgeEnabled) PerBlob("长轴判断", InRange((float)bounds.LongAxis, settings.LongAxisJudgeMin, settings.LongAxisJudgeMax));
            if (settings.ShortAxisJudgeEnabled) PerBlob("短轴判断", InRange((float)bounds.ShortAxis, settings.ShortAxisJudgeMin, settings.ShortAxisJudgeMax));
            if (settings.CircularityJudgeEnabled) PerBlob("圆形度判断", InRange(candidate.Circularity, settings.CircularityJudgeMin, settings.CircularityJudgeMax));
            if (settings.RectangularityJudgeEnabled) PerBlob("矩形度判断", InRange(candidate.Rectangularity, settings.RectangularityJudgeMin, settings.RectangularityJudgeMax));
            candidates[index] = candidate with { Passed = perBlob[index], FailedLimits = failures.AsReadOnly() };
        }
        if (candidates.Length == 0) overall = false;
        return (candidates, overall, messages.AsReadOnly());
    }

    private static bool InRange(double value, double minimum, double maximum)
        => minimum <= maximum ? value >= minimum && value <= maximum : value >= minimum || value <= maximum;

    private static IReadOnlyList<Point2> ReadContour(HRegion region, CancellationToken cancellation)
    {
        HOperatorSet.GenContourRegionXld(region, out var contours, "border");
        using var contourOwner = contours;
        HOperatorSet.CountObj(contours, out var count);
        using var countOwner = count;
        var points = new List<Point2>(256);
        for (var index = 1; index <= count.I; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.SelectObj(contours, out var selected, index);
            using var contour = selected;
            HOperatorSet.GetContourXld(contour, out var rows, out var columns);
            using var rowOwner = rows;
            using var columnOwner = columns;
            for (var point = 0; point < rows.Length; point++)
            {
                if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                points.Add(new Point2(columns[point].D, rows[point].D));
            }
        }
        return points.AsReadOnly();
    }

}

/// <summary>算法内部可诊断失败：带稳定错误码，不使用异常文本猜测原因。</summary>
public sealed class BlobLabelsCapabilityException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>按冻结连通性设置 HALCON 邻域，并在原线程恢复调用方设置；不改全局上限。</summary>
internal sealed class HalconNeighborhoodScope : IDisposable
{
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly HTuple _previous;
    private bool _disposed;

    internal HalconNeighborhoodScope(BlobLabelsConnectivity connectivity)
    {
        if (!Enum.IsDefined(connectivity)) throw new ArgumentOutOfRangeException(nameof(connectivity));
        RequireUnlimitedConnection();
        HOperatorSet.GetSystem("tsp_neighborhood", out _previous);
        try { HOperatorSet.SetSystem("tsp_neighborhood", (int)connectivity); }
        catch { _previous.Dispose(); throw; }
    }

    internal static void RequireUnlimitedConnection()
    {
        HOperatorSet.GetSystem("max_connection", out var limit);
        using (limit)
            if (limit.I != 0)
                throw new BlobLabelsCapabilityException("BLOBLABEL_HALCON_MAX_CONNECTION",
                    "HALCON 连通区域返回上限必须为 0（不截断）；数量限制在筛选与排序之后执行。");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("BlobLabels 的 HALCON 执行作用域必须在原同步线程退出。");
        try { HOperatorSet.SetSystem("tsp_neighborhood", _previous); }
        finally { _previous.Dispose(); _disposed = true; }
    }
}
