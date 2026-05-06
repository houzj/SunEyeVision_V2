using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.Morphology.Converters
{
    /// <summary>
    /// 形态学操作类型转换器 - 支持 ComboBox 与 MorphOperation 枚举的中文双向绑定
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
            if (value is string opStr)
            {
                return opStr switch
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
