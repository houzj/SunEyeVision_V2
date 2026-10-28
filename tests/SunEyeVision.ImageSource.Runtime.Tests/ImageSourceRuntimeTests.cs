using System.Text.Json;
using NSubstitute;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.ImageSource.Runtime;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.ImageSource.Runtime.Tests;

public sealed class ImageSourceRuntimeTests
{
    private static IToolExecutionContext Context(FrozenOutputResources resources,CancellationToken token=default)
    {
        var context=Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"source",Guid.NewGuid()));
        context.Cancellation.Returns(token);context.Resources.Returns(resources);return context;
    }
    private static InputValues Input(IReadOnlyDictionary<string,object?> values)=>new(ImageSourceTool.Schema.Inputs,values);

    [Fact]
    public void Settings_defaults_and_full_payload_round_trip_only_through_runtime_configuration()
    {
        Assert.True(PluginLoader.LoadPlugins(Path.Combine(SunEyeVision.TestShared.ToolPluginTestLoader.HostDebugRoot,"plugins")));
        var files=new List<string>{"assets/a.png","assets/b.png"};
        var settings=new ImageSourceSettings{FileReferences=files,StartIndex=1,SelectedReferences=new[]{"assets/b.png"},RunMode="selected",
            AutoSwitch=false,StopAtLast=true,SourceKind="camera",CameraDeviceId="device",CameraTimeoutMs=4200,OutputFormat="rgb24",InitialSequence=25};
        var node=new WorkflowNodeBase("source","源","源","ImageLoad"){RuntimeConfiguration=RuntimeToolConfiguration.Create("ImageLoad").WithSettings(settings)};
        files.Clear();
        var json=JsonSerializer.Serialize(node,WorkflowSerializationOptions.Default);
        Assert.DoesNotContain("\"ImageSourceConfig\"",json);Assert.DoesNotContain("\"InputSource\"",json);
        Assert.DoesNotContain("\"OutputCache\"",json);Assert.DoesNotContain("\"LastResult\"",json);
        var restored=JsonSerializer.Deserialize<WorkflowNodeBase>(json,WorkflowSerializationOptions.Default)!;
        var actual=Assert.IsType<ImageSourceSettings>(restored.RuntimeConfiguration!.CaptureSettings());
        Assert.Equal(new[]{"assets/a.png","assets/b.png"},actual.FileReferences);Assert.Equal(1,actual.StartIndex);
        Assert.Equal(new[]{"assets/b.png"},actual.SelectedReferences);Assert.Equal("selected",actual.RunMode);
        Assert.False(actual.AutoSwitch);Assert.True(actual.StopAtLast);Assert.Equal("camera",actual.SourceKind);
        Assert.Equal("device",actual.CameraDeviceId);Assert.Equal(4200,actual.CameraTimeoutMs);Assert.Equal("rgb24",actual.OutputFormat);Assert.Equal(25,actual.InitialSequence);
        var empty=RuntimeToolConfiguration.Create("ImageLoad");
        var reopened=JsonSerializer.Deserialize<RuntimeToolConfiguration>(JsonSerializer.Serialize(empty,WorkflowSerializationOptions.Default),WorkflowSerializationOptions.Default)!;
        var defaults=Assert.IsType<ImageSourceSettings>(reopened.CaptureSettings());
        Assert.Empty(defaults.FileReferences);Assert.Empty(defaults.SelectedReferences);Assert.Equal(-1,defaults.StartIndex);Assert.Empty(reopened.Validate());
    }

    [Fact]
    public void Fields_freeze_input_collections_and_invalid_edit_never_mutates_previous_settings()
    {
        var paths=new List<string>{"a.png"};var original=new ImageSourceSettings{FileReferences=paths,StartIndex=0}.Freeze();
        paths.Clear();Assert.Single(original.FileReferences);
        Assert.ThrowsAny<Exception>(()=>ImageSourceFields.Apply(original,new Dictionary<string,object?>{{ImageSourceFields.CameraTimeout,0L}}));
        Assert.Single(original.FileReferences);Assert.Equal(3000,original.CameraTimeoutMs);
        Assert.Null(ImageSourceTool.Schema.WorkflowImageInputId);
    }

    [Fact]
    public async Task Missing_scheduler_sequence_is_reported_at_execution_not_fabricated_in_settings()
    {
        using var resources=new FrozenOutputResources();await using var session=new ImageSourceTool().CreateSession();
        var settings=new ImageSourceSettings();Assert.Empty(settings.Validate());
        var result=await session.ExecuteAsync(new(settings,Input(new Dictionary<string,object?>()),Context(resources)));
        Assert.False(result.Success);Assert.Null(result.Output);Assert.Contains(result.Diagnostics,d=>d.Code=="IMAGE_SOURCE_SEQUENCE");
    }

    [Fact]
    public async Task Local_file_decode_has_explicit_address_and_fresh_run_frame_identity()
    {
        var directory=Path.Combine(Path.GetTempPath(),"image-source-runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"image.png");var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                2,1,96,96,System.Windows.Media.PixelFormats.Gray8,null,new byte[]{20,90},2)));
            using(var file=File.Create(path))encoder.Save(file);
            using var resources=new FrozenOutputResources();await using var session=new ImageSourceTool().CreateSession();
            var context=Context(resources);var input=Input(new Dictionary<string,object?>{{ImageSourceTool.LocalAddress,path},{ImageSourceTool.Sequence,11L}});
            var result=await session.ExecuteAsync(new(new ImageSourceSettings(),input,context));
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            using var output=result.Output!;var values=output.ToValues();var image=Assert.IsAssignableFrom<IReadOnlyImage>(values[ImageSourceOutput.Image]);
            Assert.Equal(new byte[]{20,90},image.CopyPixels());Assert.Equal(context.Identity,image.Frame.ProducerRun);
            Assert.Equal(path,values[ImageSourceOutput.FileReference]);Assert.Equal(11L,values[ImageSourceOutput.Sequence]);
            using var domain=image.EffectiveDomain.AcquireReader();Assert.Equal(new[]{new RegionRun(0,0,1)},domain.ReadRuns(0,4096).ToArray());
        }
        finally{Directory.Delete(directory,true);}
    }

    [Theory]
    [InlineData("local",false,false)]
    [InlineData("camera",false,false)]
    [InlineData("local",true,true)]
    public async Task Missing_or_wrong_input_kind_never_falls_back_to_a_default_image(string kind,bool file,bool camera)
    {
        using var resources=new FrozenOutputResources();await using var session=new ImageSourceTool().CreateSession();
        var values=new Dictionary<string,object?>{{ImageSourceTool.Sequence,1L}};
        if(file)values[ImageSourceTool.LocalAddress]=Path.GetFullPath("missing.png");
        if(camera)values[ImageSourceTool.CameraImage]=resources.CreateGray8(1,1,new byte[]{20});
        var outcome=await session.ExecuteAsync(new(new ImageSourceSettings{SourceKind=kind},Input(values),Context(resources)));
        Assert.False(outcome.Success);Assert.Null(outcome.Output);Assert.NotEmpty(outcome.Diagnostics);
    }

    [Fact]
    public async Task Camera_identity_is_checked_before_copying_and_empty_device_remains_editable()
    {
        using var resources=new FrozenOutputResources();var image=resources.CreateGray8(2,1,new byte[]{10,30});
        var input=Input(new Dictionary<string,object?>{{ImageSourceTool.Sequence,1L},{ImageSourceTool.CameraImage,image},{ImageSourceTool.DeviceId,"wrong"}});
        await using var session=new ImageSourceTool().CreateSession();
        foreach(var device in new[]{"","expected"})
        {
            var settings=new ImageSourceSettings{SourceKind="camera",CameraDeviceId=device};Assert.Empty(settings.Validate());
            var result=await session.ExecuteAsync(new(settings,input,Context(resources)));
            Assert.False(result.Success);Assert.Contains(result.Diagnostics,d=>d.Code.StartsWith("IMAGE_SOURCE_CAMERA",StringComparison.Ordinal));
        }
        Assert.Equal(new byte[]{10,30},image.CopyPixels());
    }

    [Fact]
    public async Task Camera_copy_publishes_physical_frame_actual_domain_gray_alias_and_independent_reader_lease()
    {
        using var resources=new FrozenOutputResources();
        var sourceFrame=new ImageFrameAddress(Guid.NewGuid(),"camera:test");
        var domain=resources.CreateRegion(new CoordinateFrame("pixels",CoordinateDomain.Image,CoordinateUnit.Pixel),sourceFrame,new[]{new RegionRun(0,1,1)});
        var inputImage=resources.CreateImage(new ImageLayout(2,1,2,PixelLayout.Gray8),sourceFrame,domain,new byte[]{10,30});
        var context=Context(resources);
        var inputs=Input(new Dictionary<string,object?>{{ImageSourceTool.Sequence,7L},{ImageSourceTool.CameraImage,inputImage},
            {ImageSourceTool.DeviceId,"device"},{ImageSourceTool.DeviceFrameId,"frame-7"},{ImageSourceTool.DeviceTimestamp,"time-7"}});
        await using var session=new ImageSourceTool().CreateSession();
        var result=await session.ExecuteAsync(new(new ImageSourceSettings{SourceKind="camera",CameraDeviceId="device"},inputs,context));
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
        var output=result.Output!;var values=output.ToValues();var image=Assert.IsAssignableFrom<IReadOnlyImage>(values[ImageSourceOutput.Image]);
        Assert.Same(image,values[ImageSourceOutput.GrayImage]);Assert.NotEqual(sourceFrame,image.Frame);
        Assert.Equal(context.Identity,image.Frame.ProducerRun);Assert.Equal(ImageSourceOutput.Image,image.Frame.SourcePortId);
        Assert.Equal(image.Frame,image.EffectiveDomain.SourceFrame);Assert.Equal(7L,values[ImageSourceOutput.Sequence]);
        Assert.Equal("frame-7",values[ImageSourceOutput.FrameId]);Assert.Equal("time-7",values[ImageSourceOutput.Timestamp]);
        using var pixels=image.AcquirePixels();using var runs=pixels.AcquireDomainReader();
        Assert.Equal(new[]{new RegionRun(0,1,1)},runs.ReadRuns(0,4096).ToArray());
        output.Dispose();output.Dispose();Assert.Throws<ObjectDisposedException>(()=>output.ToValues());
        Assert.Equal(new byte[]{10,30},pixels.GetRow(0).ToArray());Assert.Equal(image.Frame,runs.SourceFrame);
        Assert.Equal(new byte[]{10,30},inputImage.CopyPixels());
    }

    [Fact]
    public async Task Cancellation_after_output_scope_creation_releases_partial_capture_without_touching_input()
    {
        using var inputsOwner=new FrozenOutputResources();using var partial=new FrozenOutputResources();
        using var cancellation=new CancellationTokenSource();
        var resources=Substitute.For<IOutputResources>();
        resources.CreateScope().Returns(_=>{cancellation.Cancel();return partial;});
        var context=Substitute.For<IToolExecutionContext>();
        context.Identity.Returns(new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"source",Guid.NewGuid()));
        context.Resources.Returns(resources);context.Cancellation.Returns(cancellation.Token);
        var image=inputsOwner.CreateGray8(1,1,new byte[]{80});
        await using var session=new ImageSourceTool().CreateSession();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>
        {
            await session.ExecuteAsync(new(new ImageSourceSettings{SourceKind="camera",CameraDeviceId="device"},
                Input(new Dictionary<string,object?>{{ImageSourceTool.Sequence,1L},{ImageSourceTool.CameraImage,image},{ImageSourceTool.DeviceId,"device"}}),context));
        });
        Assert.Throws<ObjectDisposedException>(()=>partial.CreateScope());
        Assert.Equal(new byte[]{80},image.CopyPixels());
    }

    [Fact]
    public async Task Already_cancelled_invocation_publishes_no_resources_and_session_can_be_disposed_twice()
    {
        using var resources=new FrozenOutputResources();using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var session=new ImageSourceTool().CreateSession();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await session.ExecuteAsync(new(new ImageSourceSettings(),
            Input(new Dictionary<string,object?>{{ImageSourceTool.Sequence,1L}}),Context(resources,cancellation.Token))));
        await session.DisposeAsync();await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async()=>await session.ExecuteAsync(new(new ImageSourceSettings(),
            Input(new Dictionary<string,object?>{{ImageSourceTool.Sequence,1L}}),Context(resources))));
    }
}

internal static class ImageSourceRuntimeTestModule
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void LoadActualComponent()
        =>SunEyeVision.TestShared.ToolPluginTestLoader.EnsureComponentLoaded("SunEyeVision.Tool.ImageSource.Runtime");
}
