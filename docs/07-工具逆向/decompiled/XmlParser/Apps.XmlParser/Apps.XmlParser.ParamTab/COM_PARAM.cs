using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public struct COM_PARAM
{
	public uint nComNum;

	public uint nBaudRate;

	public uint nParity;

	public uint nDataBits;

	public uint nStopBits;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U4)]
	public int[] nReserved;
}
