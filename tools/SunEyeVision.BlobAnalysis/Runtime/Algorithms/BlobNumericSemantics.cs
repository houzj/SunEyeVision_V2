namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class BlobNumericSemantics
{
    // C03 axis getters report the full projected pixel-center span plus one pixel.
    internal static float LongAxis(BlobOrientedBounds bounds) => (float)(Math.Max(bounds.Width, bounds.Height) + 1d);
    internal static float ShortAxis(BlobOrientedBounds bounds) => (float)(Math.Min(bounds.Width, bounds.Height) + 1d);

    internal static bool PassSingleLimit(double value, double minimum, double maximum)
    {
        const float machineEpsilon = 1.1920928955078125e-7f;
        var measured = (float)value;
        var low = (float)minimum;
        var high = (float)maximum;
        return float.IsFinite(measured) && float.IsFinite(low) && float.IsFinite(high)
            && measured >= low - machineEpsilon && measured <= high + machineEpsilon;
    }

    internal static bool PassDoubleLimit(double value, double minimum, double maximum)
    {
        const double machineEpsilon = 2.220446049250313e-16;
        return double.IsFinite(value) && double.IsFinite(minimum) && double.IsFinite(maximum)
            && value >= minimum - machineEpsilon && value <= maximum + machineEpsilon;
    }

    /// <summary>
    /// VM 对结果数组按顺序使用单精度累加总面积，再把结果发布给托管层。
    /// </summary>
    internal static double Float32Sum(IEnumerable<double> values)
    {
        float total = 0f;
        foreach (var value in values)
            total += (float)value;
        return total;
    }
}
