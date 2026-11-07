# 协作记录区（collaboration）

> 这里放「过程凭据」：谁在什么时候交了什么、依据是什么、结论怎么定的。它不是产品文档，找功能和设计结论请回 `docs/09-现行文档/` 或 `docs/00-索引/主题台账.html`。

## 目录职责

| 位置 | 放什么 | 维护方式 |
| --- | --- | --- |
| `CURRENT_TASK.md` | 任务账本：当前主线、已定案、未验证、最近 12 个批次 | 顶部摘要人工维护；最近批次由 `docs/00-索引/拆分任务账本.ps1` 保留 |
| `ACTIVE_GOAL.md` | 当前阶段目标（短文件） | 阶段切换时改写 |
| `handoff/<年>/<月>/` | 交接单 `HANDOFF_*.md`，共 257 份 | 只追加；新增按当月目录放 |
| `decisions/<年>/<月>/` | 决策、审计、评审类记录（json / md），共 82 份 | 只追加 |
| `evidence/` | 取证材料：原件源码副本、原件窗口截图、检测器输出 | 按批次建子目录，见 `evidence/README.md` |
| `history/` | 任务账本历史分卷 `CURRENT_TASK_ARCHIVE_<年-月>.md`、旧版账本快照 | 只追加，不改写 |
| `patches/`、`subtasks/` | 补丁与子任务留档 | 按需 |

## 三个必须遵守的约定

1. **交接单一律用 `HANDOFF_TEMPLATE.md`**，命名 `HANDOFF_<主题>_YYYYMMDD.md`，放进 `handoff/年/月/`。
2. **证据与结论分开**：只有源码身份、SHA、行号、截图这类可复核材料进 `evidence/`；结论写进 `decisions/` 或产品文档。
3. **不删除、不改写历史记录**。写错了追加一条更正，不要回头改原文；`CURRENT_TASK.md` 超长时用拆分脚本按月切走，不手工删段。

## 找旧文件

2026-11-05 之前的交接单与决策记录已从根目录分层到 `handoff/`、`decisions/`。老文档里如果还写着 `collaboration/HANDOFF_xxx.md` 这类旧路径，按 `handoff/迁移映射.md` 的映射表换成新路径即可。

## 相关契约

- 取证约定：`EVIDENCE_AGREEMENT.md`
- 交接模板：`HANDOFF_TEMPLATE.md`
