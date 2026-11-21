using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ColorExtract.Runtime;

namespace SunEyeVision.Tool.ColorExtract.Wpf;

/// <summary>颜色抽取结果页显示声明：原厂 ResultShow 的四个 ImageViewConfig。</summary>
internal static class ColorExtractDisplayUi
{
    private sealed class Declaration : IDisplayDeclaration
    {
        public IReadOnlyList<string> GraphicGroups { get; } = new[]
        {
            ColorExtractResultDisplay.CheckZone,
            ColorExtractResultDisplay.FixtureInitPoint,
            ColorExtractResultDisplay.FixtureRunPoint,
            ColorExtractResultDisplay.CheckZoneAnnular
        };
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
