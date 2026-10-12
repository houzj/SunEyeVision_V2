# 协议解析与协议组装实施交接

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f。
- 当前分支：feature/comm。
- 当前执行者：Codex，唯一写入者。本轮未交给另一个AI；未提交或推送，保留此前未提交基础和方案。
- 任务目标：按通信工具链计划交付不涉及硬件读写的解析／组装首版插件。
- 允许修改目录：本通信工作区内SDK通信基础、工具插件、相关Core保存修复、UI共用分隔符、测试和文档；不修改主程序工作区和标定算法。

## 已修改文件与完成范围

新增tools/SunEyeVision.Tool.ProtocolParse和tools/SunEyeVision.Tool.ProtocolAssemble，加入SunEyeVision.slnx和UI测试项目。每个插件包含参数、执行实现、WPF配置编辑器和SDK编辑器工厂，不引用UI宿主程序集。设计时字典在XAML显式声明，执行入口复用ToolDebugControlBase的宿主执行请求；共用CommunicationDataAnalysisControl不创建第二套结果。

SDK新增CommunicationDelimiterCatalog、ProtocolBindingInput、ProtocolEditorResources和序号转换器。UI CommunicationSeparators转为映射SDK目录，既有UIAPI不变。绑定保持原有节点GUID／字段键和全局变量表达式，数组仅接受单项，BinaryPayload保留一包真实字节。

解析按字段配置产生输出，名称改动保留字段GUID和绑定键并通知契约变更。文本允许多余片段而不发布新增字段；缺项、非法类型／数值或字节区间越界整体失败。int32及有限float32转换后进入SDK规范Integer／Double，字符串保留空值。字节格式、编码、0／1编号、闭区间、数值顺序和整数符号显式配置。组装提供固定String及对应UTF-8 Binary双输出，常量不猜数值／HEX；不增加结束符或隐式写入。

新增tests/SunEyeVision.UI.Tests/ProtocolToolchainTests.cs，27项专项覆盖算法、四种字节顺序、原始分析片段、有符号／ASCII差异、原子失败、稳定绑定、宿主ToolExecutor链路、参数注册／保存／复制序列化选项、取消及STA配置编辑器加载布局。测试程序集启动时注册插件参数，符合宿主加载时序，不在并行测试途中增加尚未注册类型。

Core Services/Serialization/ParameterTypeRegistry.cs修复同一JsonPolymorphismOptions不能属于多个JsonTypeInfo的问题：每次解析ToolParameters类型时复制注册目录到独立元数据。原先组合运行插件保存和工作流保存会报Parameter already associated with a different JsonTypeInfo instance；修复及测试时序规范后最终串行／并行扩展回归均通过。影响评估为共享JSON保存路径，不改变SDK词汇、执行调度、相机和标定行为，不增加兼容迁移。

同步解析、组装方案的实施口径和ASCII图，更新实施计划及模块结果共同约定。后续候选设计若与首版实施节不同，以首版节为当前运行事实。

## 已执行构建与测试

- 新插件专项：27／27通过。
- 宽范围UI回归：过滤ProtocolToolchainTests、Communication、OpcUa、Camera，最终串行253／253和常规并行253／253通过。范围包含测试名匹配，不等于全部UI测试。
- SDK专项：Communication、SdkTypeVocabularyContractTests、ToolContractVersionContractTests共76／76通过。
- 最终完整Release：dotnet build SunEyeVision.slnx -c Release --no-restore，0错误、1145警告。测试与构建最终分开执行，避免共享测试输出文件占用。
- 最终标准UI Release：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore，0错误、1009警告。警告未全部清理，不宣称零警告。
- 只读PE元数据核验：Release发布目录21个工具DLL均声明契约2.1，包含两个新插件。
- 实际Release程序使用--release-probe启动，退出0；产品2.0.0.8、SDK契约2.1、Dark。证据artifacts/validation/protocol-tools-20261010/release-probe.json；探针在设备初始化前退出，不等于主窗口全链路验证。
- 前轮完整SDK473项中两项失败（Int32旧快照预期、NPointVerification设计字典）本轮未修复、未重跑完整套，不将专项通过写成全套通过。

## 人工验收与未完成

VS设计器和用户真实主窗口的拖入工具、保存方案重开、字段订阅及模块结果视觉验收待完成。本轮仅STA控件构造／布局、宿主执行器、注册及无硬件Release启动自动验证。未更新dev/Debug，未连接或写入用户设备，未同步主程序项目。

接收／发送插件、无UI通信服务注入、队列和真实设备端到端尚未完成，不能标记四工具整体交付。脚本解析／组装、视觉控制器、相机IO及二进制字段组装继续排除。

## 下一步

先落实SDK到宿主的无UI通信运行契约，明确设备／数据点、接收样本、执行快照和取消；再开发接收数据工具，把变量和通信来源接入模块结果及下游绑定。随后完成发送工具的目标编码、原子校验和提交明细，最后推进队列及端到端验收。主项目同步共享SDK／Core修复时应使用记录的文件差异，不覆盖其标定改动。
