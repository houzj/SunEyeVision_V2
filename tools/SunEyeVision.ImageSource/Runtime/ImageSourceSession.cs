using HalconDotNet;
using System.Diagnostics;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

internal sealed class ImageSourceSession : IToolSession<ImageSourceSettings,ImageSourceOutput>
{
    private int _executing,_disposed;
    private readonly object _lifetimeGate = new();
    private ImageSourceRunState? _directState;
    public ValueTask<ToolOutcome<ImageSourceOutput>> ExecuteAsync(ToolInvocation<ImageSourceSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        if(Interlocked.Exchange(ref _executing,1)!=0)throw new InvalidOperationException("同一图像源会话不能并发执行。");
        try
        {
            lock(_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
                return ValueTask.FromResult(Execute(invocation));
            }
        }
        finally{Volatile.Write(ref _executing,0);}
    }
    private ToolOutcome<ImageSourceOutput> Execute(ToolInvocation<ImageSourceSettings> invocation)
    {
        var cancellation=invocation.Context.Cancellation;cancellation.ThrowIfCancellationRequested();
        var settings=invocation.Settings;
        var errors=settings.Validate();
        if(errors.Count!=0)return new(false,null,BusinessJudgment.NotEvaluated,errors);
        if(!ImageSourceTool.AllowsTrigger(settings))
            return new(false,null,BusinessJudgment.NotEvaluated,Array.Empty<Diagnostic>(),Suppressed:true);
        if(!invocation.Inputs.Contains(ImageSourceTool.Sequence))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_SEQUENCE","缺少本轮冻结的SN输入。");
        var sequence=invocation.Inputs.Get<long>(ImageSourceTool.Sequence);
        if(sequence<0)return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_SEQUENCE","本轮SN不能为负数。");
        var hasFile=invocation.Inputs.Contains(ImageSourceTool.LocalAddress);
        var hasCamera=invocation.Inputs.Contains(ImageSourceTool.CameraImage);
        var cameraFailure=invocation.Inputs.Contains(ImageSourceTool.CameraFailure);
        var hasSdkFile=invocation.Inputs.Contains(ImageSourceTool.SdkAddress);
        var hasSdkImage=invocation.Inputs.Contains(ImageSourceTool.SdkImage);
        var isSdk=settings.SourceKind=="sdk";
        if(hasFile!=(settings.SourceKind=="local")||(hasCamera||cameraFailure)!=(settings.SourceKind=="camera")||hasCamera&&cameraFailure
            ||(hasSdkFile||hasSdkImage)!=isSdk||hasSdkFile&&hasSdkImage)
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_INPUT","本轮输入与冻结的图像来源不一致。");
        if(cameraFailure&&string.IsNullOrWhiteSpace(invocation.Inputs.Get<string>(ImageSourceTool.CameraFailure)))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_INPUT","相机失败输入必须包含实际采集诊断。");
        if(settings.SourceKind=="camera"&&string.IsNullOrWhiteSpace(settings.CameraDeviceId))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_CAMERA","请选择方案中的相机。");
        if(settings.SourceKind=="camera"&&(!invocation.Inputs.Contains(ImageSourceTool.DeviceId)||
            !string.Equals(invocation.Inputs.Get<string>(ImageSourceTool.DeviceId),settings.CameraDeviceId,StringComparison.OrdinalIgnoreCase)))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_CAMERA_IDENTITY","本轮帧与冻结的设备引用不一致。");
        var moduleStarted=Stopwatch.GetTimestamp();
        var address=string.Empty;
        if(settings.SourceKind=="local")
        {
            if(invocation.Context is not ILocalImageReadContext reader)
                return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_ADMISSION","本轮缺少正式本地图像读取接纳能力。");
            try { address=reader.AcceptLocalImage(); }
            catch(OperationCanceledException){throw;}
            catch(Exception error){return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_ADMISSION",error.Message);}
        }
        else if(hasSdkFile)
            address=invocation.Inputs.Get<string>(ImageSourceTool.SdkAddress);
        cancellation.ThrowIfCancellationRequested();
        if(!hasCamera&&!cameraFailure&&!hasSdkImage&&!Path.IsPathFullyQualified(address))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_FILE","本轮图像文件地址无效或不可用。");
        IOutputResourceScope? resources=null;
        Dictionary<string,object?>? produced=null;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
            var processing=invocation.Context as IImageSourceProcessingContext;
            var state = processing is not null
                ? processing.GetProcessingState(() => new ImageSourceRunState())
                : _directState ??= new ImageSourceRunState();
            var sequenceRevision=processing?.GetConfigurationRevision(ImageSourceFields.InitialSequence)??Guid.Empty;
            state.ObserveSource(settings.SourceKind,settings.CameraDeviceId,
                processing?.GetConfigurationRevision(ImageSourceFields.SourceKind)??Guid.Empty,
                processing?.GetConfigurationRevision(ImageSourceFields.CameraDevice)??Guid.Empty);
            if(cameraFailure)
            {
                produced=new()
                {
                    // Native Process reaches its common success epilogue after publishing the count.
                    // This status does not grant host execution success or supply an image.
                    [ImageSourceOutput.ModuleStatus]=1L,
                    [ImageSourceOutput.CameraFailureCount]=state.CameraFailures(increment:true),
                    [ImageSourceOutput.SourceKind]=settings.SourceKind,[ImageSourceOutput.DeviceId]=settings.CameraDeviceId
                };
                return Failed("IMAGE_SOURCE_CAPTURE",invocation.Inputs.Get<string>(ImageSourceTool.CameraFailure));
            }
            var inputImage=hasCamera?invocation.Inputs.Get<IReadOnlyImage>(ImageSourceTool.CameraImage):
                hasSdkImage?invocation.Inputs.Get<IReadOnlyImage>(ImageSourceTool.SdkImage):null;
            if(inputImage is not null)ResourceContractRules.ValidateImage(inputImage);
            if(hasSdkImage&&inputImage!.Frame.ProducerRun!=invocation.Context.Identity)
                return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_SDK_IDENTITY","SDK 图像帧不属于本次执行。");
            produced=new(){[ImageSourceOutput.ModuleStatus]=0L};
            if(hasCamera)
            {
                sequence=state.NextSequence(sequence,sequenceRevision);
                produced=new()
                {
                    [ImageSourceOutput.ModuleStatus]=0L,
                    [ImageSourceOutput.Sequence]=sequence,[ImageSourceOutput.SourceKind]=settings.SourceKind,
                    [ImageSourceOutput.DeviceId]=settings.CameraDeviceId,[ImageSourceOutput.CameraFailureCount]=state.CameraFailures()
                };
                if(invocation.Inputs.Contains(ImageSourceTool.DeviceFrameId))produced[ImageSourceOutput.FrameId]=invocation.Inputs.Get<string>(ImageSourceTool.DeviceFrameId);
                if(invocation.Inputs.Contains(ImageSourceTool.DeviceTimestamp))produced[ImageSourceOutput.Timestamp]=invocation.Inputs.Get<string>(ImageSourceTool.DeviceTimestamp);
            }
            // Native decode/copy has a bounded ownership scope; cancellation is checked before and after it.
            using var source=inputImage is not null?HalconResourceBridge.CopyImage(inputImage):
                hasFile&&invocation.Context is ILocalImageCacheContext cache
                    ?cache.OpenLocalImage(address,DecodeLocalImage, image=>image.Clone())
                    :DecodeLocalImage();
            cancellation.ThrowIfCancellationRequested();
            // The format selector belongs to local files; camera output retains the captured frame format.
            var format=settings.SourceKind=="local"?settings.OutputFormat:"original";
            resources=invocation.Context.Resources.CreateScope();
            ImageFrameAddress Frame(string port)=>new(Guid.NewGuid(),"image-source:"+invocation.Context.Identity.NodeId,
                invocation.Context.Identity,port);
            var imageFrame=Frame(ImageSourceOutput.Image);
            IReadOnlyImage image;
            using var channels=source.CountChannels();
            if(format=="mono8"&&channels.I!=1)
            {
                using var color=HalImageSnapshotSource.Capture(source,imageFrame);
                image=CreateGrayImage(color,imageFrame,resources,cancellation);
            }
            else if(format=="rgb24"&&channels.I!=3)
            {
                using var rgb=ConvertToRgb(source,channels.I);
                image=HalImageSnapshotSource.Capture(rgb,imageFrame,resources,rgb24:true);
            }
            else image=HalImageSnapshotSource.Capture(source,imageFrame,resources,
                rgb24:format=="rgb24"||inputImage?.Layout.Format==PixelLayout.Rgb24||hasSdkFile&&channels.I==3);

            if (settings.StitchEnabled && settings.StitchHeight > 0)
            {
                image = state.Stitch.Process(image, settings, resources, cancellation);
            }
            var gray=settings.OutputMono8&&image.Layout.Format!=PixelLayout.Gray8
                ?CreateGrayImage(image,Frame(ImageSourceOutput.GrayImage),resources,cancellation)
                :null;
            cancellation.ThrowIfCancellationRequested();
            if(hasFile)sequence=state.NextSequence(sequence,sequenceRevision);
            var values=new Dictionary<string,object?> {
                [ImageSourceOutput.ModuleStatus]=1L,
                [ImageSourceOutput.Image]=ImageSourceOutput.FreezeImage(image,ImageSourceOutput.ImageType),
                [ImageSourceOutput.GrayImage]=gray is null?null:ImageSourceOutput.FreezeImage(gray,ImageSourceOutput.GrayImageType),
                [ImageSourceOutput.SourceKind]=settings.SourceKind,
                [ImageSourceOutput.DeviceId]=settings.SourceKind=="camera"?settings.CameraDeviceId:string.Empty,
                [ImageSourceOutput.FileReference]=hasFile?address:string.Empty,[ImageSourceOutput.FileName]=hasFile?Path.GetFileName(address):string.Empty,
                [ImageSourceOutput.Sequence]=isSdk?0L:sequence,
                [ImageSourceOutput.FrameId]=settings.SourceKind=="camera"&&invocation.Inputs.Contains(ImageSourceTool.DeviceFrameId)?invocation.Inputs.Get<string>(ImageSourceTool.DeviceFrameId):string.Empty,
                [ImageSourceOutput.Timestamp]=settings.SourceKind=="camera"&&invocation.Inputs.Contains(ImageSourceTool.DeviceTimestamp)?invocation.Inputs.Get<string>(ImageSourceTool.DeviceTimestamp):string.Empty
            };
            if(hasCamera)values[ImageSourceOutput.CameraFailureCount]=state.CameraFailures();
            values.Add(ImageSourceOutput.ModuleRunTime,(double)(float)Stopwatch.GetElapsedTime(moduleStarted).TotalMilliseconds);
            var output=new ImageSourceOutput(values,resources);
            cancellation.ThrowIfCancellationRequested();
            resources=null;
            return ToolOutcome<ImageSourceOutput>.Completed(output);
        }
        catch(OperationCanceledException){throw;}
        catch(FileNotFoundException error){return Failed("IMAGE_SOURCE_FILE",error.Message);}
        catch(ImageSourceProcessingException error){return Failed("IMAGE_SOURCE_STITCH",error.Message,error.NativeErrorCode);}
        catch(Exception error){return Failed("IMAGE_SOURCE_READ",error.Message);}
        finally{resources?.Dispose();}

        ToolOutcome<ImageSourceOutput> Failed(string code,string message,uint? nativeErrorCode=null)
        {
            var diagnostics=new[]{new Diagnostic(code,message){NativeErrorCode=nativeErrorCode}};
            if(produced is null)return new(false,null,BusinessJudgment.NotEvaluated,diagnostics);
            var unpublished=resources;
            resources=null;
            unpublished?.Dispose();
            invocation.Context.Cancellation.ThrowIfCancellationRequested();
            var availableResources=invocation.Context.Resources.CreateScope();
            try
            {
                var output=new ImageSourceOutput(produced,availableResources,availableOnly:true);
                return new(false,output,BusinessJudgment.NotEvaluated,diagnostics);
            }
            catch{availableResources.Dispose();throw;}
        }

        HImage DecodeLocalImage()
        {
            if(!File.Exists(address))throw new FileNotFoundException("本轮图像文件地址无效或不可用。",address);
            return new HImage(address);
        }
    }
    private static IReadOnlyImage CreateGrayImage(IReadOnlyImage image,ImageFrameAddress frame,
        IOutputResourceScope resources,CancellationToken cancellation)
    {
        var channels=image.Layout.BytesPerPixel;
        var red=image.Layout.Format==PixelLayout.Rgb24?0:2;
        var blue=2-red;
        var bytes=new byte[checked(image.Width*image.Height)];
        using var pixels=image.AcquirePixels();
        for(var y=0;y<image.Height;y++)
        {
            cancellation.ThrowIfCancellationRequested();
            var row=pixels.GetRow(y);
            for(var x=0;x<image.Width;x++)
            {
                var offset=x*channels;
                bytes[y*image.Width+x]=(byte)((76*row[offset+red]+150*row[offset+1]+30*row[offset+blue])>>8);
            }
        }
        using var reader=image.EffectiveDomain.AcquireReader();
        var runs=new List<RegionRun>(reader.RunCount);
        for(var offset=0;offset<reader.RunCount;offset+=4096)
        {
            cancellation.ThrowIfCancellationRequested();
            runs.AddRange(reader.ReadRuns(offset,4096).ToArray());
        }
        var domain=resources.CreateRegion(reader.Frame,frame,runs.ToArray());
        return resources.CreateImage(new ImageLayout(image.Width,image.Height,image.Width,PixelLayout.Gray8),frame,domain,bytes);
    }
    private static HImage ConvertToRgb(HImage source,int channels)
    {
        if(channels==1)
        {
            HOperatorSet.Compose3(source,source,source,out var rgb);
            using(rgb)return new HImage(rgb);
        }
        if(channels==4)
        {
            using var red=source.AccessChannel(1);using var green=source.AccessChannel(2);using var blue=source.AccessChannel(3);
            HOperatorSet.Compose3(red,green,blue,out var rgb);
            using(rgb)return new HImage(rgb);
        }
        throw new NotSupportedException("图像源仅支持单通道、RGB和RGBA格式。");
    }
    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
            if(Interlocked.Exchange(ref _disposed,1)==0)Interlocked.Exchange(ref _directState,null)?.Dispose();
        return ValueTask.CompletedTask;
    }
}
