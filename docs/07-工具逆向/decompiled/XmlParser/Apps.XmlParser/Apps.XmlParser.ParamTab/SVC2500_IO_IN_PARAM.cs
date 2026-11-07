using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct SVC2500_IO_IN_PARAM
{
	public int nDeviceIndex;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszUuid;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput1;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput2;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput3;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput4;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput5;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput6;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput7;

	public SVC2500_IO_PARAM_SINGLE_INPUT stInput8;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
