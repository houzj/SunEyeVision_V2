using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSBlobFindModuCs;

/// \addtogroup Blob分析
/// @{
/// <summary>
/// @~chinese Blob分析结果 @~english Blob Analysis Result
/// </summary>
public class BlobFindResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 单体状态 @~english BlobStatus
	/// </summary>
	public List<int> BlobStatus { get; internal set; }

	/// <summary>
	/// @~chinese Blob个数 @~english BlobNum
	/// </summary>
	public int BlobNum { get; internal set; }

	/// <summary>
	/// @~chinese 面积 @~english Area
	/// </summary>
	public List<float> Area { get; internal set; }

	/// <summary>
	/// @~chinese 总面积 @~english TotalArea
	/// </summary>
	public float TotalArea { get; internal set; }

	/// <summary>
	/// @~chinese 质心点 @~english CentroidPoint
	/// </summary>
	public List<PointF> CentroidPoint { get; internal set; }

	/// <summary>
	/// @~chinese 最小外接矩形 @~english MinBoudingRect
	/// </summary>
	public List<RectBox> MinBoudingRect { get; internal set; }

	/// <summary>
	/// @~chinese 最小灰度值 @~english MinGrayValue
	/// </summary>
	public List<float> MinGrayValue { get; internal set; }

	/// <summary>
	/// @~chinese 最大灰度值 @~english MaxGrayValue
	/// </summary>
	public List<float> MaxGrayValue { get; internal set; }

	/// <summary>
	/// @~chinese 对比度 @~english GrayContrast
	/// </summary>
	public List<float> GrayContrast { get; internal set; }

	/// <summary>
	/// @~chinese 最大内接矩形 @~english MaxAreaInnerRect
	/// </summary>
	public List<RectBox> MaxAreaInnerRect { get; internal set; }

	/// <summary>
	/// @~chinese Blob矩形框 @~english BlobRect
	/// </summary>
	public List<RectI> BlobRect { get; internal set; }

	/// <summary>
	/// @~chinese blob矩形左上点X @~english BlobRectUpLeftX
	/// </summary>
	public List<float> BlobRectUpLeftX { get; internal set; }

	/// <summary>
	/// @~chinese blob矩形左上点Y @~english BlobRectUpLeftY
	/// </summary>
	public List<float> BlobRectUpLeftY { get; internal set; }

	/// <summary>
	/// @~chinese blob矩形右下点X @~english BlobRectBottomRightX
	/// </summary>
	public List<float> BlobRectBottomRightX { get; internal set; }

	/// <summary>
	/// @~chinese blob矩形右下点Y @~english BlobRectBottomRightY
	/// </summary>
	public List<float> BlobRectBottomRightY { get; internal set; }

	/// <summary>
	/// @~chinese 主轴角度 @~english BlobAngle
	/// </summary>
	public List<float> BlobAngle { get; internal set; }

	/// <summary>
	/// @~chinese 周长 @~english Perimeter
	/// </summary>
	public List<float> Perimeter { get; internal set; }

	/// <summary>
	/// @~chinese 长轴 @~english LongAxis
	/// </summary>
	public List<float> LongAxis { get; internal set; }

	/// <summary>
	/// @~chinese 短轴 @~english ShortAxis
	/// </summary>
	public List<float> ShortAxis { get; internal set; }

	/// <summary>
	/// @~chinese 圆形度 @~english Circularity
	/// </summary>
	public List<float> Circularity { get; internal set; }

	/// <summary>
	/// @~chinese 矩形度 @~english Rectangularity
	/// </summary>
	public List<float> Rectangularity { get; internal set; }

	/// <summary>
	/// @~chinese 分数 @~english Score
	/// </summary>
	public List<float> Score { get; internal set; }

	/// <summary>
	/// @~chinese 二值化图像数据 @~english BinaryImageComb
	/// </summary>
	public ImageBaseData BinaryImageComb { get; internal set; }

	/// <summary>
	/// @~chinese Blob图像数据 @~english BlobImageComb
	/// </summary>
	public ImageBaseData BlobImageComb { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public List<RectBox> ROI { get; internal set; }

	/// <summary>
	/// @~chinese ROI圆弧 @~english ROIAnnulus
	/// </summary>
	public List<Annulus> ROIAnnulus { get; internal set; }

	/// <summary>
	/// @~chinese 轮廓信息 @~english ContourInfo
	/// </summary>
	public MatchOutline ContourInfo { get; internal set; }

	/// <summary>
	/// @~chinese 轮廓点个数 @~english ContourNum
	/// </summary>
	public int ContourNum { get; internal set; }

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
	public BlobFindResult()
	{
		ModuStatus = 0;
		BlobStatus = new List<int>();
		BlobNum = 0;
		Area = new List<float>();
		TotalArea = 0f;
		CentroidPoint = new List<PointF>();
		MinBoudingRect = new List<RectBox>();
		MinGrayValue = new List<float>();
		MaxGrayValue = new List<float>();
		GrayContrast = new List<float>();
		MaxAreaInnerRect = new List<RectBox>();
		BlobRect = new List<RectI>();
		BlobRectUpLeftX = new List<float>();
		BlobRectUpLeftY = new List<float>();
		BlobRectBottomRightX = new List<float>();
		BlobRectBottomRightY = new List<float>();
		BlobAngle = new List<float>();
		Perimeter = new List<float>();
		LongAxis = new List<float>();
		ShortAxis = new List<float>();
		Circularity = new List<float>();
		Rectangularity = new List<float>();
		Score = new List<float>();
		BinaryImageComb = new ImageBaseData();
		BlobImageComb = new ImageBaseData();
		ROI = new List<RectBox>();
		ROIAnnulus = new List<Annulus>();
		ContourInfo = new MatchOutline();
		ContourNum = 0;
		BlindPolygonPointNum = new List<int>();
		BlindPolygonPoints = new List<PointF>();
		BlindPolygonString = new List<string>();
		OutputMask = new ImageBaseData();
	}
}
