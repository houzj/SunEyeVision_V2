using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

internal sealed class DateElement : IComparable<DateElement>
{
	private readonly int _originalIndex;

	public readonly UIElement Element;

	public readonly DateTime Date;

	public readonly DateTime DateEnd;

	public Rect PlacementRectangle;

	public int Column;

	public int ColumnSpan = 1;

	internal DateElement(UIElement element, DateTime date, DateTime dateEnd)
		: this(element, date, dateEnd, -1)
	{
	}

	internal DateElement(UIElement element, DateTime date, DateTime dateEnd, int originalIndex)
	{
		Element = element;
		Date = date;
		DateEnd = dateEnd;
		_originalIndex = originalIndex;
	}

	public override string ToString()
	{
		if (!(Element is FrameworkElement frameworkElement))
		{
			return base.ToString();
		}
		if (frameworkElement.Tag != null)
		{
			return frameworkElement.Tag.ToString();
		}
		return frameworkElement.Name;
	}

	public int CompareTo(DateElement d)
	{
		int num = Date.CompareTo(d.Date);
		if (num != 0)
		{
			return num;
		}
		if (_originalIndex >= 0)
		{
			if (_originalIndex >= d._originalIndex)
			{
				return 1;
			}
			return -1;
		}
		return -DateEnd.CompareTo(d.DateEnd);
	}
}
