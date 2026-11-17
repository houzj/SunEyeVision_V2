# D2v2 参数逐条对照 · ContourMatch

原件模块：`IMVSContourMatchModu`　原件参数：71　我方字段：65

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `UseMatchAllMode` | RunParam_Use Match All Mode | False |  |  |
| 2 | `MultiModelMaxOverlap` | NMS Overlap Threshold | 0x32 | 0x0 ~ 0x64 |  |
| 3 | `MinScore` | Min Match Score | 0.8 | 0 ~ 1.0 |  |
| 4 | `MaxMatchNum` | RunParam_Max Number to find | 0x1 | 0x1 ~ 0xC350 |  |
| 5 | `Polarity` | Match Polarity | 0x1 |  | 有 |
| 6 | `RangAngle` | AngleRange | 0 | -180 ~ 180 |  |
| 7 | `AngleStart` | Angle Start | -180 | -180 ~ 180 |  |
| 8 | `AngleEnd` | Angle End | 180 | -180 ~ 180 |  |
| 9 | `RangeScaleType` | RangeScaleType | 0 |  | 有 |
| 10 | `RangScale` | Scale Range | 1 | 0.1 ~ 10 |  |
| 11 | `ScaleStart` | Scale Start | 1 | 0.1 ~ 10 |  |
| 12 | `ScaleEnd` | Scale End | 1 | 0.1 ~ 10 |  |
| 13 | `RangScaleX` | X Scale Range | 1 | 0.1 ~ 10 |  |
| 14 | `ScaleXStart` | X Scale Start | 1 | 0.1 ~ 10 |  |
| 15 | `ScaleXEnd` | X Scale End | 1 | 0.1 ~ 10 |  |
| 16 | `RangScaleY` | Y Scale Range | 1 | 0.1 ~ 10 |  |
| 17 | `ScaleYStart` | Y Scale Start | 1 | 0.1 ~ 10 |  |
| 18 | `ScaleYEnd` | Y Scale End | 1 | 0.1 ~ 10 |  |
| 19 | `RangSkewX` | RangSkewX | 0 | -45 ~ 45 |  |
| 20 | `SkewXStart` | SkewXStart | 0 | -45 ~ 45 |  |
| 21 | `SkewXEnd` | SkewXEnd | 0 | -45 ~ 45 |  |
| 22 | `MaxOverlap` | Overlap Threshold | 0x32 | 0x0 ~ 0x64 |  |
| 23 | `SortType` | RunParam_Sort | 0x2 |  | 有 |
| 24 | `MatchThresholdFlag` | ThresholdType | 0x0 |  | 有 |
| 25 | `MatchThresholdHigh` | EdgeThreshold | 40 | 1 ~ 255 |  |
| 26 | `SpotterFlag` | Mottle Considered | False |  |  |
| 27 | `MatchExtentRate` | Extension Threshold | 0 | 0x0 ~ 90 |  |
| 28 | `TimeOut` | RunParam_Overtime Control | 0x0 | 0x0 ~ 0x2710 |  |
| 29 | `OutLineEnable` | RunParam_Contour Enabled | True |  |  |
| 30 | `OutLinePointEnable` | RunParam_ContourPoint Enabled | False |  |  |
| 31 | `OKWhenNumIsZero` | OKWhenMatchNumIsZero | False |  |  |
| 32 | `NumLimitEnable` | Quantity Check | False |  |  |
| 33 | `NumAngleLimit` | Quantity Range | 0 | 0 ~ 99999 |  |
| 34 | `NumLimitLow` | Quantity Lower Limit | 0 | 0 ~ 99999 |  |
| 35 | `NumLimitHigh` | Quantity Upper Limit | 99999 | 0 ~ 99999 |  |
| 36 | `AngleLimitEnable` | Angle Check | False |  |  |
| 37 | `RangeAngleLimit` | AngleRange | 0 | -180 ~ 180 |  |
| 38 | `AngleLimitLow` | Angle Lowe Limit | -180 | -180 ~ 180 |  |
| 39 | `AngleLimitHigh` | Angle Upper Limit | 180 | -180 ~ 180 |  |
| 40 | `ScaleLimitEnable` | Scale Check | False |  |  |
| 41 | `RangeScaleLimit` | Scale Range | 0.1 | 0.0 ~ 10 |  |
| 42 | `ScaleLimitLow` | Scale Lower Limit | 0.1 | 0.0 ~ 10.0 |  |
| 43 | `ScaleLimitHigh` | Scale Upper Limit | 10.0 | 0.0 ~ 10.0 |  |
| 44 | `ScaleXLimitEnable` | X Scale Check | False |  |  |
| 45 | `RangeScaleXLimit` | X Scale Range | 0.1 | 0.0 ~ 10 |  |
| 46 | `ScaleXLimitLow` | X Scale Lower Limit | 0.1 | 0.0 ~ 10.0 |  |
| 47 | `ScaleXLimitHigh` | X Scale Upper Limit | 10.0 | 0 ~ 10.0 |  |
| 48 | `ScaleYLimitEnable` | Y Scale Check | False |  |  |
| 49 | `RangeScaleYLimit` | Y Scale Range | 0.1 | 0 ~ 10 |  |
| 50 | `ScaleYLimitLow` | Y Scale Lower Limit | 0.1 | 0.0 ~ 10.0 |  |
| 51 | `ScaleYLimitHigh` | Y Scale Upper Limit | 10.0 | 0.0 ~ 10.0 |  |
| 52 | `ScoreLimitEnable` | Score Check | False |  |  |
| 53 | `RangeScoreLimit` | Score Range | 0 | 0 ~ 1 |  |
| 54 | `ScoreLimitLow` | Score Lower Limit | 0 | 0 ~ 1 |  |
| 55 | `ScoreLimitHigh` | Score Upper Limit | 1 | 0 ~ 1 |  |
| 56 | `MatchPointXLimitEnable` | Match Point X Check | False |  |  |
| 57 | `RangeMatchPointXLimit` | Match Point X Range | 0 | -99999 ~ 99999 |  |
| 58 | `MatchPointXLimitLow` | Match Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 59 | `MatchPointXLimitHigh` | Match Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 60 | `MatchPointYLimitEnable` | Match Point Y Check | False |  |  |
| 61 | `RangeMatchPointYLimit` | Match Point Y Range | 0 | -99999 ~ 99999 |  |
| 62 | `MatchPointYLimitLow` | Match Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 63 | `MatchPointYLimitHigh` | Match Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 64 | `BoxPointXLimitEnable` | Central Point X Check | False |  |  |
| 65 | `RangeBoxPointXLimit` | Central Point X Range | 0 | -99999 ~ 99999 |  |
| 66 | `BoxPointXLimitLow` | Central Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 67 | `BoxPointXLimitHigh` | Central Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 68 | `BoxPointYLimitEnable` | Central Point Y Check | False |  |  |
| 69 | `RangeBoxPointYLimit` | Central Point Y Range | 0 | -99999 ~ 99999 |  |
| 70 | `BoxPointYLimitLow` | Central Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 71 | `BoxPointYLimitHigh` | Central Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `UseMatchAllMode` | False | — | — | **未匹配** |
| `MultiModelMaxOverlap` | 0x32 | — | — | **未匹配** |
| `MinScore` | 0.8 | — | — | **未匹配** |
| `MaxMatchNum` | 0x1 | — | — | **未匹配** |
| `Polarity` | 0x1 | — | — | **未匹配** |
| `RangAngle` | 0 | — | — | **未匹配** |
| `AngleStart` | -180 | — | — | **未匹配** |
| `AngleEnd` | 180 | — | — | **未匹配** |
| `RangeScaleType` | 0 | — | — | **未匹配** |
| `RangScale` | 1 | — | — | **未匹配** |
| `ScaleStart` | 1 | — | — | **未匹配** |
| `ScaleEnd` | 1 | — | — | **未匹配** |
| `RangScaleX` | 1 | — | — | **未匹配** |
| `ScaleXStart` | 1 | — | — | **未匹配** |
| `ScaleXEnd` | 1 | — | — | **未匹配** |
| `RangScaleY` | 1 | — | — | **未匹配** |
| `ScaleYStart` | 1 | — | — | **未匹配** |
| `ScaleYEnd` | 1 | — | — | **未匹配** |
| `RangSkewX` | 0 | — | — | **未匹配** |
| `SkewXStart` | 0 | — | — | **未匹配** |
| `SkewXEnd` | 0 | — | — | **未匹配** |
| `MaxOverlap` | 0x32 | — | — | **未匹配** |
| `SortType` | 0x2 | — | — | **未匹配** |
| `MatchThresholdFlag` | 0x0 | — | — | **未匹配** |
| `MatchThresholdHigh` | 40 | — | — | **未匹配** |
| `SpotterFlag` | False | — | — | **未匹配** |
| `MatchExtentRate` | 0 | — | — | **未匹配** |
| `TimeOut` | 0x0 | — | — | **未匹配** |
| `OutLineEnable` | True | — | — | **未匹配** |
| `OutLinePointEnable` | False | — | — | **未匹配** |
| `OKWhenNumIsZero` | False | — | — | **未匹配** |
| `NumLimitEnable` | False | — | — | **未匹配** |
| `NumAngleLimit` | 0 | — | — | **未匹配** |
| `NumLimitLow` | 0 | — | — | **未匹配** |
| `NumLimitHigh` | 99999 | — | — | **未匹配** |
| `AngleLimitEnable` | False | — | — | **未匹配** |
| `RangeAngleLimit` | 0 | — | — | **未匹配** |
| `AngleLimitLow` | -180 | — | — | **未匹配** |
| `AngleLimitHigh` | 180 | — | — | **未匹配** |
| `ScaleLimitEnable` | False | — | — | **未匹配** |
| `RangeScaleLimit` | 0.1 | — | — | **未匹配** |
| `ScaleLimitLow` | 0.1 | — | — | **未匹配** |
| `ScaleLimitHigh` | 10.0 | — | — | **未匹配** |
| `ScaleXLimitEnable` | False | — | — | **未匹配** |
| `RangeScaleXLimit` | 0.1 | — | — | **未匹配** |
| `ScaleXLimitLow` | 0.1 | — | — | **未匹配** |
| `ScaleXLimitHigh` | 10.0 | — | — | **未匹配** |
| `ScaleYLimitEnable` | False | — | — | **未匹配** |
| `RangeScaleYLimit` | 0.1 | — | — | **未匹配** |
| `ScaleYLimitLow` | 0.1 | — | — | **未匹配** |
| `ScaleYLimitHigh` | 10.0 | — | — | **未匹配** |
| `ScoreLimitEnable` | False | — | — | **未匹配** |
| `RangeScoreLimit` | 0 | — | — | **未匹配** |
| `ScoreLimitLow` | 0 | — | — | **未匹配** |
| `ScoreLimitHigh` | 1 | — | — | **未匹配** |
| `MatchPointXLimitEnable` | False | — | — | **未匹配** |
| `RangeMatchPointXLimit` | 0 | — | — | **未匹配** |
| `MatchPointXLimitLow` | -99999 | — | — | **未匹配** |
| `MatchPointXLimitHigh` | 99999 | — | — | **未匹配** |
| `MatchPointYLimitEnable` | False | — | — | **未匹配** |
| `RangeMatchPointYLimit` | 0 | — | — | **未匹配** |
| `MatchPointYLimitLow` | -99999 | — | — | **未匹配** |
| `MatchPointYLimitHigh` | 99999 | — | — | **未匹配** |
| `BoxPointXLimitEnable` | False | — | — | **未匹配** |
| `RangeBoxPointXLimit` | 0 | — | — | **未匹配** |
| `BoxPointXLimitLow` | -99999 | — | — | **未匹配** |
| `BoxPointXLimitHigh` | 99999 | — | — | **未匹配** |
| `BoxPointYLimitEnable` | False | — | — | **未匹配** |
| `RangeBoxPointYLimit` | 0 | — | — | **未匹配** |
| `BoxPointYLimitLow` | -99999 | — | — | **未匹配** |
| `BoxPointYLimitHigh` | 99999 | — | — | **未匹配** |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 71

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
