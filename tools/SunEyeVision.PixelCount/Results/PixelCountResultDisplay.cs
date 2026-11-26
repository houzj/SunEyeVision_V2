using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>
/// 原厂「结果显示」页的图像显示项（<c>IMVSPixelCountModuVAAlgorithmTab.xml:176-249</c> 的
/// <c>CheckZone</c>/<c>Fixtured Point</c>/<c>Unfixtured Point</c>/<c>ResutlText</c>，颜色取
/// <c>IMVSPixelCountModuVADisplay.xml</c> 的 <c>OK/NG</c> 色与线宽）。
/// </summary>
/// <remarks>
/// 声明已迁到 <see cref="PixelCountManifest"/>：字段身份、默认值、显示合同与外观投影全部
/// 由 <see cref="ManifestCompiler"/> 生成，本类只保留外部引用的字段身份与便捷入口。
/// 位置修正两点（Fixtured/Unfixtured Point）随位置修正能力一起未实现，见交接单未实现项。
/// </remarks>
public static class PixelCountResultDisplay
{
    public const string Roi = "display.roi";
    public const string ReferencePoint = "display.reference-point";
    public const string RunPoint = "display.run-point";
    public const string Text = "display.text";
    public static string Field(string group, string role) => group + "." + role;

    public static ToolSchema Schema { get; } = ManifestCompiler.DisplaySchema(PixelCountManifest.Value);

    /// <summary>
    /// 结果页显示需求：行集合、行身份、矩形、区域、文本值、判定。
    /// 不写进这里的结果资源，结果页不会叠加（Blob 工具踩过的坑）。
    /// </summary>
    public static ResultDisplayDescriptor Descriptor { get; } = ManifestCompiler.Descriptor(PixelCountManifest.Value);

    public static InputValues Defaults() => ManifestCompiler.DisplayDefaults(PixelCountManifest.Value);

    /// <summary>原厂 Display.xml 的默认文本模板。</summary>
    public const string DefaultTextContent = PixelCountManifest.TextContent;

    public static ResultDisplayAppearance Capture(FieldSettings fields)
        => ManifestCompiler.Appearance(PixelCountManifest.Value, fields);
}
