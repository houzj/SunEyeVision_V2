# 协作消息总线（bus）

本目录用来替代「主对话同步转发」的协作方式。目标：队员写完回执就继续干下一张卡，不必等中心回复。

## 为什么不用会话消息

会话消息是同步的：队员汇报 → 中心在场 → 中心回 → 队员才动。中心一旦不在场，全线停摆，且进度只存在于对话上下文里，中断即失忆。

文件总线把「任务」和「回执」落成磁盘状态，谁在什么时候做到哪一步都能直接查，不依赖任何一方在场。

## 目录与归属（单写者原则）

```
docs/collaboration/bus/
  README.md      本协议，任何人可读，只有中心可改
  status.json    任务卡账本，按卡归属分写
  results/       回执，每卡每线一份
  progress/      进度清单，每条线一份（progress/<lane>.md）
  PROGRESS.md    进度汇总看板与模板，中心维护
```

| 角色 | 只写 | 只读 |
|---|---|---|
| 中心（主对话） | `README.md`、`status.json` 中自己负责的卡、跨线结论 | 全部 |
| framework-refactor | `status.json` 中 `lane=framework` 的卡的 `state/updated/note`、`results/T-*-framework.md` | 全部 |
| doc-keeper | 同上，`lane=docs` | 全部 |
| rea-investigator | 同上，`lane=rea` | 全部 |

**任何人不得移动或删除 `docs/collaboration/bus/`**，文档重组归档时跳过本目录。

## 任务卡字段

```json
{
  "id": "T-0007",
  "lane": "framework",
  "title": "一句话说明这张卡交付什么",
  "state": "pending | claimed | done | blocked",
  "priority": 1,
  "goal": "从用户可见行为描述，不讲类名",
  "scope": ["允许修改的路径"],
  "forbidden": ["明确禁止的动作，越界即回退"],
  "acceptance": ["可执行的验收项，含至少一条反例"],
  "note": "阻塞原因或完成摘要",
  "updated": "2026-11-05 14:10"
}
```

## 队员的一轮流程（固定）

1. 读 `status.json`，挑 `lane` 是自己、`state=pending` 的卡，按 `priority` 升序
2. 置 `claimed` 并更新 `updated`，开始干
3. 只改 `scope` 内的路径；`forbidden` 里的事一律不做
4. 干完写 `results/T-xxxx-<lane>.md`：做了什么 / 构建与测试的实测数字 / 未验项 / 需要裁决的问题
5. 置 `done`，**立刻回到第 1 步做下一张卡**，不要回中心等确认
6. 只有需要中心裁决或需要用户授权时才置 `blocked` 并写清原因，然后**继续做别的卡**

## 中心的一轮流程

1. `git status --porcelain` 看落盘事实
2. 读 `status.json`：`blocked` 卡逐条裁决，`done` 卡抽验
3. 新增任务卡、调整优先级
4. 只在批次边界做人工验收与提交，不做逐点审批

## 遇到需要人介入的事怎么办

**不许只写文件不吭声。** 判三级：

- **A 类：必须人类拍板** —— 改变用户可见行为、删除文件/数据、不可逆操作、跨模块取舍
- **B 类：中心可裁决** —— 技术选型、命名、代码结构；写进进度清单即可
- **C 类：只需授权** —— 启动原厂 SDK、开原机测试窗口、需要用户动手跑的验收

A/C 类走**双通道**：
1. 登记到 `docs/collaboration/bus/NEEDS-HUMAN.md`（持久，格式见该文件的「请求单格式」）
2. `send_message` 给中心（即时，会直接出现在对话里让人看见）

**发完立刻继续干别的卡**，把被阻塞的卡置 `blocked`，不许停在原地等人。

## 硬规矩

1. **路径独占**：`src/`+`tests/` 归 framework，`docs/collaboration/evidence/VM*.html` 归 rea，`docs/` 其余归 docs，本目录的 `README.md` 与卡的增删归中心
2. **一张卡 = 一条完整功能链**，不拆成单个 `catch`、单个绑定
3. **越界即回退**：未经批准的删除、移动、重命名，中心直接 `git checkout --` 还原，并要求说明原因
4. **提交**：各线完成一张卡后可自行提交，但只能提交自己 `scope` 内的路径，提交信息用中文并经 `-F` 文件传入
5. **不许悄悄夹带**：构建副产物（如 `packages.lock.json` 变更）、格式化、无关重命名，必须在回执里单列，不得混入功能改动

## 做不到的事（别抱错期待）

- 队员不会自己醒来。agent 没有后台循环，只有被派单才跑一轮。本协议把「一轮」的产出最大化（连做多张卡），并把「是否该动」的判断从中心手里拿走，但**不能消除启动依赖**。
- 并行度上限由构建锁与磁盘冲突决定。代码线要真并行，需要独立 worktree（另需用户明确要求）。
