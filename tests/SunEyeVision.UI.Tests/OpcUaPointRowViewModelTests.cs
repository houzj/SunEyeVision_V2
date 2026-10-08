using System.Collections.Generic;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OpcUaPointRowViewModelTests
{
    [Fact]
    public void Renaming_point_notifies_receive_event_dropdown_label()
    {
        var point = new OpcUaDataPoint { TagName = "Channel.Device.Old" };
        var row = new OpcUaPointRowViewModel(point);
        var changed = new List<string?>();
        row.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        row.UpdatePointConfiguration(new OpcUaDataPoint { TagName = "Channel.Device.New" });

        Assert.Equal("Channel.Device.New", row.TagName);
        Assert.Equal("New", row.Name);
        Assert.Contains(nameof(row.TagName), changed);
    }

    [Fact]
    public void Failed_read_is_visible_and_a_later_string_read_recovers()
    {
        var point = new OpcUaDataPoint { TagName = "Channel.Device.test3" };
        var row = new OpcUaPointRowViewModel(point);
        var time = new System.DateTimeOffset(2026, 10, 1, 12, 0, 0, System.TimeSpan.Zero);

        row.Apply(new OpcUaPointValueEventArgs("device", point.NodeId, null,
            BuiltInType.Null, false, StatusCodes.Bad, time, "BadNodeIdUnknown"));
        Assert.Equal("读取失败", row.TypeText);
        Assert.Equal("BadNodeIdUnknown", row.ErrorText);

        row.Apply(new OpcUaPointValueEventArgs("device", point.NodeId, "11111",
            BuiltInType.String, true, StatusCodes.Good, time));
        Assert.Equal("String", row.TypeText);
        Assert.Equal("11111", row.ValueText);
        Assert.Equal(string.Empty, row.ErrorText);
    }
}
