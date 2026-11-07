using VM.PlatformSDKCS;

namespace IMVSCaliperModuCs;

/// <summary>
/// @~chinese 卡尺工具参数 @~english Caliper Param
/// </summary>
public class CaliperParam : CModuleParamBase
{
	/// <summary>
	/// @~chinese 边缘模式 @~english Edge Mode
	/// </summary>
	public enum EdgeModeEnum
	{
		/// <summary>
		/// @~chinese 单边缘 @~english Single Edge
		/// </summary>
		SINGLE_EDGE = 1,
		/// <summary>
		/// @~chinese 边缘对 @~english Edge Pair
		/// </summary>
		EDGE_PAIR
	}

	/// <summary>
	/// @~chinese 边缘0极性 @~english Edge 0 polarity
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
	/// @~chinese 边缘1极性 @~english Edge 1 polarity
	/// </summary>
	public enum Edge1PolarityEnum
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
	/// @~chinese 排序方式 @~english Sort Type
	/// </summary>
	public enum SortTypeEnum
	{
		/// <summary>
		/// @~chinese 分数降序 @~english Score Descend
		/// </summary>
		ScoreDescend,
		/// <summary>
		/// @~chinese 分数升序 @~english Score Ascend
		/// </summary>
		ScoreAscend,
		/// <summary>
		/// @~chinese 方向正向 @~english Orient Forward
		/// </summary>
		OrientForward,
		/// <summary>
		/// @~chinese 方向逆向 @~english Orient Backward
		/// </summary>
		OrientBackward
	}

	/// <summary>
	/// @~chinese 曲线类型1 @~english Scoring1 Function
	/// </summary>
	public enum ContrastDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型2 @~english Scoring2 Function
	/// </summary>
	public enum GrayscaleDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型3 @~english Scoring3 Function
	/// </summary>
	public enum PositionDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型4 @~english Scoring4 Function
	/// </summary>
	public enum PositionNegDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public enum PositionNormDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public enum PositionNormNegDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public enum SizeNormDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public enum SizeDiffNormDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 左曲线类型 @~english Left Scoring Function
	/// </summary>
	public enum SizeDiffNormAsymDropEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 右曲线类型 @~english Right Scoring Function
	/// </summary>
	public enum SizeDiffNormAsymDropHEnum
	{
		/// <summary>
		/// @~chinese 递增 @~english Ascending
		/// </summary>
		Rise,
		/// <summary>
		/// @~chinese 递减 @~english Descending
		/// </summary>
		Drop
	}

	/// <summary>
	/// @~chinese 投影方向 @~english Projection Direction
	/// </summary>
	public enum ProjectionTypeEnum
	{
		/// <summary>
		/// @~chinese 从左到右 @~english Left to Right
		/// </summary>
		ProjectionToHeight,
		/// <summary>
		/// @~chinese 从上到下 @~english Top to Bottom
		/// </summary>
		ProjectionToWidth
	}

	private InputItem inputImage;

	private ParamItem edgeMode;

	private ParamItem halfKernelSize;

	private ParamItem contrastTH;

	private ParamItem edgePolarity;

	private ParamItem edge1Polarity;

	private ParamItem edgePairWidth;

	private ParamItem maximum;

	private ParamItem sortType;

	private ParamItem contrastEnable;

	private ParamItem contrastPairEnable;

	private ParamItem contrastDrop;

	private ParamItem contrastX0;

	private ParamItem contrastX1;

	private ParamItem contrastXC;

	private ParamItem contrastY0;

	private ParamItem contrastY1;

	private ParamItem grayscaleEnable;

	private ParamItem grayscaleDrop;

	private ParamItem grayscaleX0;

	private ParamItem grayscaleX1;

	private ParamItem grayscaleXC;

	private ParamItem grayscaleY0;

	private ParamItem grayscaleY1;

	private ParamItem positionEnable;

	private ParamItem positionDrop;

	private ParamItem positionX0;

	private ParamItem positionX1;

	private ParamItem positionXC;

	private ParamItem positionY0;

	private ParamItem positionY1;

	private ParamItem positionNegEnable;

	private ParamItem positionNegDrop;

	private ParamItem positionNegX0;

	private ParamItem positionNegX1;

	private ParamItem positionNegXC;

	private ParamItem positionNegY0;

	private ParamItem positionNegY1;

	private ParamItem positionNormEnable;

	private ParamItem positionNormDrop;

	private ParamItem positionNormX0;

	private ParamItem positionNormX1;

	private ParamItem positionNormXC;

	private ParamItem positionNormY0;

	private ParamItem positionNormY1;

	private ParamItem positionNormNegEnable;

	private ParamItem positionNormNegDrop;

	private ParamItem positionNormNegX0;

	private ParamItem positionNormNegX1;

	private ParamItem positionNormNegXC;

	private ParamItem positionNormNegY0;

	private ParamItem positionNormNegY1;

	private ParamItem sizeNormEnable;

	private ParamItem sizeNormDrop;

	private ParamItem sizeNormX0;

	private ParamItem sizeNormX1;

	private ParamItem sizeNormXC;

	private ParamItem sizeNormY0;

	private ParamItem sizeNormY1;

	private ParamItem sizeDiffNormEnable;

	private ParamItem sizeDiffNormDrop;

	private ParamItem sizeDiffNormX0;

	private ParamItem sizeDiffNormX1;

	private ParamItem sizeDiffNormXC;

	private ParamItem sizeDiffNormY0;

	private ParamItem sizeDiffNormY1;

	private ParamItem sizeDiffNormAsymEnable;

	private ParamItem sizeDiffNormAsymDrop;

	private ParamItem sizeDiffNormAsymX0;

	private ParamItem sizeDiffNormAsymX1;

	private ParamItem sizeDiffNormAsymXC;

	private ParamItem sizeDiffNormAsymY0;

	private ParamItem sizeDiffNormAsymY1;

	private ParamItem sizeDiffNormAsymDropH;

	private ParamItem sizeDiffNormAsymX0H;

	private ParamItem sizeDiffNormAsymX1H;

	private ParamItem sizeDiffNormAsymXCH;

	private ParamItem sizeDiffNormAsymY0H;

	private ParamItem sizeDiffNormAsymY1H;

	private ParamItem projectionType;

	private ParamItem fuzzyedgeFlag;

	private ParamItem numLimitEnable;

	private ParamItem numLimitLow;

	private ParamItem numLimitHigh;

	private ParamItem edgeWidthLimitEnable;

	private ParamItem edgeWidthLimitLow;

	private ParamItem edgeWidthLimitHigh;

	private ParamItem edge0PointXLimitEnable;

	private ParamItem edge0PointXLimitLow;

	private ParamItem edge0PointXLimitHigh;

	private ParamItem edge0PointYLimitEnable;

	private ParamItem edge0PointYLimitLow;

	private ParamItem edge0PointYLimitHigh;

	private ParamItem edge1PointXLimitEnable;

	private ParamItem edge1PointXLimitLow;

	private ParamItem edge1PointXLimitHigh;

	private ParamItem edge1PointYLimitEnable;

	private ParamItem edge1PointYLimitLow;

	private ParamItem edge1PointYLimitHigh;

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
	public CaliperRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese 边缘模式 @~english Edge Mode
	/// </summary>
	public EdgeModeEnum EdgeMode
	{
		get
		{
			return (EdgeModeEnum)edgeMode.GetEnumValue();
		}
		set
		{
			edgeMode.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 滤波尺寸，范围：[1,50] @~english Filter Size, Range:[1,50]
	/// </summary>
	public int HalfKernelSize
	{
		get
		{
			return halfKernelSize.GetIntValue();
		}
		set
		{
			halfKernelSize.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘阈值，范围：[0,255] @~english Edge Threshold, Range:[0,255]
	/// </summary>
	public int ContrastTH
	{
		get
		{
			return contrastTH.GetIntValue();
		}
		set
		{
			contrastTH.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘0极性 @~english Edge 0 polarity
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
	/// @~chinese 边缘1极性 @~english Edge 1 polarity
	/// </summary>
	public Edge1PolarityEnum Edge1Polarity
	{
		get
		{
			return (Edge1PolarityEnum)edge1Polarity.GetEnumValue();
		}
		set
		{
			edge1Polarity.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 边缘对宽度，范围：[1,10000] @~english EdgePairWidth, Range:[1,10000]
	/// </summary>
	public int EdgePairWidth
	{
		get
		{
			return edgePairWidth.GetIntValue();
		}
		set
		{
			edgePairWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最大结果数，范围：[1,1000] @~english Max Result Number, Range:[1,1000]
	/// </summary>
	public int Maximum
	{
		get
		{
			return maximum.GetIntValue();
		}
		set
		{
			maximum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 排序方式 @~english Sort Type
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
	/// @~chinese 对比度 @~english HistContrast
	/// </summary>
	public bool ContrastEnable
	{
		get
		{
			return contrastEnable.GetBoolValue();
		}
		set
		{
			contrastEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘对对比度 @~english Edge Pair Contrast
	/// </summary>
	public bool ContrastPairEnable
	{
		get
		{
			return contrastPairEnable.GetBoolValue();
		}
		set
		{
			contrastPairEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型1 @~english Scoring1 Function
	/// </summary>
	public ContrastDropEnum ContrastDrop
	{
		get
		{
			return (ContrastDropEnum)contrastDrop.GetEnumValue();
		}
		set
		{
			contrastDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-100000,100000] @~english Startpoint, Range:[-100000,100000]
	/// </summary>
	public int ContrastX0
	{
		get
		{
			return contrastX0.GetIntValue();
		}
		set
		{
			contrastX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-100000,100000] @~english Midpoint, Range:[-100000,100000]
	/// </summary>
	public int ContrastX1
	{
		get
		{
			return contrastX1.GetIntValue();
		}
		set
		{
			contrastX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-100000,100000] @~english Endpoint, Range:[-100000,100000]
	/// </summary>
	public int ContrastXC
	{
		get
		{
			return contrastXC.GetIntValue();
		}
		set
		{
			contrastXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int ContrastY0
	{
		get
		{
			return contrastY0.GetIntValue();
		}
		set
		{
			contrastY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int ContrastY1
	{
		get
		{
			return contrastY1.GetIntValue();
		}
		set
		{
			contrastY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 灰度 @~english Grayscale
	/// </summary>
	public bool GrayscaleEnable
	{
		get
		{
			return grayscaleEnable.GetBoolValue();
		}
		set
		{
			grayscaleEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型2 @~english Scoring2 Function
	/// </summary>
	public GrayscaleDropEnum GrayscaleDrop
	{
		get
		{
			return (GrayscaleDropEnum)grayscaleDrop.GetEnumValue();
		}
		set
		{
			grayscaleDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-100000,100000] @~english Startpoint, Range:[-100000,100000]
	/// </summary>
	public int GrayscaleX0
	{
		get
		{
			return grayscaleX0.GetIntValue();
		}
		set
		{
			grayscaleX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-100000,100000] @~english Midpoint, Range:[-100000,100000]
	/// </summary>
	public int GrayscaleX1
	{
		get
		{
			return grayscaleX1.GetIntValue();
		}
		set
		{
			grayscaleX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-100000,100000] @~english Endpoint, Range:[-100000,100000]
	/// </summary>
	public int GrayscaleXC
	{
		get
		{
			return grayscaleXC.GetIntValue();
		}
		set
		{
			grayscaleXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int GrayscaleY0
	{
		get
		{
			return grayscaleY0.GetIntValue();
		}
		set
		{
			grayscaleY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int GrayscaleY1
	{
		get
		{
			return grayscaleY1.GetIntValue();
		}
		set
		{
			grayscaleY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 位置 @~english Position
	/// </summary>
	public bool PositionEnable
	{
		get
		{
			return positionEnable.GetBoolValue();
		}
		set
		{
			positionEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型3 @~english Scoring3 Function
	/// </summary>
	public PositionDropEnum PositionDrop
	{
		get
		{
			return (PositionDropEnum)positionDrop.GetEnumValue();
		}
		set
		{
			positionDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-100000,100000] @~english Startpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionX0
	{
		get
		{
			return positionX0.GetIntValue();
		}
		set
		{
			positionX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-100000,100000] @~english Midpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionX1
	{
		get
		{
			return positionX1.GetIntValue();
		}
		set
		{
			positionX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-100000,100000] @~english Endpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionXC
	{
		get
		{
			return positionXC.GetIntValue();
		}
		set
		{
			positionXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionY0
	{
		get
		{
			return positionY0.GetIntValue();
		}
		set
		{
			positionY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionY1
	{
		get
		{
			return positionY1.GetIntValue();
		}
		set
		{
			positionY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 相对位置 @~english Relative Position
	/// </summary>
	public bool PositionNegEnable
	{
		get
		{
			return positionNegEnable.GetBoolValue();
		}
		set
		{
			positionNegEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型4 @~english Scoring4 Function
	/// </summary>
	public PositionNegDropEnum PositionNegDrop
	{
		get
		{
			return (PositionNegDropEnum)positionNegDrop.GetEnumValue();
		}
		set
		{
			positionNegDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-100000,100000] @~english Startpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionNegX0
	{
		get
		{
			return positionNegX0.GetIntValue();
		}
		set
		{
			positionNegX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-100000,100000] @~english Midpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionNegX1
	{
		get
		{
			return positionNegX1.GetIntValue();
		}
		set
		{
			positionNegX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-100000,100000] @~english Endpoint, Range:[-100000,100000]
	/// </summary>
	public int PositionNegXC
	{
		get
		{
			return positionNegXC.GetIntValue();
		}
		set
		{
			positionNegXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNegY0
	{
		get
		{
			return positionNegY0.GetIntValue();
		}
		set
		{
			positionNegY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNegY1
	{
		get
		{
			return positionNegY1.GetIntValue();
		}
		set
		{
			positionNegY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 归一位置 @~english Normalized Position
	/// </summary>
	public bool PositionNormEnable
	{
		get
		{
			return positionNormEnable.GetBoolValue();
		}
		set
		{
			positionNormEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public PositionNormDropEnum PositionNormDrop
	{
		get
		{
			return (PositionNormDropEnum)positionNormDrop.GetEnumValue();
		}
		set
		{
			positionNormDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormX0
	{
		get
		{
			return positionNormX0.GetIntValue();
		}
		set
		{
			positionNormX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormX1
	{
		get
		{
			return positionNormX1.GetIntValue();
		}
		set
		{
			positionNormX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormXC
	{
		get
		{
			return positionNormXC.GetIntValue();
		}
		set
		{
			positionNormXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNormY0
	{
		get
		{
			return positionNormY0.GetIntValue();
		}
		set
		{
			positionNormY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNormY1
	{
		get
		{
			return positionNormY1.GetIntValue();
		}
		set
		{
			positionNormY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 归一相对位置 @~english Normalized Relative Pos.
	/// </summary>
	public bool PositionNormNegEnable
	{
		get
		{
			return positionNormNegEnable.GetBoolValue();
		}
		set
		{
			positionNormNegEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public PositionNormNegDropEnum PositionNormNegDrop
	{
		get
		{
			return (PositionNormNegDropEnum)positionNormNegDrop.GetEnumValue();
		}
		set
		{
			positionNormNegDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormNegX0
	{
		get
		{
			return positionNormNegX0.GetIntValue();
		}
		set
		{
			positionNormNegX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormNegX1
	{
		get
		{
			return positionNormNegX1.GetIntValue();
		}
		set
		{
			positionNormNegX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int PositionNormNegXC
	{
		get
		{
			return positionNormNegXC.GetIntValue();
		}
		set
		{
			positionNormNegXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNormNegY0
	{
		get
		{
			return positionNormNegY0.GetIntValue();
		}
		set
		{
			positionNormNegY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int PositionNormNegY1
	{
		get
		{
			return positionNormNegY1.GetIntValue();
		}
		set
		{
			positionNormNegY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 间距 @~english Interval
	/// </summary>
	public bool SizeNormEnable
	{
		get
		{
			return sizeNormEnable.GetBoolValue();
		}
		set
		{
			sizeNormEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public SizeNormDropEnum SizeNormDrop
	{
		get
		{
			return (SizeNormDropEnum)sizeNormDrop.GetEnumValue();
		}
		set
		{
			sizeNormDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeNormX0
	{
		get
		{
			return sizeNormX0.GetIntValue();
		}
		set
		{
			sizeNormX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeNormX1
	{
		get
		{
			return sizeNormX1.GetIntValue();
		}
		set
		{
			sizeNormX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeNormXC
	{
		get
		{
			return sizeNormXC.GetIntValue();
		}
		set
		{
			sizeNormXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeNormY0
	{
		get
		{
			return sizeNormY0.GetIntValue();
		}
		set
		{
			sizeNormY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeNormY1
	{
		get
		{
			return sizeNormY1.GetIntValue();
		}
		set
		{
			sizeNormY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 间距差 @~english Interval Difference
	/// </summary>
	public bool SizeDiffNormEnable
	{
		get
		{
			return sizeDiffNormEnable.GetBoolValue();
		}
		set
		{
			sizeDiffNormEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 曲线类型 @~english Scoring Function
	/// </summary>
	public SizeDiffNormDropEnum SizeDiffNormDrop
	{
		get
		{
			return (SizeDiffNormDropEnum)sizeDiffNormDrop.GetEnumValue();
		}
		set
		{
			sizeDiffNormDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormX0
	{
		get
		{
			return sizeDiffNormX0.GetIntValue();
		}
		set
		{
			sizeDiffNormX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormX1
	{
		get
		{
			return sizeDiffNormX1.GetIntValue();
		}
		set
		{
			sizeDiffNormX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormXC
	{
		get
		{
			return sizeDiffNormXC.GetIntValue();
		}
		set
		{
			sizeDiffNormXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormY0
	{
		get
		{
			return sizeDiffNormY0.GetIntValue();
		}
		set
		{
			sizeDiffNormY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormY1
	{
		get
		{
			return sizeDiffNormY1.GetIntValue();
		}
		set
		{
			sizeDiffNormY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 相对间距差 @~english Relative Interval Diff.
	/// </summary>
	public bool SizeDiffNormAsymEnable
	{
		get
		{
			return sizeDiffNormAsymEnable.GetBoolValue();
		}
		set
		{
			sizeDiffNormAsymEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 左曲线类型 @~english Left Scoring Function
	/// </summary>
	public SizeDiffNormAsymDropEnum SizeDiffNormAsymDrop
	{
		get
		{
			return (SizeDiffNormAsymDropEnum)sizeDiffNormAsymDrop.GetEnumValue();
		}
		set
		{
			sizeDiffNormAsymDrop.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymX0
	{
		get
		{
			return sizeDiffNormAsymX0.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymX0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymX1
	{
		get
		{
			return sizeDiffNormAsymX1.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymX1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymXC
	{
		get
		{
			return sizeDiffNormAsymXC.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymXC.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormAsymY0
	{
		get
		{
			return sizeDiffNormAsymY0.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymY0.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormAsymY1
	{
		get
		{
			return sizeDiffNormAsymY1.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymY1.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 右曲线类型 @~english Right Scoring Function
	/// </summary>
	public SizeDiffNormAsymDropHEnum SizeDiffNormAsymDropH
	{
		get
		{
			return (SizeDiffNormAsymDropHEnum)sizeDiffNormAsymDropH.GetEnumValue();
		}
		set
		{
			sizeDiffNormAsymDropH.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 起点，范围：[-10000,10000] @~english Startpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymX0H
	{
		get
		{
			return sizeDiffNormAsymX0H.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymX0H.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese X中点，范围：[-10000,10000] @~english Midpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymX1H
	{
		get
		{
			return sizeDiffNormAsymX1H.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymX1H.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 终点，范围：[-10000,10000] @~english Endpoint, Range:[-10000,10000]
	/// </summary>
	public int SizeDiffNormAsymXCH
	{
		get
		{
			return sizeDiffNormAsymXCH.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymXCH.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormAsymY0H
	{
		get
		{
			return sizeDiffNormAsymY0H.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymY0H.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Y计分，范围：[0,100] @~english Scoring, Range:[0,100]
	/// </summary>
	public int SizeDiffNormAsymY1H
	{
		get
		{
			return sizeDiffNormAsymY1H.GetIntValue();
		}
		set
		{
			sizeDiffNormAsymY1H.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 投影方向 @~english Projection Direction
	/// </summary>
	public ProjectionTypeEnum ProjectionType
	{
		get
		{
			return (ProjectionTypeEnum)projectionType.GetEnumValue();
		}
		set
		{
			projectionType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 模糊边缘 @~english Fuzzy Edge
	/// </summary>
	public bool FuzzyedgeFlag
	{
		get
		{
			return fuzzyedgeFlag.GetBoolValue();
		}
		set
		{
			fuzzyedgeFlag.SetBoolValue(value);
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
	/// @~chinese 宽度判断 @~english Width Check
	/// </summary>
	public bool EdgeWidthLimitEnable
	{
		get
		{
			return edgeWidthLimitEnable.GetBoolValue();
		}
		set
		{
			edgeWidthLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 宽度范围，范围：[0,99999] @~english Width Range, Range:[0,99999]
	/// </summary>
	public double EdgeWidthLimitLow
	{
		get
		{
			return edgeWidthLimitLow.GetDoubleValue();
		}
		set
		{
			edgeWidthLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 宽度范围，范围：[0,99999] @~english Width Range, Range:[0,99999]
	/// </summary>
	public double EdgeWidthLimitHigh
	{
		get
		{
			return edgeWidthLimitHigh.GetDoubleValue();
		}
		set
		{
			edgeWidthLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0X判断 @~english Edge Point 0X Check
	/// </summary>
	public bool Edge0PointXLimitEnable
	{
		get
		{
			return edge0PointXLimitEnable.GetBoolValue();
		}
		set
		{
			edge0PointXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0X范围，范围：[-99999,99999] @~english Edge Point 0X Range, Range:[-99999,99999]
	/// </summary>
	public double Edge0PointXLimitLow
	{
		get
		{
			return edge0PointXLimitLow.GetDoubleValue();
		}
		set
		{
			edge0PointXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0X范围，范围：[-99999,99999] @~english Edge Point 0X Range, Range:[-99999,99999]
	/// </summary>
	public double Edge0PointXLimitHigh
	{
		get
		{
			return edge0PointXLimitHigh.GetDoubleValue();
		}
		set
		{
			edge0PointXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0Y判断 @~english Edge Point 0Y Check
	/// </summary>
	public bool Edge0PointYLimitEnable
	{
		get
		{
			return edge0PointYLimitEnable.GetBoolValue();
		}
		set
		{
			edge0PointYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0Y范围，范围：[-99999,99999] @~english Edge Point 0Y Range, Range:[-99999,99999]
	/// </summary>
	public double Edge0PointYLimitLow
	{
		get
		{
			return edge0PointYLimitLow.GetDoubleValue();
		}
		set
		{
			edge0PointYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点0Y范围，范围：[-99999,99999] @~english Edge Point 0Y Range, Range:[-99999,99999]
	/// </summary>
	public double Edge0PointYLimitHigh
	{
		get
		{
			return edge0PointYLimitHigh.GetDoubleValue();
		}
		set
		{
			edge0PointYLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1X判断 @~english Edge Point 1X Check
	/// </summary>
	public bool Edge1PointXLimitEnable
	{
		get
		{
			return edge1PointXLimitEnable.GetBoolValue();
		}
		set
		{
			edge1PointXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1X范围，范围：[-99999,99999] @~english Edge Point 1X Range, Range:[-99999,99999]
	/// </summary>
	public double Edge1PointXLimitLow
	{
		get
		{
			return edge1PointXLimitLow.GetDoubleValue();
		}
		set
		{
			edge1PointXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1X范围，范围：[-99999,99999] @~english Edge Point 1X Range, Range:[-99999,99999]
	/// </summary>
	public double Edge1PointXLimitHigh
	{
		get
		{
			return edge1PointXLimitHigh.GetDoubleValue();
		}
		set
		{
			edge1PointXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1Y判断 @~english Edge Point 1Y Check
	/// </summary>
	public bool Edge1PointYLimitEnable
	{
		get
		{
			return edge1PointYLimitEnable.GetBoolValue();
		}
		set
		{
			edge1PointYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1Y范围，范围：[-99999,99999] @~english Edge Point 1Y Range, Range:[-99999,99999]
	/// </summary>
	public double Edge1PointYLimitLow
	{
		get
		{
			return edge1PointYLimitLow.GetDoubleValue();
		}
		set
		{
			edge1PointYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 边缘点1Y范围，范围：[-99999,99999] @~english Edge Point 1Y Range, Range:[-99999,99999]
	/// </summary>
	public double Edge1PointYLimitHigh
	{
		get
		{
			return edge1PointYLimitHigh.GetDoubleValue();
		}
		set
		{
			edge1PointYLimitHigh.SetDoubleValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public CaliperParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new CaliperRoiManager(this, "RoiType", "Full Box Sector");
		edgeMode = new ParamItem(this, "EdgeMode");
		halfKernelSize = new ParamItem(this, "HalfKernelSize", 50.0, 1.0);
		contrastTH = new ParamItem(this, "ContrastTH", 255.0);
		edgePolarity = new ParamItem(this, "EdgePolarity");
		edge1Polarity = new ParamItem(this, "Edge1Polarity");
		edgePairWidth = new ParamItem(this, "EdgePairWidth", 10000.0, 1.0);
		maximum = new ParamItem(this, "Maximum", 1000.0, 1.0);
		sortType = new ParamItem(this, "SortType");
		contrastEnable = new ParamItem(this, "ContrastEnable");
		contrastPairEnable = new ParamItem(this, "ContrastPairEnable");
		contrastDrop = new ParamItem(this, "ContrastDrop");
		contrastX0 = new ParamItem(this, "ContrastX0", 100000.0, -100000.0);
		contrastX1 = new ParamItem(this, "ContrastX1", 100000.0, -100000.0);
		contrastXC = new ParamItem(this, "ContrastXC", 100000.0, -100000.0);
		contrastY0 = new ParamItem(this, "ContrastY0", 100.0);
		contrastY1 = new ParamItem(this, "ContrastY1", 100.0);
		grayscaleEnable = new ParamItem(this, "GrayscaleEnable");
		grayscaleDrop = new ParamItem(this, "GrayscaleDrop");
		grayscaleX0 = new ParamItem(this, "GrayscaleX0", 100000.0, -100000.0);
		grayscaleX1 = new ParamItem(this, "GrayscaleX1", 100000.0, -100000.0);
		grayscaleXC = new ParamItem(this, "GrayscaleXC", 100000.0, -100000.0);
		grayscaleY0 = new ParamItem(this, "GrayscaleY0", 100.0);
		grayscaleY1 = new ParamItem(this, "GrayscaleY1", 100.0);
		positionEnable = new ParamItem(this, "PositionEnable");
		positionDrop = new ParamItem(this, "PositionDrop");
		positionX0 = new ParamItem(this, "PositionX0", 100000.0, -100000.0);
		positionX1 = new ParamItem(this, "PositionX1", 100000.0, -100000.0);
		positionXC = new ParamItem(this, "PositionXC", 100000.0, -100000.0);
		positionY0 = new ParamItem(this, "PositionY0", 100.0);
		positionY1 = new ParamItem(this, "PositionY1", 100.0);
		positionNegEnable = new ParamItem(this, "PositionNegEnable");
		positionNegDrop = new ParamItem(this, "PositionNegDrop");
		positionNegX0 = new ParamItem(this, "PositionNegX0", 100000.0, -100000.0);
		positionNegX1 = new ParamItem(this, "PositionNegX1", 100000.0, -100000.0);
		positionNegXC = new ParamItem(this, "PositionNegXC", 100000.0, -100000.0);
		positionNegY0 = new ParamItem(this, "PositionNegY0", 100.0);
		positionNegY1 = new ParamItem(this, "PositionNegY1", 100.0);
		positionNormEnable = new ParamItem(this, "PositionNormEnable");
		positionNormDrop = new ParamItem(this, "PositionNormDrop");
		positionNormX0 = new ParamItem(this, "PositionNormX0", 10000.0, -10000.0);
		positionNormX1 = new ParamItem(this, "PositionNormX1", 10000.0, -10000.0);
		positionNormXC = new ParamItem(this, "PositionNormXC", 10000.0, -10000.0);
		positionNormY0 = new ParamItem(this, "PositionNormY0", 100.0);
		positionNormY1 = new ParamItem(this, "PositionNormY1", 100.0);
		positionNormNegEnable = new ParamItem(this, "PositionNormNegEnable");
		positionNormNegDrop = new ParamItem(this, "PositionNormNegDrop");
		positionNormNegX0 = new ParamItem(this, "PositionNormNegX0", 10000.0, -10000.0);
		positionNormNegX1 = new ParamItem(this, "PositionNormNegX1", 10000.0, -10000.0);
		positionNormNegXC = new ParamItem(this, "PositionNormNegXC", 10000.0, -10000.0);
		positionNormNegY0 = new ParamItem(this, "PositionNormNegY0", 100.0);
		positionNormNegY1 = new ParamItem(this, "PositionNormNegY1", 100.0);
		sizeNormEnable = new ParamItem(this, "SizeNormEnable");
		sizeNormDrop = new ParamItem(this, "SizeNormDrop");
		sizeNormX0 = new ParamItem(this, "SizeNormX0", 10000.0, -10000.0);
		sizeNormX1 = new ParamItem(this, "SizeNormX1", 10000.0, -10000.0);
		sizeNormXC = new ParamItem(this, "SizeNormXC", 10000.0, -10000.0);
		sizeNormY0 = new ParamItem(this, "SizeNormY0", 100.0);
		sizeNormY1 = new ParamItem(this, "SizeNormY1", 100.0);
		sizeDiffNormEnable = new ParamItem(this, "SizeDiffNormEnable");
		sizeDiffNormDrop = new ParamItem(this, "SizeDiffNormDrop");
		sizeDiffNormX0 = new ParamItem(this, "SizeDiffNormX0", 10000.0, -10000.0);
		sizeDiffNormX1 = new ParamItem(this, "SizeDiffNormX1", 10000.0, -10000.0);
		sizeDiffNormXC = new ParamItem(this, "SizeDiffNormXC", 10000.0, -10000.0);
		sizeDiffNormY0 = new ParamItem(this, "SizeDiffNormY0", 100.0);
		sizeDiffNormY1 = new ParamItem(this, "SizeDiffNormY1", 100.0);
		sizeDiffNormAsymEnable = new ParamItem(this, "SizeDiffNormAsymEnable");
		sizeDiffNormAsymDrop = new ParamItem(this, "SizeDiffNormAsymDrop");
		sizeDiffNormAsymX0 = new ParamItem(this, "SizeDiffNormAsymX0", 10000.0, -10000.0);
		sizeDiffNormAsymX1 = new ParamItem(this, "SizeDiffNormAsymX1", 10000.0, -10000.0);
		sizeDiffNormAsymXC = new ParamItem(this, "SizeDiffNormAsymXC", 10000.0, -10000.0);
		sizeDiffNormAsymY0 = new ParamItem(this, "SizeDiffNormAsymY0", 100.0);
		sizeDiffNormAsymY1 = new ParamItem(this, "SizeDiffNormAsymY1", 100.0);
		sizeDiffNormAsymDropH = new ParamItem(this, "SizeDiffNormAsymDropH");
		sizeDiffNormAsymX0H = new ParamItem(this, "SizeDiffNormAsymX0H", 10000.0, -10000.0);
		sizeDiffNormAsymX1H = new ParamItem(this, "SizeDiffNormAsymX1H", 10000.0, -10000.0);
		sizeDiffNormAsymXCH = new ParamItem(this, "SizeDiffNormAsymXCH", 10000.0, -10000.0);
		sizeDiffNormAsymY0H = new ParamItem(this, "SizeDiffNormAsymY0H", 100.0);
		sizeDiffNormAsymY1H = new ParamItem(this, "SizeDiffNormAsymY1H", 100.0);
		projectionType = new ParamItem(this, "ProjectionType");
		fuzzyedgeFlag = new ParamItem(this, "FuzzyedgeFlag");
		numLimitEnable = new ParamItem(this, "NumLimitEnable");
		numLimitLow = new ParamItem(this, "NumLimitLow", 99999.0);
		numLimitHigh = new ParamItem(this, "NumLimitHigh", 99999.0);
		edgeWidthLimitEnable = new ParamItem(this, "EdgeWidthLimitEnable");
		edgeWidthLimitLow = new ParamItem(this, "EdgeWidthLimitLow", 99999.0);
		edgeWidthLimitHigh = new ParamItem(this, "EdgeWidthLimitHigh", 99999.0);
		edge0PointXLimitEnable = new ParamItem(this, "Edge0PointXLimitEnable");
		edge0PointXLimitLow = new ParamItem(this, "Edge0PointXLimitLow", 99999.0, -99999.0);
		edge0PointXLimitHigh = new ParamItem(this, "Edge0PointXLimitHigh", 99999.0, -99999.0);
		edge0PointYLimitEnable = new ParamItem(this, "Edge0PointYLimitEnable");
		edge0PointYLimitLow = new ParamItem(this, "Edge0PointYLimitLow", 99999.0, -99999.0);
		edge0PointYLimitHigh = new ParamItem(this, "Edge0PointYLimitHigh", 99999.0, -99999.0);
		edge1PointXLimitEnable = new ParamItem(this, "Edge1PointXLimitEnable");
		edge1PointXLimitLow = new ParamItem(this, "Edge1PointXLimitLow", 99999.0, -99999.0);
		edge1PointXLimitHigh = new ParamItem(this, "Edge1PointXLimitHigh", 99999.0, -99999.0);
		edge1PointYLimitEnable = new ParamItem(this, "Edge1PointYLimitEnable");
		edge1PointYLimitLow = new ParamItem(this, "Edge1PointYLimitLow", 99999.0, -99999.0);
		edge1PointYLimitHigh = new ParamItem(this, "Edge1PointYLimitHigh", 99999.0, -99999.0);
	}
}
