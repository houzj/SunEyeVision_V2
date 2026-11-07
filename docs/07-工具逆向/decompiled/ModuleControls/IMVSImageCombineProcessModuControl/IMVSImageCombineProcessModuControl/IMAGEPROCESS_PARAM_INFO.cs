using System.Runtime.InteropServices;

namespace IMVSImageCombineProcessModuControl;

public struct IMAGEPROCESS_PARAM_INFO
{
	public int nAlgorithmIndex;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
	public string chszValue;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
