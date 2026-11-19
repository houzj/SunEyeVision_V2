using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf;

public sealed class BlobEditorFactory : IToolPresentationFactory
{
    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft,ToolSettingsViewOptions options)
        // The selected page builds first. The other declared pages compose on selection.
        =>new Views.BlobParameterEditor(draft,options with { EagerlyComposePages = true });
    // 平台组件已完整实现 IToolResultView，工具侧不再保留包装层（原 BlobResultView 只是转发四个成员）。
    public IToolResultView CreateResultView(ToolSchema schema,ResultProjectionDescriptor projection)
        =>new DeclaredResultTable(schema,projection);
}
