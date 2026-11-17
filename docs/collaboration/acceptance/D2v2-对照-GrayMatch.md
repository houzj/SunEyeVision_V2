# D2v2 参数逐条对照 · GrayMatch

原件模块：`IMVSGrayMatchModuVA`　原件参数：41　我方字段：39

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `UseMatchAllMode` | RunParam_Use Match All Mode | False |  |  |
| 2 | `MinScore` | Min Match Score | 0.5 | 0 ~ 1 |  |
| 3 | `MaxMatchNum` | RunParam_Max Number to find | 0x1 | 0x1 ~ 0xC350 |  |
| 4 | `RangAngle` | AngleRange | 0 | -180 ~ 180 |  |
| 5 | `AngleStart` | Angle Start | -45 | -180 ~ 180 |  |
| 6 | `AngleEnd` | Angle End | 45 | -180 ~ 180 |  |
| 7 | `MaxOverlap` | Overlap Threshold | 0x28 | 0x0 ~ 0x64 |  |
| 8 | `SortType` | RunParam_Sort | 0x2 |  | 有 |
| 9 | `Polarity` | Match Polarity | 0x1 | 0x0 ~ 0x1 |  |
| 10 | `MatchingPolarity` | Match Polarity | 0x1 |  | 有 |
| 11 | `TimeOut` | RunParam_Overtime Control | 0x0 | 0x0 ~ 0x2710 |  |
| 12 | `EndPyramidLevel` | RunParam_EndPyramidLevel Feature | 0x0 | 0x0 ~ 0x8 |  |
| 13 | `OKWhenNumIsZero` | OKWhenMatchNumIsZero | False |  |  |
| 14 | `NumLimitEnable` | Quantity Check | False |  |  |
| 15 | `NumAngleLimit` | Quantity Range | 0 | 0 ~ 99999 |  |
| 16 | `NumLimitLow` | Quantity Lower Limit | 0 | 0 ~ 99999 |  |
| 17 | `NumLimitHigh` | Quantity Upper Limit | 99999 | 0 ~ 99999 |  |
| 18 | `AngleLimitEnable` | Angle Check | False |  |  |
| 19 | `RangeAngleLimit` | AngleRange | 0 | -180 ~ 180 |  |
| 20 | `AngleLimitLow` | Angle Lowe Limit | -180 | -180 ~ 180 |  |
| 21 | `AngleLimitHigh` | Angle Upper Limit | 180 | -180 ~ 180 |  |
| 22 | `ScoreLimitEnable` | Score Check | False |  |  |
| 23 | `RangeScoreLimit` | Score Range | 0 | 0 ~ 1 |  |
| 24 | `ScoreLimitLow` | Score Lower Limit | 0 | 0 ~ 1 |  |
| 25 | `ScoreLimitHigh` | Score Upper Limit | 1 | 0 ~ 1 |  |
| 26 | `MatchPointXLimitEnable` | Match Point X Check | False |  |  |
| 27 | `RangeMatchPointXLimit` | Match Point X Range | 0 | -99999 ~ 99999 |  |
| 28 | `MatchPointXLimitLow` | Match Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 29 | `MatchPointXLimitHigh` | Match Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 30 | `MatchPointYLimitEnable` | Match Point Y Check | False |  |  |
| 31 | `RangeMatchPointYLimit` | Match Point Y Range | 0 | -99999 ~ 99999 |  |
| 32 | `MatchPointYLimitLow` | Match Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 33 | `MatchPointYLimitHigh` | Match Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |
| 34 | `BoxPointXLimitEnable` | Central Point X Check | False |  |  |
| 35 | `RangeBoxPointXLimit` | Central Point X Range | 0 | -99999 ~ 99999 |  |
| 36 | `BoxPointXLimitLow` | Central Point X Lower Limit | -99999 | -99999 ~ 99999 |  |
| 37 | `BoxPointXLimitHigh` | Central Point X Upper Limit | 99999 | -99999 ~ 99999 |  |
| 38 | `BoxPointYLimitEnable` | Central Point Y Check | False |  |  |
| 39 | `RangeBoxPointYLimit` | Central Point Y Range | 0 | -99999 ~ 99999 |  |
| 40 | `BoxPointYLimitLow` | Central Point Y Lower Limit | -99999 | -99999 ~ 99999 |  |
| 41 | `BoxPointYLimitHigh` | Central Point Y Upper Limit | 99999 | -99999 ~ 99999 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `UseMatchAllMode` | False | — | — | **未匹配** |
| `MinScore` | 0.5 | — | — | **未匹配** |
| `MaxMatchNum` | 0x1 | — | — | **未匹配** |
| `RangAngle` | 0 | — | — | **未匹配** |
| `AngleStart` | -45 | — | — | **未匹配** |
| `AngleEnd` | 45 | — | — | **未匹配** |
| `MaxOverlap` | 0x28 | — | — | **未匹配** |
| `SortType` | 0x2 | — | — | **未匹配** |
| `Polarity` | 0x1 | — | — | **未匹配** |
| `MatchingPolarity` | 0x1 | — | — | **未匹配** |
| `TimeOut` | 0x0 | — | — | **未匹配** |
| `EndPyramidLevel` | 0x0 | — | — | **未匹配** |
| `OKWhenNumIsZero` | False | — | — | **未匹配** |
| `NumLimitEnable` | False | — | — | **未匹配** |
| `NumAngleLimit` | 0 | — | — | **未匹配** |
| `NumLimitLow` | 0 | — | — | **未匹配** |
| `NumLimitHigh` | 99999 | — | — | **未匹配** |
| `AngleLimitEnable` | False | — | — | **未匹配** |
| `RangeAngleLimit` | 0 | — | — | **未匹配** |
| `AngleLimitLow` | -180 | — | — | **未匹配** |
| `AngleLimitHigh` | 180 | — | — | **未匹配** |
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

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 41

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
