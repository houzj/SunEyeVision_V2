using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

[Flags]
public enum AllowedSpecialValues
{
	None = 0,
	NaN = 1,
	PositiveInfinity = 2,
	NegativeInfinity = 4,
	AnyInfinity = PositiveInfinity | NegativeInfinity,
	Any = AnyInfinity | NaN
}
