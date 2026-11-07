using System.Runtime.InteropServices;

namespace FewShotCommonControl;

public struct IMVS_POLYGON_ROI_LIST
{
	public int nCount;

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10, ArraySubType = UnmanagedType.Struct)]
	public IMVS_POLYGON_ROI[] stPolygonROIs;
}
