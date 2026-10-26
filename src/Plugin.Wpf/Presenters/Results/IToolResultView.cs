using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>A read-only view. Update borrows values only for the duration of the call.
/// Views may retain scalar/immutable geometry projections, never image/region resources.</summary>
public interface IToolResultView : IDisposable
{
    FrameworkElement View { get; }
    IReadOnlyList<Port> Sources { get; }
    void Update(InputValues? values);
    event EventHandler<IReadOnlyList<GeometryValue>?>? SelectionHighlightChanged;
}

/// <summary>Optional WPF extension. Execution and result declarations belong to Runtime.</summary>
public interface IToolPresentationFactory
{
    IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection);
}
