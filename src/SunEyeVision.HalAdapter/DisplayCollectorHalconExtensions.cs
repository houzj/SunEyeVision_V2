using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// DisplayCollector Halcon 原生类型扩展方法
    /// </summary>
    public static class DisplayCollectorHalconExtensions
    {
        /// <summary>添加轮廓（Halcon HRegion）</summary>
        public static void AddContour(this DisplayCollector display, GraphicsStyle style, HRegion region, bool isPass = true)
        {
            var contourGeom = ContourGeometryFactory.FromRegion(region);
            display.AddContour(style, contourGeom.Points, isPass);
        }

        /// <summary>批量添加轮廓（Halcon HRegion tuple）</summary>
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
