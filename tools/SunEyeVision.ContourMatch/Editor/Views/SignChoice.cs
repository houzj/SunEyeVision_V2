namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 样本列表的「符号」下拉项，字段名与原厂 SignViewItem 对齐
/// （decompiled IMVSHPFeatureMatchModuControl/SignViewItem.cs）：
/// xaml 的 ComboBoxSignList 项模板直接绑定 DisplayValue / ComboxItemImgNormal|Select|Disable。
/// 原厂轮廓匹配只有两项——匹配（Lang_Match，ic_yes）与屏蔽（Lang_Blind，ic_shield）；
/// HPPyramidFeaturepatMatchDialogUI.CustomInitSign 为空实现，不追加"增强匹配"。
/// </summary>
public sealed class SignChoice
{
    private const string ResourceRoot =
        "/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/ParameterPresentation/resources/";

    public string DisplayValue { get; init; } = "";
    public string ComboxImgNormal { get; init; } = "";
    public string ComboxImgDisable { get; init; } = "";
    public string ComboxImgSelect { get; init; } = "";
    public string ComboxItemImgNormal { get; init; } = "";
    public string ComboxItemImgDisable { get; init; } = "";
    public string ComboxItemImgSelect { get; init; } = "";
    public bool IsBackgroundImage { get; init; }

    /// <summary>匹配（正样本）。</summary>
    public static SignChoice Match { get; } = Build("匹配", "ic_yes", background: false);

    /// <summary>屏蔽（背景样本）。</summary>
    public static SignChoice Blind { get; } = Build("屏蔽", "ic_shield", background: true);

    private static SignChoice Build(string display, string icon, bool background) => new()
    {
        DisplayValue = display,
        ComboxImgNormal = ResourceRoot + icon + "_normal.png",
        ComboxImgDisable = ResourceRoot + icon + "_disable.png",
        ComboxImgSelect = ResourceRoot + icon + "_select.png",
        ComboxItemImgNormal = ResourceRoot + icon + "_small_normal.png",
        ComboxItemImgDisable = ResourceRoot + icon + "_small_disable.png",
        ComboxItemImgSelect = ResourceRoot + icon + "_small_select.png",
        IsBackgroundImage = background
    };

    public override string ToString() => DisplayValue;
}
