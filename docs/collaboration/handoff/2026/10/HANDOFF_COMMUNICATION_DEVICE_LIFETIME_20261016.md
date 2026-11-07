# 通讯设备取消与方案隔离交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，未创建提交或推送，保留所有并存修改。
- 任务目标：所有已接入协议在切换方案、删除或禁用设备时取消旧重连；迟到操作不能重新建立有效连接或污染新方案设备状态。
- 允许修改目录：src/UI通讯管理与应用服务、src/DeviceDriver通讯运行时、docs/collaboration。
- 已修改文件：src/UI/Services/Communication/CommunicationApplicationService.cs、CommunicationApplicationService.Presentation.cs；src/UI/Features/CommunicationManager/CommunicationManagerViewModel.cs；src/DeviceDriver/Communications/Runtime/CommunicationRuntimeManager.cs；本批协作文档。

## 已完成

原删除入口在取得设备门之后才取消重连，可能被原生连接阻挡；原启动和手动连接只有方案令牌，禁用设备不能及时取消其建连。现在设备生命周期以当前设备对象关联方案令牌，启动、手动连接、自动重连和OPC UA监视统一接入。删除请求在等待设备门前取消并撤销资格；禁用属性事件同步取消。待删除设备被标记为退出状态，排队或新到的开启策略不得恢复它。设备生命周期取消源在服务释放时统一回收。

停止操作在等待重连收尾后，再核对方案令牌与当前设备对象，并在校验保护内调用运行时移除适配器，避免仅凭相同设备ID停止新方案连接。等待中的旧手动连接同样复核原请求令牌，不能撤销后到的删除或禁用。手动连接等待对应方案清理完成后才进入设备门。

切换期间关闭重连调度，完成旧任务、轮询和会话清理后才开放新方案连接。列表明确显示正在切换方案，不沿用旧运行时的连接、倒计时和错误。运行时清理旧快照；真实断线事件携带其状态快照，应用服务以快照引用及方案令牌拒绝跨代事件。快照身份检查不探测设备、不获取运行时状态锁；状态发布回调也不获取应用锁，避免与同步取消形成反向锁序。

重连异常只有在设备仍属当前方案且未取消时才能写入错误；轮询启动在身份和取消核对内完成；OPC UA监视使用设备令牌。旧窗口的连接、失败和删除续作核对原方案与设备对象，不启动新方案轮询、不删除新方案配置。

全部协议使用原共享应用服务和运行时入口，包括TCP客户端、TCP服务端、UDP、串口、Modbus TCP/RTU、S7、OPC UA和CIP。原配置格式、已有绑定、输出契约和公开事件构造函数保持兼容，没有缓存值补成功、业务写入重放或标定修改。

## 已执行构建与检查

- 初步独立UI Release：768警告、0错误，24.54秒。
- 中间全量Release：1159警告、0错误，73.40秒；785警告、0错误，38.94秒；821警告、0错误，43.61秒。
- 最终全量Release：dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly；821警告、0错误，49.57秒。
- 最终独立UI Release：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -clp:ErrorsOnly；1066警告、0错误，19.56秒。
- Release/SunEyeVision_v1.0.0/SunEyeVision中的UI与DeviceDriver主DLL SHA256和build_temp编译DLL一致；相关源码git diff --check通过，仅有既有换行提示。

## 未完成与人工验收结果

用户此前要求不运行测试和程序，本批未新增或运行测试、未启动Release或Visual Studio设计器、未连接设备。上述构建不等于真实协议与取消时序验收。

待人工验证：等待重连时删除或禁用；原生建连尚未完成时删除或禁用；切换到包含相同设备ID的另一方案；连续快速切换多个方案；删除等待期间切换AutoReconnect；服务器稍后恢复时旧设备不得复活，新方案设备状态与实际会话一致。

## 已知风险或禁止改动

取消立即撤销有效连接资格；不可中断的原生调用仍可能等待驱动返回后释放资源，不能承诺物理连接瞬时结束。已有运行时按适配器身份与取消令牌拒绝晚到结果，重连收尾仅释放自身所有者的连接。不要恢复仅按设备ID清理旧任务，也不要让旧窗口回调停止新方案的轮询。

## 下一步

根据人工验收的原始日志和实际会话核对跨方案、禁用及删除时序；不以界面旧值或缓存值证明连接成功。其它算法与标定不在本批范围。
