using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.BlobLabels.Wpf.Views;

namespace SunEyeVision.Tool.BlobLabels.Wpf;

/// <summary>BlobLabels 的参数页与结果页工厂。两个视图都是正式视图，没有第二套模型。</summary>
public sealed class BlobLabelsEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new BlobLabelsParameterEditor(draft, options);

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(projection);
        var errors = projection.Validate(schema);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return new BlobLabelsResultView(schema, projection);
    }
}
