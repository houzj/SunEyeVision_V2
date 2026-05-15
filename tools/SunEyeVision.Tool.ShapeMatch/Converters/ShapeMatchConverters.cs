using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.CvEngine;
using SunEyeVision.Tool.ShapeMatch.Models;

namespace SunEyeVision.Tool.ShapeMatch.Converters
{
    /// <summary>
    /// SortMode枚举到显示文本的转换
    /// </summary>
    [ValueConversion(typeof(SortMode), typeof(string))]
    public class SortModeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SortMode mode)
            {
                return mode switch
                {
                    SortMode.None => "无排序",
                    SortMode.ScoreDescending => "按分数降序",
                    SortMode.AngleDescending => "按角度降序",
                    SortMode.XAscending => "按X由小到大",
                    SortMode.YAscending => "按Y由小到大",
                    SortMode.XThenYAscending => "X由小到大, Y由小到大",
                    SortMode.YThenXAscending => "Y由小到大, X由小到大",
                    _ => mode.ToString()
                };
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text switch
                {
                    "无排序" => SortMode.None,
                    "按分数降序" => SortMode.ScoreDescending,
                    "按角度降序" => SortMode.AngleDescending,
                    "按X由小到大" => SortMode.XAscending,
                    "按Y由小到大" => SortMode.YAscending,
                    "X由小到大, Y由小到大" => SortMode.XThenYAscending,
                    "Y由小到大, X由小到大" => SortMode.YThenXAscending,
                    _ => Enum.Parse(typeof(SortMode), text)
                };
            }
            return SortMode.None;
        }
    }

    /// <summary>
    /// MultiTemplateMode枚举到显示文本的转换
    /// </summary>
    [ValueConversion(typeof(MultiTemplateMode), typeof(string))]
    public class MultiTemplateModeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MultiTemplateMode mode)
            {
                return mode switch
                {
                    MultiTemplateMode.Sequential => "顺序执行",
                    MultiTemplateMode.ParallelIndependent => "并行独立",
                    MultiTemplateMode.ParallelShared => "并行共享",
                    _ => mode.ToString()
                };
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
                return Enum.Parse(typeof(MultiTemplateMode), text);
            return MultiTemplateMode.ParallelShared;
        }
    }

    /// <summary>
    /// TemplateExtractionStatus枚举到显示文本的转换
    /// </summary>
    [ValueConversion(typeof(TemplateExtractionStatus), typeof(string))]
    public class ExtractionStatusConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TemplateExtractionStatus status)
            {
                return status switch
                {
                    TemplateExtractionStatus.NotExtracted => "未提取",
                    TemplateExtractionStatus.Success => "✓ 已提取",
                    TemplateExtractionStatus.Warning => "⚠ 警告",
                    TemplateExtractionStatus.Failed => "✗ 失败",
                    _ => status.ToString()
                };
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// MinContrastMode枚举到显示文本的转换
    /// </summary>
    [ValueConversion(typeof(MinContrastMode), typeof(string))]
    public class MinContrastModeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MinContrastMode mode)
            {
                return mode switch
                {
                    MinContrastMode.Auto => "自动",
                    MinContrastMode.Manual => "手动",
                    MinContrastMode.FromTemplate => "模版阈值",
                    _ => mode.ToString()
                };
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text switch
                {
                    "自动" => MinContrastMode.Auto,
                    "手动" => MinContrastMode.Manual,
                    "模版阈值" => MinContrastMode.FromTemplate,
                    _ => Enum.Parse(typeof(MinContrastMode), text)
                };
            }
            return MinContrastMode.Auto;
        }
    }
}
