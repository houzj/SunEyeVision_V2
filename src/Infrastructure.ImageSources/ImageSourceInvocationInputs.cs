using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>One admitted source request. The provider returns an independently owned frozen frame.</summary>
public sealed class ImageSourceHostInputPreparer : IHostInputPreparer
{
    private readonly ICameraFrameProvider? _cameraService;
    private int _readFinished;
    public ImageSourceHostInputPreparer(string? localAddress,long sequenceNumber,ICameraFrameProvider? cameraService=null)
    {
        if(sequenceNumber<0)throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        LocalAddress=localAddress;SequenceNumber=sequenceNumber;_cameraService=cameraService;
    }
    public string? LocalAddress {get;}
    public long SequenceNumber {get;}
    public string CapabilityId=>"image-source";
    public int CapabilityVersion=>1;
    public IReadOnlyList<string> Services=>LocalAddress is null ? new[]{"camera-frame"} : new[]{"local-image-read"};
    public Func<RunIdentity,CancellationToken,string>? LocalReadAdmission {get;init;}
    internal Action? ReadFinished {get;init;}
    internal void FinishRead(){if(Interlocked.Exchange(ref _readFinished,1)==0)ReadFinished?.Invoke();}
    public void Dispose()=>FinishRead();
    public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
    {
        var policy=ImageSourcePolicy.ReadFields(context.Identity.NodeId,context.Settings);
        context.Cancellation.ThrowIfCancellationRequested();
        var values=new Dictionary<string,object?>(StringComparer.Ordinal){["input.sequence"]=SequenceNumber};
        if(policy.SourceKind==ImageSourceKind.Local)
        {
            if(LocalAddress is null)throw new InvalidOperationException("缺少本轮冻结的本地图像地址。");
            values.Add("input.local-address",LocalAddress);
            return new Prepared(values,null,context,LocalReadAdmission);
        }
        if(LocalAddress is not null)throw new InvalidOperationException("相机来源不能消费本地文件输入。");
        if(string.IsNullOrWhiteSpace(policy.CameraDeviceId))throw new InvalidOperationException("请选择方案中的相机。");
        if(_cameraService is null)throw new InvalidOperationException("相机采集服务不可用。");
        ICameraFrameLease? frame=null;
        try
        {
            frame=await _cameraService.CaptureAsync(new CameraFrameRequest(policy.CameraDeviceId,
                CameraRequestPurpose.Workflow,context.Identity,TimeSpan.FromMilliseconds(policy.CameraTimeoutMs),
                context.Identity.RequestId.ToString("N")),context.Cancellation).ConfigureAwait(false);
            context.Cancellation.ThrowIfCancellationRequested();
            if(!string.Equals(frame.DeviceId,policy.CameraDeviceId,StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(frame.Image.Frame.SourceId,"camera:"+frame.DeviceId,StringComparison.OrdinalIgnoreCase) ||
               frame.Image.Frame.ProducerRun!=context.Identity)
                throw new InvalidOperationException("相机帧的设备或本次运行身份不匹配。");
            ResourceContractRules.ValidateImage(frame.Image);
            values.Add("input.camera-image",frame.Image);
            values.Add("input.device-id",frame.DeviceId);
            values.Add("input.device-frame-id",frame.DeviceFrameId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add("input.device-timestamp",frame.Timestamp.ToString("O",System.Globalization.CultureInfo.InvariantCulture));
            var prepared=new Prepared(values,frame,context,LocalReadAdmission);frame=null;return prepared;
        }
        catch(Exception failure)
        {
            if(frame is not null)try{await frame.DisposeAsync().ConfigureAwait(false);}
            catch(Exception cleanup){throw new AggregateException("图像源输入获取及资源清理均失败。",failure,cleanup);}
            throw;
        }
    }
    private sealed class Prepared : IPreparedHostInputs
    {
        private ICameraFrameLease? _frame;
        private int _disposed;
        private readonly HostInputPreparationContext _context;
        private readonly Func<RunIdentity,CancellationToken,string>? _admission;
        private readonly IReadOnlyDictionary<string,object?> _values;
        internal Prepared(Dictionary<string,object?> values,ICameraFrameLease? frame,HostInputPreparationContext context,Func<RunIdentity,CancellationToken,string>? admission)
        { _values=new ReadOnlyDictionary<string,object?>(values);_frame=frame;_context=context;_admission=admission; }
        public IReadOnlyDictionary<string,object?> Values=>Volatile.Read(ref _disposed)==0 ? _values : throw new ObjectDisposedException(nameof(Prepared));
        public IToolExecutionContext BindContext(IToolExecutionContext inner)=>Volatile.Read(ref _disposed)==0 ? new LocalContext(inner,_context,_admission) : throw new ObjectDisposedException(nameof(Prepared));
        public async ValueTask DisposeAsync(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var frame=Interlocked.Exchange(ref _frame,null);if(frame is not null)await frame.DisposeAsync().ConfigureAwait(false);}
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
