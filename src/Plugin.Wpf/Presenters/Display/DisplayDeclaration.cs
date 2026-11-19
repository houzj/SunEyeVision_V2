using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>
/// 结果页显示声明：工具只报名单与角色解析方式，图形项、可见性项和文本项由平台生成。
/// 对应原厂 Display.xml 的 Object 清单与 AlgorithmTab 的 ImageViewConfig 登记。
/// </summary>
public interface IDisplayDeclaration
{
    /// <summary>图形分组，按显示顺序排列。</summary>
    IReadOnlyList<string> GraphicGroups { get; }
    /// <summary>只有显隐开关、没有图形的分组。</summary>
    IReadOnlyList<string> VisibilityGroups { get; }
    /// <summary>文本分组；没有文本显示时返回 null。</summary>
    string? TextGroup { get; }
    /// <summary>图形分组用到的角色名，顺序固定。</summary>
    IReadOnlyList<string> GraphicRoles { get; }
    /// <summary>文本分组用到的角色名，顺序固定。</summary>
    IReadOnlyList<string> TextRoles { get; }
    /// <summary>把「分组 + 角色」解析为参数字段身份。</summary>
    string FieldId(string group, string role);
}

/// <summary>按声明生成结果页的图形、可见性与文本显示项。</summary>
public static class DisplayOptionsFactory
{
    public const string VisibleRole = "visible", PassRole = "ok", FailRole = "ng";
    public const string OpacityRole = "opacity", WidthRole = "width", ContentRole = "content";

    public static ToolSettingsViewOptions Apply(ToolSettingsViewOptions options, IDisplayDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(declaration);
        if (!declaration.GraphicRoles.Contains(VisibleRole, StringComparer.Ordinal)
            || !declaration.GraphicRoles.Contains(PassRole, StringComparer.Ordinal)
            || !declaration.GraphicRoles.Contains(FailRole, StringComparer.Ordinal)
            || !declaration.GraphicRoles.Contains(OpacityRole, StringComparer.Ordinal)
            || !declaration.GraphicRoles.Contains(WidthRole, StringComparer.Ordinal))
            throw new ArgumentException("图形角色必须包含 visible/ok/ng/opacity/width。", nameof(declaration));

        var graphics = declaration.GraphicGroups.Select(group => new NativeGraphicDisplayOptions(
            group,
            declaration.FieldId(group, VisibleRole),
            declaration.FieldId(group, PassRole),
            declaration.FieldId(group, FailRole),
            declaration.FieldId(group, OpacityRole),
            declaration.FieldId(group, WidthRole))).ToArray();

        var visibility = declaration.VisibilityGroups
            .Select(group => new NativeVisibilityDisplayOptions(declaration.FieldId(group, VisibleRole)))
            .ToArray();

        var texts = declaration.TextGroup is { Length: > 0 } textGroup
            ? new[]
            {
                new NativeTextDisplayOptions(
                    declaration.FieldId(textGroup, ContentRole),
                    declaration.FieldId(textGroup, PassRole),
                    declaration.FieldId(textGroup, FailRole),
                    declaration.FieldId(textGroup, "size"),
                    declaration.FieldId(textGroup, OpacityRole),
                    declaration.FieldId(textGroup, "x"),
                    declaration.FieldId(textGroup, "y"))
            }
            : Array.Empty<NativeTextDisplayOptions>();

        return options with { GraphicDisplays = graphics, VisibilityDisplays = visibility, TextDisplays = texts };
    }
}
