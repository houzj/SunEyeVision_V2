# 通讯管理器窗口生命周期交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入；未创建提交或推送，保留工作区已有修改。
- 任务目标：所有已接入协议共享窗口外的重连生命周期，关闭重开不创建重复任务、不重置倒计时，显示仍以实际连接状态为准。
- 允许修改目录：src/UI内本任务涉及的通讯管理、服务初始化和窗口入口；docs/collaboration。
- 已修改文件：CommunicationManagerViewModel.cs、CommunicationManagerConnectionPresentation.cs、CommunicationManagerHeartbeats.cs、CommunicationApplicationService.cs、NodeDisplayAdapterConfig.cs、MainWindowViewModel.cs及本批协作文档。上述源码位于src/UI原有目录，保留前序改动。

## 已完成

窗口创建时固定引用进程级CommunicationApplicationService及其运行时和轮询协调器。共享服务、运行时的延迟初始化使用同一个锁，避免并发创建多个后台服务。关闭窗口只停止显示计时器和释放订阅，不取消后台重连；再次打开从原服务读取实际会话、失败原因及原重试截止时间，不重新发起连接。

DisposeAsync增加幂等保护。关闭前已排队的断线、状态、接收、轮询批次、OPC UA更新和心跳回调核对窗口释放标记后退出。UI断线回调不再停止共享轮询，轮询停止仍由应用服务的真实断线状态处理，避免旧窗口回调干扰新连接。对话框创建或显示失败时同样释放已创建ViewModel的订阅。

关闭OPC UA窗口选择不保存时，恢复Settings字典可能改变AutoReconnect而不触发属性事件；现仅在恢复前后该值改变时调用公共策略核对入口，沿用原按设备合并、版本核对和重试去重机制。正常重开不调用该入口。放弃未保存设置仍按用户选择恢复，并非强行保持刚刚放弃的策略。

列表开关继续仅反映真实Connected，连接中和等待重连时保持关闭；状态文字、倒计时和失败原因来自同一服务快照。协议特定的监听、端口绑定和串口打开含义保持原规则。没有修改配置格式、输出和绑定，没有缓存补成功或重放写入；标定未改。

## 未完成与人工验收

遵此前用户限制，本批不新增或运行测试，不启动Release程序、Visual Studio设计器或真实设备。人工验收尚未执行，不将编译成功等同于各协议真实通信通过。

建议人工确认：等待重连时关闭窗口，隔数秒重开，倒计时按剩余时间继续；断网期间连续开关窗口不增加重试任务；窗口关闭期间恢复服务器，重开后真实已连接且开关开启；等待时关闭AutoReconnect立即取消，重新开启仅恢复一个任务；快速切换并关闭重开后状态与最终策略一致；OPC UA放弃设置时恢复原策略。

## 构建与检查

- 首轮全量Release：1159警告，0错误，63.21秒。
- 最终全量Release：dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly；785警告，0错误，41.55秒。
- 独立UI Release：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -clp:ErrorsOnly；1066警告，0错误，21.38秒。
- Release/SunEyeVision_v1.0.0/SunEyeVision中的UI和DeviceDriver主DLL与build_temp编译中间DLL的SHA256一致。
- 相关文件git diff --check通过，仅提示既有换行格式警告。

## 已知风险与下一步

真实网络断开、驱动取消耗时、设计时布局及跨协议运行验收仍待用户执行。应用退出和切换方案仍按原服务生命周期停止后台任务，不属于单纯关闭通讯窗口。不要让窗口释放逻辑再次调用共享服务Dispose，也不要在窗口构造时添加无条件Connect或重连任务。下一步按人工现象核对原始日志与实际会话，不以旧点值判断连接成功。
