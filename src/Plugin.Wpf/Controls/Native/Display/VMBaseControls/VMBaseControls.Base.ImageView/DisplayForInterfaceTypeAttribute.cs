using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class DisplayForInterfaceTypeAttribute : Attribute
{
	private Type _interfaceType;

	public Type InterfaceType => _interfaceType;

	public DisplayForInterfaceTypeAttribute(Type interfaceType)
	{
		if (interfaceType == null)
		{
			throw new ArgumentNullException("interfaceType");
		}
		if (!interfaceType.IsInterface)
		{
			throw new ArgumentException("interfaceType must be a interface");
		}
		_interfaceType = interfaceType;
	}
}
