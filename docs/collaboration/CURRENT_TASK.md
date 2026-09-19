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
- 2026-09-19 区域/卡尺绘制前置校验：ROI 编辑器和测量卡尺编辑器统一由管理器检查主 `ImageControl.SourceImage`；无图像时阻止进入绘制状态、清除工具选中并弹窗提示“请先加载或选择一张图像”，有图像时保持原有交互。Plugin.SDK Release 构建 0 error（历史警告未变）。
- 2026-09-19 卡尺尺度统一：卡尺带宽/长继续使用图像坐标参与算法与几何计算；方向三角形改为固定屏幕尺寸（约 10px 高、5px 半宽），通过 `InvScale(zoom, ...)` 映射到图像坐标，且交互层与结果层复用 `BuildLineDirectionArrowTriangles`/径向箭头生成逻辑。箭头尖端落在卡尺外边缘（`bandWidth / 2`），避免按带宽比例计算造成不同分辨率或缩放下大小漂移。全量 `SunEyeVision.slnx` Release 构建 0 error。
- 2026-09-19 卡尺初始尺寸自适应：新建卡尺完成时，以当前图像短边相对 640px 的比例初始化卡尺宽度/长度（带上下限）；参数和手柄调整后仍由工具参数接管，刷新图像或视图不会覆盖用户值；重新新建卡尺时才重新计算初始尺寸。直线查找和圆查找共用 SDK 管理器入口。
- 2026-09-19 工作流核心架构审视：已形成 `docs/05-框架架构/2026/09/19/工作流与执行引擎架构审视-20260919.md`。确认工作流定义存在 UI、WorkflowEngine、Solution 多份事实源；工作流 2 日志证明节点只进入 UI，未进入引擎/方案。后续必须先统一 WorkflowDefinitionStore、定义服务和运行快照边界，再处理局部执行异常和并行优化。
- 2026-09-19 架构实施第一步：新增 MainWindowViewModel.AttachTabToWorkflowDefinition。新建/切换工作流时将 Tab 节点和连接集合绑定到 WorkflowEngine 同一实例；若 UI 临时集合已有节点/连接则在首次绑定时迁移，并记录共享引用诊断。UI Release 构建 0 error，下一步继续把节点/连接命令收敛到定义服务并补一致性测试。
- 2026-09-19 架构实施第二步：新建方案时不再 `new Workflow` 复制 Tab 节点，而是复用 WorkflowEngine 定义；保存方案时发现同 ID 旧 Solution 实例会替换为 Engine 定义，Engine 未注册则直接报告定义生命周期错误；UI Release 构建 0 error。
- 2026-09-19 架构实施第三步：新增 `WorkflowDefinitionService`，提供工作流定义注册、节点/连接增删和一致性校验入口；连接创建前验证 Tab 与 Engine 共享集合，移除重复的引擎连接同步。UI Release 构建 0 error。下一步接管节点创建/删除命令，并将执行入口改为定义校验后生成运行快照。
- 2026-09-19 架构实施第四步：节点添加/删除入口增加 `WorkflowDefinitionService` 定义查找和 Tab/Engine 共享集合校验；删除不再重复操作引擎集合，命令集合即定义集合。UI Release 构建 0 error。下一步把执行入口接入定义校验与运行快照编译前置。
- 2026-09-19 架构实施第五步：新增 `WorkflowDefinitionValidator`，执行引擎在 `BuildExecutionPlan` 前校验空/重复节点 ID、连接引用、参数动态绑定来源、子程序引用和循环依赖；校验失败不会生成运行快照。UI Release 构建 0 error。下一步补运行快照契约测试，并将执行管理器的 UI/Engine 定义一致性纳入启动前检查。
- 2026-09-19 架构实施第六步：在现有 UI 测试项目加入 `WorkflowDefinitionValidatorTests`，覆盖有效定义、未知连接端点、未知绑定源、缺失子程序四项不变量；筛选测试 4/4 通过。临时独立测试项目因 NuGet 网络不可用已移除，避免污染解决方案。
- 2026-09-19 架构实施第七步：执行管理器在正式进入引擎前增加 UI Tab、WorkflowEngine、Solution 三份定义的一致性校验；根工作流要求三者引用同一实例及节点/连接集合，子程序下钻仅校验根定义，避免把子程序画布投影误判为根定义分裂。校验失败直接生成失败结果并完成快照，不进入执行器。UI Release 构建 0 error（752 个既有警告）；下一步补运行时一致性失败的自动化测试，并继续清理剩余 UI/Engine 双向同步入口。
- 2026-09-19 架构实施第八步：`WorkflowDefinitionService` 增加 `GetOrCreate`，主流程切换、新建流程和子程序创建统一经定义服务注册；方案加载也改为经服务注册，UI 不再直接调用 `WorkflowEngine.CreateWorkflow/RegisterWorkflow`。UI Release 构建 0 error（既有警告）；测试命令受并行构建进程影响未产生新的可读汇总，之前的定义校验 4/4 通过仍有效。
- 2026-09-19 架构实施第九步：定义服务增加 Solution 工作流挂接/解除入口，并替换新建方案、保存、删除、子程序同步等 UI 直接修改 `solution.Workflows` 的路径；同 ID 方案工作流会被统一替换为 Engine 定义实例。UI Release 构建 0 error（999 个既有警告）。
- 2026-09-19 验证策略调整：开发和日常回归默认使用 Debug 构建/测试；Release 构建仅在打包、发布或最终发布验收前执行，避免开发阶段反复生成发布载荷。
- 2026-09-19 Debug 验证：`dotnet test tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -c Debug --no-build --filter WorkflowDefinitionValidatorTests` 通过 4/4；后续开发验证以 Debug 为准。
- 2026-09-19 版本体系实施：产品、SDK 包、工具文件版本统一使用 `2.0.0.1`；`AssemblyVersion` 迁移到 `2.0.0.0`；新增 `ToolContractVersionAttribute`，工具构建自动写入契约 `2.0`，宿主按显式契约元数据并按主/次版本规则校验，不再从 SDK AssemblyVersion 猜契约。SDK Debug 构建、阈值工具 Debug 构建均 0 error；契约测试 Debug 7/7 通过。Release 尚未执行，按约定留到打包发布前。
- 2026-09-19 大规模区域渲染方案已落盘：`docs/01-方案设计/2026/09/19/大规模区域批量渲染优化方案-20260919.md`。方案保持图像层/结果层/交互层分离，保留完整 HRegion 和 OK/NG 判定，结果层按状态和样式批量渲染，不更换 Halcon 控件。实施第一步已完成：Blob 结果显示暂时限制轻量 WPF 轮廓/矩形/十字/文本为 5000 个，算法结果和 BlobRegions 不截断，并记录显示限制日志；下一步实现按 OK/NG 和图像 Tile 的批量 StreamGeometry 渲染器。
- 2026-09-19 大结果保护补强：`ResultRenderLayer` 对轮廓绘制增加最多 5000 个轮廓的 LOD 采样，对文本布局增加 2000 条上限；Plugin.SDK Debug 构建 0 error。当前仍属于过渡保护，正式批量 HRegion/Tile 渲染器尚未完成。
- 2026-09-19 批量轮廓第一步：SDK 新增 `BatchedContourGeometry` 和 `DisplayCollector.AddContourBatch`，Blob OK/NG 轮廓按批次写入结果层，结果渲染器可将批次内轮廓合并到同一批量 `StreamGeometry`；SDK 与 Blob Debug 构建均 0 error。当前仍保留 5000 个轻量显示预算，后续继续接入完整 HRegion 分批和异步 Tile 缓存。
- 2026-09-19 显示预算 UI 化：Blob 参数新增 `MaxDisplayBlobCount`，默认 5000、范围 100~100000，结果显示页可直接修改并持久化；该值只影响轻量 WPF 图形展开，不影响完整算法结果和区域绑定。Blob Debug 构建 0 error。
- 2026-09-19 OK/NG 区域结果拆分：`BlobAnalysisResults` 新增 `OkBlobRegions`/`NgBlobRegions`；Blob 执行阶段按最终 `JudgmentPassed` 将区域分别聚合，保留完整 `BlobRegions` 作为总集合，供后续批量结果渲染器直接消费。Blob Debug 构建 0 error。
- 2026-09-19 HalAdapter 批量入口统一：`DisplayCollectorHalconExtensions.AddContours(HRegion, ...)` 现在先提取 OK/NG 轮廓列表，再通过 `AddContourBatch` 写入结果层，不再逐区域调用 `AddContour`；HalAdapter Debug 构建 0 error。
- 2026-09-19 批量几何异步缓存接入：`ResultRenderLayer` 对批量轮廓在后台构建冻结的 `StreamGeometry`，绘制时优先直接复用缓存；加入构建版本号避免旧结果异步回写新结果，并覆盖 collector、区域项和兼容项入口。SDK、HalAdapter、Blob 工具 Debug 构建均 0 error（SDK 既有 XML/空引用警告仍存在）。
- 2026-09-19 全量 Debug 自检：`dotnet build SunEyeVision.slnx -c Debug --no-restore` 完成各核心项目和测试项目输出，未发现新增编译错误；Release 仍按版本策略留到发布前。
- 2026-09-19 批次分块：批量轮廓不再生成单个超大 `StreamGeometry`，结果层按每 1000 个轮廓构建多个冻结几何块并逐块绘制；SDK Debug 构建 0 error。该阶段按批次分块，图像空间 Tile 裁剪仍作为后续优化。
- 2026-09-19 视口裁剪：批量几何块绘制前使用冻结几何的 `Bounds` 与当前图像视口相交判断，完全位于视口外的块直接跳过；平移/缩放大图时不再绘制不可见批次。SDK Debug 构建 0 error。
- 2026-09-19 批量构建取消：结果层切换 collector、区域项、兼容项或清空时主动取消旧批量几何构建；后台任务在批次间检查取消状态，避免过期大结果继续占用 CPU；SDK Debug 构建 0 error。
- 2026-09-19 图像空间 Tile 索引：批量几何块按 512×512 图像坐标归入 Tile；有视口时只查询相交 Tile，并以 `HashSet` 去重跨 Tile 几何，避免遍历全部几何块。无视口时仍完整绘制所有块。SDK Debug 构建 0 error。
- 2026-09-19 Blob 大区域执行路径优化：移除每个 Blob 的 `Clone + ConcatObj` 区域聚合；循环中只记录通过筛选的对象索引，最后对 `shapeFilteredRegions` 分别一次性 `SelectObj` 生成总区域、OK 区域、NG 区域，避免数万区域时 O(n²) 句柄复制。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 Blob 特征批量计算：面积、质心、包围盒、矩形度、圆度、周长改为对 `shapeFilteredRegions` 一次调用 Halcon 算子，循环只读取结果元组；仅对显示预算内的 Blob 单独提取轮廓。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 显示数量入口调整：`MaxDisplayBlobCount` 默认改为 3000，名称改为“最大显示区域数”，从“结果显示”页移到 Blob 工具“基本参数”的“运行参数”组；该值继续只控制轻量 WPF 图形展开。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 查找数量语义修正：按要求移除“最大显示数量”概念，新增默认启用的 `IsBlobCountFilterEnabled` 和 `BlobCountLimit=3000`，入口移动到“Blob筛选→查找数量筛选”，无说明文字；执行流程先排序再截取数量，之后 `FilteredBlobs`、BlobRegions、OK/NG 区域、轮廓、矩形、质心和文本全部使用同一批 Blob，避免只筛轮廓造成结果不一致。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 批量渲染取消源清理修复：`ResultRenderLayer` 的后台构建完成回调在释放 `CancellationTokenSource` 前清空字段，`CancelBatchBuild` 改为先摘除引用并捕获重复释放竞态；关闭调试窗口和清理结果不再抛 `ObjectDisposedException`。同时新增“轮廓提取”阶段耗时记录，用于分析 100 Blob 约 300ms 的真实构成。SDK、Blob Debug 构建均 0 error。
- 2026-09-19 Blob 显示与筛选一致性：质心、轮廓默认 `IsVisible=false`；Blob 外接矩形改为 Halcon `SmallestRectangle2` 的最小外接旋转矩形；面积筛选在 C# 结果链再次显式校验，确保 UI 参数确实参与保留集合。显示项遵循样式可见性。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 Blob 输出开关：新增 `OutputBlobImage`、`OutputBinaryImage`、`OutputContours` 运行参数；二值图和灰度图按开关生成，OverlayImage 不再无条件生成，轮廓提取和轮廓图元也由轮廓输出开关控制。Blob筛选页增加“输出”区域，Blob图像默认开、二值化图像和轮廓默认关。BlobAnalysis Debug 构建 0 error。
