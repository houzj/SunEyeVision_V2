# 点集对位 VM 算法语义

- 当前状态：source-partial；capturePriority=C0。

## 已证实

1. 工具实例创建参数对象。
2. 结果对象包含基本模块结果和原生点集对位结果结构。
3. Clone/Dispose 是托管生命周期入口。

## 未证实

| 步骤 | 状态 | 下一条证据 |
|---|---|---|
| 点集输入/ROI与所有权 | unknown | XML参数、输入节点、reader链 |
| 对位匹配、坐标变换与阈值 | unknown | Process/ProcessAlg、原生入口、ABI/运行时 |
| 结果字段和失败状态 | unknown | 结果包解析、错误码和受控运行 |
| 保存恢复、停止和释放 | unknown | 序列化与主机生命周期 |

证据：MultiPointAlignModuTool.cs、MultiPointAlignModuParams.cs、MultiPointAlignResult.cs。
