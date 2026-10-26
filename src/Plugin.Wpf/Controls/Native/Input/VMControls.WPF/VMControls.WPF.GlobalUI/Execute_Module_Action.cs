using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public struct Execute_Module_Action
{
	public int nModuleId;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
	public byte[] szParamName;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
	public byte[] szParamValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] szReserver;
}
