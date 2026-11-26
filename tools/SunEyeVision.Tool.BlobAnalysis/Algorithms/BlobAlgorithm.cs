using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Contracts;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

public sealed partial class BlobAlgorithm
{
    private static Point2[] ReadUnorderedContour(HRegion source, CancellationToken cancellation)
    {
        HOperatorSet.Boundary(source, out var region, "inner");
        using var boundary = region;
        HOperatorSet.GetRegionPoints(boundary, out var rows, out var columns);
        using var r = rows;
        using var c = columns;
        var points = new Point2[rows.Length];
        for (var i = 0; i < points.Length; i++)
        {
            if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            points[i] = new Point2(columns[i].D, rows[i].D);
        }
        return points;
    }

    private sealed record NativeLoop(long Id, Point2[] Points, HRegion Filled, double Area,
        double MinX, double MinY, double MaxX, double MaxY);

    private static BlobBoundaryPath[] ReadBoundary(HRegion source, CancellationToken cancellation)
    {
        // border_holes preserves every unsmoothed outer/hole contour. Region remains area truth.
        HOperatorSet.GenContourRegionXld(source, out var contours, "border_holes");
        using var xld = contours;
        HOperatorSet.CountObj(xld, out var count);
        using var n = count;
        var loops = new List<NativeLoop>();
        try
        {
            for (int i = 1; i <= count.I; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(xld, out var selected, i);
                using var single = selected;
                HOperatorSet.GetContourXld(single, out var rows, out var columns);
                using var r = rows; using var c = columns;
                if (rows.Length != columns.Length || rows.Length == 0)
                    throw new InvalidDataException("HALCON returned an invalid boundary path.");
                var points = new Point2[rows.Length];
                var minX = double.PositiveInfinity;
                var minY = double.PositiveInfinity;
                var maxX = double.NegativeInfinity;
                var maxY = double.NegativeInfinity;
                for (int p = 0; p < points.Length; p++)
                {
                    if ((p & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    var x = columns[p].D;
                    var y = rows[p].D;
                    points[p] = new(x, y);
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.GenRegionContourXld(single, out var fillObject, "filled");
                using var nativeFill = fillObject;
                var filled = new HRegion(nativeFill);
                try
                {
                    HOperatorSet.AreaCenter(filled, out var area, out var cy, out var cx);
                    using (area) using (cy) using (cx)
                        loops.Add(new(i, points, filled, area.Length == 0 ? 0 : area[0].D, minX, minY, maxX, maxY));
                }
                catch { filled.Dispose(); throw; }
            }
            var parents = new int?[loops.Count];
            for (int i = 0; i < loops.Count; i++)
            {
                var child = loops[i];
                foreach (var j in Enumerable.Range(0, loops.Count).Where(j => loops[j].Area > child.Area).OrderBy(j => loops[j].Area))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var candidate = loops[j];
                    if (candidate.MinX > child.MinX || candidate.MinY > child.MinY || candidate.MaxX < child.MaxX || candidate.MaxY < child.MaxY) continue;
                    // Actual region containment avoids orientation guesses or a centroid outside a concave ring.
                    using var outside = child.Filled.Difference(candidate.Filled);
                    HOperatorSet.AreaCenter(outside, out var area, out var row, out var column);
                    using (area) using (row) using (column)
                        if (area.Length == 0 || area.LArr.All(x => x == 0)) { parents[i] = j; break; }
                }
            }
            var result = new BlobBoundaryPath[loops.Count];
            for (int i = 0; i < loops.Count; i++)
            {
                int depth = 0; var parent = parents[i];
                while (parent is { } index)
                {
                    if (++depth > loops.Count) throw new InvalidDataException("Boundary parent cycle.");
                    parent = parents[index];
                }
                result[i] = new(loops[i].Id, parents[i] is { } p ? loops[p].Id : null,
                    depth % 2 == 0 ? "outer" : "hole", Array.AsReadOnly(loops[i].Points));
            }
            return result;
        }
        finally { foreach (var loop in loops) loop.Filled.Dispose(); }
    }
}

[Flags]
public enum BlobOutputRequest { None = 0, Boundary = 4 }

/// <summary>One synchronous HALCON invocation; all public inputs/outputs are vendor-neutral.</summary>
public sealed partial class BlobAlgorithm : IToolSession<BlobSettings, BlobOutput>, IReusableToolSession
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
                if(demand.PortId==BlobSchema.BlobsPort&&(demand.FieldPath.Count==0||demand.FieldPath[0] is "boundary" or "contour" or "contour-lossy"))requested|=BlobOutputRequest.Boundary;
            }
            var inputStarted=Stopwatch.GetTimestamp();
            var input=BlobInput.Read(invocation.Inputs, invocation.Context.Log);
            invocation.Context.Log($"[ExecTimingProbe] tool=blob-entry stage=input-read elapsed={Stopwatch.GetElapsedTime(inputStarted).TotalMilliseconds:0.###}ms");
            var output=Execute(invocation.Settings,input,invocation.Context.Resources,invocation.Context.Cancellation,requested,timingLog:invocation.Context.Log);
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
        double Circularity, double Rectangularity, BlobBounds Bounds, BlobOrientedBounds OrientedBounds, double BlobAngleDegrees, Point2[] Corners, bool IsPass);

    public BlobOutput Execute(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection = null,
        IReadOnlyRegion? mask = null, RecordValue? pose = null, CancellationToken cancellation = default,
        BlobOutputRequest requested = BlobOutputRequest.None,RecordValue? localRoi=null,Action<string>? timingLog=null)
        =>ExecuteCore(settings,input,resources,inspection,mask,pose,cancellation,requested,localRoi,BlobLocalRoi.MaximumLocalInspectionCount,timingLog:timingLog);
    public BlobOutput Execute(BlobSettings settings,BlobInput input,IOutputResources resources,CancellationToken cancellation=default,BlobOutputRequest requested=BlobOutputRequest.None,Action<string>? timingLog=null)
        =>ExecuteCore(settings,input.Image,resources,input.Inspection,input.Mask,input.Pose,cancellation,requested,input.LocalRoi,
            input.IsInheritedRoi?BlobLocalRoi.MaximumInheritedCount:BlobLocalRoi.MaximumLocalInspectionCount,input.ImageMask,input.OutputMask,input.Fixture,input.IsInheritedMask,timingLog);
    private BlobOutput ExecuteCore(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection,
        IReadOnlyRegion? mask, RecordValue? pose, CancellationToken cancellation,BlobOutputRequest requested,RecordValue? localRoi,int maximumInspectionCount,IReadOnlyImage? imageMask=null,bool outputMask=false,RecordValue? fixture=null,bool inheritedMask=false,Action<string>? timingLog=null)
    {
        using var timing=BlobDiagnostics.Begin(timingLog??(_=>{}), input.Frame.ProducerRun?.NodeId ?? "unknown");
        timing.Mark("execute-core-start");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(resources);
        settings = settings.Freeze();
        timing.Mark("settings-frozen");
        if (imageMask is not null)
        {
            if (inspection is not null || mask is not null || pose is not null || localRoi is not null)
                throw new ArgumentException("图像区域类型不能混用几何区域或位置修正输入。");
            settings = settings with { IsPositionCorrectionEnabled = false };
        }
        // The VM parameter object is the single execution snapshot. The public
        // host fields are projected into it by Freeze, so execution never reads
        // a partially updated editor model.
        var vm = settings.ToFindParameters();
        timing.Mark("vm-parameters-built");
        if (!BlobThresholdCapabilities.IsSupported(settings.SegmentationMode))
            throw new InvalidOperationException("BLOB_MODE_NOT_READY: 该阈值模式尚未完成原厂语义对齐。");
        var errors = settings.Validate();
        timing.Mark("settings-validated");
        if (errors.Count != 0) throw new BlobCapabilityException("BLOB_SETTINGS_INVALID", string.Join("; ", errors.Select(x => x.Message)));
        // The module stores OverlapRatio for readback. Its execution path does not forward that slot to the SDK.
        if ((requested & ~BlobOutputRequest.Boundary) != 0)
            throw new ArgumentException("Unknown Blob output request.", nameof(requested));
        cancellation.ThrowIfCancellationRequested();
        var moduleStarted=Stopwatch.GetTimestamp();
        if(localRoi is not null)
        {
            BlobLocalRoi.Validate(localRoi,maximumInspectionCount,inheritedMask);
            if(inspection is not null||mask is not null)
                throw new ArgumentException("本地几何不能与像素区域同时执行。");
        }
        timing.Mark("input-contract-validated");
        using var algorithmContext = new HalconBlobAlgorithmContext(settings.Connectivity);
        timing.Mark("halcon-scope-init");
        using var image = HalconResourceBridge.CopyImage(input);
        timing.Mark("image-copy");
        HOperatorSet.GetImageSize(image, out var widthValue, out var heightValue);
        using var widthTuple = widthValue; using var heightTuple = heightValue;
        int width = widthValue.I, height = heightValue.I;
        timing.Mark("image-size-read");
        // Capture the same frozen input frame for every output and ROI reader admission.
        var frame = input.Frame;
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
        timing.Mark("gray-image-domain");
        timing.Mark("gray-domain-ready");
        using var imageMaskRegion = imageMask is null ? null : CreateImageMaskRegion(imageMask, width, height);
        timing.Mark("image-mask-ready");
        using var inspectionRegion = imageMaskRegion is not null ? imageMaskRegion.Clone()
            : localRoi is not null?RoiDocumentRasterizer.Build(localRoi,RoiDocumentValue.Inspection,domain,cancellation,clipToInputDomain:pose is null)
            :inspection is null ? domain.Clone() : HalconResourceBridge.CopyRegion(inspection, frame, width, height);
        using var maskRegion = localRoi is not null?RoiDocumentRasterizer.Build(localRoi,RoiDocumentValue.Mask,domain,cancellation,clipToInputDomain:pose is null)
            :mask is null ? EmptyRegion() : HalconResourceBridge.CopyRegion(mask, frame, width, height);
        var correctInspection = inspection is not null || localRoi is not null && RoiDocumentValue.Coverage(localRoi,RoiDocumentValue.Inspection)==RoiDocumentValue.Explicit;
        using var correctedInspection = correctInspection ? HalconResourceBridge.TransformRegion(inspectionRegion, pose) : inspectionRegion.Clone();
        using var correctedMask = HalconResourceBridge.TransformRegion(maskRegion, pose);
        using var difference = correctedInspection.Difference(correctedMask);
        using var effectiveDomain = domain.Intersection(difference);
        timing.Mark("roi-mask-domain-ready");
        // The C03 direct-SDK comparison removes threshold foreground on the top row and left column.
        // Keep the geometric ROI and generated output mask on the nominal domain.
        using var directRectangleInterior = width > 2 && height > 2 && pose is null && localRoi is not null
            && settings.SegmentationMode == BlobSegmentationMode.SingleThreshold && settings.Type == BlobThresholdMode.Binary
            && RoiDocumentRasterizer.IsDirectFullFrameRectangle(localRoi, width, height)
                ? HalconResourceBridge.CreateWithoutImplicitClipping(() => new HRegion(1d, 1d, height - 1d, width - 1d))
                : null;
        using var thresholdDomain = directRectangleInterior is null ? null : effectiveDomain.Intersection(directRectangleInterior);
        var transformed=BlobThresholdTransform.Apply(gray,thresholdDomain ?? effectiveDomain,settings,width,height,cancellation);
        timing.Mark("threshold-segmentation");
        using var work=transformed.Work;
        using var binary=transformed.Foreground;
        var outputResources=resources.CreateScope();
        timing.Mark("output-resource-scope");
        try
        {
            IReadOnlyImage? maskOutput = null;
            if (outputMask)
            {
                using var maskImage = BlobThresholdTransform.RegionImage(effectiveDomain, width, height);
                maskOutput = HalImageSnapshotSource.Capture(maskImage, frame, BlobSchema.ImageFrame, outputResources);
            }
            // Freeze the actual byte work image before region extraction; soft intermediate values survive.
            var binaryOutput=work is not null
                ?HalImageSnapshotSource.Capture(work,frame,BlobSchema.ImageFrame,outputResources):null;
            timing.Mark("binary-output-snapshot");
            var executionDiagnostics=new List<Diagnostic>();
            if (vm.MaxAreaInnerRectNum > 0)
                executionDiagnostics.Add(new("BLOB_INNER_RECT_LOCAL", "内接矩形使用 HALCON 轴对齐求解，原厂旋转策略和输出分配未确认。", BlobSchema.InnerRectanglesPort));
            // 填充面积阈值：0 跳过；T>0 填面积不超过 T 的封闭孔洞，填孔结果参与连通域与后续测量。
            using var holeFilled=vm.HoleMinArea>0?FillUpHoles(binary,thresholdDomain??effectiveDomain,vm.HoleMinArea,cancellation):null;
            if(holeFilled is not null)
                executionDiagnostics.Add(new("BLOB_HOLE_FILL_LOCAL","填充面积阈值按已核分支实现（0跳过、面积不超过阈值、贴工作区边界不填充）；原机逐像素等价未证。"));
            var blobRegion=holeFilled??binary;
            timing.Mark("hole-fill");
            cancellation.ThrowIfCancellationRequested();
            HalconBlobAlgorithmContext.RequireUnlimitedConnection();
            using var connected=blobRegion.Connection();
            timing.Mark("connection");
            HalconBlobAlgorithmContext.RequireUnlimitedConnection();
            using var contactBoundary=vm.MaxOutPixelNumEnable?BuildContactBoundary(effectiveDomain):null;
            var extracted=ExtractCandidates(connected,cancellation);
            timing.Mark("candidates-extract");
            var candidates=FilterCandidates(connected,extracted,vm,width,height,contactBoundary,cancellation);
            timing.Mark("candidate-count=" + candidates.Length);
            Func<Candidate,int>? integerKey=settings.SortFeature is BlobSortFeature.SortFeatureRectX or BlobSortFeature.SortFeatureRectY?value=>SortIntegerKey(value,settings.SortFeature):null;
            var retained=BlobCandidateSelection.SelectSlots(candidates,settings.BlobCountLimit,settings.SortFeature,settings.SortMode,value=>(float)value.Area,value=>SortFloatKey(value,settings.SortFeature),integerKey,cancellation);
            timing.Mark("retained-count=" + retained.Count());
            if(transformed.Receipt.Provenance=="LocalScalarFormulaCalculation")
                executionDiagnostics.Add(new("BLOB_THRESHOLD_LOCAL_CALCULATION","本轮阈值由本方按已核原标量公式计算；原厂动态阈值对外发布接口尚未证实。",BlobSchema.Field(8)));
            var ordered=BlobCandidateSelection.Order(retained,settings.SortMode,value=>SortFloatKey(value,settings.SortFeature),
                integerKey,value=>value.ComponentLabel,cancellation);
            timing.Mark("filter-sort");
            // VM 判定组语义：启用的判定组逐 Blob 给出单项 OK/NG；筛选只决定保留集合，不等于合格。
            var perBlobPass=ResultProjectionLimitsPerBlob(vm,ordered.Select(x=>x.Candidate).ToArray(),out var overallPass,out var vmLimitMessages);
            timing.Mark("result-projection");
            var judged=ordered.Select((entry,index)=>entry with{Candidate=entry.Candidate with{IsPass=perBlobPass[index]}}).ToArray();
            timing.Mark("candidate-judgment");
            var messages=new List<string>(vmLimitMessages);
            var isPass=(vm.OKWhenNumIsZero?judged.Length==0:judged.Length>0)
                &&overallPass&&judged.All(x=>x.Candidate.IsPass);
            bool unorderedContour = vm.BlobContourType == BlobContourType.NoneSequentialContour;
            bool boundaryRequested = vm.BlobContourType == BlobContourType.SequentialContour
                || requested.HasFlag(BlobOutputRequest.Boundary);
            var measurements = new List<BlobMeasurement>(judged.Length);
            var regionCaptureClock = Stopwatch.StartNew();
            var boundaryClock = Stopwatch.StartNew();
            var grayStatisticsClock = Stopwatch.StartNew();
            regionCaptureClock.Stop(); boundaryClock.Stop(); grayStatisticsClock.Stop();
            foreach (var selection in judged)
            {
                var item=selection.Candidate;
                cancellation.ThrowIfCancellationRequested();
                using var native = connected.SelectObj(item.RegionIndex);
                regionCaptureClock.Start();
                var region = HalconResourceBridge.CaptureRegion(native, BlobSchema.ImageFrame, frame, outputResources);
                regionCaptureClock.Stop();
                boundaryClock.Start();
                var boundary = boundaryRequested ? ReadBoundary(native, cancellation) : Array.Empty<BlobBoundaryPath>();
                var contour = unorderedContour ? Array.AsReadOnly(ReadUnorderedContour(native, cancellation)) : null;
                var innerRectangle = measurements.Count < vm.MaxAreaInnerRectNum ? ReadInnerRectangle(native) : (BlobOrientedBounds?)null;
                boundaryClock.Stop();
                grayStatisticsClock.Start();
                var grayStatistics=ReadGrayStatistics(native,gray);
                grayStatisticsClock.Stop();
                measurements.Add(new(item.ComponentLabel, measurements.Count, item.Area, item.Perimeter, item.X, item.Y, item.Circularity,
                    item.Rectangularity, item.Bounds, item.OrientedBounds, Array.AsReadOnly(item.Corners), Array.AsReadOnly(boundary), region, item.IsPass,
                    grayStatistics.Minimum, grayStatistics.Maximum, grayStatistics.Contrast, item.BlobAngleDegrees, contour, innerRectangle));
            }
            timing.Mark("per-blob-region-capture=" + regionCaptureClock.ElapsedMilliseconds + "ms");
            timing.Mark("per-blob-boundary=" + boundaryClock.ElapsedMilliseconds + "ms");
            timing.Mark("per-blob-gray-statistics=" + grayStatisticsClock.ElapsedMilliseconds + "ms");
            timing.Mark("per-blob-measure");
            var finalRegionClock = Stopwatch.StartNew();
            using var finalRegions = SelectRegions(connected, judged.Select(x => x.Candidate.RegionIndex).ToArray());
            finalRegionClock.Stop();
            IReadOnlyImage? blobImage=null;
            var blobImageClock = Stopwatch.StartNew();
            if(settings.OutputBlobImage)
            {
                using var candidateImage=BlobThresholdTransform.RegionImage(finalRegions,width,height);
                blobImage=HalImageSnapshotSource.Capture(candidateImage,frame,BlobSchema.ImageFrame,outputResources);
            }
            blobImageClock.Stop();
            var acceptedRegionClock = Stopwatch.StartNew();
            using var okRegions = SelectRegions(connected, judged.Where(x => x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            using var ngRegions = SelectRegions(connected, judged.Where(x => !x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            acceptedRegionClock.Stop();
            var regionOutputClock = Stopwatch.StartNew();
            var regionOutput = HalconResourceBridge.CaptureRegion(finalRegions, BlobSchema.ImageFrame, frame, outputResources);
            var okOutput = HalconResourceBridge.CaptureRegion(okRegions, BlobSchema.ImageFrame, frame, outputResources);
            var ngOutput = HalconResourceBridge.CaptureRegion(ngRegions, BlobSchema.ImageFrame, frame, outputResources);
            regionOutputClock.Stop();
            timing.Mark("region-select=" + finalRegionClock.ElapsedMilliseconds + "ms");
            timing.Mark("blob-image=" + blobImageClock.ElapsedMilliseconds + "ms");
            timing.Mark("accepted-region-select=" + acceptedRegionClock.ElapsedMilliseconds + "ms");
            timing.Mark("region-capture=" + regionOutputClock.ElapsedMilliseconds + "ms");
            timing.Mark("region-snapshots");
            HOperatorSet.AreaCenter(blobRegion, out var whiteCount, out var whiteRow, out var whiteColumn);
            using var white = whiteCount; using var wr = whiteRow; using var wc = whiteColumn;
            double whiteRatio = whiteCount.D / ((double)width * height) * 100;
            // 直方图信息输出为开关参数：未勾选不计算、不输出。
            double[] histogram = Array.Empty<double>(); double[] means = Array.Empty<double>();
            if (vm.SelectByHistInfo)
            {
                histogram = new double[256]; means = new double[] { 0 };
                if (whiteCount.D > 0)
                {
                    HOperatorSet.GrayHisto(blobRegion, gray, out var absolute, out var relative);
                    using (absolute) using (relative) histogram = absolute.LArr.Select(x => (double)x).ToArray();
                    HOperatorSet.Intensity(blobRegion, gray, out var mean, out var deviation);
                    using (mean) using (deviation) means = new[] { mean.D };
                }
            }
            timing.Mark("histogram-output");
            cancellation.ThrowIfCancellationRequested();
            var maskPolygons = BlobMaskInheritance.Publish(localRoi,pose);
            var resultGeometry = BlobResultGeometry.Capture(localRoi, input, isPass, domain, correctedInspection, correctedMask, pose,
                inspection is not null || imageMask is not null, mask is not null, maskPolygons, cancellation, fixture);
            if (resultGeometry.Groups.Any(group => group.Coverage == ResultGeometryCoverage.Unavailable))
                executionDiagnostics.Add(new("BLOB_ROI_DISPLAY_UNAVAILABLE", "部分像素区域无法在当前边界预算内完整显示。"));
            timing.Mark("result-geometry");
            var output = new BlobOutput(settings, transformed.Receipt, executionDiagnostics, judged.Select((entry,index)=>new BlobSelectionEntry(entry.Candidate.ComponentLabel,entry.Candidate.RegionIndex,entry.Slot,index+1)), measurements, blobImage, binaryOutput, maskOutput, regionOutput, okOutput, ngOutput, whiteRatio, histogram, means, isPass, messages, new[] { outputResources }, resultGeometry,
                maskPolygons, vm.BlobContourType!=BlobContourType.NoneContour,cancellation,moduleStarted);
            cancellation.ThrowIfCancellationRequested();
            timing.Mark("blob-output-constructed");
            return output;
        }
        catch (Exception failure)
        {
            try { outputResources.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    private static BlobOrientedBounds ReadInnerRectangle(HRegion region)
    {
        region.InnerRectangle1(out int top, out int left, out int bottom, out int right);
        // HALCON reports inclusive pixel centers. Width and height describe the full pixel footprint.
        return new(left + (right - left) / 2d, top + (bottom - top) / 2d, right - left + 1d, bottom - top + 1d, 0d);
    }

    private static (double Minimum,double Maximum,double Contrast) ReadGrayStatistics(HRegion region,HImage image)
    {
        HOperatorSet.GrayHisto(region,image,out var absolute,out var relative);
        using var counts=absolute;
        using var fractions=relative;
        var bins=counts.LArr;
        if(bins.Length!=256)throw new InvalidOperationException("Blob 灰度直方图必须有 256 个灰度级。");
        var minimum=Array.FindIndex(bins,count=>count>0);
        var maximum=Array.FindLastIndex(bins,count=>count>0);
        if(minimum<0)throw new InvalidOperationException("Blob 灰度统计区域没有像素。");
        if(minimum==maximum)return(minimum,maximum,0);

        Span<float> histogram=stackalloc float[256];
        long totalCount=0,totalIntensity=0;
        for(var value=0;value<bins.Length;value++)
        {
            var count=bins[value];
            histogram[value]=count;
            totalCount+=count;
            totalIntensity+=value*count;
        }
        var threshold=BlobThresholdTransform.AutomaticThresholdFromHistogram(histogram,totalCount);
        long darkCount=0,darkIntensity=0;
        for(var value=minimum;value<=threshold;value++)
        {
            darkCount+=bins[value];
            darkIntensity+=value*bins[value];
        }
        var brightCount=totalCount-darkCount;
        var contrast=darkCount==0||brightCount==0?0:
            (double)(totalIntensity-darkIntensity)/brightCount-(double)darkIntensity/darkCount;
        return(minimum,maximum,contrast);
    }

    /// <summary>Fill bounded holes without extending the detection domain through its mask or ROI.</summary>
    private static HRegion FillUpHoles(HRegion region,HRegion workDomain,int maximumArea,CancellationToken cancellation)
    {
        HOperatorSet.FillUpShape(region,out var filled,"area",1,maximumArea);
        using var filledObject=filled;
        using var filledRegion=new HRegion(filledObject);
        using var additions=filledRegion.Difference(region);
        using var holes=additions.Connection();
        var count=holes.CountObj();
        if(count==0)return region.Clone();
        using var boundary=BuildContactBoundary(workDomain);
        var closed=new List<int>();
        for(var index=1;index<=count;index++)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!ExceedsBoundaryLimit(holes,index,boundary,0))closed.Add(index);
        }
        if(closed.Count==0)return region.Clone();
        using var fill=SelectRegions(holes,closed);
        using var clipped=fill.Intersection(workDomain);
        return clipped.CountObj()==0?region.Clone():region.Union2(clipped);
    }

    private static HRegion BuildContactBoundary(HRegion effectiveDomain)
    {
        if(effectiveDomain.CountObj()==0)return EmptyRegion();
        using var interior=effectiveDomain.ErosionRectangle1(3,3);
        return interior.CountObj()==0?effectiveDomain.Clone():effectiveDomain.Difference(interior);
    }

    private static bool ExceedsBoundaryLimit(HRegion regions,int index,HRegion boundary,int maximum)
    {
        using var blob=regions.SelectObj(index);
        using var contact=blob.Intersection(boundary);
        HOperatorSet.AreaCenter(contact,out var area,out var row,out var column);
        using(area)using(row)using(column)
            return area.Length!=0&&area[0].D>maximum;
    }

    private static Candidate[] FilterCandidates(HRegion regions,Candidate[] candidates,BlobFindParameters vm,int width,int height,
        HRegion? contactBoundary,CancellationToken cancellation)
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
            if(vm.SelectByShortAxis&&!InRange(BlobNumericSemantics.ShortAxis(oriented),vm.MinShortAxis,vm.MaxShortAxis))continue;
            if(vm.SelectByLongAxis&&!InRange(BlobNumericSemantics.LongAxis(oriented),vm.MinLongAxis,vm.MaxLongAxis))continue;
            if(vm.SelectByCircularuty&&!InRange(candidate.Circularity,vm.MinCircularity,vm.MaxCircularity))continue;
            if(vm.SelectByRectangularity&&!InRange(candidate.Rectangularity,vm.MinRectangularity,vm.MaxRectangularity))continue;
            if(vm.SelectByCentraBias&&!InRange(centerBias,vm.MinCenterBias,vm.MaxCenterBias))continue;
            if(vm.AxisRatioEnable&&!InRange(axisRatio,vm.MinAxisRatio,vm.MaxAxisRatio))continue;
            if(features.Count!=0)
            {
                using var original=regions.SelectObj(candidate.RegionIndex);using var filtered=original.SelectShape(names,"and",lower,upper);
                if(filtered.CountObj()==0)continue;
            }
            if(contactBoundary is not null&&ExceedsBoundaryLimit(regions,candidate.RegionIndex,contactBoundary,vm.MaxOutPixelNum))continue;
            accepted.Add(candidate);
        }
        return accepted.ToArray();
    }

    /// <summary>VM 判定组语义：数量/总面积为整体判定；其余判定组逐 Blob 给出单项 OK/NG。</summary>
    private static bool[] ResultProjectionLimitsPerBlob(BlobFindParameters vm,Candidate[] candidates,out bool overallPass,out IReadOnlyList<string> messages)
    {
        var failures=new List<string>();
        var perBlob=new bool[candidates.Length];
        for(var i=0;i<perBlob.Length;i++)perBlob[i]=true;
        var overall=true;
        void Overall(bool pass,string name){overall&=pass;if(!pass)failures.Add("[NG] "+name);}
        void PerBlob(int index,bool pass,string name){if(!pass){perBlob[index]=false;failures.Add("[NG] "+name);}}
        if(vm.BlobNumLimitEnable)Overall(candidates.Length>=vm.BlobNumLimitLow&&candidates.Length<=vm.BlobNumLimitHigh,"BlobNumLimit");
        if(vm.BlobTotalAreaLimitEnable)Overall(BlobNumericSemantics.PassDoubleLimit(BlobNumericSemantics.Float32Sum(candidates.Select(x=>x.Area)),vm.BlobTotalAreaLimitLow,vm.BlobTotalAreaLimitHigh),"BlobTotalAreaLimit");
        for(var i=0;i<candidates.Length;i++)
        {
            var x=candidates[i];
            var oriented=x.OrientedBounds;
            if(vm.BlobAreaLimitEnable)PerBlob(i,BlobNumericSemantics.PassDoubleLimit((float)(x.Area),vm.BlobAreaLimitLow,vm.BlobAreaLimitHigh),"BlobAreaLimit");
            if(vm.CentroidXLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(x.X,vm.CentroidXLimitLow,vm.CentroidXLimitHigh),"CentroidXLimit");
            if(vm.CentroidYLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(x.Y,vm.CentroidYLimitLow,vm.CentroidYLimitHigh),"CentroidYLimit");
            if(vm.AngleLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(oriented.AngleDegrees,vm.AngleLimitLow,vm.AngleLimitHigh),"AngleLimit");
            if(vm.CenterXLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(oriented.CenterX,vm.CenterXLimitLow,vm.CenterXLimitHigh),"CenterXLimit");
            if(vm.CenterYLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(oriented.CenterY,vm.CenterYLimitLow,vm.CenterYLimitHigh),"CenterYLimit");
            if(vm.BoxHeightLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(oriented.Height,vm.BoxHeightLimitLow,vm.BoxHeightLimitHigh),"BoxHeightLimit");
            if(vm.BoxWidthLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(oriented.Width,vm.BoxWidthLimitLow,vm.BoxWidthLimitHigh),"BoxWidthLimit");
            if(vm.PerimeterLimitEnable)PerBlob(i,BlobNumericSemantics.PassDoubleLimit((float)(x.Perimeter),vm.PerimeterLimitLow,vm.PerimeterLimitHigh),"PerimeterLimit");
            if(vm.LongAxisLimitEnable)PerBlob(i,BlobNumericSemantics.PassDoubleLimit(BlobNumericSemantics.LongAxis(oriented),vm.LongAxisLimitLow,vm.LongAxisLimitHigh),"LongAxisLimit");
            if(vm.ShortAxisLimitEnable)PerBlob(i,BlobNumericSemantics.PassDoubleLimit(BlobNumericSemantics.ShortAxis(oriented),vm.ShortAxisLimitLow,vm.ShortAxisLimitHigh),"ShortAxisLimit");
            if(vm.CircularityLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(x.Circularity,vm.CircularityLimitLow,vm.CircularityLimitHigh),"CircularityLimit");
            if(vm.RectangularityLimitEnable)PerBlob(i,BlobNumericSemantics.PassSingleLimit(x.Rectangularity,vm.RectangularityLimitLow,vm.RectangularityLimitHigh),"RectangularityLimit");
        }
        overallPass=overall;
        messages=failures;
        return perBlob;
    }

    private static bool InRange(double value,double low,double high)=>BlobFindParameters.InRange(value,low,high);

    private static HRegion CreateImageMaskRegion(IReadOnlyImage mask, int width, int height)
    {
        if (mask.Width != width || mask.Height != height || mask.Layout.Format != PixelLayout.Gray8)
            throw new ArgumentException("屏蔽图像必须与输入同尺寸且为 Gray8。");
        using var image = HalconResourceBridge.CopyImage(mask);
        return image.Threshold(1d, 255d);
    }
    internal static bool AreaAccepted(int area,int minimum,int maximum)=>minimum<=maximum?area>=minimum&&area<=maximum:area>=minimum||area<=maximum;
    internal static int NativeAreaInteger(double area)
    {
        float original=(float)area;
        if(!float.IsFinite(original)||original<0||original>=2147483648f)
            throw new BlobCapabilityException("BLOB_AREA_NOT_READY","面积转原32位整数超出已核正常范围。");
        return (int)original;
    }

    private static Candidate[] ExtractCandidates(HRegion regions, CancellationToken cancellation)
    {
        HOperatorSet.AreaCenter(regions, out var areas, out var rows, out var cols);
        using var a = areas; using var r = rows; using var c = cols;
        HOperatorSet.SmallestRectangle1(regions, out var r1, out var c1, out var r2, out var c2);
        using var rb = r1; using var cb = c1; using var re = r2; using var ce = c2;
        using var circularity = regions.Circularity(); using var rectangularity = regions.Rectangularity(); using var perimeter = regions.Contlength();
        HOperatorSet.SmallestRectangle2(regions, out var rr, out var rc, out var phi, out var length1, out var length2);
        using var rrr = rr; using var rrc = rc; using var ph = phi; using var l1 = length1; using var l2 = length2;
        HOperatorSet.OrientationRegion(regions, out var blobAngle); using var ba = blobAngle;
        var result = new List<Candidate>();
        for (int i = 0; i < areas.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (areas[i].D <= 0) continue;
            double circ = Math.Min(1, circularity[i].D), rect = Math.Min(1, rectangularity[i].D);
            result.Add(new(i + 1,i + 1, areas[i].D, perimeter[i].D, cols[i].D, rows[i].D, circ, rect,
                new(c1[i].D, r1[i].D, c2[i].D - c1[i].D + 1, r2[i].D - r1[i].D + 1),
                new(rc[i].D,rr[i].D,2*length1[i].D,2*length2[i].D,length1[i].D==0&&length2[i].D==0?0:-phi[i].D*180/Math.PI),
                blobAngle[i].D*180/Math.PI,
                BuildRotatedRectangle(rr[i].D, rc[i].D, phi[i].D, length1[i].D, length2[i].D), true));
        }
        return result.ToArray();
    }

    private static float SortFloatKey(Candidate value,BlobSortFeature feature)=>(float)(feature switch
    {
        BlobSortFeature.SortFeatureArea=>value.Area,BlobSortFeature.SortFeaturePerimeter=>value.Perimeter,
        BlobSortFeature.SortFeatureCircularity=>value.Circularity,BlobSortFeature.SortFeatureRect=>value.Rectangularity,
        BlobSortFeature.SortFeatureCentroidX=>value.X,BlobSortFeature.SortFeatureCentroidY=>value.Y,
        BlobSortFeature.SortFeatureBoxAngle=>value.OrientedBounds.AngleDegrees,
        BlobSortFeature.SortFeatureBoxWidth=>value.OrientedBounds.Width,
        BlobSortFeature.SortFeatureBoxHeight=>value.OrientedBounds.Height,
        BlobSortFeature.SortFeatureRectX=>value.Bounds.X,BlobSortFeature.SortFeatureRectY=>value.Bounds.Y,
        BlobSortFeature.SortFeatureAxisAngle=>value.BlobAngleDegrees,
        BlobSortFeature.SortFeatureAxisRatio=>Math.Max(value.OrientedBounds.Width,value.OrientedBounds.Height) is var longest && longest>0
            ?Math.Min(value.OrientedBounds.Width,value.OrientedBounds.Height)/longest:0,
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

public sealed partial class BlobAlgorithm
{
        private static HRegion SelectRegions(HRegion source, IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return EmptyRegion();
            using var tuple = new HTuple(indices.ToArray());
            HOperatorSet.SelectObj(source, out var selected, tuple);
            using (selected) return new HRegion(selected);
        }

        private static HRegion EmptyRegion()
        {
            HOperatorSet.GenEmptyRegion(out var empty);
            using (empty) return new HRegion(empty);
        }

        private static Point2[] BuildRotatedRectangle(double row, double col, double phi, double length1, double length2)
        {
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            // HALCON rectangle2 phi -> screen coordinates (X=column, Y=row).
            // Its first axis is (cos(phi), -sin(phi)), not (cos(phi), sin(phi)).
            var axis = new Point2(cos, -sin);
            var normal = new Point2(sin, cos);
            return new[]
            {
                new Point2(col - axis.X * length1 - normal.X * length2, row - axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 - normal.X * length2, row + axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 + normal.X * length2, row + axis.Y * length1 + normal.Y * length2),
                new Point2(col - axis.X * length1 + normal.X * length2, row - axis.Y * length1 + normal.Y * length2)
            };
        }
}

internal readonly record struct BlobSelectedCandidate<T>(T Candidate,int Slot);

/// <summary>Capacity slots and heap entries are distinct. No image handles or visual state are owned here.</summary>
internal static class BlobCandidateSelection
{
    private readonly record struct HeapEntry(float FloatingKey,int IntegerKey,int Slot);

    internal static BlobSelectedCandidate<T>[] SelectSlots<T>(IReadOnlyList<T> source,int capacity,BlobSortFeature feature,BlobSortMode mode,
        Func<T,float> area,Func<T,float> featureKey,Func<T,int>? integerKey,CancellationToken cancellation)
    {
        if(capacity is <1 or >20000)throw new ArgumentOutOfRangeException(nameof(capacity));
        if(!Enum.IsDefined(feature)||!Enum.IsDefined(mode))throw new ArgumentException("未知候选选择模式。");
        int count=Math.Min(source.Count,capacity);var slots=new BlobSelectedCandidate<T>[count];
        var capacityKey=mode==BlobSortMode.SortModeNotSort?area:featureKey;
        bool integerCapacity=mode!=BlobSortMode.SortModeNotSort&&integerKey is not null;
        bool signedCapacity=mode!=BlobSortMode.SortModeNotSort&&feature is BlobSortFeature.SortFeatureBoxAngle or BlobSortFeature.SortFeatureAxisAngle;
        HeapEntry Entry(T candidate,int slot)
        {if(integerCapacity)return new(0,integerKey!(candidate),slot);float key=capacityKey(candidate);RequireKey(key,signedCapacity);return new(key,0,slot);}
        for(int i=0;i<count;i++){cancellation.ThrowIfCancellationRequested();_=Entry(source[i],i);slots[i]=new(source[i],i);}
        if(source.Count>capacity)
        {
            bool maximumHeap=mode==BlobSortMode.SortModeAscend;
            var heap=new HeapEntry[count];for(int i=0;i<count;i++)heap[i]=Entry(slots[i].Candidate,i);
            // Original bottom-up initialization, rather than insertion-order heap construction.
            for(int parent=count/2-1;parent>=0;parent--){cancellation.ThrowIfCancellationRequested();SiftDown(heap,parent,maximumHeap,integerCapacity);}
            for(int i=count;i<source.Count;i++)
            {
                cancellation.ThrowIfCancellationRequested();var candidate=source[i];var replacement=Entry(candidate,heap[0].Slot);
                int comparison=Compare(replacement,heap[0],integerCapacity);bool replace=maximumHeap?comparison<0:comparison>0;
                if(!replace)continue;
                int slot=heap[0].Slot;slots[slot]=new(candidate,slot); // Copy the complete immutable candidate, never selected individual measurements.
                heap[0]=replacement;SiftDown(heap,0,maximumHeap,integerCapacity);
            }
        }
        return slots;
    }
    internal static BlobSelectedCandidate<T>[] Order<T>(BlobSelectedCandidate<T>[] retained,BlobSortMode mode,
        Func<T,float> floatKey,Func<T,int>? integerKey,Func<T,int> componentLabel,CancellationToken cancellation)
    {
        var slots=retained.ToArray();
        if(mode==BlobSortMode.SortModeNotSort)return slots;
        foreach(var slot in slots){cancellation.ThrowIfCancellationRequested();if(integerKey is null)RequireFinite(floatKey(slot.Candidate));else _=integerKey(slot.Candidate);}
        // A real final comparator, including the integer component label on equal float keys.
        Array.Sort(slots,(left,right)=>
        {
            int primary;
            if(integerKey is not null)primary=integerKey(left.Candidate).CompareTo(integerKey(right.Candidate));
            else
            {
                float a=floatKey(left.Candidate),b=floatKey(right.Candidate);RequireFinite(a);RequireFinite(b);
                float difference=a-b;RequireFinite(difference);primary=difference<0?-1:difference>0?1:0;
            }
            if(primary!=0)return mode==BlobSortMode.SortModeDecend?-primary:primary;
            return componentLabel(left.Candidate).CompareTo(componentLabel(right.Candidate));
        });
        cancellation.ThrowIfCancellationRequested();return slots;
    }
    private static void RequireFinite(float value)
    {if(!float.IsFinite(value))throw new BlobCapabilityException("BLOB_SELECTION_VALUE_NOT_READY","候选选择只接纳已核正常有限浮点特征。");}
    private static void RequireKey(float value,bool signed)
    {RequireFinite(value);if(!signed&&value<0)throw new BlobCapabilityException("BLOB_SELECTION_VALUE_NOT_READY","非角度选择特征不能为负值。");}
    private static int Compare(HeapEntry left,HeapEntry right,bool integer)=>integer?left.IntegerKey.CompareTo(right.IntegerKey):left.FloatingKey.CompareTo(right.FloatingKey);
    private static void SiftDown(HeapEntry[] heap,int parent,bool maximumHeap,bool integer)
    {
        var pending=heap[parent];int count=heap.Length;
        while(parent<count/2)
        {
            int child=parent*2+1,right=child+1;
            // Strict comparison selects the left sibling on equal keys.
            if(right<count&&(maximumHeap?Compare(heap[right],heap[child],integer)>0:Compare(heap[right],heap[child],integer)<0))child=right;
            int comparison=Compare(pending,heap[child],integer);if(maximumHeap?comparison>=0:comparison<=0)break;
            heap[parent]=heap[child];parent=child;
        }
        heap[parent]=pending;
    }
}

/// <summary>Per-invocation platform identity audit; these are not native permanent identifiers or new native result ports.</summary>
public sealed record BlobSelectionEntry(int ComponentLabel,int RegionIndex,int SelectionSlot,int OutputOrdinal);

/// <summary>Blob 分段耗时探针。仅当 SUNEYEVISION_TIMING_PROBE=1 时启用，未设置时为零开销空实现，
/// 用于每次执行后核对首次与稳定态的阶段分布。</summary>
internal sealed class BlobDiagnostics : IDisposable
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("SUNEYEVISION_TIMING_PROBE"), "1", StringComparison.Ordinal);
    private static readonly BlobDiagnostics Disabled = new(null,null,false);
    private readonly Action<string>? _log; private readonly string? _node; private readonly Stopwatch? _clock;
    private readonly List<(string Stage,long Ms)>? _stages; private readonly bool _enabled; private long _last; private bool _disposed;
    private BlobDiagnostics(Action<string>? log,string? node,bool enabled)
    {
        _log=log;_node=node;_enabled=enabled;
        if(enabled){_clock=Stopwatch.StartNew();_stages=new();}
    }
    public static BlobDiagnostics Begin(Action<string> log,string node)=>Enabled?new(log,node,true):Disabled;
    public void Mark(string stage){if(!_enabled||_disposed)return;var now=_clock!.ElapsedMilliseconds;_stages!.Add((stage,now-_last));_last=now;}
    public void Dispose(){if(!_enabled||_disposed)return;_disposed=true;_clock!.Stop();_log!($"[ExecTimingProbe] tool=blob node={_node} total={_clock.ElapsedMilliseconds}ms stages={string.Join("/",_stages!.Select(x=>x.Stage+"="+x.Ms+"ms"))}");}
}


internal static class BlobNumericSemantics
{
    // C03 axis getters report the full projected pixel-center span plus one pixel.
    internal static float LongAxis(BlobOrientedBounds bounds) => (float)(Math.Max(bounds.Width, bounds.Height) + 1d);
    internal static float ShortAxis(BlobOrientedBounds bounds) => (float)(Math.Min(bounds.Width, bounds.Height) + 1d);

    internal static bool PassSingleLimit(double value, double minimum, double maximum)
    {
        const float machineEpsilon = 1.1920928955078125e-7f;
        var measured = (float)value;
        var low = (float)minimum;
        var high = (float)maximum;
        return float.IsFinite(measured) && float.IsFinite(low) && float.IsFinite(high)
            && measured >= low - machineEpsilon && measured <= high + machineEpsilon;
    }

    internal static bool PassDoubleLimit(double value, double minimum, double maximum)
    {
        const double machineEpsilon = 2.220446049250313e-16;
        return double.IsFinite(value) && double.IsFinite(minimum) && double.IsFinite(maximum)
            && value >= minimum - machineEpsilon && value <= maximum + machineEpsilon;
    }

    /// <summary>
    /// VM 对结果数组按顺序使用单精度累加总面积，再把结果发布给托管层。
    /// </summary>
    internal static double Float32Sum(IEnumerable<double> values)
    {
        float total = 0f;
        foreach (var value in values)
            total += (float)value;
        return total;
    }
}

internal static class BlobResultGeometry
{
    private const int MaximumRasterContours = 256;
    private const int MaximumRasterVertices = 65536;
    private static readonly TypeRef Vertices = RoiDeclarations.Shape.Fields.Single(port => port.Id == "vertices").Type;

    internal static ResultGeometrySnapshot Capture(RecordValue? roi, IReadOnlyImage image, bool passed,
        HRegion domain, HRegion inspection, HRegion mask, RecordValue? pose, bool pixelInspection, bool pixelMask,
        SequenceValue maskPolygons, CancellationToken cancellation, RecordValue? fixture)
    {
        var definition = roi ?? RoiDocumentValue.Empty;
        var inspectionCoverage = Coverage(RoiDocumentValue.Inspection);
        var inspectionItems = Enabled(RoiDocumentValue.Inspection);
        var rings = inspectionItems.Where(item => item.Get<RecordValue>("shape").Get<string>("kind") is "annulus" or "sector").ToArray();
        var ordinary = inspectionItems.Except(rings).ToArray();
        var groups = new List<ResultGeometryGroup>();
        if (pixelInspection)
        {
            groups.Add(Raster(ResultGeometryRole.Inspection, inspection));
            groups.Add(Empty(ResultGeometryRole.Annulus));
        }
        else if (roi is not null && inspectionCoverage == ResultGeometryCoverage.FullDomain)
        {
            // The original full-screen selection publishes a nominal rectangle, independent of the threshold work domain.
            var rectangle = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "rectangle",
                ["center"] = new PointGeometry(BlobSchema.ImageFrame, new(image.Width / 2d, image.Height / 2d)),
                ["width"] = (double)image.Width,
                ["height"] = (double)image.Height,
                ["angle-degrees"] = 0d
            });
            groups.Add(new(ResultGeometryRole.Inspection, ResultGeometryCoverage.Explicit, new[] { rectangle }));
            groups.Add(Empty(ResultGeometryRole.Annulus));
        }
        else if (pose is not null && inspectionCoverage == ResultGeometryCoverage.Explicit)
        {
            groups.Add(Corrected(ResultGeometryRole.Inspection, ordinary));
            groups.Add(Corrected(ResultGeometryRole.Annulus, rings));
        }
        else
        {
            groups.Add(Nominal(ResultGeometryRole.Inspection, inspectionCoverage, ordinary));
            groups.Add(Nominal(ResultGeometryRole.Annulus, ResultGeometryCoverage.Explicit, rings));
        }
        if (pixelMask)
            groups.Add(Raster(ResultGeometryRole.Mask, mask));
        else if (maskPolygons.Count != 0)
            groups.Add(new(ResultGeometryRole.Mask, ResultGeometryCoverage.Explicit, maskPolygons.Cast<PolygonGeometry>().Select(PolygonShape)));
        else
            groups.Add(Empty(ResultGeometryRole.Mask));
        if (pose is not null && fixture is not null)
        {
            groups.Add(FixturePoint(ResultGeometryRole.ReferencePoint, fixture.Get<RecordValue>("reference")));
            groups.Add(FixturePoint(ResultGeometryRole.RunPoint, fixture.Get<RecordValue>("run")));
        }
        return new(image.Frame, BlobSchema.ImageFrame, passed, groups);

        ResultGeometryCoverage Coverage(string group) => RoiDocumentValue.Coverage(definition, group) switch
        {
            RoiDocumentValue.FullDomain => ResultGeometryCoverage.FullDomain,
            RoiDocumentValue.None => ResultGeometryCoverage.None,
            _ => ResultGeometryCoverage.Explicit
        };
        RecordValue[] Enabled(string group) => RoiDocumentValue.Items(definition, group).Where(item => item.Get<bool>("enabled")).ToArray();
        ResultGeometryGroup Raster(ResultGeometryRole role, HRegion region)
        {
            using var clipped = region.Intersection(domain);
            return ReadContours(role, clipped, cancellation);
        }
        ResultGeometryGroup Corrected(ResultGeometryRole role, RecordValue[] items)
        {
            if (items.Length == 0) return Empty(role);
            if (TryCorrectNominal(items, pose!, out var shapes))
                return new(role, ResultGeometryCoverage.Explicit, shapes);
            if (items.Length == inspectionItems.Length) return Raster(role, inspection);
            // Reuse the invocation rasterizer for mixed inspection roles, preserving their independent display switches.
            var subset = RoiDocumentValue.Root(RoiDocumentValue.Group(RoiDocumentValue.Explicit, items),
                RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));
            using var original = RoiDocumentRasterizer.Build(subset, RoiDocumentValue.Inspection, domain, cancellation,
                clipToInputDomain: false);
            using var corrected = HalconResourceBridge.TransformRegion(original, pose);
            return Raster(role, corrected);
        }
    }

    private static ResultGeometryGroup Empty(ResultGeometryRole role) => new(role, ResultGeometryCoverage.None, Array.Empty<RecordValue>());

    private static bool TryCorrectNominal(RecordValue[] items, RecordValue pose, out RecordValue[] shapes)
    {
        shapes = [];
        var a = pose.Get<double>("m11");
        var b = pose.Get<double>("m12");
        var c = pose.Get<double>("m21");
        var d = pose.Get<double>("m22");
        var scale = double.Hypot(a, b);
        // Only orientation-preserving similarities retain both rectangle and circular-sector semantics.
        var tolerance = scale * 1e-12;
        if (scale <= 0 || !double.IsFinite(scale) || Math.Abs(a - d) > tolerance || Math.Abs(b + c) > tolerance)
            return false;
        var rotation = Math.Atan2(b, a);
        var offsetX = pose.Get<double>("offset-x");
        var offsetY = pose.Get<double>("offset-y");
        Point2 Transform(Point2 point) => new(point.X * a + point.Y * c + offsetX, point.X * b + point.Y * d + offsetY);
        var result = new List<RecordValue>(items.Length);
        foreach (var item in items)
        {
            var shape = BlobRoiDisplayProjection.Shape(item.Get<RecordValue>("shape"));
            var kind = shape.Get<string>("kind");
            if (kind == "polygon")
            {
                var polygon = new PolygonGeometry(BlobSchema.ImageFrame,
                    shape.Get<SequenceValue>("vertices").Cast<PointGeometry>().Select(point => Transform(point.Point)));
                result.Add(PolygonShape(polygon));
                continue;
            }
            var fields = shape.CopyFields().ToDictionary(pair => pair.Key, pair => pair.Value);
            fields["center"] = new PointGeometry(BlobSchema.ImageFrame, Transform(shape.Get<PointGeometry>("center").Point));
            if (kind == "rectangle")
            {
                fields["width"] = shape.Get<double>("width") * scale;
                fields["height"] = shape.Get<double>("height") * scale;
                fields["angle-degrees"] = RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees") + rotation * 180d / Math.PI);
            }
            else
            {
                var start = shape.Get<double>("start-radians");
                var sweep = shape.Get<double>("end-radians") - start;
                var correctedStart = Math.IEEERemainder(start, 2 * Math.PI) + rotation;
                fields["start-radians"] = correctedStart;
                fields["end-radians"] = correctedStart + sweep;
                fields["inner-radius"] = shape.Get<double>("inner-radius") * scale;
                fields["outer-radius"] = shape.Get<double>("outer-radius") * scale;
            }
            result.Add(new(RoiDeclarations.Shape, fields));
        }
        shapes = result.ToArray();
        return true;
    }

    private static RecordValue PolygonShape(PolygonGeometry polygon) => new(RoiDeclarations.Shape, new Dictionary<string, object?>
    {
        ["kind"] = "polygon",
        ["closed"] = true,
        ["vertices"] = new SequenceValue(Vertices, polygon.Vertices.Select(point => new PointGeometry(polygon.Frame, point)))
    });

    private static ResultGeometryGroup FixturePoint(ResultGeometryRole role, RecordValue pose)
    {
        var point = pose.Get<RecordValue>("point");
        var shape = new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
        {
            ["kind"] = "point", ["center"] = new PointGeometry(BlobSchema.ImageFrame,new(point.Get<double>("x"),point.Get<double>("y")))
        });
        return new(role, ResultGeometryCoverage.Explicit, new[] { shape });
    }

    private static ResultGeometryGroup Nominal(ResultGeometryRole role, ResultGeometryCoverage coverage, RecordValue[] items)
        => coverage == ResultGeometryCoverage.Explicit && items.Length == 0 ? Empty(role)
            : new(role, coverage, items.Select(item => BlobRoiDisplayProjection.Shape(item.Get<RecordValue>("shape"))));

    private static ResultGeometryGroup ReadContours(ResultGeometryRole role, HRegion region, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        HOperatorSet.AreaCenter(region, out var area, out var centerRow, out var centerColumn);
        using (area) using (centerRow) using (centerColumn)
            if (area.Length == 0 || area.LArr.All(value => value == 0)) return Empty(role);

        HOperatorSet.GenContourRegionXld(region, out var contours, "border_holes");
        using var contourOwner = contours;
        HOperatorSet.CountObj(contours, out var count);
        using var countOwner = count;
        if (count.I > MaximumRasterContours) return Unavailable();
        var shapes = new List<RecordValue>();
        var remaining = MaximumRasterVertices;
        for (var index = 1; index <= count.I; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            HOperatorSet.SelectObj(contours, out var selected, index);
            using var contour = selected;
            HOperatorSet.GetContourXld(contour, out var rows, out var columns);
            using var rowOwner = rows;
            using var columnOwner = columns;
            if (rows.Length != columns.Length || rows.Length > remaining) return Unavailable();
            remaining -= rows.Length;
            var points = new PointGeometry[rows.Length];
            for (var point = 0; point < points.Length; point++)
            {
                if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                points[point] = new(BlobSchema.ImageFrame, new(columns[point].D, rows[point].D));
            }
            if (points.Select(point => point.Point).Distinct().Take(3).Count() < 3) return Unavailable();
            shapes.Add(new RecordValue(RoiDeclarations.Shape, new Dictionary<string, object?>
            {
                ["kind"] = "polygon", ["vertices"] = new SequenceValue(Vertices, points), ["closed"] = true
            }));
        }
        return shapes.Count == 0 ? Unavailable() : new(role, ResultGeometryCoverage.Explicit, shapes, outlineOnly: true);
        ResultGeometryGroup Unavailable() => new(role, ResultGeometryCoverage.Unavailable, Array.Empty<RecordValue>());
    }
}

/// <summary>Immutable receipt of this invocation, not an assertion about a native result getter.</summary>
public sealed record BlobThresholdExecutionSnapshot(BlobSegmentationMode Mode,int? Lower,int? Upper,string Polarity,string Provenance);

/// <summary>Reconstruction of the evidenced byte scalar operations; HALCON owns image/domain resources.</summary>
internal static class BlobThresholdTransform
{
    internal static (HRegion Foreground,HImage? Work,BlobThresholdExecutionSnapshot Receipt) Apply(HImage gray,HRegion effectiveDomain,
        BlobSettings settings,int width,int height,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(settings.SegmentationMode is BlobSegmentationMode.SingleThreshold or BlobSegmentationMode.DoubleThreshold)
            return ApplyHardThreshold(gray,effectiveDomain,settings,width,height,cancellation);
        var pixels=ReadBytes(gray,width,height);
        using var domainImage=RegionImage(effectiveDomain,width,height);
        var domain=ReadBytes(domainImage,width,height);
        int low=settings.Threshold,high=settings.UpperThreshold;
        byte[]? table=null;
        string provenance="ConsumedInputEndpoints";
        switch(settings.SegmentationMode)
        {
            case BlobSegmentationMode.AutoThreshold:
                low=high=AutoThreshold(pixels,domain,cancellation);provenance="LocalScalarFormulaCalculation";break;
            case BlobSegmentationMode.FixedSoftThreshold:
                low=settings.FixedSoftLowerThreshold;high=settings.FixedSoftUpperThreshold;
                table=SoftTable(low,high,settings.ThresholdSoftness);break;
            case BlobSegmentationMode.RelativeSoftThreshold:
                (low,high)=RelativeThresholds(pixels,domain,settings,cancellation);
                table=SoftTable(low,high,settings.ThresholdSoftness);provenance="LocalScalarFormulaCalculation";break;
            case BlobSegmentationMode.None: break;
            default: throw new BlobCapabilityException("BLOB_MODE_NOT_READY","该阈值模式未接入工作图。");
        }
        bool dark=settings.Type==BlobThresholdMode.BinaryInv;
        for(int y=0;y<height;y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for(int x=0;x<width;x++)
            {
                int i=y*width+x;
                if(domain[i]==0){pixels[i]=0;continue;}
                int p=pixels[i];
                if(settings.SegmentationMode==BlobSegmentationMode.None)pixels[i]=dark?(byte)(255-p):(byte)p;
                else if(table is not null)pixels[i]=dark?table[p]:(byte)(255-table[p]);
                else
                {
                    bool selected=dark?p<=low:p>=low;
                    pixels[i]=selected?(byte)255:(byte)0;
                }
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return CaptureForeground(CreateImage(pixels,width,height),settings,new(settings.SegmentationMode,
            settings.SegmentationMode==BlobSegmentationMode.None?null:low,settings.SegmentationMode==BlobSegmentationMode.None?null:high,
            settings.Type.ToString(),provenance),cancellation);
    }

    private static (HRegion Foreground,HImage? Work,BlobThresholdExecutionSnapshot Receipt) ApplyHardThreshold(
        HImage gray,HRegion effectiveDomain,BlobSettings settings,int width,int height,CancellationToken cancellation)
    {
        var mode=settings.SegmentationMode;
        var low=settings.Threshold;
        var high=mode==BlobSegmentationMode.SingleThreshold?255:settings.UpperThreshold;
        var polarity=mode==BlobSegmentationMode.SingleThreshold?settings.Type:
            low>high?BlobThresholdMode.BinaryInv:BlobThresholdMode.Binary;
        using var reduced=gray.ReduceDomain(effectiveDomain);
        HRegion Band(int from,int through)
        {
            cancellation.ThrowIfCancellationRequested();
            if(from<=through)return reduced.Threshold((double)from,(double)through);
            HOperatorSet.GenEmptyRegion(out var empty);
            using(empty)return new HRegion(empty);
        }
        HRegion Select()
        {
            if(mode==BlobSegmentationMode.SingleThreshold)
                return polarity==BlobThresholdMode.BinaryInv?Band(0,low-1):Band(low,255);
            if(low<=high)return Band(low,high);
            using var below=Band(0,high-1);
            using var above=Band(low+1,255);
            return below.Union2(above);
        }
        var foreground=Select();
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var work=settings.OutputBinaryImage?RegionImage(foreground,width,height):null;
            return (foreground,work,new(mode,low,high,polarity.ToString(),"ConsumedInputEndpoints"));
        }
        catch { foreground.Dispose(); throw; }
    }

    private static (HRegion Foreground,HImage? Work,BlobThresholdExecutionSnapshot Receipt) CaptureForeground(
        HImage work,BlobSettings settings,BlobThresholdExecutionSnapshot receipt,CancellationToken cancellation)
    {
        HRegion? foreground=null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            foreground=work.Threshold(1d,255d);
            if(settings.OutputBinaryImage)return (foreground,work,receipt);
        }
        catch { work.Dispose(); throw; }
        try { work.Dispose(); }
        catch { foreground.Dispose(); throw; }
        return (foreground,null,receipt);
    }
    internal static byte[] SoftTable(int low,int high,int softness)
    {
        var table=new byte[256];int gap=high-low;
        float ratio,step;
        if(softness==0||gap==0){ratio=0;step=0;}
        else if(softness<gap){ratio=(float)softness/(float)gap;step=255f/(1f+(float)softness);}
        else {ratio=1f;step=255f/((float)gap+1f);}
        for(int p=0;p<256;p++)
        {
            if(p<=low){table[p]=255;continue;}
            if(p>=high){table[p]=0;continue;}
            // The inner CVTT and final CVTT are distinct; clamp the float before the latter.
            int bucket=(int)((float)(p-low)*ratio);
            float mapped=(255f-(float)(bucket+1)*step)+0.5f;
            table[p]=(byte)(int)Math.Clamp(mapped,0f,255f);
        }
        return table;
    }
    private static int AutoThreshold(byte[] pixels,byte[] domain,CancellationToken cancellation)
    {
        var histogram=new float[256];
        int count=0;
        for(int i=0;i<pixels.Length;i++)
        {
            if((i&65535)==0)cancellation.ThrowIfCancellationRequested();
            if(domain[i]==0)continue;
            histogram[pixels[i]]+=1f;
            count++;
        }
        RequireHistogramPixels(count);
        return AutomaticThresholdFromHistogram(histogram,count);
    }

    internal static int AutomaticThresholdFromHistogram(Span<float> histogram,long count)
    {
        float scale=1f/(float)count;
        for(int i=0;i<256;i++)histogram[i]*=scale;
        float pairedMean=0;
        // Original 1806e70e0 scalar order: bins 1,2 then 3,4 ... 253,254; bin 255 added in the score expression.
        for(int i=0;i<127;i++)
        {
            pairedMean=pairedMean+(float)(i*2+1)*histogram[i*2+1];
            pairedMean=pairedMean+(float)(i*2+2)*histogram[i*2+2];
        }
        float weight=0,moment=0,bestScore=0;int threshold=0;
        for(int i=0;i<256;i++)
        {
            weight+=histogram[i];if(weight>=1.00000011920928955078125f)break;
            moment+=(float)i*histogram[i];
            float delta=(pairedMean+255f*histogram[255])*weight-moment;
            float score=(delta*delta)/((weight-weight*weight)+1.1920928955078125e-7f);
            if(score>bestScore){bestScore=score;threshold=i;}
        }
        return threshold;
    }
    private static (int Low,int High) RelativeThresholds(byte[] pixels,byte[] domain,BlobSettings settings,CancellationToken cancellation)
    {
        var histogram=new int[256];
        int count=0;
        for(int i=0;i<pixels.Length;i++)
        {
            if((i&65535)==0)cancellation.ThrowIfCancellationRequested();
            if(domain[i]==0)continue;
            histogram[pixels[i]]++;
            count++;
        }
        RequireHistogramPixels(count);
        int left=(int)(((float)settings.RelativeLeftRatio*0.01f)*(float)count);
        int right=(int)(((float)settings.RelativeRightRatio*0.01f)*(float)count);
        int a=0,b=255;
        if(left>0){int sum=0;for(int i=0;i<256;i++){sum+=histogram[i];a=i;if(sum>=left)break;}}
        if(right>0){int sum=0;for(int i=255;i>=0;i--){sum+=histogram[i];b=i;if(sum>=right)break;}}
        int low=a+(int)((float)(b-a)*((float)settings.RelativeLowerRatio*0.01f));
        int high=a+(int)((float)(b-a)*((float)settings.RelativeUpperRatio*0.01f));
        return(Math.Min(low,high),Math.Max(low,high));
    }
    private static void RequireHistogramPixels(int count)
    {
        if(count==0)
            throw new BlobCapabilityException("BLOB_THRESHOLD_EMPTY_DOMAIN","有效检测区域没有可用于阈值统计的像素。");
    }
    internal static HImage RegionImage(HRegion region,int width,int height)
    {
        if(region.CountObj()==0){var black=new HImage();try{black.GenImageConst("byte",width,height);return black;}catch{black.Dispose();throw;}}
        HOperatorSet.RegionToBin(region,out var image,255,0,width,height);using(image)return new HImage(image);
    }
    private static byte[] ReadBytes(HImage image,int width,int height)
    {
        var pointer=image.GetImagePointer1(out string type,out int w,out int h);
        if(type!="byte"||w!=width||h!=height)throw new ArgumentException("阈值工作图需要同尺寸8位单通道图像。");
        var bytes=new byte[checked(width*height)];Marshal.Copy(pointer,bytes,0,bytes.Length);return bytes;
    }
    private static HImage CreateImage(byte[] pixels,int width,int height)
    {
        var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);var image=new HImage();
        try{image.GenImage1("byte",width,height,pinned.AddrOfPinnedObject());return image;}
        catch{image.Dispose();throw;}
        finally{pinned.Free();}
    }
}

internal sealed class BlobCapabilityException(string code,string message):InvalidOperationException(message)
{internal string Code{get;}=code;}

/// <summary>算法同步执行期间按冻结参数设置4/8邻域，并在原线程恢复调用者设置；不改HALCON全局上限。</summary>
public sealed class HalconBlobAlgorithmContext : IDisposable
{
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly HTuple _previousNeighborhood;
    private bool _disposed;

    public HalconBlobAlgorithmContext(BlobConnectivity connectivity)
    {
        if (!Enum.IsDefined(connectivity)) throw new ArgumentOutOfRangeException(nameof(connectivity));
        RequireUnlimitedConnection();
        HOperatorSet.GetSystem("tsp_neighborhood", out _previousNeighborhood);
        try { HOperatorSet.SetSystem("tsp_neighborhood", (int)connectivity); }
        catch { _previousNeighborhood.Dispose(); throw; }
    }

    public static void RequireUnlimitedConnection()
    {
        HOperatorSet.GetSystem("max_connection", out var limit);
        using (limit)
            if (limit.I != 0)
                throw new InvalidOperationException("BLOB_HALCON_MAX_CONNECTION：HALCON连通区域返回上限必须为0（不截断）。Blob数量限制在特征筛选和排序之后执行，不能提前丢失候选区域。");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new InvalidOperationException("Blob算法的HALCON上下文必须在原同步线程退出。");
        try { HOperatorSet.SetSystem("tsp_neighborhood", _previousNeighborhood); }
        finally { _previousNeighborhood.Dispose(); _disposed = true; }
    }
}
