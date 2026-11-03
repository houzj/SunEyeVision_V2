using System.Collections.Generic;

namespace SunEyeVision.Plugin.Contracts.Acquisition;

/// <summary>
/// 相机能力摘要。具体动态参数由驱动层 Schema 提供。
/// </summary>
public sealed record CameraCapabilities
{
    public bool SupportsSoftwareTrigger { get; init; }
    public bool SupportsHardwareTrigger { get; init; }
    public bool SupportsContinuousCapture { get; init; }
    public bool SupportsRoi { get; init; }
    public bool SupportsExposure { get; init; }
    public bool SupportsGain { get; init; }
    public IReadOnlySet<string> SupportedFeatures { get; init; }
        = new HashSet<string>();
}
