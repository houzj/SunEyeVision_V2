using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.MarkFind.Runtime;

namespace SunEyeVision.Tool.MarkFind.Wpf;

/// <summary>
/// 图形定位结果页显示声明（原厂 <c>IMVSMarkFindModuDisplay.xml</c> 的 ImageViewConfig 集合）。
/// 六个五角色图形组 + 一个文本组；「匹配轮廓点」是系统轮廓类型（只有可见/OK/NG 三色），
/// 不列入图形组，它的画笔由 <c>ToolDisplayRole.Outline</c> 单独承载。
/// </summary>
internal static class MarkFindDisplayUi
{
    private sealed class Declaration : IDisplayDeclaration
    {
        public IReadOnlyList<string> GraphicGroups { get; } = new[]
        {
            MarkFindResultDisplay.Match,
            MarkFindResultDisplay.Location,
            MarkFindResultDisplay.Inspection,
            MarkFindResultDisplay.ReferencePoint,
            MarkFindResultDisplay.RunPoint,
            MarkFindResultDisplay.Mask
        };
        public IReadOnlyList<string> VisibilityGroups { get; } = Array.Empty<string>();
        public string? TextGroup => MarkFindResultDisplay.Text;
        public IReadOnlyList<string> GraphicRoles { get; } = new[] { "visible", "ok", "ng", "opacity", "width" };
        public IReadOnlyList<string> TextRoles { get; } =
            new[] { "visible", "content", "ok", "ng", "size", "opacity", "x", "y" };
        public string FieldId(string group, string role) => group + "." + role;
    }

    private static readonly IDisplayDeclaration Display = new Declaration();

    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
        => DisplayOptionsFactory.Apply(options, Display);
}
