using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public struct Load_Solution
{
	public DATA_CONFIG stFilePath;

	public DATA_CONFIG stPassward;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] szReserve;
}
