using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_LIGHT_DEVICE
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256, ArraySubType = UnmanagedType.I1)]
	public byte[] chszBrandName;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256, ArraySubType = UnmanagedType.I1)]
	public byte[] chszDeviceName;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszDeviceGuid;

	public int nIndex;

	public int nDeviceType;

	[MarshalAs(UnmanagedType.I1)]
	public bool bConnected;

	[MarshalAs(UnmanagedType.I1)]
	public bool bReConnect;

	public COM_PARAM stComParam;

	public IO_PARAM stIOPARAM;

	public long pcLightDevice;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
