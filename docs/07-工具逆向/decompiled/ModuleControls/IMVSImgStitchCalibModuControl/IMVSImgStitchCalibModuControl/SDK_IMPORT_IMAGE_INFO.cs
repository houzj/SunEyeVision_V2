using System.Runtime.InteropServices;

namespace IMVSImgStitchCalibModuControl;

public struct SDK_IMPORT_IMAGE_INFO
{
	public int nImgNum;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.Struct)]
	public int[] nImgWidth;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.Struct)]
	public int[] nImgHeight;
}
