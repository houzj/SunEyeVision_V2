# 全局数据触发与心跳优化实施记录

全局触发按每次上传的原生类型比较，不将 Boolean 和数值统一转换为字符串；心跳保留固定值和 A/B 交替行为，修正目标树及输入展示。本轮 Release 已更新，待用户人工验收。

- 基线提交：1173baedc23eb879811f53242282c6072b10101a。
- 当前分支：main；当前执行者：Codex，本工作区唯一代码写入者。
- 任务目标：实施本地现行全局数据触发与心跳优化基线。
- 允许修改目录：Workflow 通讯模型和执行判断、DeviceDriver 通讯入口、UI 通讯管理、对应测试及方案协作记录。
- 已修改文件：新增 CommunicationUploadSnapshot.cs、HeartbeatValueInput.xaml/.cs 和 CommunicationGlobalUploadTests.cs；修改 CommunicationAutomationDefinition.cs、CommunicationTriggerEvaluation.cs、CommunicationAutomationService.cs、CommunicationRuntimeManager.cs、CommunicationManagerAutomation.cs、CommunicationAutomationViewModels.cs、CommunicationTriggersView.xaml/.cs、CommunicationHeartbeatViewModels.cs、CommunicationManagerHeartbeats.cs、CommunicationHeartbeatsView.xaml/.cs，及动作比较、触发界面、心跳测试和本地方案记录。既有其它改动保留。
- 已完成：统一逐次上传快照和通知，包含来源、目标、会话、时间、原生类型、质量及拥有独立副本的原始数据。全局常量按本次类型适配后等于；包含仅适用 String，不检查内容也不能放行坏质量或未解析集合。空常量不隐式全局放行，重复真实通知不自行去重。上传关闭不影响接收事件；缓存展示不上传，过期会话不派发动作。
- 已完成：移除全局来源列，旧文本规则保留类型范围，旧限定来源规则暂停；迁移按钮提示扩展范围并要求确认。事件触发继续使用 ParamBinding 自动等于，不恢复节点赋值或复杂绑定。
- 已完成：心跳本页局部树模板修复缩进、折叠及叶子箭头，原生数据设备父项不可选；叶子短名、选中摘要保留路径。模式显示固定值／两值交替，提供 A/B 标签、Boolean 选择及数值类型和范围校验；清除目标禁用心跳并保留草稿。弹窗和迁移按钮继承主题。
- 未完成：真实 TCP／OPC UA／Modbus 设备联调、可见 Visual Studio 设计器、亮暗主题、窄窗口和高 DPI 人工验收。
- 已执行构建与测试：最终 Release 通讯核心178项，触发界面1项、心跳10项、发送界面2项，共191项全部通过；三个 WPF 组分别运行。全量 Release 895警告0错误，最终 UI Release 1060警告0错误。设计时输出属性查询通过；实际 Release EXE --release-probe 退出0，版本2.0.0.8、alpha、Dark、背景#FF1E1E1E，不初始化设备。
- 人工验收结果：用户确认关闭程序后完成更新；本轮功能人工验收待确认。测试入口为 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
- 已知风险或禁止改动：全局规则可能命中多个设备及不同原生类型，例如常量1可分别匹配数值1和文本1；指定来源使用接收事件。旧受限来源规则必须确认迁移，不能默默放行。Modbus 本轮仅使用现有保持寄存器模型：单 UInt16、双寄存器 Int32 及 ByteOrder；未新增线圈或浮点配置，不猜未知类型，不取集合第一项。数值心跳输入为按既有发送解析器校验的文本控件，不是范围步进。未修改 SDK、工作流调度或发布规则。
- 下一步：用户在 Release 验收原生类型匹配、旧规则迁移、上传开关、重复通知、断线重连和心跳 A/B；发布和真实业务操作另行授权。本轮未强制结束用户进程、打包、增号、提交推送或同步其它工作区，也没有跨 AI 交接。

证据目录为 artifacts/validation/global-data-heartbeat-20261010。最终结果为 communication-runtime-final.trx、CommunicationAutomationUiTests-final.trx、CommunicationHeartbeatTests-final.trx、CommunicationSendEventUiTests-final.trx 和 Release-startup.json；较早结果保留，但不计入本轮191项之外。
