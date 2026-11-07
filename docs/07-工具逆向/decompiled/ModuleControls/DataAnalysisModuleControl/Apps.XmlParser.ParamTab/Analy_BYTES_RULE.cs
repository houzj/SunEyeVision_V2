using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct Analy_BYTES_RULE
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
	public byte[] chName;

	public PARAM_VALUE_TYPE eParamType;

	public int nStartIndex;

	public int nEndIndex;

	public GW_ORDER_TYPE eOrderType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] byReserve;
}
