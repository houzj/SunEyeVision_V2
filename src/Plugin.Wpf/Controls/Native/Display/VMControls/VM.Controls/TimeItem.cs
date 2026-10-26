using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

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
