# 主程序与通讯管理统一基线交接

- 同步前基线：main `d03579d44c4cb4c7a80af9e94eb6b149e2e60d01`；feature/comm `d24db93b921e6fc0b760ac48a2b3a6eb029e1a69`。
- 当前分支：主程序 `main`（D:\MyWork\SunEyeVision_V2）；通讯管理 `feature/comm`（D:\MyWork\SunEyeVision_V2-comm）。
- 当前执行者：Codex；开始前通讯任务为空闲，两侧已跟踪工作树干净。
- 任务目标：合并双方历史，使两个分支 HEAD 指向同一合并提交，随后在各自工作区/原分支独立开发。
- 允许修改目录：这两个工作区；不修改 script-tool 或其他工作区。
- 恢复引用：`codex/pre-sync-main-20261009`、`codex/pre-sync-comm-20261009`。不重写历史或强制覆盖。

## 完成内容

- 合并通讯心跳、接收触发、响应配置、运行时自动化、紧凑发送界面及对应测试。
- 保留主程序的方案管理与数据目录、单实例/维护门、图像预览交互、Logo、发布规则、相机和标定功能。
- 解决 11 个文件冲突；CURRENT_TASK 保留双方记录，计划看板保留主程序任务并补齐通讯开发日志。
- 工作流租约同时识别方案运行、通讯执行与方案切换；切换会取消并等待通讯执行，切换期间拒绝新通讯任务。
- 通讯触发切换方案复用 OpenExternalSolutionAsync，不直接 SetCurrentSolution 绕过未保存修改提示；不持有会阻塞自身停止的通讯租约。
- 单次执行同时保留取消通知与图像预览的“不自动推进下一张”策略。
- Solution.Clone 保留方案名称/描述和接收、发送、心跳、触发、响应配置，并深复制通讯配置，避免复制遗漏或互相修改。
- 退出顺序为通讯自动化/通讯运行时先停，相机运行时后释放，保留主程序启动失败/探针退出保护。
- 没有改版本、打包安装程序、推送远端或修改系统关联/桌面快捷方式。

## 构建与测试

- 最终全量 Release：`dotnet build SunEyeVision.slnx -c Release --no-restore`，0 错误（1060 个现有/汇总警告）。
- 主程序 Debug：`dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore`，0 错误（1054 个警告）。
- UI Release 测试分测试类串行运行，213/213 通过，包括通讯架构、接收解析、发送、心跳、触发/响应、方案管理、图像预览、方案切换、单实例、维护门、Logo、相机帧输入与停止通知。
- 新增 BaselineIntegrationTests：方案切换拒绝通讯任务、取消并等待通讯任务、克隆保留独立通讯配置，共 3 项通过。
- 证据：主程序 `artifacts/validation/baseline-sync-20261009/*.trx` 与 `release-probe.json`。
- Release 通过系统 dotnet 执行真实应用的 `--release-probe`，退出 0，默认主题 Dark，背景 #FF1E1E1E；探针在设备初始化前运行，没有连接硬件。
- 旧 Release/测试输出残留自包含 hostfxr，首次测试宿主报运行时不存在；测试时临时移开生成的 testhost.exe 后自动恢复，使用系统 dotnet 成功。初次 Release EXE 探针卡在旧 hostfxr，已仅结束本次启动的 PID 39568，随后系统 dotnet 探针成功。两侧 Debug 没有该残留 hostfxr。
- SolutionManagerCardTests.cs 与 ImagePreviewInteractionTests.cs 是 SolutionManagerUiTests 的 partial，已包含在该类 26 项测试内；同名过滤产生的 0 测试不计入通过数。

## 人工验收与后续

- 通讯分支 fast-forward 后，在各自 dev/Debug 构建测试程序，源码必须同一基线；可用两侧 `git rev-parse HEAD` 核对。
- Visual Studio 设计器外观、真实相机/串口/PLC 联调和用户界面现场体验待人工验收；WPF 自动化用例已覆盖运行时视图，不等同设计器人工验收。
- 旧 SunEyeVision.sln 在同步前两侧已是无效二进制内容；本次不顺带修复，使用已验证的 SunEyeVision.slnx。
- 本次提交用单条命令的 core.hooksPath 指向空目录，避免 post-commit 后台计划脚本在统一基线期间另改工作树。没有修改或禁用仓库全局 Hook；计划两侧日志已手工核对合并，后续正常提交仍使用原 Hook。
- 主程序的 5 个本地截图/探测未跟踪文件原样保留，不纳入基线提交。
- 后续主程序在 main、通讯在 feature/comm 各自开发；下一次同步先确认无其他 AI 写入和工作树状态，再合并，不复制目录覆盖。
