# Group VM 算法语义

- 当前状态：source-partial

## 已证实步骤

IMVSGroupTool creates GroupParam and initializes default IO; Run calls SyncSelfRun with UI response flag; result queries module result and wraps GroupResult; GroupParam exposes loop/break/data-type/compare parameters and dynamic image/string input bridges; GroupResult exposes dynamic typed output readers; Load/Save/Destroy APIs are present.

## 未证实步骤

| 步骤 | 状态 | 取证任务 |
|---|---|---|
| 条件/循环/动态输入的具体执行顺序 | unknown | 获取 Process/ProcessAlg、原生调用和运行时分支证据 |
| 结果状态和失败分支 | unknown | 结果包、错误码与受控运行 |
| 保存恢复与释放 | unknown | 序列化、Stop/DeInit/Destroy 调用链 |

证据来源：
- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/IMVSGroupTool.cs
- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/GroupParam.cs
- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/GroupResult.cs
