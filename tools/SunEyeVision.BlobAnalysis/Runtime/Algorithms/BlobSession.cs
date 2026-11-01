using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

[Flags]
public enum BlobOutputRequest { None = 0, GrayImage = 1, BinaryImage = 2, Boundary = 4 }

/// <summary>One synchronous HALCON invocation; all public inputs/outputs are vendor-neutral.</summary>
public sealed partial class BlobSession : IToolSession<BlobSettings, BlobOutput>
{
    private int _disposed;
    public ValueTask DisposeAsync() { Interlocked.Exchange(ref _disposed, 1); return ValueTask.CompletedTask; }

    public ValueTask<ToolOutcome<BlobOutput>> ExecuteAsync(ToolInvocation<BlobSettings> invocation)
    {
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
            if(invocation.Settings.SegmentationMode!=BlobSegmentationMode.SingleThreshold)
                return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_MODE_NOT_READY","该阈值模式尚未完成原厂语义对齐。"));
            var schema=new BlobTool().Describe();
            var requested=BlobOutputRequest.None;
            foreach(var demand in invocation.OutputRequests)
            {
                demand.Validate(schema);
                if(demand.PortId==BlobSchema.Field(5))requested|=BlobOutputRequest.GrayImage;
                if(demand.PortId==BlobSchema.Field(6))requested|=BlobOutputRequest.BinaryImage;
                if(demand.PortId==BlobSchema.BlobsPort&&(demand.FieldPath.Count==0||demand.FieldPath[0] is "boundary" or "contour" or "contour-lossy"))requested|=BlobOutputRequest.Boundary;
            }
            var input=BlobInput.Read(invocation.Inputs);
            var output=Execute(invocation.Settings,input.Image,invocation.Context.Resources,
                input.Inspection,input.Mask,input.Pose,
                invocation.Context.Cancellation,requested,input.LocalRoi);
            IReadOnlyList<Diagnostic> diagnostics=output.Measurements.Any(blob=>blob.Boundary.Count>1)
                ?new[]{new Diagnostic("BLOB_CONTOUR_LOSSY","单轮廓仅投影一个外环；多环和孔洞应读取boundary/Region。",BlobSchema.BlobsPort)}
                :Array.Empty<Diagnostic>();
            return ValueTask.FromResult(new ToolOutcome<BlobOutput>(true,output,output.IsPass?BusinessJudgment.Pass:BusinessJudgment.Fail,diagnostics));
        }
        catch(OperationCanceledException) { return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_CANCELLED","Blob执行已取消。")); }
        catch(Exception e) { return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_EXECUTION_FAILED",e.Message)); }
    }
    private sealed record Candidate(int Index, double Area, double Perimeter, double X, double Y,
        double Circularity, double Rectangularity, BlobBounds Bounds, Point2[] Corners, bool IsPass);

    public BlobOutput Execute(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection = null,
        IReadOnlyRegion? mask = null, RecordValue? pose = null, CancellationToken cancellation = default,
        BlobOutputRequest requested = BlobOutputRequest.None,RecordValue? localRoi=null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(resources);
        settings = settings.Freeze();
        if (settings.SegmentationMode != BlobSegmentationMode.SingleThreshold)
            throw new InvalidOperationException("BLOB_MODE_NOT_READY: 该阈值模式尚未完成原厂语义对齐。");
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors.Select(x => x.Message)));
        if ((requested & ~(BlobOutputRequest.GrayImage | BlobOutputRequest.BinaryImage | BlobOutputRequest.Boundary)) != 0)
            throw new ArgumentException("Unknown Blob output request.", nameof(requested));
        cancellation.ThrowIfCancellationRequested();
        using var execution = new HalconBlobExecutionScope(settings.Connectivity);
        if (settings.SegmentationMode == BlobSegmentationMode.None) HalconImageAdmission.RequireBinary(input, cancellation);
        using var image = HalconResourceBridge.CopyImage(input);
        HOperatorSet.GetImageSize(image, out var widthValue, out var heightValue);
        using var widthTuple = widthValue; using var heightTuple = heightValue;
        int width = widthValue.I, height = heightValue.I;
        // Capture the same frozen input frame for every output and ROI reader admission.
        var frame = input.Frame;
        if(localRoi is not null)
        {
            BlobLocalRoi.Validate(localRoi);
            if(inspection is not null||mask is not null||settings.IsPositionCorrectionEnabled)
                throw new ArgumentException("本地几何不能与像素区域或未接通的位置修正同时执行。");
        }
        if (!settings.IsPositionCorrectionEnabled) pose = null;
        else
        {
            if (pose is null) throw new ArgumentException("Position correction is enabled but its source is missing.");
            SpatialDeclarations.ValidateAffine2D(pose, inspection?.Frame.Id ?? mask?.Frame.Id ?? BlobSchema.ImageFrame.Id, BlobSchema.ImageFrame.Id);
            if (inspection is not null && mask is not null && inspection.Frame != mask.Frame)
                throw new ArgumentException("Inspection and mask must share the pose source coordinate frame.");
        }
        using var gray = HalconImageAdmission.Gray(image);
        using var domain = gray.GetDomain();
        using var inspectionRegion = localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Inspection,domain,cancellation)
            :inspection is null ? domain.Clone() : HalconResourceBridge.CopyRegion(inspection, frame, width, height);
        using var maskRegion = localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Mask,domain,cancellation)
            :mask is null ? EmptyRegion() : HalconResourceBridge.CopyRegion(mask, frame, width, height);
        using var correctedInspection = inspection is null ? inspectionRegion.Clone() : HalconResourceBridge.TransformRegion(inspectionRegion, pose);
        using var correctedMask = HalconResourceBridge.TransformRegion(maskRegion, pose);
        using var difference = correctedInspection.Difference(correctedMask);
        using var effectiveDomain = domain.Intersection(difference);
        using var cropped = gray.ReduceDomain(effectiveDomain);
        using var threshold = cropped.Threshold(settings.SegmentationMode == BlobSegmentationMode.None ? 255.0 : settings.Threshold,
            settings.SegmentationMode == BlobSegmentationMode.DoubleThreshold ? settings.UpperThreshold : 255.0);
        using var binary = settings.SegmentationMode != BlobSegmentationMode.None && settings.Type == BlobThresholdMode.BinaryInv
            ? effectiveDomain.Difference(threshold) : threshold.Clone();
        cancellation.ThrowIfCancellationRequested();
        HalconBlobExecutionScope.RequireUnlimitedConnection();
        using var connected = binary.Connection();
        HalconBlobExecutionScope.RequireUnlimitedConnection();
        using var selected = SelectFeatures(connected, settings);
        var candidates = ExtractCandidates(selected, settings, cancellation);
        var final = Sort(candidates, settings.SortFeature, settings.SortMode);
        final = final.Take(settings.BlobCountLimit);
        var ordered = final.ToArray();
        var outputResources = resources.CreateScope();
        try
        {
            bool boundaryRequested = settings.OutputBoundary || requested.HasFlag(BlobOutputRequest.Boundary);
            var measurements = new List<BlobMeasurement>(ordered.Length);
            foreach (var item in ordered)
            {
                cancellation.ThrowIfCancellationRequested();
                using var native = selected.SelectObj(item.Index);
                var region = HalconResourceBridge.CaptureRegion(native, BlobSchema.ImageFrame, frame, outputResources);
                var boundary = boundaryRequested ? ReadBoundary(native, cancellation) : Array.Empty<BlobBoundaryPath>();
                measurements.Add(new(item.Index, measurements.Count + 1, item.Area, item.Perimeter, item.X, item.Y, item.Circularity,
                    item.Rectangularity, item.Bounds, Array.AsReadOnly(item.Corners), Array.AsReadOnly(boundary), region, item.IsPass));
            }
            using var finalRegions = SelectRegions(selected, ordered.Select(x => x.Index).ToArray());
            using var okRegions = SelectRegions(selected, ordered.Where(x => x.IsPass).Select(x => x.Index).ToArray());
            using var ngRegions = SelectRegions(selected, ordered.Where(x => !x.IsPass).Select(x => x.Index).ToArray());
            var regionOutput = HalconResourceBridge.CaptureRegion(finalRegions, BlobSchema.ImageFrame, frame, outputResources);
            var okOutput = HalconResourceBridge.CaptureRegion(okRegions, BlobSchema.ImageFrame, frame, outputResources);
            var ngOutput = HalconResourceBridge.CaptureRegion(ngRegions, BlobSchema.ImageFrame, frame, outputResources);
            var grayOutput = settings.OutputGrayImage || requested.HasFlag(BlobOutputRequest.GrayImage) ? HalImageSnapshotSource.Capture(gray, frame, BlobSchema.ImageFrame, outputResources) : null;
            IReadOnlyImage? binaryOutput = null;
            if (settings.OutputBinaryImage || requested.HasFlag(BlobOutputRequest.BinaryImage))
            {
                HOperatorSet.RegionToBin(binary, out var binObject, 255, 0, width, height);
                using (binObject) using (var binImage = new HImage(binObject)) binaryOutput = HalImageSnapshotSource.Capture(binImage, frame, BlobSchema.ImageFrame, outputResources);
            }
            HOperatorSet.AreaCenter(binary, out var whiteCount, out var whiteRow, out var whiteColumn);
            using var white = whiteCount; using var wr = whiteRow; using var wc = whiteColumn;
            double whiteRatio = whiteCount.D / ((double)width * height) * 100;
            var histogram = new double[256]; var means = new double[] { 0 };
            if (whiteCount.D > 0)
            {
                HOperatorSet.GrayHisto(binary, gray, out var absolute, out var relative);
                using (absolute) using (relative) histogram = absolute.LArr.Select(x => (double)x).ToArray();
                HOperatorSet.Intensity(binary, gray, out var mean, out var deviation);
                using (mean) using (deviation) means = new[] { mean.D };
            }
            var messages = new List<string>();
            var isPass = ordered.All(x => x.IsPass);
            foreach (var rule in settings.Judgments.Where(x => x.Enabled && !x.PerItem))
            {
                double value = rule.Key switch {
                    "output-not-empty" => ordered.Length > 0 ? 1 : 0, "white-ratio" => whiteRatio, "blob-count" => ordered.Length,
                    "total-area" => ordered.Sum(x => x.Area), "average-area" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Area),
                    "max-area" => ordered.Length == 0 ? 0 : ordered.Max(x => x.Area), "min-area" => ordered.Length == 0 ? 0 : ordered.Min(x => x.Area),
                    "average-circularity" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Circularity),
                    "average-rectangularity" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Rectangularity),
                    _ => throw new ArgumentException("Unknown overall Blob judgment: " + rule.Key)
                };
                bool passed = Judge(rule, value);
                isPass &= passed; messages.Add($"[{(passed ? "OK" : "NG")}] {rule.Key}: {value}");
            }
            if (ordered.Any(x => !x.IsPass)) messages.Add($"[NG] {ordered.Count(x => !x.IsPass)} Blob未通过单项判断。");
            cancellation.ThrowIfCancellationRequested();
            var output = new BlobOutput(settings, measurements, grayOutput, binaryOutput, regionOutput, okOutput, ngOutput, whiteRatio, histogram, means, isPass, messages, new[] { outputResources });
            _ = new InputValues(BlobSchema.Outputs, output.ToValues());
            return output;
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private static HRegion SelectFeatures(HRegion regions, BlobSettings s)
    {
        var features = new List<string>(); var minima = new List<double>(); var maxima = new List<double>();
        void Add(bool enabled, string name, double min, double max) { if (enabled) { features.Add(name); minima.Add(min); maxima.Add(max); } }
        Add(s.IsAreaFilterEnabled, "area", s.FilterAreaMin, s.FilterAreaMax);
        Add(s.IsCircularityFilterEnabled, "circularity", s.FilterCircularityMin, s.FilterCircularityMax);
        Add(s.IsRectangularityFilterEnabled, "rectangularity", s.FilterRectangularityMin, s.FilterRectangularityMax);
        if (features.Count == 0) return regions.Clone();
        using var f = new HTuple(features.ToArray()); using var min = new HTuple(minima.ToArray()); using var max = new HTuple(maxima.ToArray());
        return regions.SelectShape(f, "and", min, max);
    }

    private static Candidate[] ExtractCandidates(HRegion regions, BlobSettings s, CancellationToken cancellation)
    {
        HOperatorSet.AreaCenter(regions, out var areas, out var rows, out var cols);
        using var a = areas; using var r = rows; using var c = cols;
        HOperatorSet.SmallestRectangle1(regions, out var r1, out var c1, out var r2, out var c2);
        using var rb = r1; using var cb = c1; using var re = r2; using var ce = c2;
        using var circularity = regions.Circularity(); using var rectangularity = regions.Rectangularity(); using var perimeter = regions.Contlength();
        HOperatorSet.SmallestRectangle2(regions, out var rr, out var rc, out var phi, out var length1, out var length2);
        using var rrr = rr; using var rrc = rc; using var ph = phi; using var l1 = length1; using var l2 = length2;
        var result = new List<Candidate>();
        for (int i = 0; i < areas.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (areas[i].D <= 0) continue;
            double circ = Math.Min(1, circularity[i].D), rect = Math.Min(1, rectangularity[i].D);
            bool pass = true;
            foreach (var rule in s.Judgments.Where(x => x.Enabled && x.PerItem))
            {
                double value = rule.Key switch { "per-blob-area" => areas[i].D, "per-blob-circularity" => circ, "per-blob-rectangularity" => rect,
                    _ => throw new ArgumentException("Unknown per-Blob judgment: " + rule.Key) };
                pass &= Judge(rule, value);
            }
            result.Add(new(i + 1, areas[i].D, perimeter[i].D, cols[i].D, rows[i].D, circ, rect,
                new(c1[i].D, r1[i].D, c2[i].D - c1[i].D + 1, r2[i].D - r1[i].D + 1),
                BuildRotatedRectangle(rr[i].D, rc[i].D, phi[i].D, length1[i].D, length2[i].D), pass));
        }
        return result.ToArray();
    }

    private static bool Judge(BlobJudgment rule, double value) => rule.Kind switch {
        BlobJudgmentKind.Range => value >= rule.Minimum && value <= rule.Maximum,
        BlobJudgmentKind.Boolean => value != 0,
        _ => false
    };

    // 本方明确策略：以真实已计算特征排序，同值保持HALCON候选顺序，再执行数量限制。
    // 原声明已证特征/方向，原生内核同值规则及数值等价尚未证明。
    private static IEnumerable<Candidate> Sort(IEnumerable<Candidate> source, BlobSortFeature feature, BlobSortMode mode)
    {
        Func<Candidate,double> key = feature switch {
            BlobSortFeature.SortFeatureArea => value=>value.Area,
            BlobSortFeature.SortFeaturePerimeter => value=>value.Perimeter,
            BlobSortFeature.SortFeatureCircularity => value=>value.Circularity,
            BlobSortFeature.SortFeatureRect => value=>value.Rectangularity,
            BlobSortFeature.SortFeatureCentroidX => value=>value.X,
            BlobSortFeature.SortFeatureCentroidY => value=>value.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(feature)) };
        return mode switch {
            BlobSortMode.SortModeAscend => source.OrderBy(key),
            BlobSortMode.SortModeDecend => source.OrderByDescending(key),
            BlobSortMode.SortModeNotSort => source,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    }
}
