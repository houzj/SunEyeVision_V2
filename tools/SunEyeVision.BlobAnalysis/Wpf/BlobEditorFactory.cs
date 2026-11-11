using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        // The selected page builds first. The other declared pages compose on selection.
        =>new Views.BlobParameterEditor(draft,options with { EagerlyComposePages = false });
    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
        =>new BlobResultViewFactory().CreateResultView(schema,projection);
}
