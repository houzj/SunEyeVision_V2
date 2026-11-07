using System;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;
using SunEyeVision.Plugin.Contracts.Vision;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Helpers;

public enum ImageFramePolicy { Unknown, NewFrame, PreserveCoordinates }

/// <summary>Opt-in only: resized, warped or cropped images must not claim coordinate preservation.</summary>
public interface IImageFramePolicyProvider
{
    ImageFramePolicy ImageFramePolicy { get; }
}

/// <summary>Runtime-only weak metadata. Retains neither images nor results and adds no output fields.</summary>
public static class ImageFrameProvenance
{
    private sealed record Frame(Guid Id,SunEyeVision.Plugin.Contracts.ImageFrameAddress? Address=null);
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
    public static SunEyeVision.Plugin.Contracts.ImageFrameAddress? CaptureAddress(IVisionImage? image)
    {lock(Gate)return image is not null&&Images.TryGetValue(image,out var frame)?frame.Address:null;}
    /// <summary>Only after the backend made an exact pixel-grid/Domain copy.</summary>
    public static void RecordExactCopy(IVisionImage image,SunEyeVision.Plugin.Contracts.ImageFrameAddress address)
    {ArgumentNullException.ThrowIfNull(image);ArgumentNullException.ThrowIfNull(address);lock(Gate){Images.Remove(image);Images.Add(image,new Frame(address.FrameId,address));}}
    public static void ForgetAddress(IVisionImage image)
    {lock(Gate)if(Images.TryGetValue(image,out var frame)){Images.Remove(image);Images.Add(image,new Frame(frame.Id));}}

    public static Guid? GetInputFrame(ToolResults? result)
    {
        lock (Gate) return result != null && Results.TryGetValue(result, out var input) ? input.Id : null;
    }

    public static void RecordResult(ToolResults result, Guid? input, ImageFramePolicy policy = ImageFramePolicy.Unknown,
        SunEyeVision.Plugin.Contracts.ImageFrameAddress? inputAddress=null)
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
                Images.Add(image, new Frame(outputFrame.Value,policy==ImageFramePolicy.PreserveCoordinates&&inputAddress?.FrameId==input?inputAddress:null));
            }
        }
    }
}
