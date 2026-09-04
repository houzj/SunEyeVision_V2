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
            for (int i = 1; i <= n; i++)
            {
                HOperatorSet.SelectObj(regions, out var singleObj, i);
                using var single = new HRegion(singleObj);
                var isPass = isPassList != null && i - 1 < isPassList.Count ? isPassList[i - 1] : true;
                display.AddContour(style, single, isPass);
            }
        }
    }
}