using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution;

using SunEyeVision.Plugin.SDK.Execution.Parameters;
namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Converters
{
    /// <summary>
    /// 形状类型到图标转换器
    /// </summary>
    public class ShapeTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ShapeType shapeType)
            {
                return shapeType switch
                {
                    ShapeType.Point => Geometry.Parse("M 12,2 L 12,2"),
                    ShapeType.Line => Geometry.Parse("M 2,22 L 22,2"),
                    ShapeType.Circle => Geometry.Parse("M 12,2 A 10,10 0 1 1 12,22 A 10,10 0 1 1 12,2"),
                    ShapeType.Rectangle => Geometry.Parse("M 2,2 L 22,2 L 22,22 L 2,22 Z"),
                    ShapeType.RotatedRectangle => Geometry.Parse("M 12,2 L 22,12 L 12,22 L 2,12 Z"),
                    ShapeType.Polygon => Geometry.Parse("M 12,2 L 20,8 L 18,18 L 6,18 L 4,8 Z"),
                    _ => Geometry.Empty
                };
            }
            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值到可见性转换器
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            PluginLogger.Info($"BoolToVisibilityConverter.Convert - value: {value}, type: {value?.GetType().Name ?? "null"}", "BoolToVisibilityConverter");
            
            if (value is bool b)
            {
                var result = b ? Visibility.Visible : Visibility.Collapsed;
                PluginLogger.Info($"转换结果: {b} -> {result}", "BoolToVisibilityConverter");
                return result;
            }
            
            PluginLogger.Warning($"值不是 bool 类型: {value?.GetType().Name ?? "null"}", "BoolToVisibilityConverter");
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// 模式到布尔值转换器
    /// </summary>
    public class ModeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RoiSourceMode mode && parameter is string targetMode)
            {
                return mode.ToString() == targetMode;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string targetMode)
            {
                return Enum.Parse(typeof(RoiSourceMode), targetMode);
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// 形状类型到布尔值转换器（用于RadioButton绑定）
    /// </summary>
    public class ShapeTypeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ShapeType shapeType && parameter is ShapeType targetType2)
            {
                return shapeType == targetType2;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is ShapeType targetShapeType)
            {
                return targetShapeType;
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// 形状类型到可见性的转换器
    /// </summary>
    /// <remarks>
    /// 性能优化版本：移除所有日志输出，Converter 是基础设施代码不应包含调试日志
    /// </remarks>
    public class ShapeTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 处理 null 值
            if (value == null)
                return Visibility.Collapsed;

            // 获取 ShapeType（处理可空和非可空两种情况）
            ShapeType? shapeType = null;

            if (value is ShapeType nonNullableShapeType)
            {
                shapeType = nonNullableShapeType;
            }
            else if (value is Enum enumValue)
            {
                var valueType = enumValue.GetType();
                if (valueType == typeof(ShapeType) ||
                    (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                     valueType.GetGenericArguments()[0] == typeof(ShapeType)))
                {
                    shapeType = (ShapeType)enumValue;
                }
            }

            // 匹配形状类型
            if (shapeType.HasValue && parameter is string types)
            {
                var typeList = types.Split(',');
                foreach (var type in typeList)
                {
                    if (Enum.TryParse<ShapeType>(type.Trim(), out var t) && shapeType.Value == t)
                    {
                        return Visibility.Visible;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}

