using HalconDotNet;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配的统一几何适配。形状模板的一级轮廓可能由多个 XLD 子对象组成，
/// 因此外接框必须基于全部子对象的点集计算，不能直接消费
/// <c>SmallestRectangle2Xld</c> 对多对象返回的首个元组。
/// </summary>
internal static class ContourMatchContourGeometry
{
    internal readonly record struct Rectangle2(double Row, double Column, double Phi, double Length1, double Length2);

    internal static Rectangle2 SmallestRectangle2Xld(HObject contours, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contours);
        var points = ReadPoints(contours, cancellation);
        if (points.Count < 2)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板轮廓点数不足，无法计算外接矩形。");

        // 把所有 XLD 子对象的点合成一个 HALCON XLD 对象，再计算一次全局最小旋转矩形。
        // 连接不同子轮廓只会增加其端点之间的线段，这些线段位于全部端点的凸包内，
        // 不会扩大包含全部轮廓点所需的最小外接矩形。
        using var rows = new HTuple(points.Select(point => point.Y).ToArray());
        using var columns = new HTuple(points.Select(point => point.X).ToArray());
        HOperatorSet.GenContourPolygonXld(out var aggregate, rows, columns);
        using (aggregate)
        {
            HOperatorSet.SmallestRectangle2Xld(aggregate, out var row, out var column, out var phi,
                out var length1, out var length2);
            using (row) using (column) using (phi) using (length1) using (length2)
            {
                if (row.Length != 1 || column.Length != 1 || phi.Length != 1
                    || length1.Length != 1 || length2.Length != 1
                    || !double.IsFinite(row.D) || !double.IsFinite(column.D)
                    || !double.IsFinite(phi.D) || !double.IsFinite(length1.D)
                    || !double.IsFinite(length2.D) || length1.D <= 0 || length2.D <= 0)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_NATIVE_RESULT", "HALCON 未返回有效的完整轮廓外接矩形。");
                return new Rectangle2(row.D, column.D, phi.D, length1.D, length2.D);
            }
        }
    }

    internal static IReadOnlyList<Point2> ReadPoints(HObject contours, CancellationToken cancellation = default)
    {
        HOperatorSet.CountObj(contours, out var count);
        using (count)
        {
            var points = new List<Point2>(Math.Max(256, count.I * 256));
            for (var index = 1; index <= count.I; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(contours, out var selected, index);
                using (selected)
                {
                    HOperatorSet.GetContourXld(selected, out var rows, out var columns);
                    using (rows) using (columns)
                    {
                        for (var point = 0; point < rows.Length; point++)
                        {
                            if ((point & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                            points.Add(new Point2(columns[point].D, rows[point].D));
                        }
                    }
                }
            }
            return points.AsReadOnly();
        }
    }
}
