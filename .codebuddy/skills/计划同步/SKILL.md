---
name: 计划同步
description: 开发计划任务智能归类技能。用于修正 git hook（sync_plan.ps1）机械匹配导致的计划归类错误，按语义判断提交真实归属模块并更新 plans/plan.json 后重建看板。当用户要求"更新计划/同步看板/归类任务/检查归类对不对/为什么这个提交归到了X模块"时触发。
---

# 开发计划智能归类技能（计划同步）

`sync_plan.ps1` 在每次 `git commit` 后用关键词/路径查表把提交机械归到 `plans/plan.json` 的模块，**经常归错**（关键词宽泛、路径竞态、状态靠猜）。本技能让 Agent 用语义理解校准归类。

本技能是 rule-020 的落地执行器。

## 触发条件

- 用户要求"更新计划 / 同步看板 / 归类任务 / 检查归类对不对 / 为什么提交归到了 X 模块"
- rule-020 判定归类复杂（多模块、跨层、语义模糊）时

## 核心目标

把"这次改动到底属于哪个模块/任务"判断对，并让 `plans/plan.json` 反映真实归属。

## 执行步骤

### 1. 读取本次改动的真实文件清单

先确认要校准的目标提交（默认最近一次提交）：

```bash
# 最近一次提交的 hash + 信息
git log -1 --format="%H %s"
# 本次提交改动的文件清单
git show --name-only --format="" HEAD
```

若用户指定历史提交，用 `git show <hash> --name-only --format=""`。

### 2. 读取 plan.json 模块语义

读取 `plans/plan.json` 的 `modules[]` 和 `stages[]`，重点是每个模块的：
- `id` / `name` / `goal`（语义）
- `tasks[]`（该模块下的开发任务，含 id/name/status/week）

用模块的 `name` + `goal` 判断"这个模块是干什么的"，与本次改动文件/意图对比。

### 3. 语义匹配（替代关键词查表）

判断本次改动**真实归属**，依据优先级从高到低：
1. **文件路径的直接归属**（改 `tools/SunEyeVision.Tool.Script/` → T-SCRIPT；改 `src/UI/Features/WorkflowUI/` → M-HIST-CANVAS 等）
2. **提交信息/改动意图的语义**（"修复齿轮按钮主题" → M-SRC-UI，即使没命中"主题"关键词）
3. **改动内容的本质**（加测试文件但主体是 UI → 主归属 UI，测试作为附属）

> 若改动明确横跨多模块（如同时重构工具 + 其测试），主归属选**业务改动最大**的模块，其余作为日志附属，不强制拆分。

### 4. 对比 hook 结果，找出误差

读 `plans/sync.log` 末尾，看本次提交 hook 实际归到哪：
- `匹配模块: X` → hook 的归类
- `推断状态: done/in_progress/log` → hook 的状态推断
- 是否出现"未匹配 → 临时任务池"

与第 3 步的真实归属对比，标记误差项。

### 5. 修正 plan.json（只改状态/日志/临时任务池）

若归类错误，直接修正 `plans/plan.json`：

- **模块归属错**：把误加的状态/日志从错误模块移除，加回正确模块。
  - 注意：移除 `tasks[].status` 改动时，需还原该任务到修正前的状态（参考 `plans/backup/` 里最近的备份）。
- **状态推断错**：按真实进展改 `tasks[].status`（todo/in_progress/done）和模块 `status`。
- **误入临时任务池**：从 `tempTasks[]` 移除，归入正确模块（含日志）。
- **模块不存在**：不改 `modules[]` 结构，记入 `tempTasks[]`（或提示用户是否新建模块）。

> 修正原则（rule-008）：只修状态/日志/临时任务池，**不删除、不重排**已有模块结构。

### 6. 重新生成看板

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "plans/generate_plan.ps1" -PlanDir "<仓库根>/plans"
```

### 7. 汇报给用户（说人话，rule-019）

用一句话说明修正结果，例如：
> "上次提交 hook 按关键词归到了 M-SRC-TEST，但实际改的是子程序工具 UI，我改回 T-SUBROUTINE 了，看板已刷新。"

## 防御机制

| 机制 | 说明 |
|------|------|
| 语义优先于关键词 | 以改动本质判断，不信 hook 查表 |
| 备份参照 | 修正状态前查 `plans/backup/` 最近备份，避免误还原 |
| 可见汇报 | 每次修正都向用户说明"hook归错→改到哪"，不静默（rule-015） |
| 看板重建 | 改完必重建 HTML，保证所见即所得 |

## 禁止

- 禁止静默忽略 hook 的归类错误（rule-015）
- 禁止删除/重排 `plan.json` 的模块结构（只修状态/日志/临时任务池）
- 禁止手工大改 `plan.json` 格式（保持 `sync_plan.ps1` 可继续读写）
- 禁止直接编辑 `plans/plan.html`（由脚本生成）
