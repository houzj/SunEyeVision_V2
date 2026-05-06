using System;
using System.Globalization;
using System.Windows.Data;
using OpenCvSharp;

namespace SunEyeVision.Tool.Morphology.Converters
{
    /// <summary>
    /// 结构元素形状转换器 - 支持 ComboBox 与 MorphShapes 枚举的中文双向绑定
    /// </summary>
    public class MorphShapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MorphShapes shape)
            {
                return shape switch
                {
                    MorphShapes.Rect => "矩形",
                    MorphShapes.Cross => "十字形",
                    MorphShapes.Ellipse => "椭圆形",
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
                    "矩形" => MorphShapes.Rect,
                    "十字形" => MorphShapes.Cross,
                    "椭圆形" => MorphShapes.Ellipse,
                    _ => MorphShapes.Rect
                };
            }
            return MorphShapes.Rect;
        }
    }
}
