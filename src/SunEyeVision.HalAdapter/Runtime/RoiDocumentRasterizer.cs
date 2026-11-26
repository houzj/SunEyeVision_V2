using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.HalAdapter;

/// <summary>
/// ROI 文档组到 HRegion 的一次调用栅格化。对应原厂 ROISelecter 的 ROI 集合在算法侧的
/// 区域化：只处理文档数据，不持有会话/编辑器，结果不逃出调用作用域。
/// 由平台统一提供，工具只声明 ROI 文档字段，不各自复制形状几何。
/// </summary>
public static class RoiDocumentRasterizer
{
    /// <summary>整幅矩形（无屏蔽组、单一启用矩形且与图像一致）判定，用于调用方的直接阈值路径。</summary>
    public static bool IsDirectFullFrameRectangle(RecordValue definition, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (RoiDocumentValue.Coverage(definition, RoiDocumentValue.Mask) != RoiDocumentValue.None)
            return false;
        if (RoiDocumentValue.Coverage(definition, RoiDocumentValue.Inspection) == RoiDocumentValue.FullDomain)
            return true;
        var items = RoiDocumentValue.Items(definition, RoiDocumentValue.Inspection);
        if (items.Count != 1 || !items[0].Get<bool>("enabled"))
            return false;
        var shape = items[0].Get<RecordValue>("shape");
        if (shape.Get<string>("kind") != "rectangle")
            return false;
        var center = shape.Get<RecordValue>("center");
        return center.Get<double>("x") == width / 2d && center.Get<double>("y") == height / 2d
            && shape.Get<double>("width") == width && shape.Get<double>("height") == height
            && RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees")) == 0d;
    }

    /// <summary>按组栅格化；调用方负责先校验文档结构。</summary>
    public static HRegion Build(RecordValue definition, string group, HRegion inputDomain,
        CancellationToken cancellation, bool clipToInputDomain = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentNullException.ThrowIfNull(inputDomain);
        return clipToInputDomain
            ? BuildCore(definition, group, inputDomain, cancellation, true)
            : HalconResourceBridge.CreateWithoutImplicitClipping(
                () => BuildCore(definition, group, inputDomain, cancellation, false));
    }

    private static HRegion BuildCore(RecordValue definition, string group, HRegion inputDomain,
        CancellationToken cancellation, bool clipToInputDomain)
    {
        var coverage = RoiDocumentValue.Coverage(definition, group);
        if (coverage == RoiDocumentValue.FullDomain) return inputDomain.Clone();
        HOperatorSet.GenEmptyRegion(out var empty);
        var union = new HRegion(empty);
        empty.Dispose();
        try
        {
            if (coverage == RoiDocumentValue.None) return union.Clone();
            foreach (var item in RoiDocumentValue.Items(definition, group))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!item.Get<bool>("enabled")) continue;
                using var region = Shape(item.Get<RecordValue>("shape"));
                using var next = union.Union2(region);
                var replacement = next.Clone();
                union.Dispose();
                union = replacement;
            }
            return clipToInputDomain ? union.Intersection(inputDomain) : union.Clone();
        }
        finally
        {
            union.Dispose();
        }
    }

    private static HRegion Shape(RecordValue shape)
    {
        var kind = shape.Get<string>("kind");
        if (kind == "polygon")
        {
            var points = shape.Get<SequenceValue>("vertices").Cast<RecordValue>().ToArray();
            using var rows = new HTuple(points.Select(point => point.Get<double>("y")).ToArray());
            using var columns = new HTuple(points.Select(point => point.Get<double>("x")).ToArray());
            HOperatorSet.GenRegionPolygonFilled(out var polygon, rows, columns);
            using (polygon) return new HRegion(polygon);
        }
        var center = shape.Get<RecordValue>("center");
        var y = center.Get<double>("y");
        var x = center.Get<double>("x");
        if (kind == "rectangle")
        {
            HOperatorSet.GenRectangle2(out var rectangle, y, x,
                -RoiDocumentValue.NormalizeDegrees(shape.Get<double>("angle-degrees")) * Math.PI / 180,
                shape.Get<double>("width") / 2, shape.Get<double>("height") / 2);
            using (rectangle) return new HRegion(rectangle);
        }
        var start = shape.Get<double>("start-radians");
        var end = shape.Get<double>("end-radians");
        var sweep = end - start;
        // A negative screen sweep occupies [end,start]. HALCON's positive mathematical interval reverses screen Y.
        if (sweep < 0) (start, end) = (end, start);
        using var outer = Math.Abs(Math.Abs(sweep) - 2 * Math.PI) <= 1e-9
            ? new HRegion(y, x, shape.Get<double>("outer-radius"))
            : new HRegion(y, x, shape.Get<double>("outer-radius"), Normalize(-end), Normalize(-start));
        var innerRadius = shape.Get<double>("inner-radius");
        if (innerRadius == 0) return outer.Clone();
        using var inner = new HRegion(y, x, innerRadius);
        return outer.Difference(inner);
    }

    private static double Normalize(double value) => (value % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
}
