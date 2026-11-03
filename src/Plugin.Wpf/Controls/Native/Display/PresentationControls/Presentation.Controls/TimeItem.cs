using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class TimeItem
{
	public string Display { get; set; }

	public TimeSpan Time { get; set; }

	public TimeItem(string display, TimeSpan time)
	{
		Display = display;
		Time = time;
	}

	public override bool Equals(object obj)
	{
		if (obj is TimeItem timeItem)
		{
			return Time == timeItem.Time;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Time.GetHashCode();
	}
}
