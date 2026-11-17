# D2 声明对照 · BlobLabels

> 自动生成：原件 imvsblobfindlabelsmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 78 |
| 我方声明字段 | 82 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `RectangularityLimitHigh` | 1 |
| 2 | `RectangularityLimitLow` | 0 |
| 3 | `RectangularityLimitEnable` | False |
| 4 | `CircularityLimitHigh` | 1 |
| 5 | `CircularityLimitLow` | 0 |
| 6 | `CircularityLimitEnable` | False |
| 7 | `ShortAxisLimitHigh` | 999999999 |
| 8 | `ShortAxisLimitLow` | 1 |
| 9 | `ShortAxisLimitEnable` | False |
| 10 | `LongAxisLimitHigh` | 999999999 |
| 11 | `LongAxisLimitLow` | 1 |
| 12 | `LongAxisLimitEnable` | False |
| 13 | `PerimeterLimitHigh` | 999999999 |
| 14 | `PerimeterLimitLow` | 1 |
| 15 | `PerimeterLimitEnable` | False |
| 16 | `BoxWidthLimitHigh` | 99999 |
| 17 | `BoxWidthLimitLow` | 1 |
| 18 | `BoxWidthLimitEnable` | False |
| 19 | `BoxHeightLimitHigh` | 99999 |
| 20 | `BoxHeightLimitLow` | 1 |
| 21 | `BoxHeightLimitEnable` | False |
| 22 | `CenterYLimitHigh` | 99999 |
| 23 | `CenterYLimitLow` | -99999 |
| 24 | `CenterYLimitEnable` | False |
| 25 | `CenterXLimitHigh` | 99999 |
| 26 | `CenterXLimitLow` | -99999 |
| 27 | `CenterXLimitEnable` | False |
| 28 | `AngleLimitHigh` | 180 |
| 29 | `AngleLimitLow` | -180 |
| 30 | `AngleLimitEnable` | False |
| 31 | `CentroidYLimitHigh` | 99999 |
| 32 | `CentroidYLimitLow` | -99999 |
| 33 | `CentroidYLimitEnable` | False |
| 34 | `CentroidXLimitHigh` | 99999 |
| 35 | `CentroidXLimitLow` | -99999 |
| 36 | `CentroidXLimitEnable` | False |
| 37 | `BlobTotalAreaLimitHigh` | 999999999 |
| 38 | `BlobTotalAreaLimitLow` | 1 |
| 39 | `BlobTotalAreaLimitEnable` | False |
| 40 | `BlobAreaLimitHigh` | 999999999 |
| 41 | `BlobAreaLimitLow` | 1 |
| 42 | `BlobAreaLimitEnable` | False |
| 43 | `BlobNumLimitHigh` | 99999 |
| 44 | `BlobNumLimitLow` | 1 |
| 45 | `BlobNumLimitEnable` | False |
| 46 | `MaxOutPixelNum` | 0 |
| 47 | `MaxOutPixelNumEnable` | False |
| 48 | `OverlapRatio` | 0 |
| 49 | `connectivity` | 8 |
| 50 | `SortMode` | 2 |
| 51 | `SortFeature` | 1 |
| 52 | `MaxAxisRatio` | 1.0 |
| 53 | `MinAxisRatio` | 0.01 |
| 54 | `AxisRatioEnable` | False |
| 55 | `MaxCenterBias` | 9999999.99 |
| 56 | `MinCenterBias` | 0 |
| 57 | `SelectByCentraBias` | False |
| 58 | `MaxRectangularity` | 1 |
| 59 | `MinRectangularity` | 0.1 |
| 60 | `SelectByRectangularity` | False |
| 61 | `MaxCircularity` | 1 |
| 62 | `MinCircularity` | 0.1 |
| 63 | `SelectByCircularuty` | False |
| 64 | `MaxLongAxis` | 999999999 |
| 65 | `MinLongAxis` | 10 |
| 66 | `SelectByLongAxis` | False |
| 67 | `MaxShortAxis` | 999999999 |
| 68 | `MinShortAxis` | 1 |
| 69 | `SelectByShortAxis` | False |
| 70 | `MaxPerimeter` | 999999999 |
| 71 | `MinPerimeter` | 10 |
| 72 | `SelectByPerimeter` | False |
| 73 | `BolbImageEnable` | True |
| 74 | `BolbOutLineEnable` | False |
| 75 | `MaxArea` | 999999999 |
| 76 | `MinArea` | 10 |
| 77 | `SelectByArea` | False |
| 78 | `FindNum` | 100 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `field:ecaabb638aa0925fcaf8dadca906eb38` | 类别名称 | 基本参数/类别信息 |  |  | 1 | 0 |  |
| 2 | `field:ef925245f291627756004a404b3491f8` | 灰度值 | 基本参数/类别信息 | 0 | 255 | 1 | 0 |  |
| 3 | `field:8e8d43e3b30304f1b56d47b3bdc9843d` | 标签面积使能 | 基本参数/类别信息 |  |  | 1 | 0 |  |
| 4 | `field:e72e1a6a64d10f442105cfb5c1485975` | 阈值区域 | 基本参数/类别信息 |  |  | 1 | 0 |  |
| 5 | `field:d45f3fa9405e28f7008cc2cf3d1ac86f` | 查找个数 | 运行参数/运行参数 | 1 | 20000 | 1 | 0 |  |
| 6 | `field:5a8b7229dd0b3c18c6d4048928e8fc04` | 全局面积使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 7 | `field:88757cdea826e1add4ecd5c79a50b0e5` | 面积下限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 8 | `field:bc400d5c3e851b175e5da896e45b01a7` | 面积上限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 9 | `field:e9f19141ead1a02bbb4ae39a74fc8001` | 轮廓输出使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 10 | `field:0fb9e047ed22bcf34bd4e9e49cf3def8` | Blob图像输出 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 11 | `field:452c9189cc24027a559528bf1daa2128` | 周长使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 12 | `field:9cb689c7e098faa1162c3972b6407704` | 周长下限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 13 | `field:4e1e4ec30f39168e79f42c4ad97d0938` | 周长上限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 14 | `field:4ed13a49f98fa3f8e8af353d2a2454fa` | 短轴使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 15 | `field:357db28b0ebeaa46f53b045599df066a` | 短轴下限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 16 | `field:885504981272512292b6e2ca8af4e9d0` | 短轴上限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 17 | `field:5c09311a76264e9c3ee19ab3e87a2bf6` | 长轴使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 18 | `field:cfcfd64a1f0676a9249b7e7b1f57a56e` | 长轴下限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 19 | `field:56257acbcc21a971e80014a9b8b7df6e` | 长轴上限 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 20 | `field:d2085e3825b44f1cb612396041782077` | 圆形度使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 21 | `field:5196902235020a3202eb793278fc21dc` | 圆形度下限 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 22 | `field:5963ad37888a9a2f4b8f9654407f8ad0` | 圆形度上限 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 23 | `field:f4a3caee14d09c9e07d1223866f6963e` | 矩形度使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 24 | `field:94cbf7dc59112f7815dfb4838cd65ec9` | 矩形度下限 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 25 | `field:a9cabf3fc57df519fef167448370e544` | 矩形度上限 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 26 | `field:7c84030cf180dec95c5415ad683a9d93` | 质心偏移使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 27 | `field:4db758f65f1e31fc2101941800672213` | 质心偏移下限 | 运行参数/运行参数 | 0 | 9999999.99 | 0.01 | 2 |  |
| 28 | `field:63e652444da05e1bdb39ab81629e0d44` | 质心偏移上限 | 运行参数/运行参数 | 0 | 9999999.99 | 0.01 | 2 |  |
| 29 | `field:51ccddf41037882948cb6528b79f5864` | 轴比使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 30 | `field:4c00e2c7521453fee84b98dab2890a8a` | 轴比下限 | 运行参数/运行参数 | 0.01 | 1 | 0.01 | 2 |  |
| 31 | `field:3f790d06714d32e18e3b347006bc0131` | 轴比上限 | 运行参数/运行参数 | 0 | 1 | 0.01 | 2 |  |
| 32 | `field:ebe73eaa1f93d65e645001c8040b9f53` | 排序特征 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 33 | `field:c00c41174f806384cd2a2048120a3351` | 排序方式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 34 | `field:3fc8538d19a1e036ea8e10e4c91f1175` | 连通性 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 35 | `field:ad9ffab81d1fef38b95449e9fc7d56c0` | 最小重叠率 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 36 | `field:b261a523c0a142d87d91e3361fe8b26d` | 像素超界使能 | 基本参数/运行参数 |  |  | 1 | 0 |  |
| 37 | `field:0df6345c2694b5cc2952212858716159` | 最大超界像素 | 运行参数/运行参数 | 0 | 2147483647 | 1 | 0 |  |
| 38 | `field:a62dc1b1aa043b3661925e34d1d2760b` | Blob个数判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 39 | `field:41614c1c9e62dacc7723970113350d02` | Blob个数下限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 40 | `field:03c099f36d7fba02deca8369045216d7` | Blob个数上限 | 运行参数/结果判断 | 0 | 99999 | 1 | 0 |  |
| 41 | `field:ca064fdeeede4a04caa404ac1c60a579` | Blob面积判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 42 | `field:ad4e1273da5df8b59292f11393850259` | Blob面积下限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 43 | `field:bf7fbb653e20540d502da5ac16814fe9` | Blob面积上限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 44 | `field:0968c6fcf596e5af26c8692a44914886` | Blob总面积判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 45 | `field:5046ce280c5fb2f752bc0f7ba954dd05` | 总面积下限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 46 | `field:4325a5afd0b5fff548fb55bc831f6ff8` | 总面积上限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 47 | `field:2671c05c8a4398e376be805577a6a0cb` | 质心X判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 48 | `field:2441d6d1fb8a799759828826ba48eae4` | 质心X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 49 | `field:c094ab8734ab29123729101379038c9d` | 质心X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 50 | `field:424d1ef2b7e1fd77a98ed30e978f45df` | 质心Y判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 51 | `field:8be2a6b519dfb0f3a1017ff6dd85c5e4` | 质心Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 52 | `field:643331892036bab83ab1ee51fa436320` | 质心Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 53 | `field:1e5a252be50f0850945cda2851a5f313` | Box角度判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 54 | `field:5d6bc1187c19822da05f6a1f70b44950` | Box角度下限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 55 | `field:01c386724d61fa03358ef3d4614f533d` | Box角度上限 | 运行参数/结果判断 | -180 | 180 | 0.01 | 2 |  |
| 56 | `field:7a408a4aac77ded9b0009ca6198ab285` | 中心X判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 57 | `field:9511b463cb7492121d050886600bbab4` | 中心X下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 58 | `field:f29a88229c396c3538495d97b4b3dcff` | 中心X上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 59 | `field:f987b6c5bff75c5b3ddc2c66a4025f95` | 中心Y判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 60 | `field:a06cf48f13efed8cb0fe9d1b6a2588fd` | 中心Y下限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 61 | `field:1ab743b45ac0e9583eb3faa3e8cf0d09` | 中心Y上限 | 运行参数/结果判断 | -99999 | 99999 | 0.01 | 2 |  |
| 62 | `field:f65ce3fcba22a9008cddd04f73e1e0a4` | Box高度判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 63 | `field:5cbdca61db2fe1086ad436539959928d` | Box高度下限 | 运行参数/结果判断 | 0 | 99999 | 0.01 | 2 |  |
| 64 | `field:6e92447c0396dd09b129bdc244bcd9e7` | Box高度上限 | 运行参数/结果判断 | 0 | 99999 | 0.01 | 2 |  |
| 65 | `field:77bd6f32187dbdca527f517f8b16498b` | Box宽度判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 66 | `field:e907376bca4541b505b455d5553e82e1` | Box宽度下限 | 运行参数/结果判断 | 0 | 99999 | 0.01 | 2 |  |
| 67 | `field:56b5f507fc03686bc220d7f15268d1ef` | Box宽度上限 | 运行参数/结果判断 | 0 | 99999 | 0.01 | 2 |  |
| 68 | `field:a9ded4d9e59d26bd39547d121327f786` | 周长判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 69 | `field:a1edea0abd195532d5e8056b8efcd99a` | 周长判断下限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 70 | `field:bb300678607a1f47a97871cbc1cc8b9b` | 周长判断上限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 71 | `field:d1e0a505677b7ceae4bff40dee2ae960` | 长轴判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 72 | `field:f14040993bddee245e2d37c8f5d26987` | 长轴判断下限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 73 | `field:4f524dd60498366626dcd94efed7e32e` | 长轴判断上限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 74 | `field:2d528259716f01079bfcdf9e7490837d` | 短轴判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 75 | `field:494e49c4f2953f0b559ca80c17aac636` | 短轴判断下限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 76 | `field:0e1cc42a84cb6b22de5b20ced1ce678d` | 短轴判断上限 | 运行参数/结果判断 | 0 | 999999999 | 0.01 | 2 |  |
| 77 | `field:47abd4484b6febe68ac4a2ab82ddc0b4` | 圆形度判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 78 | `field:bdc3589974fc007f679babed845e154e` | 圆形度判断下限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 79 | `field:42e57dddca236975691b3afe44c5c90d` | 圆形度判断上限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 80 | `field:541b5e31786f56d4196e8c321a017867` | 矩形度判断 | 基本参数/结果判断 |  |  | 1 | 0 |  |
| 81 | `field:bc67a15497cf038f9b5b47abc67743c9` | 矩形度判断下限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |
| 82 | `field:27a7653e9caa16a946ad7560189663b1` | 矩形度判断上限 | 运行参数/结果判断 | 0 | 1 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `RectangularityLimitHigh` |  | **无匹配** |
| `RectangularityLimitLow` |  | **无匹配** |
| `RectangularityLimitEnable` |  | **无匹配** |
| `CircularityLimitHigh` |  | **无匹配** |
| `CircularityLimitLow` |  | **无匹配** |
| `CircularityLimitEnable` |  | **无匹配** |
| `ShortAxisLimitHigh` |  | **无匹配** |
| `ShortAxisLimitLow` |  | **无匹配** |
| `ShortAxisLimitEnable` |  | **无匹配** |
| `LongAxisLimitHigh` |  | **无匹配** |
| `LongAxisLimitLow` |  | **无匹配** |
| `LongAxisLimitEnable` |  | **无匹配** |
| `PerimeterLimitHigh` |  | **无匹配** |
| `PerimeterLimitLow` |  | **无匹配** |
| `PerimeterLimitEnable` |  | **无匹配** |
| `BoxWidthLimitHigh` |  | **无匹配** |
| `BoxWidthLimitLow` |  | **无匹配** |
| `BoxWidthLimitEnable` |  | **无匹配** |
| `BoxHeightLimitHigh` |  | **无匹配** |
| `BoxHeightLimitLow` |  | **无匹配** |
| `BoxHeightLimitEnable` |  | **无匹配** |
| `CenterYLimitHigh` |  | **无匹配** |
| `CenterYLimitLow` |  | **无匹配** |
| `CenterYLimitEnable` |  | **无匹配** |
| `CenterXLimitHigh` |  | **无匹配** |
| `CenterXLimitLow` |  | **无匹配** |
| `CenterXLimitEnable` |  | **无匹配** |
| `AngleLimitHigh` |  | **无匹配** |
| `AngleLimitLow` |  | **无匹配** |
| `AngleLimitEnable` |  | **无匹配** |
| `CentroidYLimitHigh` |  | **无匹配** |
| `CentroidYLimitLow` |  | **无匹配** |
| `CentroidYLimitEnable` |  | **无匹配** |
| `CentroidXLimitHigh` |  | **无匹配** |
| `CentroidXLimitLow` |  | **无匹配** |
| `CentroidXLimitEnable` |  | **无匹配** |
| `BlobTotalAreaLimitHigh` |  | **无匹配** |
| `BlobTotalAreaLimitLow` |  | **无匹配** |
| `BlobTotalAreaLimitEnable` |  | **无匹配** |
| `BlobAreaLimitHigh` |  | **无匹配** |
| `BlobAreaLimitLow` |  | **无匹配** |
| `BlobAreaLimitEnable` |  | **无匹配** |
| `BlobNumLimitHigh` |  | **无匹配** |
| `BlobNumLimitLow` |  | **无匹配** |
| `BlobNumLimitEnable` |  | **无匹配** |
| `MaxOutPixelNum` |  | **无匹配** |
| `MaxOutPixelNumEnable` |  | **无匹配** |
| `OverlapRatio` |  | **无匹配** |
| `connectivity` |  | **无匹配** |
| `SortMode` |  | **无匹配** |
| `SortFeature` |  | **无匹配** |
| `MaxAxisRatio` |  | **无匹配** |
| `MinAxisRatio` |  | **无匹配** |
| `AxisRatioEnable` |  | **无匹配** |
| `MaxCenterBias` |  | **无匹配** |
| `MinCenterBias` |  | **无匹配** |
| `SelectByCentraBias` |  | **无匹配** |
| `MaxRectangularity` |  | **无匹配** |
| `MinRectangularity` |  | **无匹配** |
| `SelectByRectangularity` |  | **无匹配** |
| `MaxCircularity` |  | **无匹配** |
| `MinCircularity` |  | **无匹配** |
| `SelectByCircularuty` |  | **无匹配** |
| `MaxLongAxis` |  | **无匹配** |
| `MinLongAxis` |  | **无匹配** |
| `SelectByLongAxis` |  | **无匹配** |
| `MaxShortAxis` |  | **无匹配** |
| `MinShortAxis` |  | **无匹配** |
| `SelectByShortAxis` |  | **无匹配** |
| `MaxPerimeter` |  | **无匹配** |
| `MinPerimeter` |  | **无匹配** |
| `SelectByPerimeter` |  | **无匹配** |
| `BolbImageEnable` |  | **无匹配** |
| `BolbOutLineEnable` |  | **无匹配** |
| `MaxArea` |  | **无匹配** |
| `MinArea` |  | **无匹配** |
| `SelectByArea` |  | **无匹配** |
| `FindNum` |  | **无匹配** |
