using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

[Flags]
public enum BlobOutputRequest { None = 0, BlobImage = 1, BinaryImage = 2, Boundary = 4 }

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
            if(!BlobThresholdCapabilities.IsSupported(invocation.Settings.SegmentationMode))
                return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_MODE_NOT_READY","该阈值模式尚未完成原厂语义对齐。"));
            var schema=new BlobTool().Describe();
            var requested=BlobOutputRequest.None;
            foreach(var demand in invocation.OutputRequests)
            {
                demand.Validate(schema);
                if(demand.PortId==BlobSchema.Field(5))requested|=BlobOutputRequest.BlobImage;
                if(demand.PortId==BlobSchema.Field(6))requested|=BlobOutputRequest.BinaryImage;
                if(demand.PortId==BlobSchema.BlobsPort&&(demand.FieldPath.Count==0||demand.FieldPath[0] is "boundary" or "contour" or "contour-lossy"))requested|=BlobOutputRequest.Boundary;
            }
            var input=BlobInput.Read(invocation.Inputs);
            var output=Execute(invocation.Settings,input,invocation.Context.Resources,invocation.Context.Cancellation,requested);
            IReadOnlyList<Diagnostic> diagnostics=output.Measurements.Any(blob=>blob.Boundary.Count>1)
                ?new[]{new Diagnostic("BLOB_CONTOUR_LOSSY","单轮廓仅投影一个外环；多环和孔洞应读取boundary/Region。",BlobSchema.BlobsPort)}
                :Array.Empty<Diagnostic>();
            return ValueTask.FromResult(new ToolOutcome<BlobOutput>(true,output,output.IsPass?BusinessJudgment.Pass:BusinessJudgment.Fail,output.ExecutionDiagnostics.Concat(diagnostics).ToArray()));
        }
        catch(BlobCapabilityException e) { return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed(e.Code,e.Message)); }
        catch(OperationCanceledException) { return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_CANCELLED","Blob执行已取消。")); }
        catch(Exception e) { return ValueTask.FromResult(ToolOutcome<BlobOutput>.Failed("BLOB_EXECUTION_FAILED",e.Message)); }
    }
    private sealed record Candidate(int RegionIndex,int ComponentLabel, double Area, double Perimeter, double X, double Y,
        double Circularity, double Rectangularity, BlobBounds Bounds, BlobOrientedBounds OrientedBounds, Point2[] Corners, bool IsPass);

    public BlobOutput Execute(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection = null,
        IReadOnlyRegion? mask = null, RecordValue? pose = null, CancellationToken cancellation = default,
        BlobOutputRequest requested = BlobOutputRequest.None,RecordValue? localRoi=null)
        =>ExecuteCore(settings,input,resources,inspection,mask,pose,cancellation,requested,localRoi,16);
    public BlobOutput Execute(BlobSettings settings,BlobInput input,IOutputResources resources,CancellationToken cancellation=default,BlobOutputRequest requested=BlobOutputRequest.None)
        =>ExecuteCore(settings,input.Image,resources,input.Inspection,input.Mask,input.Pose,cancellation,requested,input.LocalRoi,input.IsInheritedRoi?64:16);
    private BlobOutput ExecuteCore(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection,
        IReadOnlyRegion? mask, RecordValue? pose, CancellationToken cancellation,BlobOutputRequest requested,RecordValue? localRoi,int maximumInspectionCount)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(resources);
        settings = settings.Freeze();
        // The VM parameter object is the single execution snapshot. The public
        // host fields are projected into it by Freeze, so execution never reads
        // a partially updated editor model.
        var vm = settings.ToVmParameters();
        // VM exposes contour output as a typed mode. The host still keeps the
        // legacy Boolean output gate for existing documents, so derive one
        // frozen gate before any result/resource path is selected.
        if(vm.BlobContourType != BlobVmContourType.NoneContour)
            settings = settings with { OutputBoundary = true };
        if (!BlobThresholdCapabilities.IsSupported(settings.SegmentationMode))
            throw new InvalidOperationException("BLOB_MODE_NOT_READY: 该阈值模式尚未完成原厂语义对齐。");
        var errors = settings.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors.Select(x => x.Message)));
        if ((requested & ~(BlobOutputRequest.BlobImage | BlobOutputRequest.BinaryImage | BlobOutputRequest.Boundary)) != 0)
            throw new ArgumentException("Unknown Blob output request.", nameof(requested));
        cancellation.ThrowIfCancellationRequested();
        using var execution = new HalconBlobExecutionScope(settings.Connectivity);
        using var image = HalconResourceBridge.CopyImage(input);
        HOperatorSet.GetImageSize(image, out var widthValue, out var heightValue);
        using var widthTuple = widthValue; using var heightTuple = heightValue;
        int width = widthValue.I, height = heightValue.I;
        // Capture the same frozen input frame for every output and ROI reader admission.
        var frame = input.Frame;
        if(localRoi is not null)
        {
            BlobLocalRoi.Validate(localRoi,maximumInspectionCount);
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
        using var inspectionRegion = localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Inspection,domain,cancellation,maximumInspectionCount)
            :inspection is null ? domain.Clone() : HalconResourceBridge.CopyRegion(inspection, frame, width, height);
        using var maskRegion = localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Mask,domain,cancellation,maximumInspectionCount)
            :mask is null ? EmptyRegion() : HalconResourceBridge.CopyRegion(mask, frame, width, height);
        using var correctedInspection = inspection is null ? inspectionRegion.Clone() : HalconResourceBridge.TransformRegion(inspectionRegion, pose);
        using var correctedMask = HalconResourceBridge.TransformRegion(maskRegion, pose);
        using var difference = correctedInspection.Difference(correctedMask);
        using var effectiveDomain = domain.Intersection(difference);
        var transformed=BlobThresholdTransform.Apply(gray,effectiveDomain,settings,width,height,cancellation);
        using var work=transformed.Work;
        var outputResources=resources.CreateScope();
        try
        {
            // Freeze the actual byte work image before region extraction; soft intermediate values survive.
            var binaryOutput=settings.OutputBinaryImage||requested.HasFlag(BlobOutputRequest.BinaryImage)
                ?HalImageSnapshotSource.Capture(work,frame,BlobSchema.ImageFrame,outputResources):null;
            using var binary=work.Threshold(1d,255d);
            cancellation.ThrowIfCancellationRequested();
            HalconBlobExecutionScope.RequireUnlimitedConnection();
            using var connected=binary.Connection();
            HalconBlobExecutionScope.RequireUnlimitedConnection();
            var candidates=FilterCandidates(connected,ExtractCandidates(connected,settings,cancellation),vm,width,height,cancellation);
            Func<Candidate,int>? integerKey=settings.SortFeature is BlobSortFeature.SortFeatureRectX or BlobSortFeature.SortFeatureRectY?value=>SortIntegerKey(value,settings.SortFeature):null;
            var retained=BlobCandidateSelection.SelectSlots(candidates,settings.BlobCountLimit,settings.SortFeature,settings.SortMode,value=>(float)value.Area,value=>SortFloatKey(value,settings.SortFeature),integerKey,cancellation);
            var executionDiagnostics=new List<Diagnostic>();
            if(transformed.Receipt.Provenance=="LocalScalarFormulaCalculation")
                executionDiagnostics.Add(new("BLOB_THRESHOLD_LOCAL_CALCULATION","本轮阈值由本方按已核原标量公式计算；原厂动态阈值对外发布接口尚未证实。",BlobSchema.Field(8)));
            IReadOnlyImage? blobImage=null;
            if(settings.OutputBlobImage||requested.HasFlag(BlobOutputRequest.BlobImage))
            {
                // BlobImage is the retained candidate set after every enabled
                // selector.  The previous gate rejected circularity and
                // rectangularity filters even though the retained region list
                // already represented their exact result.
                using var retainedRegions=SelectRegions(connected,retained.Select(value=>value.Candidate.RegionIndex).ToArray());
                using var candidateImage=BlobThresholdTransform.RegionImage(retainedRegions,width,height);
                blobImage=HalImageSnapshotSource.Capture(candidateImage,frame,BlobSchema.ImageFrame,outputResources);
            }
            var ordered=BlobCandidateSelection.Order(retained,settings.SortMode,value=>SortFloatKey(value,settings.SortFeature),
                integerKey,value=>value.ComponentLabel,cancellation);
            bool boundaryRequested = settings.OutputBoundary || requested.HasFlag(BlobOutputRequest.Boundary);
            var measurements = new List<BlobMeasurement>(ordered.Length);
            foreach (var selection in ordered)
            {
                var item=selection.Candidate;
                cancellation.ThrowIfCancellationRequested();
                using var native = connected.SelectObj(item.RegionIndex);
                var region = HalconResourceBridge.CaptureRegion(native, BlobSchema.ImageFrame, frame, outputResources);
                var boundary = boundaryRequested ? ReadBoundary(native, cancellation) : Array.Empty<BlobBoundaryPath>();
                HOperatorSet.MinMaxGray(native, gray, 0, out var minGray, out var maxGray, out var grayRange);
                using (minGray) using (maxGray) using (grayRange)
                measurements.Add(new(item.ComponentLabel, measurements.Count + 1, item.Area, item.Perimeter, item.X, item.Y, item.Circularity,
                    item.Rectangularity, item.Bounds, item.OrientedBounds, Array.AsReadOnly(item.Corners), Array.AsReadOnly(boundary), region, item.IsPass,
                    minGray.D, maxGray.D, grayRange.D));
            }
            using var finalRegions = SelectRegions(connected, ordered.Select(x => x.Candidate.RegionIndex).ToArray());
            using var okRegions = SelectRegions(connected, ordered.Where(x => x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            using var ngRegions = SelectRegions(connected, ordered.Where(x => !x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            var regionOutput = HalconResourceBridge.CaptureRegion(finalRegions, BlobSchema.ImageFrame, frame, outputResources);
            var okOutput = HalconResourceBridge.CaptureRegion(okRegions, BlobSchema.ImageFrame, frame, outputResources);
            var ngOutput = HalconResourceBridge.CaptureRegion(ngRegions, BlobSchema.ImageFrame, frame, outputResources);
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
            var isPass = ordered.All(x => x.Candidate.IsPass);
            if (!EvaluateVmResultLimits(vm, ordered.Select(x => x.Candidate).ToArray(), whiteRatio, width, height, out var vmLimitMessages))
            {
                isPass = false;
                messages.AddRange(vmLimitMessages);
            }
            if (vm.OKWhenNumIsZero && ordered.Length == 0)
                isPass = true;
            foreach (var rule in settings.Judgments.Where(x => x.Enabled && !x.PerItem))
            {
                double value = rule.Key switch {
                    "output-not-empty" => ordered.Length > 0 ? 1 : 0, "white-ratio" => whiteRatio, "blob-count" => ordered.Length,
                    "total-area" => ordered.Sum(x => x.Candidate.Area), "average-area" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Candidate.Area),
                    "max-area" => ordered.Length == 0 ? 0 : ordered.Max(x => x.Candidate.Area), "min-area" => ordered.Length == 0 ? 0 : ordered.Min(x => x.Candidate.Area),
                    "average-circularity" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Candidate.Circularity),
                    "average-rectangularity" => ordered.Length == 0 ? 0 : ordered.Average(x => x.Candidate.Rectangularity),
                    _ => throw new ArgumentException("Unknown overall Blob judgment: " + rule.Key)
                };
                bool passed = Judge(rule, value);
                isPass &= passed; messages.Add($"[{(passed ? "OK" : "NG")}] {rule.Key}: {value}");
            }
            if (ordered.Any(x => !x.Candidate.IsPass)) messages.Add($"[NG] {ordered.Count(x => !x.Candidate.IsPass)} Blob未通过单项判断。");
            cancellation.ThrowIfCancellationRequested();
            var output = new BlobOutput(settings, transformed.Receipt, executionDiagnostics, ordered.Select((entry,index)=>new BlobSelectionEntry(entry.Candidate.ComponentLabel,entry.Candidate.RegionIndex,entry.Slot,index+1)), measurements, blobImage, binaryOutput, regionOutput, okOutput, ngOutput, whiteRatio, histogram, means, isPass, messages, new[] { outputResources },
                BlobResultGeometry.Capture(localRoi,input,isPass,settings.IsPositionCorrectionEnabled,inspection is not null,mask is not null));
            _ = new InputValues(BlobSchema.Outputs, output.ToValues());
            return output;
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private static Candidate[] FilterCandidates(HRegion regions,Candidate[] candidates,BlobVmFindParameters vm,int width,int height,CancellationToken cancellation)
    {
        var features=new List<string>();var minima=new List<double>();var maxima=new List<double>();
        if(vm.SelectByCircularuty){features.Add("circularity");minima.Add(vm.MinCircularity);maxima.Add(vm.MaxCircularity);}
        if(vm.SelectByRectangularity){features.Add("rectangularity");minima.Add(vm.MinRectangularity);maxima.Add(vm.MaxRectangularity);}
        using var names=new HTuple(features.ToArray());using var lower=new HTuple(minima.ToArray());using var upper=new HTuple(maxima.ToArray());
        var accepted=new List<Candidate>();
        foreach(var candidate in candidates)
        {
            cancellation.ThrowIfCancellationRequested();
            if(vm.SelectByArea&&!AreaAccepted(NativeAreaInteger(candidate.Area),vm.MinArea,vm.MaxArea))continue;
            var oriented=candidate.OrientedBounds;
            var shortAxis=Math.Min(oriented.Width,oriented.Height);
            var longAxis=Math.Max(oriented.Width,oriented.Height);
            var axisRatio=longAxis<=0?0:shortAxis/longAxis;
            var centerBias=Math.Sqrt(Math.Pow(candidate.X-width/2d,2)+Math.Pow(candidate.Y-height/2d,2));
            if(vm.SelectByBoxAngle&&!InRange(oriented.AngleDegrees,vm.MinBoxAngle,vm.MaxBoxAngle))continue;
            if(vm.SelectByRectWidth&&!InRange(oriented.Width,vm.MinRectWidth,vm.MaxRectWidth))continue;
            if(vm.SelectByRectHeight&&!InRange(oriented.Height,vm.MinRectHeight,vm.MaxRectHeight))continue;
            if(vm.SelectByPerimeter&&!InRange(candidate.Perimeter,vm.MinPerimeter,vm.MaxPerimeter))continue;
            if(vm.SelectByShortAxis&&!InRange(shortAxis,vm.MinShortAxis,vm.MaxShortAxis))continue;
            if(vm.SelectByLongAxis&&!InRange(longAxis,vm.MinLongAxis,vm.MaxLongAxis))continue;
            if(vm.SelectByCircularuty&&!InRange(candidate.Circularity,vm.MinCircularity,vm.MaxCircularity))continue;
            if(vm.SelectByRectangularity&&!InRange(candidate.Rectangularity,vm.MinRectangularity,vm.MaxRectangularity))continue;
            if(vm.SelectByCentraBias&&!InRange(centerBias,vm.MinCenterBias,vm.MaxCenterBias))continue;
            if(vm.AxisRatioEnable&&!InRange(axisRatio,vm.MinAxisRatio,vm.MaxAxisRatio))continue;
            if(features.Count!=0)
            {
                using var original=regions.SelectObj(candidate.RegionIndex);using var filtered=original.SelectShape(names,"and",lower,upper);
                if(filtered.CountObj()==0)continue;
            }
            accepted.Add(candidate);
        }
        return accepted.ToArray();
    }

    private static bool EvaluateVmResultLimits(BlobVmFindParameters vm,Candidate[] candidates,double whiteRatio,int width,int height,out IReadOnlyList<string> messages)
    {
        var failures=new List<string>();
        bool Range(double value,double low,double high)=>BlobVmFindParameters.InRange(value,low,high);
        if(vm.BlobNumLimitEnable&&!Range(candidates.Length,vm.BlobNumLimitLow,vm.BlobNumLimitHigh))failures.Add("[NG] BlobNumLimit");
        if(vm.BlobTotalAreaLimitEnable&&!Range(candidates.Sum(x=>x.Area),vm.BlobTotalAreaLimitLow,vm.BlobTotalAreaLimitHigh))failures.Add("[NG] BlobTotalAreaLimit");
        if(vm.BlobAreaLimitEnable&&candidates.Any(x=>!Range(x.Area,vm.BlobAreaLimitLow,vm.BlobAreaLimitHigh)))failures.Add("[NG] BlobAreaLimit");
        if(vm.CentroidXLimitEnable&&candidates.Any(x=>!Range(x.X,vm.CentroidXLimitLow,vm.CentroidXLimitHigh)))failures.Add("[NG] CentroidXLimit");
        if(vm.CentroidYLimitEnable&&candidates.Any(x=>!Range(x.Y,vm.CentroidYLimitLow,vm.CentroidYLimitHigh)))failures.Add("[NG] CentroidYLimit");
        if(vm.AngleLimitEnable&&candidates.Any(x=>!Range(x.OrientedBounds.AngleDegrees,vm.AngleLimitLow,vm.AngleLimitHigh)))failures.Add("[NG] AngleLimit");
        if(vm.CenterXLimitEnable&&candidates.Any(x=>!Range(x.OrientedBounds.CenterX,vm.CenterXLimitLow,vm.CenterXLimitHigh)))failures.Add("[NG] CenterXLimit");
        if(vm.CenterYLimitEnable&&candidates.Any(x=>!Range(x.OrientedBounds.CenterY,vm.CenterYLimitLow,vm.CenterYLimitHigh)))failures.Add("[NG] CenterYLimit");
        if(vm.BoxHeightLimitEnable&&candidates.Any(x=>!Range(x.OrientedBounds.Height,vm.BoxHeightLimitLow,vm.BoxHeightLimitHigh)))failures.Add("[NG] BoxHeightLimit");
        if(vm.BoxWidthLimitEnable&&candidates.Any(x=>!Range(x.OrientedBounds.Width,vm.BoxWidthLimitLow,vm.BoxWidthLimitHigh)))failures.Add("[NG] BoxWidthLimit");
        if(vm.PerimeterLimitEnable&&candidates.Any(x=>!Range(x.Perimeter,vm.PerimeterLimitLow,vm.PerimeterLimitHigh)))failures.Add("[NG] PerimeterLimit");
        if(vm.LongAxisLimitEnable&&candidates.Any(x=>!Range(Math.Max(x.OrientedBounds.Width,x.OrientedBounds.Height),vm.LongAxisLimitLow,vm.LongAxisLimitHigh)))failures.Add("[NG] LongAxisLimit");
        if(vm.ShortAxisLimitEnable&&candidates.Any(x=>!Range(Math.Min(x.OrientedBounds.Width,x.OrientedBounds.Height),vm.ShortAxisLimitLow,vm.ShortAxisLimitHigh)))failures.Add("[NG] ShortAxisLimit");
        if(vm.CircularityLimitEnable&&candidates.Any(x=>!Range(x.Circularity,vm.CircularityLimitLow,vm.CircularityLimitHigh)))failures.Add("[NG] CircularityLimit");
        if(vm.RectangularityLimitEnable&&candidates.Any(x=>!Range(x.Rectangularity,vm.RectangularityLimitLow,vm.RectangularityLimitHigh)))failures.Add("[NG] RectangularityLimit");
        messages=failures;
        return failures.Count==0;
    }

    private static bool InRange(double value,double low,double high)=>BlobVmFindParameters.InRange(value,low,high);
    internal static bool AreaAccepted(int area,int minimum,int maximum)=>minimum<=maximum?area>=minimum&&area<=maximum:area>=minimum||area<=maximum;
    internal static int NativeAreaInteger(double area)
    {
        float original=(float)area;
        if(!float.IsFinite(original)||original<0||original>=2147483648f)
            throw new BlobCapabilityException("BLOB_AREA_NOT_READY","面积转原32位整数超出已核正常范围。");
        return (int)original;
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
            result.Add(new(i + 1,i + 1, areas[i].D, perimeter[i].D, cols[i].D, rows[i].D, circ, rect,
                new(c1[i].D, r1[i].D, c2[i].D - c1[i].D + 1, r2[i].D - r1[i].D + 1),
                new(rc[i].D,rr[i].D,2*length1[i].D,2*length2[i].D,length1[i].D==0&&length2[i].D==0?0:-phi[i].D*180/Math.PI),
                BuildRotatedRectangle(rr[i].D, rc[i].D, phi[i].D, length1[i].D, length2[i].D), pass));
        }
        return result.ToArray();
    }

    private static bool Judge(BlobJudgment rule, double value) => rule.Kind switch {
        BlobJudgmentKind.Range => value >= rule.Minimum && value <= rule.Maximum,
        BlobJudgmentKind.Boolean => value != 0,
        _ => false
    };

    private static float SortFloatKey(Candidate value,BlobSortFeature feature)=>(float)(feature switch
    {
        BlobSortFeature.SortFeatureArea=>value.Area,BlobSortFeature.SortFeaturePerimeter=>value.Perimeter,
        BlobSortFeature.SortFeatureCircularity=>value.Circularity,BlobSortFeature.SortFeatureRect=>value.Rectangularity,
        BlobSortFeature.SortFeatureCentroidX=>value.X,BlobSortFeature.SortFeatureCentroidY=>value.Y,
        BlobSortFeature.SortFeatureRectX=>value.Bounds.X,BlobSortFeature.SortFeatureRectY=>value.Bounds.Y,
        _=>throw new ArgumentOutOfRangeException(nameof(feature))
    });
    private static int SortIntegerKey(Candidate value,BlobSortFeature feature)
    {
        double number=feature==BlobSortFeature.SortFeatureRectX?value.Bounds.X:value.Bounds.Y;
        if(!double.IsFinite(number)||number<0||number>int.MaxValue||Math.Truncate(number)!=number)
            throw new BlobCapabilityException("BLOB_SELECTION_VALUE_NOT_READY","矩形排序坐标超出已核正常整数范围。");
        return (int)number;
    }
}
