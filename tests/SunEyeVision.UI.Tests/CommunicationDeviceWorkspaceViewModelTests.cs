using System.Text;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationDeviceWorkspaceViewModelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Modbus_history_keeps_latest_200_records_and_sequence_keeps_increasing(bool polling)
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        var point = new ModbusAddressPoint { StartAddress = 0, Count = 1, PollEnabled = true };
        Assert.True(workspace.TrySaveModbusAddressPoint(point, out var error), error);
        for (var index = 1; index <= 205; index++)
        {
            var updates = new[] { new ModbusPollUpdate(point, new[] { (ushort)index },
                DateTimeOffset.UnixEpoch.AddSeconds(index), 0) };
            if (polling) workspace.ApplyModbusPollResults(updates);
            else workspace.ApplyModbusSyncResults(updates);
        }

        Assert.Equal(200, point.ReceiveRecords.Count);
        Assert.StartsWith("#6 ", point.ReceiveRecords[0]);
        Assert.StartsWith("#205 ", point.ReceiveRecords[^1]);
        Assert.Contains("[00][CD]", point.ReceiveRecords[^1]);
    }

    [Fact]
    public void Manual_sync_updates_readable_block_even_when_polling_is_disabled()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        var point = new ModbusAddressPoint { Name = "Address", StartAddress = 0, Count = 1, PollEnabled = false };
        Assert.True(workspace.TrySaveModbusAddressPoint(point, out var error), error);
        var update = new ModbusPollUpdate(point, new ushort[] { 0x1234 }, DateTimeOffset.UnixEpoch, 0);

        Assert.Equal(0, workspace.ApplyModbusPollResults(new[] { update }));
        Assert.Equal(1, workspace.ApplyModbusSyncResults(new[] { update }));
        Assert.Equal("[12] [34]", workspace.AddressSpaceRows[0].Cells[0].ValueText);
        Assert.Single(point.ReceiveRecords);
    }

    [Fact]
    public void Manual_sync_rejects_result_if_address_changed_during_read()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        var point = new ModbusAddressPoint { StartAddress = 0, Count = 1 };
        Assert.True(workspace.TrySaveModbusAddressPoint(point, out var error), error);
        var update = new ModbusPollUpdate(point, new ushort[] { 0x1234 }, DateTimeOffset.UnixEpoch, 0);
        point.StartAddress = 1;

        Assert.Equal(0, workspace.ApplyModbusSyncResults(new[] { update }));
        Assert.Empty(point.ReceiveRecords);
    }

    [Fact]
    public void Address_space_tracks_unaligned_start_and_partial_final_row()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        workspace.ModbusRegisterStartAddress = 5;
        workspace.ModbusRegisterCount = 45;

        Assert.Equal(5, workspace.AddressSpaceRows.Count);
        Assert.Equal(5, workspace.AddressSpaceRows[0].StartAddress);
        Assert.Equal(45, workspace.AddressSpaceRows[4].StartAddress);
        Assert.All(workspace.AddressSpaceRows, row => Assert.Equal(10, row.Cells.Count));
        Assert.Equal(5, workspace.AddressSpaceRows[4].Cells.Count(cell => cell.IsInRange));
        Assert.Equal(49, workspace.AddressSpaceRows[4].Cells[4].Address);
        Assert.False(workspace.AddressSpaceRows[4].Cells[5].IsInRange);
    }

    [Fact]
    public void Address_bar_segments_use_actual_register_intersection()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        workspace.ModbusRegisterStartAddress = 5;
        workspace.ModbusRegisterCount = 12;
        var point = new ModbusAddressPoint { Name = "Address", StartAddress = 13, Count = 4 };
        Assert.True(workspace.TrySaveModbusAddressPoint(point, out var error), error);

        var first = Assert.Single(workspace.AddressSpaceRows[0].Bars);
        Assert.Equal(8, first.StartColumn);
        Assert.Equal(2, first.ColumnSpan);
        Assert.False(first.IsLast);

        var second = Assert.Single(workspace.AddressSpaceRows[1].Bars);
        Assert.Equal(0, second.StartColumn);
        Assert.Equal(2, second.ColumnSpan);
        Assert.True(second.IsLast);
    }

    [Fact]
    public void Address_space_clamps_count_at_last_modbus_address()
    {
        var workspace = new CommunicationDeviceWorkspaceViewModel(new Communication());
        workspace.ModbusRegisterStartAddress = ushort.MaxValue;

        Assert.Equal(1, workspace.ModbusRegisterCount);
        Assert.Equal(1, workspace.MaxModbusRegisterCount);
        Assert.True(workspace.AddressSpaceRows[0].Cells[0].IsInRange);
        Assert.False(workspace.AddressSpaceRows[0].Cells[1].IsInRange);
    }

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
