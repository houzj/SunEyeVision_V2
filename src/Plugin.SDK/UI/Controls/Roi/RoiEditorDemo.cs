using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi
{
    /// <summary>
    /// 区域编辑器使用示例
    /// </summary>
    public static class RoiEditorDemo
    {
        /// <summary>
        /// 创建示例区域数据
        /// </summary>
        public static List<RoiData> CreateSampleRegions()
        {
            var regions = new List<RoiData>();

            // 示例1：绘制模式的矩形
            var rectRegion = RoiData.CreateDrawingRegion("矩形区域1", ShapeType.Rectangle);
            if (rectRegion.Parameters is ShapeParameters rectDef)
            {
                rectDef.Geometry = new RotatedRect2d(200, 150, 100, 80, 0);
            }
            regions.Add(rectRegion);

            // 示例2：绘制模式的圆形
            var circleRegion = RoiData.CreateDrawingRegion("圆形区域1", ShapeType.Circle);
            if (circleRegion.Parameters is ShapeParameters circleDef)
            {
                circleDef.Geometry = new Circle2d(400, 200, 60);
            }
            regions.Add(circleRegion);

            // 示例3：绘制模式的旋转矩形
            var rotatedRectRegion = RoiData.CreateDrawingRegion("旋转矩形1", ShapeType.RotatedRectangle);
            if (rotatedRectRegion.Parameters is ShapeParameters rotatedDef)
            {
                rotatedDef.Geometry = new RotatedRect2d(300, 300, 120, 60, 30);
            }
            regions.Add(rotatedRectRegion);

            // 示例4：绘制模式的直线
            var lineRegion = RoiData.CreateDrawingRegion("直线1", ShapeType.Line);
            if (lineRegion.Parameters is ShapeParameters lineDef)
            {
                lineDef.Geometry = Line2d.FromTwoPoints(
                    new System.Windows.Point(100, 100),
                    new System.Windows.Point(300, 200));
            }
            regions.Add(lineRegion);

            // 示例5：订阅模式的区域
            var subscribedRegion = RoiData.CreateSubscribedRegion("订阅区域1", "node_001", "OutputRegion", 0);
            regions.Add(subscribedRegion);

            // 示例6：计算模式的区域
            var computedRegion = RoiData.CreateComputedRegion("计算区域1", ShapeType.Rectangle);
            if (computedRegion.Parameters is ComputedRoi computedDef)
            {
                computedDef.SetParameterBinding("CenterX", new NodeOutputSource("node_001", "CenterX"));
                computedDef.SetParameterBinding("CenterY", new NodeOutputSource("node_001", "CenterY"));
                computedDef.SetParameterBinding("Width", new ConstantSource(150.0));
                computedDef.SetParameterBinding("Height", new ConstantSource(100.0));
            }
            regions.Add(computedRegion);

            return regions;
        }

        /// <summary>
        /// 演示如何使用区域解析器
        /// </summary>
        public static void DemoResolveRegions()
        {
            var regions = CreateSampleRegions();
            var resolver = new Logic.RoiResolver();

            foreach (var region in regions)
            {
                var resolved = resolver.Resolve(region);
if (resolved.IsValid)
                {
}
                else
                {
}
}
        }
    }
}
