# D2 声明对照 · PixelCount

> 自动生成：原件 imvspixelcountmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 9 |
| 我方声明字段 | 7 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `BinaryEnable` | False |
| 2 | `HighThresh` | 255 |
| 3 | `RatioLimitEnable` | False |
| 4 | `CountLimitEnable` | False |
| 5 | `CountLimitLow` | 0 |
| 6 | `RatioLimitLow` | 0 |
| 7 | `LowThresh` | 0 |
| 8 | `CountLimitHigh` | 999999999 |
| 9 | `RatioLimitHigh` | 1 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `range.thresholds` | 阈值区间 | 运行参数/阈值范围 |  |  | 1 | 2 |  |
| 2 | `limit.count.enabled` | 数量判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 3 | `limit.count.low` | 数量范围下限 | 结果显示/结果判断 | 0 | 999999999 | 1 | 0 |  |
| 4 | `limit.count.high` | 数量范围上限 | 结果显示/结果判断 | 0 | 999999999 | 1 | 0 |  |
| 5 | `limit.ratio.enabled` | 比率判断 | 结果显示/结果判断 |  |  | 1 | 2 |  |
| 6 | `limit.ratio.low` | 比率范围下限 | 结果显示/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 7 | `limit.ratio.high` | 比率范围上限 | 结果显示/结果判断 | 0 | 1 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `BinaryEnable` |  | **无匹配** |
| `HighThresh` |  | **无匹配** |
| `RatioLimitEnable` |  | **无匹配** |
| `CountLimitEnable` |  | **无匹配** |
| `CountLimitLow` |  | **无匹配** |
| `RatioLimitLow` |  | **无匹配** |
| `LowThresh` |  | **无匹配** |
| `CountLimitHigh` |  | **无匹配** |
| `RatioLimitHigh` |  | **无匹配** |
