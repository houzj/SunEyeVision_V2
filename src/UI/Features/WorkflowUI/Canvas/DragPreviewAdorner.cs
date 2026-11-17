using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

/// <summary>
/// 拖动预览 Adorner：对应原件 <c>VM.Controls.Core.UIElementAdorner&lt;TElement&gt;</c>。
/// <para>定位方式与原件逐行一致（UIElementAdorner.cs）：
/// <list type="bullet">
/// <item>位移用 <see cref="GetDesiredTransform"/> 里叠加 <see cref="TranslateTransform"/>——不是 Canvas.SetLeft；</item>
/// <item>改位移后调用 <see cref="AdornerLayer.Update"/>，只重排 Adorner 层，不触碰画布布局；</item>
/// <item>Measure 返回子元素 DesiredSize，Arrange 把子元素铺满自身。</item>
/// </list></para>
/// </summary>
public sealed class DragPreviewAdorner : Adorner
{
    private readonly TranslateTransform _offset = new();
    private readonly System.Windows.Controls.Canvas _layer = new() { IsHitTestVisible = false };

    public DragPreviewAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        AddVisualChild(_layer);
    }

    /// <summary>预览内容层：快照与对齐线都以画布绝对坐标加在这里。</summary>
    public System.Windows.Controls.Canvas Layer => _layer;

    /// <summary>对应原件 <c>UIElementAdorner.SetOffsets</c>：改位移并刷新 Adorner 层。</summary>
    public void SetOffsets(double left, double top)
    {
        _offset.X = left;
        _offset.Y = top;
        if (Parent is AdornerLayer layer) layer.Update(AdornedElement);
    }

    /// <summary>对应原件 <c>UIElementAdorner.GetDesiredTransform</c>：在基础变换上叠加位移。</summary>
    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        => new GeneralTransformGroup
        {
            Children =
            {
                base.GetDesiredTransform(transform),
                _offset
            }
        };

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _layer;

    protected override Size MeasureOverride(Size constraint)
    {
        _layer.Measure(constraint);
        return _layer.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
