using VM.PlatformSDKCS;

namespace IMVSBlobFindModuCs;

/// <summary>
/// @~chinese Blob分析参数 @~english Blob Analysis Param
/// </summary>
public class BlobFindParam : CModuleParamBase
{
	/// <summary>
	/// @~chinese 阈值方式 @~english Threshold Mode
	/// </summary>
	public enum ThresholdTypeEnum
	{
		/// <summary>
		/// @~chinese 不进行二值化 @~english No Binarization
		/// </summary>
		BinaryImage = 1,
		/// <summary>
		/// @~chinese 单阈值 @~english Single Threshold
		/// </summary>
		SingleThreshold,
		/// <summary>
		/// @~chinese 双阈值 @~english Double Threshold
		/// </summary>
		DoubleThreshold,
		/// <summary>
		/// @~chinese 自动阈值 @~english Auto Threshold
		/// </summary>
		AutoThreshold,
		/// <summary>
		/// @~chinese 软阈值（固定） @~english Soft Threshld(Fixed)
		/// </summary>
		FixedSoftThreshold,
		/// <summary>
		/// @~chinese 软阈值（相对） @~english Soft Threshold(Relative)
		/// </summary>
		RelativeSoftThreshold
	}

	/// <summary>
	/// @~chinese 极性 @~english Polarity
	/// </summary>
	public enum PolarityEnum
	{
		/// <summary>
		/// @~chinese 亮于背景 @~english Brighter Than Background
		/// </summary>
		DarkOnBright,
		/// <summary>
		/// @~chinese 暗于背景 @~english Darker Than Background
		/// </summary>
		BrightOnDark
	}

	/// <summary>
	/// @~chinese 输出轮廓类型 @~english Output Contour Type
	/// </summary>
	public enum BlobContourTypeEnum
	{
		/// <summary>
		/// @~chinese 不输出轮廓 @~english No Contour Output
		/// </summary>
		NoneContour,
		/// <summary>
		/// @~chinese 输出无序轮廓 @~english None-Sequential Contour
		/// </summary>
		NoneSequentialContour,
		/// <summary>
		/// @~chinese 输出有序轮廓 @~english Sequential Contour
		/// </summary>
		SequentialContour
	}

	/// <summary>
	/// @~chinese 排序特征 @~english Sort Feature
	/// </summary>
	public enum SortFeatureEnum
	{
		/// <summary>
		/// @~chinese 面积 @~english Area
		/// </summary>
		SortFeatureArea = 1,
		/// <summary>
		/// @~chinese 周长 @~english Perimeter
		/// </summary>
		SortFeaturePerimeter,
		/// <summary>
		/// @~chinese 圆形度 @~english CircleScore
		/// </summary>
		SortFeatureCircularity,
		/// <summary>
		/// @~chinese 矩形度 @~english RectScore
		/// </summary>
		SortFeatureRect,
		/// <summary>
		/// @~chinese 连通域中心x @~english Connectivity center X
		/// </summary>
		SortFeatureCentroidX,
		/// <summary>
		/// @~chinese 连通域中心y @~english Connectivity center Y
		/// </summary>
		SortFeatureCentroidY,
		/// <summary>
		/// @~chinese box角度 @~english box angle
		/// </summary>
		SortFeatureBoxAngle,
		/// <summary>
		/// @~chinese box宽 @~english box width
		/// </summary>
		SortFeatureBoxWidth,
		/// <summary>
		/// @~chinese box高 @~english box height
		/// </summary>
		SortFeatureBoxHeight,
		/// <summary>
		/// @~chinese 矩形左上顶点x @~english Rectangle left upper x
		/// </summary>
		SortFeatureRectX,
		/// <summary>
		/// @~chinese 矩形左上顶点y @~english Rectangle left upper y
		/// </summary>
		SortFeatureRectY,
		/// <summary>
		/// @~chinese 二阶中心距主轴角度 @~english Two step spindle angle
		/// </summary>
		SortFeatureAxisAngle,
		/// <summary>
		/// @~chinese 轴比(box短轴/box长轴) @~english MinAxis / LongAxis
		/// </summary>
		SortFeatureAxisRatio
	}

	/// <summary>
	/// @~chinese 排序方式 @~english Sort Type
	/// </summary>
	public enum SortModeEnum
	{
		/// <summary>
		/// @~chinese 升序 @~english Ascending
		/// </summary>
		SortModeAscend = 1,
		/// <summary>
		/// @~chinese 降序 @~english Descending
		/// </summary>
		SortModeDecend,
		/// <summary>
		/// @~chinese 不排序 @~english Not Sorted
		/// </summary>
		SortModeNotSort
	}

	/// <summary>
	/// @~chinese 连通性 @~english Connectivity 
	/// </summary>
	public enum ConnectivityEnum
	{
		/// <summary>
		/// @~chinese 8连通 @~english 8-connected
		/// </summary>
		Connected_8 = 8,
		/// <summary>
		/// @~chinese 4连通 @~english 4-connected
		/// </summary>
		Connected_4 = 4
	}

	private InputItem inputImage;

	private ParamItem thresholdType;

	private ParamItem polarity;

	private ParamItem lowThreshold;

	private ParamItem hightThreshold;

	private ParamItem lowSoftThreshold;

	private ParamItem hightSoftThreshold;

	private ParamItem softness;

	private ParamItem softLeftRatio;

	private ParamItem softRightRatio;

	private ParamItem softLowRatio;

	private ParamItem softHighRatio;

	private ParamItem findNum;

	private ParamItem holeMinArea;

	private ParamItem selectByArea;

	private ParamItem minArea;

	private ParamItem maxArea;

	private ParamItem bolbOutLineEnable;

	private ParamItem blobContourType;

	private ParamItem bolbImageEnable;

	private ParamItem binaryImageEnable;

	private ParamItem selectByHistInfo;

	private ParamItem selectByBoxAngle;

	private ParamItem minBoxAngle;

	private ParamItem maxBoxAngle;

	private ParamItem selectByRectWidth;

	private ParamItem minRectWidth;

	private ParamItem maxRectWidth;

	private ParamItem selectByRectHeight;

	private ParamItem minRectHeight;

	private ParamItem maxRectHeight;

	private ParamItem selectByPerimeter;

	private ParamItem minPerimeter;

	private ParamItem maxPerimeter;

	private ParamItem selectByShortAxis;

	private ParamItem minShortAxis;

	private ParamItem maxShortAxis;

	private ParamItem selectByLongAxis;

	private ParamItem minLongAxis;

	private ParamItem maxLongAxis;

	private ParamItem selectByCircularuty;

	private ParamItem minCircularity;

	private ParamItem maxCircularity;

	private ParamItem selectByRectangularity;

	private ParamItem minRectangularity;

	private ParamItem maxRectangularity;

	private ParamItem selectByCentraBias;

	private ParamItem minCenterBias;

	private ParamItem maxCenterBias;

	private ParamItem axisRatioEnable;

	private ParamItem minAxisRatio;

	private ParamItem maxAxisRatio;

	private ParamItem sortFeature;

	private ParamItem sortMode;

	private ParamItem connectivity;

	private ParamItem maxAreaInnerRectNum;

	private ParamItem overlapRatio;

	private ParamItem maxOutPixelNumEnable;

	private ParamItem maxOutPixelNum;

	private ParamItem oKWhenNumIsZero;

	private ParamItem blobNumLimitEnable;

	private ParamItem blobNumLimitLow;

	private ParamItem blobNumLimitHigh;

	private ParamItem blobAreaLimitEnable;

	private ParamItem blobAreaLimitLow;

	private ParamItem blobAreaLimitHigh;

	private ParamItem blobTotalAreaLimitEnable;

	private ParamItem blobTotalAreaLimitLow;

	private ParamItem blobTotalAreaLimitHigh;

	private ParamItem centroidXLimitEnable;

	private ParamItem centroidXLimitLow;

	private ParamItem centroidXLimitHigh;

	private ParamItem centroidYLimitEnable;

	private ParamItem centroidYLimitLow;

	private ParamItem centroidYLimitHigh;

	private ParamItem angleLimitEnable;

	private ParamItem angleLimitLow;

	private ParamItem angleLimitHigh;

	private ParamItem centerXLimitEnable;

	private ParamItem centerXLimitLow;

	private ParamItem centerXLimitHigh;

	private ParamItem centerYLimitEnable;

	private ParamItem centerYLimitLow;

	private ParamItem centerYLimitHigh;

	private ParamItem boxHeightLimitEnable;

	private ParamItem boxHeightLimitLow;

	private ParamItem boxHeightLimitHigh;

	private ParamItem boxWidthLimitEnable;

	private ParamItem boxWidthLimitLow;

	private ParamItem boxWidthLimitHigh;

	private ParamItem perimeterLimitEnable;

	private ParamItem perimeterLimitLow;

	private ParamItem perimeterLimitHigh;

	private ParamItem longAxisLimitEnable;

	private ParamItem longAxisLimitLow;

	private ParamItem longAxisLimitHigh;

	private ParamItem shortAxisLimitEnable;

	private ParamItem shortAxisLimitLow;

	private ParamItem shortAxisLimitHigh;

	private ParamItem circularityLimitEnable;

	private ParamItem circularityLimitLow;

	private ParamItem circularityLimitHigh;

	private ParamItem rectangularityLimitEnable;

	private ParamItem rectangularityLimitLow;

	private ParamItem rectangularityLimitHigh;

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
	public BlobFindRoiManager ModuRoiManager { get; internal set; }

	/// <summary>
	/// @~chinese 阈值方式 @~english Threshold Mode
	/// </summary>
	public ThresholdTypeEnum ThresholdType
	{
		get
		{
			return (ThresholdTypeEnum)thresholdType.GetEnumValue();
		}
		set
		{
			thresholdType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 极性 @~english Polarity
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
	public int HightThreshold
	{
		get
		{
			return hightThreshold.GetIntValue();
		}
		set
		{
			hightThreshold.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 阈值范围，范围：[0,255] @~english Threshold Range, Range:[0,255]
	/// </summary>
	public int LowSoftThreshold
	{
		get
		{
			return lowSoftThreshold.GetIntValue();
		}
		set
		{
			lowSoftThreshold.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 阈值范围，范围：[0,255] @~english Threshold Range, Range:[0,255]
	/// </summary>
	public int HightSoftThreshold
	{
		get
		{
			return hightSoftThreshold.GetIntValue();
		}
		set
		{
			hightSoftThreshold.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 软阈值柔和度，范围：[0,254] @~english Soft Threshold Softness, Range:[0,254]
	/// </summary>
	public int Softness
	{
		get
		{
			return softness.GetIntValue();
		}
		set
		{
			softness.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 低尾部比例，范围：[0,100] @~english Low Tail Ratio, Range:[0,100]
	/// </summary>
	public int SoftLeftRatio
	{
		get
		{
			return softLeftRatio.GetIntValue();
		}
		set
		{
			softLeftRatio.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 高尾部比例，范围：[0,100] @~english High Tail Ratio, Range:[0,100]
	/// </summary>
	public int SoftRightRatio
	{
		get
		{
			return softRightRatio.GetIntValue();
		}
		set
		{
			softRightRatio.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 低阈值比例，范围：[0,100] @~english Low Threshold Ratio, Range:[0,100]
	/// </summary>
	public int SoftLowRatio
	{
		get
		{
			return softLowRatio.GetIntValue();
		}
		set
		{
			softLowRatio.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 高阈值比例，范围：[0,100] @~english High Threshold Ratio, Range:[0,100]
	/// </summary>
	public int SoftHighRatio
	{
		get
		{
			return softHighRatio.GetIntValue();
		}
		set
		{
			softHighRatio.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 查找个数，范围：[1,20000] @~english Number of Fund Blobs, Range:[1,20000]
	/// </summary>
	public int FindNum
	{
		get
		{
			return findNum.GetIntValue();
		}
		set
		{
			findNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 填充面积阈值，范围：[0,999999999] @~english Fill Area Threshold, Range:[0,999999999]
	/// </summary>
	public int HoleMinArea
	{
		get
		{
			return holeMinArea.GetIntValue();
		}
		set
		{
			holeMinArea.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 面积使能 @~english Area Enable
	/// </summary>
	public bool SelectByArea
	{
		get
		{
			return selectByArea.GetBoolValue();
		}
		set
		{
			selectByArea.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 面积范围，范围：[1,999999999] @~english Area Range, Range:[1,999999999]
	/// </summary>
	public int MinArea
	{
		get
		{
			return minArea.GetIntValue();
		}
		set
		{
			minArea.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 面积范围，范围：[1,999999999] @~english Area Range, Range:[1,999999999]
	/// </summary>
	public int MaxArea
	{
		get
		{
			return maxArea.GetIntValue();
		}
		set
		{
			maxArea.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 轮廓输出使能(废弃) @~english Contour Output(Obsoleted)
	/// </summary>
	public bool BolbOutLineEnable
	{
		get
		{
			return bolbOutLineEnable.GetBoolValue();
		}
		set
		{
			bolbOutLineEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 输出轮廓类型 @~english Output Contour Type
	/// </summary>
	public BlobContourTypeEnum BlobContourType
	{
		get
		{
			return (BlobContourTypeEnum)blobContourType.GetEnumValue();
		}
		set
		{
			blobContourType.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese Blob图像输出 @~english Blob Image Output
	/// </summary>
	public bool BolbImageEnable
	{
		get
		{
			return bolbImageEnable.GetBoolValue();
		}
		set
		{
			bolbImageEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 二值化图输出 @~english Binary Image Enable
	/// </summary>
	public bool BinaryImageEnable
	{
		get
		{
			return binaryImageEnable.GetBoolValue();
		}
		set
		{
			binaryImageEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 直方图信息输出 @~english Hist Info Output
	/// </summary>
	public bool SelectByHistInfo
	{
		get
		{
			return selectByHistInfo.GetBoolValue();
		}
		set
		{
			selectByHistInfo.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度使能 @~english Angle Enable
	/// </summary>
	public bool SelectByBoxAngle
	{
		get
		{
			return selectByBoxAngle.GetBoolValue();
		}
		set
		{
			selectByBoxAngle.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-9000,9000] @~english Angle Range, Range:[-9000,9000]
	/// </summary>
	public int MinBoxAngle
	{
		get
		{
			return minBoxAngle.GetIntValue();
		}
		set
		{
			minBoxAngle.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 角度范围，范围：[-9000,9000] @~english Angle Range, Range:[-9000,9000]
	/// </summary>
	public int MaxBoxAngle
	{
		get
		{
			return maxBoxAngle.GetIntValue();
		}
		set
		{
			maxBoxAngle.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT宽使能 @~english RectWidth Enable
	/// </summary>
	public bool SelectByRectWidth
	{
		get
		{
			return selectByRectWidth.GetBoolValue();
		}
		set
		{
			selectByRectWidth.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT宽范围，范围：[1,1000000] @~english RectWidth Range, Range:[1,1000000]
	/// </summary>
	public int MinRectWidth
	{
		get
		{
			return minRectWidth.GetIntValue();
		}
		set
		{
			minRectWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT宽范围，范围：[1,1000000] @~english RectWidth Range, Range:[1,1000000]
	/// </summary>
	public int MaxRectWidth
	{
		get
		{
			return maxRectWidth.GetIntValue();
		}
		set
		{
			maxRectWidth.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT高使能 @~english RectHeight Enable
	/// </summary>
	public bool SelectByRectHeight
	{
		get
		{
			return selectByRectHeight.GetBoolValue();
		}
		set
		{
			selectByRectHeight.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT高范围，范围：[1,1000000] @~english RectHeight Range, Range:[1,1000000]
	/// </summary>
	public int MinRectHeight
	{
		get
		{
			return minRectHeight.GetIntValue();
		}
		set
		{
			minRectHeight.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese RECT高范围，范围：[1,1000000] @~english RectHeight Range, Range:[1,1000000]
	/// </summary>
	public int MaxRectHeight
	{
		get
		{
			return maxRectHeight.GetIntValue();
		}
		set
		{
			maxRectHeight.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长使能 @~english Perimeter
	/// </summary>
	public bool SelectByPerimeter
	{
		get
		{
			return selectByPerimeter.GetBoolValue();
		}
		set
		{
			selectByPerimeter.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长范围，范围：[1,999999999] @~english Perimeter Range, Range:[1,999999999]
	/// </summary>
	public int MinPerimeter
	{
		get
		{
			return minPerimeter.GetIntValue();
		}
		set
		{
			minPerimeter.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长范围，范围：[1,999999999] @~english Perimeter Range, Range:[1,999999999]
	/// </summary>
	public int MaxPerimeter
	{
		get
		{
			return maxPerimeter.GetIntValue();
		}
		set
		{
			maxPerimeter.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴使能 @~english Minor Axis
	/// </summary>
	public bool SelectByShortAxis
	{
		get
		{
			return selectByShortAxis.GetBoolValue();
		}
		set
		{
			selectByShortAxis.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴范围，范围：[1,999999999] @~english Minor Axis Range, Range:[1,999999999]
	/// </summary>
	public int MinShortAxis
	{
		get
		{
			return minShortAxis.GetIntValue();
		}
		set
		{
			minShortAxis.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴范围，范围：[1,999999999] @~english Minor Axis Range, Range:[1,999999999]
	/// </summary>
	public int MaxShortAxis
	{
		get
		{
			return maxShortAxis.GetIntValue();
		}
		set
		{
			maxShortAxis.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴使能 @~english Major Axis
	/// </summary>
	public bool SelectByLongAxis
	{
		get
		{
			return selectByLongAxis.GetBoolValue();
		}
		set
		{
			selectByLongAxis.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴范围，范围：[1,999999999] @~english Major Axis Range, Range:[1,999999999]
	/// </summary>
	public int MinLongAxis
	{
		get
		{
			return minLongAxis.GetIntValue();
		}
		set
		{
			minLongAxis.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴范围，范围：[1,999999999] @~english Major Axis Range, Range:[1,999999999]
	/// </summary>
	public int MaxLongAxis
	{
		get
		{
			return maxLongAxis.GetIntValue();
		}
		set
		{
			maxLongAxis.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度使能 @~english Circularity
	/// </summary>
	public bool SelectByCircularuty
	{
		get
		{
			return selectByCircularuty.GetBoolValue();
		}
		set
		{
			selectByCircularuty.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度范围，范围：[0,1] @~english Circularity Range, Range:[0,1]
	/// </summary>
	public double MinCircularity
	{
		get
		{
			return minCircularity.GetDoubleValue();
		}
		set
		{
			minCircularity.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度范围，范围：[0,1] @~english Circularity Range, Range:[0,1]
	/// </summary>
	public double MaxCircularity
	{
		get
		{
			return maxCircularity.GetDoubleValue();
		}
		set
		{
			maxCircularity.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度使能 @~english Rectangularity
	/// </summary>
	public bool SelectByRectangularity
	{
		get
		{
			return selectByRectangularity.GetBoolValue();
		}
		set
		{
			selectByRectangularity.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度范围，范围：[0,1] @~english Rectangularity Range, Range:[0,1]
	/// </summary>
	public double MinRectangularity
	{
		get
		{
			return minRectangularity.GetDoubleValue();
		}
		set
		{
			minRectangularity.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度范围，范围：[0,1] @~english Rectangularity Range, Range:[0,1]
	/// </summary>
	public double MaxRectangularity
	{
		get
		{
			return maxRectangularity.GetDoubleValue();
		}
		set
		{
			maxRectangularity.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心偏移使能 @~english Barycenter Offset
	/// </summary>
	public bool SelectByCentraBias
	{
		get
		{
			return selectByCentraBias.GetBoolValue();
		}
		set
		{
			selectByCentraBias.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心偏移范围，范围：[0,9999999.99] @~english Barycenter Offset Range, Range:[0,9999999.99]
	/// </summary>
	public double MinCenterBias
	{
		get
		{
			return minCenterBias.GetDoubleValue();
		}
		set
		{
			minCenterBias.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心偏移范围，范围：[0,9999999.99] @~english Barycenter Offset Range, Range:[0,9999999.99]
	/// </summary>
	public double MaxCenterBias
	{
		get
		{
			return maxCenterBias.GetDoubleValue();
		}
		set
		{
			maxCenterBias.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 轴比范围 @~english Axial Ratio
	/// </summary>
	public bool AxisRatioEnable
	{
		get
		{
			return axisRatioEnable.GetBoolValue();
		}
		set
		{
			axisRatioEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 轴比范围，范围：[0.01,1] @~english Axial Ratio, Range:[0.01,1]
	/// </summary>
	public double MinAxisRatio
	{
		get
		{
			return minAxisRatio.GetDoubleValue();
		}
		set
		{
			minAxisRatio.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 轴比范围，范围：[0.01,1] @~english Axial Ratio, Range:[0.01,1]
	/// </summary>
	public double MaxAxisRatio
	{
		get
		{
			return maxAxisRatio.GetDoubleValue();
		}
		set
		{
			maxAxisRatio.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 排序特征 @~english Sort Feature
	/// </summary>
	public SortFeatureEnum SortFeature
	{
		get
		{
			return (SortFeatureEnum)sortFeature.GetEnumValue();
		}
		set
		{
			sortFeature.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 排序方式 @~english Sort Type
	/// </summary>
	public SortModeEnum SortMode
	{
		get
		{
			return (SortModeEnum)sortMode.GetEnumValue();
		}
		set
		{
			sortMode.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 连通性 @~english Connectivity 
	/// </summary>
	public ConnectivityEnum Connectivity
	{
		get
		{
			return (ConnectivityEnum)connectivity.GetEnumValue();
		}
		set
		{
			connectivity.SetEnumValue((int)value);
		}
	}

	/// <summary>
	/// @~chinese 最大内接矩形输出数量，范围：[0,10] @~english Max Area Inner Rect Output Number, Range:[0,10]
	/// </summary>
	public int MaxAreaInnerRectNum
	{
		get
		{
			return maxAreaInnerRectNum.GetIntValue();
		}
		set
		{
			maxAreaInnerRectNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最小重叠率，范围：[0,100] @~english Min. Overlap Rate, Range:[0,100]
	/// </summary>
	public int OverlapRatio
	{
		get
		{
			return overlapRatio.GetIntValue();
		}
		set
		{
			overlapRatio.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese 像素超界使能 @~english RunParam_MaxOutPixelNumEnable
	/// </summary>
	public bool MaxOutPixelNumEnable
	{
		get
		{
			return maxOutPixelNumEnable.GetBoolValue();
		}
		set
		{
			maxOutPixelNumEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 最大超界像素，范围：[0,9000000] @~english MaxOutPixelNum, Range:[0,9000000]
	/// </summary>
	public int MaxOutPixelNum
	{
		get
		{
			return maxOutPixelNum.GetIntValue();
		}
		set
		{
			maxOutPixelNum.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob个数为0 @~english OKWhenNumIsZero
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
	/// @~chinese Blob个数判断 @~english Blob Number Check
	/// </summary>
	public bool BlobNumLimitEnable
	{
		get
		{
			return blobNumLimitEnable.GetBoolValue();
		}
		set
		{
			blobNumLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob个数范围，范围：[1,99999] @~english Blob Number Range, Range:[1,99999]
	/// </summary>
	public int BlobNumLimitLow
	{
		get
		{
			return blobNumLimitLow.GetIntValue();
		}
		set
		{
			blobNumLimitLow.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob个数范围，范围：[1,99999] @~english Blob Number Range, Range:[1,99999]
	/// </summary>
	public int BlobNumLimitHigh
	{
		get
		{
			return blobNumLimitHigh.GetIntValue();
		}
		set
		{
			blobNumLimitHigh.SetIntValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob面积判断 @~english Blob Area Check
	/// </summary>
	public bool BlobAreaLimitEnable
	{
		get
		{
			return blobAreaLimitEnable.GetBoolValue();
		}
		set
		{
			blobAreaLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob面积范围，范围：[1,999999999] @~english Blob Area Range, Range:[1,999999999]
	/// </summary>
	public double BlobAreaLimitLow
	{
		get
		{
			return blobAreaLimitLow.GetDoubleValue();
		}
		set
		{
			blobAreaLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob面积范围，范围：[1,999999999] @~english Blob Area Range, Range:[1,999999999]
	/// </summary>
	public double BlobAreaLimitHigh
	{
		get
		{
			return blobAreaLimitHigh.GetDoubleValue();
		}
		set
		{
			blobAreaLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Blob总面积 @~english Blob Total Area Check
	/// </summary>
	public bool BlobTotalAreaLimitEnable
	{
		get
		{
			return blobTotalAreaLimitEnable.GetBoolValue();
		}
		set
		{
			blobTotalAreaLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 总面积范围，范围：[1,999999999] @~english Blob Total Area Range, Range:[1,999999999]
	/// </summary>
	public double BlobTotalAreaLimitLow
	{
		get
		{
			return blobTotalAreaLimitLow.GetDoubleValue();
		}
		set
		{
			blobTotalAreaLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 总面积范围，范围：[1,999999999] @~english Blob Total Area Range, Range:[1,999999999]
	/// </summary>
	public double BlobTotalAreaLimitHigh
	{
		get
		{
			return blobTotalAreaLimitHigh.GetDoubleValue();
		}
		set
		{
			blobTotalAreaLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心X判断 @~english Barycenter X Check
	/// </summary>
	public bool CentroidXLimitEnable
	{
		get
		{
			return centroidXLimitEnable.GetBoolValue();
		}
		set
		{
			centroidXLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心X范围，范围：[-99999,99999] @~english Barycenter X Range, Range:[-99999,99999]
	/// </summary>
	public double CentroidXLimitLow
	{
		get
		{
			return centroidXLimitLow.GetDoubleValue();
		}
		set
		{
			centroidXLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心X范围，范围：[-99999,99999] @~english Barycenter X Range, Range:[-99999,99999]
	/// </summary>
	public double CentroidXLimitHigh
	{
		get
		{
			return centroidXLimitHigh.GetDoubleValue();
		}
		set
		{
			centroidXLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心Y判断 @~english Barycenter Y Check
	/// </summary>
	public bool CentroidYLimitEnable
	{
		get
		{
			return centroidYLimitEnable.GetBoolValue();
		}
		set
		{
			centroidYLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心Y范围，范围：[-99999,99999] @~english Barycenter Y Range, Range:[-99999,99999]
	/// </summary>
	public double CentroidYLimitLow
	{
		get
		{
			return centroidYLimitLow.GetDoubleValue();
		}
		set
		{
			centroidYLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 质心Y范围，范围：[-99999,99999] @~english Barycenter Y Range, Range:[-99999,99999]
	/// </summary>
	public double CentroidYLimitHigh
	{
		get
		{
			return centroidYLimitHigh.GetDoubleValue();
		}
		set
		{
			centroidYLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box角度判断 @~english BoxAngle Check
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
	/// @~chinese Box角度范围，范围：[-180,180] @~english BoxAngle Range, Range:[-180,180]
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
	/// @~chinese Box角度范围，范围：[-180,180] @~english BoxAngle Range, Range:[-180,180]
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
	/// @~chinese 中心X范围，范围：[-99999,99999] @~english Center X Range, Range:[-99999,99999]
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
	/// @~chinese 中心X范围，范围：[-99999,99999] @~english Center X Range, Range:[-99999,99999]
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
	/// @~chinese Box高度判断 @~english Box Height Check
	/// </summary>
	public bool BoxHeightLimitEnable
	{
		get
		{
			return boxHeightLimitEnable.GetBoolValue();
		}
		set
		{
			boxHeightLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box高度范围，范围：[1,99999] @~english Box Height Range, Range:[1,99999]
	/// </summary>
	public double BoxHeightLimitLow
	{
		get
		{
			return boxHeightLimitLow.GetDoubleValue();
		}
		set
		{
			boxHeightLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box高度范围，范围：[1,99999] @~english Box Height Range, Range:[1,99999]
	/// </summary>
	public double BoxHeightLimitHigh
	{
		get
		{
			return boxHeightLimitHigh.GetDoubleValue();
		}
		set
		{
			boxHeightLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box宽度判断 @~english Box Width Check
	/// </summary>
	public bool BoxWidthLimitEnable
	{
		get
		{
			return boxWidthLimitEnable.GetBoolValue();
		}
		set
		{
			boxWidthLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box宽度范围，范围：[1,99999] @~english Box Width Range, Range:[1,99999]
	/// </summary>
	public double BoxWidthLimitLow
	{
		get
		{
			return boxWidthLimitLow.GetDoubleValue();
		}
		set
		{
			boxWidthLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese Box宽度范围，范围：[1,99999] @~english Box Width Range, Range:[1,99999]
	/// </summary>
	public double BoxWidthLimitHigh
	{
		get
		{
			return boxWidthLimitHigh.GetDoubleValue();
		}
		set
		{
			boxWidthLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长判断 @~english Perimeter Check
	/// </summary>
	public bool PerimeterLimitEnable
	{
		get
		{
			return perimeterLimitEnable.GetBoolValue();
		}
		set
		{
			perimeterLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长范围，范围：[1,999999999] @~english Perimeter Range, Range:[1,999999999]
	/// </summary>
	public double PerimeterLimitLow
	{
		get
		{
			return perimeterLimitLow.GetDoubleValue();
		}
		set
		{
			perimeterLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 周长范围，范围：[1,999999999] @~english Perimeter Range, Range:[1,999999999]
	/// </summary>
	public double PerimeterLimitHigh
	{
		get
		{
			return perimeterLimitHigh.GetDoubleValue();
		}
		set
		{
			perimeterLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴判断 @~english Major Axis Check
	/// </summary>
	public bool LongAxisLimitEnable
	{
		get
		{
			return longAxisLimitEnable.GetBoolValue();
		}
		set
		{
			longAxisLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴范围，范围：[1,999999999] @~english Major Axis Range, Range:[1,999999999]
	/// </summary>
	public double LongAxisLimitLow
	{
		get
		{
			return longAxisLimitLow.GetDoubleValue();
		}
		set
		{
			longAxisLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 长轴范围，范围：[1,999999999] @~english Major Axis Range, Range:[1,999999999]
	/// </summary>
	public double LongAxisLimitHigh
	{
		get
		{
			return longAxisLimitHigh.GetDoubleValue();
		}
		set
		{
			longAxisLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴判断 @~english Minor Axis Check
	/// </summary>
	public bool ShortAxisLimitEnable
	{
		get
		{
			return shortAxisLimitEnable.GetBoolValue();
		}
		set
		{
			shortAxisLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴范围，范围：[1,999999999] @~english Minor Axis Range, Range:[1,999999999]
	/// </summary>
	public double ShortAxisLimitLow
	{
		get
		{
			return shortAxisLimitLow.GetDoubleValue();
		}
		set
		{
			shortAxisLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 短轴范围，范围：[1,999999999] @~english Minor Axis Range, Range:[1,999999999]
	/// </summary>
	public double ShortAxisLimitHigh
	{
		get
		{
			return shortAxisLimitHigh.GetDoubleValue();
		}
		set
		{
			shortAxisLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度判断 @~english Circularity Check
	/// </summary>
	public bool CircularityLimitEnable
	{
		get
		{
			return circularityLimitEnable.GetBoolValue();
		}
		set
		{
			circularityLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度范围，范围：[0,1] @~english Circularity Range, Range:[0,1]
	/// </summary>
	public double CircularityLimitLow
	{
		get
		{
			return circularityLimitLow.GetDoubleValue();
		}
		set
		{
			circularityLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 圆形度范围，范围：[0,1] @~english Circularity Range, Range:[0,1]
	/// </summary>
	public double CircularityLimitHigh
	{
		get
		{
			return circularityLimitHigh.GetDoubleValue();
		}
		set
		{
			circularityLimitHigh.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度判断 @~english Rectangularity Check
	/// </summary>
	public bool RectangularityLimitEnable
	{
		get
		{
			return rectangularityLimitEnable.GetBoolValue();
		}
		set
		{
			rectangularityLimitEnable.SetBoolValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度范围，范围：[0,1] @~english Rectangularity Range, Range:[0,1]
	/// </summary>
	public double RectangularityLimitLow
	{
		get
		{
			return rectangularityLimitLow.GetDoubleValue();
		}
		set
		{
			rectangularityLimitLow.SetDoubleValue(value);
		}
	}

	/// <summary>
	/// @~chinese 矩形度范围，范围：[0,1] @~english Rectangularity Range, Range:[0,1]
	/// </summary>
	public double RectangularityLimitHigh
	{
		get
		{
			return rectangularityLimitHigh.GetDoubleValue();
		}
		set
		{
			rectangularityLimitHigh.SetDoubleValue(value);
		}
	}

	/// @cond
	/// <summary>
	/// @~chinese 构造函数 @~english Constructor
	/// </summary>
	public BlobFindParam(uint moduleID)
		: base(moduleID)
	{
		inputImage = new InputItem(this, InputItem.InputType.INPUT_TYPE_IMAGE, new string[4] { "InImage", "InImageWidth", "InImageHeight", "InImagePixelFormat" });
		ModuRoiManager = new BlobFindRoiManager(this, "RoiType", "Full Box SectorRing Polygon Sector");
		thresholdType = new ParamItem(this, "ThresholdType");
		polarity = new ParamItem(this, "Polarity");
		lowThreshold = new ParamItem(this, "LowThreshold", 255.0);
		hightThreshold = new ParamItem(this, "HightThreshold", 255.0);
		lowSoftThreshold = new ParamItem(this, "LowSoftThreshold", 255.0);
		hightSoftThreshold = new ParamItem(this, "HightSoftThreshold", 255.0);
		softness = new ParamItem(this, "Softness", 254.0);
		softLeftRatio = new ParamItem(this, "SoftLeftRatio", 100.0);
		softRightRatio = new ParamItem(this, "SoftRightRatio", 100.0);
		softLowRatio = new ParamItem(this, "SoftLowRatio", 100.0);
		softHighRatio = new ParamItem(this, "SoftHighRatio", 100.0);
		findNum = new ParamItem(this, "FindNum", 20000.0, 1.0);
		holeMinArea = new ParamItem(this, "HoleMinArea", 999999999.0);
		selectByArea = new ParamItem(this, "SelectByArea");
		minArea = new ParamItem(this, "MinArea", 999999999.0, 1.0);
		maxArea = new ParamItem(this, "MaxArea", 999999999.0, 1.0);
		bolbOutLineEnable = new ParamItem(this, "BolbOutLineEnable");
		blobContourType = new ParamItem(this, "BlobContourType");
		bolbImageEnable = new ParamItem(this, "BolbImageEnable");
		binaryImageEnable = new ParamItem(this, "BinaryImageEnable");
		selectByHistInfo = new ParamItem(this, "SelectByHistInfo");
		selectByBoxAngle = new ParamItem(this, "SelectByBoxAngle");
		minBoxAngle = new ParamItem(this, "MinBoxAngle", 9000.0, -9000.0);
		maxBoxAngle = new ParamItem(this, "MaxBoxAngle", 9000.0, -9000.0);
		selectByRectWidth = new ParamItem(this, "SelectByRectWidth");
		minRectWidth = new ParamItem(this, "MinRectWidth", 1000000.0, 1.0);
		maxRectWidth = new ParamItem(this, "MaxRectWidth", 1000000.0, 1.0);
		selectByRectHeight = new ParamItem(this, "SelectByRectHeight");
		minRectHeight = new ParamItem(this, "MinRectHeight", 1000000.0, 1.0);
		maxRectHeight = new ParamItem(this, "MaxRectHeight", 1000000.0, 1.0);
		selectByPerimeter = new ParamItem(this, "SelectByPerimeter");
		minPerimeter = new ParamItem(this, "MinPerimeter", 999999999.0, 1.0);
		maxPerimeter = new ParamItem(this, "MaxPerimeter", 999999999.0, 1.0);
		selectByShortAxis = new ParamItem(this, "SelectByShortAxis");
		minShortAxis = new ParamItem(this, "MinShortAxis", 999999999.0, 1.0);
		maxShortAxis = new ParamItem(this, "MaxShortAxis", 999999999.0, 1.0);
		selectByLongAxis = new ParamItem(this, "SelectByLongAxis");
		minLongAxis = new ParamItem(this, "MinLongAxis", 999999999.0, 1.0);
		maxLongAxis = new ParamItem(this, "MaxLongAxis", 999999999.0, 1.0);
		selectByCircularuty = new ParamItem(this, "SelectByCircularuty");
		minCircularity = new ParamItem(this, "MinCircularity", 1.0);
		maxCircularity = new ParamItem(this, "MaxCircularity", 1.0);
		selectByRectangularity = new ParamItem(this, "SelectByRectangularity");
		minRectangularity = new ParamItem(this, "MinRectangularity", 1.0);
		maxRectangularity = new ParamItem(this, "MaxRectangularity", 1.0);
		selectByCentraBias = new ParamItem(this, "SelectByCentraBias");
		minCenterBias = new ParamItem(this, "MinCenterBias", 9999999.99);
		maxCenterBias = new ParamItem(this, "MaxCenterBias", 9999999.99);
		axisRatioEnable = new ParamItem(this, "AxisRatioEnable");
		minAxisRatio = new ParamItem(this, "MinAxisRatio", 1.0, 0.01);
		maxAxisRatio = new ParamItem(this, "MaxAxisRatio", 1.0, 0.01);
		sortFeature = new ParamItem(this, "SortFeature");
		sortMode = new ParamItem(this, "SortMode");
		connectivity = new ParamItem(this, "connectivity");
		maxAreaInnerRectNum = new ParamItem(this, "MaxAreaInnerRectNum", 10.0);
		overlapRatio = new ParamItem(this, "OverlapRatio", 100.0);
		maxOutPixelNumEnable = new ParamItem(this, "MaxOutPixelNumEnable");
		maxOutPixelNum = new ParamItem(this, "MaxOutPixelNum", 9000000.0);
		oKWhenNumIsZero = new ParamItem(this, "OKWhenNumIsZero");
		blobNumLimitEnable = new ParamItem(this, "BlobNumLimitEnable");
		blobNumLimitLow = new ParamItem(this, "BlobNumLimitLow", 99999.0, 1.0);
		blobNumLimitHigh = new ParamItem(this, "BlobNumLimitHigh", 99999.0, 1.0);
		blobAreaLimitEnable = new ParamItem(this, "BlobAreaLimitEnable");
		blobAreaLimitLow = new ParamItem(this, "BlobAreaLimitLow", 999999999.0, 1.0);
		blobAreaLimitHigh = new ParamItem(this, "BlobAreaLimitHigh", 999999999.0, 1.0);
		blobTotalAreaLimitEnable = new ParamItem(this, "BlobTotalAreaLimitEnable");
		blobTotalAreaLimitLow = new ParamItem(this, "BlobTotalAreaLimitLow", 999999999.0, 1.0);
		blobTotalAreaLimitHigh = new ParamItem(this, "BlobTotalAreaLimitHigh", 999999999.0, 1.0);
		centroidXLimitEnable = new ParamItem(this, "CentroidXLimitEnable");
		centroidXLimitLow = new ParamItem(this, "CentroidXLimitLow", 99999.0, -99999.0);
		centroidXLimitHigh = new ParamItem(this, "CentroidXLimitHigh", 99999.0, -99999.0);
		centroidYLimitEnable = new ParamItem(this, "CentroidYLimitEnable");
		centroidYLimitLow = new ParamItem(this, "CentroidYLimitLow", 99999.0, -99999.0);
		centroidYLimitHigh = new ParamItem(this, "CentroidYLimitHigh", 99999.0, -99999.0);
		angleLimitEnable = new ParamItem(this, "AngleLimitEnable");
		angleLimitLow = new ParamItem(this, "AngleLimitLow", 180.0, -180.0);
		angleLimitHigh = new ParamItem(this, "AngleLimitHigh", 180.0, -180.0);
		centerXLimitEnable = new ParamItem(this, "CenterXLimitEnable");
		centerXLimitLow = new ParamItem(this, "CenterXLimitLow", 99999.0, -99999.0);
		centerXLimitHigh = new ParamItem(this, "CenterXLimitHigh", 99999.0, -99999.0);
		centerYLimitEnable = new ParamItem(this, "CenterYLimitEnable");
		centerYLimitLow = new ParamItem(this, "CenterYLimitLow", 99999.0, -99999.0);
		centerYLimitHigh = new ParamItem(this, "CenterYLimitHigh", 99999.0, -99999.0);
		boxHeightLimitEnable = new ParamItem(this, "BoxHeightLimitEnable");
		boxHeightLimitLow = new ParamItem(this, "BoxHeightLimitLow", 99999.0, 1.0);
		boxHeightLimitHigh = new ParamItem(this, "BoxHeightLimitHigh", 99999.0, 1.0);
		boxWidthLimitEnable = new ParamItem(this, "BoxWidthLimitEnable");
		boxWidthLimitLow = new ParamItem(this, "BoxWidthLimitLow", 99999.0, 1.0);
		boxWidthLimitHigh = new ParamItem(this, "BoxWidthLimitHigh", 99999.0, 1.0);
		perimeterLimitEnable = new ParamItem(this, "PerimeterLimitEnable");
		perimeterLimitLow = new ParamItem(this, "PerimeterLimitLow", 999999999.0, 1.0);
		perimeterLimitHigh = new ParamItem(this, "PerimeterLimitHigh", 999999999.0, 1.0);
		longAxisLimitEnable = new ParamItem(this, "LongAxisLimitEnable");
		longAxisLimitLow = new ParamItem(this, "LongAxisLimitLow", 999999999.0, 1.0);
		longAxisLimitHigh = new ParamItem(this, "LongAxisLimitHigh", 999999999.0, 1.0);
		shortAxisLimitEnable = new ParamItem(this, "ShortAxisLimitEnable");
		shortAxisLimitLow = new ParamItem(this, "ShortAxisLimitLow", 999999999.0, 1.0);
		shortAxisLimitHigh = new ParamItem(this, "ShortAxisLimitHigh", 999999999.0, 1.0);
		circularityLimitEnable = new ParamItem(this, "CircularityLimitEnable");
		circularityLimitLow = new ParamItem(this, "CircularityLimitLow", 1.0);
		circularityLimitHigh = new ParamItem(this, "CircularityLimitHigh", 1.0);
		rectangularityLimitEnable = new ParamItem(this, "RectangularityLimitEnable");
		rectangularityLimitLow = new ParamItem(this, "RectangularityLimitLow", 1.0);
		rectangularityLimitHigh = new ParamItem(this, "RectangularityLimitHigh", 1.0);
	}
}
