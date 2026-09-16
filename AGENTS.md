# SunEyeVision V2 项目规则

## 协作与写入

- 同一工作目录同一时间只允许一个 AI 修改代码；另一个 AI 仅可分析或评审。
- 开始任务先读取 `docs/collaboration/CURRENT_TASK.md`；交接前更新该文件。
- 跨 AI 交接前记录基线提交号并创建说明明确的临时提交；不要为每次小改动创建无意义提交。
- 需要并行开发时使用独立 Git 分支或 Worktree。

## 架构边界

- UI 不承担工作流调度；工作流执行层不得直接访问 WPF 控件。
- 工作流输入、参数和全局变量应在执行开始时以快照传递。
- 工具插件不得依赖 UI 宿主程序集；插件依赖和发布依赖必须由构建规则部署。
- 修改工作流执行、连续运行、取消、共享图像空间、主题资源、Plugin.SDK、插件加载或发布规则前，先评估跨模块影响。

## 构建与验收

- UI：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore`
- 全量 Release：`dotnet build SunEyeVision.slnx -c Release --no-restore`
- 涉及工具插件或发布规则时必须做全量 Release 构建并运行 Release 程序验证。
- 涉及 UI 时应分别验证 Visual Studio 设计时与运行时；记录用户人工验收结果。

## 交接

- 使用 `docs/collaboration/HANDOFF_TEMPLATE.md` 记录交接内容。
- CodeBuddy 处理常规、边界明确的增量实现；架构、跨模块执行链、复杂编译链和发布问题转交 Codex。
