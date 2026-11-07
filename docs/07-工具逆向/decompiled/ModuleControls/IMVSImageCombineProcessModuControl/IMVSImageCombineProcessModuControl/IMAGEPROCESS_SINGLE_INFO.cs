using System.Runtime.InteropServices;

namespace IMVSImageCombineProcessModuControl;

public struct IMAGEPROCESS_SINGLE_INFO
{
	public int nParamCount;

	public int nTypeIndex;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.Struct)]
	public IMAGEPROCESS_PARAM_INFO[] stParamInfo;

	[MarshalAs(UnmanagedType.I1)]
	public bool bEnabled;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
	public int[] nReserved;
}
