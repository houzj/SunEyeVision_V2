# 通讯触发优化实施交接

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main。
- 当前执行者：Codex，当前工作区唯一代码写入者；未向其它分支同步。
- 任务目标：实施 2026-10-10 通讯优化基线，补齐两级条件、独立参数绑定、OPC UA 监视配置及触发界面一致性，提供 Debug 测试程序，不制作安装包。
- 允许修改目录：src/Workflow、src/DeviceDriver/Communications、src/UI 中对应通讯与执行文件、tests/SunEyeVision.UI.Tests、docs 中本任务文档。

## 已完成

接收事件负责质量、解析和事件比较；Boolean 新增上升沿和下降沿，首次真实有效样本建立基准，质量错误、重连和配置变化重新建立基准。UI 缓存回放不建立自动化边沿基准。

触发规则新增独立执行条件，包括无附加条件，以及按类型提供的等于、不等于、文本包含和数值大小比较。旧 FieldId 保留在持久化中，不自动变成过滤条件；旧规则默认无附加条件。普通解析输出及协议比对的组装输出分别作为对应模式的数据源。

触发事件旁新增绑定入口，行内新增执行条件入口，统一打开独立草稿弹窗。来源复用 SDK ParamBinding，按稳定字段 ID 选择本次事件输出。目标只列工具现有契约声明的可写标量运行参数和模块输入；保存校验目标、类型和重复绑定，取消不落地。

自动化捕获本次接收字段值，条件与参数映射使用同次样本。请求携带不可变标量和只读映射列表，执行管理器通过请求上下文传递；完整流程和单模块只覆写执行参数副本。完整流程还在执行副本中覆写同名参数绑定，避免原绑定把本次通讯值覆盖回去。原节点参数和 ParamSettings 不改写，后续手动执行不会沿用上一条通讯值。执行宿主验证绑定目标属于本次执行范围；忙碌仍跳过、不排队。

OPC UA 数据点新增变化订阅或周期读取及 10～600000ms 间隔，默认变化订阅和100ms。周期读取不重叠、不补发积压请求，支持取消和停止等待；同点替换先停旧监视，监视配置操作串行化。方案切换先停止旧任务，断线重建边沿基准。编辑弹窗展示服务器接受的采样、发布间隔；读取模式明确实际周期受读取耗时影响。取消编辑恢复原模式和间隔，克隆及 JSON 往返保留新配置。

触发列表目标列宽改为280 DIP，事件、目标、执行条件入口明确分离。删除按钮复用既有 DangerButtonStyle，补 Padding=0 和居中，修复只见红框、不见“×”；删除确认复用 ThemeMessageDialog，并明确仅删除规则。禁止 DataGrid 默认 Delete 键绕过确认与同步入口。新弹窗表头、行状态和页签使用主题资源，避免暗色下默认白底回退。

## 已修改文件

- 配置与纯逻辑：src/Workflow/CommunicationAutomationDefinition.cs、OpcUaDataPoint.cs、新增 CommunicationTriggerEvaluation.cs。
- 执行快照：src/Workflow/WorkflowRunRequest.cs、WorkflowExecutionEngine.cs；src/UI/Services/Workflow/WorkflowExecutionManager.cs；src/UI/Shell/Windows/MainWindowCommunicationAutomation.cs。
- OPC UA 运行层：src/DeviceDriver/Communications/OpcUa/OpcUaAdapter.cs、Runtime/CommunicationRuntimeManager.cs。
- 应用服务：src/UI/Services/Communication/CommunicationApplicationService.cs、CommunicationAutomationService.cs、新增 OpcUaPointPolling.cs。
- 编辑模型：src/UI/Features/CommunicationManager/CommunicationAutomationViewModels.cs、CommunicationEventViewModels.cs、CommunicationManagerAutomation.cs、CommunicationManagerViewModel.cs、CommunicationReceiveProcessor.cs、OpcUaPointRowViewModel.cs。
- UI：CommunicationTriggersView.xaml 及代码、CommunicationUiResources.xaml、CommunicationManagerDialog.xaml、OpcUaDataPointDialog.xaml 及代码、新增 CommunicationTriggerEditorDialog.xaml 及代码。
- 测试：新增 CommunicationTriggerOptimizationTests.cs；更新 CommunicationAutomationTests.cs、CommunicationAutomationUiTests.cs、CommunicationReceiveProcessorTests.cs、OpcUaRuntimeTests.cs。
- 文档：优化基线、CURRENT_TASK.md、本交接记录。所有其它原有未提交文件和汇报素材均保留。

## 已执行构建与测试

- Debug UI 构建通过，测试构建也已更新 dev/Debug。
- 核心通讯与 OPC UA 回归130项通过，包括本地真实 OPC UA 测试服务器：订阅间隔可查询，周期读取相同值连续产生样本。
- 共享静态 WPF 宿主的页面分开运行：触发 UI 1项、发送 UI 2项、心跳 UI 10项全部通过；触发测试包含新条件弹窗的运行时构造和布局、参数绑定控件、280 DIP列宽及删除内边距。
- 执行基线3项、相机工作流2项通过。以上不重复合计148项。
- 新增完整流程和单模块回归验证输入覆写、执行绑定优先级、原设计数据不变和下次运行不泄露。
- 全量 Release --no-restore 通过，1060警告、0错误；最终 UI Release --no-restore 通过，777警告、0错误。最终单独 Debug UI 构建841警告、0错误。构建存在历史警告，未声称零警告。
- Debug 和 Release 实际 EXE --release-probe 都退出0，2.0.0.8、alpha、Dark，启动背景 #FF1E1E1E；探针在设备初始化前运行，不连接真实硬件。
- DesignTimeBuild=true 的 TargetPath、OutDir、ApplicationIcon 查询通过；这只是设计时属性核验，不等于可见 Visual Studio 设计器验收。
- 证据目录：artifacts/validation/communication-optimization-20261010。包含各组 TRX、Debug-startup.json、Release-startup.json。早期 communication-optimization.trx 为失败诊断保留，不是最终通过报告；最终以 communication-runtime.trx、trigger-ui.trx、CommunicationSendEventUiTests.trx、CommunicationHeartbeatTests.trx、baseline-execution.trx、camera-execution.trx 为准。

## 人工验收结果与未完成项

尚未收到本轮用户人工验收结果。真实 OPC UA 服务端、实际目标工具传参、亮暗切换、失焦、高 DPI、窄窗口及可见 VS 设计器仍需现场确认。没有使用真实相机或 PLC 执行业务流程。

当前参数绑定仅支持契约声明且同类型的标量；不做任意字符串转数值、集合或图像输入转换。文本解析沿用原有严格类型规则，不能把文本中的“2”自动冒充原生整数。若要扩展显式转换或复杂表达式，应先更新基线和测试。

多个 UI 测试类同时运行时共享静态宿主初始化出现超时；分别运行通过。没有通过延长超时或修改业务代码掩盖此现象，测试宿主并行隔离可另立任务。

VM 行内触发字符的完整内部消费仍未核实。本实现是 SunEyeVision 明确设计的两级条件，不宣称复制 VM 的内部机制。

## 已知风险与禁止改动

版本仍为2.0.0.8，未递增、未重新打包、未提交或推送、未同步通讯分支、未安装、未改文件关联或图标。原发布目录和其它用户改动不得清空。没有强制结束用户进程。

执行端新增请求级 NodeBindingOverrides，后续修改须保持仅作用于执行副本的边界，禁止写回设计对象或由 UI 调度工具。跨 AI 实际接手前须按项目规则再确认状态和基线，不能把当前未提交工作区当作干净基线。

## 下一步

用户运行 dev/Debug/SunEyeVision.UI.exe，先检查触发列表与参数弹窗，再在数据点编辑中配置监视模式和间隔。分别验收事件条件、动作条件和参数传入；未绑定执行条件时选择无附加条件。记录人工验收后再决定是否发布，打包需用户单独申请。
