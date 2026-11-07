using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct IMVS_DATAQUEUE
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256, ArraySubType = UnmanagedType.I1)]
	public byte[] chszQueueName;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
	public string chszDataType;

	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 131072, ArraySubType = UnmanagedType.I1)]
	public byte[] chszValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
