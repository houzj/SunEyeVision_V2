using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>
/// 单个微小点，对应原件 <c>Apps.Data.ShapeDefine.MicroPoint</c>
/// （TypeDef RID 112，实现原件 IMicroPoint）。
/// 原件 Point 是显式接口实现 <c>Point =&gt; new Point(CenterX, CenterY)</c>，
/// 坐标存于基类 Shape 的两个 float；本方改成公开属性，语义不变，
/// 调用方仍可直接设 CenterX/CenterY 或整点替换。
/// </summary>
public class MicroPoint : ShapePrimitive, IMicroPoint
{
    public MicroPoint()
    {
    }

    public MicroPoint(double centerX, double centerY, string stroke = "#FFFFFFFF")
    {
        CenterX = centerX;
        CenterY = centerY;
        Stroke = stroke;
    }

    public Point Point => new(CenterX, CenterY);
}
