using VM.PlatformSDKCS;

namespace IMVSFastFeatureMatchModuCs;

/// <summary>
/// @~chinese 快速匹配参数 @~english Fast Match Param
/// </summary>
public class FastFeatureMatchParam : CModuleParamBase
{
	/// <summary>
	/// @~chinese 匹配极性 @~english Match Polarity
	/// </summary>
	public enum PolarityEnum
	{
		/// <summary>
		/// @~chinese 不考虑极性 @~english Ignored
		/// </summary>
		No,
		/// <summary>
		/// @~chinese 考虑极性 @~english Considered
		/// </summary>
		Yes
	}

	/// <summary>
	/// @~chinese 排序类型 @~english Sort
	/// </summary>
	public enum SortTypeEnum
	{
		/// <summary>
		/// @~chinese 按分数降序排序 @~english Score: High to Low
		/// </summary>
		Score = 2,
		/// <summary>
		/// @~chinese 按角度降序排序 @~english Angle: Big to Small
		/// </summary>
		Angle,
		/// <summary>
		/// @~chinese 按X由小到大排序 @~english X: Small to Big
		/// </summary>
		X,
		/// <summary>
		/// @~chinese 按Y由小到大排序 @~english Y: Small to Big
		/// </summary>
		Y,
		/// <summary>
		/// @~chinese X由小到大，Y由小到大 @~english X: Small to Big, Y: Small to Big
		/// </summary>
		XY,
		/// <summary>
		/// @~chinese Y由小到大，X由小到大 @~english Y: Small to Big, X: Small to Big
		/// </summary>
		YX
	}

	/// <summary>
	/// @~chinese 阈值类型 @~english Threshold Type
	/// </summary>
	public enum MatchThresholdFlagEnum
	{
		/// <summary>
		/// @~chinese 自动阈值 @~english Auto Threshold
		/// </summary>
		Auto,
		/// <summary>
		/// @~chinese 模板阈值 @~english Model Threshold
		/// </summary>
		Model,
		/// <summary>
		/// @~chinese 手动阈值 @~english Manual Threshold
		/// </summary>
		Manual
	}

	private InputItem inputImage;

	private ParamItem useMatchAllMode;

	private ParamItem minScore;

	private ParamItem maxMatchNum;

	private ParamItem polarity;

	private ParamItem angleStart;

	private ParamItem angleEnd;

	private ParamItem scaleStart;

	private ParamItem scaleEnd;

	private ParamItem maxOverlap;

	private ParamItem sortType;

	private ParamItem matchThresholdFlag;

	private ParamItem matchThresholdHigh;

	private ParamItem spotterFlag;

	private ParamItem matchExtentRate;

	private ParamItem timeOut;

	private ParamItem outLineEnable;

	private ParamItem oKWhenNumIsZero;

	private ParamItem numLimitEnable;

	private ParamItem numLimitLow;

	private ParamItem numLimitHigh;

	private ParamItem angleLimitEnable;

	private ParamItem angleLimitLow;

	private ParamItem angleLimitHigh;

	private ParamItem scaleLimitEnable;

	private ParamItem scaleLimitLow;

	private ParamItem scaleLimitHigh;

	private ParamItem scoreLimitEnable;

	private ParamItem scoreLimitLow;

	private ParamItem scoreLimitHigh;

	private ParamItem matchPointXLimitEnable;

	private ParamItem matchPointXLimitLow;

	private ParamItem matchPointXLimitHigh;

	private ParamItem matchPointYLimitEnable;

	private ParamItem matchPointYLimitLow;

	private ParamItem matchPointYLimitHigh;

	private ParamItem boxPointXLimitEnable;

	private ParamItem boxPointXLimitLow;

	private ParamItem boxPointXLimitHigh;

	private ParamItem boxPointYLimitEnable;

	private ParamItem boxPointYLimitLow;

	private ParamItem boxPointYLimitHigh;

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
	public FastFeatureMatchRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese 全部搜索模式 @~english Search All Mode
	/// </summary>
	public bool UseMatchAllMode
	{
		get
		{
			return useMatchAllMode.GetBoolValue();
		}
		set
		{
			useMatchAllMode.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最小匹配分数，范围：[0,1.0] @~english Min Match Score, Range:[0,1.0]
	/// </summary>
	public double MinScore
	{
		get
		{
			return minScore.GetDoubleValue();
		}
		set
		{
			minScore.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最大匹配个数，范围：[1,50000] @~english Max Number to find, Range:[1,50000]
	/// </summary>
	public int MaxMatchNum
	{
		get
		{
			return maxMatchNum.GetIntValue();
		}
		set
		{
			maxMatchNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配极性 @~english Match Polarity
	/// </summary>
	public PolarityEnum Polarity
	{
		get
		{
			return (PolarityEnum)polarity.GetEnumValue();
		}
		set
		{
			polarity.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-180,180] @~english Angle Range, Range:[-180,180]
	/// </summary>
	public int AngleStart
	{
		get
		{
			return angleStart.GetIntValue();
		}
		set
		{
			angleStart.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-180,180] @~english Angle Range, Range:[-180,180]
	/// </summary>
	public int AngleEnd
	{
		get
		{
			return angleEnd.GetIntValue();
		}
		set
		{
			angleEnd.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 尺度范围，范围：[0.1,10] @~english Scale Range, Range:[0.1,10]
	/// </summary>
	public double ScaleStart
	{
		get
		{
			return scaleStart.GetDoubleValue();
		}
		set
		{
			scaleStart.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 尺度范围，范围：[0.1,10] @~english Scale Range, Range:[0.1,10]
	/// </summary>
	public double ScaleEnd
	{
		get
		{
			return scaleEnd.GetDoubleValue();
		}
		set
		{
			scaleEnd.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最大重叠率，范围：[0,100] @~english Overlap Threshold, Range:[0,100]
	/// </summary>
	public int MaxOverlap
	{
		get
		{
			return maxOverlap.GetIntValue();
		}
		set
		{
			maxOverlap.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 排序类型 @~english Sort
	/// </summary>
	public SortTypeEnum SortType
	{
		get
		{
			return (SortTypeEnum)sortType.GetEnumValue();
		}
		set
		{
			sortType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 阈值类型 @~english Threshold Type
	/// </summary>
	public MatchThresholdFlagEnum MatchThresholdFlag
	{
		get
		{
			return (MatchThresholdFlagEnum)matchThresholdFlag.GetEnumValue();
		}
		set
		{
			matchThresholdFlag.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 边缘阈值，范围：[1,255] @~english Edge Threshold, Range:[1,255]
	/// </summary>
	public int MatchThresholdHigh
	{
		get
		{
			return matchThresholdHigh.GetIntValue();
		}
		set
		{
			matchThresholdHigh.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 是否考虑噪点 @~english Noise Considered
	/// </summary>
	public bool SpotterFlag
	{
		get
		{
			return spotterFlag.GetBoolValue();
		}
		set
		{
			spotterFlag.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 延拓阈值，范围：[0,90] @~english Extension Threshold, Range:[0,90]
	/// </summary>
	public int MatchExtentRate
	{
		get
		{
			return matchExtentRate.GetIntValue();
		}
		set
		{
			matchExtentRate.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 超时控制，范围：[0,10000] @~english Overtime Control, Range:[0,10000]
	/// </summary>
	public int TimeOut
	{
		get
		{
			return timeOut.GetIntValue();
		}
		set
		{
			timeOut.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 轮廓使能 @~english Contour Enabled
	/// </summary>
	public bool OutLineEnable
	{
		get
		{
			return outLineEnable.GetBoolValue();
		}
		set
		{
			outLineEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 忽略匹配个数 @~english Ignore number of matches
	/// </summary>
	public bool OKWhenNumIsZero
	{
		get
		{
			return oKWhenNumIsZero.GetBoolValue();
		}
		set
		{
			oKWhenNumIsZero.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 数量判断 @~english Quantity Check
	/// </summary>
	public bool NumLimitEnable
	{
		get
		{
			return numLimitEnable.GetBoolValue();
		}
		set
		{
			numLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 数量范围，范围：[0,99999] @~english Quantity Range, Range:[0,99999]
	/// </summary>
	public int NumLimitLow
	{
		get
		{
			return numLimitLow.GetIntValue();
		}
		set
		{
			numLimitLow.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 数量范围，范围：[0,99999] @~english Quantity Range, Range:[0,99999]
	/// </summary>
	public int NumLimitHigh
	{
		get
		{
			return numLimitHigh.GetIntValue();
		}
		set
		{
			numLimitHigh.SetIntValue(value);
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
	/// @~chinese 尺度判断 @~english Scale Check
	/// </summary>
	public bool ScaleLimitEnable
	{
		get
		{
			return scaleLimitEnable.GetBoolValue();
		}
		set
		{
			scaleLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 尺度范围，范围：[0.0,10] @~english Scale Range, Range:[0.0,10]
	/// </summary>
	public double ScaleLimitLow
	{
		get
		{
			return scaleLimitLow.GetDoubleValue();
		}
		set
		{
			scaleLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 尺度范围，范围：[0.0,10] @~english Scale Range, Range:[0.0,10]
	/// </summary>
	public double ScaleLimitHigh
	{
		get
		{
			return scaleLimitHigh.GetDoubleValue();
		}
		set
		{
			scaleLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 分数判断 @~english Score Check
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
	/// @~chinese 分数范围，范围：[0,1] @~english Score Range, Range:[0,1]
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
	/// @~chinese 分数范围，范围：[0,1] @~english Score Range, Range:[0,1]
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

	/// <summary>
	/// @~chinese 匹配点X判断 @~english Match Point X Check
	/// </summary>
	public bool MatchPointXLimitEnable
	{
		get
		{
			return matchPointXLimitEnable.GetBoolValue();
		}
		set
		{
			matchPointXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点X范围，范围：[-99999,99999] @~english Match Point X Range, Range:[-99999,99999]
	/// </summary>
	public double MatchPointXLimitLow
	{
		get
		{
			return matchPointXLimitLow.GetDoubleValue();
		}
		set
		{
			matchPointXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点X范围，范围：[-99999,99999] @~english Match Point X Range, Range:[-99999,99999]
	/// </summary>
	public double MatchPointXLimitHigh
	{
		get
		{
			return matchPointXLimitHigh.GetDoubleValue();
		}
		set
		{
			matchPointXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点Y判断 @~english Match Point Y Check
	/// </summary>
	public bool MatchPointYLimitEnable
	{
		get
		{
			return matchPointYLimitEnable.GetBoolValue();
		}
		set
		{
			matchPointYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点Y范围，范围：[-99999,99999] @~english Match Point Y Range, Range:[-99999,99999]
	/// </summary>
	public double MatchPointYLimitLow
	{
		get
		{
			return matchPointYLimitLow.GetDoubleValue();
		}
		set
		{
			matchPointYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 匹配点Y范围，范围：[-99999,99999] @~english Match Point Y Range, Range:[-99999,99999]
	/// </summary>
	public double MatchPointYLimitHigh
	{
		get
		{
			return matchPointYLimitHigh.GetDoubleValue();
		}
		set
		{
			matchPointYLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点X判断 @~english Central Point X Check
	/// </summary>
	public bool BoxPointXLimitEnable
	{
		get
		{
			return boxPointXLimitEnable.GetBoolValue();
		}
		set
		{
			boxPointXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点X范围，范围：[-99999,99999] @~english Central Point X Range, Range:[-99999,99999]
	/// </summary>
	public double BoxPointXLimitLow
	{
		get
		{
			return boxPointXLimitLow.GetDoubleValue();
		}
		set
		{
			boxPointXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点X范围，范围：[-99999,99999] @~english Central Point X Range, Range:[-99999,99999]
	/// </summary>
	public double BoxPointXLimitHigh
	{
		get
		{
			return boxPointXLimitHigh.GetDoubleValue();
		}
		set
		{
			boxPointXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点Y判断 @~english Central Point Y Check
	/// </summary>
	public bool BoxPointYLimitEnable
	{
		get
		{
			return boxPointYLimitEnable.GetBoolValue();
		}
		set
		{
			boxPointYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点Y范围，范围：[-99999,99999] @~english Central Point Y Range, Range:[-99999,99999]
	/// </summary>
	public double BoxPointYLimitLow
	{
		get
		{
			return boxPointYLimitLow.GetDoubleValue();
		}
		set
		{
			boxPointYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 中心点Y范围，范围：[-99999,99999] @~english Central Point Y Range, Range:[-99999,99999]
	/// </summary>
	public double BoxPointYLimitHigh
	{
		get
		{
			return boxPointYLimitHigh.GetDoubleValue();
		}
		set
		{
			boxPointYLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// 获取单个模板选择状态
	/// @~english Get single model selected status
	/// </summary>
	/// <param name="index">模板索引</param>
	/// <returns>模板选择状态</returns>
	public bool GetModelStatus(int index)
	{
		string strName = "SelectedModule" + index;
		string strValue = "";
		ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(ModuleID, strName, ref strValue, bThrowException: true);
		return strValue == "1";
	}

	/// <summary>
	/// 设置单个模板选择状态
	/// @~english Get single model selected status
	/// </summary>
	/// <param name="index">模板索引</param>
	/// <param name="value">选择状态</param>
	/// <returns></returns>
	public void SetModelStatus(int index, bool value)
	{
		string strParamName = "SelectedModule";
		string text = (value ? "1" : "0");
		string strParamValue = index + "," + text;
		ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(ModuleID, strParamName, strParamValue, bThrowException: true);
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public FastFeatureMatchParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new FastFeatureMatchRoiManager(this, "RoiType", "Full Box Polygon");
		useMatchAllMode = new ParamItem(this, "UseMatchAllMode");
		minScore = new ParamItem(this, "MinScore", 1.0);
		maxMatchNum = new ParamItem(this, "MaxMatchNum", 50000.0, 1.0);
		polarity = new ParamItem(this, "Polarity");
		angleStart = new ParamItem(this, "AngleStart", 180.0, -180.0);
		angleEnd = new ParamItem(this, "AngleEnd", 180.0, -180.0);
		scaleStart = new ParamItem(this, "ScaleStart", 10.0, 0.1);
		scaleEnd = new ParamItem(this, "ScaleEnd", 10.0, 0.1);
		maxOverlap = new ParamItem(this, "MaxOverlap", 100.0);
		sortType = new ParamItem(this, "SortType");
		matchThresholdFlag = new ParamItem(this, "MatchThresholdFlag");
		matchThresholdHigh = new ParamItem(this, "MatchThresholdHigh", 255.0, 1.0);
		spotterFlag = new ParamItem(this, "SpotterFlag");
		matchExtentRate = new ParamItem(this, "MatchExtentRate", 90.0);
		timeOut = new ParamItem(this, "TimeOut", 10000.0);
		outLineEnable = new ParamItem(this, "OutLineEnable");
		oKWhenNumIsZero = new ParamItem(this, "OKWhenNumIsZero");
		numLimitEnable = new ParamItem(this, "NumLimitEnable");
		numLimitLow = new ParamItem(this, "NumLimitLow", 99999.0);
		numLimitHigh = new ParamItem(this, "NumLimitHigh", 99999.0);
		angleLimitEnable = new ParamItem(this, "AngleLimitEnable");
		angleLimitLow = new ParamItem(this, "AngleLimitLow", 180.0, -180.0);
		angleLimitHigh = new ParamItem(this, "AngleLimitHigh", 180.0, -180.0);
		scaleLimitEnable = new ParamItem(this, "ScaleLimitEnable");
		scaleLimitLow = new ParamItem(this, "ScaleLimitLow", 10.0);
		scaleLimitHigh = new ParamItem(this, "ScaleLimitHigh", 10.0);
		scoreLimitEnable = new ParamItem(this, "ScoreLimitEnable");
		scoreLimitLow = new ParamItem(this, "ScoreLimitLow", 1.0);
		scoreLimitHigh = new ParamItem(this, "ScoreLimitHigh", 1.0);
		matchPointXLimitEnable = new ParamItem(this, "MatchPointXLimitEnable");
		matchPointXLimitLow = new ParamItem(this, "MatchPointXLimitLow", 99999.0, -99999.0);
		matchPointXLimitHigh = new ParamItem(this, "MatchPointXLimitHigh", 99999.0, -99999.0);
		matchPointYLimitEnable = new ParamItem(this, "MatchPointYLimitEnable");
		matchPointYLimitLow = new ParamItem(this, "MatchPointYLimitLow", 99999.0, -99999.0);
		matchPointYLimitHigh = new ParamItem(this, "MatchPointYLimitHigh", 99999.0, -99999.0);
		boxPointXLimitEnable = new ParamItem(this, "BoxPointXLimitEnable");
		boxPointXLimitLow = new ParamItem(this, "BoxPointXLimitLow", 99999.0, -99999.0);
		boxPointXLimitHigh = new ParamItem(this, "BoxPointXLimitHigh", 99999.0, -99999.0);
		boxPointYLimitEnable = new ParamItem(this, "BoxPointYLimitEnable");
		boxPointYLimitLow = new ParamItem(this, "BoxPointYLimitLow", 99999.0, -99999.0);
		boxPointYLimitHigh = new ParamItem(this, "BoxPointYLimitHigh", 99999.0, -99999.0);
	}
}
