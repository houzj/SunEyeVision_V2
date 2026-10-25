using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Tool.BlobAnalysis;

namespace SunEyeVision.Tool.BlobAnalysis.Converters
{
    /// <summary>
    /// 阈值类型转换器 - 支持 ComboBox 与 ThresholdType 枚举的双向绑定
    /// </summary>
    public class ThresholdTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThresholdType thresholdType)
            {
                return thresholdType.ToString();
            }
            return "Binary";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "Binary" => ThresholdType.Binary,
                    "BinaryInv" => ThresholdType.BinaryInv,
                    _ => ThresholdType.Binary
                };
            }
            return ThresholdType.Binary;
        }
    }

    /// <summary>
    /// Blob排序方式转换器 - 支持 ComboBox 与 BlobSortMode 枚举的双向绑定
    /// </summary>
    public class BlobSortModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BlobSortMode mode)
            {
                return mode switch
                {
                    BlobSortMode.None => "不排序",
                    BlobSortMode.AreaAsc => "面积 ↑",
                    BlobSortMode.AreaDesc => "面积 ↓",
                    BlobSortMode.CircularityAsc => "圆度 ↑",
                    BlobSortMode.CircularityDesc => "圆度 ↓",
                    BlobSortMode.RectangularityAsc => "矩形度 ↑",
                    BlobSortMode.RectangularityDesc => "矩形度 ↓",
                    BlobSortMode.XAsc => "X坐标 ↑",
                    BlobSortMode.XDesc => "X坐标 ↓",
                    BlobSortMode.YAsc => "Y坐标 ↑",
                    BlobSortMode.YDesc => "Y坐标 ↓",
                    _ => "不排序"
                };
            }
            return "不排序";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "不排序" => BlobSortMode.None,
                    "面积 ↑" => BlobSortMode.AreaAsc,
                    "面积 ↓" => BlobSortMode.AreaDesc,
                    "圆度 ↑" => BlobSortMode.CircularityAsc,
                    "圆度 ↓" => BlobSortMode.CircularityDesc,
                    "矩形度 ↑" => BlobSortMode.RectangularityAsc,
                    "矩形度 ↓" => BlobSortMode.RectangularityDesc,
                    "X坐标 ↑" => BlobSortMode.XAsc,
                    "X坐标 ↓" => BlobSortMode.XDesc,
                    "Y坐标 ↑" => BlobSortMode.YAsc,
                    "Y坐标 ↓" => BlobSortMode.YDesc,
                    _ => BlobSortMode.None
                };
            }
            return BlobSortMode.None;
        }
    }

    /// <summary>
    /// 预处理方法转换器 - 支持 ComboBox 与 PreprocessMethod 枚举的双向绑定
    /// </summary>
    public class PreprocessMethodConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PreprocessMethod method)
            {
                return method switch
                {
                    PreprocessMethod.None => "无",
                    PreprocessMethod.Mean => "均值滤波",
                    PreprocessMethod.Gaussian => "高斯滤波",
                    PreprocessMethod.Median => "中值滤波",
                    PreprocessMethod.HistogramEqualization => "直方图均衡化",
                    _ => "无"
                };
            }
            return "无";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "无" => PreprocessMethod.None,
                    "均值滤波" => PreprocessMethod.Mean,
                    "高斯滤波" => PreprocessMethod.Gaussian,
                    "中值滤波" => PreprocessMethod.Median,
                    "直方图均衡化" => PreprocessMethod.HistogramEqualization,
                    _ => PreprocessMethod.None
                };
            }
            return PreprocessMethod.None;
        }
    }

    /// <summary>
    /// 形态学操作转换器 - 支持 ComboBox 与 MorphOperationType 枚举的双向绑定
    /// </summary>
    public class MorphOperationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MorphOperationType op)
            {
                return op switch
                {
                    MorphOperationType.Erosion => "腐蚀",
                    MorphOperationType.Dilation => "膨胀",
                    MorphOperationType.Opening => "开运算",
                    MorphOperationType.Closing => "闭运算",
                    MorphOperationType.TopHat => "顶帽",
                    MorphOperationType.BottomHat => "黑帽",
                    _ => "腐蚀"
                };
            }
            return "腐蚀";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "腐蚀" => MorphOperationType.Erosion,
                    "膨胀" => MorphOperationType.Dilation,
                    "开运算" => MorphOperationType.Opening,
                    "闭运算" => MorphOperationType.Closing,
                    "顶帽" => MorphOperationType.TopHat,
                    "黑帽" => MorphOperationType.BottomHat,
                    _ => MorphOperationType.Erosion
                };
            }
            return MorphOperationType.Erosion;
        }
    }

    /// <summary>
    /// 结构元素形状转换器 - 支持 ComboBox 与 MorphElementShapeType 枚举的双向绑定
    /// </summary>
    public class MorphElementShapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MorphElementShapeType shape)
            {
                return shape switch
                {
                    MorphElementShapeType.Rectangle => "矩形",
                    MorphElementShapeType.Circle => "圆形",
                    _ => "矩形"
                };
            }
            return "矩形";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "矩形" => MorphElementShapeType.Rectangle,
                    "圆形" => MorphElementShapeType.Circle,
                    _ => MorphElementShapeType.Rectangle
                };
            }
            return MorphElementShapeType.Rectangle;
        }
    }
}
