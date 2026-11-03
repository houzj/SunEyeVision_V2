using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

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
