# D2 声明对照 · ContourMatch

> 自动生成：原件 imvscontourmatchmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 56 |
| 我方声明字段 | 65 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `SkewXEnd` | 0 |
| 2 | `SkewXStart` | 0 |
| 3 | `OKWhenNumIsZero` | False |
| 4 | `RangeScaleType` | 0 |
| 5 | `UseMatchAllMode` | False |
| 6 | `OutLinePointEnable` | False |
| 7 | `OutLineEnable` | True |
| 8 | `ScaleYLimitHigh` | 10 |
| 9 | `ScaleYLimitLow` | 0.1 |
| 10 | `ScaleYLimitEnable` | False |
| 11 | `ScaleXLimitHigh` | 10 |
| 12 | `ScaleXLimitLow` | 0.1 |
| 13 | `ScaleXLimitEnable` | False |
| 14 | `ScaleLimitHigh` | 10 |
| 15 | `ScaleLimitLow` | 0.1 |
| 16 | `ScaleLimitEnable` | False |
| 17 | `ScaleYEnd` | 1 |
| 18 | `ScaleYStart` | 1 |
| 19 | `ScaleXEnd` | 1 |
| 20 | `ScaleXStart` | 1 |
| 21 | `SpotterFlag` | False |
| 22 | `MatchExtentRate` | 0 |
| 23 | `MatchThresholdHigh` | 40 |
| 24 | `MatchThresholdFlag` | 0 |
| 25 | `BoxPointXLimitEnable` | False |
| 26 | `NumLimitLow` | 0 |
| 27 | `AngleLimitEnable` | False |
| 28 | `BoxPointXLimitLow` | -99999 |
| 29 | `AngleLimitHigh` | 180 |
| 30 | `SortType` | 2 |
| 31 | `ScoreLimitEnable` | False |
| 32 | `MatchPointXLimitLow` | -99999 |
| 33 | `NumLimitEnable` | False |
| 34 | `ScoreLimitHigh` | 1 |
| 35 | `MatchPointYLimitEnable` | False |
| 36 | `TimeOut` | 0 |
| 37 | `AngleStart` | -45 |
| 38 | `Polarity` | 1 |
| 39 | `MatchPointYLimitLow` | -99999 |
| 40 | `NumLimitHigh` | 99999 |
| 41 | `MatchPointYLimitHigh` | 99999 |
| 42 | `AngleEnd` | 45 |
| 43 | `MinScore` | 0.5 |
| 44 | `MultiModelMaxOverlap` | 50 |
| 45 | `MaxOverlap` | 50 |
| 46 | `MatchPointXLimitHigh` | 99999 |
| 47 | `BoxPointYLimitEnable` | False |
| 48 | `MaxMatchNum` | 1 |
| 49 | `BoxPointXLimitHigh` | 99999 |
| 50 | `BoxPointYLimitLow` | -99999 |
| 51 | `ScaleEnd` | 1 |
| 52 | `ScaleStart` | 1 |
| 53 | `BoxPointYLimitHigh` | 99999 |
| 54 | `ScoreLimitLow` | 0 |
| 55 | `MatchPointXLimitEnable` | False |
| 56 | `AngleLimitLow` | -180 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `field:9cbf4b8b9cf6a1d80d3641e45cc4c7ad` | 模板数据 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 2 | `field:92fe0da55447c59c3ed9299921d04358` | 金字塔比例 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 3 | `field:01b74d196fe0cb9a0160bc91ddfe552f` | 压缩比 | 模板参数/模板参数 | 1 | 10 | 1 | 0 |  |
| 4 | `field:45b43a086ea8a41a3be503425de57fa0` | 特征尺度 | 模板参数/模板参数 | 1 | 10 | 1 | 0 |  |
| 5 | `field:4657187527c7e9faac7947fcf293a580` | 阈值模式 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 6 | `field:fcb8e871ada861fd574e47cbd5e37af6` | 边缘阈值 | 模板参数/模板参数 | 1 | 255 | 1 | 0 |  |
| 7 | `field:b7678156c1fb7e0a91084dc948122bc4` | 链式 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 8 | `field:c00626d6ee7c6ad7eb4cbabbf01f3dcd` | 最小链长 | 模板参数/模板参数 | 2 | 1000 | 1 | 0 |  |
| 9 | `field:6bf5a4ee570e68fc6b6f1ef2d038169d` | 掩膜模型 | 模板参数/模板参数 |  |  | 1 | 0 |  |
| 10 | `field:93e9acc261d20b94be91cced1226be2d` | 最小分数 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 11 | `field:c638266d005b2c3920019226c65c02fb` | 最大匹配个数 | 运行参数/运行参数 | 1 | 50000 | 1 | 0 |  |
| 12 | `field:df2b1ac6b148be417cb02ae72dd0f58a` | 极性 | 运行参数/运行参数 | 0 | 1 | 1 | 0 |  |
| 13 | `field:e389953cdeac0532c886e133a745b361` | 角度下限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 14 | `field:402545e8e027d9872af7fec53e53be84` | 角度上限 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 15 | `field:1fa5f7c8f1b18738a805146435c478a1` | 尺度下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 16 | `field:b805e08e74e0aa9a96ab0d575323a535` | 尺度上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 17 | `field:40d0f12efc7dc4766bec77b814108552` | X尺度下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 18 | `field:26f34088533b753a639ae3eaf9fd7edb` | X尺度上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 19 | `field:05474e5c2009de043a1efcad0179c5af` | Y尺度下限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 20 | `field:93f96f6c6e0171426d4602e315e553ac` | Y尺度上限 | 运行参数/运行参数 | 0.1 | 10 | 0.01 | 2 |  |
| 21 | `field:33b9c65a92da80f9d510104f9542ae4f` | 最大重叠率 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 22 | `field:2e0e3654b04b3ddabb4b7024a03c5240` | 多模板重叠率 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 23 | `field:f981fdd591201df33aa7f3a19f95d4ae` | 排序方式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 24 | `field:59c42345913f6306dfacd9fd804a0fa6` | 阈值类型 | 运行参数/运行参数 | 0 | 2 | 1 | 0 |  |
| 25 | `field:79d5a2964a0b8bd4785f973b3a4475c8` | 匹配边缘阈值 | 运行参数/运行参数 | 1 | 255 | 1 | 0 |  |
| 26 | `field:992759401eb81d125de629a6061475b6` | 考虑噪点 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 27 | `field:45b329d44053164280e57bbe467cec2a` | 延拓阈值 | 运行参数/运行参数 | 0 | 90 | 1 | 0 |  |
| 28 | `field:ca37d897c74a1c6fef312c8efb9e4b1a` | 超时控制 | 运行参数/运行参数 | 0 | 10000 | 1 | 0 |  |
| 29 | `field:0a3393fc5528a696cd7fcfa639e258fa` | 轮廓使能 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 30 | `field:201a11b34da6a5407996231350c7f884` | 轮廓点使能 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 31 | `field:c54bbfca2571112d04d1e16f94aee8a9` | 全部搜索模式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 32 | `field:d9cabd55e169fd7c83f9b3cf0d010cf7` | 尺度范围类型 | 运行参数/运行参数 | 0 | 10 | 1 | 0 |  |
| 33 | `field:b630bb3bafc65a14213bacb2a4ba2b6a` | 个数为0时判定OK | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 34 | `field:ce51ee63ebb9c83b0bce48c241a85243` | 起始横切角度 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 35 | `field:1c0d2beadba72929a0eac7ea1c7eec92` | 终止横切角度 | 运行参数/运行参数 | -180 | 180 | 1 | 0 |  |
| 36 | `field:c3e553b10d9ae169233a7e3c214b1cfa` | 匹配个数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 37 | `field:ecb381fc723978bc06740b83c586e403` | 匹配个数下限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 38 | `field:1c78924bce1a4d10ef6319f3b5fb4b64` | 匹配个数上限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 39 | `field:d97bde3b33b5f1bc58af4affa1eab116` | 角度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 40 | `field:55134b3a9f562bb6804bf46b2143254f` | 角度下限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 41 | `field:f8197fa4cadcf3e7192af66b48f2ebd0` | 角度上限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 42 | `field:dd7d9f369e991c40b1a9c76138d923b7` | 分数判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 43 | `field:a0d00970f9be778e4abe913456f6ea93` | 分数下限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 44 | `field:82b59d71a55aa47175e4ab919066e4a0` | 分数上限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 45 | `field:9db1bc6cbc098e52bda5d6e839a78b0b` | 尺度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 46 | `field:b9fd4820d33de215773ae1f5c54f3092` | 尺度下限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 47 | `field:837cf0132b95fd929b207e17342aa585` | 尺度上限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 48 | `field:150c4fb70d39560d5bd4d0ff6e55ddfb` | X尺度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 49 | `field:04937438493cfb9b020f0375b6cfe128` | X尺度下限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 50 | `field:ab56ae8278c2c064d5549c68d818c4ce` | X尺度上限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 51 | `field:2dc04d8cf2e701a32b3fc506a3f5121b` | Y尺度判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 52 | `field:25ac031126c5c5319f664b4391d50426` | Y尺度下限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 53 | `field:a11ece10371f2e3584fe38836248c171` | Y尺度上限 | 运行参数/结果判断 | 0.1 | 10 | 0.01 | 2 |  |
| 54 | `field:fac4c2230680f7fd5f955bb8c0ad5973` | 匹配点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 55 | `field:045b5bd2ace4ab761e36963b178e347f` | 匹配点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 56 | `field:e60466e2f88ce78d9af2cdb15abe4f1a` | 匹配点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 57 | `field:94d502e3a0361197695f619734d38e02` | 匹配点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 58 | `field:c2ebeaab5a0f3de5c9e8ab59b91aed3a` | 匹配点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 59 | `field:a51e420c3b945dd999536f1069e61048` | 匹配点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 60 | `field:746b66069d4184caa09a5a4c4473ccf4` | Box点X判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 61 | `field:940ae556a23f8b0541263345ee2e10ed` | Box点X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 62 | `field:74d3b7eba869c97ba3b936b88a84a1e6` | Box点X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 63 | `field:4fd20b1d2ed0a7a9ab89010919e6a6a7` | Box点Y判断 | 运行参数/结果判断 |  |  | 1 | 0 |  |
| 64 | `field:7155cd86e37b0b8b07e719aad54018a1` | Box点Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 65 | `field:edc0bae7a0802c4445c317e8eda7b610` | Box点Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `SkewXEnd` |  | **无匹配** |
| `SkewXStart` |  | **无匹配** |
| `OKWhenNumIsZero` |  | **无匹配** |
| `RangeScaleType` |  | **无匹配** |
| `UseMatchAllMode` |  | **无匹配** |
| `OutLinePointEnable` |  | **无匹配** |
| `OutLineEnable` |  | **无匹配** |
| `ScaleYLimitHigh` |  | **无匹配** |
| `ScaleYLimitLow` |  | **无匹配** |
| `ScaleYLimitEnable` |  | **无匹配** |
| `ScaleXLimitHigh` |  | **无匹配** |
| `ScaleXLimitLow` |  | **无匹配** |
| `ScaleXLimitEnable` |  | **无匹配** |
| `ScaleLimitHigh` |  | **无匹配** |
| `ScaleLimitLow` |  | **无匹配** |
| `ScaleLimitEnable` |  | **无匹配** |
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
| `MultiModelMaxOverlap` |  | **无匹配** |
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
