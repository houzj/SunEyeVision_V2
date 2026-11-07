using VM.PlatformSDKCS;

namespace IMVSCaliperModuCs;

/// \addtogroup 卡尺工具
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class CaliperRoiManager
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

	/// @cond
	/// <summary>
	/// 构造函数
	/// </summary>
	public CaliperRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public CaliperRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}
}
