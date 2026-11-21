using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.FrameMean.Runtime;

namespace SunEyeVision.Tool.FrameMean.Wpf;

/// <summary>帧均值结果页显示声明：只有原厂 ResultShow 页的 CheckZone 一行。</summary>
internal static class FrameMeanDisplayUi
{
    private sealed class Declaration : IDisplayDeclaration
    {
        public IReadOnlyList<string> GraphicGroups { get; } = new[] { FrameMeanResultDisplay.CheckZone };
        public IReadOnlyList<string> VisibilityGroups { get; } = Array.Empty<string>();
        public string? TextGroup => null;
        public IReadOnlyList<string> GraphicRoles { get; } = new[] { "visible", "ok", "ng", "opacity", "width" };
        public IReadOnlyList<string> TextRoles { get; } = Array.Empty<string>();
        public string FieldId(string group, string role) => group + "." + role;
    }

    private static readonly IDisplayDeclaration Display = new Declaration();

    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
        => DisplayOptionsFactory.Apply(options, Display);
}
