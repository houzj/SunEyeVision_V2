using System;
using System.Linq;
using HalconDotNet;

namespace SunEyeVision.HalAdapter;

/// <summary>Finite filter masks passed to HALCON's native correlation operator; no pixel filtering in C#.</summary>
public static class HalconLinearFilters
{
    public static HImage Apply(HImage image, int width, int height, double sigmaX, double sigmaY,
        bool gaussian, int border)
    {
        if (width < 1 || height < 1 || width % 2 == 0 || height % 2 == 0)
            throw new ArgumentException("滤波核宽高必须为正奇数");
        if (!double.IsFinite(sigmaX) || !double.IsFinite(sigmaY) || sigmaX < 0 || sigmaY < 0)
            throw new ArgumentException("Sigma必须为有限非负数；0表示按对应核尺寸自动计算");
        // Explicit finite support: +/-3 sigma when auto, with a minimum 0.5 pixel sigma.
        double sx = sigmaX == 0 ? Math.Max(0.5, (width - 1) / 6.0) : sigmaX;
        double sy = sigmaY == 0 ? Math.Max(0.5, (height - 1) / 6.0) : sigmaY;
        var weights = new double[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double dx = x - width / 2, dy = y - height / 2;
                weights[y * width + x] = gaussian
                    ? Math.Exp(-0.5 * (dx * dx / (sx * sx) + dy * dy / (sy * sy))) : 1.0;
            }
        return Correlate(image, width, height, weights, border);
    }

    public static HImage Sharpen(HImage image, int border)
        => Correlate(image, 3, 3, new double[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 }, border);

    /// <summary>Pass an explicit row-major mask and divisor to HALCON, without auto-normalization.</summary>
    public static HImage ApplyKernel(HImage image, int width, int height, double[] weights, double divisor, int border)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (width < 1 || height < 1 || (long)width * height != weights.Length)
            throw new ArgumentException("自定义核尺寸与系数数量不一致");
        if (!double.IsFinite(divisor) || divisor <= 0 || weights.Any(value => !double.IsFinite(value)) ||
            weights.All(value => value == 0))
            throw new ArgumentException("自定义核需有限系数、至少一个非零位置和有限正除数");
        return Correlate(image, width, height, weights, border, divisor);
    }

    private static HImage Correlate(HImage image, int width, int height, double[] weights, int border,
        double? divisor = null)
    {
        // HALCON tuple layout is [rows, columns, inverse weight, coefficients...], not coefficients alone.
        using var mask = new HTuple(new object[] { height, width, divisor ?? weights.Sum() }.Concat(weights.Cast<object>()).ToArray());
        using var margin = border switch
        {
            0 => new HTuple("mirrored"),
            1 => new HTuple("continued"),
            2 => new HTuple(0),
            4 => new HTuple("cyclic"),
            _ => throw new NotSupportedException("当前HALCON卷积未提供Reflect101/Isolated的等价映射，请显式选择Reflect、Replicate、Constant或Wrap")
        };
        string sourceType = image.GetImageType();
        if (sourceType is not ("byte" or "int1" or "int2" or "uint2" or "int4" or "real"))
            throw new NotSupportedException($"卷积不支持灰度类型 {sourceType}，未自动压缩为8位");
        if (sourceType == "int4")
        {
            using var domain = image.GetDomain();
            HOperatorSet.MinMaxGray(domain, image, 0, out var min, out var max, out var range);
            using (min) using (max) using (range)
            using (var low = min.TupleReal()) using (var high = max.TupleReal())
                if (low.D < -16777216 || high.D > 16777216)
                    throw new NotSupportedException("int4灰度超过real精确整数范围，不能无提示损失精度；请明确上游数据范围");
        }
        // Real-valued coefficients are evaluated in a supported real working image.
        // No normalization to 0..255: restore the original gray type after native correlation.
        using var working = image.ConvertImageType("real");
        try
        {
            HOperatorSet.ConvolImage(working, out var filtered, mask, margin);
            using var result = new HImage(filtered);
            return result.ConvertImageType(sourceType);
        }
        catch (HOperatorException ex)
        {
            throw new InvalidOperationException($"HALCON卷积失败：输入类型={sourceType}，计算类型=real，核={width}x{height}；{ex.Message}", ex);
        }
    }
}
