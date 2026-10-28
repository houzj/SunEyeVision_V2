using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Stable identities supplied by the caller, never a second pose/settings model.</summary>
public sealed record NativeAssistNumbers(string XFieldId,string YFieldId,string AngleFieldId,
    string ScaleXFieldId,string ScaleYFieldId);

/// <summary>The original three-way Assist composition. FixturePort is the caller's actual declaration;
/// this library neither declares a native FIXTURE representation nor computes a transform.</summary>
public sealed record NativeAssistOptions(string EnabledFieldId,string ModeFieldId,Port FixturePort,
    string BasePointFieldId,string RunPointFieldId,NativeAssistNumbers Base,NativeAssistNumbers Run);
