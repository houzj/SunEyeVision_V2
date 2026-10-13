# AI 协作交接单

- 基线提交：91199b7529b0e5de2a0b67d9f047c985e2e912e1；未新增提交。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：接收数据分析显示可读来源名称和本次值，不再显示内部GUID。
- 允许修改目录：ReceiveData插件、SDK共用分析文本样式、相关测试与docs。
- 已修改文件：tools/SunEyeVision.Tool.ReceiveData/ReceiveDataTool.cs；src/Plugin.SDK/UI/Controls/Communication/CommunicationDataAnalysisControl.xaml；tests/SunEyeVision.UI.Tests/CommunicationReceiveDataToolTests.cs及CommunicationAnalysisThemeTests.cs；接收方案及CURRENT_TASK.md。
- 已完成：服务名称映射设备／数据点和全局／分组／变量；缺展示信息时使用普通标签。说明显示输出值及时间，Binary标注HEX。设备使用样本数据时间，不误称新报文；变量使用读取时间。长文本省略与完整Tip。稳定身份、取数及输出类型不变，插件未引用宿主。
- 未完成：用户重开Release执行验收、VS设计器和高DPI。OPC类型问题和工具链剩余阶段不包含本次。
- 已执行构建与测试：18项接收及主题专项通过；标准全量Release0错误1150警告、标准UI Release0错误1026警告；实际常用Release toolbox-probe退出0，四通信插件正常加载。设计时TargetPath核对通过，不等同VS设计器验收。
- 人工验收结果：Codex目视检查共用分析控件深浅主题示例截图，来源、说明可读；Tip断言通过。artifacts/validation/communication-results-20261013包含截图和receive-names-release-toolbox.json。用户未验收。
- 已知风险或禁止改动：只改变展示，不以名称寻址；时间保留偏移。未连接或写入用户设备，无提交推送，既有修改保留。已有警告不在本轮清理。
- 下一步：用户重开常用Release并重新执行接收节点，核验来源及值；继续既有通信开发计划。
