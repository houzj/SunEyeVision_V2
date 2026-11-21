using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>
/// 模板窗口通用 Converter，逐字迁移自反编译原件（保留原语义，仅改中立命名空间）：
/// - DoubleToFloatConvert：docs/07-工具逆向/decompiled/ModuleControls/IMVSHPFeatureMatchModuControl/IMVSHPFeatureMatchModuControl/DoubleToFloatConvert.cs
/// - FloatToIntConvert：docs/.../IMVSContourMatchModuControl/.../FloatToIntConvert.cs
/// - SelectedIndexToBoolConverter：docs/.../IMVSContourMatchModuControl/.../SelectedIndexToBoolConverter.cs
/// 证据键：A:9-11 / C:11-13 / B:8（hppyramid/graymatch/basestyle xaml）。属 HP 匹配系模板窗口公共 L3 资产。
/// </summary>
public class DoubleToFloatConvert : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return DependencyProperty.UnsetValue;
        try { return ((float)double.Parse(value.ToString()!)).ToString(); }
        catch { return DependencyProperty.UnsetValue; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return DependencyProperty.UnsetValue;
        try { return double.Parse(value.ToString()!).ToString(); }
        catch { return DependencyProperty.UnsetValue; }
    }
}

public class FloatToIntConvert : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return DependencyProperty.UnsetValue;
        try { return ((int)float.Parse(value.ToString()!)).ToString(); }
        catch { return DependencyProperty.UnsetValue; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return DependencyProperty.UnsetValue;
        try { return float.Parse(value.ToString()!).ToString(); }
        catch { return DependencyProperty.UnsetValue; }
    }
}

public class SelectedIndexToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try { return !(bool)value ? 1 : 0; }
        catch { throw new NotImplementedException(); }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try { return (int)value == 1 ? false : true; }
        catch { throw new NotImplementedException(); }
    }
}
