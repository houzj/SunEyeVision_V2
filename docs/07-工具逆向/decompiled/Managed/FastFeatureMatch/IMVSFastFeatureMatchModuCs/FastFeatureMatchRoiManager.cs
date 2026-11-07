using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSFastFeatureMatchModuCs;

/// \addtogroup 快速匹配
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class FastFeatureMatchRoiManager
{
	private RoiManager moduRoiManager;

	public RectBox RoiRectangle
	{
		set
		{
			moduRoiManager.SetInheritRoi(value);
		}
	}

	public List<string> BlindPolygonString
	{
		set
		{
			moduRoiManager.SetInheritBlind(value);
		}
	}

	public ImageBaseData MaskImage
	{
		set
		{
			moduRoiManager.SetMaskImage(value);
		}
	}

	/// @cond
	/// <summary>
	/// 构造函数
	/// </summary>
	public FastFeatureMatchRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public FastFeatureMatchRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}
}
