using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct MVCC_STRINGVALUE
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
	public byte[] chCurValue;

	public long nMaxLength;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
	public uint[] nReserved;
}
