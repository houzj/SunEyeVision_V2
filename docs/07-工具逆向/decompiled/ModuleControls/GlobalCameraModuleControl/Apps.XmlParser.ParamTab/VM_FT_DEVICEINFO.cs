using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct VM_FT_DEVICEINFO
{
	public int nDeviceType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
	public byte[] cDeviceName;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	public int[] nReserved;
}
