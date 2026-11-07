using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct Analy_INPUT_BYTES
{
	public int nNum;

	public int nAsciiToHex;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public Analy_BYTES_RULE[] stInputBytesRuleList;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public byte[] byReserve;
}
