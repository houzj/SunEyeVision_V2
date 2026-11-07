using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSCircleFindModuCs;

/// \addtogroup 圆查找
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class CircleFindRoiManager
{
	private RoiManager moduRoiManager;

	public RectBox RoiRectangle
	{
		set
		{
			moduRoiManager.SetInheritRoi(value);
		}
	}

	public Annulus RoiAnnulus
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
	public CircleFindRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public CircleFindRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}
}
