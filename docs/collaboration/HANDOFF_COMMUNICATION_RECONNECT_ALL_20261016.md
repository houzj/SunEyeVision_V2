# 全通讯协议状态和自动重连修复交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单工作目录写入，保留所有既有脏工作区修改。
- 任务目标：统一已接入协议的断线状态和自动重连，分析当前设备未重连的原因，保留配置、绑定和真实读取。
- 允许修改目录：src/DeviceDriver/Communications、UI通讯应用服务/通讯管理器、docs/collaboration。
- 已修改文件：
  - 新增Runtime/CommunicationRuntimeManager.Operations.cs；更新CommunicationRuntimeManager.cs、CommunicationPollingCoordinator.cs。
  - 更新ModbusAdapter.cs、S7Adapter.cs、CipAdapter.cs及Raw下TCP客户端、TCP服务端、串口和UDP适配器。
  - 更新CommunicationApplicationService.cs及CommunicationManagerViewModel.cs。OPC UA适配器沿用前一批修复，没有本轮专属改动。
  - 更新CURRENT_TASK.md及本交接。

## 当前设备和已确认原因

只读检查D:/SunEyeVisionData/Solutions/1.solution，其最后保存时间为2026-10-16 10:58:49。TCP客户端启用，但autoReconnect=false；OPC UA启用且autoReconnect=true，两者请求超时均为3000毫秒。本次没有修改方案文件或替用户开启开关。

已落盘的D:/SunEyeVisionData/Logs/2026/10/16/SunEyeVision_103908.txt中，10:39:15.350记录TCP连接被目标拒绝，10:39:17.350记录OPC UA会话尚未连接，未见重连日志。TCP未重试符合关闭配置；OPC UA错误漏通知已在前一批修复。此日志来自旧程序，不能当作新版本运行验收。

其他已确认的设计缺口是Modbus/S7操作失败未发布断线，CIP清理失效会话后未通知重连，以及设备已断线后才打开AutoReconnect不会创建任务。不同入口还存在旧会话清理未结束就创建新会话、手动/策略操作与后台重连竞争的问题。

## 已完成

- 所有已接入协议的状态查询统一核对实际适配器；Modbus八种读写、S7读写、CIP显式消息及原始发送统一检查就绪状态、真实I/O错误和返回时连接身份。旧连接结果拒绝采用，失败写入不自动重放，也不返回历史缓存值。
- 连接级IOException、SocketException、TimeoutException、已释放资源及S7 ConnectionError进入断线状态并通知应用重连；参数/不支持操作、Modbus从站业务异常、S7地址或权限拒绝等不按所有异常一律重连。CIP仍保留完整正常响应中的业务状态。
- Modbus、S7、CIP创建和释放与原生操作共用服务门；NModbus没有取消令牌的I/O仍必须等真实操作结束，不提前释放共享流。S7异步连接和操作使用原RequestTimeout；已发送请求后取消会关闭流，避免残留响应被下一次操作读取。仅排队取消不因此重置连接。
- 运行时按当前适配器身份移除和更新状态，连接失败/主动断开/被动断线的资源回收任务均被跟踪，新一代注册前等待旧资源释放；方案关闭也等待退休资源。旧失败或旧释放不能删除新连接或覆盖状态。
- 串口/UDP接收结束立即不再报告有效，串口释放与写入/打开互斥；TCP服务端Start与Stop互斥，防止被释放的监听器又启动。UDP和TCP服务端保持本地绑定/监听语义，发送至不可达UDP目标或服务端没有客户端不直接重建健康本地端口。
- 应用服务监听当前设备的IsEnabled/AutoReconnect，自动重连不依赖通讯管理器窗口持续打开。断线后再开启AutoReconnect可启动任务；关闭开关取消重试而不强断健康连接；禁用设备会停止连接。手动断开优先，重新手动连接才解除手动停止意图。
- 每设备手动/策略操作串行，删除、配置重连、撤销新增设备使用同一入口；方案切换取消旧任务、解绑旧设备属性观察，并在自动连接各设备前复核启用和手动意图。
- 延续1秒、2秒、4秒递增至30秒的退避和原开关；新增尝试与成功日志，保留失败原因/下次等待日志。恢复监视期间再次断线继续重试。
- 断线即停止旧Modbus轮询，旧任务只条件移除自身；取消与CTS释放互斥，获取轮询生命周期使用保存的Token，旧批次不作为新连接结果发布。

## 连接状态的范围

TCP客户端接收EOF/异常或真实发送失败会触发断线。Modbus/S7/CIP在真实I/O或状态检查发现不可用后重连；本次不增加任意地址读取、CIP服务探针或未经配置的应用心跳。空闲时如果底层尚未检测到对端故障，不能承诺立即变为断线。[S7NetPlus 0.20.0源码](https://github.com/S7NetPlus/s7netplus/blob/v0.20.0/S7.Net/PLC.cs)也将IsConnected定义为最近一次I/O观察到的TCP状态，而不是实时在线保证。

TCP服务端的Connected表示监听仍可用，单个客户端退出后继续等待其重新连入；UDP的Connected表示本地端口已绑定，不代表远端设备在线；普通串口打开也不能独立证明对端设备存活。需要及时发现远端静默故障时，应使用用户已配置的合法心跳或周期读取，而不能由通用层猜测协议内容。

## 构建与验收

- 已执行构建与测试：首轮编译发现InvalidDataException不属于IOException，已修正异常模式。修正后UI Release804警告0错误（15.75秒）；中间全量1159警告0错误（64.66秒）、821警告0错误（43.01秒）。最终全量821警告0错误（38.60秒）；独立UI Release1066警告0错误（24.21秒）。常用Release中的DeviceDriver/UI主DLL与Release编译中间DLL哈希一致，相关git diff --check通过，仅换行提示。使用项目原构建部署规则，没有运行或强制关闭程序。
- 人工验收结果：遵用户先前限制，不新增或运行测试、不启动程序或设计器、不访问设备；真实断线恢复待人工验收，未宣称全协议实机通过。
- 未完成：真实网络/串口断开和恢复、AutoReconnect热切换、设备禁用/启用、删除/切方案、无客户端监听和UDP静默的运行验收。
- 已知风险或禁止改动：不改用户配置和绑定，不改SDK或插件输出契约，不改标定，不提交推送。自动重连不把已失败的当前工作流改成成功；恢复后重新执行真实操作。无法由历史日志反推出OPC UA第一次失效的外部原因。
- 下一步：使用常用Release人工验收上述场景，并根据日志区分开关关闭、未检测到断线、等待重试、实际连接失败及重连成功。TCP客户端当前autoReconnect=false，如需重试应由用户在界面显式开启。
