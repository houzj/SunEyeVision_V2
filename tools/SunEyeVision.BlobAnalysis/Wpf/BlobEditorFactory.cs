using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolPresentationFactory
{
    // Configuration is provided completely by the host's declaration/ROI editor.
    // This optional companion currently supplies the specialized read-only result view only.
    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
        =>new BlobResultViewFactory().CreateResultView(schema,projection);
}
