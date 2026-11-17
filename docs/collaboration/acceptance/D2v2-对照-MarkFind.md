# D2v2 参数逐条对照 · MarkFind

原件模块：`IMVSMarkFindModu`　原件参数：60　我方字段：59

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `MinScore` | Min Match Score | 0.8 | 0 ~ 1.0 |  |
| 2 | `MaxMatchNum` | RunParam_Max Number to find | 0x1 | 0x1 ~ 0xC8 |  |
| 3 | `Polarity` | Match Polarity | 0x1 |  | 有 |
| 4 | `RangAngle` | AngleRange | 0 | -90 ~ 90 |  |
| 5 | `AngleStart` | Angle Start | -90 | -90 ~ 90 |  |
| 6 | `AngleEnd` | Angle End | 90 | -90 ~ 90 |  |
| 7 | `RangScale` | Scale Range | 1 | 0.1 ~ 10 |  |
| 8 | `ScaleStart` | Scale Start | 1 | 0.1 ~ 10 |  |
| 9 | `ScaleEnd` | Scale End | 1 | 0.1 ~ 10 |  |
| 10 | `MarkLocationType` | RunParam_Mark LocationType | 0x0 |  | 有 |
| 11 | `RangScaleX` | X Scale Range | 1 | 0.1 ~ 10 |  |
| 12 | `ScaleXStart` | X Scale Start | 1 | 0.1 ~ 10 |  |
| 13 | `ScaleXEnd` | X Scale End | 1 | 0.1 ~ 10 |  |
| 14 | `RangScaleY` | Y Scale Range | 1 | 0.1 ~ 10 |  |
| 15 | `ScaleYStart` | Y Scale Start | 1 | 0.1 ~ 10 |  |
| 16 | `ScaleYEnd` | Y Scale End | 1 | 0.1 ~ 10 |  |
| 17 | `MaxOverlap` | Overlap Threshold | 0x32 | 0x0 ~ 0x64 |  |
| 18 | `SortType` | RunParam_Sort | 0x2 |  | 有 |
| 19 | `MatchThresholdFlag` | ThresholdType | 0x0 |  | 有 |
| 20 | `MatchThresholdHigh` | EdgeThreshold | 40 | 1 ~ 255 |  |
| 21 | `SpotterFlag` | Mottle Considered | False |  |  |
| 22 | `MatchExtentRate` | Extension Threshold | 0 | 0x0 ~ 90 |  |
| 23 | `TimeOut` | RunParam_Overtime Control | 0x0 | 0x0 ~ 0x2710 |  |
| 24 | `OutLineEnable` | RunParam_Contour Enabled | True |  |  |
| 25 | `NumLimitEnable` | Quantity Check | False |  |  |
| 26 | `NumAngleLimit` | Quantity Range | 0 | 0 ~ 99999 |  |
| 27 | `NumLimitLow` | Quantity Lower Limit | 0 | 0 ~ 99999 |  |
| 28 | `NumLimitHigh` | Quantity Upper Limit | 99999 | 0 ~ 99999 |  |
| 29 | `AngleLimitEnable` | Angle Check | False |  |  |
| 30 | `RangeAngleLimit` | AngleRange | 0 | -180 ~ 180 |  |
| 31 | `AngleLimitLow` | Angle Lowe Limit | -180 | -180 ~ 180 |  |
| 32 | `AngleLimitHigh` | Angle Upper Limit | 180 | -180 ~ 180 |  |
| 33 | `ScaleXLimitEnable` | X Scale Check | False |  |  |
| 34 | `RangeScaleXLimit` | X Scale Range | 0.1 | 0.0 ~ 10 |  |
| 35 | `ScaleXLimitLow` | X Scale Lower Limit | 0.1 | 0.0 ~ 10.0 |  |
| 36 | `ScaleXLimitHigh` | X Scale Upper Limit | 10.0 | 0 ~ 10.0 |  |
| 37 | `ScaleYLimitEnable` | Y Scale Check | False |  |  |
| 38 | `RangeScaleYLimit` | Y Scale Range | 0.1 | 0 ~ 10 |  |
| 39 | `ScaleYLimitLow` | Y Scale Lower Limit | 0.1 | 0.0 ~ 10.0 |  |
| 40 | `ScaleYLimitHigh` | Y Scale Upper Limit | 10.0 | 0.0 ~ 10.0 |  |
| 41 | `ScoreLimitEnable` | Score Check | False |  |  |
| 42 | `RangeScoreLimit` | Score Range | 0 | 0 ~ 1 |  |
| 43 | `ScoreLimitLow` | Score Lower Limit | 0 | 0 ~ 1 |  |
| 44 | `ScoreLimitHigh` | Score Upper Limit | 1 | 0 ~ 1 |  |
| 45 | `MatchPointXLimitEnable` | Match Point X Check | False |  |  |
| 46 | `RangeMatchPointXLimit` | Match Point X Range | 0 | -99999 ~ 99999 |  |
| 47 | `MatchPointXLimitLow` | Match Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 48 | `MatchPointXLimitHigh` | Match Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 49 | `MatchPointYLimitEnable` | Match Point Y Check | False |  |  |
| 50 | `RangeMatchPointYLimit` | Match Point Y Range | 0 | -99999 ~ 99999 |  |
| 51 | `MatchPointYLimitLow` | Match Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 52 | `MatchPointYLimitHigh` | Match Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 53 | `BoxPointXLimitEnable` | Central Point X Check | False |  |  |
| 54 | `RangeBoxPointXLimit` | Central Point X Range | 0 | -99999 ~ 99999 |  |
| 55 | `BoxPointXLimitLow` | Central Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 56 | `BoxPointXLimitHigh` | Central Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 57 | `BoxPointYLimitEnable` | Central Point Y Check | False |  |  |
| 58 | `RangeBoxPointYLimit` | Central Point Y Range | 0 | -99999 ~ 99999 |  |
| 59 | `BoxPointYLimitLow` | Central Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 60 | `BoxPointYLimitHigh` | Central Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `MinScore` | 0.8 | — | — | **未匹配** |
| `MaxMatchNum` | 0x1 | — | — | **未匹配** |
| `Polarity` | 0x1 | — | — | **未匹配** |
| `RangAngle` | 0 | — | — | **未匹配** |
| `AngleStart` | -90 | — | — | **未匹配** |
| `AngleEnd` | 90 | — | — | **未匹配** |
| `RangScale` | 1 | — | — | **未匹配** |
| `ScaleStart` | 1 | — | — | **未匹配** |
| `ScaleEnd` | 1 | — | — | **未匹配** |
| `MarkLocationType` | 0x0 | — | — | **未匹配** |
| `RangScaleX` | 1 | — | — | **未匹配** |
| `ScaleXStart` | 1 | — | — | **未匹配** |
| `ScaleXEnd` | 1 | — | — | **未匹配** |
| `RangScaleY` | 1 | — | — | **未匹配** |
| `ScaleYStart` | 1 | — | — | **未匹配** |
| `ScaleYEnd` | 1 | — | — | **未匹配** |
| `MaxOverlap` | 0x32 | — | — | **未匹配** |
| `SortType` | 0x2 | — | — | **未匹配** |
| `MatchThresholdFlag` | 0x0 | — | — | **未匹配** |
| `MatchThresholdHigh` | 40 | — | — | **未匹配** |
| `SpotterFlag` | False | — | — | **未匹配** |
| `MatchExtentRate` | 0 | — | — | **未匹配** |
| `TimeOut` | 0x0 | — | — | **未匹配** |
| `OutLineEnable` | True | — | — | **未匹配** |
| `NumLimitEnable` | False | — | — | **未匹配** |
| `NumAngleLimit` | 0 | — | — | **未匹配** |
| `NumLimitLow` | 0 | — | — | **未匹配** |
| `NumLimitHigh` | 99999 | — | — | **未匹配** |
| `AngleLimitEnable` | False | — | — | **未匹配** |
| `RangeAngleLimit` | 0 | — | — | **未匹配** |
| `AngleLimitLow` | -180 | — | — | **未匹配** |
| `AngleLimitHigh` | 180 | — | — | **未匹配** |
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

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 60

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
