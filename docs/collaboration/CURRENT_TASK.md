# 当前协作任务

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：人工。
- 当前任务：安装包已生成，等待人工安装验证。
- 修改权限：`installer/`（`dist/` 为产物目录，不入库）。
- 已完成：Release 包已全量构建并人工验证可正常启动、加载工具和执行流程；发布包已部署 HALCON 托管与原生依赖；
  **已用 Inno Setup 6.7.3（`D:\Program Files\Inno Setup 6\ISCC.exe`）编译 `installer/SunEyeVision.iss`**，
  产出 `dist\SunEyeVision-Setup-1.0.0-x64.exe`（158MB；源包 608.3MB / 308 文件；编译耗时 219s）；
  `dist/` 已加入 `.gitignore` 避免产物误入库。
- 下一步：人工运行安装包完成安装验证（管理员权限 → 安装到 `{autopf}\SunEyeVision` → 程序菜单与可选桌面快捷方式
  → 安装后启动、加载工具、跑通执行流程）。

## 工作流运行引擎架构优化（设计完成，待实施）

- 审计与实施设计：`docs/05-框架架构/2026/09/16/工作流执行架构审计-20260916.md`、
  `docs/01-方案设计/2026/09/16/工作流运行引擎全量重构设计-20260916.md`。
- 当前状态：仅完成只读审计和设计，不改生产代码。
- 首批工作包：Codex C0（运行契约冻结）与 CodeBuddy B0（测试/验收矩阵）；必须使用独立 Worktree，
  且 CodeBuddy 不得修改执行核心。

## CodeBuddy 侧协作配置（2026-09-16）

- 已落地规则 **rule-021 双 AI 协作写入边界**
  （`.codebuddy/rules/02-development-process/ai-collaboration-boundaries.mdc`，`alwaysApply: true`），
  并登记进 `.codebuddy/rules/README.md` 与 `.codebuddy/README.md`。
- 规则要点：任务开始必读 `AGENTS.md` + 本文件；同一工作目录单写者；**禁区**（工作流执行链 /
  Plugin.SDK / 共享主题 / 插件加载 / 发布规则）停止并转交 Codex 并出交接单；完成后编译受影响项目；
  交接前更新本文件；提交按主题且中文 message 经 UTF-8 文件 `git commit -F`。
- **代执行说明**：此前由 CodeBuddy 提交的 `src/UI/**`、`src/Plugin.SDK/**`、
  `src/Plugin.Infrastructure/**`、`Directory.Build.targets`、`installer/**` 等改动，
  **内容来源为人工 / Codex 已修改**，CodeBuddy 仅代构建、启动与提交。
  建议后续在本文件按任务写明"**代执行（不改码）**"授权，以启用 rule-021 第 7 条。
- 待人工或 Codex 复核：授权目录口径是否按任务逐条声明。
