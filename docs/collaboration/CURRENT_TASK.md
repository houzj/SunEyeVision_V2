# 当前协作任务

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：人工。
- 当前任务：安装包已生成（与当前构建配置一致），等待人工安装验证。
- 修改权限：`installer/`（`dist/` 为产物目录，不入库）。
- 已完成：
  - **Release 包已全量重建**（2026-09-16 15:40，`BUILD_EXIT=0`；308 文件 / 608.3MB；`plugins/` 下 18 个工具目录）；
    程序集版本串同步为 `1.0.0+fb49f6b2…`（.NET SDK 默认 InformationalVersion，可反查源码修订）；
    两个 HALCON 部署目标均对 Release 输出执行（25 个 native dll + `halcondotnetxl.dll` 托管程序集）。
  - **重新编译安装包**：`dist\SunEyeVision-Setup-1.0.0-x64.exe`（158MB；
    SHA256 `00766C8F2FE1AE776718F36EB8909AB02990A52B7ED0BAF67788CA9ACFA3CF48`；编译 197.8s）。
    旧包（SHA256 `508FE0…`，15:15 生成）**已作废** —— 其源包早于 `Directory.Build.targets` 的 HALCON 托管部署改动。
  - **Release 程序运行验证**：PID 44184，`Responding=True`，日志 `logs/2026/09/16/SunEyeVision_154537.txt`
    （1489 行，`[ERROR]`/`[WARN]` 均为 0）；解决方案加载成功（`Workflows=5`），16 个工具类型空洞池初始化正常。
  - `dist/` 已加入 `.gitignore`，避免 158MB 产物入库。
- 已知偏差（未处理，涉及发布规则禁区）：产品版本 `1.0.0` 硬编码于 6 处
  （`installer/SunEyeVision.iss:5,8`、`Directory.Build.props:45,47`、`Directory.Build.targets:41,168`、
  `run_rule_checker.bat:12,23`、`src/UI/Shell/Windows/AboutWindow.xaml:37`），无单一真源，
  且界面"关于"显示写死 `v1.0.0.0` 与程序集版本脱钩。
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
