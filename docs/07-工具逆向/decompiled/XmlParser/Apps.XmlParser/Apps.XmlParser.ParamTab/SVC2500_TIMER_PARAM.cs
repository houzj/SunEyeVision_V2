using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct SVC2500_TIMER_PARAM
{
	public int nDeviceIndex;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszUuid;

	public SVC2500_TIMER_PARAM_SINGLE stTimer1;

	public SVC2500_TIMER_PARAM_SINGLE stTimer2;

	public SVC2500_TIMER_PARAM_SINGLE stTimer3;

	public SVC2500_TIMER_PARAM_SINGLE stTimer4;

	public SVC2500_TIMER_PARAM_SINGLE stTimer5;

	public SVC2500_TIMER_PARAM_SINGLE stTimer6;

	public SVC2500_TIMER_PARAM_SINGLE stTimer7;

	public SVC2500_TIMER_PARAM_SINGLE stTimer8;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
