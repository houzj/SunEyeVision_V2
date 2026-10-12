using System.Globalization;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationPayloadDisplayTests
{
    [Fact]
    public void Variable_displays_exact_hex_without_changing_payload_type()
    {
        var data = new BinaryPayload(new byte[] { 0, 92 });
        var variable = new GlobalVariable { Type = "Binary", Value = new[] { data } };
        var display = new GlobalVariableValueTextConverter().Convert(variable.Value!, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("005C", display);
        Assert.IsType<BinaryPayload[]>(variable.Value);
        Assert.True(variable.IsComplexValue); // Raw bytes cannot be rewritten through a scalar text editor.
    }

    [Fact]
    public void Multiple_payloads_have_visible_boundaries()
    {
        var values = new[] { new BinaryPayload(new byte[] { 1 }), new BinaryPayload(new byte[] { 2, 3 }) };
        Assert.Equal("01 | 0203", new GlobalVariableValueTextConverter().Convert(values, typeof(string), null!, CultureInfo.InvariantCulture));
    }
}
