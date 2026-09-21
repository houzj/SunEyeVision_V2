# 当前协作任务

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：Codex。
- 当前任务：工作流运行引擎架构优化（M3 全局就绪队列、M5 全局变量数据总线与绑定 UI）。
- 修改权限：`src/Workflow/`、`src/Plugin.SDK/`、`src/UI/Services/Workflow/`、`src/UI/Shell/Windows/`、`src/UI/Features/GlobalVariableManager/`、`src/UI/Features/ParameterSetting/`、`tests/`、`docs/`。
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
  - **安装包图标改用应用图标（已完成）**：`.iss` 新增 `#define AppIcon` → `SetupIconFile`（Setup.exe 图标）
    与 `UninstallDisplayIcon`（卸载项图标）；已于 **2026-09-17 08:13 重新编译**，
    产物 `dist\SunEyeVision-Setup-1.0.0-x64.exe`（158.1MB；
    SHA256 `C347A0B03541D1A66C06372EA17D1A4599CCFBDCA30B5472DD96C121EAAF8F89`；编译 187.8s）
    ⇒ **取代**上面的 `00766C8F…`。
    **图标已验证**：安装包图标与应用 exe 图标同源（提取后 PNG-SHA256 均为 `19e6cb67…`），旧包为 Inno 默认图标（`db090be5…`）。
    **更正**：先前"脚本无 UTF-8 BOM 会导致向导中文乱码"的判断**有误** —— 实测 ISCC 6.7.3 对有/无 BOM 的
    UTF-8 脚本均正确解析（产物中文名码点一致），BOM 已保留但非必需条件。
- 已知偏差（未处理，涉及发布规则禁区）：产品版本 `1.0.0` 硬编码于 6 处
  （`installer/SunEyeVision.iss:5,8`、`Directory.Build.props:45,47`、`Directory.Build.targets:41,168`、
  `run_rule_checker.bat:12,23`、`src/UI/Shell/Windows/AboutWindow.xaml:37`），无单一真源，
  且界面"关于"显示写死 `v1.0.0.0` 与程序集版本脱钩。
- 下一步：**人工安装验证**（管理员权限 → 安装到 `{autopf}\SunEyeVision` → 程序菜单与可选桌面快捷方式
  → 安装后启动、加载工具、跑通执行流程）。
- 载荷说明：安装包内容为 **2026-09-17 08:10 重建** 的 Release 包（程序集版本串 `1.0.0+ba2b3ab9…`），
  **含**工作流运行引擎改造（`ba2b3ab9`）；载荷写入 08:10:18 早于打包 08:13:18，且 Release 目录内
  **无**比安装包更新的文件（308 文件 / 608.4MB），时间线自洽。
  ⚠ 该次重建在提交之上还带入了 **3 个当时未提交的 src 改动**
  （`src/UI/Services/Workflow/WorkflowExecutionManager.cs`、`src/UI/Shell/Windows/MainWindowViewModel.cs`、
  `src/Workflow/WorkflowExecutionEngine.cs`，08:06–08:09 修改）⇒ 载荷 ≈ **当前工作区代码状态**，而非纯 `ba2b3ab9`。

## 工作流运行引擎架构优化（实施中）

- 审计与实施设计：`docs/05-框架架构/2026/09/16/工作流执行架构审计-20260916.md`、
  `docs/01-方案设计/2026/09/16/工作流运行引擎全量重构设计-20260916.md`。
- 当前状态：M1 已实施；M2 流程内依赖图与解决方案级全局变量边已实施；M3 已有执行画像、资源租约与层内受控并发，待去除拓扑层屏障；M5 已有生产者/消费者模型、跨流程依赖图和 RunAll 的批次变量快照，待补输入来源与模块结果 UI。
- 最新实现：主界面“全部流程运行”已接入 `WorkflowExecutionManager.RunSolutionAsync`，不再使用标签页中的模拟延时；生产节点结果只更新当前批次快照，不回写设计期全局变量值。
- 已执行验证：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 成功（0 error，已有 742 warnings）；`SolutionExecutionDependencyGraph` 测试 2/2 通过。
- 当前基线提交：`ba2b3ab9 feat(workflow): 运行请求、依赖图、资源租约与工具执行画像`。
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

## 相机架构实施工作包（2026-09-18）

- 后续整改实施基线：`docs/01-方案设计/2026/09/相机采集架构整改实施计划-20260919.md`。
- 整改顺序固定为：Runtime 会话与真实连接 → 管理器状态与持久化 → MVS 真机能力 → 主题完善 → 工作流采集接入 → 全量验收。
- 已确认不恢复旧 `ICameraService`/`CameraPoolManager` 兼容 Facade；旧实现由 Git 历史保留。

- 当前执行者：Codex；同一 Worktree 暂不允许其他 AI 并行写入。
- 设计基线：`docs/01-方案设计/2026/09/相机采集架构与设备管理最终方案-20260918.md`。
- 已完成 M0 首批落地：
  - 新增 `src/Plugin.SDK/Devices/Camera/` 相机无关契约：设备引用、设备描述、帧、采集请求、参数快照、能力、状态、错误码、采集服务和执行期服务入口；
  - 新增 `tests/SunEyeVision.Plugin.SDK.Tests/CameraContractTests.cs`；
  - 新增 `src/DeviceDriver/Cameras/Contracts/` 适配器、发现器、参数 Schema、会话和运行时管理器骨架；
  - 方案文档已补充旧契约迁移、自动发现、预览、主题和测试策略。
- 当前验证：`src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj` Release 独立编译成功；测试/DeviceDriver 构建受现有 HALCON 部署规则和 `build_temp` 文件锁影响，需在清理构建进程后复验。
- 已决定并执行：不保留 `ICameraService`/`CameraPoolManager` 兼容 Facade；旧相机契约、旧发现服务和旧海康包装已从当前源码删除，历史实现由 Git 版本管理保存。
- 已完成 M1 海康接入首版：
  - `src/DeviceDriver/Cameras/Hikvision/Mvs/` 接入 MVS 官方 `MvCameraControl.Net.dll`；
  - 支持 MVS GigE/USB3 自动发现、统一设备描述、会话连接、单帧采集、连续采集循环、曝光/增益/触发模式参数读写；
  - SDK 路径通过 `MVS_SDK_ROOT`/`MvsSdkRoot` 配置，本机默认识别 `D:\Program Files\MVS\Development`，不提交厂商二进制；
  - 针对 DeviceDriver 的源码编译已通过（随后被现有 HALCON 部署目标拦截，17 个既有 nullable 警告，无新增 C# 错误）。
- UI 已切换到 `CameraRuntimeComposition` 和新发现结果投影，不再依赖 Core 旧发现服务。
- UI 已开始切换：`SunEyeVision.UI` 引用 DeviceDriver；“添加相机”改用 `CameraRuntimeComposition.Discovery` 自动发现，隐藏 GigE/USB/IP 预选；相机管理器不再显示示例设备；管理器和添加窗口根背景、面板、边框、状态栏改用主题资源。
- 已修正运行时会话生命周期：`CameraSession.DisposeAsync` 会通知 `CameraRuntimeManager` 释放占用登记，补充了直接释放后重新打开的契约测试；相机 UI 模型统一改用 SDK `ObservableObject`/`ViewModelBase` 的 `SetProperty`。
- 设备详情的连接/断开/保存参数已切换到 `CameraRuntimeManager`，参数值按 `CameraParameterSchema` 类型转换后调用适配器写入；未连接时参数页使用临时诊断会话，连接后复用当前会话。
- 已完成独立实时预览首版：`CameraPreviewWindow/CameraPreviewHost` 使用 `CameraAccessMode.Preview`，复用 SDK `ImageControl`，关闭图像源下拉，仅显示实时帧；通用 `IVisionImage` 也可通过 UI 显示桥转换为 `BitmapSource`。
- 已补充详情切换设备时释放旧诊断会话，避免参数加载与连接状态互相占用。
- 已补充参数 Schema 类型化编辑首版：Boolean 使用复选框，Enumeration 使用下拉框，其余类型保留文本输入，并按 `IsReadOnly/IsAvailable` 控制编辑权限。
- 当前未完成：Rectangle/Command/Custom 的专用编辑器、MVS 真机人工采集验收；UI 全量构建仍受当前工作区多个构建进程/输出文件锁影响。
