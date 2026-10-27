namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Presentation transition lease: freeze raw edits before native visibility/focus events.
/// Explicit capture remains available while hidden; this transition never accepts a value.</summary>
internal interface IParameterVisibility
{
    void PrepareForVisibility(bool visible);
}
