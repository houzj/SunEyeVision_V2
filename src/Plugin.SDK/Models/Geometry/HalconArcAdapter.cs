using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// WPF 弧/圆几何 ↔ HALCON metrology 角度参数的唯一转换出口。
    /// </summary>
    /// <remarks>
    /// ★ 为什么需要这一层：WPF 侧用"连续扫掠坐标"（角度可跨 2π，<see cref="Arc2d.AngularSpan"/> 带符号），
    ///   HALCON 侧要求 'start_phi'/'end_phi' 落在区间 [0, 2π]。两者语义不同，不能互改迁就：
    ///   - 给 WPF 卡尺几何加 [0, 2π) 折叠 → 打断连续扫掠（圆弧跨 2π 时整排卡尺反向的旧缺陷）
    ///   - 把 Arc2d 改成 [0, 2π) → AngularSpan 的大小弧/方向判定失效
    ///   因此只在"传给 HALCON 的这一刻"做一次性转换，转换规则由契约测试锁定。
    ///
    /// 依据（HALCON 24.05 Operator Reference — add_metrology_object_circle_measure）:
    ///   - 'start_phi'/'end_phi' 输入值自动映射到区间 [0, 2π]
    ///   - 闭合圆 = start_phi 0 + end_phi 2π（with positive point order）
    ///   - 'point_order': positive = 数学正方向（逆时针）/ negative = 数学负方向（顺时针）
    ///
    /// 坐标系换算：HALCON phi 以 Y 向上为正方向递增（逆时针），
    ///   WPF atan2(dy, dx) 屏角 Y 向下递增（视觉顺时针），故 phi = -WPF 屏角。
    /// </remarks>
    public static class HalconArcAdapter
    {
        private const double TwoPi = 2.0 * Math.PI;

        /// <summary>浮点容差（用于跨度合法性判定）</summary>
        private const double Epsilon = 1e-9;

        /// <summary>
        /// 整圆：按 HALCON 官方文档返回 (0, 2π, positive)。
        /// </summary>
        public static (double StartPhi, double EndPhi, string PointOrder) FromFullCircle()
            => (0.0, TwoPi, "positive");

        /// <summary>
        /// 圆弧：WPF 连续扫掠弧 → HALCON (start_phi, end_phi, point_order)。
        /// </summary>
        /// <exception cref="ArgumentException">
        /// 弧为空 / 半径非正 / 跨度非法（NaN 或超过 2π）时抛出（rule-015 禁止兜底回退）
        /// </exception>
        public static (double StartPhi, double EndPhi, string PointOrder) FromArc(Arc2d arc)
        {
            if (arc.IsEmpty)
                throw new ArgumentException("弧为空，无法转换为 HALCON 参数。", nameof(arc));
            if (arc.Radius <= 0)
                throw new ArgumentException($"弧半径非法: {arc.Radius}", nameof(arc));

            double span = arc.AngularSpan;
            if (double.IsNaN(span) || double.IsInfinity(span) || Math.Abs(span) > TwoPi + Epsilon)
                throw new ArgumentException($"弧跨度超出 [0, 2π] 范围: {span}", nameof(arc));

            // 起点：WPF 屏角取负后规范到 [0, 2π)
            double startPhi = NormalizeToHalconInterval(-arc.StartAngle);
            // 终点：沿 point_order 方向推进 |span| 后再次规范；两端各自 ±2πk 不改变 (end-start) mod 2π
            double signedSpan = arc.IsClockwise ? Math.Abs(span) : -Math.Abs(span);
            double endPhi = NormalizeToHalconInterval(startPhi + signedSpan);
            string pointOrder = arc.IsClockwise ? "positive" : "negative";

            return (startPhi, endPhi, pointOrder);
        }

        /// <summary>
        /// HALCON phi → WPF 屏角（结果回显时的反向转换）。
        /// </summary>
        public static double ToWpfAngle(double phi) => -phi;

        /// <summary>
        /// 规范到 HALCON 角度区间 [0, 2π)。
        /// </summary>
        private static double NormalizeToHalconInterval(double angleRad)
        {
            if (double.IsNaN(angleRad) || double.IsInfinity(angleRad))
                throw new ArgumentException($"角度非法: {angleRad}", nameof(angleRad));

            double t = angleRad % TwoPi;
            return t < 0 ? t + TwoPi : t;
        }
    }
}
