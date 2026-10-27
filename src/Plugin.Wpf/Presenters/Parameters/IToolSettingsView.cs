using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>InputContent ownership transfers to the settings view on successful attachment.
/// Rebinding the same instance retains it; replacement and final disposal release IDisposable content.
/// If creation fails before attachment, the creating caller releases its unaccepted content.</summary>
public sealed record ToolSettingsViewOptions(
    IReadOnlyDictionary<string,ReferenceSetting>? References=null,
    ReferenceFieldOptions? ReferenceOptions=null,
    Func<OutputReference,string>? FormatReference=null,
    string? InputSection=null,FrameworkElement? InputContent=null,
    Action<object,IReadOnlyDictionary<string,ReferenceSetting>>? AcceptCandidate=null);

public interface IToolSettingsView : IDisposable
{
    FrameworkElement View { get; }
    bool HasInvalidInput { get; }
    event Action? Changed;
    bool TryCapture(out object settings,out IReadOnlyDictionary<string,ReferenceSetting> references,out string error);
}
