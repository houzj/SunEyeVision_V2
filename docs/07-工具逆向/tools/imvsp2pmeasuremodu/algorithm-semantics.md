# 点点测量 VM 算法语义

- 当前状态：source-partial；capturePriority=C0。

## 已证实

1. 工具实例创建参数对象。
2. 角度/距离/中点限制参数通过 ParamItem 读写。
3. 结果对象携带基本结果和原生点点测量结构。

## 未证实

| 步骤 | 状态 | 下一条证据 |
|---|---|---|
| 两点输入和几何计算 | unknown | 输入绑定、Process/ProcessAlg和运行时 |
| OK/NG判定、结果字段和显示 | unknown | PF结果结构、错误码和结果视图 |
| 保存恢复、停止和释放 | unknown | 序列化和主机生命周期 |

证据：P2PMeasureModuTool.cs、P2PMeasureModuParams.cs、P2PMeasureResult.cs。
