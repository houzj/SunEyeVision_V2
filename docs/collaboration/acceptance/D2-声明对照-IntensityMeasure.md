# D2 声明对照 · IntensityMeasure

> 自动生成：原件 imvsintensitymeasuremodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 15 |
| 我方声明字段 | 15 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `ContrastLimitHigh` | 255 |
| 2 | `ContrastLimitLow` | 0 |
| 3 | `ContrastLimitEnable` | False |
| 4 | `MinValueLimitEnable` | False |
| 5 | `StdLimitLow` | 0 |
| 6 | `MinValueLimitLow` | 0 |
| 7 | `MinValueLimitHigh` | 255 |
| 8 | `MeanLimitEnable` | False |
| 9 | `MaxValueLimitLow` | 0 |
| 10 | `MeanLimitHigh` | 255 |
| 11 | `MaxValueLimitEnable` | False |
| 12 | `StdLimitEnable` | False |
| 13 | `MaxValueLimitHigh` | 255 |
| 14 | `MeanLimitLow` | 0 |
| 15 | `StdLimitHigh` | 255 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `limit.max-value.enabled` | 最大值判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 2 | `limit.max-value.low` | 最大值范围下限 | 结果显示/结果判断 | 0 | 255 | 1 | 0 |  |
| 3 | `limit.max-value.high` | 最大值范围上限 | 结果显示/结果判断 | 0 | 255 | 1 | 0 |  |
| 4 | `limit.min-value.enabled` | 最小值判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 5 | `limit.min-value.low` | 最小值范围下限 | 结果显示/结果判断 | 0 | 255 | 1 | 0 |  |
| 6 | `limit.min-value.high` | 最小值范围上限 | 结果显示/结果判断 | 0 | 255 | 1 | 0 |  |
| 7 | `limit.mean.enabled` | 均值判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 8 | `limit.mean.low` | 均值范围下限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |
| 9 | `limit.mean.high` | 均值范围上限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |
| 10 | `limit.std.enabled` | 标准差判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 11 | `limit.std.low` | 标准差范围下限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |
| 12 | `limit.std.high` | 标准差范围上限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |
| 13 | `limit.contrast.enabled` | 直方图对比度判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 14 | `limit.contrast.low` | 对比度范围下限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |
| 15 | `limit.contrast.high` | 对比度范围上限 | 结果显示/结果判断 | 0 | 255 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `ContrastLimitHigh` |  | **无匹配** |
| `ContrastLimitLow` |  | **无匹配** |
| `ContrastLimitEnable` |  | **无匹配** |
| `MinValueLimitEnable` |  | **无匹配** |
| `StdLimitLow` |  | **无匹配** |
| `MinValueLimitLow` |  | **无匹配** |
| `MinValueLimitHigh` |  | **无匹配** |
| `MeanLimitEnable` |  | **无匹配** |
| `MaxValueLimitLow` |  | **无匹配** |
| `MeanLimitHigh` |  | **无匹配** |
| `MaxValueLimitEnable` |  | **无匹配** |
| `StdLimitEnable` |  | **无匹配** |
| `MaxValueLimitHigh` |  | **无匹配** |
| `MeanLimitLow` |  | **无匹配** |
| `StdLimitHigh` |  | **无匹配** |
