using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>One admitted source request. The provider returns an independently owned frozen frame.</summary>
public sealed class ImageSourceHostInputPreparer : IHostInputPreparer
{
    private readonly ICameraFrameProvider? _cameraService;
    private readonly string? _sdkAddress;
    private readonly SdkImageInput? _sdkImage;
    private int _readFinished;
    public ImageSourceHostInputPreparer(string? localAddress,long sequenceNumber,ICameraFrameProvider? cameraService=null,string? sdkAddress=null,SdkImageInput? sdkImage=null)
    {
        if(sequenceNumber<0)throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        LocalAddress=localAddress;SequenceNumber=sequenceNumber;_cameraService=cameraService;_sdkAddress=sdkAddress;_sdkImage=sdkImage;
    }
    public string? LocalAddress {get;}
    public long SequenceNumber {get;}
    public string CapabilityId=>"image-source";
    public int CapabilityVersion=>1;
    public IReadOnlyList<string> Services=>_sdkAddress is not null||_sdkImage is not null?new[]{"sdk-image"}:
        LocalAddress is null?new[]{"camera-frame"}:new[]{"local-image-read"};
    public Func<RunIdentity,CancellationToken,string>? LocalReadAdmission {get;init;}
    internal LocalImagePathCache? PathCache {get;init;}
    internal Action? ReadFinished {get;init;}
    internal void FinishRead(){if(Interlocked.Exchange(ref _readFinished,1)==0)ReadFinished?.Invoke();}
    public void Dispose()=>FinishRead();
    public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
    {
        var policy=ImageSourcePolicy.ReadFields(context.Identity.NodeId,context.Settings);
        context.Cancellation.ThrowIfCancellationRequested();
        var values=new Dictionary<string,object?>(StringComparer.Ordinal){["input.sequence"]=SequenceNumber};
        if(policy.SourceKind==ImageSourceKind.Sdk)
        {
            if(_sdkImage is not null)
            {
                var image=_sdkImage.Admit(context.Identity);
                try
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    values["input.sdk-image"]=image;
                    return new Prepared(values,null,context,null,null,image);
                }
                catch { image.Dispose();throw; }
            }
            if(string.IsNullOrEmpty(_sdkAddress)||!Path.IsPathFullyQualified(_sdkAddress))
                throw new InvalidOperationException("SDK 图像源缺少本轮外部图像。");
            values["input.sdk-address"]=_sdkAddress;
            return new Prepared(values,null,context,null,null);
        }
        if(_sdkAddress is not null||_sdkImage is not null)throw new InvalidOperationException("本轮 SDK 输入不属于当前图像来源模式。");
        if(policy.SourceKind==ImageSourceKind.Local)
        {
            if(LocalAddress is null)throw new InvalidOperationException("缺少本轮冻结的本地图像地址。");
            values.Add("input.local-address",LocalAddress);
            return new Prepared(values,null,context,LocalReadAdmission,PathCache);
        }
        if(LocalAddress is not null)throw new InvalidOperationException("相机来源不能消费本地文件输入。");
        if(string.IsNullOrWhiteSpace(policy.CameraDeviceId))throw new InvalidOperationException("请选择方案中的相机。");
        if(_cameraService is null)throw new InvalidOperationException("相机采集服务不可用。");
        ICameraFrameLease? frame=null;
        try
        {
            double? ReadOverride(string field)=>context.Settings.Contains(field)?(double?)context.Settings.Get<object?>(field):null;
            try
            {
            frame=await _cameraService.CaptureAsync(new CameraFrameRequest(policy.CameraDeviceId,
                CameraRequestPurpose.Workflow,context.Identity,TimeSpan.FromMilliseconds(policy.CameraTimeoutMs),
                context.Identity.RequestId.ToString("N"),ReadOverride("camera.exposure-time"),ReadOverride("camera.gain")),
                context.Cancellation).ConfigureAwait(false);
            }
            catch(CameraFrameAcquisitionException error)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                values.Add("input.device-id",policy.CameraDeviceId);
                values.Add("input.camera-failure",error.ErrorCode+": "+error.Message);
                return new Prepared(values,null,context,null,null);
            }
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
            var prepared=new Prepared(values,frame,context,LocalReadAdmission,null);frame=null;return prepared;
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
        private readonly IDisposable? _sdkFrame;
        private int _disposed;
        private int _bound;
        private readonly HostInputPreparationContext _context;
        private readonly Func<RunIdentity,CancellationToken,string>? _admission;
        private readonly LocalImagePathCache? _cache;
        private readonly IReadOnlyDictionary<string,object?> _values;
        internal Prepared(Dictionary<string,object?> values,ICameraFrameLease? frame,HostInputPreparationContext context,Func<RunIdentity,CancellationToken,string>? admission,LocalImagePathCache? cache,IDisposable? sdkFrame=null)
        { _values=new ReadOnlyDictionary<string,object?>(values);_frame=frame;_context=context;_admission=admission;_cache=cache;_sdkFrame=sdkFrame; }
        public IReadOnlyDictionary<string,object?> Values=>Volatile.Read(ref _disposed)==0 ? _values : throw new ObjectDisposedException(nameof(Prepared));
        public IToolExecutionContext BindContext(IToolExecutionContext inner)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed)!=0,this);
            if(Interlocked.Exchange(ref _bound,1)!=0)
                throw new InvalidOperationException("同一图像源输入租约只能绑定一次调用上下文。");
            return new LocalContext(inner,_context,_admission,_cache,()=>Volatile.Read(ref _disposed)==0);
        }
        public async ValueTask DisposeAsync(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;var frame=Interlocked.Exchange(ref _frame,null);if(frame is not null)await frame.DisposeAsync().ConfigureAwait(false);_sdkFrame?.Dispose();}
    }
    private sealed class LocalContext(IToolExecutionContext inner,HostInputPreparationContext request,Func<RunIdentity,CancellationToken,string>? admission,
        LocalImagePathCache? cache,Func<bool> current):IToolExecutionContext,ILocalImageReadContext,ILocalImageCacheContext
    {
        private string? _address;
        private int _opened;
        public RunIdentity Identity=>inner.Identity;
        public CancellationToken Cancellation=>inner.Cancellation;
        public IOutputResources Resources=>inner.Resources;
        public void Log(string message)=>inner.Log(message);
        public string AcceptLocalImage()
        {
            ObjectDisposedException.ThrowIf(!current(),this);
            var accept=admission??throw new InvalidOperationException("本轮没有已接入的本地图像读取接纳。");
            string? address=null;
            if(!request.Gate.TryInvoke(()=>address=accept(request.Identity,request.Cancellation)))
            {request.Cancellation.ThrowIfCancellationRequested();throw new InvalidOperationException("图像读取身份已退役、取消或被替换。");}
            return _address=address??throw new InvalidOperationException("图像读取接纳没有返回地址。");
        }
        public T OpenLocalImage<T>(string address,Func<T> load,Func<T,T> clone) where T:class,IDisposable
        {
            if(!current()||_address is null||!string.Equals(_address,address,StringComparison.Ordinal)
                ||Interlocked.Exchange(ref _opened,1)!=0)
                throw new InvalidOperationException("图像缓存仅允许本轮已接纳地址。");
            return cache is null?OpenCurrent(load):cache.Open(address,()=>OpenCurrent(load),value=>OpenCurrent(()=>clone(value)));
        }

        private T OpenCurrent<T>(Func<T> open) where T:class,IDisposable
        {
            EnsureCurrent();
            var image=open();
            try { EnsureCurrent();return image; }
            catch(Exception failure)
            {
                try { image.Dispose(); }
                catch(Exception cleanup) { throw new AggregateException("已退役的图像读取及释放均失败。",failure,cleanup); }
                throw;
            }
        }

        private void EnsureCurrent()
        {
            Cancellation.ThrowIfCancellationRequested();
            if(!current()||!request.Gate.TryInvoke(static()=>{}))
            {
                Cancellation.ThrowIfCancellationRequested();
                throw new InvalidOperationException("图像读取身份已退役、取消或被替换。");
            }
        }
    }
}
