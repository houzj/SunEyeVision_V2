# D2v2 参数逐条对照 · IntensityMeasure

原件模块：`IMVSIntensityMeasureModu`　原件参数：20　我方字段：15

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `MaxValueLimitEnable` | Max Value Check | False |  |  |
| 2 | `RangMaxValueLimit` | Max Value Range | 1 | 0x0 ~ 255 |  |
| 3 | `MaxValueLimitLow` | Max Value Lower Limit | 0 | 0x0 ~ 255 |  |
| 4 | `MaxValueLimitHigh` | Max Value Upper Limit | 255 | 0x0 ~ 255 |  |
| 5 | `MinValueLimitEnable` | Min Value Check | False |  |  |
| 6 | `RangMinValueLimit` | Min Value Range | 1 | 0x0 ~ 255 |  |
| 7 | `MinValueLimitLow` | Min Value Lower Limit | 0 | 0x0 ~ 255 |  |
| 8 | `MinValueLimitHigh` | Min Value Upper Limit | 255 | 0x0 ~ 255 |  |
| 9 | `MeanLimitEnable` | Mean Value Check | False |  |  |
| 10 | `RangMeanLimit` | Mean Value Range | 0 | 0 ~ 255 |  |
| 11 | `MeanLimitLow` | Mean Value Lower Limit | 0 | 0 ~ 255 |  |
| 12 | `MeanLimitHigh` | Mean Value Upper Limit | 255 | 0 ~ 255 |  |
| 13 | `StdLimitEnable` | SD Check | False |  |  |
| 14 | `RangStdLimit` | SD Range | 1 | 0 ~ 255 |  |
| 15 | `StdLimitLow` | SD lower Limit | 0 | 0 ~ 255 |  |
| 16 | `StdLimitHigh` | SD Upper Limit | 255 | 0 ~ 255 |  |
| 17 | `ContrastLimitEnable` | Contrast Judge | False |  |  |
| 18 | `ContrastLimit` | Contrast Range | 1 | 0 ~ 255 |  |
| 19 | `ContrastLimitLow` | Contrast Low | 0 | 0 ~ 255 |  |
| 20 | `ContrastLimitHigh` | Contrast High | 255 | 0 ~ 255 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `MaxValueLimitEnable` | False | `limit.max-value.enabled` (最大值判断) |  | 未知 |
| `RangMaxValueLimit` | 1 | `limit.max-value.enabled` (最大值判断) |  | 未知 |
| `MaxValueLimitLow` | 0 | `limit.max-value.low` (最大值范围下限) |  | 未知 |
| `MaxValueLimitHigh` | 255 | `limit.max-value.high` (最大值范围上限) |  | 未知 |
| `MinValueLimitEnable` | False | `limit.min-value.enabled` (最小值判断) |  | 未知 |
| `RangMinValueLimit` | 1 | `limit.min-value.enabled` (最小值判断) |  | 未知 |
| `MinValueLimitLow` | 0 | `limit.min-value.low` (最小值范围下限) |  | 未知 |
| `MinValueLimitHigh` | 255 | `limit.min-value.high` (最小值范围上限) |  | 未知 |
| `MeanLimitEnable` | False | `limit.mean.enabled` (均值判断) |  | 未知 |
| `RangMeanLimit` | 0 | `limit.mean.enabled` (均值判断) |  | 未知 |
| `MeanLimitLow` | 0 | `limit.mean.low` (均值范围下限) |  | 未知 |
| `MeanLimitHigh` | 255 | `limit.mean.high` (均值范围上限) |  | 未知 |
| `StdLimitEnable` | False | `limit.std.enabled` (标准差判断) |  | 未知 |
| `RangStdLimit` | 1 | `limit.std.enabled` (标准差判断) |  | 未知 |
| `StdLimitLow` | 0 | `limit.std.low` (标准差范围下限) |  | 未知 |
| `StdLimitHigh` | 255 | `limit.std.high` (标准差范围上限) |  | 未知 |
| `ContrastLimitEnable` | False | `limit.contrast.enabled` (直方图对比度判断) |  | 未知 |
| `ContrastLimit` | 1 | `limit.contrast.enabled` (直方图对比度判断) |  | 未知 |
| `ContrastLimitLow` | 0 | `limit.contrast.low` (对比度范围下限) |  | 未知 |
| `ContrastLimitHigh` | 255 | `limit.contrast.high` (对比度范围上限) |  | 未知 |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 0

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
