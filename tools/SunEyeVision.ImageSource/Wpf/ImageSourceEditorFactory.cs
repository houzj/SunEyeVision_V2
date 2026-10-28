using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Tool.ImageSource.Wpf;

/// <summary>The companion composes declared editors; acquisition, devices and files remain host-owned.</summary>
public sealed class ImageSourceEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new Views.ImageSourceParameterEditor(draft, options);

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => new DeclaredResultTable(schema, projection);
}
