using System;

namespace VMBaseControls.Base.ImageView;

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
