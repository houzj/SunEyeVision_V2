using System;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>Frame identity at bitmap conversion time. Does not retain the originating vision image.</summary>
public static class PreviewFrameProvenance
{
    private sealed record Frame(Guid Id);
    private static readonly ConditionalWeakTable<BitmapSource, Frame> Frames = new();
    private static readonly object Gate = new();

    public static void Attach(BitmapSource? bitmap, Guid? frame)
    {
        if (bitmap == null) return;
        lock (Gate)
        {
            Frames.Remove(bitmap);
            if (frame.HasValue) Frames.Add(bitmap, new Frame(frame.Value));
        }
    }

    public static Guid? Read(BitmapSource? bitmap)
    {
        lock (Gate) return bitmap != null && Frames.TryGetValue(bitmap, out var frame) ? frame.Id : null;
    }
}
