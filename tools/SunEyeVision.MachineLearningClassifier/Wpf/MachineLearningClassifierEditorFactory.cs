using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Tool.MachineLearningClassifier.Wpf.Views;

namespace SunEyeVision.Tool.MachineLearningClassifier.Wpf;

/// <summary>ML 分类参数页与结果页工厂。结果为属性列表（标量预测数量 + 数组标签 + ROI 属性）。</summary>
public sealed class MachineLearningClassifierEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new MachineLearningClassifierParameterEditor(draft, options with { EagerlyComposePages = true });

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => projection.Kind == ResultProjectionKind.PropertyList
            ? new DeclaredPropertyList(schema, projection)
            : new DeclaredResultTable(schema, projection);
}
