using System.Runtime.InteropServices;

namespace FewShotCommonControl;

public struct IMVS_POLYGON_ROI_LIST_EL
{
	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 500, ArraySubType = UnmanagedType.Struct)]
	public IMVS_POLYGON_ROI_EL[] stPolygonROIs;
}
