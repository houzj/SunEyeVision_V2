using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSBlobFindModuCs;

/// \addtogroup Blob分析
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class BlobFindRoiManager
{
	private RoiManager moduRoiManager;

	public List<RectBox> RoiRectangle
	{
		set
		{
			SetRect4BlobFind(value);
		}
	}

	public List<Annulus> RoiAnnulus
	{
		set
		{
			SetAnnulus4BlobFind(value);
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
	public BlobFindRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public BlobFindRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}

	private void SetRect4BlobFind(List<RectBox> roiValueList)
	{
		if (roiValueList == null)
		{
			throw new VmException(-536870911);
		}
		int num = moduRoiManager.RefModuParams.SetParamValue("ROIAreaType", "0");
		if (num != 0)
		{
			throw new VmException(num);
		}
		new InputItem(moduRoiManager.RefModuParams, InputItem.InputType.INPUT_TYPE_BOX, new string[5] { "roicenterx", "roicentery", "roiwidth", "roiheight", "roiangle" }).SetInputValue(roiValueList);
	}

	private void SetAnnulus4BlobFind(List<Annulus> roiValueList)
	{
		if (roiValueList == null)
		{
			throw new VmException(-536870911);
		}
		int num = moduRoiManager.RefModuParams.SetParamValue("ROIAreaType", "0");
		if (num != 0)
		{
			throw new VmException(num);
		}
		new InputItem(moduRoiManager.RefModuParams, InputItem.InputType.INPUT_TYPE_ANNULUS, new string[6] { "roiAnnulusCenterX", "roiAnnulusCenterY", "roiAnnulusInnerRadius", "roiAnnulusOuterRadius", "roiAnnulusStartAngle", "roiAnnulusAngleExtend" }).SetInputValue(roiValueList);
	}
}
