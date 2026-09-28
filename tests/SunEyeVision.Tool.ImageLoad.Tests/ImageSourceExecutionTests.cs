using NSubstitute;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Tool.ImageLoad.Tests;

public sealed class ImageSourceExecutionTests
{
    [Fact]
    public void ToolboxShowsImageSourceButKeepsCompatibleToolId()
    {
        var metadata = typeof(ImageLoadTool).GetCustomAttribute<ToolAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal("ImageLoad", metadata.Id);
        Assert.Equal("图像源", metadata.DisplayName);
        Assert.Equal("采集", metadata.Category);
    }

    [Fact]
    public async Task CameraModeUsesHostCaptureAndOwnsClonedOutput()
    {
        var original = Substitute.For<IVisionImage>();
        original.Width.Returns(3);
        original.Height.Returns(2);
        original.Channels.Returns(1);
        original.IsEmpty.Returns(false);
        original.GetPixelData().Returns(new byte[] { 1, 2, 3, 4, 5, 6 });
        var service = Substitute.For<ICameraCaptureService>();
        service.CaptureAsync(Arg.Any<CameraCaptureRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CameraFrame(
                ((CameraCaptureRequest)call[0]).CameraRef, 42,
                DateTimeOffset.UtcNow, original)));

        var result = (ImageLoadResults)await new ImageLoadTool().ExecuteAsync(new ToolExecutionRequest(
            new ImageLoadParameters
            {
                SourceKind = ImageSourceKind.Camera,
                CameraDeviceId = "camera-1",
                CameraTimeoutMs = 1200
            })
        {
            CameraCaptureService = service
        });

        Assert.Equal(ExecutionStatus.Success, result.Status);
        Assert.Equal(ImageSourceKind.Camera, result.SourceKind);
        Assert.Equal("camera-1", result.SourceDeviceId);
        Assert.Equal(42, result.FrameId);
        Assert.NotNull(result.OutputImage);
        Assert.Equal(3, result.OutputImage.Width);
        Assert.Equal(2, result.OutputImage.Height);
        original.Received(1).Dispose();
        result.OutputImage.Dispose();
    }

    [Fact]
    public async Task CameraModeConsumesPreboundFrameWithoutCapturingAgain()
    {
        var original = Substitute.For<IVisionImage>();
        original.Width.Returns(3);
        original.Height.Returns(2);
        original.Channels.Returns(1);
        original.IsEmpty.Returns(false);
        original.GetPixelData().Returns(new byte[] { 1, 2, 3, 4, 5, 6 });
        var service = Substitute.For<ICameraCaptureService>();
        using var frame = new CameraFrame(new CameraDeviceRef("camera-1"), 123,
            DateTimeOffset.UtcNow, original);

        var result = (ImageLoadResults)await new ImageLoadTool().ExecuteAsync(new ToolExecutionRequest(
            new ImageLoadParameters { SourceKind = ImageSourceKind.Camera, CameraDeviceId = "camera-1" })
        {
            CameraCaptureService = service,
            CameraFrame = frame
        });

        Assert.Equal(ExecutionStatus.Success, result.Status);
        Assert.Equal(123, result.FrameId);
        Assert.False(frame.IsDisposed);
        await service.DidNotReceiveWithAnyArgs().CaptureAsync(default!, default);
        result.OutputImage.Dispose();
    }

    [Fact]
    public async Task CameraModeDoesNotFallBackToLocalFileWhenServiceIsMissing()
    {
        var result = (ImageLoadResults)await new ImageLoadTool().ExecuteAsync(new ToolExecutionRequest(
            new ImageLoadParameters
            {
                SourceKind = ImageSourceKind.Camera,
                CameraDeviceId = "camera-1",
                FilePath = @"C:\stale-image.bmp"
            }));

        Assert.Equal(ExecutionStatus.Failed, result.Status);
        Assert.Null(result.OutputImage);
    }

    [Theory]
    [InlineData(ImageOutputFormat.Original, 1, new byte[] { 7, 8 })]
    [InlineData(ImageOutputFormat.Mono8, 1, new byte[] { 7, 8 })]
    [InlineData(ImageOutputFormat.Rgb24, 3, new byte[] { 7, 7, 7, 8, 8, 8 })]
    public async Task CameraOutputFormatConvertsPixelsWithoutChangingDeviceFrame(
        ImageOutputFormat format, int channels, byte[] expected)
    {
        var original = Substitute.For<IVisionImage>();
        original.Width.Returns(2);
        original.Height.Returns(1);
        original.Channels.Returns(1);
        original.GetPixelData().Returns(new byte[] { 7, 8 });
        using var frame = new CameraFrame(new CameraDeviceRef("camera-1"), 99,
            DateTimeOffset.UtcNow, original);
        var result = (ImageLoadResults)await new ImageLoadTool().ExecuteAsync(new ToolExecutionRequest(
            new ImageLoadParameters
            {
                SourceKind = ImageSourceKind.Camera,
                CameraDeviceId = "camera-1",
                OutputFormat = format,
                SequenceNumber = 12
            }) { CameraFrame = frame });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(channels, result.Channels);
        Assert.Equal(expected, result.OutputImage.GetPixelData());
        Assert.Equal(12, result.SequenceNumber);
        Assert.Equal(99, result.FrameId);
        Assert.Equal(channels == 1 ? "MONO8" : "RGB24", result.PixelFormat);
        result.OutputImage.Dispose();
    }

    [Theory]
    [InlineData(ImageSourceKind.Local)]
    [InlineData(ImageSourceKind.Camera)]
    public void ResultContractUsesSupportedTypesForSourceMetadata(ImageSourceKind sourceKind)
    {
        var timestamp = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var result = new ImageLoadResults
        {
            SourceKind = sourceKind,
            SourceDeviceId = "camera-1",
            FrameId = sourceKind == ImageSourceKind.Camera ? 42 : null,
            FrameTimestamp = sourceKind == ImageSourceKind.Camera ? timestamp : null
        };

        var outputs = result.GetOutputVariables();
        Assert.Contains(outputs, item => item.Name == "SourceKindText" && item.VariableType == "String");
        Assert.Contains(outputs, item => item.Name == "FrameIdText" && item.VariableType == "String");
        Assert.Contains(outputs, item => item.Name == "FrameTimestampText" && item.VariableType == "String");
        Assert.Contains(outputs, item => item.Name == "SequenceNumber" && item.VariableType == "Integer");
        Assert.Equal(sourceKind.ToString(), result.SourceKindText);
        Assert.Equal(sourceKind == ImageSourceKind.Camera ? "42" : string.Empty, result.FrameIdText);
        Assert.Equal(sourceKind == ImageSourceKind.Camera ? timestamp.ToString("O") : string.Empty,
            result.FrameTimestampText);
    }
}
