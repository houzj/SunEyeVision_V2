using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

public class PathDrawAlgorithmDef
{
	public enum MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE
	{
		MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_MIN = 1,
		MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_LINE = MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_MIN,
		MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_ARC = 2,
		MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_MAX = MVBEDGEINSP_TRAJ_GEO_ELEM_TYPE_ARC
	}

	public struct HKA_POINT_F
	{
		public float x;

		public float y;
	}

	public struct HKA_POINT_F_STATUS
	{
		public HKA_POINT_F point;

		public int status;
	}

	public struct MVB_ARC
	{
		public HKA_POINT_F center;

		public float radius;

		public float start_angle;

		public float angle_range;
	}

	public struct MVB_LINE_F
	{
		public HKA_POINT_F point_start;

		public HKA_POINT_F point_end;

		public float angle;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct MVBEDGEINSP_TRAJ_UNION
	{
		[FieldOffset(0)]
		public MVB_LINE_F traj_line;

		[FieldOffset(0)]
		public MVB_ARC traj_arc;
	}

	public struct MVBEDGEINSP_TRAJ_GEO_ELEM
	{
		public MVBEDGEINSP_TRAJ_UNION traj_union;

		public int elem_type;
	}

	public struct EDGEINSP_TRAJ_LIST
	{
		public int tran_num;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
		public EDGEINSP_TRAJ[] tranj;
	}

	public struct EDGEINSP_TRAJ
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
		public MVBEDGEINSP_TRAJ_GEO_ELEM[] traj_elem;

		public int elem_num;
	}

	public struct MVBI_BOX_F
	{
		public float center_x;

		public float center_y;

		public float width;

		public float height;

		public float angle;
	}

	public struct PATH_EDGE_POINT_RESULT
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10000)]
		public HKA_POINT_F_STATUS[] edge_pts;

		public int edge_pts_num;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10000)]
		public MVBI_BOX_F[] caliper_box;

		public int center_box_num;
	}

	public const int MVBEDGEINSP_MAX_TRAJ_POLY_POINTS_NUM = 2048;

	public const int MVBEDGEINSP_MAX_MUL_TRAJ_POLY_NUM = 10;

	public const int MVBEDGEINSP_MAX_CALIPER_NUM = 10000;
}
