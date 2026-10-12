# 通信解析优化实施交接

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，保留既有未提交修改；未发生跨AI交接或创建临时提交。
- 当前分支：feature/comm。
- 当前执行者：Codex，唯一代码写入者。
- 任务目标：按官方复核方案实施共用解析、通信管理类型适配和解析高级设置，并更新旧计划；纳入用户所见即所得的取数原则。
- 允许修改目录：必要的SDK、Workflow编解码入口、UI通信管理、协议解析插件、对应测试与本地docs。不改主项目或标定工具链。
- 已修改文件：新增src/Plugin.SDK/Models/Communication/ProtocolValueCodec.cs和对应测试；更新src/Workflow/CommunicationPayloadCodec.cs，UI通信管理Processor／ViewModels／Dialog，ProtocolParse参数／算法／编辑器，通信与布局测试；更新接收、解析、优化基线、模块结果约定、实施计划及CURRENT_TASK。
- 已完成：R1共用无UI解析；R2原始文本数值转换和字节int／float／string／Binary，Binary组装值独立于展示；R4固定0基、Auto默认数值解释、条件高级设置、520×600、主题与列表交互。默认1至3字节非负、4字节有符号；显式无符号仍不得超出int32。管理器事件ASCII严格校验后按二进制解释，模块ASCII按数字字符转换。OPC UA原生类型检查不放宽。
- 未完成：R3无UI来源服务、样本关联、接收工具，P4发送，P5队列，P6整链验收。Binary字段不代表Binary触发／回复已接通。旧IndexBase／Signed配置不提供迁移，开发期旧方案需重新确认解析参数。
- 已执行构建与测试：最终UI专项270通过、SDK专项68通过；标准dotnet build SunEyeVision.slnx -c Release --no-restore（quiet／ErrorsOnly）0错误1148警告；标准UI Release同参数0错误947警告。运行常用Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe --toolbox-probe，退出0，9类22工具含ProtocolParse／ProtocolAssemble，SDK2.1、产品2.0.0.8、Dark；证据artifacts/validation/protocol-optimization-20261012/release-probe.json。git diff --check通过。实际WPF渲染图位于artifacts/validation/protocol-assemble-layout-20261012/parse-*.png。
- 人工验收结果：尚无本轮用户实际UI验收；STA运行时加载和深浅色渲染已检查，VS设计器及高DPI未验证。无硬件启动探针不等于设备收发业务验收。
- 已知风险或禁止改动：SDK全套前阶段存在两项失败，本轮仅专项不宣称全套通过。标准通信Release已更新，不混用Debug或主项目Release。不连接／写用户设备，不强制结束用户进程，不同步主项目或提交推送。保留既有0／非0边沿语义，不因参考VM改为仅1。
- 下一步：按接收当前值定稿改造执行期服务和ReceiveData，模块结果／分析展示真正读取的来源和时间；不增加配置页运行预览。服务通过执行层注入，复用连接并协调取消／超时。新规则见HANDOFF_RECEIVE_CURRENT_VALUE_DESIGN_20261012.md。
