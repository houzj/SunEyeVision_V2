using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Converters;
using Xunit;

namespace SunEyeVision.UI.Tests;

public class PreviewFrameProvenanceTests
{
    private sealed class Image : IVisionImage
    {
        public int Width => 1;
        public int Height => 1;
        public int Channels => 1;
        public bool IsEmpty => false;
        public Action? Reading;
        public byte[] GetPixelData() { Reading?.Invoke(); return new byte[] { 7 }; }
        public IVisionImage Clone() => new Image();
        public void Dispose() { }
    }
    private sealed class Result : ToolResults
    {
        public IVisionImage? Image;
        public override Dictionary<string, IVisionImage?> GetDisplayImages() => new() { ["Image"] = Image };
    }

    [Fact]
    public void Converted_preview_keeps_conversion_frame_when_source_object_is_reused()
    {
        var image = new Image(); var result = new Result { Image = image };
        ImageFrameProvenance.RecordResult(result, null, ImageFramePolicy.NewFrame);
        var firstFrame = ImageFrameProvenance.CaptureInput(image);
        var first = VisionImageToBitmapSourceConverter.ToBitmapSource(image);
        Assert.NotNull(first); Assert.True(first.IsFrozen);
        Assert.Equal(firstFrame, PreviewFrameProvenance.Read(first));
        ImageFrameProvenance.RecordResult(result, null, ImageFramePolicy.NewFrame);
        var second = new UiVisionImageToBitmapSourceConverter().ToBitmapSource(image);
        Assert.Equal(firstFrame, PreviewFrameProvenance.Read(first));
        Assert.NotEqual(firstFrame, PreviewFrameProvenance.Read(second));
        Assert.Equal(ImageFrameProvenance.CaptureInput(image), PreviewFrameProvenance.Read(second));
        PreviewFrameProvenance.Attach(second, null);
        Assert.Null(PreviewFrameProvenance.Read(second));
    }

    [Fact]
    public void Unknown_images_remain_unverified_and_frame_change_during_conversion_is_not_published()
    {
        var image = new Image();
        Assert.Null(PreviewFrameProvenance.Read(VisionImageToBitmapSourceConverter.ToBitmapSource(image)));
        var result = new Result { Image = image };
        ImageFrameProvenance.RecordResult(result, null, ImageFramePolicy.NewFrame);
        image.Reading = () => ImageFrameProvenance.RecordResult(result, null, ImageFramePolicy.NewFrame);
        Assert.Null(VisionImageToBitmapSourceConverter.ToBitmapSource(image));
    }
}
