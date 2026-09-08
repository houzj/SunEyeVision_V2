using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

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
            foreach (var roi in rois.Where(r => r.IsEnabled))
            {
                HRegion? single = ResolveSingleRoiRegion(roi, context);

                if (single != null)
                {
                    if (correctionMatrix != null)
                    {
                        var resolved = roi.IsLocal && roi.LocalGeometry?.Geometry != null
                            ? PositionCorrectionResolver.ResolveForShape(roi.LocalGeometry.Geometry, correctionMatrix)
                            : correctionMatrix; // 订阅/拼装模式无显式几何 → 完整矩阵（跟随旋转）
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
        /// 解析单个 Roi 为 HRegion（三态：本地 LocalGeometry / 整对象订阅 Binding / 参数拼装 ParameterBindings）。
        /// </summary>
        private static HRegion? ResolveSingleRoiRegion(Roi roi, IParameterContext? context)
        {
            if (roi.IsLocal)
                return BuildFromRoiShape(roi.LocalGeometry);

            if (roi.IsSubscribed)
            {
                if (context == null)
                    throw new InvalidOperationException(
                        $"无法解析整对象订阅区域: 节点 {roi.Binding!.SourceNodeId}.{roi.Binding.SourceProperty}: 未提供 NodeResults");
                return BuildFromSubscription(roi.Binding!, context);
            }

            if (roi.IsComposed)
            {
                if (context == null)
                    throw new InvalidOperationException(
                        $"无法解析参数拼装区域 '{roi.Name}': 未提供 NodeResults");
                return BuildFromComposed(roi, context);
            }

            return null;
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
            else if (shape.Geometry is System.Windows.Rect rect)
            {
                return BuildHalconRegionFromRect(rect);
            }

            return null;
        }

        /// <summary>
        /// 整对象订阅解析并构建 HRegion：契约路径解析出 ROI 几何（RoiShape[]/RoiShape/几何对象），逐个转区域。
        /// </summary>
        private static HRegion? BuildFromSubscription(ParamSetting binding, IParameterContext context)
        {
            object? value;
            try
            {
                value = context.GetNodeOutputValue(binding.SourceNodeId!, binding.SourceProperty!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"解析整对象订阅区域失败: 节点 {binding.SourceNodeId}.{binding.SourceProperty}: {ex.Message}");
            }

            return BuildFromResolvedValue(value);
        }

        /// <summary>
        /// 把订阅解析值转区域（兼容 RoiShape[] / RoiShape / IVisionRegion / Rect / 几何对象）。
        /// </summary>
        private static HRegion? BuildFromResolvedValue(object? value)
        {
            if (value == null) return null;

            switch (value)
            {
                case RoiShape[] shapes when shapes.Length > 0:
                {
                    HRegion? result = null;
                    foreach (var shape in shapes)
                    {
                        var single = BuildFromRoiShape(shape);
                        if (single == null) continue;
                        result = result == null
                            ? single
                            : Concat(result, single);
                    }
                    return result;
                }
                case RoiShape shape:
                    return BuildFromRoiShape(shape);
                case Rect rect:
                    return BuildHalconRegionFromRect(rect);
                case IVisionRegion region:
                    return BuildHalconRegionFromVisionRegion(region);
                default:
                    return null;
            }
        }

        private static HRegion Concat(HRegion a, HRegion b)
        {
            HOperatorSet.ConcatObj(a, b, out var concatObj);
            a.Dispose();
            b.Dispose();
            return new HRegion(concatObj);
        }

        /// <summary>
        /// 参数拼装解析并构建 HRegion：按 ComposeShapeType 模板 + ParameterBindings 合成几何，再转区域。
        /// </summary>
        private static HRegion? BuildFromComposed(Roi roi, IParameterContext context)
        {
            // 把 IParameterContext 包装为 nodeResults 字典供 RoiComposeResolver 使用（逐参数走输出契约）
            var geometry = ResolveComposedGeometry(roi, context);
            return geometry switch
            {
                RoiShape shape => BuildFromRoiShape(shape),
                _ => null,
            };
        }

        /// <summary>
        /// 通过 OutputVariableResolver 契约解析拼装几何（每次绑定逐键解析）。
        /// 复用 SDK RoiComposeResolver 的纯模板合成 + 绑定解析委托。
        /// </summary>
        private static RoiShape? ResolveComposedGeometry(Roi roi, IParameterContext context)
        {
            // RoiComposeResolver.ResolveComposedGeometry 需要 nodeResults 字典；
            // 此处由 IParameterContext 兜底逐键解析（适配 Execution 契约寻址）。
            var bindings = roi.ParameterBindings;
            if (bindings == null || bindings.Count == 0)
                return null;

            double? Resolve(string key)
            {
                if (!bindings.TryGetValue(key, out var setting) || setting == null)
                    return null;
                object? raw;
                switch (setting.BindingType)
                {
                    case BindingType.Constant:
                        raw = setting.ConstantValue;
                        break;
                    case BindingType.Binding:
                        if (string.IsNullOrEmpty(setting.SourceNodeId) || string.IsNullOrEmpty(setting.SourceProperty))
                            return null;
                        try { raw = context.GetNodeOutputValue(setting.SourceNodeId!, setting.SourceProperty!); }
                        catch { return null; }
                        break;
                    default:
                        return null;
                }
                return raw switch
                {
                    double d => d,
                    int i => i,
                    long l => l,
                    float f => f,
                    string s when double.TryParse(s, out var parsed) => parsed,
                    _ => null,
                };
            }

            var geometry = RoiComposeResolver.ComposeByTemplate(roi.ComposeShapeType, Resolve);
            if (geometry == null)
                return null;

            return new RoiShape { Geometry = geometry };
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
        /// <param name="maskRois">屏蔽几何输入集合（仅支持本地绘制的 Roi；订阅/拼装模式显式报错）</param>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标 → 当前图像坐标），null=不修正</param>
        /// <returns>保留点（屏蔽区外）与被剔除点（屏蔽区内）</returns>
        public static (Point2d[] Kept, Point2d[] Excluded) FilterPointsOutOfMaskRois(
            IEnumerable<Point2d> points,
            IEnumerable<Roi>? maskRois,
            AffineMatrix? correctionMatrix)
        {
            var src = (points ?? Array.Empty<Point2d>()).ToArray();
            var masks = (maskRois ?? Array.Empty<Roi>())
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
        /// 将屏蔽 Roi 解析为几何判定器（位置修正统一在此应用，按形状有向性解析矩阵）。
        /// 仅支持本地绘制模式（订阅/拼装需运行期解析，测量屏蔽暂不支持）。
        /// </summary>
        private static MaskShape ParseMaskShape(Roi mask, AffineMatrix? correctionMatrix)
        {
            var shape = mask.LocalGeometry;
            if (shape == null)
                throw new NotSupportedException(
                    $"屏蔽区域 '{mask.Name}' 仅支持直接绘制模式，当前订阅/拼装模式不被测量工具支持（请在参数中改用直接绘制的屏蔽区域）");

            // 无向形状（轴对齐矩形/圆/圆环）剥转 / 有向形状完整跟随（对齐 ROI 构建与 UI 显示语义）
            var resolvedMatrix = PositionCorrectionResolver.ResolveForShape(shape.Geometry, correctionMatrix);

            double scale = resolvedMatrix?.ScaleFactor() ?? 1.0;
            switch (shape.Geometry)
            {
                case Circle2d c:
                    var cc = resolvedMatrix != null ? resolvedMatrix.TransformWpfPoint(c.Center) : c.Center;
                    return new CircleMaskShape(cc.X, cc.Y, c.Radius * scale);

                case Annulus2d a:
                    if (!a.IsFullCircle)
                        throw new NotSupportedException(
                            $"屏蔽区域 '{mask.Name}' 的圆环必须是完整圆环，部分扇环暂不支持点过滤");
                    var ac = resolvedMatrix != null ? resolvedMatrix.TransformWpfPoint(a.Center) : a.Center;
                    return new AnnulusMaskShape(ac.X, ac.Y, a.InnerRadius * scale, a.OuterRadius * scale);

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

        #region 几何提取

        /// <summary>
        /// 从检测区域集合中提取本地几何定义（RoiShape），填充到 result.InspectionGeometries（ROI 契约）。
        /// 仅提取本地绘制模式的 Roi.LocalGeometry（订阅/拼装区域需运行期解析，不在此输出）。
        /// 输出进契约后，下游可四级绑定（整体 / [Type=X] / [i] / [i].Geometry.属性）。
        /// </summary>
        public static void ExtractInspectionGeometries(ToolResults result,
            IEnumerable<Roi>? rois)
        {
            result.InspectionGeometries = rois?
                .Where(r => r.IsEnabled)
                .Select(r => r.LocalGeometry)
                .Where(g => g?.Geometry != null)
                .ToArray() ?? Array.Empty<RoiShape>();
        }

        /// <summary>
        /// 从屏蔽几何输入集合中提取本地几何定义（RoiShape），填充到 result.MaskGeometries（ROI 契约）。
        /// 仅提取本地绘制模式的 Roi.LocalGeometry（订阅/拼装区域需运行期解析，不在此输出）。
        /// </summary>
        public static void ExtractMaskGeometries(ToolResults result,
            IEnumerable<Roi>? rois)
        {
            result.MaskGeometries = rois?
                .Where(r => r.IsEnabled)
                .Select(r => r.LocalGeometry)
                .Where(g => g?.Geometry != null)
                .ToArray() ?? Array.Empty<RoiShape>();
        }

        #endregion
    }
}
