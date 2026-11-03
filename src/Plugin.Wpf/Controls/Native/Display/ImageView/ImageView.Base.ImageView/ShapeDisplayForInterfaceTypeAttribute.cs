using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView;

public class ShapeDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ShapeDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
