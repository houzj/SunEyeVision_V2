using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public class CommomDragHelper : IDisposable
{
	private class DragDropAdorner : Adorner
	{
		private Rectangle child = null;

		private double offsetLeft = 0.0;

		private double offsetTop = 0.0;

		public double OffsetLeft
		{
			get
			{
				return offsetLeft;
			}
			set
			{
				offsetLeft = value;
				UpdateLocation();
			}
		}

		public double OffsetTop
		{
			get
			{
				return offsetTop;
			}
			set
			{
				offsetTop = value;
				UpdateLocation();
			}
		}

		protected override int VisualChildrenCount => 1;

		public DragDropAdorner(UIElement adornedElement, FrameworkElement DragObj, Rect region, Brush brush)
			: base(adornedElement)
		{
			child = new Rectangle
			{
				Clip = new RectangleGeometry(new Rect(0.0 - region.X, 0.0 - region.Y, DragObj.ActualWidth, DragObj.ActualHeight)),
				Margin = new Thickness(region.X, region.Y, 0.0, 0.0),
				Fill = brush,
				Width = region.Width,
				Height = region.Height,
				IsHitTestVisible = false
			};
		}

		public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
		{
			if (transform != null)
			{
				GeneralTransformGroup generalTransformGroup = new GeneralTransformGroup();
				transform = base.GetDesiredTransform(transform);
				string[] array = transform.ToString().Split(',');
				double num = 1.0;
				double num2 = 1.0;
				if (array.Length > 4)
				{
					num = double.Parse(array[0]);
					num2 = double.Parse(array[3]);
				}
				generalTransformGroup.Children.Add(transform);
				generalTransformGroup.Children.Add(new TranslateTransform(offsetLeft * num, offsetTop * num2));
				return generalTransformGroup;
			}
			return null;
		}

		public void SetOffsets(double left, double top)
		{
			offsetLeft = left;
			offsetTop = top;
			UpdateLocation();
		}

		protected override Size MeasureOverride(Size constraint)
		{
			child.Measure(constraint);
			return child.DesiredSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			child.Arrange(new Rect(finalSize));
			return finalSize;
		}

		protected override Visual GetVisualChild(int index)
		{
			return child;
		}

		private void UpdateLocation()
		{
			if (Parent is AdornerLayer adornerLayer)
			{
				adornerLayer.Update(AdornedElement);
			}
		}
	}

	private Point? _mouseDown;

	private Point _offset;

	private Control _source;

	private DragDropAdorner _adorner;

	private DragEventHandler _dragOver;

	private DragEventHandler _dragEnter;

	private DragEventHandler _dragLeave;

	private FrameworkElement page;

	private bool IsIndrag = false;

	private FrameworkElement curAdornerSource = null;

	public CommomDragHelper(Control source, FrameworkElement Container)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		_dragEnter = DragScope_DragEnter;
		_dragOver = DragScope_DragOver;
		_dragLeave = DragScope_DragLeave;
		page = Container;
		_source = source;
		_source.PreviewMouseLeftButtonDown += SourceMouseLeftButtonDown;
		_source.PreviewMouseMove += SourceMouseMove;
	}

	private void SourceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point position = e.GetPosition(_source);
		if (!IsMouseOverScrollbar(position))
		{
			_mouseDown = position;
		}
	}

	private void SourceMouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			if (_mouseDown.HasValue && HasMoved(e.GetPosition(_source)) && !IsIndrag && e.OriginalSource is FrameworkElement frameworkElement && page != null)
			{
				page.AllowDrop = true;
				_offset = e.GetPosition(frameworkElement);
				IsIndrag = true;
				DoDragDrop(frameworkElement);
				e.Handled = true;
			}
		}
		else
		{
			_mouseDown = null;
		}
	}

	private bool HasMoved(Point point)
	{
		if (point.X == 0.0 && point.Y == 0.0)
		{
			return false;
		}
		return Math.Abs(point.X - _mouseDown.Value.X) > SystemParameters.MinimumHorizontalDragDistance / 2.0 || Math.Abs(point.Y - _mouseDown.Value.Y) > SystemParameters.MinimumVerticalDragDistance / 2.0;
	}

	private bool IsMouseOverScrollbar(Point ptMouse)
	{
		HitTestResult hitTestResult = VisualTreeHelper.HitTest(_source, ptMouse);
		if (hitTestResult == null)
		{
			return false;
		}
		for (DependencyObject dependencyObject = hitTestResult.VisualHit; dependencyObject != null; dependencyObject = ((!(dependencyObject is Visual) && !(dependencyObject is Visual3D)) ? LogicalTreeHelper.GetParent(dependencyObject) : VisualTreeHelper.GetParent(dependencyObject)))
		{
			if (dependencyObject is ScrollBar)
			{
				return true;
			}
		}
		return false;
	}

	private void DoDragDrop(FrameworkElement adornerSource)
	{
		if (adornerSource != null)
		{
			Rect descendantBounds = VisualTreeHelper.GetDescendantBounds(adornerSource);
			VisualBrush brush = new VisualBrush(adornerSource);
			_adorner = new DragDropAdorner(page, adornerSource, descendantBounds, brush);
			_adorner.Opacity = 0.7;
			_adorner.Visibility = Visibility.Visible;
		}
		DragDrop.AddPreviewDragEnterHandler(page, _dragEnter);
		DragDrop.AddPreviewDragOverHandler(page, _dragOver);
		DragDrop.AddPreviewDragLeaveHandler(page, _dragLeave);
		curAdornerSource = adornerSource;
		Button button = new Button();
		button.Tag = new object[3] { _source.DataContext, _offset, _source };
		DragDropEffects ret = DragDrop.DoDragDrop(_source, button, DragDropEffects.Copy);
		DragFinished(ret);
		DragDrop.RemovePreviewDragEnterHandler(page, _dragEnter);
		DragDrop.RemovePreviewDragOverHandler(page, _dragOver);
		DragDrop.RemovePreviewDragLeaveHandler(page, _dragLeave);
		page = null;
		IsIndrag = false;
		Dispose();
	}

	private void DragScope_DragEnter(object sender, DragEventArgs args)
	{
		if (curAdornerSource != null)
		{
			curAdornerSource.IsHitTestVisible = false;
		}
		if (_adorner != null && page != null)
		{
			Point position = args.GetPosition(page);
			_adorner.SetOffsets(position.X - _offset.X, position.Y - _offset.Y);
			if (_adorner.Parent == null)
			{
				AdornerLayer.GetAdornerLayer(page)?.Add(_adorner);
			}
		}
	}

	private void DragScope_DragOver(object sender, DragEventArgs args)
	{
		if (_adorner != null)
		{
			Point position = args.GetPosition(page);
			_adorner.SetOffsets(position.X - _offset.X, position.Y - _offset.Y);
		}
	}

	private void DragScope_DragLeave(object sender, DragEventArgs args)
	{
		if (_adorner != null && page != null)
		{
			AdornerLayer.GetAdornerLayer(page)?.Remove(_adorner);
		}
	}

	protected void DragFinished(DragDropEffects ret)
	{
		if (curAdornerSource != null)
		{
			curAdornerSource.IsHitTestVisible = true;
			curAdornerSource = null;
		}
		if (_adorner != null && page != null)
		{
			AdornerLayer.GetAdornerLayer(page)?.Remove(_adorner);
		}
		_adorner = null;
	}

	public void Dispose()
	{
		_source.PreviewMouseLeftButtonDown -= SourceMouseLeftButtonDown;
		_source.PreviewMouseMove -= SourceMouseMove;
	}
}
