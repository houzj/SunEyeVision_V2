using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

internal struct MVCC_ENUMVALUE
{
	public uint nCurValue;

	public uint nSupportedNum;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	public uint[] nSupportValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	public uint[] nReserved;
}
