using VM.PlatformSDKCS;

namespace IMVSDivideImageModuCs;

/// <summary>
/// @~chinese 划片拆分参数 @~english Divide Image Param
/// </summary>
public class DivideImageParam : CModuleParamBase
{
	private InputItem inputImage;

	private ParamItem numX;

	private ParamItem numY;

	private ParamItem overlaprateX;

	private ParamItem overlaprateY;

	public ImageBaseData InputImage
	{
		set
		{
			inputImage.SetInputValue(value);
		}
	}

	/// <summary>
	/// @~chinese ROI管理器 @~english ROI Manager
	/// </summary>
	public DivideImageRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese X方向划片数，范围：[1,50] @~english Divisions in X Direction, Range:[1,50]
	/// </summary>
	public int NumX
	{
		get
		{
			return numX.GetIntValue();
		}
		set
		{
			numX.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y方向划片数，范围：[1,50] @~english Divisions in Y Direction, Range:[1,50]
	/// </summary>
	public int NumY
	{
		get
		{
			return numY.GetIntValue();
		}
		set
		{
			numY.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X方向重叠率，范围：[0,100] @~english X-Direction Overlap Rate, Range:[0,100]
	/// </summary>
	public int OverlaprateX
	{
		get
		{
			return overlaprateX.GetIntValue();
		}
		set
		{
			overlaprateX.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y方向重叠率，范围：[0,100] @~english Y-Direction Overlap Rate, Range:[0,100]
	/// </summary>
	public int OverlaprateY
	{
		get
		{
			return overlaprateY.GetIntValue();
		}
		set
		{
			overlaprateY.SetIntValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public DivideImageParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new DivideImageRoiManager(this, "RoiType", "Full Box");
		numX = new ParamItem(this, "NumX", 50.0, 1.0);
		numY = new ParamItem(this, "NumY", 50.0, 1.0);
		overlaprateX = new ParamItem(this, "OverlaprateX", 100.0);
		overlaprateY = new ParamItem(this, "OverlaprateY", 100.0);
	}
}
