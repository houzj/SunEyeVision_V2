using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Workflow;

/// <summary>Host-selected immutable input for one source in one round; not persistent settings.</summary>
public sealed record ImageSourceRunInput
{
    public ImageSourceRunInput(string? localAddress, long sequenceNumber)
    {
        if(sequenceNumber<0)throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        LocalAddress=localAddress;SequenceNumber=sequenceNumber;
    }
    public string? LocalAddress {get;}
    public long SequenceNumber {get;}
}

/// <summary>One source's acquired input lease. Shared by formal execution and private-draft debug.</summary>
internal sealed class ImageSourceInvocationInputs : IDisposable
{
    private IDisposable? _imageOwner;
    public IReadOnlyDictionary<string,object?> Values {get;}
    private ImageSourceInvocationInputs(Dictionary<string,object?> values,IDisposable? imageOwner=null)
    {Values=new ReadOnlyDictionary<string,object?>(values);_imageOwner=imageOwner;}

    internal static ImageSourceRunInput PrepareDebug(WorkflowNodeBase node,IResourcePathResolver? resourcePaths)
    {
        var policy=ImageSourcePolicy.Read(node)??throw new InvalidOperationException("图像源没有正式Runtime设置。");
        if(policy.SourceKind==ImageSourceKind.Camera)return new(null,policy.SNInitialValue);
        var eligible=Enumerable.Range(0,policy.FilePaths.Count)
            .Where(index=>policy.RunMode==0||policy.ForRunPaths.Contains(policy.FilePaths[index],StringComparer.Ordinal)).ToArray();
        var selected=policy.CurrentIndex>=0?policy.CurrentIndex:eligible.FirstOrDefault(-1);
        if(!eligible.Contains(selected))throw new InvalidOperationException("请选择可运行的本地图像。");
        var resolver=resourcePaths as Persistence.SolutionResourceResolver
            ??throw new InvalidOperationException("图像源调试缺少冻结资源基目录。");
        var address=resolver.ResolveAddress("ImageSourceSettings.FileReferences",policy.FilePaths[selected]);
        return new(address,policy.SNInitialValue);
    }

    internal static async Task<ImageSourceInvocationInputs> CaptureAsync(WorkflowNodeBase frozenNode,ImageSourceRunInput input,
        RunIdentity identity,ICameraCaptureService? cameraService,CameraFrame? borrowedFrame,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var policy=ImageSourcePolicy.Read(frozenNode)??throw new InvalidOperationException("图像源没有正式Runtime设置。");
        var values=new Dictionary<string,object?>(StringComparer.Ordinal){["input.sequence"]=input.SequenceNumber};
        if(policy.SourceKind==ImageSourceKind.Local)
        {
            if(input.LocalAddress is null)throw new InvalidOperationException("缺少本轮冻结的本地图像地址。");
            values.Add("input.local-address",input.LocalAddress);
            return new(values);
        }
        if(input.LocalAddress is not null)throw new InvalidOperationException("相机来源不能消费本地文件输入。");
        if(string.IsNullOrWhiteSpace(policy.CameraDeviceId))throw new InvalidOperationException("请选择方案中的相机。");
        CameraFrame? frame=borrowedFrame;var ownsFrame=false;
        SunEyeVision.Plugin.Runtime.FrozenImageResource? image=null;
        try
        {
            if(frame is null)
            {
                if(cameraService is null)throw new InvalidOperationException("相机采集服务不可用。");
                frame=await cameraService.CaptureAsync(new CameraCaptureRequest {
                    CameraRef=new(policy.CameraDeviceId),TriggerMode=CameraTriggerMode.Continuous,
                    Timeout=TimeSpan.FromMilliseconds(policy.CameraTimeoutMs),DiagnosticCorrelationId=identity.RequestId.ToString("N")
                },cancellation).ConfigureAwait(false);
                ownsFrame=true;
            }
            cancellation.ThrowIfCancellationRequested();
            if(frame.IsDisposed||!string.Equals(frame.CameraRef.Value,policy.CameraDeviceId,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("本轮相机帧已失效或设备身份不匹配。");
            if(frame.Image is not ITypedImageSnapshotSource typed)
                throw new InvalidOperationException("相机适配器尚未提供实际Domain和像素布局快照，不能用猜测的全图区域替代。");
            image=typed.CaptureSnapshot(new ImageFrameAddress(Guid.NewGuid(),"camera:"+frame.CameraRef.Value,identity));
            ResourceContractRules.ValidateImage(image);cancellation.ThrowIfCancellationRequested();
            values.Add("input.camera-image",image);
            values.Add("input.device-id",frame.CameraRef.Value);
            values.Add("input.device-frame-id",frame.FrameId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add("input.device-timestamp",frame.Timestamp.ToString("O",System.Globalization.CultureInfo.InvariantCulture));
            // Release an acquired device frame before transferring the independent snapshot.
            // If frame cleanup throws, the snapshot remains owned by this finally block.
            if(ownsFrame){ownsFrame=false;frame.Dispose();}
            var lease=new ImageSourceInvocationInputs(values,image);image=null;return lease;
        }
        catch(Exception failure)
        {
            var cleanup=new List<Exception>();
            try{image?.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(ownsFrame)try{frame?.Dispose();}catch(Exception error){cleanup.Add(error);}
            if(cleanup.Count!=0)
                throw new AggregateException("图像源输入获取及资源清理均失败。",new[]{failure}.Concat(cleanup));
            throw;
        }
    }
    public void Dispose()=>Interlocked.Exchange(ref _imageOwner,null)?.Dispose();
}
