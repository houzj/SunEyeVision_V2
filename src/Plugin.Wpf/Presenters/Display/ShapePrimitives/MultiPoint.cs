using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Presenters.Display;

/// <summary>
/// 轮廓点聚合容器，对应原件 <c>Apps.Data.ShapeDefine.MultiPoint</c>
/// （TypeDef RID 110，实现原件 IMultiPoint）。
/// 原件同时承载三条子通道，MultiPointShape 一次绘制全部消费：
/// <list type="bullet">
/// <item>OutlinePoints —— 结果页轮廓（IOutlinePoint，二进制点集）。</item>
/// <item>MicroPoints —— 模板窗口模型轮廓点、点集（IMicroPoint，逐点像素坐标）。</item>
/// <item>Rectangles —— 矩形叠加。</item>
/// </list>
/// 原件三个属性可写并带变更通知；本方集合在构造时即初始化，避免原件调用方必须
/// 先赋空集合才能 Add 的隐含约定，其余语义保持一致。
/// </summary>
public class MultiPoint : ShapePrimitive, IMultiPoint
{
    public ObservableCollection<IOutlinePoint> OutlinePoints { get; set; } = new();

    public ObservableCollection<IMicroPoint> MicroPoints { get; set; } = new();

    public ObservableCollection<IRectangle> Rectangles { get; set; } = new();
}
