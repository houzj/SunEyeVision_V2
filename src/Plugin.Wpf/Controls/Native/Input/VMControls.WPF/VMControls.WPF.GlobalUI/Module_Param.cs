using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public struct Module_Param
{
	public int nModuleId;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
	public byte[] szModuleParamName;
}
