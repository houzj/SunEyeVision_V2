using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_DATAQUEUE_LIST
{
	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.Struct)]
	public IMVS_DATAQUEUE[] stDataQueue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
