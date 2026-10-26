using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class ROIDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ROIDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
