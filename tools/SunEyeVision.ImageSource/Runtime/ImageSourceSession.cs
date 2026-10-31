using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

internal sealed class ImageSourceSession : IToolSession<ImageSourceSettings,ImageSourceOutput>
{
    private int _executing,_disposed;
    public ValueTask<ToolOutcome<ImageSourceOutput>> ExecuteAsync(ToolInvocation<ImageSourceSettings> invocation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
        if(Interlocked.Exchange(ref _executing,1)!=0)throw new InvalidOperationException("同一图像源会话不能并发执行。");
        try{return ValueTask.FromResult(Execute(invocation));}
        finally{Volatile.Write(ref _executing,0);}
    }
    private static ToolOutcome<ImageSourceOutput> Execute(ToolInvocation<ImageSourceSettings> invocation)
    {
        var cancellation=invocation.Context.Cancellation;cancellation.ThrowIfCancellationRequested();
        var settings=invocation.Settings;
        var errors=settings.Validate();
        if(errors.Count!=0)return new(false,null,BusinessJudgment.NotEvaluated,errors);
        if(!invocation.Inputs.Contains(ImageSourceTool.Sequence))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_SEQUENCE","缺少本轮冻结的SN输入。");
        var sequence=invocation.Inputs.Get<long>(ImageSourceTool.Sequence);
        if(sequence<0)return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_SEQUENCE","本轮SN不能为负数。");
        var hasFile=invocation.Inputs.Contains(ImageSourceTool.LocalAddress);
        var hasCamera=invocation.Inputs.Contains(ImageSourceTool.CameraImage);
        if(settings.SourceKind=="local"&&(!hasFile||hasCamera)||settings.SourceKind=="camera"&&(!hasCamera||hasFile))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_INPUT","本轮输入与冻结的图像来源不一致。");
        if(settings.SourceKind=="camera"&&string.IsNullOrWhiteSpace(settings.CameraDeviceId))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_CAMERA","请选择方案中的相机。");
        if(settings.SourceKind=="camera"&&(!invocation.Inputs.Contains(ImageSourceTool.DeviceId)||
            !string.Equals(invocation.Inputs.Get<string>(ImageSourceTool.DeviceId),settings.CameraDeviceId,StringComparison.OrdinalIgnoreCase)))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_CAMERA_IDENTITY","本轮帧与冻结的设备引用不一致。");
        var address=string.Empty;
        if(settings.SourceKind=="local")
        {
            if(invocation.Context is not ILocalImageReadContext reader)
                return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_ADMISSION","本轮缺少正式本地图像读取接纳能力。");
            try { address=reader.AcceptLocalImage(); }
            catch(OperationCanceledException){throw;}
            catch(Exception error){return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_ADMISSION",error.Message);}
        }
        cancellation.ThrowIfCancellationRequested();
        if(settings.SourceKind=="local"&&(!Path.IsPathFullyQualified(address)||!File.Exists(address)))
            return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_FILE","本轮图像文件地址无效或不可用。");
        IOutputResourceScope? resources=null;
        try
        {
            if(settings.SourceKind=="camera")
                ResourceContractRules.ValidateImage(invocation.Inputs.Get<IReadOnlyImage>(ImageSourceTool.CameraImage));
            // Native decode/copy has a bounded ownership scope; cancellation is checked before and after it.
            using var source=settings.SourceKind=="local"?new HImage(address):
                HalconResourceBridge.CopyImage(invocation.Inputs.Get<IReadOnlyImage>(ImageSourceTool.CameraImage));
            cancellation.ThrowIfCancellationRequested();
            // The format selector belongs to local files; camera output retains the captured frame format.
            using var converted=Convert(source,settings.SourceKind=="local"?settings.OutputFormat:"original");
            resources=invocation.Context.Resources.CreateScope();
            ImageFrameAddress Frame(string port)=>new(Guid.NewGuid(),"image-source:"+invocation.Context.Identity.NodeId,
                invocation.Context.Identity,port);
            var image=HalImageSnapshotSource.Capture(converted,Frame(ImageSourceOutput.Image),resources);
            IReadOnlyImage gray;
            // 语义占位：灰度原图暂复用同一受租约保护的图像。原件何时输出空灰度分支
            // 在当前取证条件下尚未完整确认；替换点为此处的灰度输出生成。
            if(image.Layout.Format==PixelLayout.Gray8)gray=image;
            else if(settings.SourceKind=="camera")
            {
                var pixels=image.CopyPixels();var bytes=new byte[checked(image.Width*image.Height)];
                for(var y=0;y<image.Height;y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for(var x=0;x<image.Width;x++)
                    {
                        var offset=y*image.Layout.RowStrideBytes+x*(image.Layout.Format==PixelLayout.Bgra32?4:3);
                        bytes[y*image.Width+x]=(byte)((76*pixels[offset+2]+150*pixels[offset+1]+30*pixels[offset])>>8);
                    }
                }
                var frame=Frame(ImageSourceOutput.GrayImage);
                using var reader=image.EffectiveDomain.AcquireReader();var runs=new List<RegionRun>();
                for(var offset=0;offset<reader.RunCount;offset+=4096)
                    runs.AddRange(reader.ReadRuns(offset,4096).ToArray());
                var domain=resources.CreateRegion(reader.Frame,frame,runs.ToArray());
                gray=resources.CreateImage(new ImageLayout(image.Width,image.Height,image.Width,PixelLayout.Gray8),frame,domain,bytes);
            }
            else
            {
                using var grayNative=HalconImageAdmission.Gray(converted);
                gray=HalImageSnapshotSource.Capture(grayNative,Frame(ImageSourceOutput.GrayImage),resources);
            }
            cancellation.ThrowIfCancellationRequested();
            var values=new Dictionary<string,object?> {
                [ImageSourceOutput.Image]=new RecordValue(ImageSourceOutput.ImageType,new Dictionary<string,object?> {
                    [ImageSourceOutput.ImageData]=image,[ImageSourceOutput.ImageWidth]=(long)image.Width,
                    [ImageSourceOutput.ImageHeight]=(long)image.Height,[ImageSourceOutput.ImageFormat]=image.Layout.Format.ToString()}),
                [ImageSourceOutput.GrayImage]=new RecordValue(ImageSourceOutput.GrayImageType,new Dictionary<string,object?> {
                    [ImageSourceOutput.GrayData]=gray,[ImageSourceOutput.GrayWidth]=(long)gray.Width,
                    [ImageSourceOutput.GrayHeight]=(long)gray.Height,[ImageSourceOutput.GrayFormat]=gray.Layout.Format.ToString()}),
                [ImageSourceOutput.SourceKind]=settings.SourceKind,
                [ImageSourceOutput.DeviceId]=settings.SourceKind=="camera"?settings.CameraDeviceId:string.Empty,
                [ImageSourceOutput.FileReference]=address,[ImageSourceOutput.FileName]=address.Length==0?string.Empty:Path.GetFileName(address),[ImageSourceOutput.Sequence]=sequence,
                [ImageSourceOutput.FrameId]=settings.SourceKind=="camera"&&invocation.Inputs.Contains(ImageSourceTool.DeviceFrameId)?invocation.Inputs.Get<string>(ImageSourceTool.DeviceFrameId):string.Empty,
                [ImageSourceOutput.Timestamp]=settings.SourceKind=="camera"&&invocation.Inputs.Contains(ImageSourceTool.DeviceTimestamp)?invocation.Inputs.Get<string>(ImageSourceTool.DeviceTimestamp):string.Empty
            };
            var output=new ImageSourceOutput(values,resources);resources=null;
            return ToolOutcome<ImageSourceOutput>.Completed(output);
        }
        catch(OperationCanceledException){throw;}
        catch(Exception error){return ToolOutcome<ImageSourceOutput>.Failed("IMAGE_SOURCE_READ",error.Message);}
        finally{resources?.Dispose();}
    }
    private static HImage Convert(HImage source,string format)
    {
        if(format=="original")return source.Clone();
        if(format=="mono8")return HalconImageAdmission.Gray(source);
        using var count=source.CountChannels();
        if(count.I==3)return source.Clone();
        if(count.I==1)
        {
            HOperatorSet.Compose3(source,source,source,out var rgb);
            using(rgb)return new HImage(rgb);
        }
        if(count.I==4)
        {
            using var red=source.AccessChannel(1);using var green=source.AccessChannel(2);using var blue=source.AccessChannel(3);
            HOperatorSet.Compose3(red,green,blue,out var rgb);
            using(rgb)return new HImage(rgb);
        }
        throw new NotSupportedException("图像源仅支持单通道、RGB和RGBA格式。");
    }
    public ValueTask DisposeAsync(){Interlocked.Exchange(ref _disposed,1);return ValueTask.CompletedTask;}
}
