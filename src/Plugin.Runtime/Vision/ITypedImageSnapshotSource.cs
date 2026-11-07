using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime.Vision;

/// <summary>Captures pixels and actual Domain together. Caller supplies the admitted invocation/output frame identity.</summary>
public interface ITypedImageSnapshotSource
{
    FrozenImageResource CaptureSnapshot(ImageFrameAddress frame);
}
