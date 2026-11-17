# D2v2 参数逐条对照 · ColorExtract

原件模块：`IMVSColorExtractModu`　原件参数：4　我方字段：4

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `AreaLimitEnable` | DispParam_Color Total Area Check | False |  |  |
| 2 | `RangeAreaLimit` | RunParam_Area Range | 9999999 | 0 ~ 9999999 |  |
| 3 | `AreaLimitLow` | RunParam_Area Lower Limit | 1 | 0 ~ 9999999 |  |
| 4 | `AreaLimitHigh` | RunParam_Area Upper Limit | 9999999 | 0 ~ 9999999 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `AreaLimitEnable` | False | `proc.area-limit-enabled` (启用面积上下限) |  | 未知 |
| `RangeAreaLimit` | 9999999 | `proc.area-limit-enabled` (启用面积上下限) |  | 未知 |
| `AreaLimitLow` | 1 | `proc.area-limit-low` (面积下限) |  | 未知 |
| `AreaLimitHigh` | 9999999 | `proc.area-limit-high` (面积上限) |  | 未知 |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 0

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
