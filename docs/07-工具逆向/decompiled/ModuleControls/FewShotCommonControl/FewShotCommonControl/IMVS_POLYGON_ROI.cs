using System.Runtime.InteropServices;

namespace FewShotCommonControl;

public struct IMVS_POLYGON_ROI
{
	public int nPointCount;

	public int nRoiType;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.Struct)]
	public HKA_POINT_F[] stPoints;
}
