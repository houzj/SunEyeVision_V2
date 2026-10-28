using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>A read-only view. Present borrows values from one explicit publication.
/// Views may retain scalar/immutable geometry projections, never image/region resources.</summary>
public interface IToolResultView : IDisposable
{
    FrameworkElement View { get; }
    IReadOnlyList<Port> Sources { get; }
    void Present(IResultPublication publication);
    void Clear();
    event EventHandler<ResultSelection>? SelectionHighlightChanged;
}

/// <summary>Optional WPF extension. Execution and result declarations belong to Runtime.</summary>
public interface IToolPresentationFactory
{
    IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection);
    SunEyeVision.Plugin.Wpf.Presenters.Parameters.IToolSettingsView CreateSettingsView(IToolSettingsDraft draft,
        SunEyeVision.Plugin.Wpf.Presenters.Parameters.ToolSettingsViewOptions options)
        =>new SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView(draft,options);
}
