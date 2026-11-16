using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace IMVSBinaryModuCs;

/// \addtogroup 图像二值化
/// @{
/// <summary>
/// @~chinese ROI管理类 @~english ROI Manager Class
/// </summary>
public class ImageBinaryRoiManager
{
	private RoiManager moduRoiManager;

	public RectBox RoiRectangle
	{
		set
		{
			moduRoiManager.SetInheritRoi(value);
		}
	}

	public List<RectBox> RoisRectangle
	{
		set
		{
			SetRect4ImageBinary(value);
		}
	}

	public Annulus RoiAnnulus
	{
		set
		{
			moduRoiManager.SetInheritRoi(value);
		}
	}

	public List<Annulus> RoisAnnulus
	{
		set
		{
			SetAnnulus4ImageBinary(value);
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
	public ImageBinaryRoiManager()
	{
	}

	/// <summary>
	/// 构造函数
	/// </summary>
	public ImageBinaryRoiManager(CModuleParamBase moduParams, string roiName, string roiType)
	{
		moduRoiManager = new RoiManager(moduParams, roiName, roiType);
	}

	private void SetRect4ImageBinary(List<RectBox> roiValueList)
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

	private void SetAnnulus4ImageBinary(List<Annulus> roiValueList)
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
