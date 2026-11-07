using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct SVC2500_IO_OUT_PARAM
{
	public int nDeviceIndex;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszUuid;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput1;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput2;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput3;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput4;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput5;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput6;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput7;

	public SVC2500_IO_PARAM_SINGLE_OUTPUT stOutput8;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
