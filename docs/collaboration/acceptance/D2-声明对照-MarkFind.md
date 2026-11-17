# D2 声明对照 · MarkFind

> 自动生成：原件 imvsmarkfindmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 47 |
| 我方声明字段 | 59 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `MarkLocationType` | 0 |
| 2 | `OutLineEnable` | True |
| 3 | `ScaleYLimitHigh` | 10 |
| 4 | `ScaleYLimitLow` | 0.1 |
| 5 | `ScaleYLimitEnable` | False |
| 6 | `ScaleXLimitHigh` | 10 |
| 7 | `ScaleXLimitLow` | 0.1 |
| 8 | `ScaleXLimitEnable` | False |
| 9 | `ScaleYEnd` | 1 |
| 10 | `ScaleYStart` | 1 |
| 11 | `ScaleXEnd` | 1 |
| 12 | `ScaleXStart` | 1 |
| 13 | `SpotterFlag` | False |
| 14 | `MatchExtentRate` | 0 |
| 15 | `MatchThresholdHigh` | 40 |
| 16 | `MatchThresholdFlag` | 0 |
| 17 | `BoxPointXLimitEnable` | False |
| 18 | `NumLimitLow` | 0 |
| 19 | `AngleLimitEnable` | False |
| 20 | `BoxPointXLimitLow` | -99999 |
| 21 | `AngleLimitHigh` | 180 |
| 22 | `SortType` | 2 |
| 23 | `ScoreLimitEnable` | False |
| 24 | `MatchPointXLimitLow` | -99999 |
| 25 | `NumLimitEnable` | False |
| 26 | `ScoreLimitHigh` | 1 |
| 27 | `MatchPointYLimitEnable` | False |
| 28 | `TimeOut` | 0 |
| 29 | `AngleStart` | -45 |
| 30 | `Polarity` | 1 |
| 31 | `MatchPointYLimitLow` | -99999 |
| 32 | `NumLimitHigh` | 99999 |
| 33 | `MatchPointYLimitHigh` | 99999 |
| 34 | `AngleEnd` | 45 |
| 35 | `MinScore` | 0.5 |
| 36 | `MaxOverlap` | 80 |
| 37 | `MatchPointXLimitHigh` | 99999 |
| 38 | `BoxPointYLimitEnable` | False |
| 39 | `MaxMatchNum` | 1 |
| 40 | `BoxPointXLimitHigh` | 99999 |
| 41 | `BoxPointYLimitLow` | -99999 |
| 42 | `ScaleEnd` | 1 |
| 43 | `ScaleStart` | 1 |
| 44 | `BoxPointYLimitHigh` | 99999 |
| 45 | `ScoreLimitLow` | 0 |
| 46 | `MatchPointXLimitEnable` | False |
| 47 | `AngleLimitLow` | -180 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `field:7d5ed6bb373c043e68c176f660053a8b` | 模板数据 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 2 | `field:5f10fae1644c01c7a03ee93ac55a63a2` | 金字塔比例 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 3 | `field:51432b66bdc3f6d2a986220def5425b6` | 压缩比 | 模板参数/模板参数 | 1 | 20 | 1 | 0 |  |
| 4 | `field:9f94af46b8cb80647a388f981c015801` | 特征尺度 | 模板参数/模板参数 | 1 | 20 | 1 | 0 |  |
| 5 | `field:748b78ecb0e455ef59183af5bdfa8e6e` | 阈值模式 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 6 | `field:b8402e1f609e128f8ab5aa9192040bb0` | 对比度阈值 | 模板参数/模板参数 | 1 | 255 | 1 | 0 |  |
| 7 | `field:57ce2e0621ec9f7591da4affc6d0e672` | 定位方式 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 8 | `field:9c5c31e409247f1df7960cdb1abf3de1` | 旋转角度 | 模板参数/模板参数 | 5 | 45 | 1 | 0 |  |
| 9 | `field:79748150a4ba5a071d9b16a06eccd9ac` | 旋转步长 | 模板参数/模板参数 | 0.1 | 1 | 0.1 | 1 |  |
| 10 | `field:3ae3d31dc63d9a4a062f0e9a62b80d89` | 投影区间 | 模板参数/模板参数 | 1 | 10 | 1 | 0 |  |
| 11 | `field:95f859b2c3734f5715adc998384cbfde` | Mark类型 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 12 | `field:1320c588f072e53709c17d828c9723e1` | 标记类型 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 13 | `field:714299bd2c0bf69cd787b117c303741f` | 最小分数 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 14 | `field:c5d284e3dfbd57dbd56be08405f49b58` | 最大匹配数 | 运行参数/运行参数 | 1 | 1000 | 1 | 0 |  |
| 15 | `field:4139f6806772ed4d58d26e46ae574094` | 极性 | 运行参数/运行参数 | 1 | 3 | 1 | 0 |  |
| 16 | `field:13576a9f67874801c2415f1278783524` | 角度下限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 17 | `field:64671324cbc0c1947658f63b97d9fee6` | 角度上限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 18 | `field:cc91ba2e30f90e7743407c92ca21ccfb` | 缩放下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 19 | `field:1a28c100c45b333ad98f877ce57a91d6` | 缩放上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 20 | `field:ec840ee3d67ec0e481bd161ed765e794` | 最大重叠率 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 21 | `field:de30475b0cfaf4ff1a517e01dac85e42` | 排序方式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 22 | `field:cd382cb0a31d2e7f2af12c082cf9e232` | 匹配阈值使能 | 运行参数/运行参数 | 0 | 10 | 1 | 0 |  |
| 23 | `field:73e265a770cd37b0d9139e10525d629c` | 匹配阈值 | 运行参数/运行参数 | 0 | 255 | 1 | 0 |  |
| 24 | `field:4c5f323205b669486716382e94757289` | 斑点定位 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 25 | `field:2c78a7a1288061b7e178419d3fde9ee8` | 匹配范围比例 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 26 | `field:a25c036681e0277f5b684928f4cf6096` | 时间限制 | 运行参数/运行参数 | 0 | 600000 | 1 | 0 |  |
| 27 | `field:49c3ff28a27f6e67ab83bf30ff3441ab` | 轮廓输出使能 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 28 | `field:2bee019a758850f6d30a512aaaacdaef` | 标记定位方式 | 运行参数/运行参数 | 0 | 10 | 1 | 0 |  |
| 29 | `field:29373fb3353e49b8fa36373b3736f885` | X缩放下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 30 | `field:d31ccc3ed3e93b139f62e41cf39943e8` | X缩放上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 31 | `field:7cba8c1f63251099659e5880d3ac1d3b` | Y缩放下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 32 | `field:6af2c1af57cd5d877a75b4a7e2e36ceb` | Y缩放上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 33 | `field:75cbef37d385bb555d1ec86886a626fa` | 匹配个数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 34 | `field:3be2e1d65a9e51e9a88c950a9d0b585c` | 匹配个数下限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 35 | `field:0f644d173a4881cc48ac8cbcdd2b9714` | 匹配个数上限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 36 | `field:b46eb7c81568fa0e7acdd73f507786a5` | 角度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 37 | `field:a58c11443dbd814dc0eb5032de1012a6` | 角度下限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 38 | `field:de43aa84551f633751373629fe6c52d9` | 角度上限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 39 | `field:7b4969f7044ba6c05376f72acf039054` | 分数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 40 | `field:78d5c3811d947110a9d1705e999edf20` | 分数下限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 41 | `field:7cbd848aa2162acdc272207312970aa2` | 分数上限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 42 | `field:3c4bb347a671f4c1500f7d328355d2b9` | X缩放判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 43 | `field:0f362c484fd8a012534dbc82cd91a9d2` | X缩放下限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 44 | `field:929e3a849fd7d8f737fa471c6c20ff5f` | X缩放上限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 45 | `field:cb9e5778d2a113dd2b3f12d8e7db216c` | Y缩放判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 46 | `field:c7fdf18980e946844fd1595ef0667748` | Y缩放下限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 47 | `field:30c03d5e06021f6729af4f5866a9a6fb` | Y缩放上限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 48 | `field:f1b95c847da91c77fcecab7ba0272118` | 匹配点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 49 | `field:b80d38dc474730e2ab62a4f3cf759881` | 匹配点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 50 | `field:2735ae8f120c7e89eb8fdc3b1db58bfd` | 匹配点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 51 | `field:08e8c77473f6127e2a6c3ff124c4b64a` | 匹配点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 52 | `field:a8b6b0d836a75cdde43c2fc347232767` | 匹配点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 53 | `field:342fa29d2b9eec212d52b81aede61420` | 匹配点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 54 | `field:8b00b9c3c7642011a84961df66a31caf` | Box点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 55 | `field:c8aaa2f4b5affc5c2a550be96d7a301d` | Box点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 56 | `field:db5ac4b3265dc8635c111ce1e70a5923` | Box点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 57 | `field:486fe55683bbb7e489347091da4ff30b` | Box点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 58 | `field:df4e20007e9f94fcac3c7269528f8b83` | Box点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 59 | `field:8ec04ee3fe38692a866fae096488b456` | Box点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `MarkLocationType` |  | **无匹配** |
| `OutLineEnable` |  | **无匹配** |
| `ScaleYLimitHigh` |  | **无匹配** |
| `ScaleYLimitLow` |  | **无匹配** |
| `ScaleYLimitEnable` |  | **无匹配** |
| `ScaleXLimitHigh` |  | **无匹配** |
| `ScaleXLimitLow` |  | **无匹配** |
| `ScaleXLimitEnable` |  | **无匹配** |
| `ScaleYEnd` |  | **无匹配** |
| `ScaleYStart` |  | **无匹配** |
| `ScaleXEnd` |  | **无匹配** |
| `ScaleXStart` |  | **无匹配** |
| `SpotterFlag` |  | **无匹配** |
| `MatchExtentRate` |  | **无匹配** |
| `MatchThresholdHigh` |  | **无匹配** |
| `MatchThresholdFlag` |  | **无匹配** |
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
| `ScaleEnd` |  | **无匹配** |
| `ScaleStart` |  | **无匹配** |
| `BoxPointYLimitHigh` |  | **无匹配** |
| `ScoreLimitLow` |  | **无匹配** |
| `MatchPointXLimitEnable` |  | **无匹配** |
| `AngleLimitLow` |  | **无匹配** |
