using VM.PlatformSDKCS;

namespace IMVSBinaryModuCs;

/// <summary>
/// @~chinese 图像二值化参数 @~english Image Binarization Param
/// </summary>
public class ImageBinaryParam : CModuleParamBase
{
	/// <summary>
	/// @~chinese ROI外处理 @~english Outside Of ROI
	/// </summary>
	public enum NonROIPrcTypeEnum
	{
		/// <summary>
		/// @~chinese 黑色 @~english Black
		/// </summary>
		Zero,
		/// <summary>
		/// @~chinese 原图 @~english Original Image
		/// </summary>
		Src
	}

	/// <summary>
	/// @~chinese 二值化类型 @~english Binarization Type
	/// </summary>
	public enum BinaryTypeEnum
	{
		/// <summary>
		/// @~chinese 硬阈值二值化 @~english Hard Threshold Binarization
		/// </summary>
		HardThreshold = 1,
		/// <summary>
		/// @~chinese 均值二值化 @~english Mean Binarization
		/// </summary>
		MeanThreshold = 2,
		/// <summary>
		/// @~chinese 高斯二值化 @~english Gaussian Binarization
		/// </summary>
		GaussianThreshold = 3,
		/// <summary>
		/// @~chinese Sauvola二值化 @~english Sauvola Binarization
		/// </summary>
		Sauvola = 8,
		/// <summary>
		/// @~chinese 自动 @~english Auto
		/// </summary>
		OTSU = 4
	}

	/// <summary>
	/// @~chinese 比较类型 @~english Comparison Type
	/// </summary>
	public enum CompareTypeEnum
	{
		/// <summary>
		/// @~chinese 大于等于 @~english Greater Than or Equal to
		/// </summary>
		GreaterEq,
		/// <summary>
		/// @~chinese 小于等于 @~english Less Than or Equal to
		/// </summary>
		LessEq,
		/// <summary>
		/// @~chinese 等于 @~english Equal to
		/// </summary>
		Equal,
		/// <summary>
		/// @~chinese 不等于 @~english Not Equal to
		/// </summary>
		NotEqual
	}

	/// <summary>
	/// @~chinese 分割类型 @~english Segment Type
	/// </summary>
	public enum SauvolaSegTypeEnum
	{
		/// <summary>
		/// @~chinese 暗于背景 @~english Darker Than Background
		/// </summary>
		Dark,
		/// <summary>
		/// @~chinese 亮于背景 @~english Brighter Than Bachground
		/// </summary>
		Bright
	}

	private InputItem inputImage;

	private ParamItem nonROIPrcType;

	private ParamItem binaryType;

	private ParamItem lowThreshold;

	private ParamItem highThreshold;

	private ParamItem kernelWidth;

	private ParamItem kernelHeight;

	private ParamItem gaussKernelSize;

	private ParamItem gaussSigma;

	private ParamItem compareType;

	private ParamItem thresholdOffset;

	private ParamItem sauvolaAdjustCoef;

	private ParamItem sauvolaRange;

	private ParamItem sauvolaSegType;

	private ParamItem sauvolaWinWidth;

	private ParamItem sauvolaWinHeight;

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
	public ImageBinaryRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese ROI外处理 @~english Outside Of ROI
	/// </summary>
	public NonROIPrcTypeEnum NonROIPrcType
	{
		get
		{
			return (NonROIPrcTypeEnum)nonROIPrcType.GetEnumValue();
		}
		set
		{
			nonROIPrcType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 二值化类型 @~english Binarization Type
	/// </summary>
	public BinaryTypeEnum BinaryType
	{
		get
		{
			return (BinaryTypeEnum)binaryType.GetEnumValue();
		}
		set
		{
			binaryType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 低阈值，范围：[0,255] @~english Low Threshold, Range:[0,255]
	/// </summary>
	public int LowThreshold
	{
		get
		{
			return lowThreshold.GetIntValue();
		}
		set
		{
			lowThreshold.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 高阈值，范围：[0,255] @~english High Threshold, Range:[0,255]
	/// </summary>
	public int HighThreshold
	{
		get
		{
			return highThreshold.GetIntValue();
		}
		set
		{
			highThreshold.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 滤波核宽度，范围：[1,51] @~english Filter Kernel Width, Range:[1,51]
	/// </summary>
	public int KernelWidth
	{
		get
		{
			return kernelWidth.GetIntValue();
		}
		set
		{
			kernelWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 滤波核高度，范围：[1,51] @~english Filter Kernel Height, Range:[1,51]
	/// </summary>
	public int KernelHeight
	{
		get
		{
			return kernelHeight.GetIntValue();
		}
		set
		{
			kernelHeight.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 高斯滤波核，范围：[1,51] @~english Gaussian Filter Kernel, Range:[1,51]
	/// </summary>
	public int GaussKernelSize
	{
		get
		{
			return gaussKernelSize.GetIntValue();
		}
		set
		{
			gaussKernelSize.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 高斯标准差，范围：[0.1,100] @~english Gaussian Standard Deviation, Range:[0.1,100]
	/// </summary>
	public double GaussSigma
	{
		get
		{
			return gaussSigma.GetDoubleValue();
		}
		set
		{
			gaussSigma.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 比较类型 @~english Comparison Type
	/// </summary>
	public CompareTypeEnum CompareType
	{
		get
		{
			return (CompareTypeEnum)compareType.GetEnumValue();
		}
		set
		{
			compareType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 阈值偏移量，范围：[-255,255] @~english Threshold Offset, Range:[-255,255]
	/// </summary>
	public int ThresholdOffset
	{
		get
		{
			return thresholdOffset.GetIntValue();
		}
		set
		{
			thresholdOffset.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 校正系数，范围：[-1.0,1.0] @~english Adjust Coefficient, Range:[-1.0,1.0]
	/// </summary>
	public double SauvolaAdjustCoef
	{
		get
		{
			return sauvolaAdjustCoef.GetDoubleValue();
		}
		set
		{
			sauvolaAdjustCoef.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 动态范围，范围：[0.1,255] @~english Range, Range:[0.1,255]
	/// </summary>
	public double SauvolaRange
	{
		get
		{
			return sauvolaRange.GetDoubleValue();
		}
		set
		{
			sauvolaRange.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 分割类型 @~english Segment Type
	/// </summary>
	public SauvolaSegTypeEnum SauvolaSegType
	{
		get
		{
			return (SauvolaSegTypeEnum)sauvolaSegType.GetEnumValue();
		}
		set
		{
			sauvolaSegType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 滤波核宽度，范围：[1,100] @~english Filter Kernel Width, Range:[1,100]
	/// </summary>
	public int SauvolaWinWidth
	{
		get
		{
			return sauvolaWinWidth.GetIntValue();
		}
		set
		{
			sauvolaWinWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 滤波核高度，范围：[1,100] @~english Filter Kernel Height, Range:[1,100]
	/// </summary>
	public int SauvolaWinHeight
	{
		get
		{
			return sauvolaWinHeight.GetIntValue();
		}
		set
		{
			sauvolaWinHeight.SetIntValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public ImageBinaryParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new ImageBinaryRoiManager(this, "RoiType", "Full Box SectorRing Polygon Sector");
		nonROIPrcType = new ParamItem(this, "NonROIPrcType");
		binaryType = new ParamItem(this, "BinaryType");
		lowThreshold = new ParamItem(this, "LowThreshold", 255.0);
		highThreshold = new ParamItem(this, "HighThreshold", 255.0);
		kernelWidth = new ParamItem(this, "KernelWidth", 51.0, 1.0);
		kernelHeight = new ParamItem(this, "KernelHeight", 51.0, 1.0);
		gaussKernelSize = new ParamItem(this, "GaussKernelSize", 51.0, 1.0);
		gaussSigma = new ParamItem(this, "GaussSigma", 100.0, 0.1);
		compareType = new ParamItem(this, "CompareType");
		thresholdOffset = new ParamItem(this, "ThresholdOffset", 255.0, -255.0);
		sauvolaAdjustCoef = new ParamItem(this, "SauvolaAdjustCoef", 1.0, -1.0);
		sauvolaRange = new ParamItem(this, "SauvolaRange", 255.0, 0.1);
		sauvolaSegType = new ParamItem(this, "SauvolaSegType");
		sauvolaWinWidth = new ParamItem(this, "SauvolaWinWidth", 100.0, 1.0);
		sauvolaWinHeight = new ParamItem(this, "SauvolaWinHeight", 100.0, 1.0);
	}
}
