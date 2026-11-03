using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;
using SunEyeVision.DeviceDriver.Cameras.Industrial;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;

var options=new SimulatedCameraOptions{Width=4,Height=3,FrameDelay=TimeSpan.Zero};
await using var manager=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(options)});
manager.RegisterDevices(await new SimulatedCameraDiscoveryProvider(options).DiscoverAsync(CancellationToken.None));
var identity=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"source",Guid.NewGuid());
var request=new CameraFrameRequest(options.DeviceId,CameraRequestPurpose.Workflow,identity,TimeSpan.FromSeconds(2));
var lease=await manager.CaptureAsync(request,CancellationToken.None);
if(lease.Image.Layout.Format!=PixelLayout.Gray8 || lease.Image.Frame.ProducerRun!=identity || lease.Image.CopyPixels().Length!=12)throw new Exception("帧契约错误");
var reader=lease.Image.AcquirePixels();await lease.DisposeAsync();
using(var domain=reader.AcquireDomainReader()) if(reader.GetRow(0).Length!=4 || domain.RunCount!=3)throw new Exception("reader 未独立保留帧");
using(manager.AcquireCaptureLease(new CameraDeviceRef(options.DeviceId),"预览"))
{
    try{await manager.CaptureAsync(request,CancellationToken.None);throw new Exception("互斥未拒绝");}
    catch(InvalidOperationException error) when(error.Message.Contains("预览")){}
}
using(var cancelled=new CancellationTokenSource())
{
    cancelled.Cancel();try{await manager.CaptureAsync(new CameraFrameRequest("not-registered",CameraRequestPurpose.Workflow,identity,TimeSpan.FromSeconds(1)),cancelled.Token);throw new Exception("取消未生效");}
    catch(OperationCanceledException){}
}
await using(var unsupported=new CameraRuntimeManager(new[]{new IndustrialCameraAdapterFactory(new IndustrialCameraDiscoveryProvider())}))
{
    const string id="industrial-camera:unverified";
    unsupported.RegisterDevice(new CameraDescriptor{DeviceId=id,CameraRef=new CameraDeviceRef(id),Manufacturer="Hikvision",Protocol=CameraProtocolKind.GenICam});
    try{await unsupported.CaptureAsync(new CameraFrameRequest(id,CameraRequestPurpose.Workflow,identity,TimeSpan.FromSeconds(1)),CancellationToken.None);throw new Exception("未知有效域意外准入");}
    catch(InvalidOperationException error) when(error.Message.Contains("有效域尚未确认")){}
    if(unsupported.TryGetSession(new CameraDeviceRef(id),out _))throw new Exception("拒绝前不应打开设备");
}
try{_ = new CameraFrameRequest(options.DeviceId,CameraRequestPurpose.Preview,identity,TimeSpan.FromSeconds(1));throw new Exception("预览伪运行身份未拒绝");}
catch(ArgumentException){}
foreach(var fail in new[]{false,true})
{
    var slow=new SimulatedCameraOptions{DeviceId=fail?"sim-fail":"sim-cancel",Width=2,Height=2,FrameDelay=fail?TimeSpan.Zero:TimeSpan.FromSeconds(5),FailCapture=fail};
    await using var candidate=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(slow)});
    candidate.RegisterDevices(await new SimulatedCameraDiscoveryProvider(slow).DiscoverAsync(CancellationToken.None));
    using var stop=new CancellationTokenSource();if(!fail)stop.CancelAfter(TimeSpan.FromMilliseconds(30));
    try{await candidate.CaptureAsync(new CameraFrameRequest(slow.DeviceId,CameraRequestPurpose.Workflow,identity,TimeSpan.FromSeconds(2)),stop.Token);throw new Exception("故障采集意外成功");}
    catch(TimeoutException) when(fail){} catch(OperationCanceledException) when(!fail){}
    if(candidate.TryGetSession(new CameraDeviceRef(slow.DeviceId),out _))throw new Exception("失败后临时会话未关闭");
}
var previewLease=manager.AcquireCaptureLease(new CameraDeviceRef(options.DeviceId),"关闭中预览");
var closing=manager.DisposeAsync().AsTask();var concurrentClose=manager.DisposeAsync().AsTask();
if(closing.IsCompleted || concurrentClose.IsCompleted)throw new Exception("预览租约未释放时管理器提前关闭");
try{await manager.CaptureAsync(request,CancellationToken.None);throw new Exception("关闭中错误接受新采集");}catch(ObjectDisposedException){}
previewLease.Dispose();await Task.WhenAll(closing,concurrentClose).WaitAsync(TimeSpan.FromSeconds(3));
try{await manager.CaptureAsync(request,CancellationToken.None);throw new Exception("关闭后错误接受新采集");}catch(ObjectDisposedException){}
if(reader.GetRow(0).Length!=4)throw new Exception("管理器关闭后 reader 不可用");reader.Dispose();
var busyOptions=new SimulatedCameraOptions{DeviceId="sim-drain",Width=2,Height=2,FrameDelay=TimeSpan.FromMilliseconds(200)};
await using(var busy=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(busyOptions)}))
{
 busy.RegisterDevices(await new SimulatedCameraDiscoveryProvider(busyOptions).DiscoverAsync(CancellationToken.None));
 var inFlight=busy.CaptureAsync(new CameraFrameRequest(busyOptions.DeviceId,CameraRequestPurpose.Workflow,identity,TimeSpan.FromSeconds(2)),CancellationToken.None).AsTask();
 await Task.Delay(50);var drain=busy.DisposeAsync().AsTask();if(drain.IsCompleted)throw new Exception("在途采集未排空时管理器提前关闭");
 await using var accepted=await inFlight.WaitAsync(TimeSpan.FromSeconds(3));await drain.WaitAsync(TimeSpan.FromSeconds(3));
 if(accepted.Image.CopyPixels().Length!=4)throw new Exception("关闭影响已完成的独立帧");
}
var retryOptions=new SimulatedCameraOptions{DeviceId="sim-retry"};var retryFactory=new FailingCloseFactory(retryOptions);
var retryManager=new CameraRuntimeManager(new[]{retryFactory});
retryManager.RegisterDevices(await new SimulatedCameraDiscoveryProvider(retryOptions).DiscoverAsync(CancellationToken.None));
var retryRef=new CameraDeviceRef(retryOptions.DeviceId);await retryManager.OpenAsync(retryRef,CameraAccessMode.Exclusive);
try{await retryManager.DisposeAsync();throw new Exception("驱动释放失败未报告");}catch(AggregateException error) when(error.Flatten().InnerExceptions.Any(e=>e.Message=="simulated-dispose-failure")){}
if(!retryManager.TryGetSession(retryRef,out _))throw new Exception("释放失败时错误移除会话");
try{await retryManager.OpenAsync(retryRef,CameraAccessMode.Exclusive);throw new Exception("失败后接受新操作");}catch(ObjectDisposedException){}
await retryManager.DisposeAsync();if(retryManager.TryGetSession(retryRef,out _))throw new Exception("重试后会话未释放");
var manualOptions=new SimulatedCameraOptions{DeviceId="sim-manual-retry"};
await using(var manual=new CameraRuntimeManager(new[]{new FailingCloseFactory(manualOptions)}))
{
 manual.RegisterDevices(await new SimulatedCameraDiscoveryProvider(manualOptions).DiscoverAsync(CancellationToken.None));
 var reference=new CameraDeviceRef(manualOptions.DeviceId);var session=await manual.OpenAsync(reference,CameraAccessMode.Exclusive);
 try{await session.DisposeAsync();throw new Exception("会话释放失败未报告");}catch(InvalidOperationException error) when(error.Message=="simulated-dispose-failure"){}
 if(!manual.TryGetSession(reference,out _))throw new Exception("会话释放失败时丢失原会话");
 await session.DisposeAsync();if(manual.TryGetSession(reference,out _))throw new Exception("会话重试未完成释放");
}
Console.WriteLine("模拟相机：帧身份/Gray8/reader、预览互斥和取消/故障清理/关闭后 reader/未知域写前保护通过");

sealed class FailingCloseFactory(SimulatedCameraOptions options):ICameraAdapterFactory
{
 public string AdapterId=>"simulated-failure";
 public bool CanCreate(CameraDescriptor descriptor)=>true;
 public bool SupportsWorkflowFrames(CameraDescriptor descriptor)=>true;
 public ICameraAdapter Create(CameraDescriptor descriptor)=>new FailingCloseAdapter(new SimulatedCameraAdapter(descriptor,options));
}
sealed class FailingCloseAdapter(ICameraAdapter inner):ICameraAdapter
{
 private int _attempt;
 public string AdapterId=>inner.AdapterId;public CameraDescriptor Descriptor=>inner.Descriptor;public CameraCapabilities Capabilities=>inner.Capabilities;public CameraState State=>inner.State;
 public Task ConnectAsync(CameraConnectionOptions options,CancellationToken cancellation)=>inner.ConnectAsync(options,cancellation);
 public Task DisconnectAsync(CancellationToken cancellation)=>inner.DisconnectAsync(cancellation);
 public Task<ICameraFrameLease> CaptureAsync(CameraCaptureRequest request,CancellationToken cancellation)=>inner.CaptureAsync(request,cancellation);
 public Task StartStreamAsync(CameraCaptureRequest request,ICameraFrameSink sink,CancellationToken cancellation)=>inner.StartStreamAsync(request,sink,cancellation);
 public Task StopStreamAsync(CancellationToken cancellation)=>inner.StopStreamAsync(cancellation);
 public Task ExecuteSoftwareTriggerAsync(CancellationToken cancellation)=>inner.ExecuteSoftwareTriggerAsync(cancellation);
 public Task<CameraParameterSchema> GetParameterSchemaAsync(CancellationToken cancellation)=>inner.GetParameterSchemaAsync(cancellation);
 public Task<CameraCaptureConfiguration> ReadCaptureConfigurationAsync(CancellationToken cancellation)=>inner.ReadCaptureConfigurationAsync(cancellation);
 public Task<IReadOnlyDictionary<string,object?>> ReadParametersAsync(IReadOnlyCollection<string> names,CancellationToken cancellation)=>inner.ReadParametersAsync(names,cancellation);
 public Task ApplyParametersAsync(IReadOnlyDictionary<string,object?> values,CancellationToken cancellation)=>inner.ApplyParametersAsync(values,cancellation);
 public Task ExecuteCommandAsync(string name,CancellationToken cancellation)=>inner.ExecuteCommandAsync(name,cancellation);
 public ValueTask DisposeAsync(){if(Interlocked.Increment(ref _attempt)==1)throw new InvalidOperationException("simulated-dispose-failure");return inner.DisposeAsync();}
}