namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 形状类型枚举（UI 绘制、序列化、绑定 <c>[Type=X]</c> 过滤共用）。
/// </summary>
public enum ShapeType
{
    /// <summary>
    /// 无工具（空闲状态）
    /// </summary>
    None = 0,

    /// <summary>
    /// 点
    /// </summary>
    Point,

    /// <summary>
    /// 直线
    /// </summary>
    Line,

    /// <summary>
    /// 圆形
    /// </summary>
    Circle,

    /// <summary>
    /// 矩形
    /// </summary>
    Rectangle,

    /// <summary>
    /// 旋转矩形
    /// </summary>
    RotatedRectangle,

    /// <summary>
    /// 多边形
    /// </summary>
    Polygon,

    /// <summary>
    /// 圆环
    /// </summary>
    Annulus,

    /// <summary>
    /// 弧形
    /// </summary>
    Arc
}
