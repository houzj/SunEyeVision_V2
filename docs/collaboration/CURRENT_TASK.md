# 当前协作任务

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：Codex。
- 当前任务：通讯管理器协议驱动完整接入（TCP Client/Server、UDP 已实现；继续接入 Serial、Modbus TCP/RTU、EtherNet/IP CIP TCP、Siemens S7 TCP、OPC UA Client，并完成协议专属操作 UI、测试及阶段验收）。
- 本任务修改权限：`src/Workflow/`、`src/UI/Features/CommunicationManager/`、通讯相关 `tests/`、`docs/` 及为协议驱动所需的通讯专属项目/依赖配置；第三方依赖必须先核对许可、目标框架、原生部署及离线构建可用性，不得将 UI 配置存在误报为驱动已接入。
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

## 通讯能力设计任务（2026-09-21）

- 2026-09-23 用户授权开始按最终方案接入真实协议实现。实施从真实 TCP Client 运行闭环开始：本机异步运行时、连接测试、连接/断开、UTF-8 发送、接收数据回显到调试区；TCP Server/UDP/Serial/Modbus/CIP/S7/OPC UA 尚未接入，未支持协议必须返回明确错误而非伪成功。工业协议 SDK/许可评估与验证按阶段进行。
- 当前进行中实现：`src/Workflow/CommunicationRuntimeManager.cs` 提供 TCP 客户端会话生命周期；通讯管理 UI 的按钮和收发框已绑定该运行时，异常断线会更新状态，删除/关闭会释放连接。验证：用户授权后执行 `dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -p:OutputPath=../../dev/Debug/`，成功（0 error，831 warnings）；未启动程序，待用户手工验收。
- 用户手工验收反馈：TCP 客户端配合调试助手，发送与接收交互正常（2026-09-23）。后续实施转入 TCP Server 与 UDP。
- TCP Server/UDP 代码增量已接入同一运行时：Server 执行真实本机 bind/listen，接收多个连接并向最近活跃客户端发送；UDP 真实 bind/receive 并向配置目标端点发送。用户再次授权构建后，执行相同 Debug 构建成功（0 error，831 warnings）；未启动程序，待用户手工验收。
- UI 交互修订：TCP Server 的单一状态切换按钮显示“开始监听/停止监听”；TCP Client 显示“连接/断开”；UDP 显示“绑定端口/解除绑定”。已移除重复的连接、断开双按钮，测试连接仍单独保留。此交互修订尚未重新构建。
- 2026-09-23 用户进一步确认设备列表交互：改为垂直选项卡式设备项，标题序号/名称，副标题显示协议类型与状态，圆形状态灯呈现连接状态；每个设备项使用 SDK ToggleSwitch 执行对应协议启停，未接入驱动的设备禁用该开关。右侧保留参数、调试区和测试连接，不再放独立启停按钮。本轮 XAML/VM 改动尚未构建。
- 已按该反馈替换 DataGrid 为主题化 ListBox 选项卡项；副标题绑定协议+状态，圆形指示灯区分连接中/已连接/故障/断开，SDK ToggleSwitch 按设备直接启停，未接入的协议开关禁用。当前二进制尚未包含此改动，等待用户要求后再构建。
- 2026-09-23 右键菜单样式修复：通讯设备菜单不再使用局部不完整的 ContextMenu/MenuItem 样式，改复用应用全局主题菜单模板，并设置合理最小宽度；UI Release 构建成功（0 error，1117 warnings）。本轮未启动 UI，待用户实际检查不同主题下菜单背景、文字间距与边缘显示。
- 本轮基线提交：`8a66d177884bdb038b6082b1b36c2041464e6237`。

- 2026-09-23 最终范围修订：基于用户逐协议截图与确认，新增完整架构基线 `docs/01-方案设计/2026/09/通讯管理器运行时架构与协议扩展最终设计方案-20260923.md`。已明确 OPC UA 仅客户端；当前纳入 TCP Client/Server、UDP、Serial、Modbus TCP/Serial、EtherNet/IP CIP TCP、Siemens S7 TCP；不纳入 HTTP 与 OPC UA Server。
- 新方案覆盖传输/工业协议/角色术语边界、运行时分层和契约、配置 schema/迁移、连接生命周期、安全凭据与证书、协议扩展流程、各协议参数基线、OPC UA Client 安全/身份/会话参数，以及完整主窗口/创建设备/OPC UA 编辑器 ASCII 线框、按协议差异化调试和分阶段验收。
- 用户截图确认 Modbus 的 TCP Client/串口切换，但尚未提供 Modbus 串口模式的详细参数截图；新方案将字段标为待确认，避免把建议字段误写为 VM 既定行为。此前的“当前仅做 UI、不实施通用通讯协议”阶段性说明不构成本次最终协议范围；本次目标是按新架构逐步实现真实驱动，尚未宣称驱动已接通。

- 已完成现状审计：`src/Workflow/Communication.cs` 和 `src/UI/Features/CommunicationManager/CommunicationManagerViewModel.cs` 仍为弱类型配置与 `Thread.Sleep(500)` 占位测试，尚未接入真实通讯运行时。
- 已形成设计基线：`docs/01-方案设计/2026/09/通讯管理器与通讯工具完整设计方案-20260921.md`。
- 方案覆盖配置 schema、运行时连接管理器、传输层/协议适配器、通讯工具、工作流快照集成、UI、持久化迁移、安全可靠性及 M0–M4 实施验收；UI 章节已补充参考界面收敛方案、设备/控制器分层、六类功能页面、协议创建对话框、测试/删除对话框、交互时序、验收清单及 ASCII 线框图。
- 历史阶段记录（已被 2026-09-23 的真实协议接入授权取代）：曾以 VisionMaster 通讯管理器 UI 为参考，仅做设备管理骨架；现按本节最新实现范围逐步接入通用通讯驱动。
- UI 反馈修订：移除设备管理页的“控制器列表”；协议下拉框只保留 TCP 客户端、TCP 服务端、UDP、串口、HTTP、Modbus TCP、Modbus RTU、OPC UA，厂商专用协议通过后续协议适配器扩展。
- UI 交互修正：保留参考 VM 的左侧功能导航和控制器列表布局；当前只实现设备管理页，控制器列表不实现业务逻辑；内容区顶部工具栏移除，设备新增入口保留在设备列表标题旁。
- 当前已开始设备管理 UI 实施：通信设备创建对话框和选中设备详情均按 TCP 客户端/服务端、UDP、Modbus TCP/RTU、串口、OPC UA、HTTP 显示差异化参数；开关统一使用 SDK `ToggleSwitch`。未选中设备时右侧隐藏所有参数控件并显示空状态提示；设备管理对话框文本和输入控件使用主题资源。接收结束符关闭时隐藏编辑框和快捷按钮，开启后显示可滚动多行编辑框及 `\r`、`\n`、`\r\n` 快捷输入，默认值为空，不再预填 `0D 0A`。Debug 构建已验证 0 error。
- 最新主题与布局修正：SDK ComboBox 当前值和 ComboBoxItem 下拉项显式继承主题 Foreground，并补充禁用项主题色；通信设备 DataGrid 的行、单元格、列文本统一使用主题资源，选中/失焦状态不再使用固定白字或系统默认失焦色；协议类型列显示协议名称而非枚举名；参数绑定属性补齐正确的属性变更通知。Debug 构建通过，0 error，产物位于 `dev/Debug/`，尚未做本轮运行时人工验收。
- 本轮布局修正：按当前阶段要求移除控制器列表及空占位，设备列表区域占满左侧；接收结束符改为“标签/SDK 开关同一行 + 自适应多行文本框 + 等宽三列快捷符号按钮”的结构。Debug 构建通过，0 error，尚未做本轮运行时人工验收。
- 调试数据区补齐：接收数据页增加带主题背景/边框的只读多行 TextBox，发送数据页增加带主题背景/边框的可编辑多行 TextBox 和禁用发送按钮；16 进制显示改用 SDK ToggleSwitch。Debug 构建通过，0 error。
- 接收/发送记录均保留独立历史框并带毫秒时间戳与 `RECV/SEND UTF-8` 标记；发送区使用紧凑主题化圆角组合输入器、空值提示、强调色发送按钮水平并排，单行约 50px、支持多行扩展，Enter 发送、Shift+Enter 换行，成功后清空。已删除“测试连接”按钮及对应命令/运行时方法。最新视觉修订待 Debug 构建及运行时人工验收。
- 设备解耦方案 `docs/01-方案设计/2026/09/通讯管理器按设备解耦与收发调试优化方案-20260923.md`：M1 每设备 Workspace 已实施，接收事件按设备 ID 路由；M3 收发格式已实施，RX/TX 保留原始字节与时间戳，接收文本/HEX 显示可独立切换，发送文本/HEX 输入可独立切换，HEX 发送经校验后走原始字节发送；RX 清空和 TX 历史清空分别绑定当前设备 Workspace。HEX 解析覆盖常见分隔符、0x 前缀、非法字符、奇数半字节和空输入。2026-09-23 UI Debug 构建成功（0 error，829 warnings）；待用户验收多设备切换、HEX 收发和独立清空。M2 设置草稿事务与 M4 界面全状态验收仍待实施。
- 2026-09-23 Tab 工具项布局修正：接收/发送 Tab 名称先并排显示，格式/清空操作区固定在“发送数据”右侧，并按 SelectedIndex 切换接收/发送各自控件；格式状态提示仅显示“文本”或“HEX”，徽标前景/输入背景/边框均使用动态主题资源；两个“清空”分别只作用于各自历史。自定义 TabControl 顶栏模板，内容区不增加额外工具栏行。UI Debug 构建成功（0 error，740 warnings），待运行时检查。
- 2026-09-23 文本接收可见性修正：调试助手发送 HEX `11` 时，该字节解码为 ASCII 控制码 DC1，UTF-8 文本模式正文不可见。现在纯不可显示/无效 UTF-8 数据会显示解释、原始 HEX 和“切换 HEX 查看”提示；可读文本与控制码混合时，保留文本并转义显示控制字符。针对性 UI 测试 4/4 通过；全量 Debug UI 由测试项目依赖构建通过，0 error。
- 2026-09-23 设备列表右键菜单：列表项右键先选中目标，提供主题化“重命名/删除”；重命名使用独立轻量对话框并修改目标设备名称，删除复用现有运行时断开和配置删除流程。
- 2026-09-23 TCP Server 多客户端发送语义确认：用户要求向所有已连接客户端发送。服务端现逐个向连接写入相同负载，并返回实际成功写入数，UI 反馈客户端数量；该应用层 fan-out 不是 UDP 广播包。新增两个 TCP 客户端集成测试，验证两者都收到数据。通讯相关测试 5/5 通过；UI Debug 构建 0 error（987 warnings），产物 `dev/Debug/SunEyeVision.UI.exe`，待用户手工验证右键菜单与调试助手多客户端收发。
- 2026-09-23 完整协议接入 M1：Workflow 已接入串口打开/收发（System.IO.Ports 9.0.0，MIT）及 Modbus TCP/RTU 主站（NModbus 3.0.81，MIT）；Modbus 类型化支持 FC1/2/3/4/5/6/15/16，禁止任意原始字节发送。UI 有串口、Modbus 参数和专属读写操作。Modbus TCP 本地模拟端覆盖 FC3 读、FC6/15/16 写；串口必填校验已测。**未完成验收**：串口/RTU 尚无真实 COM/RS-485 测试。
- 2026-09-23 完整协议接入 M2：引入 S7netplus 0.20.0（MIT）与 OPC Foundation UA Client 1.5.378.176；S7 TCP Client 支持 CPU 型号、rack/slot 及 DB/Marker/Input/Output 区域字节读写；原始字节发送会明确拒绝。CIP TCP 已实现 EtherNet/IP RegisterSession、SendRRData、逻辑路径显式消息及 UnregisterSession；本地 CIP 服务器模拟器验证会话注册、Get_Attribute_Single 请求路径、响应和注销，CIP 当前是通用显式消息入口，**不包含隐式 I/O、Scanner/Adapter 或厂商 Tag 浏览层**。
- 2026-09-23 完整协议接入 M3（按用户当前范围收敛）：OPC UA Client 支持匿名身份、按配置安全模式发现端点、证书校验（默认拒绝未知证书）、Browse/Read/Write/Subscribe/Unsubscribe；`Sign` 与 `SignAndEncrypt` 两种安全模式本地端到端测试通过，包含取消订阅后无后续通知。通讯面板有独立“订阅/取消订阅”，按 NodeId 移除监视项，移除最后一项后释放底层 Subscription；断开设备统一释放剩余项。测试曾揭示并修复写响应列表校验问题。安全模式 `None` 的同版本测试服务端在空客户端证书时自身抛 NullReference；未通过放宽客户端证书验证规避。**用户名/密码及用户证书身份本阶段不实现**：用户确认当前设备未配置凭据，按需再开发；遗留配置中若身份不是“匿名”，运行时明确拒绝，不尝试连接或读取凭据。S7 尚无真实 PLC/仿真器数据读写验收；Modbus RTU 和串口尚无真实设备验收。
- 当前验证记录：2026-09-23 UI Debug 构建通过；合并通讯专项测试 11/11 通过（OPC UA `Sign`/`SignAndEncrypt` 浏览、读、写、订阅、取消订阅；Modbus TCP FC3/6/15/16；CIP 回环模拟端；TCP Server 多客户端；设备工作区）；最新 UI Release 重建通过（0 错误，869 个既有警告）。Release 目录确认包含 `System.IO.Ports.dll`、`NModbus.dll`、`S7.Net.dll` 与 OPC UA Client 栈 DLL。未启动 UI，未验证 Visual Studio 设计时和运行时交互；仍需用户/设备人工验收。
- 2026-09-23 OPC UA 显式证书信任管理已实施：通讯面板可查看可信对端证书主题/指纹，选择 `.der/.cer/.crt/.pem` 后先校验有效期并展示主题、指纹和 UTC 到期时间，用户二次确认后才导入；支持逐张撤销。未知证书仍默认拒绝，导入/撤销只影响新连接，不会静默接受或强制断开现有会话。信任存储复用 OPC UA Directory Certificate Store 的 `UA Applications/certs`；自动测试覆盖 DER 导入、列表、撤销、过期拒绝，并由 OPC UA 本地 E2E 通过同一信任存储导入测试端点证书后完成两种安全模式浏览/读写/订阅/取消订阅。按用户决定，身份收敛为匿名，新增旧用户名身份配置的快速拒绝测试；合并通讯回归 14/14 通过，UI Release 构建 0 错误（869 个既有警告）。尚未做设计器视觉检查或运行时人工 UI 验收。
- 2026-09-23 依赖边界复核：System.IO.Ports 9.0.0 包元数据标记 MIT；NModbus 3.0.81 和 OPC Foundation UA 1.5.378.176 包内均带 MIT license；S7netplus 0.20.0 的 nuspec 未声明 license 字段，但对应上游仓库 License.txt 为 MIT（应在第三方声明/发布清单中显式收录）。当前 Release 输出实查含 System.IO.Ports.dll、NModbus.dll、S7.Net.dll、Opc.Ua.Client/Core/Configuration/Security.Certificates/Types 等运行时依赖。该核查仅覆盖通讯直接依赖和 UI Release 目录，不等于全量传递依赖 SBOM/许可审计。
- 2026-09-24 串口参数 UI 优化：新建设备页和已选设备详情页的普通串口、Modbus RTU 串口号均改为不可编辑 ComboBox，数据来自当前主机 `SerialPort.GetPortNames()`；已保存但暂时未枚举到的端口会保留在列表首项，避免旧配置无法显示。UI Release 构建 0 错误（758 个既有警告），通讯专项回归 14/14 通过；尚未启动程序做实际 COM 设备人工验收。
- 下一阶段仍在进行：OPC UA 当前只验收匿名身份，不再投入用户名/密码凭据实现，待用户明确需要时另立工作；对串口/RTU、S7、CIP 及 OPC UA 用兼容设备/仿真器验证；完成全量传递依赖许可/SBOM 和安装包部署审计。真实串口硬件、PLC/设备验收仍需要可用设备或兼容仿真器。HTTP 与 OPC UA Server 不在已确认范围。
- 历史首批工作包记录：Codex C0（运行契约冻结）与 CodeBuddy B0（测试/验收矩阵）；必须使用独立 Worktree，
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
- **结果渲染统一 WPF 方案基线（2026-09-21）**：已记录于
  `docs/01-方案设计/2026/09/21/统一WPF结果渲染与调试预览分层优化方案-20260921.md`。
  方案要求上层按 `Pass/Fail/Neutral/Highlight` 结果状态组织显示，几何类型仅作为渲染器内部数据；
  工作流结果与调试预览分层，关闭调试窗口只清理调试预览/交互层，不误清理工作流结果；
  统一走 WPF 批量 Geometry 与少量 Visual，不按小结果/大结果切换多套公共渲染模式。
  当前由 CodeBuddy 按该方案实施；完成后由 Codex 审核代码、生命周期语义、性能回归、构建和测试。
- **渲染实施设计决策（2026-09-21）**：
  1. 本次明确授权 CodeBuddy 修改 `src/Plugin.SDK/**`、`tools/SunEyeVision.BlobAnalysis/**`、
     `src/UI/Services/Workflow/**`、`src/UI/Shell/Windows/**`；仅限本次统一 WPF 结果渲染任务，
     不修改 `src/Workflow/**` 执行核心；同一时段 CodeBuddy 为唯一代码写入者，Codex 只读审核。
  2. 调试窗口立即执行结果**不写入生产工作流的 `WorkflowContext`**，避免污染工作流结果、数据源和下游执行；
     如调试数据分析或调试链路需要缓存，使用独立的 DebugExecutionContext/调试结果缓存。
  3. 关闭调试窗口后**自动恢复已缓存的工作流结果**，但不重新执行算法；清除 DebugPreview/交互层后，
     从生产工作流结果缓存重新提交 WorkflowResult。该结论取代“重新选中节点后才恢复”的临时建议。
- **区域渲染最终决策（2026-09-21）**：本轮**不实施** CodeBuddy 提出的甲/乙/丙区域栅格化方案。
  工作流结果区域继续纳入统一 WPF 矢量结果层，按 `Pass/Fail/Neutral` 结果状态组织，沿用统一坐标、缩放、
  图像身份和命中语义。不得因为区域数量引入矢量/栅格自动切换，也不增加逐显示项的矢量/栅格配置开关。
  原始 `HRegion`/轮廓数据继续保留用于算法、结果树和下游绑定。透明栅格贴图仅作为未来有明确性能证据、
  且确认静态区域不需要逐个点选后的可选后端优化；本轮施工不得引入贴图生命周期、图像身份同步和独立区域命中路径。
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
- 2026-09-19 交互渲染优化：文本保持可见，不隐藏文本；文本绘制先按当前视口过滤，只排版视口内文本。结果层缩放/视口变化通过 Dispatcher Render 优先级合并连续重绘请求，避免一次拖动触发多次完整重绘；处理 Dispatcher 关闭竞态。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 文本绘制缓存：结果层按文本内容、颜色、字号和粗体状态缓存 `FormattedText`，最多保留 4096 条并自动淘汰，文本仍始终可见；Plugin.SDK Debug 构建 0 error。
- 2026-09-19 变换缓存：确认图像、结果层、交互层共享 `ImageContainer` 的缩放/平移变换；结果层启用 WPF `BitmapCache`，平移时优先复用已绘制轮廓和文本缓存，结果内容变化时由 WPF 自动失效重绘。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 渲染架构基线：保存 `docs/01-方案设计/2026/09/19/大批量结果渲染架构基线-20260919.md`。确认现有单体 `FrameworkElement.OnRender` 全量重绘是轮廓交互卡顿的根本架构风险；后续以 `RenderScene + TileScene + ResultTileHost` 为目标，先完成 WPF DrawingVisual Tile 后端，再评估 Direct2D，不再继续堆叠单体结果层的局部补丁。
- 2026-09-19 M1 渲染契约启动：新增 SDK `RenderScene`、`RenderTileScene`、`RenderTileKey` 和 `IRenderSceneCompiler`，建立算法结果与 WPF/Direct2D 后端之间的场景编译边界；暂不替换旧结果层，Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M2 场景编译器第一版：新增 `RenderSceneCompiler`，按图像坐标边界将图形/文本结果项编译到 512×512 Tile，并提供视口 Tile 查询；当前保留结果项引用，后续再将单个大批次细分为独立 Tile 几何。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M3 Tile 宿主第一版：`ResultRenderLayer` 改为继承 `ResultTileHost`，结果项异步编译为 `RenderScene`，仅创建和刷新当前视口内的 `DrawingVisual`；平移/缩放不再进入单体 `OnRender` 全量路径，场景编译支持取消和版本隔离。SDK、UI Debug 构建均 0 error；仍需运行时大结果验证及后续 M4 交互空间索引。
- 2026-09-19 M4 空间索引第一步：`RenderScene` 增加按矩形/点位的去重候选查询，`ResultRenderLayer.QueryHitCandidates` 暴露图像坐标命中候选；索引只缩小候选范围，精确几何命中仍由交互层执行。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M4 精确命中第一版：新增 `RenderHitTester`，支持点、线段、轮廓/批量轮廓、圆、旋转矩形、多边形和圆弧的容差命中；`ResultRenderLayer.HitTest` 先走 Tile 候选索引，再执行几何精确测试。Plugin.SDK Debug 构建 0 error；尚未接入具体宿主的点击选择语义。
- 2026-09-19 M4 宿主接入：`ImageControl` 新增 `ResultHit` 路由事件；现有 `CanvasMouseLeftButtonDown` 未处理时才调用结果命中，ROI/卡尺等已有交互优先，命中事件处理后才阻止平移。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M4 契约测试：新增 `RenderHitTestingContractTests`，覆盖圆周容差、旋转矩形内部/边缘命中和跨 Tile 候选去重，3/3 通过；测试构建存在既有 x64/MSIL 架构警告，无新增错误。
- 2026-09-19 M5 性能基线测试：新增 `RenderScenePerformanceTests`，覆盖 3000、10000、71905、100000 个点结果的场景编译与 Tile 候选查询，4/4 通过；当前 Debug 测试总耗时约 2.7 秒，后续需在真实轮廓、文本和 UI 平移/缩放场景下补充帧率与内存指标。
- 2026-09-19 M5 Tile 复用优化：`ResultTileHost` 视口变化时复用已存在的 `DrawingVisual`，只为新进入视口的 Tile 首次绘制；样式/缩放/场景变化仍显式强制重绘。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M5 运行统计：`ResultTileHost` 新增 `ResultTileHostStats`，统计视口刷新、Tile 创建/重绘/复用/卸载，可在运行时验证平移是否命中 Tile 复用路径。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M5 UI 回归：主 UI Debug 构建通过，0 error；现有 XML/nullable/架构警告未因渲染改动新增错误。
- 2026-09-19 运行卡死修复：用户实测算法已成功但 UI 在渲染阶段未响应、内存约 1.5GB；日志确认卡点位于结果提交之后。根因是场景先于图像布局完成时，`ResultTileHost` 在无视口状态下默认渲染全部 Tile。现改为无视口时清空/不创建 Tile，等待 `ImageControl` 首次提交视口后只渲染可见 Tile；UI Debug 构建 0 error，需用户复测大批量 Blob。
- 2026-09-20 卡死复核与诊断：最新 `logs/2026/09/20/SunEyeVision_000038.txt` 在图像载入成功后没有出现 Blob 工具“开始执行/成功”，因此不能把该次卡死直接归因于渲染；同时确认 `RenderContours` 的批量几何缓存未完成时存在逐轮廓整批回退，会在每个可见 Tile 重复展开大结果。已新增 `RenderDiag` 阶段日志（场景编译、视口刷新、Tile 创建/重绘/复用）并移除该高风险回退：缓存未就绪时跳过绘制，缓存完成后再刷新。UI Debug 构建 0 error，需依据下一次日志判断实际卡点。
- 2026-09-20 日志 `logs/2026/09/20/SunEyeVision_001431.txt` 已完成证据闭环：Blob 算法 00:14:49.107 成功、工作流 00:14:49.112 完成；场景编译约 103 ms，但随后 100 个可见 Tile 连续完整重绘两次（1077.8 ms、882.6 ms），与批量缓存未就绪后再次 `RequestRender()` 的代码路径一致。`ResultRenderLayer` 现将场景提交延后到批量几何缓存完成，并记录缓存构建开始/完成耗时，消除首次未命中缓存的整批重绘。Plugin.SDK Debug 构建 0 error；渲染契约/性能测试 7/7 通过，待用户复测并提供新日志确认重绘次数与总耗时。
- 2026-09-20 日志 `logs/2026/09/20/SunEyeVision_003037.txt` 验证重复重绘已消失：批量缓存构建 1224.9 ms，场景提交后仅一次 `renders=100`，但该次仍耗时 2252.3 ms。代码追溯确认每个 Tile 的 `RenderContours` 原先按整个视口选择缓存几何，导致 100 个 Tile 重复绘制同一批几何。现改为使用当前 Tile 的 `(X,Y)` 只绘制对应缓存几何；SDK Debug 构建 0 error，渲染契约/性能测试 7/7 通过，待 UI Debug 构建及下一次运行日志验证单次渲染耗时。
- 2026-09-20 Tile 设计复核：日志确认按 Tile 渲染仍可能让 WPF 提交 80–100 个 Visual；同时旧批次几何按 1000 条轮廓合并后再按 Bounds 复制到多个 Tile，单个 Tile 仍承载大几何。结果层现改为按单轮廓空间范围分组、每个 Tile 独立构建几何，并增加 `tileTotalMs`/`tileMaxMs` 诊断；大视口超过 24 个可见 Tile 时合并为单 Visual，局部视口继续使用 Tile。SDK/UI Debug 构建 0 error；渲染契约/性能测试 7/7 通过，待单例运行确认大视口和局部缩放耗时。
- 2026-09-21 渲染自分析日志：`ResultTileHost` 增加 trace、刷新 reason、viewport、composite/tiles 模式、绘制总耗时、最慢 Tile、管理开销、托管内存/工作集，并在超过 100ms 时输出 `RenderAnalysis` 自动诊断（`single-tile-heavy`、`tile-count-heavy`、`visual-management-heavy`、`composite-geometry-heavy`、`batch-cache-build-heavy`、`scene-compile-heavy`）。Plugin.SDK/UI Debug 构建 0 error；渲染契约/性能测试 7/7 通过。
- 2026-09-21 单例闭环日志 `logs/2026/09/21/SunEyeVision_081158.txt`：图像路径为 `C:\Users\houzhongjie\Desktop\OK\103821989.png`，图像 5120×5120，Blob 结果 3000 个、总面积 1419758；图像载入约 979 ms，Blob 算法约 600 ms，工作流约 1.7 s。首次全视口结果提交进入 `mode=composite`，`sceneTiles=100`，实际绘制 594.2 ms、刷新总耗时 599.4 ms，自动诊断为 `composite-geometry-heavy`，管理开销仅 5.2 ms，工作集约 942 MB；随后一次大视口变化仍有 158.4 ms 绘制/160.9 ms 总耗时。完成缓存后局部 Tile/composite 缩放平移大多约 0.2–12 ms。证据表明当前首帧瓶颈是合并 Visual 内绘制 322 个 Tile 几何，而不是 Tile Visual 管理；后续优化应先做可独立测量的“首帧几何绘制”实验，不再凭感觉调整 Tile 大小。
- 2026-09-21 方案固化：新增 `docs/01-方案设计/2026/09/21/全视口大视口GPU几何批处理渲染优化方案-20260921.md`。方案明确不以位图替代矢量作为主方向，改为 GPU 连续顶点/索引 Batch；Tile 退回空间索引、命中候选和局部范围管理，不再作为独立 WPF Visual；记录 Direct3D 11 主路径、Direct2D Geometry Realization 过渡路径、图像切换的 ImageIdentity/RenderGeneration 失效规则及 M1–M4 验收指标。

- 2026-09-21 GPU Batch 第一阶段实现：新增 SDK 后端无关的 `RenderBatchScene`、`RenderBatch`、`RenderBatchRange` 与 `RenderBatchCompiler`，按颜色/线宽/闭合状态合并普通轮廓和 `BatchedContourGeometry`，连续保存顶点和范围表；结果层后台编译并缓存冻结 WPF 几何，绘制时跳过已编译轮廓，保留变换轮廓兼容路径。新增 `ImageIdentity` 和渲染代次校验，图像切换会取消旧批次并丢弃过期回写。当前实际后端标记为 `wpf-batch`，尚未宣称为 D3D11 设备绘制。
- 验证：Plugin.SDK Release 构建 0 error；渲染相关测试 22/22 通过；此前 UI Release 构建 0 error。等待重新构建后由用户手动验证大批量 Blob 全视口、缩放/平移、图像切换、OK/NG 可见性、命中高亮及快速刷新。
- 2026-09-21 日志 `logs/2026/09/21/SunEyeVision_102232.txt` 诊断：选中 Blob 节点时同一组 6000 条轮廓连续启动两次批量/场景编译，第一次约 144 ms 后取消，第二次批量几何编译约 476 ms，首帧 WPF 几何绘制约 218 ms。结果层增加同一显示项引用集合去重，避免 `SetDisplayCollector`/`SetRegionItems` 连续调用重复编译；图像身份变化仍会强制清空并重建。Plugin.SDK Debug 构建、渲染测试 22/22、UI Release 构建均通过。
- 2026-09-21 取消竞态修复：日志报告图像切换时 `CancelRenderBatchBuild()` 对已由 Dispatcher 回调释放的 `CancellationTokenSource` 再次 `Cancel()`，抛出 `ObjectDisposedException`。取消操作现为幂等，捕获已释放源并安全忽略；Plugin.SDK Debug 构建 0 error，渲染测试 22/22 通过。
- 2026-09-21 调试预览生命周期收口：`UpdateDebugPreview()` 使用独立 debug collector，`ClearDebugPreview()` 不再调用结果层整体 `Clear()`；关闭调试后 `RestoreWorkflowResult()` 现从生产 `WorkflowContext` 同步恢复结果树、图像源和几何层，避免调试结果残留覆盖生产显示。渲染事务测试 5/5 通过；UI Release 串行构建 0 error（795 个既有警告）。
- 2026-09-24 串口参数下拉优化：新建设备与设备详情中的普通串口、Modbus RTU 波特率统一改为 ComboBox，提供 300–921600 的标准波特率并保留非标准已保存值；校验位统一改为 ComboBox，支持无、奇、偶、标记、空格五种校验。运行时解析兼容既有中文值及 Odd/Even/Mark/Space 英文值。UI Release 构建 0 error；通信回归测试 14/14 通过；尚未进行真实串口硬件人工验收。
- 2026-09-24 创建通讯设备后参数为空修复：`CommunicationDeviceEditorViewModel` 创建对象时改用 `Communication` 的类型化属性写入协议键、串口、波特率、网络端点、Modbus/S7/OPC UA 等初始参数，避免直接写 `Settings` 字典绕过属性通知导致创建后详情页 ComboBox/TextBox 显示空值。UI Debug 构建通过（0 error，既有警告）；默认 `dev/Debug` 被运行中程序锁定，本轮使用 `BuildProjectReferences=false` 完成编译验证，未执行 Release。
- 2026-09-24 串口/Modbus RTU 下拉值继续为空的根因修复：详情页刷新串口和波特率选项时，ComboBox 双向绑定在 `ItemsSource.Clear()` 瞬间把 `SelectedCommunication.SerialPort`/`BaudRate` 回写为空值或 0；现刷新前保存值、刷新后恢复，避免仅串口协议受影响。UI Debug 构建通过（0 error，既有警告）。
