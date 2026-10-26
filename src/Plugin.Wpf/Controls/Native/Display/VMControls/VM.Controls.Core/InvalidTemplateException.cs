using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

public class InvalidTemplateException : Exception
{
	public InvalidTemplateException(string message)
		: base(message)
	{
	}

	public InvalidTemplateException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
