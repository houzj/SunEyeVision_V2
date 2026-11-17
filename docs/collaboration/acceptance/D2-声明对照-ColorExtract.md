# D2 声明对照 · ColorExtract

> 自动生成：原件 imvscolorextractmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 4 |
| 我方声明字段 | 4 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `InputColorRange` | 255,0,255,0,255,0,1,0,1 |
| 2 | `AreaLimitHigh` | 9999999 |
| 3 | `AreaLimitLow` | 1 |
| 4 | `AreaLimitEnable` | False |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `proc.color-range` | 颜色区间 | 基本参数/区间列表 |  |  | 1 | 0 |  |
| 2 | `proc.area-limit-enabled` | 启用面积上下限 | 基本参数/参数设置 |  |  | 1 | 2 |  |
| 3 | `proc.area-limit-low` | 面积下限 | 基本参数/参数设置 | 0 | 9999999 | 1 | 0 |  |
| 4 | `proc.area-limit-high` | 面积上限 | 基本参数/参数设置 | 0 | 9999999 | 1 | 0 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `InputColorRange` |  | **无匹配** |
| `AreaLimitHigh` | proc.area-limit-high | 高 |
| `AreaLimitLow` | proc.area-limit-low | 高 |
| `AreaLimitEnable` | proc.area-limit-enabled | 高 |
