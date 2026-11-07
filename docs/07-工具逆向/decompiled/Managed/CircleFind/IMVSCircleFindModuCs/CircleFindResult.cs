using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSCircleFindModuCs;

/// \addtogroup 圆查找
/// @{
/// <summary>
/// @~chinese 圆查找结果 @~english Circle Search Result
/// </summary>
public class CircleFindResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 输出圆 @~english OutputCircle
	/// </summary>
	public Circle OutputCircle { get; internal set; }

	/// <summary>
	/// @~chinese 输出圆环 @~english OutputCircleAnnulus
	/// </summary>
	public Annulus OutputCircleAnnulus { get; internal set; }

	/// <summary>
	/// @~chinese 拟合误差 @~english Fit Error
	/// </summary>
	public float FitError { get; internal set; }

	/// <summary>
	/// @~chinese 圆轮廓点 @~english CircleCoutourPoint
	/// </summary>
	public List<PointF> CircleCoutourPoint { get; internal set; }

	/// <summary>
	/// @~chinese 轮廓点状态 @~english EdgePointStatus
	/// </summary>
	public List<int> EdgePointStatus { get; internal set; }

	/// <summary>
	/// @~chinese 边缘点个数 @~english EdgePointNum
	/// </summary>
	public int EdgePointNum { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// <summary>
	/// @~chinese ROI圆弧 @~english ROIAnnulus
	/// </summary>
	public Annulus ROIAnnulus { get; internal set; }

	/// <summary>
	/// @~chinese 卡尺框 @~english Caliper Box
	/// </summary>
	public List<RectBox> CaliperBox { get; internal set; }

	/// <summary>
	/// @~chinese 卡尺框检测区 @~english Detect Caliper Box
	/// </summary>
	public List<RectBox> DetectCaliperBox { get; internal set; }

	/// <summary>
	/// @~chinese 输出圆弧 @~english OutputAnnulus
	/// </summary>
	public Annulus OutputAnnulus { get; internal set; }

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
	public CircleFindResult()
	{
		ModuStatus = 0;
		OutputCircle = new Circle();
		OutputCircleAnnulus = new Annulus();
		FitError = 0f;
		CircleCoutourPoint = new List<PointF>();
		EdgePointStatus = new List<int>();
		EdgePointNum = 0;
		ROI = new RectBox();
		ROIAnnulus = new Annulus();
		CaliperBox = new List<RectBox>();
		DetectCaliperBox = new List<RectBox>();
		OutputAnnulus = new Annulus();
		BlindPolygonPointNum = new List<int>();
		BlindPolygonPoints = new List<PointF>();
		BlindPolygonString = new List<string>();
	}
}
