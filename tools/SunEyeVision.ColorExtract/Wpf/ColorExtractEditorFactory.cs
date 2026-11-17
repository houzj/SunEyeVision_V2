using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Tool.ColorExtract.Wpf;

/// <summary>伴随编辑器只组装声明字段；图像采集与 ROI 绘制由宿主/上游负责。</summary>
public sealed class ColorExtractEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new Views.ColorExtractParameterEditor(draft, options with { EagerlyComposePages = true });

    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => projection.Kind == ResultProjectionKind.PropertyList
            ? new DeclaredPropertyList(schema, projection)
            : new DeclaredResultTable(schema, projection);
}
