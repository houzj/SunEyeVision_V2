using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSLineFindModuCs;

/// \addtogroup 直线查找
/// @{
/// <summary>
/// @~chinese 直线查找结果 @~english Line Search Result
/// </summary>
public class LineFindResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 输出直线 @~english OutputLine
	/// </summary>
	public Line OutputLine { get; internal set; }

	/// <summary>
	/// @~chinese 中点 @~english Midpoint
	/// </summary>
	public PointF Midpoint { get; internal set; }

	/// <summary>
	/// @~chinese 直线角度 @~english LineAngle
	/// </summary>
	public float LineAngle { get; internal set; }

	/// <summary>
	/// @~chinese 拟合误差 @~english LineFitError
	/// </summary>
	public float LineFitError { get; internal set; }

	/// <summary>
	/// @~chinese 直线轮廓点 @~english Linear Contour Point
	/// </summary>
	public List<PointF> LinearContourPoint { get; internal set; }

	/// <summary>
	/// @~chinese 边缘点个数 @~english EdgePointNum
	/// </summary>
	public int EdgePointNum { get; internal set; }

	/// <summary>
	/// @~chinese 轮廓点状态 @~english EdgePointStatus
	/// </summary>
	public List<int> EdgePointStatus { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// <summary>
	/// @~chinese 卡尺框 @~english Caliper Box
	/// </summary>
	public List<RectBox> CaliperBox { get; internal set; }

	/// <summary>
	/// @~chinese 卡尺框检测区 @~english Detect Caliper Box
	/// </summary>
	public List<RectBox> DetectCaliperBox { get; internal set; }

	/// <summary>
	/// @~chinese 多边形点数 @~english PolygonPointNum
	/// </summary>
	public List<int> BlindPolygonPointNum { get; internal set; }

	/// <summary>
	/// @~chinese 多边形点集 @~english PolygonPoints
	/// </summary>
	public List<PointF> BlindPolygonPoints { get; internal set; }

	/// <summary>
	/// @~chinese 屏蔽区字符串 @~english BlindPolygonString
	/// </summary>
	public List<string> BlindPolygonString { get; internal set; }

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public LineFindResult()
	{
		ModuStatus = 0;
		OutputLine = new Line();
		Midpoint = new PointF();
		LineAngle = 0f;
		LineFitError = 0f;
		LinearContourPoint = new List<PointF>();
		EdgePointNum = 0;
		EdgePointStatus = new List<int>();
		ROI = new RectBox();
		CaliperBox = new List<RectBox>();
		DetectCaliperBox = new List<RectBox>();
		BlindPolygonPointNum = new List<int>();
		BlindPolygonPoints = new List<PointF>();
		BlindPolygonString = new List<string>();
	}
}
