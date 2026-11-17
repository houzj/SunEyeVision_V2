# D2v2 参数逐条对照 · BlobAnalysis

原件模块：`IMVSBlobFindModu`　原件参数：129　我方字段：104

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `ThresholdType` | RunParam_Threshold Mode | 0x2 |  | 有 |
| 2 | `Polarity` | Polarity | 0 |  | 有 |
| 3 | `LowThreshold` | Low Threshold | 0x64 | 0x0 ~ 0xff |  |
| 4 | `HightThreshold` | High Threshold | 0x96 | 0x0 ~ 0xff |  |
| 5 | `ThresholdRangArea` | Threshold Range | 10000 | 0 ~ 255 |  |
| 6 | `LowSoftThreshold` | RunParam_Soft Low Threshold | 0x64 | 0x1 ~ 255 |  |
| 7 | `HightSoftThreshold` | RunParam_Soft High Threshold | 0x96 | 0 ~ 255 |  |
| 8 | `Softness` | Soft Threshold Softness | 254 | 0x0 ~ 254 |  |
| 9 | `SoftLeftRatio` | RunParam_Low Tail Ratio | 5 | 0x0 ~ 100 |  |
| 10 | `SoftRightRatio` | RunParam_High Tail Ratio | 5 | 0x0 ~ 100 |  |
| 11 | `SoftLowRatio` | Low Threshold Ratio | 40 | 0x0 ~ 100 |  |
| 12 | `SoftHighRatio` | High Threshold Ratio | 60 | 0x0 ~ 100 |  |
| 13 | `FindNum` | RunParam_Number to Find | 100 | 1 ~ 20000 |  |
| 14 | `HoleMinArea` | Fill Area Threshold | 0 | 0 ~ 999999999 |  |
| 15 | `SelectByArea` | Area Enable | True |  |  |
| 16 | `RangArea` | RunParam_Area Range | 10000 | 0x1 ~ 999999999 |  |
| 17 | `MinArea` | Min Area | 10 | 0x1 ~ 999999999 |  |
| 18 | `MaxArea` | Max Area | 999999999 | 0x1 ~ 999999999 |  |
| 19 | `BlobContourType` | RunParam_BlobContourType | 0 |  | 有 |
| 20 | `BolbImageEnable` | RunParam_BolbImageEnable | True |  |  |
| 21 | `BinaryImageEnable` | RunParam_BinaryImageEnable | True |  |  |
| 22 | `SelectByHistInfo` | RunParam_SelectByHistInfoEnable | False |  |  |
| 23 | `SelectByBoxAngle` | Angle Enable | False |  |  |
| 24 | `RangBoxAngle` | RunParam_Angle Range | 0x0 | -9000 ~ 9000 |  |
| 25 | `MinBoxAngle` | RunParam_Min BoxAngle | -9000 | -9000 ~ 9000 |  |
| 26 | `MaxBoxAngle` | RunParam_Max BoxAngle | 9000 | -9000 ~ 9000 |  |
| 27 | `SelectByRectWidth` | RectWidth Enable | False |  |  |
| 28 | `RangRectWidth` | RunParam_RectWidth Range | 0x0 | 1 ~ 1000000 |  |
| 29 | `MinRectWidth` | Min RectWidth | 100 | 1 ~ 1000000 |  |
| 30 | `MaxRectWidth` | Max RectWidth | 200 | 1 ~ 1000000 |  |
| 31 | `SelectByRectHeight` | RectHeight Enable | False |  |  |
| 32 | `RangRectHeight` | RunParam_RectHeight Range | 0x0 | 1 ~ 1000000 |  |
| 33 | `MinRectHeight` | Min RectHeight | 100 | 1 ~ 1000000 |  |
| 34 | `MaxRectHeight` | Max RectHeight | 200 | 1 ~ 1000000 |  |
| 35 | `SelectByPerimeter` | RunParam_Perimeter Enable | False |  |  |
| 36 | `RangPerimeter` | Perimeter Range | 1000 | 0x1 ~ 999999999 |  |
| 37 | `MinPerimeter` | Min Perimeter | 10 | 0x1 ~ 999999999 |  |
| 38 | `MaxPerimeter` | Max Perimeter | 999999999 | 0x1 ~ 999999999 |  |
| 39 | `SelectByShortAxis` | Minor Axis Enable | False |  |  |
| 40 | `RangShortAxis` | Minor Axis Range | 1000 | 0x1 ~ 999999999 |  |
| 41 | `MinShortAxis` | Min Minor Axis | 1 | 0x1 ~ 999999999 |  |
| 42 | `MaxShortAxis` | Max Minor Axis | 999999999 | 0x1 ~ 999999999 |  |
| 43 | `SelectByLongAxis` | Major Axis Enable | False |  |  |
| 44 | `MinLongAxis` | Min Major Axis | 10 | 0x1 ~ 999999999 |  |
| 45 | `MaxLongAxis` | Max Major Axis | 999999999 | 0x1 ~ 999999999 |  |
| 46 | `SelectByCircularuty` | Circularity Enable | False |  |  |
| 47 | `RangCircularity` | Circularity Range | 1 | 0 ~ 1 |  |
| 48 | `MinCircularity` | Min Circularity | 0.1 | 0 ~ 1 |  |
| 49 | `MaxCircularity` | Max Circularity | 1 | 0 ~ 1 |  |
| 50 | `SelectByRectangularity` | Rectangularity Enable | False |  |  |
| 51 | `RangRectangularity` | Rectangularity Range | 1 | 0 ~ 1 |  |
| 52 | `MinRectangularity` | Min Rectangularity | 0.1 | 0 ~ 1 |  |
| 53 | `MaxRectangularity` | Max Rectangularity | 1.0 | 0 ~ 1 |  |
| 54 | `SelectByCentraBias` | RunParam_Barycenter Offset Enable | False |  |  |
| 55 | `RangCenterBias` | RunParam_Barycenter Offset Range | 10 | 0 ~ 9999999.99 |  |
| 56 | `MinCenterBias` | Min Barycenter Offset | 0 | 0 ~ 9999999.99 |  |
| 57 | `MaxCenterBias` | Max Barycenter Offset | 9999999.99 | 0 ~ 9999999.99 |  |
| 58 | `AxisRatioEnable` | RunParam_Axial Ratio | False |  |  |
| 59 | `AxisRatioRange` | RunParam_Axial Ratio | 1 | 0.01 ~ 1 |  |
| 60 | `MinAxisRatio` | MinAxis | 0.01 | 0.01 ~ 1 |  |
| 61 | `MaxAxisRatio` | RunParam_MaxAxis | 1.0 | 0 ~ 1 |  |
| 62 | `SortFeature` | Ranking | 1 |  | 有 |
| 63 | `SortMode` | Sort Type | 2 |  | 有 |
| 64 | `connectivity` | Connectivity | 0x8 |  | 有 |
| 65 | `MaxAreaInnerRectNum` | RunParam_MaxAreaInnerRectNum | 0x0 | 0x0 ~ 10 |  |
| 66 | `OverlapRatio` | MinOverlap | 0 | 0 ~ 100 |  |
| 67 | `MaxOutPixelNumEnable` | RunParam_MaxOutPixelNumEnable | False |  |  |
| 68 | `MaxOutPixelNum` | MaxOutPixelNum | 0 | 0 ~ 9000000 |  |
| 69 | `OKWhenNumIsZero` | OKWhenNumIsZero | False |  |  |
| 70 | `BlobNumLimitEnable` | Blob Number Check | False |  |  |
| 71 | `RangBlobNumLimit` | Blob Number Range | 1 | 1 ~ 99999 |  |
| 72 | `BlobNumLimitLow` | Blob Number Lower Limit | 1 | 1 ~ 99999 |  |
| 73 | `BlobNumLimitHigh` | Blob Number Upper Limit | 99999 | 0 ~ 99999 |  |
| 74 | `BlobAreaLimitEnable` | Blob Area Check | False |  |  |
| 75 | `RangBlobAreaLimit` | Blob Area Range | 99999 | 1 ~ 999999999 |  |
| 76 | `BlobAreaLimitLow` | Blob Area Lower Limit | 1 | 1 ~ 999999999 |  |
| 77 | `BlobAreaLimitHigh` | Blob Area Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 78 | `BlobTotalAreaLimitEnable` | RunParam_Blob Total Area Check | False |  |  |
| 79 | `RangBlobTotalAreaLimit` | RunParam_Blob Total Area Range | 99999 | 1 ~ 999999999 |  |
| 80 | `BlobTotalAreaLimitLow` | Blob Total Area Lower Limit | 1 | 1 ~ 999999999 |  |
| 81 | `BlobTotalAreaLimitHigh` | Blob Total Area Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 82 | `CentroidXLimitEnable` | Barycenter X Check | False |  |  |
| 83 | `RangCentroidXLimit` | Barycenter X Range | 99999 | -99999 ~ 99999 |  |
| 84 | `CentroidXLimitLow` | Barycenter X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 85 | `CentroidXLimitHigh` | Barycenter X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 86 | `CentroidYLimitEnable` | Barycenter Y Check | False |  |  |
| 87 | `RangCentroidYLimit` | Barycenter Y Range | 99999 | -99999 ~ 99999 |  |
| 88 | `CentroidYLimitLow` | Barycenter Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 89 | `CentroidYLimitHigh` | Barycenter Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 90 | `AngleLimitEnable` | RunParam_BoxAngle Check | False |  |  |
| 91 | `RangAngleLimit` | RunParam_BoxAngle Range | 100 | -180 ~ 180 |  |
| 92 | `AngleLimitLow` | Angle Lowe Limit | -180 | -180 ~ 180 |  |
| 93 | `AngleLimitHigh` | Angle Upper Limit | 180 | -180 ~ 180 |  |
| 94 | `CenterXLimitEnable` | Center X Check | False |  |  |
| 95 | `RangCenterXLimit` | Center X Range | 99999 | -99999 ~ 99999 |  |
| 96 | `CenterXLimitLow` | Center X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 97 | `CenterXLimitHigh` | Center X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 98 | `CenterYLimitEnable` | Center Y Check | False |  |  |
| 99 | `RangCenterYLimit` | Center Y Range | 99999 | -99999 ~ 99999 |  |
| 100 | `CenterYLimitLow` | Center Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 101 | `CenterYLimitHigh` | Center Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 102 | `BoxHeightLimitEnable` | Box Height Check | False |  |  |
| 103 | `BoxHeightLimit` | RunParam_Box Height Range | 99999 | 1 ~ 99999 |  |
| 104 | `BoxHeightLimitLow` | RunParam_Box Height Lower Limit | 1 | 1 ~ 99999 |  |
| 105 | `BoxHeightLimitHigh` | RunParam_Box Height Upper Limit | 99999 | 1 ~ 99999 |  |
| 106 | `BoxWidthLimitEnable` | RunParam_Box Width Check | False |  |  |
| 107 | `BoxWidthLimit` | RunParam_Box Width Range | 99999 | 1 ~ 99999 |  |
| 108 | `BoxWidthLimitLow` | RunParam_Box Width Lower Limit | 1 | 1 ~ 99999 |  |
| 109 | `BoxWidthLimitHigh` | RunParam_Box Width Upper Limit | 99999 | 1 ~ 99999 |  |
| 110 | `PerimeterLimitEnable` | Perimeter Check | False |  |  |
| 111 | `RangPerimeterLimit` | Perimeter Range | 1000000 | 1 ~ 999999999 |  |
| 112 | `PerimeterLimitLow` | Perimeter Lower Limit | 1 | 1 ~ 999999999 |  |
| 113 | `PerimeterLimitHigh` | Perimeter Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 114 | `LongAxisLimitEnable` | Major Axis Check | False |  |  |
| 115 | `RangLongAxisLimit` | Major Axis Range | 1000000 | 1 ~ 999999999 |  |
| 116 | `LongAxisLimitLow` | Major Axis Lower Limit | 1 | 1 ~ 999999999 |  |
| 117 | `LongAxisLimitHigh` | Major Axis Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 118 | `ShortAxisLimitEnable` | Minor Axis Check | False |  |  |
| 119 | `RangShortAxisLimit` | Minor Axis Range | 1000000 | 1 ~ 999999999 |  |
| 120 | `ShortAxisLimitLow` | Minor Axis Lower Limit | 1 | 1 ~ 999999999 |  |
| 121 | `ShortAxisLimitHigh` | Minor Axis Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 122 | `CircularityLimitEnable` | Circularity Check | False |  |  |
| 123 | `RangCircularityLimit` | Circularity Range | 100 | 0 ~ 1 |  |
| 124 | `CircularityLimitLow` | Circularity Lower Limit | 0 | 0 ~ 1 |  |
| 125 | `CircularityLimitHigh` | Circularity Upper Limit | 1 | 0 ~ 1 |  |
| 126 | `RectangularityLimitEnable` | Rectangularity Check | False |  |  |
| 127 | `RangRectangularityLimit` | Rectangularity Range | 100 | 0 ~ 1 |  |
| 128 | `RectangularityLimitLow` | Rectangularity Lower Limit | 0 | 0 ~ 1 |  |
| 129 | `RectangularityLimitHigh` | Rectangularity Upper Limit | 1 | 0 ~ 1 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `ThresholdType` | 0x2 | — | — | **未匹配** |
| `Polarity` | 0 | — | — | **未匹配** |
| `LowThreshold` | 0x64 | — | — | **未匹配** |
| `HightThreshold` | 0x96 | — | — | **未匹配** |
| `ThresholdRangArea` | 10000 | — | — | **未匹配** |
| `LowSoftThreshold` | 0x64 | — | — | **未匹配** |
| `HightSoftThreshold` | 0x96 | — | — | **未匹配** |
| `Softness` | 254 | — | — | **未匹配** |
| `SoftLeftRatio` | 5 | — | — | **未匹配** |
| `SoftRightRatio` | 5 | — | — | **未匹配** |
| `SoftLowRatio` | 40 | — | — | **未匹配** |
| `SoftHighRatio` | 60 | — | — | **未匹配** |
| `FindNum` | 100 | — | — | **未匹配** |
| `HoleMinArea` | 0 | — | — | **未匹配** |
| `SelectByArea` | True | — | — | **未匹配** |
| `RangArea` | 10000 | — | — | **未匹配** |
| `MinArea` | 10 | — | — | **未匹配** |
| `MaxArea` | 999999999 | — | — | **未匹配** |
| `BlobContourType` | 0 | — | — | **未匹配** |
| `BolbImageEnable` | True | — | — | **未匹配** |
| `BinaryImageEnable` | True | — | — | **未匹配** |
| `SelectByHistInfo` | False | — | — | **未匹配** |
| `SelectByBoxAngle` | False | — | — | **未匹配** |
| `RangBoxAngle` | 0x0 | — | — | **未匹配** |
| `MinBoxAngle` | -9000 | — | — | **未匹配** |
| `MaxBoxAngle` | 9000 | — | — | **未匹配** |
| `SelectByRectWidth` | False | — | — | **未匹配** |
| `RangRectWidth` | 0x0 | — | — | **未匹配** |
| `MinRectWidth` | 100 | — | — | **未匹配** |
| `MaxRectWidth` | 200 | — | — | **未匹配** |
| `SelectByRectHeight` | False | — | — | **未匹配** |
| `RangRectHeight` | 0x0 | — | — | **未匹配** |
| `MinRectHeight` | 100 | — | — | **未匹配** |
| `MaxRectHeight` | 200 | — | — | **未匹配** |
| `SelectByPerimeter` | False | — | — | **未匹配** |
| `RangPerimeter` | 1000 | — | — | **未匹配** |
| `MinPerimeter` | 10 | — | — | **未匹配** |
| `MaxPerimeter` | 999999999 | — | — | **未匹配** |
| `SelectByShortAxis` | False | — | — | **未匹配** |
| `RangShortAxis` | 1000 | — | — | **未匹配** |
| `MinShortAxis` | 1 | — | — | **未匹配** |
| `MaxShortAxis` | 999999999 | — | — | **未匹配** |
| `SelectByLongAxis` | False | — | — | **未匹配** |
| `MinLongAxis` | 10 | — | — | **未匹配** |
| `MaxLongAxis` | 999999999 | — | — | **未匹配** |
| `SelectByCircularuty` | False | — | — | **未匹配** |
| `RangCircularity` | 1 | — | — | **未匹配** |
| `MinCircularity` | 0.1 | — | — | **未匹配** |
| `MaxCircularity` | 1 | — | — | **未匹配** |
| `SelectByRectangularity` | False | — | — | **未匹配** |
| `RangRectangularity` | 1 | — | — | **未匹配** |
| `MinRectangularity` | 0.1 | — | — | **未匹配** |
| `MaxRectangularity` | 1.0 | — | — | **未匹配** |
| `SelectByCentraBias` | False | — | — | **未匹配** |
| `RangCenterBias` | 10 | — | — | **未匹配** |
| `MinCenterBias` | 0 | — | — | **未匹配** |
| `MaxCenterBias` | 9999999.99 | — | — | **未匹配** |
| `AxisRatioEnable` | False | — | — | **未匹配** |
| `AxisRatioRange` | 1 | — | — | **未匹配** |
| `MinAxisRatio` | 0.01 | — | — | **未匹配** |
| `MaxAxisRatio` | 1.0 | — | — | **未匹配** |
| `SortFeature` | 1 | — | — | **未匹配** |
| `SortMode` | 2 | — | — | **未匹配** |
| `connectivity` | 0x8 | — | — | **未匹配** |
| `MaxAreaInnerRectNum` | 0x0 | — | — | **未匹配** |
| `OverlapRatio` | 0 | — | — | **未匹配** |
| `MaxOutPixelNumEnable` | False | — | — | **未匹配** |
| `MaxOutPixelNum` | 0 | — | — | **未匹配** |
| `OKWhenNumIsZero` | False | — | — | **未匹配** |
| `BlobNumLimitEnable` | False | — | — | **未匹配** |
| `RangBlobNumLimit` | 1 | — | — | **未匹配** |
| `BlobNumLimitLow` | 1 | — | — | **未匹配** |
| `BlobNumLimitHigh` | 99999 | — | — | **未匹配** |
| `BlobAreaLimitEnable` | False | — | — | **未匹配** |
| `RangBlobAreaLimit` | 99999 | — | — | **未匹配** |
| `BlobAreaLimitLow` | 1 | — | — | **未匹配** |
| `BlobAreaLimitHigh` | 999999999 | — | — | **未匹配** |
| `BlobTotalAreaLimitEnable` | False | — | — | **未匹配** |
| `RangBlobTotalAreaLimit` | 99999 | — | — | **未匹配** |
| `BlobTotalAreaLimitLow` | 1 | — | — | **未匹配** |
| `BlobTotalAreaLimitHigh` | 999999999 | — | — | **未匹配** |
| `CentroidXLimitEnable` | False | — | — | **未匹配** |
| `RangCentroidXLimit` | 99999 | — | — | **未匹配** |
| `CentroidXLimitLow` | -99999 | — | — | **未匹配** |
| `CentroidXLimitHigh` | 99999 | — | — | **未匹配** |
| `CentroidYLimitEnable` | False | — | — | **未匹配** |
| `RangCentroidYLimit` | 99999 | — | — | **未匹配** |
| `CentroidYLimitLow` | -99999 | — | — | **未匹配** |
| `CentroidYLimitHigh` | 99999 | — | — | **未匹配** |
| `AngleLimitEnable` | False | — | — | **未匹配** |
| `RangAngleLimit` | 100 | — | — | **未匹配** |
| `AngleLimitLow` | -180 | — | — | **未匹配** |
| `AngleLimitHigh` | 180 | — | — | **未匹配** |
| `CenterXLimitEnable` | False | — | — | **未匹配** |
| `RangCenterXLimit` | 99999 | — | — | **未匹配** |
| `CenterXLimitLow` | -99999 | — | — | **未匹配** |
| `CenterXLimitHigh` | 99999 | — | — | **未匹配** |
| `CenterYLimitEnable` | False | — | — | **未匹配** |
| `RangCenterYLimit` | 99999 | — | — | **未匹配** |
| `CenterYLimitLow` | -99999 | — | — | **未匹配** |
| `CenterYLimitHigh` | 99999 | — | — | **未匹配** |
| `BoxHeightLimitEnable` | False | — | — | **未匹配** |
| `BoxHeightLimit` | 99999 | — | — | **未匹配** |
| `BoxHeightLimitLow` | 1 | — | — | **未匹配** |
| `BoxHeightLimitHigh` | 99999 | — | — | **未匹配** |
| `BoxWidthLimitEnable` | False | — | — | **未匹配** |
| `BoxWidthLimit` | 99999 | — | — | **未匹配** |
| `BoxWidthLimitLow` | 1 | — | — | **未匹配** |
| `BoxWidthLimitHigh` | 99999 | — | — | **未匹配** |
| `PerimeterLimitEnable` | False | — | — | **未匹配** |
| `RangPerimeterLimit` | 1000000 | — | — | **未匹配** |
| `PerimeterLimitLow` | 1 | — | — | **未匹配** |
| `PerimeterLimitHigh` | 999999999 | — | — | **未匹配** |
| `LongAxisLimitEnable` | False | — | — | **未匹配** |
| `RangLongAxisLimit` | 1000000 | — | — | **未匹配** |
| `LongAxisLimitLow` | 1 | — | — | **未匹配** |
| `LongAxisLimitHigh` | 999999999 | — | — | **未匹配** |
| `ShortAxisLimitEnable` | False | — | — | **未匹配** |
| `RangShortAxisLimit` | 1000000 | — | — | **未匹配** |
| `ShortAxisLimitLow` | 1 | — | — | **未匹配** |
| `ShortAxisLimitHigh` | 999999999 | — | — | **未匹配** |
| `CircularityLimitEnable` | False | — | — | **未匹配** |
| `RangCircularityLimit` | 100 | — | — | **未匹配** |
| `CircularityLimitLow` | 0 | — | — | **未匹配** |
| `CircularityLimitHigh` | 1 | — | — | **未匹配** |
| `RectangularityLimitEnable` | False | — | — | **未匹配** |
| `RangRectangularityLimit` | 100 | — | — | **未匹配** |
| `RectangularityLimitLow` | 0 | — | — | **未匹配** |
| `RectangularityLimitHigh` | 1 | — | — | **未匹配** |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 129

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
