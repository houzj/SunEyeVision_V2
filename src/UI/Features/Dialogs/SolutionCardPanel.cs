using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.Dialogs;

/// <summary>Responsive, evenly sized solution cards with viewport-only container generation.</summary>
public sealed class SolutionCardPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(nameof(CardWidth), typeof(double),
        typeof(SolutionCardPanel), new FrameworkPropertyMetadata(240d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double CardWidth { get => (double)GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    private const double Gap = 12;
    private const double CardHeight = 112;
    private readonly List<double> _rowTops = new();
    private int _columns = 1;
    private double _cardWidth, _extentHeight, _viewportWidth, _viewportHeight, _offset;
    public int ColumnCount => _columns;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return new Size();
        _viewportWidth = double.IsInfinity(availableSize.Width) ? CardWidth : Math.Max(0, availableSize.Width);
        _viewportHeight = double.IsInfinity(availableSize.Height) ? 600 : Math.Max(0, availableSize.Height);
        _columns = Math.Max(1, (int)((_viewportWidth + Gap) / (CardWidth + Gap)));
        // CardWidth is the minimum readable width; distribute the remaining space
        // across all columns, including when the last row is incomplete.
        _cardWidth = Math.Max(0, (_viewportWidth - (_columns - 1) * Gap) / _columns);
        _rowTops.Clear(); _rowTops.Add(0);
        for (var first = 0; first < owner.Items.Count; first += _columns)
        {
            _rowTops.Add(_rowTops[^1] + CardHeight + Gap);
        }
        _extentHeight = Math.Max(0, _rowTops[^1] - (owner.Items.Count == 0 ? 0 : Gap));
        _offset = Math.Clamp(_offset, 0, Math.Max(0, _extentHeight - _viewportHeight));
        var firstRow = RowAt(_offset);
        var lastRow = RowAt(_offset + _viewportHeight);
        var firstIndex = Math.Max(0, firstRow - 1) * _columns;
        var lastIndex = Math.Min(owner.Items.Count - 1, (lastRow + 2) * _columns - 1);
        // InternalChildren connects an items-host panel to its generator on first
        // access. Reading ItemContainerGenerator before that can cache null,
        // even though GetItemsOwner already returns the ListBox.
        var children = InternalChildren;
        var generator = ItemContainerGenerator;
        if (generator == null) return new Size(_viewportWidth, _viewportHeight);
        for (var child = children.Count - 1; child >= 0; child--)
        {
            var index = owner.ItemContainerGenerator.IndexFromContainer(InternalChildren[child]);
            if (index >= firstIndex && index <= lastIndex) continue;
            var position = new GeneratorPosition(child, 0);
            if (generator is IRecyclingItemContainerGenerator recycling) recycling.Recycle(position, 1);
            else generator.Remove(position, 1);
            RemoveInternalChildRange(child, 1);
        }
        if (owner.Items.Count > 0)
        {
            var position = generator.GeneratorPositionFromIndex(firstIndex);
            var childIndex = position.Offset == 0 ? position.Index : position.Index + 1;
            using (generator.StartAt(position, GeneratorDirection.Forward, true))
                for (var index = firstIndex; index <= lastIndex; index++, childIndex++)
                {
                    var child = (UIElement)generator.GenerateNext(out var generated);
                    // Theme/template changes can reuse generated containers in a new panel.
                    if (generated || !InternalChildren.Contains(child))
                    {
                        if (VisualTreeHelper.GetParent(child) is SolutionCardPanel previous && previous != this)
                        {
                            var previousIndex = previous.InternalChildren.IndexOf(child);
                            if (previousIndex >= 0) previous.RemoveInternalChildRange(previousIndex, 1);
                        }
                        if (childIndex >= InternalChildren.Count) AddInternalChild(child);
                        else InsertInternalChild(childIndex, child);
                        generator.PrepareItemContainer(child);
                    }
                    var row = index / _columns;
                    child.Measure(new Size(_cardWidth, _rowTops[row + 1] - _rowTops[row] - Gap));
                }
        }
        ScrollOwner?.InvalidateScrollInfo();
        return new Size(_viewportWidth, _viewportHeight);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner != null)
            foreach (UIElement child in InternalChildren)
            {
                var index = owner.ItemContainerGenerator.IndexFromContainer(child);
                if (index < 0 || index / _columns + 1 >= _rowTops.Count) continue;
                var row = index / _columns;
                var height = CardHeight;
                child.Arrange(new Rect(index % _columns * (_cardWidth + Gap), _rowTops[row] - _offset, _cardWidth, height));
            }
        return finalSize;
    }
    private int RowAt(double offset)
    {
        if (_rowTops.Count <= 1) return 0;
        var index = _rowTops.BinarySearch(offset);
        return Math.Clamp(index >= 0 ? index : ~index - 1, 0, _rowTops.Count - 2);
    }
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset) RemoveInternalChildRange(0, InternalChildren.Count);
        else if (args.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move)
        {
            var position = args.Action == NotifyCollectionChangedAction.Move ? args.OldPosition : args.Position;
            if (position.Index >= 0 && position.Index < InternalChildren.Count && args.ItemUICount > 0)
                RemoveInternalChildRange(position.Index, Math.Min(args.ItemUICount, InternalChildren.Count - position.Index));
        }
        InvalidateMeasure();
    }
    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || _rowTops.Count <= index / _columns + 1) return;
        var row = index / _columns;
        if (_rowTops[row] < _offset) SetVerticalOffset(_rowTops[row]);
        else if (_rowTops[row + 1] - Gap > _offset + _viewportHeight)
            SetVerticalOffset(_rowTops[row + 1] - Gap - _viewportHeight);
    }
    public bool CanVerticallyScroll { get; set; }
    public bool CanHorizontallyScroll { get; set; }
    public double ExtentWidth => _viewportWidth;
    public double ExtentHeight => _extentHeight;
    public double ViewportWidth => _viewportWidth;
    public double ViewportHeight => _viewportHeight;
    public double HorizontalOffset => 0;
    public double VerticalOffset => _offset;
    public ScrollViewer? ScrollOwner { get; set; }
    public void LineUp() => SetVerticalOffset(_offset - 36);
    public void LineDown() => SetVerticalOffset(_offset + 36);
    public void MouseWheelUp() => SetVerticalOffset(_offset - 108);
    public void MouseWheelDown() => SetVerticalOffset(_offset + 108);
    public void PageUp() => SetVerticalOffset(_offset - _viewportHeight);
    public void PageDown() => SetVerticalOffset(_offset + _viewportHeight);
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset)) return;
        _offset = Math.Clamp(offset, 0, Math.Max(0, _extentHeight - _viewportHeight));
        InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo();
    }
    public void SetHorizontalOffset(double offset) { }
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual == null || !IsAncestorOf(visual)) return Rect.Empty;
        var bounds = visual.TransformToAncestor(this).TransformBounds(rectangle);
        if (bounds.Top < 0) SetVerticalOffset(_offset + bounds.Top);
        else if (bounds.Bottom > _viewportHeight) SetVerticalOffset(_offset + bounds.Bottom - _viewportHeight);
        return bounds;
    }
}
