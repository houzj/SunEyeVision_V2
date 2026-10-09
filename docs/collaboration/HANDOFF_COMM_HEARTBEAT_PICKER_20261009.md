# AI 协作交接单

- 基线提交：cef87553901c38f2fe4dcabb2ea05df19dab5ac9
- 当前分支：feature/comm
- 当前执行者：Codex（本轮单写入，无跨 AI 编码交接）
- 任务目标：修复用户反馈的心跳管理设备下拉没有设备的问题。
- 允许修改目录：src/UI/Features/CommunicationManager、tests/SunEyeVision.UI.Tests、docs/collaboration。
- 已修改文件：CommunicationHeartbeatsView.xaml、CommunicationHeartbeatTests.cs、CURRENT_TASK.md、本交接单。
- 已完成：真实 WPF DataGrid 中确认按钮 IsChecked=true 而行 IsTargetOpen=false，候选集合已有设备；显式即时同步按钮与弹层状态，并将 Popup 数据上下文明确绑定到按钮所在行。实际 TreeView 选择事件验证 TCP 设备、Modbus 地址与 OPC UA 数据点回填，选择后关闭弹层，连续重新打开可用，共享方案同步。
- 未完成：VS 设计时和用户实际应用的人工视觉验收。
- 已执行构建与测试：串行通讯、OPC UA、相机执行 Release 回归 123/123 通过；标准 UI Release 构建 0 错误、1049 警告；标准 UI Debug 构建 0 错误、1047 警告；git diff --check 通过。警告为既有构建警告，未作清理。
- 人工验收结果：用户已确认关闭程序，dev/Debug 修复产物已更新；用户设备选择复验待确认。新增测试通过隔离窗口运行，不连接用户设备。
- 已知风险或禁止改动：不修改候选去重规则、协议编码、后台心跳调度、工作流执行、SDK、全局主题或发布规则。原生协议需展开并选择具体地址/数据点，不能只选择设备分组。禁止将运行时自动测试描述为 VS 设计时或用户设备人工验收。
- 下一步：用户启动 dev/Debug，在心跳管理新增行并选择设备/地址，确认实际交互；如仍异常，提供所选协议及下拉截图。
