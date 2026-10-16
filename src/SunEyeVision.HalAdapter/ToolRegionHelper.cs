using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// 工具区域处理辅助类 - 消除各工具中重复的 ROI 构建、位置修正等代码。
    /// 消费 Execution 域 <see cref="Roi"/> 三态模型（本地绘制 / 整对象订阅 / 参数拼装），
    /// 取代历史 RoiData/ShapeParameters/FixedRoi/ComputedRoi 旧模型。
    /// </summary>
    public static partial class ToolRegionHelper
    {
        #region Halcon 系列

        /// <summary>
        /// 从 ShapeParameters 构建 Halcon 旋转矩形区域
        /// 角度转换：Angle(屏幕角度系,顺时针+,0°向右,度数) → Halcon Phi(数学,逆时针+,0°向右,弧度)
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
        /// 从 Roi 集合构建 HRegion（保持 tuple 结构，支持本地绘制/整对象订阅/参数拼装三态）。
        /// </summary>
        public static HRegion BuildHalconRegion(
            IEnumerable<Roi> rois,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;
            return BuildHalconRegion(rois, context, correctionMatrix: null);
        }

        /// <summary>
        /// 从 Roi 集合构建 HRegion（内部方法，接受 IParameterContext；可带位置修正矩阵）。
        /// 位置修正按"形状有向性"逐 Roi 应用（对齐 UI 显示语义）：
        ///   - 本地绘制的无向形状（轴对齐矩形/圆/圆环）→ 剥转矩阵（平移+均匀缩放）；
        ///   - 有向形状（旋转矩形/多边形/直线）与订阅/拼装模式 → 完整矩阵（跟随旋转）。
        /// </summary>
        private static HRegion BuildHalconRegion(
            IEnumerable<Roi> rois,
            IParameterContext? context,
            AffineMatrix? correctionMatrix)
        {
            HRegion? result = null;
            // ★ 组级运行时出口：订阅/拼装组内本地 Roi 不参与构建处理域（按模式过滤，根治双区处理）
            foreach (var roi in RoiCollection.GetRuntimeActiveInputs(rois))
            {
                HRegion? single = ResolveSingleRoiRegion(roi, context);

                if (single != null)
                {
                    // ★ 位置修正只作用于本地绘制 Roi（参考系几何 → 当前系）；
                    //   订阅/拼装 Roi 的几何来自上游输出契约，已含上游坐标/修正，再套本节点矩阵=二次修正。
                    if (correctionMatrix != null && roi.IsLocal)
                    {
                        var resolved = roi.LocalGeometry?.Geometry != null
                            ? PositionCorrectionResolver.ResolveForShape(roi.LocalGeometry.Geometry, correctionMatrix)
                            : correctionMatrix;
                        if (resolved != null)
                        {
                            HOperatorSet.AffineTransRegion(single, out var trans,
                                new HTuple(resolved.ToHalconArray()), "nearest_neighbor");
                            single.Dispose();
                            single = new HRegion(trans);
                        }
                    }

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
        /// 解析单个 Roi 为 HRegion：先经 SDK 统一出口 <see cref="Roi.ResolveRuntime"/> 归一三态
        /// （本地/整对象订阅/参数拼装）得到 RoiShape[]，再逐个转 HRegion（本类只做"几何→区域"）。
        /// </summary>
        private static HRegion? ResolveSingleRoiRegion(Roi roi, IParameterContext? context)
        {
            var shapes = roi.ResolveRuntime(context);
            HRegion? result = null;
            foreach (var shape in shapes)
            {
                var single = BuildFromRoiShape(shape);
                if (single == null) continue;
                result = result == null ? single : Concat(result, single);
            }
            return result;
        }

        /// <summary>
        /// 从本地几何（RoiShape）构建 HRegion
        /// </summary>
        private static HRegion? BuildFromRoiShape(RoiShape? shape)
        {
            if (shape?.Geometry == null) return null;

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
            else if (shape.Geometry is Annulus2d annulus)
            {
                ValidateAnnulus(annulus);
                // Screen clockwise interval [start,end] maps to mathematical [-end,-start].
                // Full circles must use GenCircle: GenCircleSector(0,2pi) is empty in HALCON.
                using var outer = annulus.IsFullCircle
                    ? new HRegion(annulus.Center.Y, annulus.Center.X, annulus.OuterRadius)
                    : new HRegion(annulus.Center.Y, annulus.Center.X, annulus.OuterRadius,
                        NormalizeSectorAngle(-annulus.EndAngle), NormalizeSectorAngle(-annulus.StartAngle));
                if (annulus.InnerRadius <= 0) return outer.Clone();
                using var inner = new HRegion(annulus.Center.Y, annulus.Center.X, annulus.InnerRadius);
                return outer.Difference(inner);
            }
            // P0-2 封口（完全删除 WPF Rect）：几何值恒为 Models.Geometry 契约类型，
            // 轴对齐矩形即 RotatedRect2d(Angle=0)，故此处不再有 Rect 分支。

            throw new NotSupportedException($"几何类型 {shape.Geometry.GetType().Name} 不支持HALCON处理域，不能静默忽略该区域");
        }

        private static double NormalizeSectorAngle(double angle)
            => (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);

        private static void ValidateAnnulus(Annulus2d annulus)
        {
            if (!double.IsFinite(annulus.Center.X) || !double.IsFinite(annulus.Center.Y)
                || !double.IsFinite(annulus.InnerRadius) || !double.IsFinite(annulus.OuterRadius)
                || !double.IsFinite(annulus.StartAngle) || !double.IsFinite(annulus.EndAngle)
                || !double.IsFinite(annulus.AngularSpan)
                || annulus.InnerRadius < 0 || annulus.IsEmpty || annulus.AngularSpan <= 0)
                throw new ArgumentException("圆环需要有限圆心、0≤内半径<外半径及正向连续角度区间");
        }

        private static HRegion Concat(HRegion a, HRegion b)
        {
            HOperatorSet.ConcatObj(a, b, out var concatObj);
            a.Dispose();
            b.Dispose();
            return new HRegion(concatObj);
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
        /// 构建 Halcon ROI = 检测区域 - 屏蔽区域（支持订阅/拼装模式）
        /// </summary>
        /// <returns>(roiRegion 用于处理, inspHRegion 用于显示轮廓, maskHRegion 用于显示轮廓)</returns>
        public static (HRegion? Roi, HRegion? Insp, HRegion? Mask) BuildHalconROI(
            int imgWidth, int imgHeight,
            IEnumerable<Roi> inspectionRois,
            IEnumerable<Roi> maskRois,
            IDictionary<string, ToolResults>? nodeResults = null,
            AffineMatrix? correctionMatrix = null)
        {
            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;

            HRegion? roiRegion = null;
            HRegion? inspHRegion = null;
            HRegion? maskHRegion = null;

            bool hasInspection = inspectionRois.Any();
            bool hasMask = maskRois.Any();

            if (!hasInspection && !hasMask)
                return (null, null, null);

            // 构建检测区域（未设置时 = 全图）；位置修正按形状有向性在逐区域构建时应用
            if (hasInspection)
            {
                inspHRegion = BuildHalconRegion(inspectionRois, context, correctionMatrix); // 保持 tuple 供独立轮廓渲染

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
                maskHRegion = BuildHalconRegion(maskRois, context, correctionMatrix); // 保持 tuple 供独立轮廓渲染
                HOperatorSet.Union1(maskHRegion, out var maskUnion); // 合并后再做差集
                HOperatorSet.Difference(diffTarget, maskUnion, out var diffObj);
                maskUnion.Dispose();
                roiRegion = new HRegion(diffObj);
            }

            return (roiRegion, inspHRegion, maskHRegion);
        }

        #endregion

        #region 屏蔽点集过滤（测量工具：屏蔽区在采样点进入拟合前生效，纯几何判定）

        /// <summary>
        /// 测量点屏蔽区空间过滤：落在任意启用的屏蔽几何输入内的点被剔除（不参与后续拟合）。
        /// 纯 C# 几何判定（无 Halcon 引擎依赖），供圆查找/直线查找等在拟合前剔除屏蔽区内的坏点。
        /// </summary>
        /// <param name="points">测量点集（图像坐标，X=列、Y=行）</param>
        /// <param name="maskRois">屏蔽几何输入集合，统一解析本地绘制、订阅和参数拼装</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标 → 当前图像坐标），null=不修正</param>
        /// <returns>保留点（屏蔽区外）与被剔除点（屏蔽区内）</returns>
        public static (Point2d[] Kept, Point2d[] Excluded) FilterPointsOutOfMaskRois(
            IEnumerable<Point2d> points,
            IEnumerable<Roi>? maskRois,
            AffineMatrix? correctionMatrix)
            => FilterPointsOutOfMaskRois(points, maskRois, correctionMatrix, null);

        /// <summary>Runtime-context overload; keeps the original three-argument binary API available.</summary>
        public static (Point2d[] Kept, Point2d[] Excluded) FilterPointsOutOfMaskRois(
            IEnumerable<Point2d> points,
            IEnumerable<Roi>? maskRois,
            AffineMatrix? correctionMatrix,
            IDictionary<string, ToolResults>? nodeResults)
        {
            var src = (points ?? Array.Empty<Point2d>()).ToArray();
            // Preserve subpixel containment and the same runtime geometry as output/preview.
            var shapes = ExtractRuntimeShapes(maskRois, nodeResults, correctionMatrix)
                .Select(shape => ParseMaskShape(shape, correctionMatrix: null)).ToArray();
            if (shapes.Length == 0) return (src, Array.Empty<Point2d>());
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
        /// 将屏蔽 Roi 解析为几何判定器（位置修正统一在此应用，按形状有向性解析矩阵）。
        /// 几何已由运行时出口归一；本地修正一次，订阅/拼装不重复修正。
        /// </summary>
        private static MaskShape ParseMaskShape(RoiShape shape, AffineMatrix? correctionMatrix)
        {

            // 无向形状（轴对齐矩形/圆/圆环）剥转 / 有向形状完整跟随（对齐 ROI 构建与 UI 显示语义）
            var resolvedMatrix = PositionCorrectionResolver.ResolveForShape(shape.Geometry, correctionMatrix);

            double scale = resolvedMatrix?.ScaleFactor() ?? 1.0;
            switch (shape.Geometry)
            {
                case Circle2d c:
                    var cc = resolvedMatrix != null ? resolvedMatrix.TransformWpfPoint(c.Center) : c.Center;
                    return new CircleMaskShape(cc.X, cc.Y, c.Radius * scale);

                case Annulus2d a:
                    ValidateAnnulus(a);
                    var ac = resolvedMatrix != null ? resolvedMatrix.TransformWpfPoint(a.Center) : a.Center;
                    return new AnnulusMaskShape(new Annulus2d(ac, a.InnerRadius * scale,
                        a.OuterRadius * scale, a.StartAngle, a.EndAngle));

                case RotatedRect2d rr:
                    return new PolygonMaskShape(TransformPoints(rr.GetCorners(), resolvedMatrix));

                case Polygon2d poly:
                    return new PolygonMaskShape(TransformPoints(poly.Vertices, resolvedMatrix));

                case Rect rect:
                    return new PolygonMaskShape(TransformPoints(
                        new[] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft },
                        resolvedMatrix));

                default:
                    throw new NotSupportedException(
                        $"屏蔽几何类型 {shape.Geometry?.GetType().Name ?? "null"} 不支持用于测量屏蔽" +
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
            private readonly Annulus2d _annulus;
            public AnnulusMaskShape(Annulus2d annulus) => _annulus = annulus;
            public override bool Contains(double x, double y) => _annulus.Contains(new Point(x, y));
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

        #region 几何提取

        /// <summary>
        /// 从检测区域集合提取实际几何（RoiShape[]），填充 result.InspectionGeometries（ROI 契约）。
        /// 统一经 SDK 出口 <see cref="Roi.ResolveRuntime"/>：本地/整对象订阅/参数拼装三态归一；
        /// 输出进契约后，下游可四级绑定（整体 / [Type=X] / [i] / [i].Geometry.属性）。
        /// </summary>
        /// <param name="result">工具结果</param>
        /// <param name="rois">检测几何输入集合</param>
        /// <param name="nodeResults">上游节点执行结果（订阅/拼装区域解析用；null=仅本地绘制可解析，订阅区域将报错，rule-015）</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考系 → 当前图像系）。非 null 时输出契约几何为修正后坐标，
        /// 使下游订阅到的区域与上游运行/显示位置一致；null 输出参考系原始几何。</param>
        public static void ExtractInspectionGeometries(ToolResults result,
            IEnumerable<Roi>? rois,
            IDictionary<string, ToolResults>? nodeResults = null,
            AffineMatrix? correctionMatrix = null)
        {
            result.InspectionGeometries = ExtractRuntimeShapes(rois, nodeResults, correctionMatrix);
        }

        /// <summary>
        /// 从屏蔽几何输入集合提取实际几何（RoiShape[]），填充 result.MaskGeometries（ROI 契约）。
        /// 统一经 SDK 出口 <see cref="Roi.ResolveRuntime"/>（语义同检测区）。
        /// </summary>
        public static void ExtractMaskGeometries(ToolResults result,
            IEnumerable<Roi>? rois,
            IDictionary<string, ToolResults>? nodeResults = null,
            AffineMatrix? correctionMatrix = null)
        {
            result.MaskGeometries = ExtractRuntimeShapes(rois, nodeResults, correctionMatrix);
        }

        /// <summary>对启用几何输入统一调用 SDK 出口，收集运行时 RoiShape[]；带位置修正矩阵时输出修正后坐标。</summary>
        private static RoiShape[] ExtractRuntimeShapes(
            IEnumerable<Roi>? rois,
            IDictionary<string, ToolResults>? nodeResults,
            AffineMatrix? correctionMatrix = null)
        {
            if (rois == null) return Array.Empty<RoiShape>();

            var context = nodeResults != null ? new NodeResultsParameterContext(nodeResults) : null;
            var shapes = new List<RoiShape>();
            // ★ 组级运行时出口：订阅/拼装组内本地 Roi 不参与契约输出（按模式过滤，与执行/结果显示一致）
            foreach (var roi in RoiCollection.GetRuntimeActiveInputs(rois))
            {
                foreach (var shape in roi.ResolveRuntime(context))
                {
                    // 契约几何：本地绘制 = 修正后坐标（参考系 → 当前图像系），使下游订阅区与源显示一致；
                    // ★ 订阅/拼装 Roi 解析出的几何已含上游坐标，不再套本节点矩阵（防二次修正）。
                    shapes.Add(roi.IsLocal
                        ? RoiGeometryTransformer.Transform(shape, correctionMatrix) ?? shape
                        : shape);
                }
            }
            return shapes.ToArray();
        }

        #endregion
    }
}
