namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models
{
    /// <summary>
    /// 点元素输入方式（点-线 / 点-圆 / 点-点 三工具统一范式）。
    /// </summary>
    /// <remarks>
    /// 对齐 RoiSubscribePanel 的订阅二分语义：
    /// - <see cref="ByShape"/>（按图形）：整体绑定上游几何图形输出（词目 Point），一次绑定即得 X/Y；
    /// - <see cref="ByCoordinate"/>（按参数）：逐参数 X/Y，每项可手输常量或绑上游标量。
    ///
    /// 点元素无引导绘制入口，两种方式均由参数订阅承载（不产生 measure 调用）。
    /// </remarks>
    public enum PointInputMode
    {
        /// <summary>按图形：整体绑定上游点图形输出（词目 Point）</summary>
        ByShape = 0,

        /// <summary>按参数：逐参数 X/Y（常量或标量绑定）</summary>
        ByCoordinate = 1,
    }
}
