using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationReceiveTargetKindTests
{
    [Theory]
    [InlineData("tcp-client", "None", "")]
    [InlineData("udp", "None", "")]
    [InlineData("modbus", "ModbusAddress", "绑定地址")]
    [InlineData("modbus-rtu", "ModbusAddress", "绑定地址")]
    [InlineData("s7", "S7Memory", "绑定内存地址")]
    [InlineData("cip", "CipAttribute", "绑定属性")]
    [InlineData("opcua", "OpcUaDataPoint", "绑定数据点")]
    public void Target_selector_depends_on_device_addressing_model(string protocolKey,
        string expectedKind, string expectedLabel)
    {
        var device = new Communication("device", CommunicationType.TCP) { ProtocolKey = protocolKey };

        var kind = CommunicationReceiveTargetKinds.For(device);

        Assert.Equal(expectedKind, kind.ToString());
        Assert.Equal(expectedLabel, CommunicationReceiveTargetKinds.Label(kind));
    }

    [Fact]
    public void Missing_device_does_not_require_target()
    {
        Assert.Equal(CommunicationReceiveTargetKind.None, CommunicationReceiveTargetKinds.For(null));
    }
}
