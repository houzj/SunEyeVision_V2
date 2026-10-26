using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public struct Execute_Procedure
{
	public DATA_CONFIG stProcedureId;

	public DATA_CONFIG stTriggerStr;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] szReserve;
}
