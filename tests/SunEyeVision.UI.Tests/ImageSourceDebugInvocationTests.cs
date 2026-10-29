using System.IO;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NSubstitute;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>Actual source Runtime through formal/debug host entry points; controlled camera service is not hardware acceptance.</summary>
public sealed class ImageSourceDebugInvocationTests
{
    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath {get;}
        public WorkflowEngine Engine {get;}=new(Substitute.For<ILogger>());
        public SunEyeVision.Workflow.Workflow Flow {get;}
        public WorkflowNodeBase Node {get;}
        public Solution Document {get;}
        public NodeConfigurationEditHost Host {get;}
        public Fixture()
        {
            var release=typeof(ImageSourceDebugInvocationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(item=>item.Key=="HostDebugRoot").Value!;
            DirectoryPath=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")??Path.Combine(release,"test-data"),"source-debug-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            Flow=Engine.CreateWorkflow("debug-"+Guid.NewGuid().ToString("N"),"图像源调试");
            Node=ImageSourceTestRuntime.Node();Flow.Nodes.Add(Node);
            ImageSourceTestRuntime.Edit(Node,p=>{p.FilePaths=new(){"image.png"};p.CurrentIndex=0;p.SNInitialValue=7;});
            Document=new Solution{Workflows=new(){Flow},ResourceBaseDirectory=DirectoryPath};
            Host=new(Document);
        }
        public string WriteImage(string directory,byte pixel)
        {
            Directory.CreateDirectory(directory);var path=Path.Combine(directory,"image.png");
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,1,96,96,PixelFormats.Gray8,null,new[]{pixel,pixel},2)));
            using(var output=File.Create(path))encoder.Save(output);return path;
        }
        public RuntimeDebugInvocation Capture(WorkflowNodeBase? draft=null,CancellationToken cancellation=default,
            ICameraCaptureService? camera=null,IVisionImage? defaultImage=null,IResourcePathResolver? resolver=null)
            =>RuntimeDebugInvocation.Capture(Flow,draft??Host.BeginEdit(Flow.Id,Node.Id).Node,new ToolExecutionOwner(),Host.Capture().Version,
                _=>throw new InvalidOperationException("图像源不得读取上游发布作为默认输入。"),cancellation,defaultImage,
                resourcePaths:resolver??new SolutionResourceResolver(Document.ResourceBaseDirectory),cameraService:camera);
        public void Edit(Action<ImageSourcePolicy> change)
        {var edit=Host.BeginEdit(Flow.Id,Node.Id);ImageSourceTestRuntime.Edit(edit.Node,change);Host.Commit(edit);}
        public void Camera(string device="camera",int timeout=1234)
            =>Edit(p=>{p.SourceKind=ImageSourceKind.Camera;p.CameraDeviceId=device;p.CameraTimeoutMs=timeout;});
        public void Dispose()=>Directory.Delete(DirectoryPath,true);
    }

    private static IReadOnlyImage Image(DeclaredOutputReader reader)=>reader.Values!.Get<IReadOnlyImage>("output.image");

    [Fact]
    public async Task Local_debug_freezes_private_settings_base_and_sequence_without_publishing_formal_state()
    {
        using var f=new Fixture();var a=Path.Combine(f.DirectoryPath,"a");var b=Path.Combine(f.DirectoryPath,"b");
        var expected=f.WriteImage(a,20);f.WriteImage(b,90);f.Document.ResourceBaseDirectory=a;
        var draft=f.Host.BeginEdit(f.Flow.Id,f.Node.Id).Node;
        var cache=new object();var last=new object();f.Node.OutputCache=cache;f.Node.LastResult=last;
        using var invocation=f.Capture(draft);
        ImageSourceTestRuntime.Edit(draft,p=>p.SNInitialValue=88);
        var edit=f.Host.BeginEdit(f.Flow.Id,f.Node.Id);ImageSourceTestRuntime.Edit(edit.Node,p=>p.SNInitialValue=99);f.Host.Commit(edit);
        f.Document.ResourceBaseDirectory=b;var publication=f.Host.Capture();
        using var result=await invocation.ExecuteAsync();
        using var reader=result.AcquireReader();
        try
        {
            Assert.True(result.IsSuccess,result.ErrorMessage);Assert.Equal(expected,reader.Values!.Get<string>("output.file"));
            Assert.Equal(7L,reader.Values.Get<long>("output.sequence"));Assert.Equal(new byte[]{20,20},Image(reader).CopyPixels());
            Assert.Equal(invocation.Identity.ToInvocationIdentity(f.Node.Id),Image(reader).Frame.ProducerRun);
            Assert.Empty(invocation.SourceIdentities);Assert.Same(publication,f.Host.Capture());
            Assert.Equal(99,ImageSourceTestRuntime.Read(f.Node).SNInitialValue);Assert.Equal(88,ImageSourceTestRuntime.Read(draft).SNInitialValue);
            Assert.Same(cache,f.Node.OutputCache);Assert.Same(last,f.Node.LastResult);
        }
        finally{reader.Dispose();}
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("excluded")]
    [InlineData("base")]
    public void Invalid_local_debug_never_uses_a_default_or_upstream_image(string failure)
    {
        using var f=new Fixture();using var fallback=HalImage.CreateFromBytes(new byte[]{90},1,1,1);
        if(failure=="empty")f.Edit(p=>{p.FilePaths.Clear();p.CurrentIndex=-1;});
        if(failure=="excluded")f.Edit(p=>{p.RunMode=1;p.ForRunPaths.Clear();});
        if(failure=="base")f.Document.ResourceBaseDirectory=null;
        Assert.ThrowsAny<Exception>(()=>f.Capture(defaultImage:fallback));
        Assert.False(fallback.IsEmpty);
    }

    [Fact]
    public async Task Direct_formal_source_inputs_and_debug_share_pixels_domain_but_keep_distinct_run_and_frame_identities()
    {
        using var f=new Fixture();var path=f.WriteImage(f.DirectoryPath,40);
        var executor=new WorkflowExecutionEngine(f.Engine,Substitute.For<IPluginManager>(),Substitute.For<ILogger>());
        var sourceInputs=new Dictionary<string,ImageSourceRunInput>{{f.Node.Id,new(path,7)}};
        var request=new WorkflowRunRequest(f.Flow.Id,sourceInputs:sourceInputs);
        sourceInputs[f.Node.Id]=new(path+".missing",99); // Request must retain its admitted immutable mapping.
        using var run=await executor.ExecuteWorkflowAsync(request).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(run.Success,string.Join(";",run.Errors));
        var formal=Assert.IsType<DeclaredOutputSnapshot>(run.NodeResults[f.Node.Id].Result);
        using var formalReader=formal.AcquireReader();
        using var debug=f.Capture();using var preview=await debug.ExecuteAsync();using var previewReader=preview.AcquireReader();
        try
        {
            Assert.True(preview.IsSuccess,preview.ErrorMessage);var a=Image(formalReader);var b=Image(previewReader);
            Assert.Equal(a.CopyPixels(),b.CopyPixels());Assert.Equal(7L,formalReader.Values!.Get<long>("output.sequence"));
            Assert.Equal(7L,previewReader.Values!.Get<long>("output.sequence"));Assert.NotEqual(a.Frame,b.Frame);
            Assert.NotEqual(a.Frame.ProducerRun,b.Frame.ProducerRun);Assert.Equal(debug.Identity.ToInvocationIdentity(f.Node.Id),b.Frame.ProducerRun);
            using var da=a.EffectiveDomain.AcquireReader();using var db=b.EffectiveDomain.AcquireReader();
            Assert.Equal(da.ReadRuns(0,4096).ToArray(),db.ReadRuns(0,4096).ToArray());
        }
        finally{previewReader.Dispose();}
    }

    [Fact]
    public async Task Direct_formal_execution_rejects_missing_scheduler_input_and_persistent_constant_bypass()
    {
        using var f=new Fixture();var path=f.WriteImage(f.DirectoryPath,50);
        var executor=new WorkflowExecutionEngine(f.Engine,Substitute.For<IPluginManager>(),Substitute.For<ILogger>());
        using var fallback=HalImage.CreateFromBytes(new byte[]{90},1,1,1);
        using(var missing=await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(f.Flow.Id,fallback)))
            Assert.False(missing.Success);
        var overrides=new Dictionary<string,IReadOnlyDictionary<string,object?>>{{f.Node.Id,new Dictionary<string,object?>{{"input.local-address",path},{"input.sequence",7L}}}};
        var rejected=await Assert.ThrowsAsync<ArgumentException>(async()=>
            await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(f.Flow.Id,fallback,nodeParameterOverrides:overrides)));
        Assert.Contains("Unknown Runtime setting override: input.local-address",rejected.Message);
        Assert.Equal(7,ImageSourceTestRuntime.Read(f.Node).SNInitialValue);
        Assert.Null(f.Node.LastResult);Assert.Null(f.Node.OutputCache);
        // Rejection must drain the same engine root slot; only an explicit frozen scheduler input may run.
        using var admitted=await executor.ExecuteWorkflowAsync(new WorkflowRunRequest(f.Flow.Id,sourceInputs:
            new Dictionary<string,ImageSourceRunInput>{{f.Node.Id,new(path,7)}}));
        Assert.True(admitted.Success,string.Join(";",admitted.Errors));
        var output=Assert.IsType<DeclaredOutputSnapshot>(admitted.NodeResults[f.Node.Id].Result);
        using var reader=output.AcquireReader();
        Assert.Equal(7L,reader.Values!.Get<long>("output.sequence"));
        Assert.Equal(new byte[]{50,50},Image(reader).CopyPixels());
    }

    private sealed class TrackedFrame : IDisposable
    {
        private readonly HalImage? _native;
        public int ImageDisposals {get;private set;}
        public CameraFrame Frame {get;}
        public TrackedFrame(string device="camera",bool typed=true)
        {
            IVisionImage image;
            if(typed)
            {
                _native=HalImage.CreateFromBytes(new byte[]{30,80},2,1,1);
                image=Substitute.For<IVisionImage,ITypedImageSnapshotSource>();
                ((ITypedImageSnapshotSource)image).CaptureSnapshot(Arg.Any<ImageFrameAddress>()).Returns(call=>
                    ((ITypedImageSnapshotSource)_native).CaptureSnapshot(call.Arg<ImageFrameAddress>()));
            }
            else image=Substitute.For<IVisionImage>();
            image.When(value=>value.Dispose()).Do(_=>{ImageDisposals++;_native?.Dispose();});
            Frame=new(new(device),42,DateTimeOffset.Parse("2026-10-28T01:02:03+00:00"),image);
        }
        public void Dispose()=>Frame.Dispose();
    }

    [Fact]
    public async Task Camera_debug_uses_frozen_device_timeout_metadata_and_releases_acquired_frame_before_returning()
    {
        using var f=new Fixture();f.Camera();using var frame=new TrackedFrame();
        var service=Substitute.For<ICameraCaptureService>();CameraCaptureRequest? captured=null;
        service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {captured=call.Arg<CameraCaptureRequest>();return Task.FromResult(frame.Frame);});
        using var invocation=f.Capture(camera:service);
        ImageSourceTestRuntime.Edit(f.Node,p=>{p.CameraDeviceId="changed";p.CameraTimeoutMs=9999;p.SNInitialValue=99;});
        using var result=await invocation.ExecuteAsync();using var reader=result.AcquireReader();
        try
        {
            Assert.True(result.IsSuccess,result.ErrorMessage);Assert.Equal("camera",captured!.CameraRef.Value);
            Assert.Equal(TimeSpan.FromMilliseconds(1234),captured.Timeout);Assert.Equal(CameraTriggerMode.Continuous,captured.TriggerMode);
            Assert.Equal(invocation.Identity.RequestId.ToString("N"),captured.DiagnosticCorrelationId);
            Assert.True(frame.Frame.IsDisposed);Assert.Equal(1,frame.ImageDisposals);
            Assert.Equal("camera",reader.Values!.Get<string>("output.device"));Assert.Equal("42",reader.Values.Get<string>("output.device-frame-id"));
            Assert.Equal(frame.Frame.Timestamp.ToString("O",System.Globalization.CultureInfo.InvariantCulture),reader.Values.Get<string>("output.device-timestamp"));
            Assert.Equal(7L,reader.Values.Get<long>("output.sequence"));Assert.Equal(new byte[]{30,80},Image(reader).CopyPixels());
        }
        finally{reader.Dispose();}
        Assert.Equal(1,frame.ImageDisposals);
    }

    [Theory]
    [InlineData("wrong-device")]
    [InlineData("untyped")]
    [InlineData("missing-device")]
    [InlineData("service-failure")]
    public async Task Camera_debug_rejects_invalid_capture_without_success_publication_or_resource_leaks(string failure)
    {
        using var f=new Fixture();f.Camera(failure=="missing-device"?"":"camera");
        using var frame=new TrackedFrame(failure=="wrong-device"?"other":"camera",failure!="untyped");
        var service=Substitute.For<ICameraCaptureService>();var captures=0;
        service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(_=>
        {captures++;return failure=="service-failure"?Task.FromException<CameraFrame>(new IOException("采集失败")):Task.FromResult(frame.Frame);});
        using var invocation=f.Capture(camera:service);
        await Assert.ThrowsAnyAsync<Exception>(async()=>{using var unexpected=await invocation.ExecuteAsync();});
        Assert.Equal(failure=="missing-device"?0:1,captures);
        Assert.Equal(failure is "wrong-device" or "untyped"?1:0,frame.ImageDisposals);
        Assert.Null(f.Node.LastResult);Assert.Null(f.Node.OutputCache);
    }

    [Fact]
    public void Camera_cancel_before_debug_capture_does_not_admit_or_call_the_service()
    {
        using var f=new Fixture();f.Camera();using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var service=Substitute.For<ICameraCaptureService>();
        Assert.ThrowsAny<OperationCanceledException>(()=>f.Capture(cancellation:cancellation.Token,camera:service));
        Assert.Empty(service.ReceivedCalls());
        Assert.Null(f.Node.LastResult);Assert.Null(f.Node.OutputCache);
    }

    [Fact]
    public async Task Direct_formal_borrowed_camera_frame_remains_host_owned_after_run_disposal()
    {
        using var f=new Fixture();f.Camera();using var frame=new TrackedFrame();
        var executor=new WorkflowExecutionEngine(f.Engine,Substitute.For<IPluginManager>(),Substitute.For<ILogger>());
        var service=Substitute.For<ICameraCaptureService>();
        var request=new WorkflowRunRequest(f.Flow.Id,cameraCaptureService:service,
            cameraFrames:new Dictionary<string,CameraFrame>{{f.Node.Id,frame.Frame}},
            sourceInputs:new Dictionary<string,ImageSourceRunInput>{{f.Node.Id,new(null,7)}});
        using(var run=await executor.ExecuteWorkflowAsync(request).WaitAsync(TimeSpan.FromSeconds(20)))
        {
            Assert.True(run.Success,string.Join(";",run.Errors));
            var snapshot=Assert.IsType<DeclaredOutputSnapshot>(run.NodeResults[f.Node.Id].Result);
            using var reader=snapshot.AcquireReader();
            Assert.Equal(new byte[]{30,80},Image(reader).CopyPixels());
            Assert.False(frame.Frame.IsDisposed);Assert.Equal(0,frame.ImageDisposals);
        }
        Assert.False(frame.Frame.IsDisposed);Assert.Equal(0,frame.ImageDisposals);
        Assert.Empty(service.ReceivedCalls());
        frame.Dispose();Assert.Equal(1,frame.ImageDisposals);
    }

    [Fact]
    public async Task Camera_debug_cancellation_disposes_late_frame_and_inflight_close_waits_for_drain()
    {
        using var f=new Fixture();f.Camera();using var frame=new TrackedFrame();using var cancellation=new CancellationTokenSource();
        var pending=new TaskCompletionSource<CameraFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service=Substitute.For<ICameraCaptureService>();
        service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(_=>{entered.SetResult();return pending.Task;});
        using var invocation=f.Capture(cancellation:cancellation.Token,camera:service);
        var task=invocation.ExecuteAsync();await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(invocation.Dispose);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>{await invocation.ExecuteAsync();});
        cancellation.Cancel();pending.SetResult(frame.Frame);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{await task;});
        Assert.True(frame.Frame.IsDisposed);Assert.Equal(1,frame.ImageDisposals);Assert.Equal(0,invocation.Identity.Owner.ActiveRuns);
        invocation.Dispose();Assert.Null(f.Node.LastResult);Assert.Null(f.Node.OutputCache);
    }

    [Fact]
    public async Task Debug_snapshot_is_single_use_and_retained_output_readers_survive_result_close()
    {
        using var f=new Fixture();f.WriteImage(f.DirectoryPath,60);
        var abandoned=f.Capture();abandoned.Dispose();abandoned.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async()=>{await abandoned.ExecuteAsync();});
        using var invocation=f.Capture();using var result=await invocation.ExecuteAsync();
        using var reader=result.AcquireReader();var image=Image(reader);var publishedFrame=image.Frame;
        using var pixels=image.AcquirePixels();using var domain=pixels.AcquireDomainReader();
        var expectedRuns=domain.ReadRuns(0,4096).ToArray();
        reader.Dispose();result.Dispose();Assert.Throws<ObjectDisposedException>(()=>image.Frame);
        Assert.Equal(new byte[]{60,60},pixels.GetRow(0).ToArray());
        Assert.Equal(publishedFrame,pixels.Frame);Assert.Equal(publishedFrame,domain.SourceFrame);
        pixels.Dispose();Assert.Throws<ObjectDisposedException>(()=>pixels.GetRow(0).ToArray());
        Assert.Equal(expectedRuns,domain.ReadRuns(0,4096).ToArray());
        domain.Dispose();Assert.Throws<ObjectDisposedException>(()=>domain.SourceFrame);
        await Assert.ThrowsAnyAsync<Exception>(async()=>{await invocation.ExecuteAsync();});
        invocation.Dispose();Assert.Equal(0,invocation.Identity.Owner.ActiveRuns);Assert.Null(f.Node.LastResult);Assert.Null(f.Node.OutputCache);
    }
}
