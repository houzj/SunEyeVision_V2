using SunEyeVision.Plugin.Wpf.Adapters.References;
using System.Windows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

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
