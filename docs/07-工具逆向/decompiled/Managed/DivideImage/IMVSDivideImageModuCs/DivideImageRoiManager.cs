using VM.PlatformSDKCS;

namespace IMVSDivideImageModuCs;

/// \addtogroup 划片拆分
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class DivideImageRoiManager
{
	private RoiManager moduRoiManager;

	public RectBox RoiRectangle
	{
		set
		{
			moduRoiManager.SetInheritRoi(value);
		}
	}

	/// @cond
	/// <summary>
	/// 构造函数
	/// </summary>
	public DivideImageRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public DivideImageRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}
}
