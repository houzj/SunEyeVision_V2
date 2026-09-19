using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.HalAdapter
{
    public static class DisplayCollectorHalconExtensions
    {
        public static void AddContour(this DisplayCollector display, GraphicsStyle style, HRegion region, bool isPass = true)
        {
            var contourGeom = ContourGeometryFactory.FromRegion(region);
            display.AddContour(style, contourGeom.Points, isPass);
        }

        public static void AddContours(this DisplayCollector display, GraphicsStyle style, HRegion regions, IReadOnlyList<bool>? isPassList = null)
        {
            HOperatorSet.CountObj(regions, out var count);
            int n = (int)count.L;
            var okContours = new List<System.Windows.Point[]>();
            var ngContours = new List<System.Windows.Point[]>();
            for (int i = 1; i <= n; i++)
            {
                HOperatorSet.SelectObj(regions, out var singleObj, i);
                using var single = new HRegion(singleObj);
                var isPass = isPassList != null && i - 1 < isPassList.Count ? isPassList[i - 1] : true;
                var contour = ContourGeometryFactory.FromRegion(single);
                if (contour.Points.Length < 2) continue;
                if (isPass)
                    okContours.Add(contour.Points);
                else
                    ngContours.Add(contour.Points);
            }

            display.AddContourBatch(style, okContours, true);
            display.AddContourBatch(style, ngContours, false);
        }
    }
}
