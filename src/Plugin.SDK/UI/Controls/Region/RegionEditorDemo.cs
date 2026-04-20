using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region
{
    /// <summary>
    /// 区域编辑器使用示例
    /// </summary>
    public static class RegionEditorDemo
    {
        /// <summary>
        /// 创建示例区域数据
        /// </summary>
        public static List<RegionData> CreateSampleRegions()
        {
            var regions = new List<RegionData>();

            // 示例1：绘制模式的矩形
            var rectRegion = RegionData.CreateDrawingRegion("矩形区域1", ShapeType.Rectangle);
            if (rectRegion.Parameters is ShapeParameters rectDef)
            {
                rectDef.CenterX = 200;
                rectDef.CenterY = 150;
                rectDef.Width = 100;
                rectDef.Height = 80;
            }
            regions.Add(rectRegion);

            // 示例2：绘制模式的圆形
            var circleRegion = RegionData.CreateDrawingRegion("圆形区域1", ShapeType.Circle);
            if (circleRegion.Parameters is ShapeParameters circleDef)
            {
                circleDef.CenterX = 400;
                circleDef.CenterY = 200;
                circleDef.Radius = 60;
            }
            regions.Add(circleRegion);

            // 示例3：绘制模式的旋转矩形
            var rotatedRectRegion = RegionData.CreateDrawingRegion("旋转矩形1", ShapeType.RotatedRectangle);
            if (rotatedRectRegion.Parameters is ShapeParameters rotatedDef)
            {
                rotatedDef.CenterX = 300;
                rotatedDef.CenterY = 300;
                rotatedDef.Width = 120;
                rotatedDef.Height = 60;
                rotatedDef.Angle = 30;
            }
            regions.Add(rotatedRectRegion);

            // 示例4：绘制模式的直线
            var lineRegion = RegionData.CreateDrawingRegion("直线1", ShapeType.Line);
            if (lineRegion.Parameters is ShapeParameters lineDef)
            {
                lineDef.StartX = 100;
                lineDef.StartY = 100;
                lineDef.EndX = 300;
                lineDef.EndY = 200;
            }
            regions.Add(lineRegion);

            // 示例5：订阅模式的区域
            var subscribedRegion = RegionData.CreateSubscribedRegion("订阅区域1", "node_001", "OutputRegion", 0);
            regions.Add(subscribedRegion);

            // 示例6：计算模式的区域
            var computedRegion = RegionData.CreateComputedRegion("计算区域1", ShapeType.Rectangle);
            if (computedRegion.Parameters is ComputedRegion computedDef)
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
            var resolver = new Logic.RegionResolver();

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
