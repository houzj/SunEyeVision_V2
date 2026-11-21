using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>一次成功抽取发布的度量：总面积与提取区域外接矩形（图像像素坐标）。</summary>
internal readonly record struct ColorExtractionMeasurement(double TotalArea, bool HasRegion,
    double CenterX, double CenterY, double Width, double Height, double AngleDegrees);

internal sealed record ColorExtractionResult(IReadOnlyImage Image, ColorExtractionMeasurement Measurement);

/// <summary>
/// 颜色抽取的 HALCON 实现：按各区间通道上下限取交集得到掩膜，合并全部启用区间后可选按面积下限/上限筛选连通域，
/// 输出域限定在提取区域上的原彩色图像与提取区域外接矩形。
/// 一律使用 <c>HOperatorSet</c> 静态算子，旧对象显式释放（rule-014）。
/// </summary>
internal static class ColorExtraction
{
    internal static ColorExtractionResult Run(HImage frame, IReadOnlyImage source, ImageFrameAddress frameAddress,
        IOutputResources resources, IReadOnlyList<ColorExtractRange> ranges,
        bool areaLimitEnabled, int areaLimitLow, int areaLimitHigh, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(frameAddress);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(ranges);
        cancellation.ThrowIfCancellationRequested();
        HOperatorSet.GetDomain(frame, out var domainObject);
        using (domainObject)
        using (var domain = new HRegion(domainObject))
        using (var mask = BuildMask(frame, domain, ranges, cancellation))
        using (var extracted = ApplyAreaLimit(mask, areaLimitEnabled, areaLimitLow, areaLimitHigh))
        {
            cancellation.ThrowIfCancellationRequested();
            var measurement = Measure(extracted);
            cancellation.ThrowIfCancellationRequested();
            var image = measurement.HasRegion
                ? CaptureRegion(frame, extracted, frameAddress, resources, source.Layout.Format)
                : EmptyDomainImage(source, frameAddress, resources);
            return new(image, measurement);
        }
    }

    private static IReadOnlyImage CaptureRegion(HImage frame, HRegion region, ImageFrameAddress frameAddress,
        IOutputResources resources, PixelLayout layout)
    {
        HOperatorSet.ReduceDomain(frame, region, out var reduced);
        using (reduced)
        using (var reducedImage = new HImage(reduced))
            return HalImageSnapshotSource.Capture(reducedImage, frameAddress, resources,
                rgb24: layout == PixelLayout.Rgb24);
    }

    /// <summary>
    /// 空结果必须发布"无匹配像素"的图像。HALCON 对空掩膜降域会产出无对象图像，
    /// 因此直接以输入像素 + 显式空域构造，避免把整幅输入图像当成提取结果。
    /// </summary>
    private static IReadOnlyImage EmptyDomainImage(IReadOnlyImage source, ImageFrameAddress frameAddress,
        IOutputResources resources)
    {
        var coordinates = new CoordinateFrame("image:" + frameAddress.SourceId, CoordinateDomain.Image, CoordinateUnit.Pixel);
        // 区域由输出资源域持有，CreateImage 会复制行程，无需调用方释放。
        var empty = resources.CreateRegion(coordinates, frameAddress, ReadOnlySpan<RegionRun>.Empty);
        return resources.CreateImage(source.Layout, frameAddress, empty, source.CopyPixels());
    }

    private static HRegion BuildMask(HImage frame, HRegion domain, IReadOnlyList<ColorExtractRange> ranges,
        CancellationToken cancellation)
    {
        HRegion? union = null;
        try
        {
            // 各区间可以独立选择 RGB/HSV/HSI，按色彩空间分组只做一次转换。
            foreach (var group in ranges.Where(range => range.IsChecked).GroupBy(range => range.ColorSpace))
            {
                cancellation.ThrowIfCancellationRequested();
                using var channels = ColorChannels.Create(frame, group.Key);
                foreach (var range in group)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using var selected = SelectChannels(channels, domain, range);
                    if (union is null)
                    {
                        union = selected.Clone();
                        continue;
                    }
                    var merged = Union(union, selected);
                    union.Dispose();
                    union = merged;
                }
            }
            return union ?? EmptyRegion();
        }
        catch
        {
            union?.Dispose();
            throw;
        }
    }

    private static HRegion SelectChannels(ColorChannels channels, HRegion domain, ColorExtractRange range)
    {
        using var channel1 = Threshold(channels[1], range.LowOf(1), range.HighOf(1));
        using var channel2 = Threshold(channels[2], range.LowOf(2), range.HighOf(2));
        using var channel3 = Threshold(channels[3], range.LowOf(3), range.HighOf(3));
        HOperatorSet.Intersection(channel1, channel2, out var pair);
        HRegion result;
        using (pair)
        using (var joined = new HRegion(pair))
        {
            HOperatorSet.Intersection(joined, channel3, out var all);
            using (all) result = new HRegion(all);
        }
        if (!range.IsColorInversion) return result;
        // 反色以输入图像域为基准取补，不使用可能被前序运算缩小的中间域。
        try
        {
            HOperatorSet.Difference(domain, result, out var inverted);
            using (inverted) return new HRegion(inverted);
        }
        finally { result.Dispose(); }
    }

    private static HRegion Threshold(HImage channel, int low, int high)
    {
        HOperatorSet.Threshold(channel, out var selected, low, high);
        using (selected) return new HRegion(selected);
    }

    private static HRegion Union(HRegion left, HRegion right)
    {
        HOperatorSet.Union2(left, right, out var merged);
        using (merged) return new HRegion(merged);
    }

    private static HRegion EmptyRegion()
    {
        HOperatorSet.GenEmptyRegion(out var empty);
        using (empty) return new HRegion(empty);
    }

    /// <summary>原厂 AreaLimitEnable/RangeAreaLimit：按连通域面积筛选，再合并为单一目标域。</summary>
    private static HRegion ApplyAreaLimit(HRegion mask, bool enabled, int low, int high)
    {
        if (!enabled) return mask.Clone();
        HOperatorSet.Connection(mask, out var connected);
        using (connected)
        using (var components = new HRegion(connected))
        {
            HOperatorSet.SelectShape(components, out var selected, "area", "and", low, high);
            using (selected)
            using (var kept = new HRegion(selected))
            {
                HOperatorSet.Union1(kept, out var union);
                using (union) return new HRegion(union);
            }
        }
    }

    private static ColorExtractionMeasurement Measure(HRegion region)
    {
        // 区域行程数直接给出像素总数，空区域无需依赖算子对空输入的行为。
        HOperatorSet.GetRegionRuns(region, out var rows, out var firstColumns, out var lastColumns);
        using (rows) using (firstColumns) using (lastColumns)
        {
            if (rows.Length == 0) return new(0, false, 0, 0, 0, 0, 0);
            double total = 0;
            for (var index = 0; index < rows.Length; index++)
                total += lastColumns[index].I - firstColumns[index].I + 1;
            HOperatorSet.Union1(region, out var merged);
            using (merged)
            using (var single = new HRegion(merged))
            {
                HOperatorSet.SmallestRectangle2(single, out var centerRow, out var centerColumn, out var phi,
                    out var halfLength1, out var halfLength2);
                using (centerRow) using (centerColumn) using (phi) using (halfLength1) using (halfLength2)
                {
                    var empty = halfLength1[0].D == 0 && halfLength2[0].D == 0;
                    // 与平台既有外接矩形约定一致（Blob 检测矩形）：X=列、Y=行、半长×2、角度为屏幕角度。
                    return new(total, true, centerColumn[0].D, centerRow[0].D,
                        2 * halfLength1[0].D, 2 * halfLength2[0].D, empty ? 0 : -phi[0].D * 180 / Math.PI);
                }
            }
        }
    }

    /// <summary>按色彩空间暴露语义通道：1..3 = R,G,B / H,S,V / H,S,I。</summary>
    private sealed class ColorChannels : IDisposable
    {
        private readonly HImage[] _planes;

        private ColorChannels(HImage[] planes) => _planes = planes;

        internal HImage this[int channel] => _planes[channel - 1];

        internal static ColorChannels Create(HImage frame, ColorExtractColorSpace space)
        {
            HOperatorSet.Decompose3(frame, out var channel1, out var channel2, out var channel3);
            using (channel1) using (channel2) using (channel3)
            using (var red = new HImage(channel1))
            using (var green = new HImage(channel2))
            using (var blue = new HImage(channel3))
            {
                // GenImageInterleaved 的 rgb/bgr 只决定输入缓冲的字节顺序，HALCON 通道 1..3 恒为 R,G,B
                // （与 HalImageSnapshotSource 的卷积顺序一致，由本工具 BGR24 用例实测确认）。
                if (space == ColorExtractColorSpace.Rgb)
                    return new(new[] { new HImage(red), new HImage(green), new HImage(blue) });
                var conversion = space == ColorExtractColorSpace.Hsv ? "hsv" : "hsi";
                HOperatorSet.TransFromRgb(red, green, blue, out var first, out var second, out var third, conversion);
                using (first) using (second) using (third)
                    return new(new[] { new HImage(first), new HImage(second), new HImage(third) });
            }
        }

        public void Dispose()
        {
            foreach (var plane in _planes) plane.Dispose();
        }
    }
}
