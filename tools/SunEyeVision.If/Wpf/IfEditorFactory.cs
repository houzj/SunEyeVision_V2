using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Tool.If.Wpf;

/// <summary>条件分支只组合已声明的参数页与结果呈现；分支走向由 Runtime 报给宿主。</summary>
public sealed class IfEditorFactory:IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        =>new Views.IfParameterEditor(draft,options with {EagerlyComposePages=true});

    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
        =>projection.Kind==ResultProjectionKind.PropertyList
            ?new DeclaredPropertyList(schema,projection)
            :new DeclaredResultTable(schema,projection);
}
