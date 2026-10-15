using System;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers;

public enum ImageFramePolicy { Unknown, NewFrame, PreserveCoordinates }

/// <summary>Opt-in only: resized, warped or cropped images must not claim coordinate preservation.</summary>
public interface IImageFramePolicyProvider
{
    ImageFramePolicy ImageFramePolicy { get; }
}

/// <summary>Runtime-only weak metadata. Retains neither images nor results and adds no output fields.</summary>
public static class ImageFrameProvenance
{
    private sealed record Frame(Guid Id);
    private sealed record Input(Guid? Id);
    private static readonly ConditionalWeakTable<IVisionImage, Frame> Images = new();
    private static readonly ConditionalWeakTable<ToolResults, Input> Results = new();
    private static readonly object Gate = new();

    public static Guid? CaptureInput(IVisionImage? image)
    {
        if (image == null) return null;
        // Unknown plugins must remain unverified, not be guessed to start a new frame.
        lock (Gate) return Images.TryGetValue(image, out var frame) ? frame.Id : null;
    }

    public static Guid? GetInputFrame(ToolResults? result)
    {
        lock (Gate) return result != null && Results.TryGetValue(result, out var input) ? input.Id : null;
    }

    public static void RecordResult(ToolResults result, Guid? input, ImageFramePolicy policy = ImageFramePolicy.Unknown)
    {
        // Enumerate before taking the metadata lock: plugins must never run under our lock.
        var outputs = policy == ImageFramePolicy.Unknown ? null : result.GetDisplayImages();
        var outputFrame = policy == ImageFramePolicy.NewFrame ? Guid.NewGuid() : input;
        lock (Gate)
        {
            Results.Remove(result);
            Results.Add(result, new Input(input));
            if (outputs == null || !outputFrame.HasValue) return;
            foreach (var image in outputs.Values)
            {
                if (image == null) continue;
                Images.Remove(image);
                Images.Add(image, new Frame(outputFrame.Value));
            }
        }
    }
}
