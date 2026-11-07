using System;

namespace VMBaseControls.Base.ImageView;

public class ShapeDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ShapeDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
