using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView;

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
