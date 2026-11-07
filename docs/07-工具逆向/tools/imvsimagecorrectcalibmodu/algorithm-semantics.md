# 畸变校正 VM 算法语义

- 当前状态：source-partial；capturePriority=C0。

## 已证实

1. 工具实例 → ImageCorrectModuParams。
2. LoadCalibPath/CorrectModel 通过 ParamItem 读写。
3. 结果构造从 native image pointer/length 复制图像数据；析构释放分配内存。

## 未证实

| 步骤 | 状态 | 下一条证据 |
|---|---|---|
| 标定文件加载/校正矩阵解析 | unknown | 文件格式、读取成员和资源 |
| 三种校正模式的算法执行 | unknown | Process/ProcessAlg、原生 ABI、运行时 |
| 输出图像所有权/失败状态 | unknown | 结果包、错误码和释放链 |
| UI/保存恢复/停止 | unknown | XML/XAML和主机生命周期 |

证据：ImageCorrectModuTool.cs、ImageCorrectModuParams.cs、ImageCorrectResult.cs。
