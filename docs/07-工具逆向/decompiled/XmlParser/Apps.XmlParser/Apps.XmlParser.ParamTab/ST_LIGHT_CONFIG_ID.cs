using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct ST_LIGHT_CONFIG_ID
{
	public int nIndex;

	public int nDeviceType;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszDeviceGuid;

	public ST_LIGHT_CONFIG stLightConfig;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
