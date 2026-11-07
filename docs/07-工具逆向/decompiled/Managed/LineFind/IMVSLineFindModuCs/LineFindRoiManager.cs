using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSLineFindModuCs;

/// \addtogroup 直线查找
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class LineFindRoiManager
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

	/// @cond
	/// <summary>
	/// 构造函数
	/// </summary>
	public LineFindRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public LineFindRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}
}
