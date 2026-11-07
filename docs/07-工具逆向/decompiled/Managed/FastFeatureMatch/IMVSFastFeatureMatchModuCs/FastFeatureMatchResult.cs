using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSFastFeatureMatchModuCs;

/// \addtogroup 快速匹配
/// @{
/// <summary>
/// @~chinese 快速匹配结果 @~english Fast Match Result
/// </summary>
public class FastFeatureMatchResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 匹配状态 @~english MatchStatus
	/// </summary>
	public List<int> MatchStatus { get; internal set; }

	/// <summary>
	/// @~chinese 匹配个数 @~english MatchNum
	/// </summary>
	public int MatchNum { get; internal set; }

	/// <summary>
	/// @~chinese 匹配模板编号 @~english MatchModelIndex
	/// </summary>
	public List<int> MatchModelIndex { get; internal set; }

	/// <summary>
	/// @~chinese 匹配模板名称 @~english MatchModelName
	/// </summary>
	public List<string> MatchModelName { get; internal set; }

	/// <summary>
	/// @~chinese 匹配框 @~english MatchRect
	/// </summary>
	public List<RectBox> MatchRect { get; internal set; }

	/// <summary>
	/// @~chinese 匹配点 @~english Match Point
	/// </summary>
	public List<PointF> MatchPoint { get; internal set; }

	/// <summary>
	/// @~chinese 尺度 @~english MatchScale
	/// </summary>
	public List<float> MatchScale { get; internal set; }

	/// <summary>
	/// @~chinese 分数 @~english MatchScore
	/// </summary>
	public List<float> MatchScore { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// <summary>
	/// @~chinese 匹配轮廓信息 @~english MatchOutline
	/// </summary>
	public MatchOutline MatchOutline { get; internal set; }

	/// <summary>
	/// @~chinese 匹配轮廓点个数 @~english MatchOutlineNum
	/// </summary>
	public int MatchOutlineNum { get; internal set; }

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

	/// <summary>
	/// @~chinese 输出掩膜 @~english Output Mask
	/// </summary>
	public ImageBaseData OutputMask { get; internal set; }

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public FastFeatureMatchResult()
	{
		ModuStatus = 0;
		MatchStatus = new List<int>();
		MatchNum = 0;
		MatchModelIndex = new List<int>();
		MatchModelName = new List<string>();
		MatchRect = new List<RectBox>();
		MatchPoint = new List<PointF>();
		MatchScale = new List<float>();
		MatchScore = new List<float>();
		ROI = new RectBox();
		MatchOutline = new MatchOutline();
		MatchOutlineNum = 0;
		BlindPolygonPointNum = new List<int>();
		BlindPolygonPoints = new List<PointF>();
		BlindPolygonString = new List<string>();
		OutputMask = new ImageBaseData();
	}
}
