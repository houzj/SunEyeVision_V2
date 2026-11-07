# AI 协作交接单

- 基线提交：91199b7529b0e5de2a0b67d9f047c985e2e912e1；未新增提交，保留既有P4未提交改动。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：修复发送事件候选空白、区分同名事件并保持稳定绑定。
- 允许修改目录：本通信工作区Workflow名称辅助、UI宿主发送服务及管理器显示、SendData编辑器、专项测试和docs。
- 已修改文件：src/Workflow/CommunicationSendEventDefinition.cs；src/UI/Features/CommunicationManager/CommunicationSendEventViewModels.cs；src/UI/Services/Communication/WorkflowCommunicationSendService.cs；tools/SunEyeVision.Tool.SendData/Views/SendDataEditor.xaml及.xaml.cs；tests/SunEyeVision.UI.Tests/SendEventSelectionTests.cs；发送方案、通信工具链实施计划及CURRENT_TASK.md。
- 已完成：共用默认名称；候选按设备及稳定ID区分；短ID冲突使用完整ID；未选择事件隐藏虚构行；选择加载声明参数但不使用管理器测试值；刷新按FieldId更新元数据并保留输入；删除后保留失效EventId，不自动改绑同名替代事件。没有更改持久化名称、SDK契约或执行寻址规则。
- 未完成：OPC UA直接输入类型、精确绑定过滤和忽略错误诊断；剩余P4、编码UI、P5队列及P6。U1至U3继续后置。
- 已执行构建与测试：Release发送事件选择／发送工具／既有事件及UI专项33通过0失败；标准全量Release0错误1150警告；标准UI Release0错误1062警告。常用Release实际toolbox-probe退出0，9分类24工具，ReceiveData／ProtocolParse／ProtocolAssemble／SendData正常加载。设计时TargetPath指向常用Release插件目录；不等于已验证VS设计器。
- 人工验收结果：Codex目视核验实际WPF编辑器深浅主题截图。用户Release点击验收、VS设计器、高DPI及设备硬件未验收。artifacts/validation/send-event-selection-20261013含editor-dark.png、editor-light.png、release-toolbox.json。
- 已知风险或禁止改动：事件命名与绑定身份独立；不得以Name重新寻址或自动修复失效ID。未操作用户设备，无提交／推送，不修改主项目和标定。既有1150／1062警告未在本轮清理。
- 下一步：用户重新打开常用Release验证事件候选与参数；另行处理OPC UA类型及绑定问题，随后继续通信计划剩余范围。
