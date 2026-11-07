using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

[assembly: PluginContractAttribute(FoundationContract.Major, FoundationContract.Minor)]

namespace ExternalWpfTool;

public sealed class WpfToolPresentationFactory : IToolPresentationFactory
{
    public IToolResultView CreateResultView(ToolSchema schema, ResultProjectionDescriptor projection)
        => new EmptyResultView(schema.Outputs);

    public IToolSettingsView CreateSettingsView(IToolSettingsDraft draft, ToolSettingsViewOptions options)
        => new DeclaredSettingsView(draft, options);

    private sealed class EmptyResultView(IReadOnlyList<Port> sources) : IToolResultView
    {
        public FrameworkElement View { get; } = new Border { Child = new TextBlock { Text = "External tool result" } };
        public IReadOnlyList<Port> Sources { get; } = sources;
        public event EventHandler<ResultSelection>? SelectionHighlightChanged;
        public void Present(IResultPublication publication) { }
        public void Clear() { }
        public void Dispose() => SelectionHighlightChanged = null;
    }
}
