using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WindowUtilities.Extern;

/// <summary>
/// 拖拽跟随装饰：被拖项的半透明影像，随指针移动，对应原件 DragDropAdorner。
/// </summary>
public class DragDropAdorner : Adorner
{
    private readonly Rectangle _child;
    private double _left;
    private double _top;

    /// <param name="adornedElement">被装饰元素，通常是图形页。</param>
    /// <param name="source">被拖拽的源元素，提供尺寸。</param>
    /// <param name="descendantBounds">源元素的内容边界，用于对齐影像。</param>
    /// <param name="fill">影像画刷。</param>
    public DragDropAdorner(FrameworkElement adornedElement, FrameworkElement source, Rect descendantBounds, Brush fill)
        : base(adornedElement)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fill);
        _child = new Rectangle
        {
            Width = source.ActualWidth,
            Height = source.ActualHeight,
            Fill = fill,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        _left = descendantBounds.X;
        _top = descendantBounds.Y;
        AddVisualChild(_child);
    }

    /// <summary>装饰内容只有一块跟随影像。</summary>
    protected override int VisualChildrenCount => 1;

    /// <summary>取装饰内容。</summary>
    protected override Visual GetVisualChild(int index) => _child;

    /// <summary>设置影像相对被装饰元素的偏移。</summary>
    public void SetOffsets(double left, double top)
    {
        _left = left;
        _top = top;
        InvalidateArrange();
    }

    /// <summary>测量跟随影像。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        _child.Measure(constraint);
        return constraint;
    }

    /// <summary>按当前偏移摆放跟随影像。</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _child.Arrange(new Rect(_left, _top, _child.Width, _child.Height));
        return finalSize;
    }
}
