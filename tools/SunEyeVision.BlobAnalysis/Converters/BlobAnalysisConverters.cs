using System;
using System.Globalization;
using System.Windows.Data;

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
    /// 图像预处理滤波类型转换器 - 支持 ComboBox 与 PreprocessFilterType 枚举的双向绑定
    /// </summary>
    public class PreprocessFilterTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PreprocessFilterType filterType)
            {
                return filterType switch
                {
                    PreprocessFilterType.Gaussian => "高斯滤波",
                    PreprocessFilterType.Median => "中值滤波",
                    PreprocessFilterType.Smooth => "均值滤波",
                    PreprocessFilterType.Emphasize => "锐化",
                    _ => "高斯滤波"
                };
            }
            return "高斯滤波";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "高斯滤波" => PreprocessFilterType.Gaussian,
                    "中值滤波" => PreprocessFilterType.Median,
                    "均值滤波" => PreprocessFilterType.Smooth,
                    "锐化" => PreprocessFilterType.Emphasize,
                    _ => PreprocessFilterType.Gaussian
                };
            }
            return PreprocessFilterType.Gaussian;
        }
    }

    /// <summary>
    /// 形态学操作类型转换器 - 支持 ComboBox 与 MorphOperation 枚举的双向绑定
    /// </summary>
    public class MorphOperationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MorphOperation op)
            {
                return op switch
                {
                    MorphOperation.Erode => "腐蚀",
                    MorphOperation.Dilate => "膨胀",
                    MorphOperation.Open => "开运算",
                    MorphOperation.Close => "闭运算",
                    MorphOperation.Gradient => "梯度",
                    MorphOperation.TopHat => "顶帽",
                    MorphOperation.BlackHat => "黑帽",
                    _ => "开运算"
                };
            }
            return "开运算";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "腐蚀" => MorphOperation.Erode,
                    "膨胀" => MorphOperation.Dilate,
                    "开运算" => MorphOperation.Open,
                    "闭运算" => MorphOperation.Close,
                    "梯度" => MorphOperation.Gradient,
                    "顶帽" => MorphOperation.TopHat,
                    "黑帽" => MorphOperation.BlackHat,
                    _ => MorphOperation.Open
                };
            }
            return MorphOperation.Open;
        }
    }
}
