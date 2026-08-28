using System;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Common.Geometry
{
    /// <summary>
    /// 通用几何算法工具集 - 纯几何计算（无 UI 依赖），供各交互宿主共享复用
    /// </summary>
    /// <remarks>
    /// 收敛交互编辑器（区域编辑器 / 测量编辑器）中重复的纯几何判定逻辑，
    /// 保证命中测试 / 距离 / 角度 / 区域判定行为一致。
    /// </remarks>
    public static class GeometryToolkit
    {
        /// <summary>
        /// 点到线段的最短距离
        /// </summary>
        /// <param name="p">测试点</param>
        /// <param name="a">线段起点</param>
        /// <param name="b">线段终点</param>
        /// <returns>点到线段的最短距离</returns>
        public static double DistanceToSegment(Point p, Point a, Point b)
        {
            var ab = b - a;
            var ap = p - a;
            var lenSq = ab.LengthSquared;
            if (lenSq < 1e-12) return (p - a).Length;
            double t = Math.Clamp(Vector.Multiply(ap, ab) / lenSq, 0.0, 1.0);
            var proj = a + ab * t;
            return (p - proj).Length;
        }

        /// <summary>
        /// 角度是否在 [start, end] 范围内（含容差，处理跨 0 情况）
        /// </summary>
        /// <param name="angle">待测角度（弧度）</param>
        /// <param name="start">起始角度（弧度）</param>
        /// <param name="end">终止角度（弧度）</param>
        /// <param name="tolerance">角向容差（弧度）</param>
        /// <returns>角度是否落在弧段范围内</returns>
        public static bool AngleInRange(double angle, double start, double end, double tolerance)
        {
            // 弧段跨度（处理跨 0：当 end < start 时弧段越过 0°，跨度补 +2π）
            double span = end - start;
            if (span < 0) span += 2 * Math.PI;
            // 规范化角度相对 start（0~2π）
            double normalized = ((angle - start) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            return normalized <= span + tolerance || normalized >= 2 * Math.PI - tolerance;
        }

        /// <summary>
        /// 点是否在四边形内（叉积法，所有边同侧判定）
        /// </summary>
        /// <param name="p">测试点</param>
        /// <param name="a">四边形顶点 1</param>
        /// <param name="b">四边形顶点 2</param>
        /// <param name="c">四边形顶点 3</param>
        /// <param name="d">四边形顶点 4</param>
        /// <returns>点是否在四边形内（含边界）</returns>
        public static bool PointInQuad(Point p, Point a, Point b, Point c, Point d)
        {
            return Sign(p, a, b) >= 0 && Sign(p, b, c) >= 0 &&
                   Sign(p, c, d) >= 0 && Sign(p, d, a) >= 0;
        }

        /// <summary>
        /// 三点叉积符号（用于点在多边形/四边形内判定）
        /// </summary>
        private static double Sign(Point p1, Point p2, Point p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }
    }
}
