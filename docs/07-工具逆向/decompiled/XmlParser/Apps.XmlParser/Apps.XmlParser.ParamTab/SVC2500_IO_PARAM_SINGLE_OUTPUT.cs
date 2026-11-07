using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct SVC2500_IO_PARAM_SINGLE_OUTPUT
{
	public int nIndex;

	public int nSource;

	[MarshalAs(UnmanagedType.I1)]
	public bool bInvert;

	[MarshalAs(UnmanagedType.I1)]
	public bool bStatus;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
	public string chReserved;
}
