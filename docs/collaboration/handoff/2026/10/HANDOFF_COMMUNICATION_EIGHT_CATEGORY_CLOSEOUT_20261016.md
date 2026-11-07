# 通讯八类集中排查收尾交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，未提交推送；所有原有并存修改保留。
- 任务目标：C1至C8集中源码排查、修复确认问题，不新增功能、不改变已有配置和绑定；区分代码完成和人工验收后返回算法主线。
- 允许修改目录：src/DeviceDriver/Communications、src/UI/Services/Communication、src/UI/Features/CommunicationManager及docs/collaboration。

## 已完成

| 类别 | 本轮源码检查与修复 |
| --- | --- |
| C1 状态一致性 | 复核实际连接、监听/端口语义及后台截止时间展示；旧方案窗口不再接收新方案显示与配置更新 |
| C2 生命周期并发 | 复核前批按设备唯一重连、策略版本、删除与禁用取消、同ID隔离；设备生命周期缓存令牌，取消后及时释放来源，旧方案取消源与运行时完成清理记录一并回收 |
| C3 实时读写 | 通用入口在原生操作后核对取消与适配器身份；OPC UA写入、订阅和浏览等统一身份保护；准备发送的状态快照交给运行时，在取得会话的同一状态锁内复核；工作流、宿主发送事件、界面发送和自动响应接入设备取消约束 |
| C4 恢复任务 | 已停止但I/O仍在退出的Modbus轮询纳入StopAll等待；订阅创建异常清理资源、取消订阅按服务器操作结果更新本地状态；设备启停重新生成原有心跳计划，自动响应刷新并取得设备令牌；取消后的旧心跳/周期读不再报告新失败 |
| C5 超时资源 | TCP服务端接收任务完成后移除并观察异常；原始TCP发送串行且使用原请求超时，服务端与UDP发送、CIP注销同样受限；Modbus每次请求有超时并在取消时关闭传输，但不提前释放操作门；重复建连在创建占端口适配器前拒绝 |
| C6 报文类型 | 复核原结束符分帧、UDP数据报、编码容量和类型路径；补Modbus读取数量/地址边界、CIP上下文/会话号/选项检查；TCP写入中断后不复用半帧连接；OPC UA非Good读取或写入响应不能报成功；S7保持原类型化流程 |
| C7 配置绑定 | 窗口记录原方案身份，保存、放弃修改、旧回调、接收/发送事件、心跳和自动化配置写入均避免跨方案；未迁移配置、修改输出身份或绑定格式，原公开运行时调用保留兼容入口 |
| C8 诊断响应 | OPC UA原同步浏览放入后台并释放服务器浏览游标；FC03高频明细保留文件诊断，不逐条刷UI；保留设备、失败原因和取消诊断，旧取消任务不补成功 |

没有增加协议、连接池、报文模式、UI功能或运行配置字段。缓存仅保持原展示/元数据用途，不作为实时读写失败后的成功替代；写入取消或超时只报告失败/不确定，不自动重发。算法、标定、工具输出和屏蔽功能均未改。

## 本轮修改文件

- DeviceDriver：CommunicationProtocolFactories.cs；Modbus/ModbusAdapter.cs；OpcUa/OpcUaAdapter.cs；Cip/CipAdapter.cs；Raw下TcpClient、TcpServer、Udp、Serial适配器；Runtime下CommunicationRuntimeManager.cs、CommunicationRuntimeManager.Operations.cs、CommunicationPollingCoordinator.cs。
- UI服务：CommunicationApplicationService.cs、CommunicationSendEventService.cs、WorkflowCommunicationSendService.cs、CommunicationAutomationService.cs、CommunicationHeartbeatService.cs、CommunicationHeartbeatRunner.cs、OpcUaPointPolling.cs。
- 通讯窗口：CommunicationManagerViewModel.cs、CommunicationManagerConnectionPresentation.cs、CommunicationManagerSendEvents.cs、CommunicationManagerHeartbeats.cs、CommunicationManagerAutomation.cs。
- 文档：CURRENT_TASK.md、COMMUNICATION_CLOSEOUT_SCOPE_20261016.md与本交接单。部分文件原本已含前批未提交修改，本轮不将全部Git差异归为新改动。

## 已执行构建与检查

- 中间全量Release均零错误：1162警告/60.03秒、824警告/36.01秒、824警告/40.91秒、824警告/41.06秒；中间UI1069警告/19.23秒。
- 最终全量：dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly；1162警告、0错误，64.81秒。
- 最终UI：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -clp:ErrorsOnly；1069警告、0错误，19.96秒。
- Release/SunEyeVision_v1.0.0/SunEyeVision中的UI和DeviceDriver主DLL SHA256与build_temp编译DLL一致；相关源码和文档diff检查通过，换行提示不属于差异错误。
- 本轮未修改发布规则或工具插件依赖；未修改用户方案文件。

## 待人工验收

遵用户此前限制，本轮不新增或运行测试、不启动Release程序或Visual Studio设计器、不连接设备。以下未验收，不可写成全部通过。

| 项目 | 对应类别 | 应人工确认 |
| --- | --- | --- |
| 实际显示 | C1、C8 | 开关、灯、状态、倒计时和原始失败原因一致，OPC UA浏览期间UI可操作 |
| 快速操作与晚到连接 | C2、C5 | 快速切AutoReconnect和禁用启用；删除、同ID切方案、窗口重开；服务器恢复后旧连接不能复活或断开新设备 |
| 实时读写与取消 | C3、C6 | 每个实际使用协议的成功、协议拒绝、断网、取消和超时；取消/超时写入的设备端结果需确认，软件不得重发或报成功 |
| 恢复 | C4 | 重连后Modbus轮询、OPC UA订阅/周期读、心跳和自动响应正常且不重复；取消订阅/创建失败无幽灵任务 |
| 资源与压力 | C5、C8 | 多次连接断开、服务端反复接入、慢驱动和较大报文；长期任务数、句柄、内存和日志容量不异常增长 |
| 报文和旧方案 | C6、C7 | TCP/串口分包合包及结束符、UDP边界、CIP响应、Modbus地址/字节序、S7/CIP类型与OPC UA质量；旧方案打开、保存/放弃修改、绑定身份不变 |

无对应设备的协议应记录未验收；生产认证、证书和网络权限按部署环境单独确认。本轮没有改动证书或安全策略。

## 已知风险与禁止扩展

原生调用无法即时中止时，必须等待其退出或请求超时才能完全释放资源，不能将取消解释为物理写入一定未发生。编译与静态排查不能证明真实驱动取消耗时、设备兼容性或长期稳定性。后续只处理上述验收复现的正确性缺陷，不自动开展新协议、性能平台或UI重构。

## 下一步

通讯源码集中整改已收尾，运行验收单独保留。开发返回docs/01-方案设计/2026/10/全算法工具HALCON标准流程分批优化方案-20261015.md，继续既定HALCON标准流程优化；保留原绑定、输出和屏蔽功能，标定仍单独处理。此次未擅自开始新的算法批次。
