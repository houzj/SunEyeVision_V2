using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct LIGHT_CHANNEL_PARAM
{
	public int nChannelNum;

	public bool bChannelEnable;

	public int nLightBrightness;

	public int nLightState;

	public int nTriggerEdge;

	public int nDurationTime;

	public int nLightSource;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U4)]
	public int[] nReserved;
}
