using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_LIGHT_DEVICE_LIST
{
	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.Struct)]
	public IMVS_LIGHT_DEVICE[] stLightDevice;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
