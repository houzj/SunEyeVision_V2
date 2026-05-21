using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public static class ContourGeometryFactory
    {
        public static ContourGeometry FromRegion(HRegion region)
        {
            HOperatorSet.GetRegionContour(region, out var rows, out var cols);
            int ptCount = rows.Length;
            var points = new Point[ptCount];
            for (int i = 0; i < ptCount; i++)
                points[i] = new Point(cols.LArr[i], rows.LArr[i]);
            return new ContourGeometry(points, true);
        }

        public static ContourGeometry FromRegionSmooth(HRegion region)
        {
            HOperatorSet.GenContourRegionXld(region, out var xld, "border");
            HOperatorSet.GetContourXld(xld, out var rows, out var cols);
            xld.Dispose();
            int ptCount = rows.Length;
            var points = new Point[ptCount];
            for (int i = 0; i < ptCount; i++)
                points[i] = new Point(cols.DArr[i], rows.DArr[i]);
            return new ContourGeometry(points, true);
        }
    }
}