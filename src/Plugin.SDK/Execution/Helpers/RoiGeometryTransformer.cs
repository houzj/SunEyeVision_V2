using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers
{
    /// <summary>
    /// ROI 几何按位置修正矩阵变换（参考系几何 → 当前图像坐标系几何）。
    /// 语义与 PositionCorrectionResolver 完全一致：
    ///   - 无向形状（轴对齐矩形/圆/圆环）→ 剥转矩阵（中心完整修正 + 尺寸等比缩放，不旋转）；
    ///   - 有向形状（旋转矩形/多边形/直线/弧/点）→ 完整矩阵（跟随旋转，角度/方向随矩阵旋转）。
    /// 用于把"输出契约几何（如 InspectionGeometries/MaskGeometries）"从参考系变换到当前系，
    /// 使下游订阅到的区域与上游运行/显示所见一致。
    /// </summary>
    public static class RoiGeometryTransformer
    {
        /// <summary>
        /// 若矩阵为空返回原几何；否则按形状有向性解析矩阵并变换为新几何对象。
        /// 无法识别的几何类型原样返回（不抛错，避免下游解析崩）。
        /// </summary>
        public static object? TransformGeometry(object? geometry, AffineMatrix? fullMatrix)
        {
            if (geometry == null || fullMatrix == null) return geometry;

            // 形状有向性解析：无向剥转（中心完整 + 不旋转）/ 有向完整（跟随旋转）
            var matrix = PositionCorrectionResolver.ResolveForShape(geometry, fullMatrix);
            if (matrix == null) return geometry;

            double s = matrix.ScaleFactor();
            Point T(Point p) => matrix.TransformWpfPoint(p);

            switch (geometry)
            {
                case Circle2d c:
                    return new Circle2d(T(c.Center), c.Radius * s);

                case Annulus2d a:
                    return new Annulus2d(T(a.Center), a.InnerRadius * s, a.OuterRadius * s, a.StartAngle, a.EndAngle);

                case RotatedRect2d rr:
                    return new RotatedRect2d(
                        T(rr.Center),
                        rr.Width * s,
                        rr.Height * s,
                        PositionCorrectionResolver.DisplayAngleDeg(matrix, rr.Angle));

                case Polygon2d poly:
                    return new Polygon2d(
                        poly.Vertices.Select(v => T(v)).ToArray(),
                        poly.IsClosed);

                // P0-2 封口（完全删除 WPF Rect）：几何值恒为 RotatedRect2d（轴对齐即 Angle=0），
                // 老工程数据在读入时已由 GeometryJsonConverter 迁移，此处不再需要 Rect 分支。

                case Line2d l:
                {
                    // 方向向量随矩阵线性部分旋转（平移不影响方向）
                    var dir1 = T(l.Point);
                    var dir2 = T(new Point(l.Point.X + l.Direction.X, l.Point.Y + l.Direction.Y));
                    return new Line2d(dir1, dir2 - dir1);
                }

                case Arc2d arc:
                {
                    // Arc 角度字段为弧度；矩阵旋转偏移以度表示 → 转弧度后叠加
                    double angleOffsetRad = PositionCorrectionResolver.DisplayAngleDeg(matrix, 0.0) * Math.PI / 180.0;
                    return new Arc2d(
                        T(arc.Center),
                        arc.Radius * s,
                        arc.StartAngle + angleOffsetRad,
                        arc.EndAngle + angleOffsetRad,
                        arc.IsMajorArc);
                }

                case Point2d p:
                    var tp = T(new Point(p.X, p.Y));
                    return new Point2d(tp.X, tp.Y);

                default:
                    // 未知/自定义几何：不做变换，避免破坏解析
                    return geometry;
            }
        }

        /// <summary>
        /// 变换 RoiShape：矩阵为空或几何不变时返回原实例；否则新建 RoiShape 并携带原样式。
        /// </summary>
        public static RoiShape? Transform(RoiShape? shape, AffineMatrix? fullMatrix)
        {
            if (shape == null || fullMatrix == null) return shape;

            var newGeometry = TransformGeometry(shape.Geometry, fullMatrix);
            if (ReferenceEquals(newGeometry, shape.Geometry))
                return shape;

            return new RoiShape
            {
                Geometry = newGeometry,
                FillColorArgb = shape.FillColorArgb,
                StrokeColorArgb = shape.StrokeColorArgb,
                StrokeThickness = shape.StrokeThickness,
                Opacity = shape.Opacity,
            };
        }
    }
}
