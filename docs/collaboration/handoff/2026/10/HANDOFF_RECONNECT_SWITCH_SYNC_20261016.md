# 五次重连停止与开关同步交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，未提交推送，保留全部并存修改。
- 任务目标：五次自动重连失败后同步关闭开关，再次开启直接开始新一轮，所有协议一致。
- 允许修改目录：src/UI/Services/Communication、docs/collaboration。
- 已修改文件：CommunicationApplicationService.cs、CommunicationApplicationService.Presentation.cs、CURRENT_TASK.md、COMMUNICATION_CLOSEOUT_SCOPE_20261016.md及本交接单。
- 已完成：第五次失败时在当前设备和任务身份核对后关闭实际AutoReconnect属性，复用属性策略取消旧任务；停止摘要和失败原因保留。用户再次开启通过原唯一策略入口等待旧任务清理、重置预算并重连。
- 未完成：真实设备和界面时序验收。
- 已执行构建与测试：UI Release --no-restore -m:1 -clp:ErrorsOnly，768警告0错误，22.73秒；发布UI DLL与编译DLL SHA256一致。未运行测试、程序或设计器。
- 人工验收结果：待确认第五次失败后开关关闭、无第六次；再次打开无需其他操作即可开始新一轮；快速开关、窗口重开、方案切换期间旧任务不影响新任务。覆盖各已接入协议。
- 已知风险或禁止改动：按用户最新要求修改内存中既有autoReconnect设置为关闭，沿用原保存机制，不主动写方案文件；不新增字段、不改绑定、不用缓存值补成功、不重放写入、不动标定。
- 下一步：人工验收上述交互；开发回到既有HALCON算法优化方案，不继续扩展通讯功能。
