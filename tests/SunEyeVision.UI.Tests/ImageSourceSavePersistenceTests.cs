using System.Collections.Generic;
using SunEyeVision.UI.Shared;

using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 图像源保存端持久化测试。
/// 验证：AppendFiles 改为写 ImageInputSource（真相源）后，AddImages 填充 _images，
/// CapturePolicy() 能产生非空 FilePaths → 保存进 JSON → 重启后能恢复。
/// 覆盖用户"加载图像→保存→重启图像源数量空"的保存端修复。
/// </summary>
public class ImageSourceSavePersistenceTests
{
    [Fact]
    public void RuntimeCursorDoesNotPersistOrInvalidateItsOwnVersionButUserEditsRejectOldPresentation()
    {
        var source = new ImageInputSource("cursor");
        var a = new ImageInfo { FilePath = "a", SourceReference = "assets/a" };
        var b = new ImageInfo { FilePath = "b", SourceReference = "assets/b" };
        source.AddImages(new[] { a, b });
        source.CurrentIndex = 0;
        var writes = 0; source.ConfigurationWriter = _ => writes++;
        var captured = source.EditVersion;
        Assert.True(source.TryPresentRuntimeCursor(captured, b.Id, "assets/b", "b"));
        Assert.Equal(1, source.CurrentIndex);
        Assert.Equal(captured, source.EditVersion);
        Assert.Equal(0, writes);
        source.CurrentIndex = 0;
        Assert.False(source.TryPresentRuntimeCursor(captured, b.Id, "assets/b", "b"));
        Assert.Equal(0, source.CurrentIndex);
        var beforeEdit = source.EditVersion;
        b.IsForRun = true;
        Assert.False(source.TryPresentRuntimeCursor(beforeEdit, b.Id, "assets/b", "b"));
        var beforeReset = source.EditVersion;
        source.LoadFromPolicy(source.CapturePolicy(),new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(null));
        Assert.False(source.TryPresentRuntimeCursor(beforeReset, b.Id, "assets/b", "b"));
    }

    [Fact]
    public void CheckOnlyEditImmediatelySurvivesNodeSerialization()
    {
        var node = new WorkflowNode("checks", "图像源", "图像源", "ImageLoad"){RuntimeConfiguration=ImageSourceTestRuntime.Configuration()};
        var input = node.EnsureInputSource();
        var editGate = new SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager(
            NSubstitute.Substitute.For<SunEyeVision.UI.Services.Interaction.IInputProvider>(),
            new WorkflowEngine(NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Logging.ILogger>()),
            new SunEyeVision.UI.Services.RuntimeInputSnapshotService());
        input.ConfigurationLeaseProvider = editGate.BeginHistoryMutation;
        input.ConfigurationWriter = candidate => ImageSourceTestRuntime.Edit(node,config=>config.UpdateLocalInput(candidate.Configuration));
        var image = new ImageInfo { FilePath = @"C:\samples\a.bmp", SourceReference = "assets/a.bmp" };
        input.AddImage(image);
        image.IsForRun = true;

        var json = System.Text.Json.JsonSerializer.Serialize<WorkflowNodeBase>(node, WorkflowSerializationOptions.Default);
        var restored = System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json, WorkflowSerializationOptions.Default);
        Assert.Equal(new[] { "assets/a.bmp" }, ImageSourceTestRuntime.Read(restored!).ForRunPaths);
        image.IsForRun = false;
        Assert.Empty(ImageSourceTestRuntime.Read(node)!.ForRunPaths);
    }

    [Fact]
    public void RemovedOrResetEntriesCannotWriteBackToTheirOldSource()
    {
        var source = new ImageInputSource("checks");
        var removed = new ImageInfo { FilePath = @"C:\samples\a.bmp", IsForRun = true };
        var replaced = new ImageInfo { FilePath = @"C:\samples\b.bmp", IsForRun = true };
        source.AddImages(new[] { removed, replaced });
        source.CapturePolicy();
        var writes = 0;
        source.ConfigurationWriter = _ => writes++;
        source.RemoveImage(removed);
        Assert.DoesNotContain(removed.FilePath, source.CapturePolicy().ForRunPaths);
        var afterRemove = writes;
        removed.IsForRun = false;
        Assert.Equal(afterRemove, writes);

        var replacement = new ImageInfo { FilePath = @"C:\samples\c.bmp" };
        source.LoadFromPolicy(new ImageSourcePolicy { NodeId="checks",FilePaths=new(){replacement.FilePath},ForRunPaths=new() },new SunEyeVision.Workflow.Persistence.SolutionResourceResolver(null));
        replacement=Assert.Single(source.Images);
        var afterReset = writes;
        replaced.IsForRun = false;
        Assert.Equal(afterReset, writes);
        Assert.Empty(source.CapturePolicy().ForRunPaths);
        replacement.IsForRun = true;
        Assert.Equal(afterReset + 1, writes);
    }

    [Fact]
    public void LocalPreviewChangesPreserveCameraSelectionAndSourceMode()
    {
        var node = new WorkflowNode("node-merge", "图像源", "图像源", "ImageLoad"){RuntimeConfiguration=ImageSourceTestRuntime.Configuration()};
        ImageSourceTestRuntime.Edit(node,config=>{
            config.SourceKind=ImageSourceKind.Camera;config.CameraDeviceId="camera-001";config.CameraTimeoutMs=4200;
            config.OutputFormat=ImageOutputFormat.Rgb24;config.SNInitialValue=25;config.StopAtLastImage=true;
        });
        var input = node.EnsureInputSource();
        var editGate = new SunEyeVision.UI.Services.Workflow.WorkflowExecutionManager(
            NSubstitute.Substitute.For<SunEyeVision.UI.Services.Interaction.IInputProvider>(),
            new WorkflowEngine(NSubstitute.Substitute.For<SunEyeVision.Plugin.SDK.Logging.ILogger>()),
            new SunEyeVision.UI.Services.RuntimeInputSnapshotService());
        input.ConfigurationLeaseProvider = editGate.BeginHistoryMutation;
        input.ConfigurationWriter = candidate => ImageSourceTestRuntime.Edit(node,config=>config.UpdateLocalInput(candidate.Configuration));
        input.AddImage(new ImageInfo { FilePath = @"C:\samples\a.bmp", Name = "a" });
        input.CurrentIndex = 0;

        Assert.Equal(ImageSourceKind.Camera, ImageSourceTestRuntime.Read(node).SourceKind);
        Assert.Equal("camera-001", ImageSourceTestRuntime.Read(node).CameraDeviceId);
        Assert.Equal(4200, ImageSourceTestRuntime.Read(node).CameraTimeoutMs);
        Assert.Equal(ImageOutputFormat.Rgb24, ImageSourceTestRuntime.Read(node).OutputFormat);
        Assert.Equal(25, ImageSourceTestRuntime.Read(node).SNInitialValue);
        Assert.True(ImageSourceTestRuntime.Read(node).StopAtLastImage);
        Assert.Single(ImageSourceTestRuntime.Read(node).FilePaths);
        Assert.Equal(0, ImageSourceTestRuntime.Read(node).CurrentIndex);

        var json = System.Text.Json.JsonSerializer.Serialize<WorkflowNodeBase>(node, WorkflowSerializationOptions.Default);
        var restored = System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json, WorkflowSerializationOptions.Default);
        Assert.Equal(ImageSourceKind.Camera, ImageSourceTestRuntime.Read(restored!).SourceKind);
        Assert.Equal("camera-001", ImageSourceTestRuntime.Read(restored!).CameraDeviceId);
        Assert.Equal(ImageOutputFormat.Rgb24, ImageSourceTestRuntime.Read(restored!).OutputFormat);
        Assert.Equal(25, ImageSourceTestRuntime.Read(restored!).SNInitialValue);
        Assert.True(ImageSourceTestRuntime.Read(restored!).StopAtLastImage);
        Assert.Single(ImageSourceTestRuntime.Read(restored!).FilePaths);
    }

    [Fact]
    public void AddImages_PopulatesTruthSource_CapturePolicy_HasNonEmptyFilePaths()
    {
        // 模拟 正式宿主导入批接纳 走真相源：ActiveInputSource.AddImages
        var source = new ImageInputSource("img-input-save-1");
        source.AddImages(new List<ImageInfo>
        {
            new() { FilePath = @"C:\samples\a.bmp", Name = "a" },
            new() { FilePath = @"C:\samples\b.bmp", Name = "b" },
            new() { FilePath = @"C:\samples\c.bmp", Name = "c" },
        });

        // 真相源 _images 已填充
        Assert.Equal(3, source.Count);

        // 保存端：CapturePolicy 产生非空 FilePaths（这是修复前为空的断点）
        var config = source.CapturePolicy();
        Assert.NotNull(config);
        Assert.Equal(3, config.FilePaths.Count);
        Assert.Equal(@"C:\samples\a.bmp", config.FilePaths[0]);
    }

    [Fact]
    public void AddImages_CapturePolicy_Serializes_And_Deserializes_FilePaths()
    {
        var source = new ImageInputSource("img-input-save-2");
        source.AddImages(new List<ImageInfo>
        {
            new() { FilePath = @"D:\work\1.png", Name = "1" },
            new() { FilePath = @"D:\work\2.png", Name = "2" },
        });

        var config = source.CapturePolicy();
        var node = ImageSourceTestRuntime.Node("node-save-2");
        config.NodeId=node.Id;config.ApplyTo(node);

        var json = System.Text.Json.JsonSerializer.Serialize(node, WorkflowSerializationOptions.Default);
        var back = System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json, WorkflowSerializationOptions.Default);

        Assert.NotNull(ImageSourceTestRuntime.Read(back!));
        Assert.Equal(2, ImageSourceTestRuntime.Read(back!).FilePaths.Count);
        Assert.Equal(@"D:\work\1.png", ImageSourceTestRuntime.Read(back!).FilePaths[0]);
    }
}
