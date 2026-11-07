using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct MVCC_INTVALUE_EX
{
	public long nCurValue;

	public long nMax;

	public long nMin;

	public long nInc;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
	public uint[] nReserved;
}
