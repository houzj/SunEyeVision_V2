using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSBinaryModuCs;

/// \addtogroup 图像二值化
/// @{
/// <summary>
/// @~chinese 图像二值化结果 @~english Image Binarization Result
/// </summary>
public class ImageBinaryResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 输出图像 @~english OutputImage
	/// </summary>
	public ImageBaseData OutputImage { get; internal set; }

	/// <summary>
	/// @~chinese 输出掩膜 @~english Output Mask
	/// </summary>
	public ImageBaseData OutputMask { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROIsBox
	/// </summary>
	public List<RectBox> ROIsBox { get; internal set; }

	/// <summary>
	/// @~chinese ROI圆弧 @~english ROIAnnulus
	/// </summary>
	public Annulus ROIAnnulus { get; internal set; }

	/// <summary>
	/// @~chinese ROI圆弧 @~english ROIAnnuluss
	/// </summary>
	public List<Annulus> ROIAnnuluss { get; internal set; }

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
	public ImageBinaryResult()
	{
		ModuStatus = 0;
		OutputImage = new ImageBaseData();
		OutputMask = new ImageBaseData();
		ROI = new RectBox();
		ROIsBox = new List<RectBox>();
		ROIAnnulus = new Annulus();
		ROIAnnuluss = new List<Annulus>();
		BlindPolygonPointNum = new List<int>();
		BlindPolygonPoints = new List<PointF>();
		BlindPolygonString = new List<string>();
	}
}
