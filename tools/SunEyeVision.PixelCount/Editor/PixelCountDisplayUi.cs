using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.PixelCount.Runtime;

namespace SunEyeVision.Tool.PixelCount.Wpf;

/// <summary>
/// PixelCount 的结果显示只提供原厂分组与字段身份，具体行模板和生命周期由
/// DisplayOptionsFactory/DeclaredSettingsView 统一创建。
/// </summary>
internal static class PixelCountDisplayUi
{
    private sealed class Declaration : IDisplayDeclaration
    {
        public IReadOnlyList<string> GraphicGroups { get; } = new[]
        {
            PixelCountResultDisplay.Roi,
            PixelCountResultDisplay.ReferencePoint,
            PixelCountResultDisplay.RunPoint
        };
        public IReadOnlyList<string> VisibilityGroups { get; } = new[] { PixelCountResultDisplay.Text };
        public string? TextGroup => PixelCountResultDisplay.Text;
        public IReadOnlyList<string> GraphicRoles { get; } = new[] { "visible", "ok", "ng", "opacity", "width" };
        public IReadOnlyList<string> TextRoles { get; } = new[] { "content", "ok", "ng", "size", "opacity", "x", "y" };
        public string FieldId(string group, string role) => PixelCountResultDisplay.Field(group, role);
    }

    private static readonly IDisplayDeclaration Display = new Declaration();

    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
        => DisplayOptionsFactory.Apply(options, Display);
}
