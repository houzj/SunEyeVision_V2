using System.Collections.Generic;
using System.Text.Json;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 图像源持久化（ImageSourceConfig）序列化契约测试。
/// 守卫"保存方案 → JSON 含 ImageSourceConfig 字段 → 重开恢复"链路，防止回归导致缩略图丢失。
/// </summary>
public class ImageSourceConfigSerializationTests
{
    private static JsonSerializerOptions Options => WorkflowSerializationOptions.Default;

    [Fact]
    public void ImageSourceConfig_RoundTrip_PreservesAllFields()
    {
        // 构造完整配置（覆盖全部 6 个字段）
        var config = new ImageSourceConfig
        {
            NodeId = "node-123",
            FilePaths = new List<string> { @"C:\img\a.jpg", @"C:\img\b.png", @"C:\img\c.bmp" },
            CurrentIndex = 1,
            AutoSwitchEnabled = false,
            RunMode = 1,
            ForRunPaths = new List<string> { @"C:\img\b.png" },
        };

        var json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<ImageSourceConfig>(json, Options);

        Assert.NotNull(restored);
        Assert.Equal("node-123", restored!.NodeId);
        Assert.Equal(3, restored.FilePaths.Count);
        Assert.Equal(@"C:\img\a.jpg", restored.FilePaths[0]);
        Assert.Equal(@"C:\img\b.png", restored.FilePaths[1]);
        Assert.Equal(@"C:\img\c.bmp", restored.FilePaths[2]);
        Assert.Equal(1, restored.CurrentIndex);
        Assert.False(restored.AutoSwitchEnabled);
        Assert.Equal(1, restored.RunMode);
        Assert.Single(restored.ForRunPaths);
        Assert.Equal(@"C:\img\b.png", restored.ForRunPaths[0]);
    }

    [Fact]
    public void ImageSourceConfig_RoundTrip_EmptyDefaults()
    {
        // 空配置默认值往返：FilePaths/ForRunPaths 应为空集合而非 null
        var config = new ImageSourceConfig();

        var json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<ImageSourceConfig>(json, Options);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.FilePaths);
        Assert.Empty(restored.FilePaths);
        Assert.NotNull(restored.ForRunPaths);
        Assert.Empty(restored.ForRunPaths);
        Assert.Equal(-1, restored.CurrentIndex);
        Assert.True(restored.AutoSwitchEnabled);
    }

    [Fact]
    public void WorkflowNodeBase_ImageSourceConfig_SerializedIntoJson()
    {
        // 保存侧契约：ImageSourceConfig 必须出现在节点 JSON 中（问题1 的根因守卫）
        var node = new WorkflowNodeBase("node-1", "图像载入1", "图像载入", "图像载入")
        {
            ImageSourceConfig = new ImageSourceConfig
            {
                NodeId = "node-1",
                FilePaths = new List<string> { @"C:\img\x.jpg" },
                CurrentIndex = 0,
            },
        };

        var json = JsonSerializer.Serialize(node, Options);

        // ImageSourceConfig 字段必须存在于 JSON（路径反斜杠会被 JSON 转义成 \\，此处只断言字段键存在，
        // 路径往返正确性由 RestoredOnDeserialize 测试验证）
        Assert.Contains("\"ImageSourceConfig\"", json);
        Assert.Contains("\"FilePaths\"", json);
    }

    [Fact]
    public void WorkflowNodeBase_ImageSourceConfig_RestoredOnDeserialize()
    {
        // 加载侧契约：节点 JSON 中的 ImageSourceConfig 反序列化后应恢复
        var node = new WorkflowNodeBase("node-2", "图像载入2", "图像载入", "图像载入")
        {
            ImageSourceConfig = new ImageSourceConfig
            {
                NodeId = "node-2",
                FilePaths = new List<string> { @"C:\img\y.png", @"C:\img\z.png" },
                CurrentIndex = 1,
                AutoSwitchEnabled = false,
                RunMode = 1,
                ForRunPaths = new List<string> { @"C:\img\z.png" },
            },
        };

        var json = JsonSerializer.Serialize(node, Options);
        var restored = JsonSerializer.Deserialize<WorkflowNodeBase>(json, Options);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.ImageSourceConfig);
        Assert.Equal("node-2", restored.ImageSourceConfig!.NodeId);
        Assert.Equal(2, restored.ImageSourceConfig.FilePaths.Count);
        Assert.Equal(@"C:\img\z.png", restored.ImageSourceConfig.FilePaths[1]);
        Assert.Equal(1, restored.ImageSourceConfig.CurrentIndex);
        Assert.Single(restored.ImageSourceConfig.ForRunPaths);
    }

    [Fact]
    public void WorkflowNodeBase_RuntimeObjects_NotSerializedIntoJson()
    {
        // 防御契约：运行时对象（InputSource/OutputCache/LastResult）不得泄漏进 JSON，
        // 否则污染序列化甚至抛异常。基类属性均已 [JsonIgnore]。
        var node = new WorkflowNodeBase("node-3", "图像载入3", "图像载入", "图像载入")
        {
            ImageSourceConfig = new ImageSourceConfig { NodeId = "node-3" },
        };

        var json = JsonSerializer.Serialize(node, Options);

        Assert.DoesNotContain("\"InputSource\"", json);
        Assert.DoesNotContain("\"OutputCache\"", json);
        Assert.DoesNotContain("\"LastResult\"", json);
    }
}
