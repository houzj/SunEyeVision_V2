using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSDivideImageModuCs;

/// \addtogroup 划片拆分
/// @{
/// <summary>
/// @~chinese 划片拆分结果 @~english Divide Image Result
/// </summary>
public class DivideImageResult : CModuleResultBase
{
	/// @endcond
	/// <summary>
	/// @~chinese 模块状态 @~english ModuStatus
	/// </summary>
	public int ModuStatus { get; internal set; }

	/// <summary>
	/// @~chinese 检测区域 @~english ROI
	/// </summary>
	public RectBox ROI { get; internal set; }

	/// <summary>
	/// @~chinese 分片矩形 @~english Divide Rect
	/// </summary>
	public List<RectBox> DivideRect { get; internal set; }

	/// <summary>
	/// @~chinese 划片个数 @~english Divide Count
	/// </summary>
	public int DivideCount { get; internal set; }

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public DivideImageResult()
	{
		ModuStatus = 0;
		ROI = new RectBox();
		DivideRect = new List<RectBox>();
		DivideCount = 0;
	}
}
