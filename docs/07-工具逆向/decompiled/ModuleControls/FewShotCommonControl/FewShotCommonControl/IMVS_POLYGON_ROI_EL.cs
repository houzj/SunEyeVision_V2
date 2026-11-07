using System.Runtime.InteropServices;

namespace FewShotCommonControl;

public struct IMVS_POLYGON_ROI_EL
{
	public int nPointCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.Struct)]
	public HKA_POINT_F[] stPoints;
}
