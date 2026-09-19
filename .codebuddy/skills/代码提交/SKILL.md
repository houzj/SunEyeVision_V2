---
name: 代码提交
description: >
  代码提交技能。保证所有 git 提交符合项目规范且 message 无乱码：
  - message 必须用 UTF-8 文件经 git commit -F 提交，禁止经命令行参数传中文（避免 shell 编码破坏）
  - message 采用中文 Conventional Commits 格式：type(scope): 描述
  - 提交前检查改动范围、提交后自查无乱码

  Trigger keywords: 提交/commit/推送/push/提交并推送/提交代码/提交并推送远程/commit and push
  DO NOT trigger on: 日常编码讨论、只读分析、不涉及 git 提交的请求。
---

# 代码提交 — 规范且无乱码的 Git 提交流程

## 1. 硬性红线（防止乱码与格式违规）

- ❌ **禁止** `git commit -m "中文内容"` 直接传中文参数：命令行参数经 shell/PowerShell 传递时编码会被破坏（已多次导致 message 落库为乱码）。
- ✅ **必须** 将 message 写入 UTF-8 文本文件，再 `git commit -F <文件>` 提交。
- ✅ 提交后用 `git log -1 --format=%s` 自查，若出现乱码立即修正。
- ❌ 禁止用英文 message 规避中文（除非仓库明确英文规范）。
- ❌ 禁止 force push；如需修正已推送历史需先征得用户明确授权。

## 2. Message 规范（中文 Conventional Commits）

格式：

```
type(scope): 一句话描述（中文，简洁概括做了什么）
```

- `type`（必填，小写）：`feat` / `fix` / `docs` / `refactor` / `chore` / `perf` / `test` / `build`
- `scope`（可选，小写括号）：`measure` / `roi` / `render` / `cv` / `workflow` / `sdk` / `plan` / `docs` 等模块名
- 描述：中文、动词开头、一句概括（可含 `+` 连接多要点），**不要**堆叠细节、不加句号

示例：

```
feat(measure): 极性方向三角两层屏幕恒定 + 测量带描边幂律修复
fix(roi): 屏蔽区渲染器缩放同步，线宽不再随放大变粗
docs: 双渲染层幂律缩放治理方案
chore(plan): 同步提交记录到计划看板
chore(release): 记录 2.0.0.1 内测版发布产物
```

## 2.1 版本号写在哪个提交里（防"假映射"）

**普通功能/修复提交禁止写版本号** —— 不是习惯问题，而是写进去必然是假映射：

- **提交时版本尚未分配**：第 4 位构建号由 `installer/Pack-Release.ps1` 在**打包时** +1 并写回 `version.json`（其 `_fields.build` 明确"由打包脚本维护，智能体与人均不得手改"）；
- **一个提交会进入多个版本**：同一份代码可二次打包（如改了图标/安装脚本），版本变、代码不变；
- **一个版本由多个提交组成**：常见 3~5 条提交才对应一个版本；
- 于是 message 里的版本号**既不能唯一定位版本、也不能唯一定位提交**，回退时无用。

**只有"发布记录提交"必须写版本号**：

```
chore(release): 记录 <版本> <渠道>发布产物
```

它与 `version.json` 的构建号写回、`CHANGELOG.md` 一起入库，是"版本 ↔ 提交"的锚点。

**回退不靠 message，靠这三处**：

| 机制 | 说明 |
|---|---|
| **git tag `v<版本><渠道>`** | git 原生锚点，可直接 `git checkout` 回到该次交付 |
| **产物内嵌 `InformationalVersion = <版本>+<提交SHA>`** | 从任意 exe/dll 反查确切提交（`AppVersionInfo.Full` 可读），无需安装包 |
| 真源与发布清单 | `version.json`（版本/build/渠道/日期）＋ `dist/release-manifest-<版本>.json`（含 `commit`/`commitShort`/`version`） |

⚠ 注意：`dist/` 已被 `.gitignore` 忽略，发布清单**只存在于本机**；因此**正式对外交付必须 `Pack-Release.ps1 -Tag`**（内部自测包可免），否则"版本 ↔ 提交"的锚点不在仓库里。

## 3. 提交流程

### Step A: 检查改动

1. 运行 `git status --porcelain` 与 `git diff --stat`，确认改动范围。
2. 确认无无关文件、无构建产物/临时文件（`.codebuddy/build_temp`、`dev/` 等被忽略目录不提交）。
3. 按主题拆分子提交（`git add <指定文件>`，避免 `git add .` 误纳入无关改动）。

### Step B: 写 message 文件（UTF-8）

1. 用 write 工具把 message 写到一个临时 UTF-8 文件（放在仓库根或临时目录均可，提交后删除），内容即完整规范 message（可含多行 body，body 用空行分隔）。
2. message 第一行为标题（subject），后续行为正文（body），全部 UTF-8。

### Step C: 提交

```
git commit -F <message文件路径>
```

### Step D: 自查（必须）

1. `git log -1 --format="%h %s"` 检查标题：无乱码、格式为 `type(scope): 描述`。
2. 若乱码：`git commit --amend -F <修正后的message文件>`（仅未推送时）；已推送则报告用户并征得授权再处理。

### Step E: 推送

- 仅在用户明确要求"推送/提交并推送"时执行 `git push origin main`。
- 推送后确认输出无错误；不主动做 force push。

### Step F: 清理

- 删除临时 message 文件（不残留垃圾文件）。

## 4. 特殊文件约定

- `plans/plan.json`：由 git hook（sync_plan）自动登记提交；若工作区出现其改动且与本次提交相关，单独以 `chore(plan): 同步提交记录到计划看板` 提交；若为历史残留且与本主题无关，可单独提交或询问用户。
- `docs/00-索引/文档索引.md`：随 docs 类提交一起提交（由更新索引脚本维护）。

## 5. 自查清单（输出提交报告前）

- [ ] message 为中文 Conventional Commits（`type(scope): 描述`）
- [ ] 提交走 `git commit -F <UTF-8文件>`，未用 `-m "中文"`
- [ ] `git log -1` 无乱码
- [ ] 提交范围只含本主题文件
- [ ] 推送仅在用户要求时执行
- [ ] 普通提交未写版本号；若为发布记录提交，已含版本号（`chore(release): 记录 <版本> …`）
- [ ] 正式对外交付已打 tag（`v<版本><渠道>`）；内部自测包可免
