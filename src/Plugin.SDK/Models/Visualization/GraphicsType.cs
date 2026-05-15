namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 图形类型枚举 - 对齐 Halcon 原生显示体系（HDrawingObject / Disp* 算子）
    /// </summary>
    public enum GraphicsType
    {
        /// <summary>轴对齐矩形 → DispRectangle1 / HDrawingObject.RECTANGLE1</summary>
        Rectangle,

        /// <summary>旋转矩形 → DispRectangle2 / HDrawingObject.RECTANGLE2</summary>
        RotatedRectangle,

        /// <summary>圆 → DispCircle / HDrawingObject.CIRCLE</summary>
        Circle,

        /// <summary>椭圆 → DispEllipse / HDrawingObject.ELLIPSE</summary>
        Ellipse,

        /// <summary>线段 → DispLine / HDrawingObject.LINE</summary>
        Line,

        /// <summary>十字标记 → DispCross（HDrawingObject 不支持，回退 DispObj）</summary>
        Cross,

        /// <summary>箭头 → DispArrow（HDrawingObject 不支持，回退 DispObj）</summary>
        Arrow,

        /// <summary>轮廓 → DispObj(HXLDCont)（HDrawingObject 不支持，回退 DispObj）</summary>
        Contour,

        /// <summary>圆弧 → DispArc（HDrawingObject 不支持，回退 DispObj）</summary>
        Arc,
    }
}
