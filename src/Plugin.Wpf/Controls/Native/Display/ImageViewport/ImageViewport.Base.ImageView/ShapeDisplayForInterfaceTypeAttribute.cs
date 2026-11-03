using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView;

public class ShapeDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ShapeDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
