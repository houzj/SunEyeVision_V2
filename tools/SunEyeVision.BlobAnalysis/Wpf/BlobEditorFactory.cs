using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        // VM binds the selected tab first. Remaining tabs use the same
        // template/data tree and are materialized during idle, then reused
        // on selection instead of delaying the window for every heavy slot.
        =>new Views.BlobParameterEditor(draft,options with { EagerlyComposePages = false });
    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
        =>new BlobResultViewFactory().CreateResultView(schema,projection);
}
