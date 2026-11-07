using VM.PlatformSDKCS;

namespace IMVSLineFindModuCs;

/// <summary>
/// @~chinese 直线查找参数 @~english Line Search Param
/// </summary>
public class LineFindParam : CModuleParamBase
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
		/// @~chinese 第一条 @~english First Edge
		/// </summary>
		First,
		/// <summary>
		/// @~chinese 最后一条 @~english Last Edge
		/// </summary>
		Last,
		/// <summary>
		/// @~chinese 接近中线 @~english Close To Mid
		/// </summary>
		Mid
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

	private ParamItem findMode;

	private ParamItem edgePolarity;

	private ParamItem edgeStrength;

	private ParamItem kernelSize;

	private ParamItem rayNum;

	private ParamItem revertFindOrient;

	private ParamItem angleNormalization;

	private ParamItem lineAngleEnable;

	private ParamItem expectAngle;

	private ParamItem rotateTolerance;

	private ParamItem rejectNum;

	private ParamItem rejectDist;

	private ParamItem regionWidth;

	private ParamItem initType;

	private ParamItem fitFun;

	private ParamItem fitPointsLimitEnable;

	private ParamItem fitPointsLimitLow;

	private ParamItem fitPointsLimitHigh;

	private ParamItem angleLimitEnable;

	private ParamItem angleLimitLow;

	private ParamItem angleLimitHigh;

	private ParamItem scoreLimitEnable;

	private ParamItem scoreLimitLow;

	private ParamItem scoreLimitHigh;

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
	public LineFindRoiManager ModuRoiManager { get; internal set; }

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
	/// @~chinese 边缘阈值，范围：[1,255] @~english Edge Threshold, Range:[1,255]
	/// </summary>
	public int EdgeStrength
	{
		get
		{
			return edgeStrength.GetIntValue();
		}
		set
		{
			edgeStrength.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 滤波尺寸，范围：[1,50] @~english Filter Size, Range:[1,50]
	/// </summary>
	public int KernelSize
	{
		get
		{
			return kernelSize.GetIntValue();
		}
		set
		{
			kernelSize.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 卡尺数量，范围：[2,1000] @~english Caliper Number, Range:[2,1000]
	/// </summary>
	public int RayNum
	{
		get
		{
			return rayNum.GetIntValue();
		}
		set
		{
			rayNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 直线查找反向 @~english RevertFindOrient
	/// </summary>
	public bool RevertFindOrient
	{
		get
		{
			return revertFindOrient.GetBoolValue();
		}
		set
		{
			revertFindOrient.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度归一化 @~english AngleNormalization
	/// </summary>
	public bool AngleNormalization
	{
		get
		{
			return angleNormalization.GetBoolValue();
		}
		set
		{
			angleNormalization.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度使能 @~english Line Angle Enable
	/// </summary>
	public bool LineAngleEnable
	{
		get
		{
			return lineAngleEnable.GetBoolValue();
		}
		set
		{
			lineAngleEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 期望直线角度，范围：[-180,180] @~english Expect Angle, Range:[-180,180]
	/// </summary>
	public double ExpectAngle
	{
		get
		{
			return expectAngle.GetDoubleValue();
		}
		set
		{
			expectAngle.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度旋转容忍，范围：[0,180] @~english Rotate Tolerance, Range:[0,180]
	/// </summary>
	public double RotateTolerance
	{
		get
		{
			return rotateTolerance.GetDoubleValue();
		}
		set
		{
			rotateTolerance.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 剔除点数，范围：[0,998] @~english Reject Number, Range:[0,998]
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
	/// @~chinese 投影宽度，范围：[1,100] @~english Projection Width, Range:[1,100]
	/// </summary>
	public int RegionWidth
	{
		get
		{
			return regionWidth.GetIntValue();
		}
		set
		{
			regionWidth.SetIntValue(value);
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
	/// @~chinese 拟合点数范围，范围：[2,99999] @~english Fit Points Range, Range:[2,99999]
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
	/// @~chinese 拟合点数范围，范围：[2,99999] @~english Fit Points Range, Range:[2,99999]
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

	/// <summary>
	/// @~chinese 角度判断 @~english Angle Check
	/// </summary>
	public bool AngleLimitEnable
	{
		get
		{
			return angleLimitEnable.GetBoolValue();
		}
		set
		{
			angleLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-180,180] @~english Angle Range, Range:[-180,180]
	/// </summary>
	public double AngleLimitLow
	{
		get
		{
			return angleLimitLow.GetDoubleValue();
		}
		set
		{
			angleLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-180,180] @~english Angle Range, Range:[-180,180]
	/// </summary>
	public double AngleLimitHigh
	{
		get
		{
			return angleLimitHigh.GetDoubleValue();
		}
		set
		{
			angleLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差判断 @~english Fit Error Check
	/// </summary>
	public bool ScoreLimitEnable
	{
		get
		{
			return scoreLimitEnable.GetBoolValue();
		}
		set
		{
			scoreLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差范围，范围：[0,9999] @~english Fit Error Range, Range:[0,9999]
	/// </summary>
	public double ScoreLimitLow
	{
		get
		{
			return scoreLimitLow.GetDoubleValue();
		}
		set
		{
			scoreLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 拟合误差范围，范围：[0,9999] @~english Fit Error Range, Range:[0,9999]
	/// </summary>
	public double ScoreLimitHigh
	{
		get
		{
			return scoreLimitHigh.GetDoubleValue();
		}
		set
		{
			scoreLimitHigh.SetDoubleValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public LineFindParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new LineFindRoiManager(this, "RoiType", "LineCaliper");
		findMode = new ParamItem(this, "FindMode");
		edgePolarity = new ParamItem(this, "EdgePolarity");
		edgeStrength = new ParamItem(this, "EdgeStrength", 255.0, 1.0);
		kernelSize = new ParamItem(this, "KernelSize", 50.0, 1.0);
		rayNum = new ParamItem(this, "RayNum", 1000.0, 2.0);
		revertFindOrient = new ParamItem(this, "RevertFindOrient");
		angleNormalization = new ParamItem(this, "AngleNormalization");
		lineAngleEnable = new ParamItem(this, "LineAngleEnable");
		expectAngle = new ParamItem(this, "ExpectAngle", 180.0, -180.0);
		rotateTolerance = new ParamItem(this, "RotateTolerance", 180.0);
		rejectNum = new ParamItem(this, "RejectNum", 998.0);
		rejectDist = new ParamItem(this, "RejectDist", 1000.0, 1.0);
		regionWidth = new ParamItem(this, "RegionWidth", 100.0, 1.0);
		initType = new ParamItem(this, "InitType");
		fitFun = new ParamItem(this, "FitFun");
		fitPointsLimitEnable = new ParamItem(this, "FitPointsLimitEnable");
		fitPointsLimitLow = new ParamItem(this, "FitPointsLimitLow", 99999.0, 2.0);
		fitPointsLimitHigh = new ParamItem(this, "FitPointsLimitHigh", 99999.0, 2.0);
		angleLimitEnable = new ParamItem(this, "AngleLimitEnable");
		angleLimitLow = new ParamItem(this, "AngleLimitLow", 180.0, -180.0);
		angleLimitHigh = new ParamItem(this, "AngleLimitHigh", 180.0, -180.0);
		scoreLimitEnable = new ParamItem(this, "ScoreLimitEnable");
		scoreLimitLow = new ParamItem(this, "ScoreLimitLow", 9999.0);
		scoreLimitHigh = new ParamItem(this, "ScoreLimitHigh", 9999.0);
	}
}
