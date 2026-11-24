using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>
/// 结果轮廓点，对应原件 <c>Apps.Data.ShapeDefine.Outlinepoint</c>
/// （TypeDef RID 109，实现原件 IOutlinePoint）。
/// 原件 OutlineData 取 <c>OutLineInfo?.BinaryPointData ?? BinaryPointData</c>，
/// 即优先用关联轮廓对象的二进制点集，其次用自身缓存；本方没有 OutLineInfo 对应类型，
/// 直接以二进制点集作为唯一来源，打包格式仍由 NativeShapeProjection 负责
/// （16 字节/点：x、y、passed、resultIndex）。
/// </summary>
public class OutlinePoint : ShapePrimitive, IOutlinePoint
{
    public OutlinePoint()
    {
    }

    public OutlinePoint(byte[] outlineData, string stroke, string ngColor)
    {
        OutlineData = outlineData;
        Stroke = stroke;
        NgColor = ngColor;
    }

    public byte[] OutlineData { get; set; } = System.Array.Empty<byte>();

    public string NgColor { get; set; } = "#FFFF0000";
}
