using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

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
