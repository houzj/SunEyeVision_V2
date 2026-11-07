using System.Runtime.InteropServices;

namespace VMControls.WPF.GlobalUI;

public struct Module_Input
{
	public int nModuleId;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
	public byte[] szModuleInputName;
}
