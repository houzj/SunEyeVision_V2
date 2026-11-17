# D2 声明对照 · GrayMatch

> 自动生成：原件 imvsgraymatchmoduva / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 33 |
| 我方声明字段 | 39 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `UseMatchAllMode` | False |
| 2 | `EndPyramidLevel` | 0 |
| 3 | `OKWhenNumIsZero` | False |
| 4 | `BoxPointXLimitEnable` | False |
| 5 | `NumLimitLow` | 0 |
| 6 | `AngleLimitEnable` | False |
| 7 | `BoxPointXLimitLow` | -99999 |
| 8 | `AngleLimitHigh` | 180 |
| 9 | `SortType` | 2 |
| 10 | `ScoreLimitEnable` | False |
| 11 | `MatchPointXLimitLow` | -99999 |
| 12 | `NumLimitEnable` | False |
| 13 | `ScoreLimitHigh` | 1 |
| 14 | `MatchPointYLimitEnable` | False |
| 15 | `TimeOut` | 0 |
| 16 | `AngleStart` | -45 |
| 17 | `MatchingPolarity` | 1 |
| 18 | `Polarity` | 1 |
| 19 | `MatchPointYLimitLow` | -99999 |
| 20 | `NumLimitHigh` | 99999 |
| 21 | `MatchPointYLimitHigh` | 99999 |
| 22 | `AngleEnd` | 45 |
| 23 | `MinScore` | 0.5 |
| 24 | `MaxOverlap` | 40 |
| 25 | `MatchPointXLimitHigh` | 99999 |
| 26 | `BoxPointYLimitEnable` | False |
| 27 | `MaxMatchNum` | 1 |
| 28 | `BoxPointXLimitHigh` | 99999 |
| 29 | `BoxPointYLimitLow` | -99999 |
| 30 | `BoxPointYLimitHigh` | 99999 |
| 31 | `ScoreLimitLow` | 0 |
| 32 | `MatchPointXLimitEnable` | False |
| 33 | `AngleLimitLow` | -180 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `field:b8580a42f1114557f847023b47f7f9e2` | 模板数据 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 2 | `field:7d5324d90f8107177685d888236e708f` | 金字塔层数标记 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 3 | `field:cf10f7797f531c777d210beb2a0559ef` | 金字塔层数 | 模板参数/模板参数 | 1 | 8 | 1 | 0 |  |
| 4 | `field:9d245ffa8bb9353c140dc374fac7d6ff` | 角度步长 | 模板参数/模板参数 | 1 | 50 | 1 | 0 |  |
| 5 | `field:e2225d013a556295452484d4c1229aff` | 起始角度 | 模板参数/模板参数 | -180 | 180 | 1 | 0 |  |
| 6 | `field:4e8b56c7eb10b188cef0e2b3d58735a2` | 终止角度 | 模板参数/模板参数 | -180 | 180 | 1 | 0 |  |
| 7 | `field:bfc5e616c10cdd5ebfa67730f7c9379e` | 最小分数 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 8 | `field:2c544c0d61b587f1abc157af062a8470` | 最大匹配个数 | 运行参数/运行参数 | 1 | 50000 | 1 | 0 |  |
| 9 | `field:b80687f3d616673bd9d3e44e60323bad` | 最大重叠率 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 10 | `field:c1a3519e1d2e414c3f517500a605b4c2` | 极性 | 运行参数/运行参数 | 0 | 1 | 1 | 0 |  |
| 11 | `field:13c5cc20f6a8567d891545cc4bd4a272` | 匹配极性 | 运行参数/运行参数 | 0 | 1 | 1 | 0 |  |
| 12 | `field:71e2b4a8f0354cc3fe8df2e3525af4df` | 角度下限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 13 | `field:8c5146451f7f8648cc588b23a68ed46f` | 角度上限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 14 | `field:4c1f5433261a5c27a6982c255ee81765` | 排序方式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 15 | `field:9031a854bf63b5e7c25de623ac087488` | 超时时间(毫秒) | 运行参数/运行参数 | 0 | 10000 | 1 | 0 |  |
| 16 | `field:4386be054d2ab684bc19a1e7bbdc39cd` | 匹配结束层 | 运行参数/运行参数 | 0 | 8 | 1 | 0 |  |
| 17 | `field:b63fa059414fafb467e95e47818fb6a0` | 全部搜索模式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 18 | `field:ba7865fcc25578d906aae2cea6dcf235` | 个数为0时判定OK | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 19 | `field:73ddab0765adfac96ba01e85c1317d94` | 匹配个数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 20 | `field:1bbeba772fdeae0725f9385e0b066c06` | 匹配个数下限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 21 | `field:94bcc19d559a76237b0df31d7b7fd376` | 匹配个数上限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 22 | `field:993e63f095ed4e528253fc9d5cd3898a` | 角度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 23 | `field:cec874a726735217e9f0f75160ae6edf` | 角度下限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 24 | `field:45b1489de68ecc9b41677e702c0bbb03` | 角度上限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 25 | `field:2c30a8b78de873ac9c1a527385477314` | 分数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 26 | `field:05bb20321ad7df00e540d948ffa66c12` | 分数下限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 27 | `field:ebceb67147b2229816cd0b27c674c1a7` | 分数上限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 28 | `field:5fcc40cbdc48f686122ce29987658c16` | 匹配点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 29 | `field:ce758782bafcbf73498a9674b1f8e3f7` | 匹配点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 30 | `field:281c274e54872b05530404d4d20c4284` | 匹配点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 31 | `field:0e59731d0de378afcf607832a5f44503` | 匹配点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 32 | `field:0e80a20a716b259914beb9ff332271e4` | 匹配点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 33 | `field:dfb7c312a91ef3f59e11cef156496bb4` | 匹配点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 34 | `field:74843e07132466477c4397fc8de3c251` | Box点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 35 | `field:29995fc1bf705640a94f0fce8304eef2` | Box点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 36 | `field:83ba893cd97f6489b930e939a6832b06` | Box点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 37 | `field:d5054500eb1a2b7441338f27c7fa72a6` | Box点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 38 | `field:884fe1821c3c224b018957816e98b8d8` | Box点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 39 | `field:6c9d3dc83b53e00cbc177566f3af35e3` | Box点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `UseMatchAllMode` |  | **无匹配** |
| `EndPyramidLevel` |  | **无匹配** |
| `OKWhenNumIsZero` |  | **无匹配** |
| `BoxPointXLimitEnable` |  | **无匹配** |
| `NumLimitLow` |  | **无匹配** |
| `AngleLimitEnable` |  | **无匹配** |
| `BoxPointXLimitLow` |  | **无匹配** |
| `AngleLimitHigh` |  | **无匹配** |
| `SortType` |  | **无匹配** |
| `ScoreLimitEnable` |  | **无匹配** |
| `MatchPointXLimitLow` |  | **无匹配** |
| `NumLimitEnable` |  | **无匹配** |
| `ScoreLimitHigh` |  | **无匹配** |
| `MatchPointYLimitEnable` |  | **无匹配** |
| `TimeOut` |  | **无匹配** |
| `AngleStart` |  | **无匹配** |
| `MatchingPolarity` |  | **无匹配** |
| `Polarity` |  | **无匹配** |
| `MatchPointYLimitLow` |  | **无匹配** |
| `NumLimitHigh` |  | **无匹配** |
| `MatchPointYLimitHigh` |  | **无匹配** |
| `AngleEnd` |  | **无匹配** |
| `MinScore` |  | **无匹配** |
| `MaxOverlap` |  | **无匹配** |
| `MatchPointXLimitHigh` |  | **无匹配** |
| `BoxPointYLimitEnable` |  | **无匹配** |
| `MaxMatchNum` |  | **无匹配** |
| `BoxPointXLimitHigh` |  | **无匹配** |
| `BoxPointYLimitLow` |  | **无匹配** |
| `BoxPointYLimitHigh` |  | **无匹配** |
| `ScoreLimitLow` |  | **无匹配** |
| `MatchPointXLimitEnable` |  | **无匹配** |
| `AngleLimitLow` |  | **无匹配** |
