# D2 声明对照 · DivideImage

> 自动生成：原件 imvsdivideimagemodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 4 |
| 我方声明字段 | 4 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `OverlaprateY` | 0 |
| 2 | `OverlaprateX` | 0 |
| 3 | `NumY` | 1 |
| 4 | `NumX` | 1 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `proc.num-x` | X方向划片数量 | 基本参数/划片参数 | 1 | 50 | 1 | 0 |  |
| 2 | `proc.num-y` | Y方向划片数量 | 基本参数/划片参数 | 1 | 50 | 1 | 0 |  |
| 3 | `proc.overlap-rate-x` | X方向重叠率 (%) | 基本参数/划片参数 | 0 | 100 | 1 | 0 |  |
| 4 | `proc.overlap-rate-y` | Y方向重叠率 (%) | 基本参数/划片参数 | 0 | 100 | 1 | 0 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `OverlaprateY` | proc.overlap-rate-y | 高 |
| `OverlaprateX` | proc.overlap-rate-x | 高 |
| `NumY` | proc.num-y | 高 |
| `NumX` | proc.num-x | 高 |
