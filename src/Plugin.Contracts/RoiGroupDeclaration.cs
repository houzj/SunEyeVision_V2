using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>ROI 参数行：字段身份与显示标签。</summary>
public sealed record RoiParameterRowDeclaration(string FieldId, string Label);

/// <summary>ROI 整数单选选项：取值与显示名。</summary>
public sealed record RoiChoiceDeclaration(long Value, string Label);

/// <summary>
/// 参数页里一段 ROI 组声明，对应原厂 AlgorithmTab 的 ROISelecter + 三个 GroupLinkItem。
/// 平台据此在分组内直接创建控件，工具不再于运行时挂接。
/// </summary>
public sealed record RoiGroupDeclaration(
    string Section,
    string ModeFieldId,
    string LocalModeValue,
    IReadOnlyList<string> InheritedModes,
    string RectangleSourceFieldId,
    string AnnulusSourceFieldId,
    IReadOnlyList<RoiParameterRowDeclaration> RectangleRows,
    IReadOnlyList<RoiParameterRowDeclaration> AnnulusRows,
    string AreaTypeFieldId,
    string AreaTypeLabel,
    IReadOnlyList<RoiChoiceDeclaration> AreaTypeChoices,
    string MaskImageFieldId,
    string MaskImageLabel,
    string OutputMaskFieldId,
    string OutputMaskLabel,
    string MaskSourceFieldId,
    string MaskSourceLabel,
    string MaskInheritedFieldId)
{
    /// <summary>解析 &lt;RoiGroup&gt; 段；分组归属由所在 &lt;Group&gt; 决定，与原厂 GroupLinkItem 一致。</summary>
    public static IReadOnlyList<RoiGroupDeclaration> Load(XElement? root)
    {
        if (root is null) return Array.Empty<RoiGroupDeclaration>();
        return root.Elements("Group")
            .SelectMany(group => group.Elements("RoiGroup")
                .Select(item => Parse(item, Required(group, "section"))))
            .ToArray();
    }

    private static RoiGroupDeclaration Parse(XElement element, string section)
    {
        var rows = element.Elements("Row").Select(Row).ToArray();
        var rectangle = rows.Where(row => row.Kind == "rectangle").Select(row => row.Row).ToArray();
        var annulus = rows.Where(row => row.Kind == "annulus").Select(row => row.Row).ToArray();
        var choices = element.Elements("Choice").Select(choice => new RoiChoiceDeclaration(
            long.Parse(Required(choice, "value"), System.Globalization.CultureInfo.InvariantCulture),
            Required(choice, "label"))).ToArray();
        return new RoiGroupDeclaration(
            section,
            Required(element, "modeField"),
            Required(element, "localMode"),
            Required(element, "inheritedModes").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Required(element, "rectangleSource"),
            Required(element, "annulusSource"),
            rectangle,
            annulus,
            Required(element, "areaTypeField"),
            Required(element, "areaTypeLabel"),
            choices,
            Required(element, "maskImageField"),
            Required(element, "maskImageLabel"),
            Required(element, "outputMaskField"),
            Required(element, "outputMaskLabel"),
            Required(element, "maskSourceField"),
            Required(element, "maskSourceLabel"),
            Required(element, "maskInheritedField"));
    }

    private static (string Kind, RoiParameterRowDeclaration Row) Row(XElement element)
    {
        var kind = (string?)element.Attribute("kind") ?? throw new InvalidDataException("ROI 行缺少 kind。");
        if (kind is not ("rectangle" or "annulus"))
            throw new InvalidDataException("ROI 行的 kind 必须是 rectangle 或 annulus。");
        return (kind, new RoiParameterRowDeclaration(Required(element, "field"), Required(element, "label")));
    }

    private static string Required(XElement element, string name)
        => element.Attribute(name) is { } attribute && !string.IsNullOrWhiteSpace(attribute.Value)
            ? attribute.Value
            : throw new InvalidDataException("缺少 ROI 声明属性：" + name);
}
/// <summary>位置修正来源行：模式序号、字段身份与显示名。</summary>
public sealed record RoiAssistSourceDeclaration(int Mode, string FieldId, string Label);

/// <summary>
/// 位置修正辅助组声明，对应原厂 AlgorithmTab 的 AssistGroup 链接。
/// </summary>
public sealed record RoiAssistDeclaration(
    string Section,
    string PoseFieldId,
    string ModeFieldId,
    IReadOnlyList<RoiChoiceDeclaration> ModeChoices,
    IReadOnlyList<RoiAssistSourceDeclaration> Sources)
{
    /// <summary>解析 &lt;AssistGroup&gt; 段；分组归属由所在 &lt;Group&gt; 决定。</summary>
    public static IReadOnlyList<RoiAssistDeclaration> Load(XElement? root)
    {
        if (root is null) return Array.Empty<RoiAssistDeclaration>();
        return root.Elements("Group")
            .SelectMany(group => group.Elements("AssistGroup")
                .Select(item => Parse(item, Required(group, "section"))))
            .ToArray();
    }

    private static RoiAssistDeclaration Parse(XElement element, string section)
    {
        var choices = element.Elements("Choice").Select(choice => new RoiChoiceDeclaration(
            long.Parse(Required(choice, "value"), System.Globalization.CultureInfo.InvariantCulture),
            Required(choice, "label"))).ToArray();
        var sources = element.Elements("Source").Select(source => new RoiAssistSourceDeclaration(
            int.Parse(Required(source, "mode"), System.Globalization.CultureInfo.InvariantCulture),
            Required(source, "field"), Required(source, "label"))).ToArray();
        return new RoiAssistDeclaration(
            section, Required(element, "poseField"), Required(element, "modeField"), choices, sources);
    }

    private static string Required(XElement element, string name)
        => element.Attribute(name) is { } attribute && !string.IsNullOrWhiteSpace(attribute.Value)
            ? attribute.Value
            : throw new InvalidDataException("缺少位置修正声明属性：" + name);
}