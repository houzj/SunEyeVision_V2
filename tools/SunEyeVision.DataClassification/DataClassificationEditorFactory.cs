using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Tool.DataClassification.Wpf.Views;

namespace SunEyeVision.Tool.DataClassification.Wpf;

/// <summary>数据分类的参数页与结果页工厂。结果为属性列表（原厂仅 ModuRunTime/ModuStatus 两项）。</summary>
public sealed class DataClassificationEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new DataClassificationParameterEditor(draft, options with { EagerlyComposePages = true });

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => projection.Kind == ResultProjectionKind.PropertyList
            ? new DeclaredPropertyList(schema, projection)
            : new DeclaredResultTable(schema, projection);
}
