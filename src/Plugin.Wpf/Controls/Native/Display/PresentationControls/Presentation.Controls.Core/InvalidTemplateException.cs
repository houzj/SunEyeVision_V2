using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

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
