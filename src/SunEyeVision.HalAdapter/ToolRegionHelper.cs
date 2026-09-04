using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.HalAdapter
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
        private static HRegion BuildHalconRotatedRectFromParams(double cx, double cy, double w, double h, double angle)
        {
            double phi = -angle * Math.PI / 180.0;
            HOperatorSet.GenRectangle2(out var rect, cy, cx, phi, w / 2.0, h / 2.0);
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
        /// 从 RoiData 集合构建 HRegion（保持 tuple 结构，支持 Drawing/Subscribe 模式）
        /// </summary>
        public static HRegion BuildHalconRegion(
            IEnumerable<RoiData> regions,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;
            return BuildHalconRegion(regions, context);
        }

        /// <summary>
        /// 从 RoiData 集合构建 HRegion（内部方法，接受 IParameterContext）
        /// </summary>
        private static HRegion BuildHalconRegion(
            IEnumerable<RoiData> regions,
            IParameterContext? context)
        {
            HRegion? result = null;
            foreach (var region in regions.Where(r => r.IsEnabled))
            {
                HRegion? single = region.Parameters switch
                {
                    ShapeParameters shape => BuildFromShapeParams(shape),
                    FixedRoi fixedDef when context != null => BuildFromFixedRegion(fixedDef, context),
                    ComputedRoi computedDef when context != null => BuildFromComputedRegion(computedDef, context),
                    FixedRoi fixedDef => throw new InvalidOperationException(
                        $"无法解析固定区域订阅: 节点 {fixedDef.SourceNodeId}.{fixedDef.OutputName}: 未提供 NodeResults"),
                    ComputedRoi computedDef => throw new InvalidOperationException(
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
            if (shape.Geometry == null) return null;

            if (shape.Geometry is Circle2d c)
            {
                return new HRegion(c.Center.Y, c.Center.X, c.Radius);
            }
            else if (shape.Geometry is RotatedRect2d rr)
            {
                return BuildHalconRotatedRectFromParams(rr.CenterX, rr.CenterY, rr.Width, rr.Height, rr.Angle);
            }
            else if (shape.Geometry is Polygon2d poly)
            {
                return BuildHalconPolygonFromPoints(poly.Vertices);
            }
            else if (shape.Geometry is System.Windows.Rect rect)
            {
                return BuildHalconRegionFromRect(rect);
            }

            return null;
        }

        /// <summary>
        /// 从 FixedRoi 解析并构建 HRegion
        /// </summary>
        private static HRegion? BuildFromFixedRegion(FixedRoi fixedDef, IParameterContext context)
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
        /// 从 ComputedRoi 解析并构建 HRegion
        /// </summary>
        private static HRegion? BuildFromComputedRegion(ComputedRoi computedDef, IParameterContext context)
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

        /// <summary>
        /// 从多边形点集构建 HRegion（SDK Point[] 重载）
        /// </summary>
        private static HRegion BuildHalconPolygonFromPoints(System.Windows.Point[] points)
        {
            if (points.Length < 3) return new HRegion();
            var rows = new HTuple();
            var cols = new HTuple();
            for (int i = 0; i < points.Length; i++)
            {
                rows = rows.TupleConcat(points[i].Y);
                cols = cols.TupleConcat(points[i].X);
            }
            // 闭合多边形
            rows = rows.TupleConcat(points[0].Y);
            cols = cols.TupleConcat(points[0].X);
            var region = new HRegion();
            region.GenRegionPolygon(rows, cols);
            HOperatorSet.FillUp(region, out var filled);
            region.Dispose();
            return new HRegion(filled);
        }

        /// <summary>
        /// 从多边形点集构建 HRegion（UI Point2D 重载）
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
            IEnumerable<RoiData> inspectionRegions,
            IEnumerable<RoiData> maskRegions,
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

        #region 屏蔽点集过滤（测量工具：屏蔽区在采样点进入拟合前生效，纯几何判定）

        /// <summary>
        /// 测量点屏蔽区空间过滤：落在任意启用的屏蔽区域内的点被剔除（不参与后续拟合）。
        /// 纯 C# 几何判定（无 Halcon 引擎依赖），供圆查找/直线查找等在拟合前剔除屏蔽区内的坏点。
        /// </summary>
        /// <param name="points">测量点集（图像坐标，X=列、Y=行）</param>
        /// <param name="maskRegions">屏蔽区域集合（仅支持直接绘制的 ShapeParameters；绑定/计算模式显式报错）</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标 → 当前图像坐标），null=不修正</param>
        /// <returns>保留点（屏蔽区外）与被剔除点（屏蔽区内）</returns>
        public static (Point2d[] Kept, Point2d[] Excluded) FilterPointsOutOfMaskRegions(
            IEnumerable<Point2d> points,
            IEnumerable<RoiData>? maskRegions,
            AffineMatrix? correctionMatrix)
        {
            var src = (points ?? Array.Empty<Point2d>()).ToArray();
            var masks = (maskRegions ?? Array.Empty<RoiData>())
                .Where(r => r.IsEnabled)
                .ToArray();
            if (masks.Length == 0)
                return (src, Array.Empty<Point2d>());

            var shapes = masks.Select(m => ParseMaskShape(m, correctionMatrix)).ToArray();
            var kept = new List<Point2d>(src.Length);
            var excluded = new List<Point2d>(Math.Min(src.Length, 32));

            foreach (var p in src)
            {
                if (shapes.Any(s => s.Contains(p.X, p.Y)))
                    excluded.Add(p);
                else
                    kept.Add(p);
            }

            return (kept.ToArray(), excluded.ToArray());
        }

        /// <summary>
        /// 将屏蔽 RoiData 解析为几何判定器（位置修正统一在此应用）。
        /// </summary>
        private static MaskShape ParseMaskShape(RoiData mask, AffineMatrix? correctionMatrix)
        {
            if (mask.Parameters is not ShapeParameters shape)
                throw new NotSupportedException(
                    $"屏蔽区域 '{mask.Name}' 仅支持直接绘制模式，当前绑定/计算模式不被测量工具支持（请在参数中改用直接绘制的屏蔽区域）");

            double scale = correctionMatrix?.ScaleFactor() ?? 1.0;
            switch (shape.Geometry)
            {
                case Circle2d c:
                    var cc = correctionMatrix != null ? correctionMatrix.TransformWpfPoint(c.Center) : c.Center;
                    return new CircleMaskShape(cc.X, cc.Y, c.Radius * scale);

                case Annulus2d a:
                    if (!a.IsFullCircle)
                        throw new NotSupportedException(
                            $"屏蔽区域 '{mask.Name}' 的圆环必须是完整圆环，部分扇环暂不支持点过滤");
                    var ac = correctionMatrix != null ? correctionMatrix.TransformWpfPoint(a.Center) : a.Center;
                    return new AnnulusMaskShape(ac.X, ac.Y, a.InnerRadius * scale, a.OuterRadius * scale);

                case RotatedRect2d rr:
                    return new PolygonMaskShape(TransformPoints(rr.GetCorners(), correctionMatrix));

                case Polygon2d poly:
                    return new PolygonMaskShape(TransformPoints(poly.Vertices, correctionMatrix));

                case Rect rect:
                    return new PolygonMaskShape(TransformPoints(
                        new[] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft },
                        correctionMatrix));

                default:
                    throw new NotSupportedException(
                        $"屏蔽区域 '{mask.Name}' 的几何类型 {shape.Geometry?.GetType().Name ?? "null"} 不支持用于测量屏蔽" +
                        "（支持：圆/圆环/旋转矩形/多边形/矩形）");
            }
        }

        private static Point[] TransformPoints(IEnumerable<Point> points, AffineMatrix? matrix)
        {
            if (matrix == null)
                return points.ToArray();
            return points.Select(matrix.TransformWpfPoint).ToArray();
        }

        /// <summary>
        /// 几何掩膜判定器（屏蔽形状对单点做包含测试）。
        /// </summary>
        private abstract class MaskShape
        {
            public abstract bool Contains(double x, double y);
        }

        private sealed class CircleMaskShape : MaskShape
        {
            private readonly double _cx;
            private readonly double _cy;
            private readonly double _r2;

            public CircleMaskShape(double cx, double cy, double radius)
            {
                _cx = cx;
                _cy = cy;
                _r2 = radius * radius;
            }

            public override bool Contains(double x, double y)
            {
                var dx = x - _cx;
                var dy = y - _cy;
                return dx * dx + dy * dy <= _r2;
            }
        }

        private sealed class AnnulusMaskShape : MaskShape
        {
            private readonly double _cx;
            private readonly double _cy;
            private readonly double _innerR2;
            private readonly double _outerR2;

            public AnnulusMaskShape(double cx, double cy, double innerRadius, double outerRadius)
            {
                _cx = cx;
                _cy = cy;
                _innerR2 = innerRadius * innerRadius;
                _outerR2 = outerRadius * outerRadius;
            }

            public override bool Contains(double x, double y)
            {
                var dx = x - _cx;
                var dy = y - _cy;
                var d2 = dx * dx + dy * dy;
                return d2 >= _innerR2 && d2 <= _outerR2;
            }
        }

        private sealed class PolygonMaskShape : MaskShape
        {
            private readonly Point[] _polygon;

            public PolygonMaskShape(Point[] polygon) => _polygon = polygon;

            public override bool Contains(double x, double y)
            {
                if (_polygon.Length < 3)
                    return false;

                bool inside = false;
                for (int i = 0, j = _polygon.Length - 1; i < _polygon.Length; j = i++)
                {
                    var xi = _polygon[i].X;
                    var yi = _polygon[i].Y;
                    var xj = _polygon[j].X;
                    var yj = _polygon[j].Y;

                    if ((yi > y) != (yj > y) &&
                        x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        inside = !inside;
                    }
                }
                return inside;
            }
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

        #region 几何提取

        /// <summary>
        /// 从检测区域集合中提取 ShapeParameters（RoI），填充到 result.InspectionGeometries。
        /// 仅提取 Drawing 模式的 ShapeParameters（Fixed/Computed 模式的区域需运行期解析）。
        /// 输出进契约后，下游可四级绑定（整体 / [Type=X] / [i] / [i].Geometry.属性）。
        /// </summary>
        public static void ExtractInspectionGeometries(ToolResults result,
            IEnumerable<RoiData>? regions)
        {
            result.InspectionGeometries = regions?
                .Where(r => r.IsEnabled)
                .Select(r => r.Parameters)
                .OfType<ShapeParameters>()
                .Where(sp => sp.Geometry != null)
                .ToArray() ?? Array.Empty<ShapeParameters>();
        }

        /// <summary>
        /// 从屏蔽区域集合中提取 ShapeParameters（RoI），填充到 result.MaskGeometries。
        /// 仅提取 Drawing 模式的 ShapeParameters（Fixed/Computed 模式的区域需运行期解析）。
        /// </summary>
        public static void ExtractMaskGeometries(ToolResults result,
            IEnumerable<RoiData>? regions)
        {
            result.MaskGeometries = regions?
                .Where(r => r.IsEnabled)
                .Select(r => r.Parameters)
                .OfType<ShapeParameters>()
                .Where(sp => sp.Geometry != null)
                .ToArray() ?? Array.Empty<ShapeParameters>();
        }

        #endregion
    }
}
