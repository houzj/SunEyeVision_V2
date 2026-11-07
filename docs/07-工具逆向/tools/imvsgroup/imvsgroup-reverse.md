# Group 工具逆向主文档

- 工具 ID：imvsgroup
- 原厂工具：IMVSGroup
- UI 组装类别：compound-control
- 逆向优先级：P4
- 当前状态：source-partial

## 已确认的 VM 语义

IMVSGroupTool creates GroupParam and initializes default IO; Run calls SyncSelfRun with UI response flag; result queries module result and wraps GroupResult; GroupParam exposes loop/break/data-type/compare parameters and dynamic image/string input bridges; GroupResult exposes dynamic typed output readers; Load/Save/Destroy APIs are present.

## 证据路径

- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/IMVSGroupTool.cs
- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/GroupParam.cs
- analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/GroupResult.cs

## 尚未闭合

- Process/ProcessAlg 或原生算法实现和返回码。
- XML/XAML 绑定、条件显隐、主题/折叠状态与真实 UI 预览。
- 保存/恢复版本键、取消/停止和最终释放的完整链路。

详见 algorithm-semantics.md、ui-preview.json、evidence.json；unknown 不得按产品意图补齐。
