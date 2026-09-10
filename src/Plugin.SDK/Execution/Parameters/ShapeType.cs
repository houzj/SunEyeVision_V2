using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 形状类型枚举（UI 绘制、序列化、绑定 <c>[Type=X]</c> 过滤共用）。
/// </summary>
/// <remarks>
/// 矩形统一为 <see cref="RotatedRectangle"/>：几何值恒为 <c>RotatedRect2d</c>，轴对齐矩形即 Angle=0。
/// 历史 <c>Rectangle</c>（数值 4）已删除，由 <see cref="ShapeTypeJsonConverter"/> 在读入时迁移为
/// <see cref="RotatedRectangle"/>，老工程不受影响。
///
/// 枚举值必须显式赋值：删除 Rectangle 后不得让后续成员序号前移，否则存量工程数据错位。
/// </remarks>
[JsonConverter(typeof(ShapeTypeJsonConverter))]
public enum ShapeType
{
    /// <summary>
    /// 无工具（空闲状态）
    /// </summary>
    None = 0,

    /// <summary>
    /// 点
    /// </summary>
    Point = 1,

    /// <summary>
    /// 直线
    /// </summary>
    Line = 2,

    /// <summary>
    /// 圆形
    /// </summary>
    Circle = 3,

    // 数值 4 = 历史 Rectangle（已删除）。读入时由 ShapeTypeJsonConverter 迁移为 RotatedRectangle，
    // 此处不定义成员，避免矩形概念复活（rule-008）。

    /// <summary>
    /// 矩形（可旋转；轴对齐即 Angle=0，几何值为 RotatedRect2d）
    /// </summary>
    RotatedRectangle = 5,

    /// <summary>
    /// 多边形
    /// </summary>
    Polygon = 6,

    /// <summary>
    /// 圆环
    /// </summary>
    Annulus = 7,

    /// <summary>
    /// 弧形
    /// </summary>
    Arc = 8,
}
