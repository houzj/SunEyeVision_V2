using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Plugin.SDK.Vision.Contracts;

/// <summary>Captures pixels and actual Domain together. Caller supplies the admitted invocation/output frame identity.</summary>
public interface ITypedImageSnapshotSource
{
    FrozenImageResource CaptureSnapshot(ImageFrameAddress frame);
}
