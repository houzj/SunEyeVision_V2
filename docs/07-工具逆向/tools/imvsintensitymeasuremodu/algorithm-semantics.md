# 亮度测量 VM 算法语义

- 当前状态：source-partial；capturePriority=C0。

## 已证实

1. 工具实例创建参数对象。
2. 五类统计指标的启用/范围参数通过 ParamItem 读写。
3. 结果对象携带基本结果和原生统计结构。

## 未证实

| 步骤 | 状态 | 下一条证据 |
|---|---|---|
| 图像/ROI采样和统计计算 | unknown | 输入绑定、Process/ProcessAlg和运行时 |
| OK/NG判定与状态码 | unknown | 结果结构、错误码和显示链 |
| 保存恢复、停止和释放 | unknown | 序列化和主机生命周期 |

证据：IntensityMeasureModuTool.cs、IntensityMeasureModuParams.cs、IntensityMeasureResult.cs。
