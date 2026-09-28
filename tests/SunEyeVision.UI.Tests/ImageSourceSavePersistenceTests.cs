using System.Collections.Generic;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Shell.Controls.Panels;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.UI.Tests;

/// <summary>
/// 图像源保存端持久化测试。
/// 验证：AppendFiles 改为写 ImageInputSource（真相源）后，AddImages 填充 _images，
/// ToConfig() 能产生非空 FilePaths → 保存进 JSON → 重启后能恢复。
/// 覆盖用户"加载图像→保存→重启图像源数量空"的保存端修复。
/// </summary>
public class ImageSourceSavePersistenceTests
{
    [Fact]
    public void LocalPreviewChangesPreserveCameraSelectionAndSourceMode()
    {
        var node = new WorkflowNode("node-merge", "图像源", "图像源", "ImageLoad")
        {
            ImageSourceConfig = new ImageSourceConfig
            {
                NodeId = "node-merge",
                SourceKind = ImageSourceKind.Camera,
                CameraDeviceId = "camera-001",
                CameraTimeoutMs = 4200
            }
        };
        var input = node.EnsureInputSource();
        input.AddImage(new ImageInfo { FilePath = @"C:\samples\a.bmp", Name = "a" });
        input.CurrentIndex = 0;

        Assert.Equal(ImageSourceKind.Camera, node.ImageSourceConfig.SourceKind);
        Assert.Equal("camera-001", node.ImageSourceConfig.CameraDeviceId);
        Assert.Equal(4200, node.ImageSourceConfig.CameraTimeoutMs);
        Assert.Single(node.ImageSourceConfig.FilePaths);
        Assert.Equal(0, node.ImageSourceConfig.CurrentIndex);

        var json = System.Text.Json.JsonSerializer.Serialize<WorkflowNodeBase>(node, WorkflowSerializationOptions.Default);
        var restored = System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json, WorkflowSerializationOptions.Default);
        Assert.Equal(ImageSourceKind.Camera, restored!.ImageSourceConfig!.SourceKind);
        Assert.Equal("camera-001", restored.ImageSourceConfig.CameraDeviceId);
        Assert.Single(restored.ImageSourceConfig.FilePaths);
    }

    [Fact]
    public void AddImages_PopulatesTruthSource_ToConfig_HasNonEmptyFilePaths()
    {
        // 模拟 ImagePreviewControl.AppendFiles 走真相源：ActiveInputSource.AddImages
        var source = new ImageInputSource("img-input-save-1");
        source.AddImages(new List<ImageInfo>
        {
            new() { FilePath = @"C:\samples\a.bmp", Name = "a" },
            new() { FilePath = @"C:\samples\b.bmp", Name = "b" },
            new() { FilePath = @"C:\samples\c.bmp", Name = "c" },
        });

        // 真相源 _images 已填充
        Assert.Equal(3, source.Count);

        // 保存端：ToConfig 产生非空 FilePaths（这是修复前为空的断点）
        var config = source.ToConfig();
        Assert.NotNull(config);
        Assert.Equal(3, config.FilePaths.Count);
        Assert.Equal(@"C:\samples\a.bmp", config.FilePaths[0]);
    }

    [Fact]
    public void AddImages_ToConfig_Serializes_And_Deserializes_FilePaths()
    {
        var source = new ImageInputSource("img-input-save-2");
        source.AddImages(new List<ImageInfo>
        {
            new() { FilePath = @"D:\work\1.png", Name = "1" },
            new() { FilePath = @"D:\work\2.png", Name = "2" },
        });

        var config = source.ToConfig();
        var node = new WorkflowNodeBase("node-save-2", "载入", "载入", "图像载入")
        {
            ImageSourceConfig = config,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(node, WorkflowSerializationOptions.Default);
        var back = System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json, WorkflowSerializationOptions.Default);

        Assert.NotNull(back!.ImageSourceConfig);
        Assert.Equal(2, back.ImageSourceConfig!.FilePaths.Count);
        Assert.Equal(@"D:\work\1.png", back.ImageSourceConfig.FilePaths[0]);
    }
}
