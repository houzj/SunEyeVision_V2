# 全协议连接意图和实际状态显示交接

本文件记录上一版实现。用户随后明确改为列表开关反映实际连接、AutoReconnect开启时手动断开也重连；当前规则以HANDOFF_ACTUAL_CONNECTION_SWITCH_20261016.md为准，下面的意图开关及手动停止优先约定已被覆盖。

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，保留既有算法、绑定及通讯修改。
- 任务目标：所有已接入通讯协议统一分离启用连接与实际连接状态；掉线开关保持开启，显示等待重连、倒计时和失败原因，不伪装已连接。
- 允许修改目录：UI通讯应用服务/通讯管理器、Workflow通讯模型仅运行时显示属性、DeviceDriver通讯诊断原因、本地协作记录。
- 已修改文件：
  - 新增UI/Services/Communication/CommunicationApplicationService.Presentation.cs及UI/Features/CommunicationManager/CommunicationManagerConnectionPresentation.cs。
  - 更新CommunicationApplicationService.cs、CommunicationManagerViewModel.cs、CommunicationManagerDialog.xaml和代码后置。
  - 更新Workflow/Communication.cs，所有新增显示属性均JsonIgnore，原RuntimeStatus及IsRuntimeActive语义保留。
  - 更新CommunicationRuntimeManager.cs、Operations.cs、OPC UA及TCP客户端/服务端、UDP、串口适配器的断线原因传递。
  - 更新CURRENT_TASK.md及本交接。

## 已完成

应用服务维护独立连接意图集合：加载时沿用方案已有IsEnabled规则创建首次连接意图，新建设备在发起连接前没有运行意图。真实掉线、连接失败和重试不会撤销意图；手动停止移除意图并取消重试。AutoReconnect只控制意图已开启时的非主动断线恢复，不能越过手动停止。意图仅本次运行有效，不写入方案；重启继续沿用原配置启用规则，未增加配置迁移或修改用户文件。

左侧开关增加“启用连接”标签和说明，改绑IsConnectionEnabled；事件和命令不再依据实际是否已连接反向操作。即使掉线，开关也保持开启。状态灯绑定真实RuntimeStatus，等待重连显示警告色，只有实际连接成功才显示成功色。状态文字独立显示已停止、连接中、等待重连、正在重连尚未连接、连接失败；不把开关开启当成已连接，也移除了命令内预先硬编码Connecting的显示。

TCP服务端成功显示监听中，UDP显示本地端口已绑定，普通串口显示串口已打开，避免把本地资源就绪误称为远端在线。业务错误不按断线处理，读写沿用真实设备路径，不用缓存补成功、不自动重放失败写入。

重试任务使用Stopwatch单调时钟记录真实等待截止时间。窗口的DispatcherTimer每秒只读取快照并投影剩余秒数，不负责重连调度；关闭窗口清理该计时器，后台任务继续。等待旧资源释放或重试已启动但未连上时，文字明确尚未连接。

应用服务保存最近连接错误，实际连接成功后清除；列表显示失败原因，完整内容可悬停查看。断线事件新增可选原因重载，原单参数构造函数保留。OPC UA KeepAlive/服务错误、TCP EOF/读写异常、串口接收停止、UDP接收及TCP监听异常传递各自原因，未获具体异常时显示底层会话不可用。显示快照不是设备数据缓存。

已停止但没有活动会话的故障设备，显式停止后运行时归一为Disconnected，避免“已停止”旁还亮历史故障红灯。停止不等待整批方案自动连接完成；启用仍遵循原启动准备规则。参数无效时仍允许停止有连接意图的设备。

## 构建和验收

- 已执行构建与测试：UI Release905警告0错误（26.77秒）；中间全量1159警告0错误（64.71秒）；最终全量785警告0错误（32.84秒）。最终独立UI Release1066警告0错误（20.55秒）；发布DeviceDriver/Workflow/UI主DLL与Release编译中间DLL哈希一致，相关git diff --check通过，仅换行提示。沿用原构建部署规则，未启动程序。
- 人工验收结果：遵用户前序要求，不新增/运行测试、不启动程序或Visual Studio设计器、不连接设备。实际布局、颜色和断线倒计时待人工验收，构建成功不等于运行验收通过。
- 未完成：各协议意外掉线与自动重连、开关停止、连接失败无重连、关闭/重开窗口和方案重载的人工验收。
- 已知风险或禁止改动：所有新显示属性不序列化；不修改用户设备配置、绑定、输出契约、认证策略或标定；不提交推送。空闲链路故障仍需底层或既有心跳/真实I/O检测，未注入任意协议探针。监听中/端口已绑定/串口已打开不保证对端在线。
- 下一步：启动常用Release，确认开关开启但服务端关闭时不亮绿灯，等待时间持续递减并显示原因；恢复后真实连接成功才转成功色；关闭开关后停止重试，关闭窗口不影响已启用的后台重连。
