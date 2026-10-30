using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobAnalysis.Views;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobResultViewFactory : IToolPresentationFactory
{
    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
    {
        var errors=projection.Validate(schema);
        if(errors.Count!=0)throw new ArgumentException(string.Join("; ",errors.Select(e=>e.Message)));
        return new BlobResultView(schema,projection);
    }
}
