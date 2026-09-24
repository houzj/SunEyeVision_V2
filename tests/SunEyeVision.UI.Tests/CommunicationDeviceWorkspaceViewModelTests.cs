using System.Text;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationDeviceWorkspaceViewModelTests
{
    [Fact]
    public void Text_receive_explains_when_payload_is_only_non_printable_bytes()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());

        workspace.AppendReceived([0x11], DateTimeOffset.UnixEpoch);

        Assert.Contains("收到不可显示数据", workspace.ReceivedText);
        Assert.Contains("原始 HEX：11", workspace.ReceivedText);
        Assert.Contains("切换 HEX 查看", workspace.ReceivedText);
    }

    [Fact]
    public void Text_receive_preserves_printable_ascii_and_marks_control_bytes()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());

        workspace.AppendReceived([0x41, 0x11, 0x42], DateTimeOffset.UnixEpoch);

        Assert.Contains("A\\x11B", workspace.ReceivedText);
    }

    [Fact]
    public void Hex_receive_mode_still_shows_raw_bytes()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        workspace.IsReceiveHexDisplay = true;

        workspace.AppendReceived([0x11], DateTimeOffset.UnixEpoch);

        Assert.Contains("11", workspace.ReceivedText);
        Assert.DoesNotContain("\\x11", workspace.ReceivedText);
    }

    [Fact]
    public void Text_receive_decodes_utf8_text()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());

        workspace.AppendReceived(Encoding.UTF8.GetBytes("你好"), DateTimeOffset.UnixEpoch);

        Assert.Contains("你好", workspace.ReceivedText);
    }
}
