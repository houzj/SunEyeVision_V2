# D2v2 参数逐条对照 · DivideImage

原件模块：`IMVSDivideImageModu`　原件参数：4　我方字段：4

## 1 原件参数（AlgorithmTab.xml）

| # | 参数名 | 中文标签 | 默认值 | 范围 | 枚举项数 |
|---|---|---|---|---|---|
| 1 | `NumX` | RunParam_NumX | 1 | 1 ~ 50 |  |
| 2 | `NumY` | RunParam_NumY | 1 | 1 ~ 50 |  |
| 3 | `OverlaprateX` | RunParam_OverlaprateX | 0 | 0 ~ 100 |  |
| 4 | `OverlaprateY` | RunParam_OverlaprateY | 0 | 0 ~ 100 |  |

## 2 名称/默认值对齐（令牌匹配）

| 原件参数 | 原厂默认 | 我方字段 | 我方默认 | 一致 |
|---|---|---|---|---|
| `NumX` | 1 | `proc.num-x` (X方向划片数量) |  | 未知 |
| `NumY` | 1 | `proc.num-y` (Y方向划片数量) |  | 未知 |
| `OverlaprateX` | 0 | — | — | **未匹配** |
| `OverlaprateY` | 0 | — | — | **未匹配** |

## 3 小结：默认值一致 0 · 不一致 0 · 未匹配 2

> 未匹配不等于缺陷：我方字段命名是语义 id，且显示字段不在原厂运行参数内。需人工逐条确认。
