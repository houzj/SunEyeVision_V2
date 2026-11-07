using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IO_PARAM
{
	public uint nOutType;

	public uint nOutputPolarity;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20, ArraySubType = UnmanagedType.U4)]
	public uint[] nDurationTime;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20, ArraySubType = UnmanagedType.U4)]
	public uint[] nElectricalLevel;

	public uint nSendInterval;

	public bool nPollingEnable;

	public uint nPollingInterval;

	public uint nDelayTime;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U4)]
	public int[] nReserved;
}
