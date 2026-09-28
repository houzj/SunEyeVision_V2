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

### 0. 先确认「实时同步」链路是活的（用户抱怨"看板不实时"时必查）

计划同步由 `.git/hooks/post-commit` → `plans/sync_catchup.ps1` 驱动。链路断了的典型表现是
**看板进度长期不动、新提交查不到**。按下面顺序排查，不要直接开始归类：

```bash
ls -la .git/hooks/ | grep post-commit      # 1. hook 是否被改名/停用（曾出现 post-commit.disabled）
cat plans/.last_synced_commit              # 2. 水位线停在哪条提交（与 HEAD 差多少条）
git log --oneline $(cat plans/.last_synced_commit)..HEAD | wc -l   # 3. 漏了多少
tail -20 plans/sync.log                    # 4. 最近一次同步是否报错
```

| 症状 | 根因 | 处置 |
|------|------|------|
| 水位线远落后于 HEAD | hook 被停用（改名 .disabled）、换 worktree/分支、连续提交时上一个还没跑完 | 恢复 hook（`chmod +x .git/hooks/post-commit`）+ 跑一次 `sync_catchup.ps1` 补齐 |
| 单提交同步但总漏 | 旧 hook 只处理 HEAD，漏一次永久漏 | 升级为 catch-up（按水位线补齐区间内所有非 merge 提交） |

补齐完成后**必须把水位线推到 HEAD**（`sync_catchup.ps1` 在无失败时自动做），否则下次 hook 会
重复扫同一区间。水位线严格对齐"最后一个已语义同步的提交"，是避免日志重复的关键。

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

### 6.5 大批量补齐（区间提交 > 20 条时走这条批量通道）

hook 漏跑 / 换 worktree / 切主线后常出现几十上百条提交未同步，逐条改不现实，用现成脚本三步走：

```bash
# ① 干跑预览：按修正后规则分类，看模块分布与可疑归属（不写任何文件）
py plans/_sync_preview.py <last_synced_hash>            # 加 --all 看全量明细

# ② 生成载荷：分类结果 + 状态更新表 + 临时任务池归位 → plans/_sync_payload.json
py plans/_build_sync_payload.py <last_synced_hash>

# ③ 重建周追溯（必须放在 apply 之前/之后各一次，保证 gitlogs 与 plan.json 一致）
py plans/generate_weekly.py

# ④ 应用到 plan.json（幂等：按 hash 去重日志，已应用的重复运行不会产生变更）
#    注意必须用「嵌套 powershell.exe -File」，直接 & 脚本会被沙箱拒绝
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<仓库根>/plans/_apply_plan_sync.ps1"

# ⑤ 重建看板
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<仓库根>/plans/generate_plan.ps1" -PlanDir "<仓库根>/plans"

# ⑥ 把水位线推到 HEAD（关键收尾）：告诉 hook「这批提交已经语义同步过了」，
#    否则下次提交时 hook 会用机械规则再记一遍，导致同条提交两条日志（去重逻辑会兜住，但没必要）
git rev-parse HEAD > "<仓库根>/plans/.last_synced_commit"
```

批量通道的状态判定表写在 `plans/_build_sync_payload.py`（模块级 `module_status` + 任务级 `task_status_flat`），
每次批量同步按当时的真实进展改这两张表即可。

### 6.6 匹配规则的三个维护点（归类偏差的根因都在这）

1. **`pathMap` 的目录名必须与磁盘一致**。曾因写成 `tools/SunEyeVision.Tool.PointToPoint`（实际是
   `SunEyeVision.Tool.PointPointMeasure`）导致六个几何测量工具的提交全部落到别的模块。改前先 `ls tools/`。
2. **浅命中规则**：交叉性「工具基座 / 契约 / 结果呈现」提交会同时改十几个工具目录、每处仅 1~2 个文件，
   命中面广而浅时不再认领给第一个工具模块，改判给 src 侧模块（契约/参数/结果树/预览），工具目录仍作关联。
   `generate_weekly.py:match_module` 与 `sync_plan.ps1:Match-ModuleId` 两处逻辑必须同步改。
3. **历史覆盖表**：无法用规则表达的一次性语义判定，写进 `generate_weekly.py` 的
   `manualOverrides`（hash 前 8 位 → 模块 ID）与 `skipCommits`（不计入产品计划的提交，如 checkpoint / 协作规则）。
   `_build_sync_payload.py` 复用同一张表，保证看板与日志口径一致。

### 7. 汇报给用户（说人话，rule-019）

用一句话说明修正结果，例如：
> "上次提交 hook 按关键词归到了 M-SRC-TEST，但实际改的是子程序工具 UI，我改回 T-SUBROUTINE 了，看板已刷新。"

批量补齐时，汇报要包含：区间提交数、计入/跳过数、被归错并纠正的典型样例、状态变更汇总、看板进度变化。

## 已知坑（踩过的）

| 坑 | 现象 | 处理 |
|----|------|------|
| PowerShell 工具无法执行 .ps1 | 直接 `& script.ps1` 或工具内 `Invoke-Expression` 均被沙箱拒绝，且该工具 stdout 拿不到 | 一律用嵌套调用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <脚本>`；脚本把结果写进自己的日志文件，再用 Read 读回 |
| .ps1 缺 UTF-8 BOM | 中文被 PS 5.1 按 ANSI 解码，随机踩坏引号 → 解析失败、exit 1 且无任何输出 | 新建/改写 .ps1 后确认文件头是 `EF BB BF`，且**只能有一个 BOM**（重复 BOM 同样解析失败） |
| 跨模块任务 ID 撞名 | `M-SRC-COMM` 与 `T-COMMTOOL` 都有 `COMM-1..4`，按 id 全局搜索会把状态写到错误的模块 | 任务状态一律按「模块 ID + 任务 ID」定位，载荷按模块归组（`TASK_OWNER` 前缀表） |
| `--no-merges` 口径 | generate_weekly.py 跳过 merge 提交，批量补同步若含 merge 会造成统计口径不一致 | 分类时同样跳过 merge（merge 内容由被合并的原子提交承载） |
| 业务前缀白名单 | `installer/`、`version.json`、`testdata/`、`Directory.Build.props`、`.slnx` 曾不在白名单，发布/测试基建提交被整段丢弃 | 需要计入时把前缀补进 `_business_prefixes`（py）与临时任务池判据（ps1） |
| 双通道重复记日志 | 语义批量同步（`_apply_plan_sync.ps1`）与 hook 单提交同步各记一次同一提交 → 同条提交在模块日志里出现两次 | `_apply_plan_sync.ps1` 内置按 `[hash]` 去重（人工写的无 hash 备注保留）；批量同步后把水位线推到 HEAD，避免 hook 再扫一遍 |
| 阶段状态不跟着走 | 测量工具链完工后，看板仍显示"所处步骤 → CC 测量工具链（在建）"，当前开发模块不带 ▶ 徽标 | 修 `stages[].status`（完工 → done、开工 → in_progress 并校正 start/end），`generate_plan.ps1` 支持多个 in_progress 阶段并行显示为 NOW |
| 看板"不实时"感受来自阶段而非提交 | 提交日志其实进了 plan.json，但路线图"现在在哪一步"是错的 | 每次批量同步顺带核对 `stages[]` 与 `milestones[]`，写进载荷的 `stageFields` / `milestoneFields` |

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
