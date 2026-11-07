# 圆圆测量 VM 算法语义

- 当前状态：source-partial；capturePriority=C0。

## 已证实

1. 工具实例 → C2CMeasureModuParams。
2. 两组圆边缘/拟合/限制参数通过 ParamItem 管理。
3. 结果桥的托管包装复制两组检测区域指针并在终结器释放。

## 未证实

| 步骤 | 状态 | 下一条证据 |
|---|---|---|
| 输入圆/ROI和卡尺区域生成 | unknown | XML绑定、输入结构和Process入口 |
| 两圆边缘提取、拟合和距离/交点计算 | unknown | 原生函数体、ABI、运行时差分 |
| 结果字段、判定与显示发布 | unknown | PF结果结构、结果视图和错误码 |
| 保存恢复、取消和释放顺序 | unknown | 主机生命周期/序列化证据 |

证据：C2CMeasureModuTool.cs、C2CMeasureModuParams.cs、C2CMeasureResult.cs。
