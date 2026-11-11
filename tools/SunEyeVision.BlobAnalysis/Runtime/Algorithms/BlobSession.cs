using HalconDotNet;
using System.Diagnostics;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

[Flags]
public enum BlobOutputRequest { None = 0, Boundary = 4 }

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
        double Circularity, double Rectangularity, BlobBounds Bounds, BlobOrientedBounds OrientedBounds, double BlobAngleDegrees, Point2[] Corners, bool IsPass);

    public BlobOutput Execute(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection = null,
        IReadOnlyRegion? mask = null, RecordValue? pose = null, CancellationToken cancellation = default,
        BlobOutputRequest requested = BlobOutputRequest.None,RecordValue? localRoi=null)
        =>ExecuteCore(settings,input,resources,inspection,mask,pose,cancellation,requested,localRoi,BlobLocalRoi.MaximumLocalInspectionCount);
    public BlobOutput Execute(BlobSettings settings,BlobInput input,IOutputResources resources,CancellationToken cancellation=default,BlobOutputRequest requested=BlobOutputRequest.None)
        =>ExecuteCore(settings,input.Image,resources,input.Inspection,input.Mask,input.Pose,cancellation,requested,input.LocalRoi,
            input.IsInheritedRoi?BlobLocalRoi.MaximumInheritedCount:BlobLocalRoi.MaximumLocalInspectionCount,input.ImageMask,input.OutputMask,input.Fixture,input.IsInheritedMask);
    private BlobOutput ExecuteCore(BlobSettings settings, IReadOnlyImage input, IOutputResources resources, IReadOnlyRegion? inspection,
        IReadOnlyRegion? mask, RecordValue? pose, CancellationToken cancellation,BlobOutputRequest requested,RecordValue? localRoi,int maximumInspectionCount,IReadOnlyImage? imageMask=null,bool outputMask=false,RecordValue? fixture=null,bool inheritedMask=false)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(resources);
        settings = settings.Freeze();
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
        if (!BlobThresholdCapabilities.IsSupported(settings.SegmentationMode))
            throw new InvalidOperationException("BLOB_MODE_NOT_READY: 该阈值模式尚未完成原厂语义对齐。");
        var errors = settings.Validate();
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
        using var execution = new HalconBlobExecutionScope(settings.Connectivity);
        using var image = HalconResourceBridge.CopyImage(input);
        HOperatorSet.GetImageSize(image, out var widthValue, out var heightValue);
        using var widthTuple = widthValue; using var heightTuple = heightValue;
        int width = widthValue.I, height = heightValue.I;
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
        using var imageMaskRegion = imageMask is null ? null : CreateImageMaskRegion(imageMask, width, height);
        using var inspectionRegion = imageMaskRegion is not null ? imageMaskRegion.Clone()
            : localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Inspection,domain,cancellation,clipToInputDomain:pose is null)
            :inspection is null ? domain.Clone() : HalconResourceBridge.CopyRegion(inspection, frame, width, height);
        using var maskRegion = localRoi is not null?BlobLocalRoiRasterizer.Build(localRoi,BlobLocalRoi.Mask,domain,cancellation,clipToInputDomain:pose is null)
            :mask is null ? EmptyRegion() : HalconResourceBridge.CopyRegion(mask, frame, width, height);
        var correctInspection = inspection is not null || localRoi is not null && BlobLocalRoi.Coverage(localRoi,BlobLocalRoi.Inspection)==BlobLocalRoi.Explicit;
        using var correctedInspection = correctInspection ? HalconResourceBridge.TransformRegion(inspectionRegion, pose) : inspectionRegion.Clone();
        using var correctedMask = HalconResourceBridge.TransformRegion(maskRegion, pose);
        using var difference = correctedInspection.Difference(correctedMask);
        using var effectiveDomain = domain.Intersection(difference);
        // The C03 direct-SDK comparison removes threshold foreground on the top row and left column.
        // Keep the geometric ROI and generated output mask on the nominal domain.
        using var directRectangleInterior = width > 2 && height > 2 && pose is null && localRoi is not null
            && settings.SegmentationMode == BlobSegmentationMode.SingleThreshold && settings.Type == BlobThresholdMode.Binary
            && BlobLocalRoiRasterizer.IsDirectFullFrameRectangle(localRoi, width, height)
                ? HalconResourceBridge.CreateWithoutImplicitClipping(() => new HRegion(1d, 1d, height - 1d, width - 1d))
                : null;
        using var thresholdDomain = directRectangleInterior is null ? null : effectiveDomain.Intersection(directRectangleInterior);
        var transformed=BlobThresholdTransform.Apply(gray,thresholdDomain ?? effectiveDomain,settings,width,height,cancellation);
        using var work=transformed.Work;
        using var binary=transformed.Foreground;
        var outputResources=resources.CreateScope();
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
            var executionDiagnostics=new List<Diagnostic>();
            if (vm.MaxAreaInnerRectNum > 0)
                executionDiagnostics.Add(new("BLOB_INNER_RECT_LOCAL", "内接矩形使用 HALCON 轴对齐求解，原厂旋转策略和输出分配未确认。", BlobSchema.InnerRectanglesPort));
            // 填充面积阈值：0 跳过；T>0 填面积不超过 T 的封闭孔洞，填孔结果参与连通域与后续测量。
            using var holeFilled=vm.HoleMinArea>0?FillUpHoles(binary,thresholdDomain??effectiveDomain,vm.HoleMinArea,cancellation):null;
            if(holeFilled is not null)
                executionDiagnostics.Add(new("BLOB_HOLE_FILL_LOCAL","填充面积阈值按已核分支实现（0跳过、面积不超过阈值、贴工作区边界不填充）；原机逐像素等价未证。"));
            var blobRegion=holeFilled??binary;
            cancellation.ThrowIfCancellationRequested();
            HalconBlobExecutionScope.RequireUnlimitedConnection();
            using var connected=blobRegion.Connection();
            HalconBlobExecutionScope.RequireUnlimitedConnection();
            using var contactBoundary=vm.MaxOutPixelNumEnable?BuildContactBoundary(effectiveDomain):null;
            var candidates=FilterCandidates(connected,ExtractCandidates(connected,cancellation),vm,width,height,contactBoundary,cancellation);
            Func<Candidate,int>? integerKey=settings.SortFeature is BlobSortFeature.SortFeatureRectX or BlobSortFeature.SortFeatureRectY?value=>SortIntegerKey(value,settings.SortFeature):null;
            var retained=BlobCandidateSelection.SelectSlots(candidates,settings.BlobCountLimit,settings.SortFeature,settings.SortMode,value=>(float)value.Area,value=>SortFloatKey(value,settings.SortFeature),integerKey,cancellation);
            if(transformed.Receipt.Provenance=="LocalScalarFormulaCalculation")
                executionDiagnostics.Add(new("BLOB_THRESHOLD_LOCAL_CALCULATION","本轮阈值由本方按已核原标量公式计算；原厂动态阈值对外发布接口尚未证实。",BlobSchema.Field(8)));
            var ordered=BlobCandidateSelection.Order(retained,settings.SortMode,value=>SortFloatKey(value,settings.SortFeature),
                integerKey,value=>value.ComponentLabel,cancellation);
            // VM 判定组语义：启用的判定组逐 Blob 给出单项 OK/NG；筛选只决定保留集合，不等于合格。
            var perBlobPass=ResultProjectionLimitsPerBlob(vm,ordered.Select(x=>x.Candidate).ToArray(),out var overallPass,out var vmLimitMessages);
            var judged=ordered.Select((entry,index)=>entry with{Candidate=entry.Candidate with{IsPass=perBlobPass[index]}}).ToArray();
            var messages=new List<string>(vmLimitMessages);
            var isPass=(vm.OKWhenNumIsZero?judged.Length==0:judged.Length>0)
                &&overallPass&&judged.All(x=>x.Candidate.IsPass);
            bool unorderedContour = vm.BlobContourType == BlobContourType.NoneSequentialContour;
            bool boundaryRequested = vm.BlobContourType == BlobContourType.SequentialContour
                || requested.HasFlag(BlobOutputRequest.Boundary);
            var measurements = new List<BlobMeasurement>(judged.Length);
            foreach (var selection in judged)
            {
                var item=selection.Candidate;
                cancellation.ThrowIfCancellationRequested();
                using var native = connected.SelectObj(item.RegionIndex);
                var region = HalconResourceBridge.CaptureRegion(native, BlobSchema.ImageFrame, frame, outputResources);
                var boundary = boundaryRequested ? ReadBoundary(native, cancellation) : Array.Empty<BlobBoundaryPath>();
                var contour = unorderedContour ? Array.AsReadOnly(ReadUnorderedContour(native, cancellation)) : null;
                var innerRectangle = measurements.Count < vm.MaxAreaInnerRectNum ? ReadInnerRectangle(native) : (BlobOrientedBounds?)null;
                var grayStatistics=ReadGrayStatistics(native,gray);
                measurements.Add(new(item.ComponentLabel, measurements.Count, item.Area, item.Perimeter, item.X, item.Y, item.Circularity,
                    item.Rectangularity, item.Bounds, item.OrientedBounds, Array.AsReadOnly(item.Corners), Array.AsReadOnly(boundary), region, item.IsPass,
                    grayStatistics.Minimum, grayStatistics.Maximum, grayStatistics.Contrast, item.BlobAngleDegrees, contour, innerRectangle));
            }
            using var finalRegions = SelectRegions(connected, judged.Select(x => x.Candidate.RegionIndex).ToArray());
            IReadOnlyImage? blobImage=null;
            if(settings.OutputBlobImage)
            {
                using var candidateImage=BlobThresholdTransform.RegionImage(finalRegions,width,height);
                blobImage=HalImageSnapshotSource.Capture(candidateImage,frame,BlobSchema.ImageFrame,outputResources);
            }
            using var okRegions = SelectRegions(connected, judged.Where(x => x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            using var ngRegions = SelectRegions(connected, judged.Where(x => !x.Candidate.IsPass).Select(x => x.Candidate.RegionIndex).ToArray());
            var regionOutput = HalconResourceBridge.CaptureRegion(finalRegions, BlobSchema.ImageFrame, frame, outputResources);
            var okOutput = HalconResourceBridge.CaptureRegion(okRegions, BlobSchema.ImageFrame, frame, outputResources);
            var ngOutput = HalconResourceBridge.CaptureRegion(ngRegions, BlobSchema.ImageFrame, frame, outputResources);
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
            cancellation.ThrowIfCancellationRequested();
            var maskPolygons = BlobMaskInheritance.Publish(localRoi,pose);
            var resultGeometry = BlobResultGeometry.Capture(localRoi, input, isPass, domain, correctedInspection, correctedMask, pose,
                inspection is not null || imageMask is not null, mask is not null, maskPolygons, cancellation, fixture);
            if (resultGeometry.Groups.Any(group => group.Coverage == ResultGeometryCoverage.Unavailable))
                executionDiagnostics.Add(new("BLOB_ROI_DISPLAY_UNAVAILABLE", "部分像素区域无法在当前边界预算内完整显示。"));
            var output = new BlobOutput(settings, transformed.Receipt, executionDiagnostics, judged.Select((entry,index)=>new BlobSelectionEntry(entry.Candidate.ComponentLabel,entry.Candidate.RegionIndex,entry.Slot,index+1)), measurements, blobImage, binaryOutput, maskOutput, regionOutput, okOutput, ngOutput, whiteRatio, histogram, means, isPass, messages, new[] { outputResources }, resultGeometry,maskPolygons,
                vm.BlobContourType!=BlobContourType.NoneContour,cancellation,moduleStarted);
            cancellation.ThrowIfCancellationRequested();
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
