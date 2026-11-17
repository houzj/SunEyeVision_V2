# D2 声明对照 · BlobAnalysis

> 自动生成：原件 imvsblobfindmodu / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 105 |
| 我方声明字段 | 104 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `MaxAreaInnerRectNum` | 0 |
| 2 | `SelectByHistInfo` | False |
| 3 | `MaxRectHeight` | 200 |
| 4 | `MinRectHeight` | 100 |
| 5 | `SelectByRectHeight` | False |
| 6 | `MaxRectWidth` | 200 |
| 7 | `MinRectWidth` | 100 |
| 8 | `SelectByRectWidth` | False |
| 9 | `MaxBoxAngle` | 9000 |
| 10 | `MinBoxAngle` | -9000 |
| 11 | `SelectByBoxAngle` | False |
| 12 | `OKWhenNumIsZero` | False |
| 13 | `MaxOutPixelNum` | 0 |
| 14 | `MaxOutPixelNumEnable` | False |
| 15 | `BinaryImageEnable` | True |
| 16 | `BolbImageEnable` | True |
| 17 | `BlobContourType` | 0 |
| 18 | `SortMode` | 2 |
| 19 | `SortFeature` | 1 |
| 20 | `MaxAxisRatio` | 1.0 |
| 21 | `MinAxisRatio` | 0.01 |
| 22 | `AxisRatioEnable` | False |
| 23 | `HoleMinArea` | 0 |
| 24 | `RectangularityLimitLow` | 0 |
| 25 | `PerimeterLimitLow` | 1 |
| 26 | `LongAxisLimitLow` | 1 |
| 27 | `PerimeterLimitHigh` | 999999999 |
| 28 | `RectangularityLimitHigh` | 1 |
| 29 | `CenterYLimitHigh` | 99999 |
| 30 | `BlobNumLimitEnable` | False |
| 31 | `BlobTotalAreaLimitHigh` | 999999999 |
| 32 | `BlobAreaLimitHigh` | 999999999 |
| 33 | `AngleLimitEnable` | False |
| 34 | `BoxHeightLimitLow` | 1 |
| 35 | `BoxHeightLimitEnable` | False |
| 36 | `BoxHeightLimitHigh` | 99999 |
| 37 | `CircularityLimitLow` | 0 |
| 38 | `LongAxisLimitHigh` | 999999999 |
| 39 | `BoxWidthLimitEnable` | False |
| 40 | `CenterYLimitEnable` | False |
| 41 | `BoxWidthLimitHigh` | 99999 |
| 42 | `CenterXLimitHigh` | 99999 |
| 43 | `AngleLimitHigh` | 180 |
| 44 | `ScoreLimitLow` | 0 |
| 45 | `RectangularityLimitEnable` | False |
| 46 | `BoxWidthLimitLow` | 1 |
| 47 | `LongAxisLimitEnable` | False |
| 48 | `ShortAxisLimitHigh` | 999999999 |
| 49 | `CentroidYLimitHigh` | 99999 |
| 50 | `CenterYLimitLow` | -99999 |
| 51 | `PerimeterLimitEnable` | False |
| 52 | `CentroidXLimitHigh` | 99999 |
| 53 | `ScoreLimitHigh` | 1 |
| 54 | `BlobTotalAreaLimitEnable` | False |
| 55 | `BlobAreaLimitEnable` | False |
| 56 | `BlobNumLimitLow` | 1 |
| 57 | `CentroidXLimitEnable` | False |
| 58 | `ShortAxisLimitLow` | 1 |
| 59 | `ScoreLimitEnable` |  |
| 60 | `BlobNumLimitHigh` | 99999 |
| 61 | `CenterXLimitLow` | -99999 |
| 62 | `CentroidYLimitEnable` | False |
| 63 | `ShortAxisLimitEnable` | False |
| 64 | `AngleLimitLow` | -180 |
| 65 | `CenterXLimitEnable` | False |
| 66 | `CircularityLimitHigh` | 1 |
| 67 | `CentroidYLimitLow` | -99999 |
| 68 | `CircularityLimitEnable` | False |
| 69 | `CentroidXLimitLow` | -99999 |
| 70 | `BlobTotalAreaLimitLow` | 1 |
| 71 | `BlobAreaLimitLow` | 1 |
| 72 | `SelectByCentraBias` | False |
| 73 | `SelectByPerimeter` | False |
| 74 | `SelectByArea` | True |
| 75 | `SelectByShortAxis` | False |
| 76 | `SelectByLongAxis` | False |
| 77 | `SelectByRectangularity` | False |
| 78 | `SelectByCircularuty` | False |
| 79 | `connectivity` | 8 |
| 80 | `MaxCenterBias` | 9999999.99 |
| 81 | `MinCenterBias` | 0 |
| 82 | `MaxRectangularity` | 1 |
| 83 | `MinRectangularity` | 0.1 |
| 84 | `MaxCircularity` | 1 |
| 85 | `MinCircularity` | 0.1 |
| 86 | `MaxLongAxis` | 999999999 |
| 87 | `MinLongAxis` | 10 |
| 88 | `MaxShortAxis` | 999999999 |
| 89 | `MinShortAxis` | 1 |
| 90 | `MaxPerimeter` | 999999999 |
| 91 | `MinPerimeter` | 10 |
| 92 | `MaxArea` | 999999999 |
| 93 | `MinArea` | 10 |
| 94 | `SoftHighRatio` | 60 |
| 95 | `SoftLowRatio` | 40 |
| 96 | `SoftRightRatio` | 5 |
| 97 | `SoftLeftRatio` | 5 |
| 98 | `Softness` | 254 |
| 99 | `FindNum` | 100 |
| 100 | `HightSoftThreshold` | 150 |
| 101 | `LowSoftThreshold` | 100 |
| 102 | `HightThreshold` | 150 |
| 103 | `LowThreshold` | 100 |
| 104 | `Polarity` | 0 |
| 105 | `ThresholdType` | 2 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `field:c95f3a26edf55097863a7ec47d17cce5` | 位置修正 | 基本参数/ROI区域 |  |  | 1 | 0 |  |
| 2 | `field:8ca20000000040008000000000000001` | 阈值方式 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 3 | `field:88b45ab61b965761b901670fd587903f` | 极性 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 4 | `field:43bda03261df55b88f963bd18f2ba4d9` | 低阈值 | 运行参数/运行参数 | 0 | 255 | 1 | 0 |  |
| 5 | `field:8ca20000000040008000000000000002` | 高阈值 | 运行参数/运行参数 | 0 | 255 | 1 | 0 |  |
| 6 | `field:8ca20000000040008000000000000010` | 软阈值低阈值（固定） | 运行参数/运行参数 | 1 | 255 | 1 | 0 |  |
| 7 | `field:8ca20000000040008000000000000011` | 软阈值高阈值（固定） | 运行参数/运行参数 | 0 | 255 | 1 | 0 |  |
| 8 | `field:8ca20000000040008000000000000012` | 软阈值柔和度 | 运行参数/运行参数 | 0 | 254 | 1 | 0 |  |
| 9 | `field:8ca20000000040008000000000000013` | 低尾部比例 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 10 | `field:8ca20000000040008000000000000014` | 高尾部比例 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 11 | `field:8ca20000000040008000000000000015` | 低阈值比例 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 12 | `field:8ca20000000040008000000000000016` | 高阈值比例 | 运行参数/运行参数 | 0 | 100 | 1 | 0 |  |
| 13 | `field:e748553b7cb45d1a8003c2b345b1d98f` | 查找个数 | 运行参数/运行参数 | 1 | 20000 | 1 | 0 |  |
| 14 | `field:175c8d2f58d24bf7b1f481e226c9406c` | 填充面积阈值 | 运行参数/运行参数 | 0 | 999999999 | 1 | 0 |  |
| 15 | `field:e9845ab7a46e5ee9b42310bef5a11c19` | 面积使能 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 16 | `field:fcdb01b095c25c6ea22384f993450aac` | 最小面积 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 17 | `field:cdce3e942b555fa58489e08eeb39236f` | 最大面积 | 运行参数/运行参数 | 1 | 999999999 | 1 | 0 |  |
| 18 | `field:8592aa1bb7394e81b462a917de7d3d76` | 输出轮廓类型 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 19 | `field:2fff619e51a359a6a9af286c5e8fef65` | Blob图像输出 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 20 | `field:73f2c01e7b385e0ab2d35f739a88e718` | 二值化图输出 | 运行参数/运行参数 |  |  | 1 | 0 |  |
| 21 | `field:e3d31765426f499e9dbcaa477d973bb1` | 直方图信息输出 | 运行参数/高级 |  |  | 1 | 0 |  |
| 22 | `field:9cdcd21eb8d44b51bbbc2ff4391050e6` | 角度使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 23 | `field:c4a179715ea54256becbad63a76bc03e` | 最小角度 | 运行参数/高级 | -9000 | 9000 | 1 | 0 |  |
| 24 | `field:52ed17d6f572459d9030ed63202d7aab` | 最大角度 | 运行参数/高级 | -9000 | 9000 | 1 | 0 |  |
| 25 | `field:7655ec4f182c49ae91bbcf08337d2fe0` | RECT宽使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 26 | `field:a0b425f6af404364a2122d9593f7c11a` | 最小RECT宽 | 运行参数/高级 | 1 | 1000000 | 1 | 0 |  |
| 27 | `field:a3e9bc5fea1745b19e2bef4f6a956b66` | 最大RECT宽 | 运行参数/高级 | 1 | 1000000 | 1 | 0 |  |
| 28 | `field:a9b12b0fd4ca45d886bd1cdcb6c2ee0d` | RECT高使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 29 | `field:a205cc3f6f6d41439fb3e0ea3689cb28` | 最小RECT高 | 运行参数/高级 | 1 | 1000000 | 1 | 0 |  |
| 30 | `field:621f28b9a0d44fc7ba184f70c34311c2` | 最大RECT高 | 运行参数/高级 | 1 | 1000000 | 1 | 0 |  |
| 31 | `field:3c8c55f5d820404cb49c9d8f6d9a395f` | 周长使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 32 | `field:d0c37c3a1f5b42768a576e0e0d74d702` | 最小周长 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 33 | `field:36dd349aba3047beb9be85f4337743ba` | 最大周长 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 34 | `field:7075d2a2a356464b91a20a5a0337963f` | 短轴使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 35 | `field:d33029aed8ae4519bc0e6391f2c31ffc` | 最小短轴 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 36 | `field:7fddab47c0ca446e8a1e397f295d1c1d` | 最大短轴 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 37 | `field:fa92c2bdb6684bbca34b1ea1d1beef30` | 长轴使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 38 | `field:02bccae0e36840f4b14f5a6ab17163c8` | 最小长轴 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 39 | `field:a680c5dae07747cfa48324e0254c9d52` | 最大长轴 | 运行参数/高级 | 1 | 999999999 | 1 | 0 |  |
| 40 | `field:7d59f57f71c65911b88bae5f90d994d9` | 圆形度使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 41 | `field:da4d70261e23551e9a5bd832b2a686d1` | 最小圆形度 | 运行参数/高级 | 0 | 1 | 0.01 | 2 |  |
| 42 | `field:1a8be75176b350f09f3cd64506c136da` | 最大圆形度 | 运行参数/高级 | 0 | 1 | 0.01 | 2 |  |
| 43 | `field:34886e8b5b765d49b8662bb061b02bf7` | 矩形度使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 44 | `field:13d8c5e0c7445f1087497a21f4103a5e` | 最小矩形度 | 运行参数/高级 | 0 | 1 | 0.01 | 2 |  |
| 45 | `field:c38989cfdf9e51799d27e0eeee34e726` | 最大矩形度 | 运行参数/高级 | 0 | 1 | 0.01 | 2 |  |
| 46 | `field:17f4a3db699945919a9b89694bd18186` | 质心偏移使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 47 | `field:2a2f4521a84447b5b9e3a77e71d34aa5` | 最小质心偏移 | 运行参数/高级 | 0 | 9999999.99 | 0.01 | 2 |  |
| 48 | `field:ba75154dc5364e8ca6119b17da7bbc3d` | 最大质心偏移 | 运行参数/高级 | 0 | 9999999.99 | 0.01 | 2 |  |
| 49 | `field:db1b2633f75043e283b42874cbd5f90c` | 轴比范围 | 运行参数/高级 |  |  | 1 | 0 |  |
| 50 | `field:555371aba49a4efe88f6b5285a79024c` | 最小轴比 | 运行参数/高级 | 0.01 | 1 | 0.01 | 2 |  |
| 51 | `field:5b70efd30cb34cf591824e8a731d49eb` | 最大轴比 | 运行参数/高级 | 0 | 1 | 0.01 | 2 |  |
| 52 | `field:8ca20000000040008000000000000004` | 排序特征 | 运行参数/高级 |  |  | 1 | 0 |  |
| 53 | `field:cb01714e6b0b503a812fe7a99f1c815e` | 排序方式 | 运行参数/高级 |  |  | 1 | 0 |  |
| 54 | `field:8ca20000000040008000000000000003` | 连通性 | 运行参数/高级 |  |  | 1 | 0 |  |
| 55 | `field:81963cf70859440880f2d9478fd1e246` | 最大内接矩形输出数量 | 运行参数/高级 | 0 | 10 | 1 | 0 |  |
| 56 | `field:1f1624765afb42d280d8090aa4c0a7db` | 最小重叠率 | 运行参数/高级 | 0 | 100 | 1 | 0 |  |
| 57 | `field:54f22aab059546c396046927f0d9f7ba` | 像素超界使能 | 运行参数/高级 |  |  | 1 | 0 |  |
| 58 | `field:cd013f061e38431ebdb31252f4001a92` | 最大超界像素 | 运行参数/高级 | 0 | 9000000 | 1 | 0 |  |
| 59 | `field:2e46ec3997424050a6aecbdfd92b4f28` | Blob个数为0 | 结果显示/结果判定 |  |  | 1 | 0 |  |
| 60 | `field:99a8e45eec7f406b93ab7226c73744ce` | Blob个数判断 | 结果显示/结果判定 |  |  | 1 | 0 |  |
| 61 | `field:4dd2475077314fc68baf68900d8d2b93` | Blob个数下限 | 结果显示/结果判定 | 1 | 99999 | 1 | 0 |  |
| 62 | `field:d723e4dc65354ac098db0ec358d3d942` | Blob个数上限 | 结果显示/结果判定 | 0 | 99999 | 1 | 0 |  |
| 63 | `field:3b19267848f445a597c28dad0cca04d7` | Blob面积判断 | 结果显示/结果判定 |  |  | 1 | 0 |  |
| 64 | `field:3edf84dee19843a0abf2134a1795a899` | Blob面积下限 | 结果显示/结果判定 | 1 | 999999999 | 1 | 2 |  |
| 65 | `field:5540dd3e9db44b7e8cc309b686a1b299` | Blob面积上限 | 结果显示/结果判定 | 1 | 999999999 | 1 | 2 |  |
| 66 | `field:c61ef5034b7f47d5a1cf44e872ed257f` | Blob总面积 | 结果显示/结果判定 |  |  | 1 | 0 |  |
| 67 | `field:839561414124417c9277e408e9ae0576` | Blob总面积下限 | 结果显示/结果判定 | 1 | 999999999 | 1 | 2 |  |
| 68 | `field:ed6d62bc4fe44d8bb4de8b60ed0b6bec` | Blob总面积上限 | 结果显示/结果判定 | 1 | 999999999 | 1 | 2 |  |
| 69 | `field:562e91d03cfd497b92ee85bddd2bac80` | 质心X判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 70 | `field:3fa5fa5bc0724534928321f48dc19a0e` | 质心X下限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 71 | `field:d171117d2077414c945d306001c8704a` | 质心X上限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 72 | `field:71abf3ae5d1a4aabb8d7d40622f00be7` | 质心Y判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 73 | `field:e936597353cf48578de8f44407934f94` | 质心Y下限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 74 | `field:efadf8c729384dea920ef4dbaa8ecbbd` | 质心Y上限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 75 | `field:138012d80bd94b45be6024b76c5adb7e` | Box角度判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 76 | `field:1582e0e16b0b42f09efdfccdcf8a591c` | 角度下限 | 结果显示/高级判定 | -180 | 180 | 0.1 | 2 |  |
| 77 | `field:bb3cb8a2f07b417984e972146a14ef68` | 角度上限 | 结果显示/高级判定 | -180 | 180 | 0.1 | 2 |  |
| 78 | `field:a1bb04ee152f4dbb97d9045a13bcd7f7` | 中心X判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 79 | `field:8a85108b2ad645d7a5997e94bb409be0` | 中心X下限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 80 | `field:32a7a025620b4f6d9b3f7264fbf44a89` | 中心X上限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 81 | `field:c8b5368780e44acf9faedb77de7ec30c` | 中心Y判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 82 | `field:c37de96e957a4df0a392f0a47702eb57` | 中心Y下限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 83 | `field:633179d6c94e48efbeb50854631ce417` | 中心Y上限 | 结果显示/高级判定 | -99999 | 99999 | 1 | 2 |  |
| 84 | `field:1967fb42011f4b39b53a2b785bae2958` | Box高度判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 85 | `field:dd208ea5d2854009b915cf9b488f6a52` | Box高度下限 | 结果显示/高级判定 | 1 | 99999 | 1 | 2 |  |
| 86 | `field:39559851b052434f985292b0d6821602` | Box高度上限 | 结果显示/高级判定 | 1 | 99999 | 1 | 2 |  |
| 87 | `field:8b5ec0307f0b4357bdff0b320e54b96c` | Box宽度判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 88 | `field:06f72e6e528840b0acd11d73436eae33` | Box宽度下限 | 结果显示/高级判定 | 1 | 99999 | 1 | 2 |  |
| 89 | `field:88fb598776d54c6580d4e7cdb8adeed7` | Box宽度上限 | 结果显示/高级判定 | 1 | 99999 | 1 | 2 |  |
| 90 | `field:3d5feb5fc109413dac1e3f58b06fa818` | 周长判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 91 | `field:dfd14c58860940b3922e1e7669692baa` | 周长下限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 92 | `field:d8f346a67d3949e58a2aef1b4d0836ad` | 周长上限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 93 | `field:bdd11b978d7b4f78a49d3944dc2f7915` | 长轴判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 94 | `field:5d5ed8c3a3e44c8bb2e431d12e176bbf` | 长轴下限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 95 | `field:a98c82ac02eb4300a16490ba868a50d9` | 长轴上限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 96 | `field:05a9581e276842b0837f5b3a2865aa74` | 短轴判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 97 | `field:cda28083f9a64007a6a021644fae2919` | 短轴下限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 98 | `field:8f496b319e504562832229c68eb6a1f2` | 短轴上限 | 结果显示/高级判定 | 1 | 999999999 | 1 | 2 |  |
| 99 | `field:935374cc47f5464aa28379ffd52eaca3` | 圆形度判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 100 | `field:2f0acab6c8874b6e9d63f87917a958f4` | 圆形度下限 | 结果显示/高级判定 | 0 | 1 | 0.01 | 2 |  |
| 101 | `field:37fb470a069c413fb8869d964475036f` | 圆形度上限 | 结果显示/高级判定 | 0 | 1 | 0.01 | 2 |  |
| 102 | `field:b9fd93fe1caf45729e9df109282bf9e0` | 矩形度判断 | 结果显示/高级判定 |  |  | 1 | 0 |  |
| 103 | `field:f5c35982e72f4f6b829f94fa674b2d5d` | 矩形度下限 | 结果显示/高级判定 | 0 | 1 | 0.01 | 2 |  |
| 104 | `field:4212c7c338f049028cc3d24f74548639` | 矩形度上限 | 结果显示/高级判定 | 0 | 1 | 0.01 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `MaxAreaInnerRectNum` |  | **无匹配** |
| `SelectByHistInfo` |  | **无匹配** |
| `MaxRectHeight` |  | **无匹配** |
| `MinRectHeight` |  | **无匹配** |
| `SelectByRectHeight` |  | **无匹配** |
| `MaxRectWidth` |  | **无匹配** |
| `MinRectWidth` |  | **无匹配** |
| `SelectByRectWidth` |  | **无匹配** |
| `MaxBoxAngle` |  | **无匹配** |
| `MinBoxAngle` |  | **无匹配** |
| `SelectByBoxAngle` |  | **无匹配** |
| `OKWhenNumIsZero` |  | **无匹配** |
| `MaxOutPixelNum` |  | **无匹配** |
| `MaxOutPixelNumEnable` |  | **无匹配** |
| `BinaryImageEnable` |  | **无匹配** |
| `BolbImageEnable` |  | **无匹配** |
| `BlobContourType` |  | **无匹配** |
| `SortMode` |  | **无匹配** |
| `SortFeature` |  | **无匹配** |
| `MaxAxisRatio` |  | **无匹配** |
| `MinAxisRatio` |  | **无匹配** |
| `AxisRatioEnable` |  | **无匹配** |
| `HoleMinArea` |  | **无匹配** |
| `RectangularityLimitLow` |  | **无匹配** |
| `PerimeterLimitLow` |  | **无匹配** |
| `LongAxisLimitLow` |  | **无匹配** |
| `PerimeterLimitHigh` |  | **无匹配** |
| `RectangularityLimitHigh` |  | **无匹配** |
| `CenterYLimitHigh` |  | **无匹配** |
| `BlobNumLimitEnable` |  | **无匹配** |
| `BlobTotalAreaLimitHigh` |  | **无匹配** |
| `BlobAreaLimitHigh` |  | **无匹配** |
| `AngleLimitEnable` |  | **无匹配** |
| `BoxHeightLimitLow` |  | **无匹配** |
| `BoxHeightLimitEnable` |  | **无匹配** |
| `BoxHeightLimitHigh` |  | **无匹配** |
| `CircularityLimitLow` |  | **无匹配** |
| `LongAxisLimitHigh` |  | **无匹配** |
| `BoxWidthLimitEnable` |  | **无匹配** |
| `CenterYLimitEnable` |  | **无匹配** |
| `BoxWidthLimitHigh` |  | **无匹配** |
| `CenterXLimitHigh` |  | **无匹配** |
| `AngleLimitHigh` |  | **无匹配** |
| `ScoreLimitLow` |  | **无匹配** |
| `RectangularityLimitEnable` |  | **无匹配** |
| `BoxWidthLimitLow` |  | **无匹配** |
| `LongAxisLimitEnable` |  | **无匹配** |
| `ShortAxisLimitHigh` |  | **无匹配** |
| `CentroidYLimitHigh` |  | **无匹配** |
| `CenterYLimitLow` |  | **无匹配** |
| `PerimeterLimitEnable` |  | **无匹配** |
| `CentroidXLimitHigh` |  | **无匹配** |
| `ScoreLimitHigh` |  | **无匹配** |
| `BlobTotalAreaLimitEnable` |  | **无匹配** |
| `BlobAreaLimitEnable` |  | **无匹配** |
| `BlobNumLimitLow` |  | **无匹配** |
| `CentroidXLimitEnable` |  | **无匹配** |
| `ShortAxisLimitLow` |  | **无匹配** |
| `ScoreLimitEnable` |  | **无匹配** |
| `BlobNumLimitHigh` |  | **无匹配** |
| `CenterXLimitLow` |  | **无匹配** |
| `CentroidYLimitEnable` |  | **无匹配** |
| `ShortAxisLimitEnable` |  | **无匹配** |
| `AngleLimitLow` |  | **无匹配** |
| `CenterXLimitEnable` |  | **无匹配** |
| `CircularityLimitHigh` |  | **无匹配** |
| `CentroidYLimitLow` |  | **无匹配** |
| `CircularityLimitEnable` |  | **无匹配** |
| `CentroidXLimitLow` |  | **无匹配** |
| `BlobTotalAreaLimitLow` |  | **无匹配** |
| `BlobAreaLimitLow` |  | **无匹配** |
| `SelectByCentraBias` |  | **无匹配** |
| `SelectByPerimeter` |  | **无匹配** |
| `SelectByArea` |  | **无匹配** |
| `SelectByShortAxis` |  | **无匹配** |
| `SelectByLongAxis` |  | **无匹配** |
| `SelectByRectangularity` |  | **无匹配** |
| `SelectByCircularuty` |  | **无匹配** |
| `connectivity` |  | **无匹配** |
| `MaxCenterBias` |  | **无匹配** |
| `MinCenterBias` |  | **无匹配** |
| `MaxRectangularity` |  | **无匹配** |
| `MinRectangularity` |  | **无匹配** |
| `MaxCircularity` |  | **无匹配** |
| `MinCircularity` |  | **无匹配** |
| `MaxLongAxis` |  | **无匹配** |
| `MinLongAxis` |  | **无匹配** |
| `MaxShortAxis` |  | **无匹配** |
| `MinShortAxis` |  | **无匹配** |
| `MaxPerimeter` |  | **无匹配** |
| `MinPerimeter` |  | **无匹配** |
| `MaxArea` |  | **无匹配** |
| `MinArea` |  | **无匹配** |
| `SoftHighRatio` |  | **无匹配** |
| `SoftLowRatio` |  | **无匹配** |
| `SoftRightRatio` |  | **无匹配** |
| `SoftLeftRatio` |  | **无匹配** |
| `Softness` |  | **无匹配** |
| `FindNum` |  | **无匹配** |
| `HightSoftThreshold` |  | **无匹配** |
| `LowSoftThreshold` |  | **无匹配** |
| `HightThreshold` |  | **无匹配** |
| `LowThreshold` |  | **无匹配** |
| `Polarity` |  | **无匹配** |
| `ThresholdType` |  | **无匹配** |
