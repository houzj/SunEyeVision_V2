using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ShapeDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ShapeDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
