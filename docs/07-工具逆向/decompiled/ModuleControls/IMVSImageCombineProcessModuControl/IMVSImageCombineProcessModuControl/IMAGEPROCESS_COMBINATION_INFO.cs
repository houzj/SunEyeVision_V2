using System.Runtime.InteropServices;

namespace IMVSImageCombineProcessModuControl;

public struct IMAGEPROCESS_COMBINATION_INFO
{
	public int nProcessCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.Struct)]
	public IMAGEPROCESS_SINGLE_INFO[] stProcessInfo;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
