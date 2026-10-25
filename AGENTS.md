# SunEyeVision V2 项目规则

## 协作与写入

- 同一工作目录同一时间只允许一个 AI 修改代码；另一个 AI 仅可分析或评审。
- 开始任务先读取 `docs/collaboration/CURRENT_TASK.md`；交接前更新该文件。
- 需要逆向或实机证据时遵守 [取证约定](docs/collaboration/EVIDENCE_AGREEMENT.md)，先复用已有材料，按缺口取证；该约定不恢复旧多角色协作。
- 跨 AI 交接前记录基线提交号并创建说明明确的临时提交；不要为每次小改动创建无意义提交。
- 当前只在本总监对话中工作，不跨对话派工、不自行启用其他角色或子智能体；主仓库是唯一日常工作目录。确需并行开发时先取得用户明确要求，再使用独立 Git 分支或 Worktree。
- 后续所有 Git 提交的标题和正文均使用中文描述；必要的类名、路径、命令及版本号可保留原文。适用于正式提交、临时保存点和合并提交，不改写已有历史提交。

## 架构边界

- UI 不承担工作流调度；工作流执行层不得直接访问 WPF 控件。
- 工作流输入、参数和全局变量应在执行开始时以快照传递。
- 工具插件不得依赖 UI 宿主程序集；插件依赖和发布依赖必须由构建规则部署。
- 修改工作流执行、连续运行、取消、共享图像空间、主题资源、Plugin.SDK、插件加载或发布规则前，先评估跨模块影响。

## 构建与验收

- 自动测试与手动测试均针对主仓库正式 Release 目录 `Release/SunEyeVision_v1.0.0/SunEyeVision` 的同一版本产物；测试数据与日志在 Release 下按用途隔离，不另复制仓库、程序或算法库，不以其他隔离包代替正式 Release 验收。

- UI：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore`
- 全量 Release：`dotnet build SunEyeVision.slnx -c Release --no-restore`
- 涉及工具插件或发布规则时必须做全量 Release 构建并运行 Release 程序验证。
- 涉及 UI 时应分别验证 Visual Studio 设计时与运行时；记录用户人工验收结果。

## 交接

- 使用 `docs/collaboration/HANDOFF_TEMPLATE.md` 记录交接内容。
- 形成完整且可追溯的变更后，按优化用途分类使用中文提交并按需推送 GitHub origin；不强制推送，不改写已有历史。代码同步、构建通过、独测通过与用户人工验收分别记录，禁止混称完成。
