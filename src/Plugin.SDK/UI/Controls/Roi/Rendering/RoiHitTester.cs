using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering
{
    /// <summary>
    /// 区域命中测试器 - 独立于渲染实现
    /// 提供高性能的区域和手柄命中测试功能
    /// </summary>
    public static class RoiHitTester
    {
        /// <summary>
        /// 默认命中容差（像素）
        /// </summary>
        public const double DefaultTolerance = 5.0;

        /// <summary>
        /// 默认手柄大小（像素）
        /// </summary>
        public const double DefaultHandleSize = 60.0;

        /// <summary>
        /// 测试点击位置命中的区域
        /// </summary>
        /// <param name="point">点击位置（图像坐标）</param>
        /// <param name="regions">区域渲染上下文集合</param>
        /// <param name="tolerance">命中容差</param>
        /// <returns>命中的区域上下文，未命中返回null</returns>
        public static RoiRenderContext? HitTest(
            Point point,
            IEnumerable<RoiRenderContext> regions,
            double tolerance = DefaultTolerance)
        {
            // 从后往前测试（后绘制的在上面）
            foreach (var region in regions.Reverse())
            {
                if (!region.IsVisible) continue;

                if (IsPointInRegion(point, region, tolerance))
                {
                    return region;
                }
            }
            return null;
        }

        /// <summary>
        /// 测试点击位置命中的区域ID
        /// </summary>
        /// <param name="point">点击位置（图像坐标）</param>
        /// <param name="regions">区域渲染上下文集合</param>
        /// <param name="tolerance">命中容差</param>
        /// <returns>命中的区域ID，未命中返回null</returns>
        public static Guid? HitTestId(
            Point point,
            IEnumerable<RoiRenderContext> regions,
            double tolerance = DefaultTolerance)
        {
            var hitRegion = HitTest(point, regions, tolerance);
            return hitRegion?.Id;
        }

        /// <summary>
        /// 测试点击位置命中的手柄
        /// </summary>
        /// <param name="point">点击位置（图像坐标）</param>
        /// <param name="selectedRegion">选中的区域上下文</param>
        /// <param name="handleSize">手柄大小</param>
        /// <param name="tolerance">命中容差</param>
        /// <returns>命中的手柄类型</returns>
        public static HandleType HitTestHandle(
            Point point,
            RoiRenderContext? selectedRegion,
            double handleSize = DefaultHandleSize,
            double tolerance = DefaultTolerance)
        {
            if (selectedRegion == null) return HandleType.None;

            var handles = CreateHandles(selectedRegion, handleSize);
            return HitTestHandle(point, handles, tolerance);
        }

        /// <summary>
        /// 测试点击位置命中的手柄
        /// </summary>
        /// <param name="point">点击位置（图像坐标）</param>
        /// <param name="handles">手柄数组</param>
        /// <param name="tolerance">命中容差</param>
        /// <returns>命中的手柄类型</returns>
        public static HandleType HitTestHandle(Point point, EditHandle[] handles, double tolerance = DefaultTolerance)
        {
            foreach (var handle in handles)
            {
                var expandedBounds = new Rect(
                    handle.Bounds.X - tolerance,
                    handle.Bounds.Y - tolerance,
                    handle.Bounds.Width + tolerance * 2,
                    handle.Bounds.Height + tolerance * 2);

                if (expandedBounds.Contains(point))
                {
                    return handle.Type;
                }
            }
            return HandleType.None;
        }

        /// <summary>
        /// 为指定区域创建编辑手柄
        /// </summary>
        /// <param name="region">区域渲染上下文</param>
        /// <param name="scale">当前缩放值</param>
        /// <returns>手柄数组</returns>
        public static EditHandle[] CreateHandles(RoiRenderContext region, double scale = 1.0)
        {
            return region.ShapeType switch
            {
                ShapeType.Rectangle => CreateRectangleHandles(region, scale),
                ShapeType.Circle => CreateCircleHandles(region, scale),
                ShapeType.RotatedRectangle => CreateRotatedRectangleHandles(region, scale),
                ShapeType.Line => CreateLineHandles(region, scale),
                ShapeType.Polygon => CreatePolygonHandles(region, scale),
                _ => Array.Empty<EditHandle>()
            };
        }

        #region 区域命中测试

        private static bool IsPointInRegion(Point point, RoiRenderContext region, double tolerance)
        {
            return region.ShapeType switch
            {
                ShapeType.Rectangle => IsPointInRectangle(point, region),
                ShapeType.Circle => IsPointInCircle(point, region),
                ShapeType.RotatedRectangle => IsPointInRotatedRectangle(point, region),
                ShapeType.Line => IsPointNearLine(point, region, tolerance),
                ShapeType.Polygon => IsPointInPolygon(point, region),
                _ => false
            };
        }

        private static bool IsPointInRectangle(Point point, RoiRenderContext region)
        {
            var bounds = new Rect(
                region.Center.X - region.Width / 2,
                region.Center.Y - region.Height / 2,
                region.Width,
                region.Height);
            return bounds.Contains(point);
        }

        private static bool IsPointInCircle(Point point, RoiRenderContext region)
        {
            var dx = point.X - region.Center.X;
            var dy = point.Y - region.Center.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= region.Radius;
        }

        private static bool IsPointInRotatedRectangle(Point point, RoiRenderContext region)
        {
            double dx = point.X - region.Center.X;
            double dy = point.Y - region.Center.Y;

            double angleRad = -region.Angle * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;

            return Math.Abs(localX) <= region.Width / 2 && Math.Abs(localY) <= region.Height / 2;
        }

        private static bool IsPointNearLine(Point point, RoiRenderContext region, double tolerance)
        {
            return DistanceToLine(point, region.LineStart, region.LineEnd) < tolerance;
        }

        private static bool IsPointInPolygon(Point point, RoiRenderContext region)
        {
            var points = region.PolygonPoints;
            if (points.Count < 3) return false;

            // 射线法判断点是否在多边形内
            bool inside = false;
            int j = points.Count - 1;

            for (int i = 0; i < points.Count; j = i++)
            {
                if (((points[i].Y > point.Y) != (points[j].Y > point.Y)) &&
                    (point.X < (points[j].X - points[i].X) * (point.Y - points[i].Y) /
                    (points[j].Y - points[i].Y) + points[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static double DistanceToLine(Point point, Point lineStart, Point lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);

            if (length < 0.001) return Point.Subtract(point, lineStart).Length;

            var t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (length * length)));

            var projection = new Point(lineStart.X + t * dx, lineStart.Y + t * dy);
            return Point.Subtract(point, projection).Length;
        }

        #endregion

        #region 手柄创建

        private static EditHandle[] CreateRectangleHandles(RoiRenderContext region, double scale)
        {
            var bounds = new Rect(
                region.Center.X - region.Width / 2,
                region.Center.Y - region.Height / 2,
                region.Width,
                region.Height);
            return HandleRenderer.CreateRectangleHandles(bounds, scale);
        }

        private static EditHandle[] CreateCircleHandles(RoiRenderContext region, double scale)
        {
            return HandleRenderer.CreateCircleHandles(region.Center, region.Radius, scale);
        }

        private static EditHandle[] CreateRotatedRectangleHandles(RoiRenderContext region, double scale)
        {
            var corners = RoiRenderContext.GetRotatedRectangleCorners(
                region.Center, region.Width, region.Height, region.Angle);
            var bottomCenter = new Point(
                (corners[2].X + corners[3].X) / 2,
                (corners[2].Y + corners[3].Y) / 2);
            return HandleRenderer.CreateRotatedRectangleHandles(corners, region.Angle, bottomCenter, scale, region.Width);
        }

        private static EditHandle[] CreateLineHandles(RoiRenderContext region, double scale)
        {
            return HandleRenderer.CreateLineHandles(region.LineStart, region.LineEnd, scale);
        }

        private static EditHandle[] CreatePolygonHandles(RoiRenderContext region, double scale)
        {
            var points = region.PolygonPoints
                .Select(p => new Point2D(p.X, p.Y))
                .ToList();
            return HandleRenderer.CreatePolygonHandles(points, scale);
        }

        #endregion
    }
}
