using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class CameraContractTests
{
    [Fact]
    public void CameraFrame_OwnsAndDisposesImageExactlyOnce()
    {
        var image = new TestVisionImage();
        var frame = new CameraFrame(
            new CameraDeviceRef("inspection-camera"),
            7,
            DateTimeOffset.UtcNow,
            image);

        frame.Dispose();
        frame.Dispose();

        Assert.True(frame.IsDisposed);
        Assert.Equal(1, image.DisposeCount);
    }

    [Fact]
    public void CaptureRequest_DefaultsToSoftwareTriggerAndFiniteTimeout()
    {
        var request = new CameraCaptureRequest();

        Assert.Equal(CameraTriggerMode.Software, request.TriggerMode);
        Assert.True(request.Timeout > TimeSpan.Zero);
        Assert.Empty(request.Parameters);
    }

    [Fact]
    public void CameraDescriptor_PreservesStableIdentityAndTransport()
    {
        var descriptor = new CameraDescriptor
        {
            DeviceId = "hikvision:ABC123",
            CameraRef = new CameraDeviceRef("inspection-camera"),
            Manufacturer = "Hikvision",
            SerialNumber = "ABC123",
            Transport = CameraTransportKind.GigEVision,
            Protocol = CameraProtocolKind.GenICam
        };

        Assert.Equal("ABC123", descriptor.SerialNumber);
        Assert.Equal(CameraTransportKind.GigEVision, descriptor.Transport);
        Assert.Equal(CameraProtocolKind.GenICam, descriptor.Protocol);
    }

    [Fact]
    public void CameraParameterSnapshot_IsRunScoped()
    {
        var snapshot = new CameraParameterSnapshot
        {
            CameraRef = new CameraDeviceRef("inspection-camera"),
            CapturedAt = DateTimeOffset.UtcNow,
            Values = new Dictionary<string, object?>
            {
                ["ExposureTime"] = 10000d,
                ["Gain"] = 2.5d
            }
        };

        Assert.Equal(10000d, snapshot.Values["ExposureTime"]);
        Assert.Equal(2.5d, snapshot.Values["Gain"]);
    }

    private sealed class TestVisionImage : IVisionImage
    {
        public int DisposeCount { get; private set; }
        public int Width => 1;
        public int Height => 1;
        public int Channels => 1;
        public bool IsEmpty => false;

        public byte[] GetPixelData() => new byte[] { 0 };

        public IVisionImage Clone() => new TestVisionImage();

        public void Dispose() => DisposeCount++;
    }
}
