using System;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using Xunit;

namespace SunEyeVision.DeviceDriver.Tests;

public sealed class MvsCaptureConfigurationParserTests
{
    [Theory]
    [InlineData("0", "Software")]
    [InlineData("7", "Software")]
    public void SoftwareUsesSymbolicRatherThanVendorNumericValue(string raw, string symbolic)
    {
        var config = MvsCaptureConfigurationParser.Parse(
            EnumNode("AcquisitionMode", "2", ("2", "Continuous")),
            EnumNode("TriggerMode", "1", ("1", "On")),
            EnumNode("TriggerSource", raw, (raw, symbolic)),
            EnumNode("TriggerSelector", "0", ("0", "FrameStart")));

        Assert.True(config.IsValid, config.StatusMessage);
        Assert.Equal(CameraTriggerMode.Software, config.StreamMode);
        Assert.Equal("软件触发", config.TriggerSourceName);
    }

    [Fact]
    public void SameNumericValueCanMeanExternalLineOnAnotherDevice()
    {
        var config = MvsCaptureConfigurationParser.Parse(
            EnumNode("AcquisitionMode", "0", ("0", "SingleFrame")),
            EnumNode("TriggerMode", "1", ("1", "On")),
            EnumNode("TriggerSource", "0", ("0", "Line0")));

        Assert.True(config.IsValid, config.StatusMessage);
        Assert.True(config.IsSingleFrame);
        Assert.Equal(CameraTriggerMode.Hardware, config.StreamMode);
        Assert.Equal("线路 0", config.TriggerSourceName);
    }

    [Fact]
    public void TriggerOffIgnoresStaleSourceValue()
    {
        var config = MvsCaptureConfigurationParser.Parse(
            EnumNode("AcquisitionMode", "9", ("9", "Continuous")),
            EnumNode("TriggerMode", "4", ("4", "Off")),
            EnumNode("TriggerSource", "0", ("0", "Software")));

        Assert.True(config.IsValid);
        Assert.Equal(CameraTriggerMode.Continuous, config.StreamMode);
        Assert.Equal("触发关闭", config.StatusMessage);
    }

    [Fact]
    public void MissingSymbolicDoesNotGuessHardware()
    {
        var config = MvsCaptureConfigurationParser.Parse(
            EnumNode("AcquisitionMode", "2", ("2", "Continuous")),
            EnumNode("TriggerMode", "1", ("1", "On")),
            EnumNode("TriggerSource", "0", ("0", "0")));

        Assert.False(config.IsValid);
        Assert.Contains("触发源", config.StatusMessage);
    }

    [Fact]
    public void NonFrameTriggerSelectorIsRejected()
    {
        var config = MvsCaptureConfigurationParser.Parse(
            EnumNode("AcquisitionMode", "2", ("2", "Continuous")),
            EnumNode("TriggerMode", "1", ("1", "On")),
            EnumNode("TriggerSource", "0", ("0", "Software")),
            EnumNode("TriggerSelector", "3", ("3", "ExposureStart")));

        Assert.False(config.IsValid);
        Assert.Contains("FrameStart", config.StatusMessage);
    }

    private static CameraParameterDescriptor EnumNode(string name, string raw,
        params (string Value, string Symbolic)[] entries)
        => new()
        {
            Name = name,
            Kind = CameraParameterKind.Enumeration,
            Value = raw,
            EnumValues = Array.ConvertAll(entries,
                entry => new CameraEnumValue(entry.Value, entry.Symbolic))
        };
}
