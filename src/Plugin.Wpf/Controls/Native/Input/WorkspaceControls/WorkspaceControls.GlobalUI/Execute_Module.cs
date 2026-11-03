using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceControls.GlobalUI;

public struct Execute_Module
{
	public DATA_CONFIG stModuleId;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] szReserve;
}
