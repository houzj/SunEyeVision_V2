using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers
{
    /// <summary>
    /// 工具区域处理辅助类 - 消除各工具中重复的 ROI 构建、位置修正等代码
    /// </summary>
    public static class ToolRegionHelper
    {
        #region Halcon 系列

        /// <summary>
        /// 从 RegionData 集合构建 HRegion（保持 tuple 结构，支持多边形）
        /// </summary>
        public static HRegion BuildHalconRegion(IEnumerable<RegionData> regions)
        {
            HRegion? result = null;
            foreach (var region in regions.Where(r => r.IsEnabled))
            {
                if (region.Parameters is not ShapeParameters shape) continue;

                HRegion? single = shape.ShapeType switch
                {
                    ShapeType.Rectangle => new HRegion(
                        shape.CenterY - shape.Height / 2,
                        shape.CenterX - shape.Width / 2,
                        shape.CenterY + shape.Height / 2,
                        shape.CenterX + shape.Width / 2),

                    ShapeType.Circle => new HRegion(
                        shape.CenterY, shape.CenterX, shape.Radius),

                    ShapeType.Polygon when shape.Points.Count >= 3
                        => BuildHalconPolygonRegion(shape.Points),

                    ShapeType.RotatedRectangle => new HRegion(
                        shape.CenterY - shape.Height / 2,
                        shape.CenterX - shape.Width / 2,
                        shape.CenterY + shape.Height / 2,
                        shape.CenterX + shape.Width / 2),

                    _ => null
                };

                if (single != null)
                {
                    if (result == null)
                        result = single;
                    else
                    {
                        HOperatorSet.ConcatObj(result, single, out var concatObj);
                        result.Dispose();
                        single.Dispose();
                        result = new HRegion(concatObj);
                    }
                }
            }

            return result ?? new HRegion();
        }

        /// <summary>
        /// 从多边形点集构建 HRegion
        /// </summary>
        private static HRegion BuildHalconPolygonRegion(IList<Point2D> points)
        {
            var rows = new HTuple();
            var cols = new HTuple();
            for (int i = 0; i < points.Count; i++)
            {
                rows = rows.TupleConcat(points[i].Y);
                cols = cols.TupleConcat(points[i].X);
            }
            var region = new HRegion();
            region.GenRegionPolygon(rows, cols);
            return region;
        }

        /// <summary>
        /// 构建 Halcon ROI = 检测区域 - 屏蔽区域
        /// </summary>
        /// <returns>(roiRegion 用于处理, inspHRegion 用于显示轮廓, maskHRegion 用于显示轮廓)</returns>
        public static (HRegion? Roi, HRegion? Insp, HRegion? Mask) BuildHalconROI(
            int imgWidth, int imgHeight,
            IEnumerable<RegionData> inspectionRegions,
            IEnumerable<RegionData> maskRegions)
        {
            HRegion? roiRegion = null;
            HRegion? inspHRegion = null;
            HRegion? maskHRegion = null;

            bool hasInspection = inspectionRegions.Any();
            bool hasMask = maskRegions.Any();

            if (!hasInspection && !hasMask)
                return (null, null, null);

            // 构建检测区域（未设置时 = 全图）
            if (hasInspection)
            {
                inspHRegion = BuildHalconRegion(inspectionRegions); // 保持 tuple 供独立轮廓渲染
                HOperatorSet.Union1(inspHRegion, out var unionRegion); // 合并为单一处理域
                roiRegion = new HRegion(unionRegion);
            }
            else
            {
                roiRegion = new HRegion(0.0, 0.0, (double)imgHeight - 1, (double)imgWidth - 1);
            }

            if (hasMask)
            {
                using var diffTarget = roiRegion;
                maskHRegion = BuildHalconRegion(maskRegions); // 保持 tuple 供独立轮廓渲染
                HOperatorSet.Union1(maskHRegion, out var maskUnion); // 合并后再做差集
                HOperatorSet.Difference(diffTarget, maskUnion, out var diffObj);
                maskUnion.Dispose();
                roiRegion = new HRegion(diffObj);
            }

            return (roiRegion, inspHRegion, maskHRegion);
        }

        /// <summary>
        /// 对 Halcon ROI 应用位置修正（正向变换）
        /// </summary>
        /// <returns>(变换后的 roiRegion, 变换后的 inspHRegion, 变换后的 maskHRegion)</returns>
        public static (HRegion? Roi, HRegion? Insp, HRegion? Mask) ApplyPositionCorrectionHalcon(
            HRegion? roiRegion, HRegion? inspHRegion, HRegion? maskHRegion,
            AffineMatrix matrix, int imgWidth, int imgHeight)
        {
            var halconArr = matrix.ToHalconArray();
            var homMat2D = new HTuple(halconArr);

            if (inspHRegion != null)
            {
                HOperatorSet.AffineTransRegion(inspHRegion, out var transInsp,
                    homMat2D, "nearest_neighbor");
                inspHRegion.Dispose();
                inspHRegion = new HRegion(transInsp);
            }

            if (maskHRegion != null)
            {
                HOperatorSet.AffineTransRegion(maskHRegion, out var transMask,
                    homMat2D, "nearest_neighbor");
                maskHRegion.Dispose();
                maskHRegion = new HRegion(transMask);
            }

            // 重新构建 roiRegion = insp - mask
            if (roiRegion != null)
            {
                roiRegion.Dispose();
                if (inspHRegion != null && maskHRegion != null)
                {
                    HOperatorSet.Union1(inspHRegion, out var inspUnion);
                    HOperatorSet.Union1(maskHRegion, out var maskUnion);
                    HOperatorSet.Difference(inspUnion, maskUnion, out var diffObj);
                    inspUnion.Dispose();
                    maskUnion.Dispose();
                    roiRegion = new HRegion(diffObj);
                }
                else if (inspHRegion != null)
                {
                    roiRegion = new HRegion(inspHRegion);
                }
                else
                {
                    roiRegion = new HRegion(0.0, 0.0, (double)imgHeight - 1, (double)imgWidth - 1);
                }
            }

            return (roiRegion, inspHRegion, maskHRegion);
        }

        #endregion
    }
}
