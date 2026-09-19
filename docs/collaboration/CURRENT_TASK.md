# 当前协作任务

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：Codex。
- 当前任务：工作流运行引擎架构优化（M3 全局就绪队列、M4 执行快照 UI、M5 全局变量数据总线与绑定 UI）。
- 修改权限：`src/Workflow/`、`src/Plugin.SDK/`、`src/UI/Services/Workflow/`、`src/UI/Shell/Windows/`、`src/UI/Features/GlobalVariableManager/`、`src/UI/Features/ParameterSetting/`、`tests/`、`docs/`。
- CodeBuddy 并行授权目录（2026-09-18 起，"发布与版本管理"线；与上表**不重叠**，rule-021 §3 的 `installer/**` 禁区在本次授权范围内）：
  `installer/`、`src/UI/Icons/`、`.codebuddy/rules/`、`.codebuddy/skills/`、`version.json`、`CHANGELOG.md`。
  方案见 `docs/01-方案设计/2026/09/18/内测版发布与版本管理最终方案-20260918-112404.md`。
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
- 当前状态：M1 已实施；M2 流程内依赖图与解决方案级全局变量边已实施；M3 已使用全局就绪队列、执行画像与资源租约；M4 已接入状态栏“执行快照”与逐节点实时刷新；M5 已有生产者/消费者模型、跨流程依赖图、RunAll 批次变量快照；生产者唯一在模块结果绑定，变量管理器只读显示输入来源，目标参数管理器已主题化并改为二态复选框交互；模块结果通过 `IsBindable`/完整结果路径统一支持普通结果、ROI 图形和图形字段绑定。B2 目标树全集逻辑、B5 第一批工作流作用域消费者键、D1-D3 SDK 参数契约和校验器已实施；D4-D5 工具契约迁移、子程序实例路径和后续模型迁移待实施。
- D1-D3 验证：`ParameterContractValidationTests` 3/3 通过；`dotnet build src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj -c Release --no-restore` 与 `dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 均 0 error。架构方向已修正为：契约和算法参数只使用纯 CLR 值；当前 `ParamValue<T>` 解包仅用于迁移施工，后续必须删除，不保留旧工具兼容层。
- D4-D5 第一批：Blob 分析参数实现 `IInputSlotContract`/`IRuntimeParameterContract`，登记检测区、屏蔽区和主要运行参数；`BlobAnalysisParameterContractTests` 1/1 通过，工具 Release 构建 0 error。其他工具仍按批次迁移，暂不启用全局严格完整性校验。
- D4-D5 第二批：图像二值化参数实现同一组契约，登记检测区、屏蔽区、固定阈值、自适应阈值和 InRange 参数；工具 Release 构建 0 error。
- D4-D5 第三批：图像滤波和形态学参数实现同一组契约，登记检测区、屏蔽区及核心滤波/形态学参数；两个工具 Release 构建均 0 error。
- 参数模型拆分第一步已实施：新增 `ParameterBindingDefinition`、`ParameterBindingSet`、`RuntimeParameterSnapshot` 和 `RuntimeParameterSnapshotResolver`；`ToolExecutionRequest` 支持绑定定义与运行快照，`ToolExecutor.ResolveRuntimeParameters` 生成快照且不修改设计参数。SDK 快照测试 3/3 通过。Blob 分析、图像二值化、图像滤波、形态学处理四类工具已完成纯值化，下一步移除解析器对 `ParamValue<T>` 的迁移依赖。
- 绑定生产端第一步已实施：全局变量管理器现在同时把普通 CLR 参数的绑定写入节点 `ParamSettings`；清除绑定时同步移除外部定义，旧 `ParamValue<T>` 内部配置仍保留作过渡。UI Release 构建 0 error。下一步把 `AlgorithmNode` 的执行请求接入节点 `ParamSettings` 快照，并让运行参数从快照注入运行副本。
- 执行接入已实施：`AlgorithmNode` 从节点 `ParamSettings` 构造 `ParameterBindingSet`，创建参数运行副本；`ToolExecutor` 解析目标属性类型、生成快照并将值注入运行副本，设计期参数不被覆盖。SDK 测试 443/443 通过，UI Release 构建 0 error。下一步验证全局变量到纯 CLR 参数的端到端执行，再清理旧解析路径。
- 端到端验收方案已记录：`docs/01-方案设计/2026/09/18/全局变量运行快照端到端验证方案-20260918.md`，CodeBuddy 可按 E2E-01 至 E2E-08 执行并回填证据。
- 调试参数持久化桥接已补齐：`ToolDebugControlBase` 统一监听 `ConfigSetting.BindingConfigChanged`，将纯 CLR 参数绑定写入当前节点 `ParamSettings`，并在调试控件加载/切换节点时恢复绑定状态；不再只依赖 `ParamValue<T>` 内部配置。UI 验证构建 0 error。
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

- 2026-09-18 最新进度：全工具纯 CLR 参数迁移完成。新增/迁移点圆、点线、点点测量参数（点数组、坐标、卡尺与枚举均为直接 CLR 属性），同步改造三类工具执行逻辑；前一批 Blob、阈值、滤波、形态学、形状匹配、线/圆测量也已完成。工具源码扫描不再发现 `ParamValue<T>` 参数属性或 `BindingConfig` 依赖。
- 运行链已停止从参数对象内部应用旧绑定：`AlgorithmNode` 与 `ToolExecutor` 仅通过节点 `ParamSettings` 生成 `RuntimeParameterSnapshot` 并注入执行副本；全局变量管理器与目标参数筛选也仅操作纯 CLR 属性及 `ParamSettings`。
- 调试控件基类已统一监听 `ConfigSetting` 与 `ParamBinding`，普通标量和图形订阅均写入/恢复节点 `ParamSettings`，用于关闭调试窗口后持久化绑定。
- 验证：全部实际工具项目 Release 编译通过（FixEncoding 工具因工作区未还原而跳过）；Plugin.SDK/UI 自定义输出 Release 编译均 0 error。现有警告为历史 nullable/XML/隐藏成员警告，未新增错误。
- 下一步：清理 SDK/UI 中仅用于旧 `ParamValue<T>` 的死代码与注释，跑 SDK 测试及全量 Release 构建，再交给用户统一手动验证。

- 2026-09-18 收尾：移除 `ToolExecutionRequest.ApplyBindings` 及 `ToolExecutor` 的旧执行开关，执行 API 统一只使用 `ParameterBindingSet`/`RuntimeParameterSnapshot`；迁移测试改为纯 CLR 参数往返契约。
- Release 验证：Plugin.SDK 测试 443/443；点圆、点线、点点、线圆、线线测量测试全部通过；SDK、UI、全量 `SunEyeVision.slnx` 构建均 0 error。工具目录不再包含 `ParamValue<T>` 参数声明或 `BindingConfig` 执行依赖。
- 当前状态：代码实现和自动化验证完成，等待用户统一进行端到端手动验证（绑定持久化、全局变量传递、工具调试执行、工作流执行、结果显示）。
- 2026-09-18 位置修正回归修复：运行快照解析现在只接收工具参数类型中真实存在的可写属性；调试控件的“位置修正”等展示绑定不会再被当作普通参数做 `Convert.ChangeType`，继续由 `AvailableDataSource/ImageSourceReference` 专用链路处理。Plugin.SDK 针对性 Release 构建 0 error。
- 2026-09-18 绑定重开修复：`ToolDebugControlBase` 在派生控件完成 `Parameters` 注入后通过 Dispatcher 再恢复一次 `ParamSettings` 绑定，并在窗口卸载前做最终持久化；全局变量选择表增加深色/浅色主题下的行、选中行、单元格颜色适配。
- 2026-09-18 绑定持久化根因修复：`ParamSettingContainer.Settings` 原先只有只读 `IEnumerable`，System.Text.Json 能写出设置但不能把设置回填到私有字典；现改为可读写 `List<ParamSetting>`，setter 统一重建字典，并放宽旧字典反序列化的枚举读取，确保调试窗口/方案重开后仍保留绑定。
- 2026-09-18 关闭时序补强：`ToolDebugControlBase` 增加公开 `PersistParameterBindings()`，标准调试窗口在 `Closing`、自定义窗口在 `Closed` 显式提交绑定；恢复阶段若模板控件尚未生成则通过 Dispatcher 重试。UI Release 构建 0 error。
- 2026-09-18 绑定问题诊断埋点：新增 `BindingDiag` 日志，记录绑定选择、当前节点、ParamSettings 设置数、控件绑定状态、关闭前后提交及重开恢复结果；用于区分“未写入、写入后丢失、恢复未命中控件”三类问题。UI Release 构建 0 error。
- 2026-09-18 发布运行修复：PointLine 插件目录残留 `SunEyeVision.HalAdapter 1.0.1`，与宿主 `1.0.0` 不一致，导致图像载入节点加载失败并引发就绪队列无可派发节点。已重建 PointLine 插件，确认其 deps 改为 `SunEyeVision.HalAdapter 1.0.0`，并清理插件目录内旧 HalAdapter 副本。
- 2026-09-18 Debug 绑定恢复时序修复：根据 `logs/2026/09/18/SunEyeVision_165750.txt` 确认绑定已写入 `ParamSettings`，但重开窗口时恢复发生在 `ConfigSetting` 模板生成前，关闭时把临时常量状态反向清除了绑定。`ToolDebugControlBase` 现使用 DispatcherTimer 以 100ms 间隔有限重试（最多 12 次），直到标量/图形绑定控件出现后再恢复；Debug UI 构建 0 error（仅历史警告）。
- 2026-09-18 绑定恢复架构调整：移除定时轮询作为主路径；`ConfigSetting` 在 `Loaded` 后通过 `InstanceLoaded` 通知，`ToolDebugControlBase` 仅对属于当前调试控件的实例注册并触发恢复。Tab 延迟创建由控件就绪事件自然驱动，未生成控件不会覆盖已有 `ParamSettings`。Debug UI 构建 0 error（仅历史警告）。
- 2026-09-18 全局变量显示名修复：恢复绑定时对 `__GlobalVars__` 统一投影为 `全局变量.变量名`；持久化 `ConfigSetting.ToParamSetting()` 时即使数据源树尚未建立也写入语义显示名，避免关闭重开后显示内部节点 ID。当前 Debug DLL 被运行中的 `SunEyeVision.UI` 占用，需关闭程序后重新构建 UI 验证。
- 2026-09-18 工具立即执行全局变量修复：`ToolDebugControlBase.CreateLiveExecutionRequest` 现在携带当前节点 `ParamSettings`，并从 `DataSourceQueryService` 注入方案级全局变量与 `__GlobalVars__` 合成结果；工具调试执行与工作流执行使用同一绑定快照解析链，不再要求先运行工作流。Plugin.SDK Debug 隔离输出构建 0 error；当前 UI 进程占用正式 Debug 输出，需关闭后由 CodeBuddy 重建 UI。
- 2026-09-18 调试全局变量缓存修复：`DataSourceQueryService` 按工作流缓存时，原先只在首次创建时复制全局变量，导致服务先创建后变量才可用时立即执行缺少 `__GlobalVars__`。`GetDataProviderForNode` 现在每次获取节点服务都刷新方案级变量快照。UI 隔离构建因未同步各项目引用输出无法完成，需关闭运行中的 Debug 程序后执行正常 Debug 构建验证。
- 2026-09-18 方案保存同步修复：发现“进程内重开正常、保存方案重启后绑定消失”还可能来自 UI 节点与引擎/方案节点实例不一致。`SaveSolutionData` 现在按稳定节点 ID 将 UI 节点 `ParamSettings` 显式同步到引擎节点，并在最终序列化集合中再次对齐，记录保存前绑定数量。
- 2026-09-19 SSOT 诊断埋点：`MainWindowViewModel` 在打开调试窗口和方案保存前记录 UI/Engine/Solution 节点对象身份、`ParamSettings` 对象身份、绑定数量及 `ReferenceEquals` 结果（日志分类 `SSOT-Diag`），用于区分实际对象分裂与纯序列化/加载问题。
- 2026-09-19 根因确认与修复：日志证明 UI/Engine/Solution 始终是同一节点对象，保存前 `ParamSettings count=2`，保存文件也包含两个绑定；重启加载后在 `WorkflowNode.FromBase` 类型转换阶段变为 `count=0`。原因是转换复制了 `Parameters` 等字段但漏复制 `ParamSettings`。已补充 `ParamSettings = baseNode.ParamSettings`，修复方案重启后绑定丢失。
- 2026-09-19 Blob 阈值语义修复：检查 `BlobAnalysisTool.Run` 确认 Halcon 实际执行的是 `Threshold(processingImage, thresholdMin, thresholdMax)`，原实现却把 `(min+max)/2` 写成“实际使用的阈值”，导致 30–100 显示 65。已移除中点计算，结果改为 `ThresholdMinUsed`/`ThresholdMaxUsed`，分析 UI 显示“阈值范围：30–100”，结果契约同步使用“实际阈值下限/上限”。
- 2026-09-19 Blob Run 逻辑修复：增加阈值下限不得大于上限的参数校验；修复 BinaryInv/形态学处理对 `using` 变量重新赋值造成的 Halcon 区域错误释放风险；`BlobRegions` 改为与最终 `FilteredBlobs` 相同的 Tab3 筛选集合，保留 Tab4 NG Blob 供颜色判断，避免图形区域和数值结果不一致；更正 `GrayImage` 语义为原始灰度输入。Blob 工具 Release 构建 0 error；Blob 参数契约测试 2/2 通过。
