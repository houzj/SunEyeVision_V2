using System.Runtime.InteropServices;

namespace VMControls.WPF.GlobalUI;

public struct GlobalTriggerData
{
	public GLOBAL_COMMAND_TYPE eCommandType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1352)]
	public byte[] uTriggerParam;
}
