using HalconDotNet;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Tool.BlobAnalysis.Runtime;
public sealed partial class BlobSession
{
        private static HRegion SelectRegions(HRegion source, IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return EmptyRegion();
            using var tuple = new HTuple(indices.ToArray());
            HOperatorSet.SelectObj(source, out var selected, tuple);
            using (selected) return new HRegion(selected);
        }

        private static HRegion EmptyRegion()
        {
            HOperatorSet.GenEmptyRegion(out var empty);
            using (empty) return new HRegion(empty);
        }

        private static Point2[] BuildRotatedRectangle(double row, double col, double phi, double length1, double length2)
        {
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            // HALCON rectangle2 phi -> screen coordinates (X=column, Y=row).
            // Its first axis is (cos(phi), -sin(phi)), not (cos(phi), sin(phi)).
            var axis = new Point2(cos, -sin);
            var normal = new Point2(sin, cos);
            return new[]
            {
                new Point2(col - axis.X * length1 - normal.X * length2, row - axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 - normal.X * length2, row + axis.Y * length1 - normal.Y * length2),
                new Point2(col + axis.X * length1 + normal.X * length2, row + axis.Y * length1 + normal.Y * length2),
                new Point2(col - axis.X * length1 + normal.X * length2, row - axis.Y * length1 + normal.Y * length2)
            };
        }
}
