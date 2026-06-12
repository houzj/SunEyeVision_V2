using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers
{
    /// <summary>
    /// 工具区域处理辅助类 - 消除各工具中重复的 ROI 构建、位置修正等代码
    /// </summary>
    public static partial class ToolRegionHelper
    {
        #region Halcon 系列

        /// <summary>
        /// 从 ShapeParameters 构建 Halcon 旋转矩形区域
        /// 角度转换：ShapeParameters.Angle(屏幕角度系,顺时针+,0°向右,度数) → Halcon Phi(数学,逆时针+,0°向右,弧度)
        /// 公式：phi = -Angle * π / 180（屏幕角度系顺时针为正 → Halcon 逆时针为正，取负号转换）
        /// </summary>
        private static HRegion BuildHalconRotatedRectangle(ShapeParameters shape)
        {
            double phi = -shape.Angle * Math.PI / 180.0;
            HOperatorSet.GenRectangle2(out var rect,
                shape.CenterY, shape.CenterX,
                phi,
                shape.Width / 2.0,
                shape.Height / 2.0);
            return new HRegion(rect);
        }

        /// <summary>
        /// 从 WpfRect 构建 HRegion（轴对齐矩形）
        /// </summary>
        private static HRegion BuildHalconRegionFromRect(Rect rect)
        {
            return new HRegion(
                rect.Top, rect.Left,
                rect.Bottom - 1, rect.Right - 1);
        }

        /// <summary>
        /// 从 IVisionRegion 解包为 HRegion（直接使用底层 HRegion）
        /// 非 HalRegion 类型时取其外接矩形
        /// </summary>
        private static HRegion BuildHalconRegionFromVisionRegion(IVisionRegion visionRegion)
        {
            if (visionRegion is HalRegion halRegion)
                return halRegion.Unwrap().Clone();

            // 非 HalRegion 回退到外接矩形
            var bbox = visionRegion.BoundingBox;
            return BuildHalconRegionFromRect(bbox);
        }

        /// <summary>
        /// 从 RegionData 集合构建 HRegion（保持 tuple 结构，支持 Drawing/Subscribe 模式）
        /// </summary>
        public static HRegion BuildHalconRegion(
            IEnumerable<RegionData> regions,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;
            return BuildHalconRegion(regions, context);
        }

        /// <summary>
        /// 从 RegionData 集合构建 HRegion（内部方法，接受 IParameterContext）
        /// </summary>
        private static HRegion BuildHalconRegion(
            IEnumerable<RegionData> regions,
            IParameterContext? context)
        {
            HRegion? result = null;
            foreach (var region in regions.Where(r => r.IsEnabled))
            {
                HRegion? single = region.Parameters switch
                {
                    ShapeParameters shape => BuildFromShapeParams(shape),
                    FixedRegion fixedDef when context != null => BuildFromFixedRegion(fixedDef, context),
                    ComputedRegion computedDef when context != null => BuildFromComputedRegion(computedDef, context),
                    FixedRegion fixedDef => throw new InvalidOperationException(
                        $"无法解析固定区域订阅: 节点 {fixedDef.SourceNodeId}.{fixedDef.OutputName}: 未提供 NodeResults"),
                    ComputedRegion computedDef => throw new InvalidOperationException(
                        $"无法解析计算区域: 未提供 NodeResults"),
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
        /// 从 ShapeParameters 构建 HRegion
        /// </summary>
        private static HRegion? BuildFromShapeParams(ShapeParameters shape)
        {
            return shape.ShapeType switch
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

                ShapeType.RotatedRectangle => BuildHalconRotatedRectangle(shape),

                _ => null
            };
        }

        /// <summary>
        /// 从 FixedRegion 解析并构建 HRegion
        /// </summary>
        private static HRegion? BuildFromFixedRegion(FixedRegion fixedDef, IParameterContext context)
        {
            var value = context.GetNodeOutputValue(
                fixedDef.SourceNodeId,
                fixedDef.OutputName,
                fixedDef.RegionIndex);

            if (value == null)
                return null;

            return value switch
            {
                Rect rect => BuildHalconRegionFromRect(rect),
                IVisionRegion region => BuildHalconRegionFromVisionRegion(region),
                _ => null
            };
        }

        /// <summary>
        /// 从 ComputedRegion 解析并构建 HRegion
        /// </summary>
        private static HRegion? BuildFromComputedRegion(ComputedRegion computedDef, IParameterContext context)
        {
            double ResolveParam(string name, double defaultValue)
            {
                var source = computedDef.GetParameterBinding(name);
                if (source == null) return defaultValue;
                var val = source.GetValue(context);
                if (val == null) return defaultValue;
                try { return Convert.ToDouble(val); }
                catch { return defaultValue; }
            }

            var centerX = ResolveParam("CenterX", 0.0);
            var centerY = ResolveParam("CenterY", 0.0);
            var width = ResolveParam("Width", 100.0);
            var height = ResolveParam("Height", 100.0);
            var angle = ResolveParam("Angle", 0.0);
            var radius = ResolveParam("Radius", 50.0);

            return computedDef.TargetShapeType switch
            {
                ShapeType.Rectangle => new HRegion(
                    centerY - height / 2,
                    centerX - width / 2,
                    centerY + height / 2,
                    centerX + width / 2),

                ShapeType.Circle => new HRegion(centerY, centerX, radius),

                ShapeType.RotatedRectangle => BuildHalconRotatedRectFromParams(centerX, centerY, width, height, angle),

                _ => null
            };
        }

        private static HRegion BuildHalconRotatedRectFromParams(double cx, double cy, double w, double h, double angle)
        {
            double phi = -angle * Math.PI / 180.0;
            HOperatorSet.GenRectangle2(out var rect, cy, cx, phi, w / 2.0, h / 2.0);
            return new HRegion(rect);
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
            // 闭合多边形：GenRegionPolygon 不会自动闭合首尾顶点
            if (points.Count >= 3)
            {
                rows = rows.TupleConcat(points[0].Y);
                cols = cols.TupleConcat(points[0].X);
            }
            var region = new HRegion();
            region.GenRegionPolygon(rows, cols);

            HOperatorSet.FillUp(region, out var filled);
            region.Dispose();
            region = new HRegion(filled);

            return region;
        }

        /// <summary>
        /// 构建 Halcon ROI = 检测区域 - 屏蔽区域（支持订阅模式）
        /// </summary>
        /// <returns>(roiRegion 用于处理, inspHRegion 用于显示轮廓, maskHRegion 用于显示轮廓)</returns>
        public static (HRegion? Roi, HRegion? Insp, HRegion? Mask) BuildHalconROI(
            int imgWidth, int imgHeight,
            IEnumerable<RegionData> inspectionRegions,
            IEnumerable<RegionData> maskRegions,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;

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
                inspHRegion = BuildHalconRegion(inspectionRegions, context); // 保持 tuple 供独立轮廓渲染

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
                maskHRegion = BuildHalconRegion(maskRegions, context); // 保持 tuple 供独立轮廓渲染
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
                    HOperatorSet.Union1(inspHRegion, out var inspUnion);
                    roiRegion = new HRegion(inspUnion);
                    inspUnion.Dispose();
                }
                else
                {
                    roiRegion = new HRegion(0.0, 0.0, (double)imgHeight - 1, (double)imgWidth - 1);
                }
            }

            return (roiRegion, inspHRegion, maskHRegion);
        }

        #endregion

        #region NodeResults 参数上下文适配器

        /// <summary>
        /// IParameterContext 适配器 - 将 IDictionary&lt;string, ToolResults&gt; 适配为参数上下文
        /// </summary>
        private sealed class NodeResultsParameterContext : IParameterContext
        {
            private readonly IDictionary<string, ToolResults> _nodeResults;

            public NodeResultsParameterContext(IDictionary<string, ToolResults> nodeResults)
            {
                _nodeResults = nodeResults ?? throw new ArgumentNullException(nameof(nodeResults));
            }

            public object? GetNodeOutputValue(string nodeId, string outputName, int? index = null, string? propertyPath = null)
            {
                if (!_nodeResults.TryGetValue(nodeId, out var result))
                    return null;

                // 先尝试直接属性访问
                var property = result.GetType().GetProperty(outputName);
                if (property == null)
                    return null;

                var value = property.GetValue(result);

                // 处理数组索引
                if (index.HasValue && value is Array array)
                {
                    if (index.Value >= 0 && index.Value < array.Length)
                        return array.GetValue(index.Value);
                    return null;
                }

                // 属性路径访问
                if (!string.IsNullOrEmpty(propertyPath) && value != null)
                {
                    return GetNestedPropertyValue(value, propertyPath);
                }

                return value;
            }

            private static object? GetNestedPropertyValue(object obj, string path)
            {
                object? current = obj;
                foreach (var part in path.Split('.'))
                {
                    if (current == null) return null;
                    var prop = current.GetType().GetProperty(part);
                    if (prop == null) return null;
                    current = prop.GetValue(current);
                }
                return current;
            }
        }

        #endregion
    }
}
