using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OpcUaDataPointInputTests
{
    [Theory]
    [InlineData("通道 1.设备 1.test3")]
    [InlineData("s=通道 1.设备 1.test3")]
    [InlineData("ns=2;s=通道 1.设备 1.test3")]
    public void String_node_id_inputs_are_normalized_once(string input)
    {
        Assert.True(OpcUaDataPointInput.TryParse(2, input, out var point, out var error), error);
        Assert.Equal("通道 1.设备 1.test3", point.TagName);
        Assert.Equal("ns=2;s=通道 1.设备 1.test3", point.NodeId);
    }

    [Fact]
    public void Fully_qualified_node_id_can_represent_a_literal_s_prefix()
    {
        Assert.True(OpcUaDataPointInput.TryParse(2, "ns=2;s=s=literal", out var point, out _));
        Assert.Equal("s=literal", point.TagName);
        Assert.Equal("ns=2;s=s=literal", point.NodeId);
    }

    [Fact]
    public void Pasted_namespace_must_match_the_namespace_box()
    {
        Assert.False(OpcUaDataPointInput.TryParse(2, "ns=3;s=test3", out _, out var error));
        Assert.Contains("不一致", error);
    }
}
