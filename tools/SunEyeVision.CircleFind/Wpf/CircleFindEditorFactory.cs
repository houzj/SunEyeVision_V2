using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Tool.CircleFind.Wpf.Views;

namespace SunEyeVision.Tool.CircleFind.Wpf;

/// <summary>
/// 圆查找的参数页与结果页工厂。参数页沿用声明式设置视图（字段、默认值与显隐都来自 Runtime 声明），
/// 结果页是正式的结果表视图，没有第二套模型。
/// </summary>
public sealed class CircleFindEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new CircleFindParameterEditor(draft, options);

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(projection);
        var errors = projection.Validate(schema);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
        return new CircleFindResultView(schema, projection);
    }
}
