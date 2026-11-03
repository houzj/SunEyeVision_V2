using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceControls.GlobalUI;

public struct GlobalTriggerData
{
	public GLOBAL_COMMAND_TYPE eCommandType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1352)]
	public byte[] uTriggerParam;
}
