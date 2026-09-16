# Codex × CodeBuddy 实际协作方案

## 目标

在不让两个 AI 同时污染同一工作目录的前提下，利用 CodeBuddy 完成边界明确的增量实现，由 Codex 负责架构、复杂故障、发布与终审。

## 核心规则

1. 同一时间、同一工作目录只允许一个 AI 修改代码；另一方只能分析或评审。
2. 并行开发使用不同 Git 分支或 Worktree。
3. 交接前记录基线提交号，并创建有说明的临时提交；不为每次小改动提交。
4. 所有任务开始前读取 `AGENTS.md` 和 `CURRENT_TASK.md`。
5. 所有任务完成后更新 `CURRENT_TASK.md`，记录构建、验收与风险。

## 分工

| 任务类型 | 主体 |
|---|---|
| 单工具 UI、参数页、单文件业务逻辑、样式微调 | CodeBuddy |
| 边界明确的增量功能与基础编译修复 | CodeBuddy |
| 工作流执行、连续运行、取消、并发、快照、子程序 | Codex |
| 共享图像空间、主题基础设施、Plugin.SDK、插件加载、发布部署 | Codex |
| CodeBuddy 两轮后仍无法闭环的跨文件或编译链问题 | Codex |
| 发布前全量构建、发布包依赖检查与终审 | Codex |
| 视觉效果与业务流程验收 | 人工 |

## Git 流程

### 常规增量开发

```text
稳定分支 → buddy/任务名 → CodeBuddy 实现与自测 → Codex 评审 → 合并
```

### 高风险架构任务

```text
稳定分支 → codex/任务名（推荐 Worktree）→ Codex 修改、构建、验收 → 合并
```

## 交接材料

交接时使用 [HANDOFF_TEMPLATE.md](HANDOFF_TEMPLATE.md)，至少包含：

- 基线提交和当前分支；
- 目标与允许修改目录；
- 已修改文件与未完成事项；
- 已执行的构建、测试和人工验收；
- 风险、禁止改动与下一步。

当前进行中的任务记录在 [CURRENT_TASK.md](CURRENT_TASK.md)。

## Codex 配置与使用

项目根目录的 `AGENTS.md` 是 Codex 自动读取的项目规则入口。

每个独立目标单独新建 Codex 任务。任务开头使用：

```text
读取 AGENTS.md、docs/collaboration/CURRENT_TASK.md 和当前 Git 状态。
按项目规则完成以下任务：……
```

任务结束时要求：

```text
更新 CURRENT_TASK.md，说明已修改文件、构建结果、人工验收项和下一步。
```

## CodeBuddy 项目规则

**已落地（2026-09-16）**：`.codebuddy/rules/02-development-process/ai-collaboration-boundaries.mdc`（rule-021，
`alwaysApply: true`），并登记于 `.codebuddy/rules/README.md` 与 `.codebuddy/README.md`。
下方清单与 rule-021 保持同一口径，修改任一处时两边同步。

将以下规则放入 CodeBuddy 的项目规则设置：

```text
先读取仓库根目录 AGENTS.md 和 docs/collaboration/CURRENT_TASK.md。

1. 同一工作目录同一时间只能有一个 AI 修改。
2. 只修改当前任务允许的目录。
3. 不修改 WorkflowExecutionManager、Plugin.SDK、共享主题、插件加载、发布规则；
   如需修改，停止并填写交接单转交 Codex。
4. 完成后必须编译受影响项目。
5. 交接前更新 CURRENT_TASK.md：已改文件、构建结果、人工验收项、风险和下一步。
6. 不要为每个小改动提交 Git；跨 AI 交接前才创建说明明确的临时提交。
```

CodeBuddy 接手提示词：

```text
读取 AGENTS.md 和 CURRENT_TASK.md。
你负责当前任务的增量实现，只修改授权目录。
完成后编译受影响项目，更新 CURRENT_TASK.md。
若涉及工作流执行、SDK、插件加载、主题基础设施或发布规则，停止并转交 Codex。
```
