using System;

namespace VMBaseControls.Base.ImageView;

public class ROIDisplayForInterfaceTypeAttribute : DisplayForInterfaceTypeAttribute
{
	public ROIDisplayForInterfaceTypeAttribute(Type interfaceType)
		: base(interfaceType)
	{
	}
}
