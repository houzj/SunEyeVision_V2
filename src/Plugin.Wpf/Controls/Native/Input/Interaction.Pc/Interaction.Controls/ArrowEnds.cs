using SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.Controls;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Input.Interaction.Controls;

[Flags]
public enum ArrowEnds
{
	None = 0,
	Start = 1,
	End = 2,
	Both = Start | End
}
