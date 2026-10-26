using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.Morphology.Converters
{
    /// <summary>
    /// 结构元素形状转换器 - 支持 ComboBox 与 MorphKernelShape 枚举的中文双向绑定
    /// </summary>
    public class MorphShapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MorphKernelShape shape)
            {
                return shape switch
                {
                    MorphKernelShape.Rect => "矩形",
                    MorphKernelShape.Cross => "十字形",
                    MorphKernelShape.Ellipse => "椭圆形",
                    MorphKernelShape.Circle => "圆形",
                    _ => "矩形"
                };
            }
            return "矩形";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string shapeStr)
            {
                return shapeStr switch
                {
                    "矩形" => MorphKernelShape.Rect,
                    "十字形" => MorphKernelShape.Cross,
                    "椭圆形" => MorphKernelShape.Ellipse,
                    "圆形" => MorphKernelShape.Circle,
                    _ => MorphKernelShape.Rect
                };
            }
            return MorphKernelShape.Rect;
        }
    }
}
