using HalconDotNet;

namespace SunEyeVision.HalAdapter;

/// <summary>
/// Process-wide HALCON platform initialisation. HALCON is admitted as a deferred
/// platform dependency, so its native runtime and operator table were previously
/// mapped by the first <c>new HImage(file)</c> inside a real execution. That put a
/// one-off platform cost (observed 24-3134 ms against a 3-5 ms steady state) on the
/// first run and made it depend on the OS file cache. One version query loads and
/// initialises the whole platform; after it every operator is registered, so no
/// per-operator prewarm is needed. Idempotent for every tool that shares HALCON.
/// </summary>
public static class HalconPlatform
{
    private static readonly object Gate = new();
    private static bool _initialised;

    /// <summary>Maps the native platform and builds the operator runtime. Safe from any
    /// thread and idempotent; a failure propagates so the host can report it instead of
    /// pretending the platform is ready.</summary>
    public static void Initialise()
    {
        lock (Gate)
        {
            if (_initialised) return;
            HOperatorSet.GetSystem("version", out var reported);
            reported.Dispose();
            _initialised = true;
        }
    }
}
