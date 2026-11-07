using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct SVC2500_TIMER_PARAM_SINGLE
{
	public int nIndex;

	public int nDuration;

	public int nDelay;

	public int nMode;

	public int nBurstCount;

	public int nTriggerSource;

	public int nTriggerActivation;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
	public string chReserved;
}
