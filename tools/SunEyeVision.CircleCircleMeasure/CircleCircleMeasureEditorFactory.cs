using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.CircleCircleMeasure.Wpf.Views;

namespace SunEyeVision.Tool.CircleCircleMeasure.Wpf;

/// <summary>
/// 亮度测量的参数页与结果页工厂。两个视图都是正式视图：参数页组合 Runtime 的字段声明与
/// 显示分组声明，结果页组合官方声明式呈现控件，没有第二套模型。
/// </summary>
public sealed class CircleCircleMeasureEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new CircleCircleMeasureParameterEditor(draft, options);

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(projection);
        var errors = projection.Validate(schema);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return new CircleCircleMeasureResultView(schema, projection);
    }
}
