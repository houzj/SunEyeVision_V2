using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_IMAGE_CONFIG_LIST
{
	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.Struct)]
	public IMVS_IMAGE_CONFIG[] stImageConfig;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
