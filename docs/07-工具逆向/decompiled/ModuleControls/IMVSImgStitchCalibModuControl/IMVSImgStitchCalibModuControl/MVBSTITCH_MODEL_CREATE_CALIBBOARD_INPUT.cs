using System.Runtime.InteropServices;
using HikExternalCall.MvbOCR;

namespace IMVSImgStitchCalibModuControl;

public struct MVBSTITCH_MODEL_CREATE_CALIBBOARD_INPUT
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.Struct)]
	public HKA_IMAGE[] astImage;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100, ArraySubType = UnmanagedType.Struct)]
	public MVBSTITCH_CALIBBOARD_CFG[] astStitchConfig;

	public int nImgNum;

	public int nBoardType;

	public int nCornersOutputFlag;
}
