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
public sealed class ImageSourceHostInputPreparer : IHostInputPreparer
{
    public ImageSourceHostInputPreparer(string? localAddress, long sequenceNumber, ICameraCaptureService? cameraService=null, CameraFrame? borrowedFrame=null)
    {
        if(sequenceNumber<0)throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        LocalAddress=localAddress;SequenceNumber=sequenceNumber;_cameraService=cameraService;_borrowedFrame=borrowedFrame;
    }
    public string? LocalAddress {get;}
    public long SequenceNumber {get;}
    /// <summary>Run-scoped admission supplied by the host, never a persistent setting.</summary>
    private readonly ICameraCaptureService? _cameraService;
    private readonly CameraFrame? _borrowedFrame;
    public string CapabilityId=>"image-source";
    public int CapabilityVersion=>1;
    public IReadOnlyList<string> Services=>LocalAddress is null ? new[]{"camera-frame"} : new[]{"local-image-read"};
    public Func<RunIdentity,CancellationToken,string>? LocalReadAdmission {get;init;}
    internal Action? ReadFinished {get;init;}
    private int _readFinished;
    internal void FinishRead()
    {
        if(Interlocked.Exchange(ref _readFinished,1)==0)ReadFinished?.Invoke();
    }
    public void Dispose()=>FinishRead();
    public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
    {
        var policy=ImageSourcePolicy.ReadFields(context.Identity.NodeId,context.Settings);
        var acquired=await ImageSourceInvocationInputs.CaptureAsync(policy,this,context.Identity,_cameraService,_borrowedFrame,context.Cancellation).ConfigureAwait(false);
        return new Prepared(acquired,context,LocalReadAdmission);
    }
    private sealed class Prepared(ImageSourceInvocationInputs acquired,HostInputPreparationContext context,Func<RunIdentity,CancellationToken,string>? admission):IPreparedHostInputs
    {
        private ImageSourceInvocationInputs? _acquired=acquired;
        public IReadOnlyDictionary<string,object?> Values=>_acquired?.Values??throw new ObjectDisposedException(nameof(Prepared));
        public IToolExecutionContext BindContext(IToolExecutionContext inner)=>new LocalContext(inner,context,admission);
        public ValueTask DisposeAsync(){Interlocked.Exchange(ref _acquired,null)?.Dispose();return ValueTask.CompletedTask;}
    }
    private sealed class LocalContext(IToolExecutionContext inner,HostInputPreparationContext request,Func<RunIdentity,CancellationToken,string>? admission):IToolExecutionContext,ILocalImageReadContext
    {
        public RunIdentity Identity=>inner.Identity;
        public CancellationToken Cancellation=>inner.Cancellation;
        public IOutputResources Resources=>inner.Resources;
        public void Log(string message)=>inner.Log(message);
        public string AcceptLocalImage()
        {
            var accept=admission??throw new InvalidOperationException("本轮没有已接入的本地图像读取接纳。");
            string? address=null;
            if(!request.Gate.TryInvoke(()=>address=accept(request.Identity,request.Cancellation)))
            {request.Cancellation.ThrowIfCancellationRequested();throw new InvalidOperationException("图像读取身份已退役、取消或被替换。");}
            return address??throw new InvalidOperationException("图像读取接纳没有返回地址。");
        }
    }
}

/// <summary>One source's acquired input lease. Shared by formal execution and private-draft debug.</summary>
internal sealed class ImageSourceInvocationInputs : IDisposable
{
    private IDisposable? _imageOwner;
    public IReadOnlyDictionary<string,object?> Values {get;}
    private ImageSourceInvocationInputs(Dictionary<string,object?> values,IDisposable? imageOwner=null)
    {Values=new ReadOnlyDictionary<string,object?>(values);_imageOwner=imageOwner;}

    internal static async Task<ImageSourceInvocationInputs> CaptureAsync(ImageSourcePolicy policy,ImageSourceHostInputPreparer input,
        RunIdentity identity,ICameraCaptureService? cameraService,CameraFrame? borrowedFrame,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
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
