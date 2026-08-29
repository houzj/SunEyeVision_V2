namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure
{
    /// <summary>
    /// 测量工具栏图标工厂 - 生成带卡尺特征的几何图标 Path
    /// </summary>
    /// <remarks>
    /// 与区域编辑图标的区分：检测图标在基础几何周围带"测量带"方块特征，
    /// 直观表达"卡尺扫过几何"的边缘检测语义。
    /// </remarks>
    public static class MeasureToolIconFactory
    {
        /// <summary>
        /// 直线卡尺图标（横线 + 左右 2 个测量带方块）
        /// </summary>
        public const string LineIcon =
            "M 4,12 L 20,12 " +                       // 引导线（横线）
            "M 8,7 L 13,7 L 13,17 L 8,17 Z " +        // 左测量带方块
            "M 9,7 L 9,17 " +
            "M 11,7 L 11,17 " +
            "M 14,7 L 19,7 L 19,17 L 14,17 Z " +      // 右测量带方块
            "M 15,7 L 15,17 " +
            "M 17,7 L 17,17";

        /// <summary>
        /// 圆卡尺图标（圆环 + 上/下/左/右 4 个测量带方块）
        /// </summary>
        public const string CircleIcon =
            "M 12,3 A 9,9 0 1 1 12,21 A 9,9 0 1 1 12,3 " +   // 引导圆环
            "M 10,1 L 14,1 L 14,3 L 10,3 Z " +               // 上测量带
            "M 10,21 L 14,21 L 14,23 L 10,23 Z " +           // 下测量带
            "M 1,10 L 3,10 L 3,14 L 1,14 Z " +               // 左测量带
            "M 21,10 L 23,10 L 23,14 L 21,14 Z ";            // 右测量带

        /// <summary>
        /// 圆弧卡尺图标（弧段上凸穿过顶测量带 + 左/右测量带方块）
        /// </summary>
        /// <remarks>
        /// 弧段用三次贝塞尔 C 命令绘制，方向由控制点唯一确定（无 sweep 歧义），
        /// 曲线顶点位于控制点 y 附近，穿过顶测量带 y=4-7。
        /// </remarks>
        public const string ArcIcon =
            "M 5,16 C 5,4 19,4 19,16 " +                    // 引导弧段（贝塞尔，顶点 y≈4-5 穿过顶测量带）
            "M 9,4 L 13,4 L 13,7 L 9,7 Z " +               // 顶测量带
            "M 2,12 L 4,12 L 4,15 L 2,15 Z " +             // 左测量带
            "M 20,12 L 22,12 L 22,15 L 20,15 Z ";          // 右测量带
    }
}
