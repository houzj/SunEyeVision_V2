using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Converters;

/// <summary>
/// 形状类型（<see cref="ShapeType"/>）→ 中文短名（"圆" / "旋转矩形" / …），供形状 ComboBox 显示。
/// </summary>
/// <remarks>
/// 真相源：<see cref="ArrayTreeFormatter.GetShapeTypeShortName"/>（与绑定树标签**同源**，rule-008 不造第二份命名表）。
/// 历史观感缺陷：形状下拉直接显示枚举英文名（RotatedRectangle 等）——本转换器统一为中文短名。
/// 仅用于显示，不支持反向转换（<c>SelectedItem</c> 仍绑定枚举本身）。
/// </remarks>
public sealed class ShapeTypeDisplayNameConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ShapeType shapeType ? ArrayTreeFormatter.GetShapeTypeShortName(shapeType) : string.Empty;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("形状显示名仅用于显示，不支持反向转换（SelectedItem 绑定枚举本身）。");
}
