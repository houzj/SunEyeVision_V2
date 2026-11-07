using VM.PlatformSDKCS;

namespace IMVSCircleFindModuCs;

/// <summary>
/// @~chinese 圆查找参数 @~english Circle Search Param
/// </summary>
public class CircleFindParam : CModuleParamBase
{
	/// <summary>
	/// @~chinese 边缘类型 @~english Edge Type
	/// </summary>
	public enum FindModeEnum
	{
		/// <summary>
		/// @~chinese 最强 @~english Strongest Edge
		/// </summary>
		Best = 1,
		/// <summary>
		/// @~chinese 最后一条 @~english Last Edge
		/// </summary>
		Largest,
		/// <summary>
		/// @~chinese 第一条 @~english First Edge
		/// </summary>
		SMALLEST
	}

	/// <summary>
	/// @~chinese 边缘极性 @~english EdgePolarity
	/// </summary>
	public enum EdgePolarityEnum
	{
		/// <summary>
		/// @~chinese 从黑到白 @~english Dark to Light
		/// </summary>
		BlackToWhite = 1,
		/// <summary>
		/// @~chinese 从白到黑 @~english Light to Dark
		/// </summary>
		WhiteToBlack,
		/// <summary>
		/// @~chinese 任意极性 @~english Both
		/// </summary>
		Both
	}

	/// <summary>
	/// @~chinese 初始拟合 @~english Initial Fit
	/// </summary>
	public enum InitTypeEnum
	{
		/// <summary>
		/// @~chinese 全局 @~english Global
		/// </summary>
		ALS = 1,
		/// <summary>
		/// @~chinese 局部 @~english Local
		/// </summary>
		LLS
	}

	/// <summary>
	/// @~chinese 拟合方式 @~english Fit Mode
	/// </summary>
	public enum FitFunEnum
	{
		/// <summary>
		/// @~chinese 最小二乘 @~english Least Squares
		/// </summary>
		LS = 1,
		/// <summary>
		/// @~chinese huber @~english huber
		/// </summary>
		Huber,
		/// <summary>
		/// @~chinese tukey @~english tukey
		/// </summary>
		Tukey
	}

	private InputItem inputImage;

	private ParamItem minRadius;

	private ParamItem maxRadius;

	private ParamItem findMode;

	private ParamItem edgePolarity;

	private ParamItem edgeThresh;

	private ParamItem edgeWidth;

	private ParamItem radNum;

	private ParamItem rejectNum;

	private ParamItem coarseDetectFlag;

	private ParamItem cCDSampleScale;

	private ParamItem cCDCircleThresh;

	private ParamItem rejectDist;

	private ParamItem proLength;

	private ParamItem initType;

	private ParamItem fitFun;

	private ParamItem radiusLimitEnable;

	private ParamItem radiusLimitLow;

	private ParamItem radiusLimitHigh;

	private ParamItem centerXLimitEnable;

	private ParamItem centerXLimitLow;

	private ParamItem centerXLimitHigh;

	private ParamItem centerYLimitEnable;

	private ParamItem centerYLimitLow;

	private ParamItem centerYLimitHigh;

	private ParamItem fitErrorLimitEnable;

	private ParamItem fitErrorLimitLow;

	private ParamItem fitErrorLimitHigh;

	private ParamItem fitPointsLimitEnable;

	private ParamItem fitPointsLimitLow;

	private ParamItem fitPointsLimitHigh;

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
	public CircleFindRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese 扇环半径，范围：[1,10000] @~english Sector Radius, Range:[1,10000]
	/// </summary>
	public int MinRadius
	{
		get
		{
			return minRadius.GetIntValue();
		}
		set
		{
			minRadius.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 扇环半径，范围：[1,10000] @~english Sector Radius, Range:[1,10000]
	/// </summary>
	public int MaxRadius
	{
		get
		{
			return maxRadius.GetIntValue();
		}
		set
		{
			maxRadius.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘类型 @~english Edge Type
	/// </summary>
	public FindModeEnum FindMode
	{
		get
		{
			return (FindModeEnum)findMode.GetEnumValue();
		}
		set
		{
			findMode.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 边缘极性 @~english EdgePolarity
	/// </summary>
	public EdgePolarityEnum EdgePolarity
	{
		get
		{
			return (EdgePolarityEnum)edgePolarity.GetEnumValue();
		}
		set
		{
			edgePolarity.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 边缘阈值，范围：[0,255] @~english Edge Threshold, Range:[0,255]
	/// </summary>
	public int EdgeThresh
	{
		get
		{
			return edgeThresh.GetIntValue();
		}
		set
		{
			edgeThresh.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 滤波尺寸，范围：[1,50] @~english Filter Size, Range:[1,50]
	/// </summary>
	public int EdgeWidth
	{
		get
		{
			return edgeWidth.GetIntValue();
		}
		set
		{
			edgeWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 卡尺数量，范围：[3,1000] @~english Caliper Number, Range:[3,1000]
	/// </summary>
	public int RadNum
	{
		get
		{
			return radNum.GetIntValue();
		}
		set
		{
			radNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 剔除点数，范围：[0,997] @~english Reject Number, Range:[0,997]
	/// </summary>
	public int RejectNum
	{
		get
		{
			return rejectNum.GetIntValue();
		}
		set
		{
			rejectNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 初定位 @~english Initial Locating
	/// </summary>
	public bool CoarseDetectFlag
	{
		get
		{
			return coarseDetectFlag.GetBoolValue();
		}
		set
		{
			coarseDetectFlag.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 下采样系数，范围：[1,8] @~english Subsampling Coefficient, Range:[1,8]
	/// </summary>
	public int CCDSampleScale
	{
		get
		{
			return cCDSampleScale.GetIntValue();
		}
		set
		{
			cCDSampleScale.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆定位敏感度，范围：[1,1000] @~english Locating Sensitivity, Range:[1,1000]
	/// </summary>
	public int CCDCircleThresh
	{
		get
		{
			return cCDCircleThresh.GetIntValue();
		}
		set
		{
			cCDCircleThresh.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 剔除距离，范围：[1,1000] @~english Distance to Remove, Range:[1,1000]
	/// </summary>
	public int RejectDist
	{
		get
		{
			return rejectDist.GetIntValue();
		}
		set
		{
			rejectDist.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 投影宽度，范围：[1,500] @~english Projection Width, Range:[1,500]
	/// </summary>
	public int ProLength
	{
		get
		{
			return proLength.GetIntValue();
		}
		set
		{
			proLength.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 初始拟合 @~english Initial Fit
	/// </summary>
	public InitTypeEnum InitType
	{
		get
		{
			return (InitTypeEnum)initType.GetEnumValue();
		}
		set
		{
			initType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 拟合方式 @~english Fit Mode
	/// </summary>
	public FitFunEnum FitFun
	{
		get
		{
			return (FitFunEnum)fitFun.GetEnumValue();
		}
		set
		{
			fitFun.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 半径判断 @~english Radius Check
	/// </summary>
	public bool RadiusLimitEnable
	{
		get
		{
			return radiusLimitEnable.GetBoolValue();
		}
		set
		{
			radiusLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 半径判断，范围：[0,99999] @~english Radius Check, Range:[0,99999]
	/// </summary>
	public double RadiusLimitLow
	{
		get
		{
			return radiusLimitLow.GetDoubleValue();
		}
		set
		{
			radiusLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 半径判断，范围：[0,99999] @~english Radius Check, Range:[0,99999]
	/// </summary>
	public double RadiusLimitHigh
	{
		get
		{
			return radiusLimitHigh.GetDoubleValue();
		}
		set
		{
			radiusLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心X判断 @~english Center X Check
	/// </summary>
	public bool CenterXLimitEnable
	{
		get
		{
			return centerXLimitEnable.GetBoolValue();
		}
		set
		{
			centerXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心X判断，范围：[-99999,99999] @~english Center X Check, Range:[-99999,99999]
	/// </summary>
	public double CenterXLimitLow
	{
		get
		{
			return centerXLimitLow.GetDoubleValue();
		}
		set
		{
			centerXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心X判断，范围：[-99999,99999] @~english Center X Check, Range:[-99999,99999]
	/// </summary>
	public double CenterXLimitHigh
	{
		get
		{
			return centerXLimitHigh.GetDoubleValue();
		}
		set
		{
			centerXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心Y判断 @~english Center Y Check
	/// </summary>
	public bool CenterYLimitEnable
	{
		get
		{
			return centerYLimitEnable.GetBoolValue();
		}
		set
		{
			centerYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心Y范围，范围：[-99999,99999] @~english Center Y Range, Range:[-99999,99999]
	/// </summary>
	public double CenterYLimitLow
	{
		get
		{
			return centerYLimitLow.GetDoubleValue();
		}
		set
		{
			centerYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心Y范围，范围：[-99999,99999] @~english Center Y Range, Range:[-99999,99999]
	/// </summary>
	public double CenterYLimitHigh
	{
		get
		{
			return centerYLimitHigh.GetDoubleValue();
		}
		set
		{
			centerYLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差判断 @~english Fit Error Check
	/// </summary>
	public bool FitErrorLimitEnable
	{
		get
		{
			return fitErrorLimitEnable.GetBoolValue();
		}
		set
		{
			fitErrorLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差范围，范围：[0,100.0] @~english Fit Error Range, Range:[0,100.0]
	/// </summary>
	public double FitErrorLimitLow
	{
		get
		{
			return fitErrorLimitLow.GetDoubleValue();
		}
		set
		{
			fitErrorLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差范围，范围：[0,100.0] @~english Fit Error Range, Range:[0,100.0]
	/// </summary>
	public double FitErrorLimitHigh
	{
		get
		{
			return fitErrorLimitHigh.GetDoubleValue();
		}
		set
		{
			fitErrorLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点数判断 @~english Fit Points Check
	/// </summary>
	public bool FitPointsLimitEnable
	{
		get
		{
			return fitPointsLimitEnable.GetBoolValue();
		}
		set
		{
			fitPointsLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合点数范围，范围：[3,99999] @~english Fit Points Range, Range:[3,99999]
	/// </summary>
	public int FitPointsLimitLow
	{
		get
		{
			return fitPointsLimitLow.GetIntValue();
		}
		set
		{
			fitPointsLimitLow.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合点数范围，范围：[3,99999] @~english Fit Points Range, Range:[3,99999]
	/// </summary>
	public int FitPointsLimitHigh
	{
		get
		{
			return fitPointsLimitHigh.GetIntValue();
		}
		set
		{
			fitPointsLimitHigh.SetIntValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public CircleFindParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new CircleFindRoiManager(this, "RoiType", "Full Box CircleCaliper SectorCaliper");
		minRadius = new ParamItem(this, "MinRadius", 10000.0, 1.0);
		maxRadius = new ParamItem(this, "MaxRadius", 10000.0, 1.0);
		findMode = new ParamItem(this, "FindMode");
		edgePolarity = new ParamItem(this, "EdgePolarity");
		edgeThresh = new ParamItem(this, "EdgeThresh", 255.0);
		edgeWidth = new ParamItem(this, "EdgeWidth", 50.0, 1.0);
		radNum = new ParamItem(this, "RadNum", 1000.0, 3.0);
		rejectNum = new ParamItem(this, "RejectNum", 997.0);
		coarseDetectFlag = new ParamItem(this, "CoarseDetectFlag");
		cCDSampleScale = new ParamItem(this, "CCDSampleScale", 8.0, 1.0);
		cCDCircleThresh = new ParamItem(this, "CCDCircleThresh", 1000.0, 1.0);
		rejectDist = new ParamItem(this, "RejectDist", 1000.0, 1.0);
		proLength = new ParamItem(this, "ProLength", 500.0, 1.0);
		initType = new ParamItem(this, "InitType");
		fitFun = new ParamItem(this, "FitFun");
		radiusLimitEnable = new ParamItem(this, "RadiusLimitEnable");
		radiusLimitLow = new ParamItem(this, "RadiusLimitLow", 99999.0);
		radiusLimitHigh = new ParamItem(this, "RadiusLimitHigh", 99999.0);
		centerXLimitEnable = new ParamItem(this, "CenterXLimitEnable");
		centerXLimitLow = new ParamItem(this, "CenterXLimitLow", 99999.0, -99999.0);
		centerXLimitHigh = new ParamItem(this, "CenterXLimitHigh", 99999.0, -99999.0);
		centerYLimitEnable = new ParamItem(this, "CenterYLimitEnable");
		centerYLimitLow = new ParamItem(this, "CenterYLimitLow", 99999.0, -99999.0);
		centerYLimitHigh = new ParamItem(this, "CenterYLimitHigh", 99999.0, -99999.0);
		fitErrorLimitEnable = new ParamItem(this, "FitErrorLimitEnable");
		fitErrorLimitLow = new ParamItem(this, "FitErrorLimitLow", 100.0);
		fitErrorLimitHigh = new ParamItem(this, "FitErrorLimitHigh", 100.0);
		fitPointsLimitEnable = new ParamItem(this, "FitPointsLimitEnable");
		fitPointsLimitLow = new ParamItem(this, "FitPointsLimitLow", 99999.0, 3.0);
		fitPointsLimitHigh = new ParamItem(this, "FitPointsLimitHigh", 99999.0, 3.0);
	}
}
