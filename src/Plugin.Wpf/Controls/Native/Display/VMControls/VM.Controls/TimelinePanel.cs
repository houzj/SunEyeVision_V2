using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class TimelinePanel : Panel, IScrollInfo
{
	private List<DateElement> _visibleElements;

	public static readonly DependencyProperty BeginDateProperty;

	public static readonly DependencyProperty EndDateProperty;

	public static readonly DependencyProperty OverlapBehaviorProperty;

	public static readonly DependencyProperty KeepOriginalOrderForOverlapProperty;

	public static readonly DependencyProperty OrientationProperty;

	public static readonly DependencyProperty UnitTimeSpanProperty;

	public static readonly DependencyProperty UnitSizeProperty;

	public static readonly DependencyProperty DateProperty;

	public static readonly DependencyProperty DateEndProperty;

	private bool _allowHorizontal;

	private bool _allowVertical;

	private Vector _computedOffset = new Vector(0.0, 0.0);

	private Size _extent = new Size(0.0, 0.0);

	private Vector _offset = new Vector(0.0, 0.0);

	private ScrollViewer _scrollOwner;

	private Size _viewport;

	private Size _physicalViewport;

	public DateTime BeginDate
	{
		get
		{
			return (DateTime)GetValue(BeginDateProperty);
		}
		set
		{
			SetValue(BeginDateProperty, value);
		}
	}

	public DateTime EndDate
	{
		get
		{
			return (DateTime)GetValue(EndDateProperty);
		}
		set
		{
			SetValue(EndDateProperty, value);
		}
	}

	public OverlapBehavior OverlapBehavior
	{
		get
		{
			return (OverlapBehavior)GetValue(OverlapBehaviorProperty);
		}
		set
		{
			SetValue(OverlapBehaviorProperty, value);
		}
	}

	public bool KeepOriginalOrderForOverlap
	{
		get
		{
			return (bool)GetValue(KeepOriginalOrderForOverlapProperty);
		}
		set
		{
			SetValue(KeepOriginalOrderForOverlapProperty, value);
		}
	}

	public Orientation Orientation
	{
		get
		{
			return (Orientation)GetValue(OrientationProperty);
		}
		set
		{
			SetValue(OrientationProperty, value);
		}
	}

	public TimeSpan UnitTimeSpan
	{
		get
		{
			return (TimeSpan)GetValue(UnitTimeSpanProperty);
		}
		set
		{
			SetValue(UnitTimeSpanProperty, value);
		}
	}

	public double UnitSize
	{
		get
		{
			return (double)GetValue(UnitSizeProperty);
		}
		set
		{
			SetValue(UnitSizeProperty, value);
		}
	}

	public bool CanHorizontallyScroll
	{
		get
		{
			return _allowHorizontal;
		}
		set
		{
			_allowHorizontal = value;
		}
	}

	public bool CanVerticallyScroll
	{
		get
		{
			return _allowVertical;
		}
		set
		{
			_allowVertical = value;
		}
	}

	public double ExtentHeight => _extent.Height;

	public double ExtentWidth => _extent.Width;

	public double HorizontalOffset => _offset.X;

	public ScrollViewer ScrollOwner
	{
		get
		{
			return _scrollOwner;
		}
		set
		{
			if (_scrollOwner != value)
			{
				_scrollOwner = value;
				ResetScrollInfo();
			}
		}
	}

	public double VerticalOffset => _offset.Y;

	public double ViewportHeight => _viewport.Height;

	public double ViewportWidth => _viewport.Width;

	private bool IsScrolling => _scrollOwner != null;

	public static DateTime GetDate(DependencyObject obj)
	{
		return (DateTime)obj.GetValue(DateProperty);
	}

	public static void SetDate(DependencyObject obj, DateTime value)
	{
		obj.SetValue(DateProperty, value);
	}

	public static DateTime GetDateEnd(DependencyObject obj)
	{
		return (DateTime)obj.GetValue(DateEndProperty);
	}

	public static void SetDateEnd(DependencyObject obj, DateTime value)
	{
		obj.SetValue(DateEndProperty, value);
	}

	static TimelinePanel()
	{
		BeginDateProperty = DependencyProperty.Register("BeginDate", typeof(DateTime), typeof(TimelinePanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsMeasure));
		EndDateProperty = DependencyProperty.Register("EndDate", typeof(DateTime), typeof(TimelinePanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsMeasure));
		OverlapBehaviorProperty = DependencyProperty.Register("OverlapBehavior", typeof(OverlapBehavior), typeof(TimelinePanel), new FrameworkPropertyMetadata(OverlapBehavior.Stack, FrameworkPropertyMetadataOptions.AffectsMeasure));
		KeepOriginalOrderForOverlapProperty = DependencyProperty.Register("KeepOriginalOrderForOverlap", typeof(bool), typeof(TimelinePanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
		OrientationProperty = StackPanel.OrientationProperty.AddOwner(typeof(TimelinePanel), new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure));
		UnitTimeSpanProperty = DependencyProperty.Register("UnitTimeSpan", typeof(TimeSpan), typeof(TimelinePanel), new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.AffectsMeasure));
		UnitSizeProperty = DependencyProperty.Register("UnitSize", typeof(double), typeof(TimelinePanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		DateProperty = DependencyProperty.RegisterAttached("Date", typeof(DateTime), typeof(TimelinePanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
		DateEndProperty = DependencyProperty.RegisterAttached("DateEnd", typeof(DateTime), typeof(TimelinePanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(TimelinePanel), new FrameworkPropertyMetadata(typeof(TimelinePanel)));
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		DateTime dateTime = DateTime.MaxValue;
		DateTime dateTime2 = DateTime.MinValue;
		foreach (UIElement internalChild in InternalChildren)
		{
			DateTime date = GetDate(internalChild);
			DateTime dateEnd = GetDateEnd(internalChild);
			if (date < dateTime)
			{
				dateTime = date;
			}
			if (date > dateTime2)
			{
				dateTime2 = date;
			}
			if (dateEnd > dateTime2)
			{
				dateTime2 = dateEnd;
			}
		}
		if (BeginDate == DateTime.MinValue)
		{
			BeginDate = dateTime;
		}
		if (EndDate == DateTime.MinValue)
		{
			EndDate = dateTime2;
		}
		foreach (UIElement internalChild2 in InternalChildren)
		{
			DateTime date2 = GetDate(internalChild2);
			DateTime dateEnd2 = GetDateEnd(internalChild2);
			Size availableSize2 = availableSize;
			if (dateEnd2 > DateTime.MinValue && dateEnd2 > date2)
			{
				if (Orientation == Orientation.Horizontal)
				{
					if (UnitTimeSpan != TimeSpan.Zero && UnitSize > 0.0)
					{
						double num = (double)(dateEnd2.Ticks - date2.Ticks) / (double)UnitTimeSpan.Ticks;
						availableSize2.Width = num * UnitSize;
					}
					else if (!double.IsPositiveInfinity(availableSize.Width))
					{
						availableSize2.Width = CalculateTimelineOffset(dateEnd2, availableSize.Width) - CalculateTimelineOffset(date2, availableSize.Width);
					}
				}
				else if (UnitTimeSpan != TimeSpan.Zero && UnitSize > 0.0)
				{
					double num2 = (double)(dateEnd2.Ticks - date2.Ticks) / (double)UnitTimeSpan.Ticks;
					availableSize2.Height = num2 * UnitSize;
				}
				else if (!double.IsPositiveInfinity(availableSize.Height))
				{
					availableSize2.Height = CalculateTimelineOffset(dateEnd2, availableSize.Height) - CalculateTimelineOffset(date2, availableSize.Height);
				}
			}
			internalChild2.Measure(availableSize2);
		}
		Size availableSize3 = new Size(availableSize.Width, availableSize.Height);
		if (UnitTimeSpan != TimeSpan.Zero && UnitSize > 0.0)
		{
			double num3 = (double)(EndDate.Ticks - BeginDate.Ticks) / (double)UnitTimeSpan.Ticks;
			if (Orientation == Orientation.Horizontal)
			{
				availableSize3.Width = num3 * UnitSize;
			}
			else
			{
				availableSize3.Height = num3 * UnitSize;
			}
		}
		Size result = default;
		if ((Orientation == Orientation.Vertical && double.IsPositiveInfinity(availableSize3.Height)) || (Orientation == Orientation.Horizontal && double.IsPositiveInfinity(availableSize3.Width)))
		{
			_visibleElements = null;
		}
		else
		{
			LayoutItems(InternalChildren, availableSize3);
			Rect rect = default;
			foreach (DateElement visibleElement in _visibleElements)
			{
				rect.Union(visibleElement.PlacementRectangle);
			}
			if (Orientation == Orientation.Horizontal)
			{
				result.Width = availableSize3.Width;
				result.Height = rect.Size.Height;
			}
			else
			{
				result.Width = rect.Size.Width;
				result.Height = availableSize3.Height;
			}
		}
		if (IsScrolling)
		{
			Size viewport = new Size(availableSize.Width, availableSize.Height);
			Size extent = new Size(result.Width, result.Height);
			SetScrollingData(offset: new Vector(Math.Max(0.0, Math.Min(_offset.X, extent.Width - viewport.Width)), Math.Max(0.0, Math.Min(_offset.Y, extent.Height - viewport.Height))), viewport: viewport, extent: extent);
			result.Width = Math.Min(result.Width, availableSize.Width);
			result.Height = Math.Min(result.Height, availableSize.Height);
			_physicalViewport = availableSize;
		}
		return result;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		Rect rect = default;
		if (_visibleElements == null)
		{
			LayoutItems(InternalChildren, finalSize);
		}
		foreach (DateElement visibleElement in _visibleElements)
		{
			if (IsScrolling)
			{
				Rect finalRect = new Rect(visibleElement.PlacementRectangle.Location, visibleElement.PlacementRectangle.Size);
				finalRect.Offset(-_offset);
				visibleElement.Element.Arrange(finalRect);
			}
			else
			{
				visibleElement.Element.Arrange(visibleElement.PlacementRectangle);
			}
			rect.Union(visibleElement.PlacementRectangle);
		}
		Size result = ((Orientation != Orientation.Horizontal) ? new Size(rect.Size.Width, finalSize.Height) : new Size(finalSize.Width, rect.Size.Height));
		return result;
	}

	private void ResetScrollInfo()
	{
		_offset = default;
		_physicalViewport = (_viewport = (_extent = new Size(0.0, 0.0)));
	}

	private void SetScrollingData(Size viewport, Size extent, Vector offset)
	{
		_offset = offset;
		if (!AreVirtuallyEqual(viewport, _viewport) || !AreVirtuallyEqual(extent, _extent) || !AreVirtuallyEqual(offset, _computedOffset))
		{
			_viewport = viewport;
			_extent = extent;
			_offset = offset;
			OnScrollChange();
		}
	}

	private double ValidateInputOffset(double offset, string parameterName)
	{
		if (double.IsNaN(offset))
		{
			throw new ArgumentOutOfRangeException(parameterName);
		}
		return Math.Max(0.0, offset);
	}

	private void OnScrollChange()
	{
		if (ScrollOwner != null)
		{
			ScrollOwner.InvalidateScrollInfo();
		}
	}

	private double CalculateTimelineOffset(DateTime d, double finalWidth)
	{
		long num = EndDate.Ticks - BeginDate.Ticks;
		long num2 = d.Ticks - BeginDate.Ticks;
		if (UnitTimeSpan != TimeSpan.Zero && UnitSize > 0.0)
		{
			return (double)num2 / (double)UnitTimeSpan.Ticks * UnitSize;
		}
		if (num > 0)
		{
			return (double)num2 / (double)num * finalWidth;
		}
		return 0.0;
	}

	private static int CompareElementsByLeft(DateElement a, DateElement b)
	{
		return a.PlacementRectangle.Left.CompareTo(b.PlacementRectangle.Left);
	}

	private static int CompareElementsByTop(DateElement a, DateElement b)
	{
		return a.PlacementRectangle.Top.CompareTo(b.PlacementRectangle.Top);
	}

	private void LayoutItems(UIElementCollection children, Size availableSize)
	{
		_visibleElements = new List<DateElement>();
		List<DateElement> list = new List<DateElement>();
		int num = 0;
		foreach (UIElement child in children)
		{
			if (child == null)
			{
				continue;
			}
			DateTime date = GetDate(child);
			DateTime dateEnd = GetDateEnd(child);
			if (child.Visibility != Visibility.Collapsed)
			{
				if (KeepOriginalOrderForOverlap)
				{
					_visibleElements.Add(new DateElement(child, date, dateEnd, num));
				}
				else
				{
					_visibleElements.Add(new DateElement(child, date, dateEnd));
				}
			}
			num++;
		}
		_visibleElements.Sort();
		foreach (DateElement visibleElement in _visibleElements)
		{
			DateTime date2 = GetDate(visibleElement.Element);
			DateTime dateEnd2 = GetDateEnd(visibleElement.Element);
			if (Orientation == Orientation.Vertical)
			{
				visibleElement.PlacementRectangle.Y = CalculateTimelineOffset(date2, availableSize.Height);
				if (dateEnd2 > DateTime.MinValue && dateEnd2 > date2)
				{
					visibleElement.PlacementRectangle.Height = CalculateTimelineOffset(dateEnd2, availableSize.Height) - CalculateTimelineOffset(date2, availableSize.Height);
				}
				else
				{
					visibleElement.PlacementRectangle.Height = visibleElement.Element.DesiredSize.Height;
				}
				switch (OverlapBehavior)
				{
				case OverlapBehavior.None:
					visibleElement.PlacementRectangle.X = 0.0;
					visibleElement.PlacementRectangle.Width = visibleElement.Element.DesiredSize.Width;
					break;
				case OverlapBehavior.Hide:
					list.Clear();
					foreach (DateElement visibleElement2 in _visibleElements)
					{
						if (visibleElement == visibleElement2)
						{
							break;
						}
						Rect placementRectangle9 = visibleElement.PlacementRectangle;
						Rect placementRectangle10 = visibleElement2.PlacementRectangle;
						if (placementRectangle9.Top >= placementRectangle10.Top && placementRectangle9.Top < placementRectangle10.Bottom)
						{
							list.Add(visibleElement2);
						}
					}
					if (list.Count > 0)
					{
						visibleElement.PlacementRectangle.X = 0.0;
						visibleElement.PlacementRectangle.Y = 0.0;
						visibleElement.PlacementRectangle.Width = 0.0;
						visibleElement.PlacementRectangle.Height = 0.0;
					}
					else
					{
						visibleElement.PlacementRectangle.X = 0.0;
						visibleElement.PlacementRectangle.Width = visibleElement.Element.DesiredSize.Width;
					}
					break;
				case OverlapBehavior.Stretch:
				{
					list.Clear();
					foreach (DateElement visibleElement3 in _visibleElements)
					{
						if (visibleElement == visibleElement3)
						{
							break;
						}
						Rect placementRectangle5 = visibleElement.PlacementRectangle;
						Rect placementRectangle6 = visibleElement3.PlacementRectangle;
						if (placementRectangle5.Top >= placementRectangle6.Top && placementRectangle5.Top < placementRectangle6.Bottom)
						{
							list.Add(visibleElement3);
						}
					}
					list.Sort(CompareElementsByLeft);
					double x = 0.0;
					double num2 = availableSize.Width;
					if (list.Count > 0)
					{
						bool flag = false;
						for (int j = 0; j < list.Count; j++)
						{
							Rect placementRectangle7 = list[j].PlacementRectangle;
							if (j == 0 && placementRectangle7.Left > 0.0)
							{
								x = 0.0;
								num2 = placementRectangle7.Left;
								flag = true;
								break;
							}
							if (j == list.Count - 1)
							{
								break;
							}
							Rect placementRectangle8 = list[j + 1].PlacementRectangle;
							if (placementRectangle8.Left - placementRectangle7.Right > 0.0)
							{
								x = placementRectangle7.Right;
								num2 = placementRectangle8.Left - placementRectangle7.Right;
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							num2 = Math.Min(availableSize.Width / (double)(list.Count + 1), list[0].PlacementRectangle.Width);
							x = 0.0;
							foreach (DateElement item in list)
							{
								item.PlacementRectangle.Width = num2;
								item.PlacementRectangle.X = x;
								x += num2;
							}
						}
					}
					visibleElement.PlacementRectangle.X = x;
					if (double.IsPositiveInfinity(num2))
					{
						visibleElement.PlacementRectangle.Width = visibleElement.Element.DesiredSize.Width;
					}
					else
					{
						visibleElement.PlacementRectangle.Width = num2;
					}
					break;
				}
				case OverlapBehavior.Stack:
				{
					list.Clear();
					foreach (DateElement visibleElement4 in _visibleElements)
					{
						if (visibleElement == visibleElement4)
						{
							break;
						}
						Rect placementRectangle = visibleElement.PlacementRectangle;
						Rect placementRectangle2 = visibleElement4.PlacementRectangle;
						if (placementRectangle.Top >= placementRectangle2.Top && placementRectangle.Top < placementRectangle2.Bottom)
						{
							list.Add(visibleElement4);
						}
					}
					list.Sort(CompareElementsByLeft);
					double x = 0.0;
					visibleElement.PlacementRectangle.Width = visibleElement.Element.DesiredSize.Width;
					for (int i = 0; i < list.Count; i++)
					{
						Rect placementRectangle3 = list[i].PlacementRectangle;
						if (i == 0 && placementRectangle3.Left >= visibleElement.PlacementRectangle.Width)
						{
							x = 0.0;
							break;
						}
						if (i == list.Count - 1)
						{
							x = placementRectangle3.Right;
							break;
						}
						Rect placementRectangle4 = list[i + 1].PlacementRectangle;
						if (placementRectangle4.Left - placementRectangle3.Right >= visibleElement.PlacementRectangle.Width)
						{
							x = placementRectangle3.Right;
							break;
						}
					}
					visibleElement.PlacementRectangle.X = x;
					break;
				}
				}
				continue;
			}
			visibleElement.PlacementRectangle.X = CalculateTimelineOffset(date2, availableSize.Width);
			if (dateEnd2 > DateTime.MinValue && dateEnd2 > date2)
			{
				visibleElement.PlacementRectangle.Width = CalculateTimelineOffset(dateEnd2, availableSize.Width) - CalculateTimelineOffset(date2, availableSize.Width);
			}
			else
			{
				visibleElement.PlacementRectangle.Width = visibleElement.Element.DesiredSize.Width;
			}
			switch (OverlapBehavior)
			{
			case OverlapBehavior.None:
				visibleElement.PlacementRectangle.Y = 0.0;
				visibleElement.PlacementRectangle.Height = visibleElement.Element.DesiredSize.Height;
				break;
			case OverlapBehavior.Hide:
				list.Clear();
				foreach (DateElement visibleElement5 in _visibleElements)
				{
					if (visibleElement == visibleElement5)
					{
						break;
					}
					Rect placementRectangle19 = visibleElement.PlacementRectangle;
					Rect placementRectangle20 = visibleElement5.PlacementRectangle;
					if (placementRectangle19.Left >= placementRectangle20.Left && placementRectangle19.Left < placementRectangle20.Right)
					{
						list.Add(visibleElement5);
					}
				}
				if (list.Count > 0)
				{
					visibleElement.PlacementRectangle.X = 0.0;
					visibleElement.PlacementRectangle.Y = 0.0;
					visibleElement.PlacementRectangle.Width = 0.0;
					visibleElement.PlacementRectangle.Height = 0.0;
				}
				else
				{
					visibleElement.PlacementRectangle.Y = 0.0;
					visibleElement.PlacementRectangle.Height = visibleElement.Element.DesiredSize.Height;
				}
				break;
			case OverlapBehavior.Stretch:
			{
				list.Clear();
				foreach (DateElement visibleElement6 in _visibleElements)
				{
					if (visibleElement == visibleElement6)
					{
						break;
					}
					Rect placementRectangle15 = visibleElement.PlacementRectangle;
					Rect placementRectangle16 = visibleElement6.PlacementRectangle;
					if (placementRectangle15.Left >= placementRectangle16.Left && placementRectangle15.Left < placementRectangle16.Right)
					{
						list.Add(visibleElement6);
					}
				}
				list.Sort(CompareElementsByTop);
				double y = 0.0;
				double num3 = availableSize.Height;
				if (list.Count > 0)
				{
					bool flag2 = false;
					for (int l = 0; l < list.Count; l++)
					{
						Rect placementRectangle17 = list[l].PlacementRectangle;
						if (l == 0 && placementRectangle17.Top > 0.0)
						{
							y = 0.0;
							num3 = placementRectangle17.Top;
							flag2 = true;
							break;
						}
						if (l == list.Count - 1)
						{
							break;
						}
						Rect placementRectangle18 = list[l + 1].PlacementRectangle;
						if (placementRectangle18.Top - placementRectangle17.Bottom > 0.0)
						{
							y = placementRectangle17.Bottom;
							num3 = placementRectangle18.Top - placementRectangle17.Bottom;
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						num3 = Math.Min(availableSize.Height / (double)(list.Count + 1), list[0].PlacementRectangle.Height);
						y = 0.0;
						foreach (DateElement item2 in list)
						{
							item2.PlacementRectangle.Height = num3;
							item2.PlacementRectangle.Y = y;
							y += num3;
						}
					}
				}
				visibleElement.PlacementRectangle.Y = y;
				if (double.IsPositiveInfinity(num3))
				{
					visibleElement.PlacementRectangle.Height = visibleElement.Element.DesiredSize.Height;
				}
				else
				{
					visibleElement.PlacementRectangle.Height = num3;
				}
				break;
			}
			case OverlapBehavior.Stack:
			{
				list.Clear();
				foreach (DateElement visibleElement7 in _visibleElements)
				{
					if (visibleElement == visibleElement7)
					{
						break;
					}
					Rect placementRectangle11 = visibleElement.PlacementRectangle;
					Rect placementRectangle12 = visibleElement7.PlacementRectangle;
					if (placementRectangle11.Left >= placementRectangle12.Left && placementRectangle11.Left < placementRectangle12.Right)
					{
						list.Add(visibleElement7);
					}
				}
				list.Sort(CompareElementsByTop);
				double y = 0.0;
				visibleElement.PlacementRectangle.Height = visibleElement.Element.DesiredSize.Height;
				for (int k = 0; k < list.Count; k++)
				{
					Rect placementRectangle13 = list[k].PlacementRectangle;
					if (k == 0 && placementRectangle13.Top >= visibleElement.PlacementRectangle.Height)
					{
						y = 0.0;
						break;
					}
					if (k == list.Count - 1)
					{
						y = placementRectangle13.Bottom;
						break;
					}
					Rect placementRectangle14 = list[k + 1].PlacementRectangle;
					if (placementRectangle14.Top - placementRectangle13.Bottom >= visibleElement.PlacementRectangle.Height)
					{
						y = placementRectangle13.Bottom;
						break;
					}
				}
				visibleElement.PlacementRectangle.Y = y;
				break;
			}
			}
		}
	}

	private static bool AreVirtuallyEqual(double d1, double d2)
	{
		if (double.IsPositiveInfinity(d1))
		{
			return double.IsPositiveInfinity(d2);
		}
		if (double.IsNegativeInfinity(d1))
		{
			return double.IsNegativeInfinity(d2);
		}
		if (double.IsNaN(d1))
		{
			return double.IsNaN(d2);
		}
		double num = d1 - d2;
		double num2 = (Math.Abs(d1) + Math.Abs(d2) + 10.0) * 1E-15;
		if (0.0 - num2 < num)
		{
			return num2 > num;
		}
		return false;
	}

	private static bool AreVirtuallyEqual(Size s1, Size s2)
	{
		if (AreVirtuallyEqual(s1.Width, s2.Width))
		{
			return AreVirtuallyEqual(s1.Height, s2.Height);
		}
		return false;
	}

	private static bool AreVirtuallyEqual(Vector v1, Vector v2)
	{
		if (AreVirtuallyEqual(v1.X, v2.X))
		{
			return AreVirtuallyEqual(v1.Y, v2.Y);
		}
		return false;
	}

	public void LineDown()
	{
		SetVerticalOffset(VerticalOffset + ((Orientation == Orientation.Vertical) ? 1.0 : 16.0));
	}

	public void LineLeft()
	{
		SetHorizontalOffset(HorizontalOffset - ((Orientation == Orientation.Horizontal) ? 1.0 : 16.0));
	}

	public void LineRight()
	{
		SetHorizontalOffset(HorizontalOffset + ((Orientation == Orientation.Horizontal) ? 1.0 : 16.0));
	}

	public void LineUp()
	{
		SetVerticalOffset(VerticalOffset - ((Orientation == Orientation.Vertical) ? 1.0 : 16.0));
	}

	public Rect MakeVisible(Visual visual, Rect rectangle)
	{
		return rectangle;
	}

	public void MouseWheelDown()
	{
		SetVerticalOffset(VerticalOffset + (double)SystemParameters.WheelScrollLines * ((Orientation == Orientation.Vertical) ? 1.0 : 16.0));
	}

	public void MouseWheelLeft()
	{
		SetHorizontalOffset(HorizontalOffset - 3.0 * ((Orientation == Orientation.Horizontal) ? 1.0 : 16.0));
	}

	public void MouseWheelRight()
	{
		SetHorizontalOffset(HorizontalOffset + 3.0 * ((Orientation == Orientation.Horizontal) ? 1.0 : 16.0));
	}

	public void MouseWheelUp()
	{
		SetVerticalOffset(VerticalOffset - (double)SystemParameters.WheelScrollLines * ((Orientation == Orientation.Vertical) ? 1.0 : 16.0));
	}

	public void PageDown()
	{
		SetVerticalOffset(VerticalOffset + ViewportHeight);
	}

	public void PageLeft()
	{
		SetHorizontalOffset(HorizontalOffset - ViewportWidth);
	}

	public void PageRight()
	{
		SetHorizontalOffset(HorizontalOffset + ViewportWidth);
	}

	public void PageUp()
	{
		SetVerticalOffset(VerticalOffset - ViewportHeight);
	}

	public void SetHorizontalOffset(double offset)
	{
		offset = ValidateInputOffset(offset, "HorizontalOffset");
		if (!AreVirtuallyEqual(offset, _offset.X))
		{
			_offset.X = offset;
			InvalidateMeasure();
		}
	}

	public void SetVerticalOffset(double offset)
	{
		offset = ValidateInputOffset(offset, "VerticalOffset");
		if (!AreVirtuallyEqual(offset, _offset.Y))
		{
			_offset.Y = offset;
			InvalidateMeasure();
		}
	}
}
