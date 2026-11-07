using System.Runtime.InteropServices;

namespace VMControls.WPF.GlobalUI;

public struct DATA_CONFIG
{
	public eDATA_ORIGIN eDataOrigin;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
	public byte[] szValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] szReserve;
}
