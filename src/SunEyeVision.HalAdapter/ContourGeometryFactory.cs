using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// ContourGeometry 工厂方法 - 从 Halcon 原生类型创建轮廓几何
    /// </summary>
    public static class ContourGeometryFactory
    {
        /// <summary>
        /// 从 Halcon HRegion 提取轮廓点集创建 ContourGeometry（整数精度）
        /// </summary>
        public static ContourGeometry FromRegion(HRegion region)
        {
            HOperatorSet.GetRegionContour(region, out var rows, out var cols);
            int ptCount = rows.Length;
            var points = new Point[ptCount];
            for (int i = 0; i < ptCount; i++)
                points[i] = new Point(cols.LArr[i], rows.LArr[i]);
            return new ContourGeometry(points, true);
        }

        /// <summary>
        /// 从 Halcon HRegion 提取亚像素级平滑轮廓（使用 gen_contour_region_xld）
        /// </summary>
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
