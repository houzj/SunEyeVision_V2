using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

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
