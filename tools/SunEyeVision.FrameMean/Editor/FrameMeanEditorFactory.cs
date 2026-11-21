using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

namespace SunEyeVision.Tool.FrameMean.Wpf;

/// <summary>The companion composes declared editors; image acquisition stays upstream.</summary>
public sealed class FrameMeanEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new Views.FrameMeanParameterEditor(draft, options with { EagerlyComposePages = true });

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => projection.Kind == ResultProjectionKind.PropertyList
            ? new DeclaredPropertyList(schema, projection)
            : new DeclaredResultTable(schema, projection);
}
