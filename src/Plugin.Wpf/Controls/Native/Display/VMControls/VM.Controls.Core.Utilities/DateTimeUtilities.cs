using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class DateTimeUtilities
{
	public static DateTime GetContextNow(DateTimeKind kind)
	{
		return kind switch
		{
			DateTimeKind.Unspecified => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), 
			DateTimeKind.Utc => DateTime.UtcNow, 
			_ => DateTime.Now, 
		};
	}

	public static bool IsSameDate(DateTime? date1, DateTime? date2)
	{
		if (!date1.HasValue || !date2.HasValue)
		{
			return false;
		}
		return date1.Value.Date == date2.Value.Date;
	}
}
