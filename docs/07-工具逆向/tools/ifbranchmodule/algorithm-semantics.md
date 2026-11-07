# 条件分支 VM 算法语义

- 工具 ID：`ifbranchmodule`
- 组装类别：compound-control
- 当前状态：**inventory-only**（本库没有任何该工具的原件源码或资源）
- 语义状态：8 步 inferred 来自 Codex 记录、未复验；1 步 unknown

本文 9 步与 `evidence.json` 的 `semanticChain` 同步；页面由卡片数据渲染。

## 证据入口与其局限

| 文件 | SHA-256 前缀 | 能证明什么 |
|---|---|---|
| `docs/07-工具逆向/_reference-codex/site/工具/Logic-IfBranchModule-caf95b67.html` | `D675B6C7F761` | 只证明 Codex 记过这些结论；页面描述 VisionReplica 状态，不是我方状态 |

原卡引用的三份反编译源码**不在本仓库**，无法复查：

- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchModuleTool.cs`
- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchParam.cs`
- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchResult.cs`

因此下面所有 `inferred` 都读作「Codex 记录、我方未复验」，不是源码级确认。

## 执行语义链

| 步骤 | 结论 | 原版证据 | 仍需补什么 |
|---|---|---|---|
| 工具创建/初始化 | inferred | Codex 记录 `IfBranchModuleTool` 为 dynamic 且内嵌 model | 构造函数、注册链、资源绑定；原始源码未随库迁入 |
| 参数读取与条件显隐 | inferred | Codex 记录 `IfBranchParam` 以 `name_Min` / `name_Max` / `name_State` 动态存放条件边界 | 参数结构与 XML 绑定；`name_State` 取值集合 |
| 输入/ROI/变量准备 | inferred | 条件值取自上游模块结果；我方由 `ConditionInputResolver` 解析绑定表达式或常量 | 原版上游取值路径与类型转换；本工具无 ROI |
| Process/ProcessAlg/原生桥 | inferred | Codex 记录 `Run` 调 `SyncSelfRun` 完成判定 | 判定函数体；是否遇命中即短路 |
| 分支、失败与取消 | inferred | Codex 记录结果同时暴露 `ModuStatus` 与 `BranchMatch` | 未命中/失败/取消的错误码与剪枝行为 |
| 结果写回与判定 | inferred | 原版写 `ModuStatus` + `BranchMatch`；我方写 `IsMatched` / `NextNodeIds` / `MatchedRuleNames` | `BranchMatch` 的类型与是否支持多命中 |
| 图形/文本显示发布 | unknown | 未取证 | 条件标签、命中高亮的显示路径 |
| 保存恢复 | inferred | Codex 记录 `GetModel`/`ImportModel` 传输 `IfBranchText` 字节，`Paste` 把条件引用从旧 ID 重映射到 `NewIdList` | 序列化格式、版本迁移与完整重映射规则 |
| DeInit/Destroy/释放 | unknown | 未取证 | 释放侧完全没有证据 |

## 未证实项

1. 原卡引用的三份反编译源码不在本仓库，全部结论无法复查。
2. 判定函数体、端点语义（含/不含）与短路行为。
3. `BranchMatch` 的类型、语义与多命中支持。
4. `name_Min` / `name_Max` / `name_State` 的取值集合与原版默认边界。
5. XML/XAML 绑定、条件显隐与主题状态。
6. 显示发布与释放链。
7. 原机差分：空输入、未配置目标节点、失败、取消与嵌套分支生命周期。

## 规则

只接受原厂反编译源码、资源、汇编/ABI 或受控运行证据。Codex 的工具页是二手记录，只能算线索；方案设计文档只支撑我方实现，不能作为原件语义证据。
