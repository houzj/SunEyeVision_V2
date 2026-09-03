using System;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 双渲染层（结果层 ResultRenderLayer / 交互层 InteractionOverlay）装饰元素缩放公式唯一源。
    /// 幂律管控表（docs/01-方案设计/…/双渲染层幂律缩放治理方案）：
    ///   几何数据（圆心/半径/顶点/带边界）→ p=0 贴图（不进本类）
    ///   线宽/箭头/内边距 → 线性反除 p=1（屏幕恒定）
    ///   手柄/字号/虚线间隔 → 屏幕恒定幂律 p=0.15 + clamp
    ///   十字/点标记 → 缓和幂律 p=0.7
    /// 公式：Screen = Base / zoom^power；本类返回 WPF 逻辑尺寸 = Screen / zoom，
    ///       由 ImageContainer 的 ImageScaleTransform 乘回后得到屏幕像素。
    /// Fail-Forward：zoom ≤ 0.001 一律回退到逻辑尺寸 = 屏幕尺寸（不再反除），杜绝除零/负数。
    /// </summary>
    public static class ViewportScaling
    {
        /// <summary>安全下限（Fail-Forward，防除零/负数）</summary>
        private const double MinZoom = 0.001;

        /// <summary>装饰类（手柄/字号/虚线）幂律指数</summary>
        public const double DecorPower = 0.15;

        /// <summary>标记类（十字/点）幂律指数</summary>
        public const double MarkerPower = 0.7;

        /// <summary>
        /// 线性反除（p=1）：返回 WPF 逻辑尺寸，乘回 zoom 后屏幕恒定 = size。
        /// </summary>
        public static double InvScale(double zoom, double size)
            => zoom > MinZoom ? size / zoom : size;

        /// <summary>
        /// 幂律反除：返回 WPF 逻辑尺寸，乘回 zoom 后屏幕 = baseSize / zoom^(power-1)。
        /// power=1 时即 InvScale；power&lt;1 时为缓和幂律（如十字 0.7）。
        /// </summary>
        public static double Power(double zoom, double baseSize, double power)
            => zoom > MinZoom ? baseSize / Math.Pow(zoom, power) : baseSize;

        /// <summary>
        /// 屏幕恒定幂律 + 屏幕像素上下限：返回 WPF 逻辑尺寸。
        /// Screen = clamp(baseSize / zoom^power, minScreen, maxScreen)，再反除 zoom。
        /// </summary>
        public static double Clamped(double zoom, double baseSize, double power, double minScreen, double maxScreen)
        {
            zoom = Math.Max(zoom, 0.01);
            var screen = baseSize / Math.Pow(zoom, power);
            screen = Math.Clamp(screen, minScreen, maxScreen);
            return screen / zoom;
        }

        // ===== 语义化入口（幂律管控表逐元素） =====

        /// <summary>线宽：屏幕恒定 baseWidth（默认 2px，与 Blob 检测区域一致）</summary>
        public static double LineWidth(double zoom, double baseWidth = 2.0)
            => InvScale(zoom, baseWidth);

        /// <summary>操作手柄尺寸：屏幕恒定 10~12px</summary>
        public static double HandleSize(double zoom)
            => Clamped(zoom, 10.0, DecorPower, 10.0, 12.0);

        /// <summary>标签字号：屏幕恒定 9~12px</summary>
        public static double LabelFontSize(double zoom)
            => Clamped(zoom, 12.0, DecorPower, 9.0, 12.0);

        /// <summary>虚线间隔：屏幕恒定 baseInterval~baseInterval×1.5（默认 4~6px）</summary>
        public static double DashInterval(double zoom, double baseInterval = 4.0)
            => Clamped(zoom, baseInterval, DecorPower, baseInterval, baseInterval * 1.5);
    }
}
