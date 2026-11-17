# D2v2 参数逐条对照 · BlobLabels

原件模块：`IMVSBlobFindLabelsModu`　原件参数：101　我方字段：82

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `LabelWiseAreaSelectEnable` | Enable label-level area filtering | False |  |  |
| 2 | `FindNum` | RunParam_Number to Find | 0x64 | 0x1 ~ 0xc8 |  |
| 3 | `SelectByArea` | Global Area Enable | False |  |  |
| 4 | `RangArea` | RunParam_GlobalAreaRange | 10000 | 0x1 ~ 999999999 |  |
| 5 | `MinArea` | Min Area | 10 | 0x1 ~ 999999999 |  |
| 6 | `MaxArea` | Max Area | 999999999 | 0x1 ~ 999999999 |  |
| 7 | `BolbOutLineEnable` | RunParam_BolbOutLineEnable | False |  |  |
| 8 | `BolbImageEnable` | RunParam_BolbImageEnable | True |  |  |
| 9 | `SelectByPerimeter` | RunParam_Perimeter Enable | False |  |  |
| 10 | `RangPerimeter` | Perimeter Range | 1000 | 0x1 ~ 999999999 |  |
| 11 | `MinPerimeter` | Min Perimeter | 10 | 0x1 ~ 999999999 |  |
| 12 | `MaxPerimeter` | Max Perimeter | 999999999 | 0x1 ~ 999999999 |  |
| 13 | `SelectByShortAxis` | Minor Axis Enable | False |  |  |
| 14 | `RangShortAxis` | Minor Axis Range | 1000 | 0x1 ~ 999999999 |  |
| 15 | `MinShortAxis` | Min Minor Axis | 1 | 0x1 ~ 999999999 |  |
| 16 | `MaxShortAxis` | Max Minor Axis | 999999999 | 0x1 ~ 999999999 |  |
| 17 | `SelectByLongAxis` | Major Axis Enable | False |  |  |
| 18 | `MinLongAxis` | Min Major Axis | 10 | 0x1 ~ 999999999 |  |
| 19 | `MaxLongAxis` | Max Major Axis | 999999999 | 0x1 ~ 999999999 |  |
| 20 | `SelectByCircularuty` | Circularity Enable | False |  |  |
| 21 | `RangCircularity` | Circularity Range | 1 | 0 ~ 1 |  |
| 22 | `MinCircularity` | Min Circularity | 0.1 | 0 ~ 1 |  |
| 23 | `MaxCircularity` | Max Circularity | 1 | 0 ~ 1 |  |
| 24 | `SelectByRectangularity` | Rectangularity Enable | False |  |  |
| 25 | `RangRectangularity` | Rectangularity Range | 1 | 0 ~ 1 |  |
| 26 | `MinRectangularity` | Min Rectangularity | 0.1 | 0 ~ 1 |  |
| 27 | `MaxRectangularity` | Max Rectangularity | 1.0 | 0 ~ 1 |  |
| 28 | `SelectByCentraBias` | RunParam_Barycenter Offset Enable | False |  |  |
| 29 | `RangCenterBias` | RunParam_Barycenter Offset Range | 10 | 0 ~ 9999999.99 |  |
| 30 | `MinCenterBias` | Min Barycenter Offset | 0 | 0 ~ 9999999.99 |  |
| 31 | `MaxCenterBias` | Max Barycenter Offset | 9999999.99 | 0 ~ 9999999.99 |  |
| 32 | `AxisRatioEnable` | RunParam_Axial Ratio | False |  |  |
| 33 | `AxisRatioRange` | RunParam_Axial Ratio | 1 | 0.01 ~ 1 |  |
| 34 | `MinAxisRatio` | MinAxis | 0.01 | 0.01 ~ 1 |  |
| 35 | `MaxAxisRatio` | RunParam_MaxAxis | 1.0 | 0 ~ 1 |  |
| 36 | `SortFeature` | Ranking | 1 |  | 有 |
| 37 | `SortMode` | Sort Type | 2 |  | 有 |
| 38 | `connectivity` | Connectivity | 0x8 |  | 有 |
| 39 | `OverlapRatio` | MinOverlap | 0 | 0 ~ 100 |  |
| 40 | `MaxOutPixelNumEnable` | RunParam_MaxOutPixelNumEnable | False |  |  |
| 41 | `MaxOutPixelNum` | MaxOutPixelNum | 0 | 0 ~ 9000000 |  |
| 42 | `BlobNumLimitEnable` | Blob Number Check | False |  |  |
| 43 | `RangBlobNumLimit` | Blob Number Range | 1 | 1 ~ 99999 |  |
| 44 | `BlobNumLimitLow` | Blob Number Lower Limit | 1 | 1 ~ 99999 |  |
| 45 | `BlobNumLimitHigh` | Blob Number Upper Limit | 99999 | 0 ~ 99999 |  |
| 46 | `BlobAreaLimitEnable` | Blob Area Check | False |  |  |
| 47 | `RangBlobAreaLimit` | Blob Area Range | 99999 | 1 ~ 999999999 |  |
| 48 | `BlobAreaLimitLow` | Blob Area Lower Limit | 1 | 1 ~ 999999999 |  |
| 49 | `BlobAreaLimitHigh` | Blob Area Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 50 | `BlobTotalAreaLimitEnable` | RunParam_Blob Total Area Check | False |  |  |
| 51 | `RangBlobTotalAreaLimit` | RunParam_Blob Total Area Range | 99999 | 1 ~ 999999999 |  |
| 52 | `BlobTotalAreaLimitLow` | Blob Total Area Lower Limit | 1 | 1 ~ 999999999 |  |
| 53 | `BlobTotalAreaLimitHigh` | Blob Total Area Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 54 | `CentroidXLimitEnable` | Barycenter X Check | False |  |  |
| 55 | `RangCentroidXLimit` | Barycenter X Range | 99999 | -99999 ~ 99999 |  |
| 56 | `CentroidXLimitLow` | Barycenter X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 57 | `CentroidXLimitHigh` | Barycenter X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 58 | `CentroidYLimitEnable` | Barycenter Y Check | False |  |  |
| 59 | `RangCentroidYLimit` | Barycenter Y Range | 99999 | -99999 ~ 99999 |  |
| 60 | `CentroidYLimitLow` | Barycenter Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 61 | `CentroidYLimitHigh` | Barycenter Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 62 | `AngleLimitEnable` | RunParam_BoxAngle Check | False |  |  |
| 63 | `RangAngleLimit` | RunParam_BoxAngle Range | 100 | -180 ~ 180 |  |
| 64 | `AngleLimitLow` | Angle Lowe Limit | -180 | -180 ~ 180 |  |
| 65 | `AngleLimitHigh` | Angle Upper Limit | 180 | -180 ~ 180 |  |
| 66 | `CenterXLimitEnable` | Center X Check | False |  |  |
| 67 | `RangCenterXLimit` | Center X Range | 99999 | -99999 ~ 99999 |  |
| 68 | `CenterXLimitLow` | Center X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 69 | `CenterXLimitHigh` | Center X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 70 | `CenterYLimitEnable` | Center Y Check | False |  |  |
| 71 | `RangCenterYLimit` | Center Y Range | 99999 | -99999 ~ 99999 |  |
| 72 | `CenterYLimitLow` | Center Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 73 | `CenterYLimitHigh` | Center Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 74 | `BoxHeightLimitEnable` | Box Height Check | False |  |  |
| 75 | `BoxHeightLimit` | RunParam_Box Height Range | 99999 | 1 ~ 99999 |  |
| 76 | `BoxHeightLimitLow` | RunParam_Box Height Lower Limit | 1 | 1 ~ 99999 |  |
| 77 | `BoxHeightLimitHigh` | RunParam_Box Height Upper Limit | 99999 | 1 ~ 99999 |  |
| 78 | `BoxWidthLimitEnable` | RunParam_Box Width Check | False |  |  |
| 79 | `BoxWidthLimit` | RunParam_Box Width Range | 99999 | 1 ~ 99999 |  |
| 80 | `BoxWidthLimitLow` | RunParam_Box Width Lower Limit | 1 | 1 ~ 99999 |  |
| 81 | `BoxWidthLimitHigh` | RunParam_Box Width Upper Limit | 99999 | 1 ~ 99999 |  |
| 82 | `PerimeterLimitEnable` | Perimeter Check | False |  |  |
| 83 | `RangPerimeterLimit` | Perimeter Range | 1000000 | 1 ~ 999999999 |  |
| 84 | `PerimeterLimitLow` | Perimeter Lower Limit | 1 | 1 ~ 999999999 |  |
| 85 | `PerimeterLimitHigh` | Perimeter Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 86 | `LongAxisLimitEnable` | Major Axis Check | False |  |  |
| 87 | `RangLongAxisLimit` | Major Axis Range | 1000000 | 1 ~ 999999999 |  |
| 88 | `LongAxisLimitLow` | Major Axis Lower Limit | 1 | 1 ~ 999999999 |  |
| 89 | `LongAxisLimitHigh` | Major Axis Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 90 | `ShortAxisLimitEnable` | Minor Axis Check | False |  |  |
| 91 | `RangShortAxisLimit` | Minor Axis Range | 1000000 | 1 ~ 999999999 |  |
| 92 | `ShortAxisLimitLow` | Minor Axis Lower Limit | 1 | 1 ~ 999999999 |  |
| 93 | `ShortAxisLimitHigh` | Minor Axis Upper Limit | 999999999 | 1 ~ 999999999 |  |
| 94 | `CircularityLimitEnable` | Circularity Check | False |  |  |
| 95 | `RangCircularityLimit` | Circularity Range | 100 | 0 ~ 1 |  |
| 96 | `CircularityLimitLow` | Circularity Lower Limit | 0 | 0 ~ 1 |  |
| 97 | `CircularityLimitHigh` | Circularity Upper Limit | 1 | 0 ~ 1 |  |
| 98 | `RectangularityLimitEnable` | Rectangularity Check | False |  |  |
| 99 | `RangRectangularityLimit` | Rectangularity Range | 100 | 0 ~ 1 |  |
| 100 | `RectangularityLimitLow` | Rectangularity Lower Limit | 0 | 0 ~ 1 |  |
| 101 | `RectangularityLimitHigh` | Rectangularity Upper Limit | 1 | 0 ~ 1 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `LabelWiseAreaSelectEnable` | False | — | — | **未匹配** |
| `FindNum` | 0x64 | — | — | **未匹配** |
| `SelectByArea` | False | — | — | **未匹配** |
| `RangArea` | 10000 | — | — | **未匹配** |
| `MinArea` | 10 | — | — | **未匹配** |
| `MaxArea` | 999999999 | — | — | **未匹配** |
| `BolbOutLineEnable` | False | — | — | **未匹配** |
| `BolbImageEnable` | True | — | — | **未匹配** |
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
| `OverlapRatio` | 0 | — | — | **未匹配** |
| `MaxOutPixelNumEnable` | False | — | — | **未匹配** |
| `MaxOutPixelNum` | 0 | — | — | **未匹配** |
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

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 101

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
