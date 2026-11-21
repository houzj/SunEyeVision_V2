# 条件分支单项目与算法链复核

日期 2026-11-21

## 原厂证据边界

- 原厂条件分支托管入口、动态行、类型选择、范围、判断、执行模块 ID、增删行和 `IfBranchText` 字符串来自 `docs/evidence/ifbranchmodule-managed-evidence-20261120.json` 与 `docs/evidence/ifbranchmodule-control-evidence-20261120.json`。
- 静态证据确认 `int` 与 `float` 两种类型、整数范围 ±999999999、浮点范围 ±100000、OK/NG 状态和动态 `AddIO/RemoveIO` 接缝，但没有确认原厂运行时的区间端点、多个命中分支、动态端口持久化和失败清理时序。

## 本次代码迁移

条件分支已从 Runtime/Wpf 双项目合并到 `tools/SunEyeVision.If/SunEyeVision.Tool.If.csproj`。解决方案、产品注册、逻辑测试、控件探针和生产探针均引用这一单项目。旧项目定义已删除，Release 不再发布 If Wpf DLL。

- `IfTool.cs` 保留工具入口、Schema、输入和控制输出声明。
- `Algorithms/IfSession.cs` 保留冻结校验、逐槽判定、分支选择、诊断和释放。
- `Parameters/` 保留槽位字段、设置、动态文本编解码和参数物化。
- `Results/IfOutput.cs` 保留模块状态、运行时间和命中分支结果。
- `Editor/` 保留动态条件行面板、参数页和设计数据，面板继续通过 `IfBranchText` 接入共享参数页。

## 当前算法链复核

`IfSession` 当前按以下链路工作。

1. 检查会话未释放、取消令牌、设置对象和条件配置。
2. 校验每个槽位的启用状态、来源数值、上下限和 `BranchText`。
3. 依声明顺序遍历四个固定槽位，跳过未启用或无来源槽位，对有限数值执行闭区间比较。
4. 生成命中槽位名称、模块状态、运行时间和 `SelectedBranches`，并在完成、取消或释放时保持会话状态一致。

固定四槽位和闭区间是当前产品实现事实。原厂动态增删行、`int/float` 类型切换、OK/NG 判断、`RunModuleId`、动态输出端口以及 `BranchMatch` 的精确运行语义仍未闭合，不能声明算法同构。

## 验证结果

- 条件分支专项测试 29/29 通过。
- ControlNamingProbe 已确认动态条件行面板在完整产品目录中物化，基本参数页可准入。
- 最终 Release 封存哈希为 `5CDC77BBD3004318EA47A8EDF00AE833BDD06F008C413D0BC42C61AC9594C5BE`。
- 该哈希仅证明本次载荷一致性，不代表原厂动态分支算法已数值等价。

## Release Agent Desktop 运行探针

- 六工具精确节点名批量探针已通过，条件分支窗口成功打开并关闭。
- 结果记录在 `docs/evidence/six-tool-agentdesktop-runtime-20261121.json`，对应运行目录为 `Release/_validation/AgentDesktopToolReview/batch-all/20261120-001532-条件分支1`。
- 该探针确认窗口准入和关闭链路，不替代动态分支、端口和边界语义的原厂等价证据。


