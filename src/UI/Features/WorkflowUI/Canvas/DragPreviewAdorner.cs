using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

/// <summary>
/// 拖动预览 Adorner：对应原件 DiagramItem 里的 AdornerPanel 容器。
/// <para>预览快照与对齐线都画在 Adorner 层，靠 <see cref="Layer"/> 内的坐标移动，
/// 完全不参与 WorkflowCanvas 的 Measure/Arrange——原件正是这样避免拖动时触发画布布局。</para>
/// </summary>
public sealed class DragPreviewAdorner : Adorner
{
    private readonly System.Windows.Controls.Canvas _layer = new() { IsHitTestVisible = false };

    public DragPreviewAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        AddVisualChild(_layer);
    }

    /// <summary>预览内容层：快照与对齐线都加在这里，改坐标只影响 Adorner 内部布局。</summary>
    public System.Windows.Controls.Canvas Layer => _layer;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _layer;

    protected override Size MeasureOverride(Size constraint)
    {
        _layer.Measure(constraint);
        return AdornedElement.RenderSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layer.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
