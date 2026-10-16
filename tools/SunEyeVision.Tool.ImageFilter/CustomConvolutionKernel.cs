using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SunEyeVision.Tool.ImageFilter;

/// <summary>Parse configuration only; HALCON performs all image correlation.</summary>
internal sealed record CustomConvolutionKernel(int Width, int Height, double[] Coefficients)
{
    public const string DefaultText = "0 -1 0\n-1 5 -1\n0 -1 0";

    public static CustomConvolutionKernel Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("自定义核不能为空，请按行输入矩形系数");
        // Bound input before splitting/allocating; 31x31 double literals fit comfortably.
        if (text.Length > 65536)
            throw new ArgumentException("自定义核文本过长，最多65536字符");
        var rows = text.Trim().Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace(';', '\n').Split('\n');
        if (rows.Length > ImageFilterParameters.KernelMax)
            throw new ArgumentException($"自定义核高度最多{ImageFilterParameters.KernelMax}行");
        var values = new List<double>();
        int width = 0;
        for (int y = 0; y < rows.Length; y++)
        {
            // Keep empty comma-separated cells invalid instead of silently shifting columns.
            var cells = rows[y].Split(',');
            if (cells.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException($"自定义核第{y + 1}行存在空行或空系数");
            var tokens = cells.SelectMany(cell => cell.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).ToArray();
            if (tokens.Length < 1 || tokens.Length > ImageFilterParameters.KernelMax)
                throw new ArgumentException($"自定义核第{y + 1}行需有1至{ImageFilterParameters.KernelMax}个系数");
            if (y == 0) width = tokens.Length;
            else if (tokens.Length != width)
                throw new ArgumentException($"自定义核第{y + 1}行有{tokens.Length}列，应为{width}列；不会自动补齐或截断");
            for (int x = 0; x < tokens.Length; x++)
            {
                if (!double.TryParse(tokens[x], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    !double.IsFinite(value))
                    throw new ArgumentException($"自定义核第{y + 1}行第{x + 1}列不是有限数值，请使用小数点或科学计数法");
                values.Add(value);
            }
        }
        if (values.All(value => value == 0))
            throw new ArgumentException("自定义核不能全为0，HALCON参考点需要非零核位置");
        return new(width, rows.Length, values.ToArray());
    }
}
