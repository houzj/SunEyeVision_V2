using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct MVCC_FLOATVALUE
{
	public float fCurValue;

	public float fMax;

	public float fMin;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	public uint[] nReserved;
}
