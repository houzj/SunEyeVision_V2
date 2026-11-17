# D2v2 参数逐条对照 · PixelCount

原件模块：`IMVSPixelCountModu`　原件参数：11　我方字段：7

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `LowThresh` | Low Threshold | 0x0 | 0x0 ~ 0xFF |  |
| 2 | `HighThresh` | High Threshold | 0xFF | 0x0 ~ 0xFF |  |
| 3 | `CountLimitEnable` | Quantity Check | False |  |  |
| 4 | `RangCountLimit` | Quantity Range | 1 | 0 ~ 999999999 |  |
| 5 | `CountLimitLow` | Quantity Lower Limit | 0 | 0 ~ 999999999 |  |
| 6 | `CountLimitHigh` | Quantity Upper Limit | 999999999 | 0 ~ 999999999 |  |
| 7 | `RatioLimitEnable` | Ratio Check | False |  |  |
| 8 | `RangRatioLimit` | Ratio Range | 1 | 0 ~ 1 |  |
| 9 | `RatioLimitLow` | Ratio Lower Limit | 0 | 0 ~ 1 |  |
| 10 | `RatioLimitHigh` | Ratio Upper Limit | 1 | 0 ~ 1 |  |
| 11 | `BinaryEnable` | OutImageComb | False |  |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `LowThresh` | 0x0 | — | — | **未匹配** |
| `HighThresh` | 0xFF | — | — | **未匹配** |
| `CountLimitEnable` | False | `limit.count.enabled` (数量判断) |  | 未知 |
| `RangCountLimit` | 1 | `limit.count.enabled` (数量判断) |  | 未知 |
| `CountLimitLow` | 0 | `limit.count.low` (数量范围下限) |  | 未知 |
| `CountLimitHigh` | 999999999 | `limit.count.high` (数量范围上限) |  | 未知 |
| `RatioLimitEnable` | False | `limit.ratio.enabled` (比率判断) |  | 未知 |
| `RangRatioLimit` | 1 | `limit.ratio.enabled` (比率判断) |  | 未知 |
| `RatioLimitLow` | 0 | `limit.ratio.low` (比率范围下限) |  | 未知 |
| `RatioLimitHigh` | 1 | `limit.ratio.high` (比率范围上限) |  | 未知 |
| `BinaryEnable` | False | — | — | **未匹配** |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 3

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
