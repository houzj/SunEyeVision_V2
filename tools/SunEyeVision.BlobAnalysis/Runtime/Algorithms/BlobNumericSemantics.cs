namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

internal static class BlobNumericSemantics
{
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
