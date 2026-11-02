using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

public class InvalidContentException : Exception
{
	public InvalidContentException(string message)
		: base(message)
	{
	}

	public InvalidContentException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
