using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSCaliperModuCs;

/// \addtogroup 卡尺工具
/// @{
/// <summary>
/// @~chinese 卡尺工具结果 @~english Caliper Result
/// </summary>
public class CaliperResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 边缘状态 @~english EdgeStatus
	/// </summary>
	public List<int> EdgeStatus { get; internal set; }

	/// <summary>
	/// @~chinese 边缘个数 @~english CaliperEdgeNum
	/// </summary>
	public int CaliperEdgeNum { get; internal set; }

	/// <summary>
	/// @~chinese 分数 @~english CaliperEdgeScore
	/// </summary>
	public List<float> CaliperEdgeScore { get; internal set; }

	/// <summary>
	/// @~chinese 边缘0极性 @~english CaliperEdge0Polarity
	/// </summary>
	public List<int> CaliperEdge0Polarity { get; internal set; }

	/// <summary>
	/// @~chinese 边缘0位置 @~english CaliperEdge0Bias
	/// </summary>
	public List<float> CaliperEdge0Bias { get; internal set; }

	/// <summary>
	/// @~chinese 边缘点0 @~english Edge Point 0
	/// </summary>
	public List<PointF> EdgePoint0 { get; internal set; }

	/// <summary>
	/// @~chinese 边缘1极性 @~english CaliperEdge1Polarity
	/// </summary>
	public List<int> CaliperEdge1Polarity { get; internal set; }

	/// <summary>
	/// @~chinese 边缘1位置 @~english CaliperEdge1Bias
	/// </summary>
	public List<float> CaliperEdge1Bias { get; internal set; }

	/// <summary>
	/// @~chinese 边缘点1 @~english Edge Point 1
	/// </summary>
	public List<PointF> EdgePoint1 { get; internal set; }

	/// <summary>
	/// @~chinese 测量宽度 @~english CaliperEdgePairWidth
	/// </summary>
	public List<float> CaliperEdgePairWidth { get; internal set; }

	/// <summary>
	/// @~chinese 直线0起点 @~english Line0StartPoint
	/// </summary>
	public List<PointF> Line0StartPoint { get; internal set; }

	/// <summary>
	/// @~chinese 直线0终点 @~english Line0EndPoint
	/// </summary>
	public List<PointF> Line0EndPoint { get; internal set; }

	/// <summary>
	/// @~chinese 直线0角度 @~english Line0Angle
	/// </summary>
	public List<float> Line0Angle { get; internal set; }

	/// <summary>
	/// @~chinese 直线1起点 @~english Line1StartPoint
	/// </summary>
	public List<PointF> Line1StartPoint { get; internal set; }

	/// <summary>
	/// @~chinese 直线1终点 @~english Line1EndPoint
	/// </summary>
	public List<PointF> Line1EndPoint { get; internal set; }

	/// <summary>
	/// @~chinese 直线1角度 @~english Line1Angle
	/// </summary>
	public List<float> Line1Angle { get; internal set; }

	/// <summary>
	/// @~chinese 圆弧0 @~english Annulus0
	/// </summary>
	public List<Annulus> Annulus0 { get; internal set; }

	/// <summary>
	/// @~chinese 圆弧1 @~english Annulus1
	/// </summary>
	public List<Annulus> Annulus1 { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public CaliperResult()
	{
		ModuStatus = 0;
		EdgeStatus = new List<int>();
		CaliperEdgeNum = 0;
		CaliperEdgeScore = new List<float>();
		CaliperEdge0Polarity = new List<int>();
		CaliperEdge0Bias = new List<float>();
		EdgePoint0 = new List<PointF>();
		CaliperEdge1Polarity = new List<int>();
		CaliperEdge1Bias = new List<float>();
		EdgePoint1 = new List<PointF>();
		CaliperEdgePairWidth = new List<float>();
		Line0StartPoint = new List<PointF>();
		Line0EndPoint = new List<PointF>();
		Line0Angle = new List<float>();
		Line1StartPoint = new List<PointF>();
		Line1EndPoint = new List<PointF>();
		Line1Angle = new List<float>();
		Annulus0 = new List<Annulus>();
		Annulus1 = new List<Annulus>();
		ROI = new RectBox();
	}
}
