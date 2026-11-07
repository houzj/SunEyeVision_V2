# 条件分支 工具逆向主文档

- 稳定工具 ID：`ifbranchmodule`
- 原厂工具：`IfBranchModule`
- 原厂类别：Logic
- UI 组装类别：compound-control
- 逆向优先级：P4
- 当前状态：**inventory-only**（本库没有原件源码，只有资源声明与 Codex 二手记录）
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Logic\IfBranchModule`

## 逆向范围

资源身份、UI 组装、参数与绑定、条件取值、判定与分支、结果与判定、保存恢复、停止释放，以及我方 `tools/SunEyeVision.Tool.Condition` 的接入差异。

## 已登记清单

| 项 | 数量 | 口径 |
|---|---|---|
| XML 资源 | 6 份 | 均带 SHA-256 |
| 页签 | 1 个 | Tab_Basic Params |
| UI 控件 | 2 条 | HeadBar/ListHead、ButtonSelecter_IfBranch/RangeConFFields |
| 共享组 | 1 个 | BottomCommandGroup |
| 输出 | 3 项 | Codex 清单口径 |
| 参数 | 0 项 | 固定参数表为空，条件是动态键 |

## 当前证据状态

- 7 个分析环节：ui / common / parametersRoi 为 **partial**（仅 XML 声明级），execution / display / storage / native 为 **unknown**。
- 9 步语义链：8 步 inferred（来自 Codex 记录，我方未复验），displayPublish 与 release 为 unknown。
- 7 维算法对照：4 维 partial，3 维 not-established。

**状态纠偏**：原卡标 `source-partial`，依据是三份 `analysis/decompiled/IfBranchModule.WorkflowAudit/...` 反编译源码。该路径在本仓库不存在，无法复查，故降为 `inventory-only`。

## 已有逆向证据

| 文件 | 内容 | 局限 |
|---|---|---|
| `docs/07-工具逆向/_reference-codex/site/工具/Logic-IfBranchModule-caf95b67.html` | Codex 的逆向记录 | 描述 VisionReplica 状态，非我方、非原件复验 |
| 同目录 `resources.json` | 6 份 XML 的路径与 SHA-256、页签与控件声明 | 静态声明，不含绑定与显隐 |

## 引用但缺文件

- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchModuleTool.cs`
- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchParam.cs`
- `analysis/decompiled/IfBranchModule.WorkflowAudit/IfBranchModuleCs/IfBranchResult.cs`

## 我方实现（非原件证据）

| 文件 | 作用 |
|---|---|
| `tools/SunEyeVision.Tool.Condition/ConditionTool.cs` | 判定入口，多选并行收集 NextNodeIds |
| `tools/SunEyeVision.Tool.Condition/Models/ConditionParameters.cs` | 强类型 ConditionRule（RuleType / InputText / Min / Max / Judgment / ExpectedText / ExecutionModuleId） |
| `tools/SunEyeVision.Tool.Condition/Models/ConditionResults.cs` | IsMatched / NextNodeIds / MatchedRuleNames，实现 IControlFlowResult |
| `docs/01-方案设计/2026/08/26/条件分支多选与整条剪枝执行方案-20260826-161849.md` | 多选并行与剪枝的设计依据 |

## 下一步

1. 取回三份反编译源码或重新反编译，否则上面 8 个 inferred 只能长期停在二手记录。
2. 证 `BranchMatch` 的类型与多命中行为，这是我方多选并行是否与原版一致的唯一判据。
3. 取显示发布与释放链的证据，补两处 unknown。
4. 原机差分：空规则、未配置目标节点、失败、取消与嵌套分支生命周期。
