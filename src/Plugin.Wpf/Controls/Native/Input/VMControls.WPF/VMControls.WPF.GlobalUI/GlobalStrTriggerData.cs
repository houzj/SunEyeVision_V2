using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public struct GlobalStrTriggerData
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
	public byte[] szTriggerString;

	public MATCH_MODE matchMode;

	public GlobalTriggerData tiggerData;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
	public byte[] nReserved;
}
