# AI 协作交接单

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6，保留既有修改，无提交推送。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：TCP／UDP／串口按节点逐次接收，不再跨轮复用已消费报文。
- 允许修改目录：本工作区SDK、Workflow、ReceiveData插件、测试及docs；不改用户方案、不操作真实设备。
- 已修改文件：新增CommunicationReceiveConsumption.cs及CommunicationReceiveConsumptionTests.cs；修改CommunicationReceiveService、RuntimeVariableReceiveService、ToolExecutor、ExecutionContext、ReceiveDataTool、WorkflowRunScope／Session、WorkflowExecutionEngine及接收工具测试；同步消费方案、接收基线、来源方案、实施计划和CURRENT_TASK。
- 已完成：有界报文缓冲和独立游标；锁内提交接收工具内存输出后推进；失败／取消不消费；同消费者并发拒绝。稳定根流程／节点及子程序调用路径，窗口和工作流入口共享进度。报文序号区分相同内容的多次发送；提前报文按序。会话变化清理并拒绝旧等待，事件不消费游标，主动读／变量不变。保留ExternalDataWait隔离。分析显示序号、会话及时间。
- 未完成：用户原方案及等待期间结果展示、VS设计器、高DPI人工验收。节点删除后的逐个消费者自动回收未接入，使用总量上限保护并明确报错；溢出恢复尚为重载方案，无独立跳过／丢弃按钮。前批3项扩展自动化失败未修复，本批未重跑，B3未启用。
- 已执行构建与测试：最终标准全量Release0错误1255警告；UI Release0错误1064警告。核心UI167项、SDK含会话资源103项、脚本17项通过。消费10项、接收26项在核心中，不重复累计。真实TCP双报文两轮顺序、同一工作流第二轮等待新7、多等待无关分支、入口共享／跨流程隔离、子程序稳定路径、取消／提交异常和会话检查及条数／字节溢出均由对应回归覆盖。首次旧最新值断言失败已改为顺序契约；初轮旧同步重复读取测试曾挂起并中止，不作为新实现通过证据。证据artifacts/validation/receive-consumption-20261014下regression.trx、sdk.trx、script.trx、receive-ordered.trx。
- 人工验收结果：用户确认已关闭，常用Release已更新；SDK及Workflow测试／发布哈希一致，实际Release启动探针退出0、24工具。用户新版本现场反馈待提供。
- 已知风险或禁止改动：每来源256条／4 MiB，总消费者4096；首次消费从会话序号1开始，早期已淘汰即明确缺口，不静默跳最新。断线／配置变化／方案重置清运行状态；单纯停止不重置游标。ReadAsync保留诊断当前值接口，正式被动接收必须ConsumeAsync。工具内存结果提交不是端到端业务恰好一次事务，不能宣称对端副作用或进程崩溃事务保证。提交后的下游失败不返还报文，避免重放。
- 下一步：用户重启原方案，发7完成第一轮，第二轮不发应等待，再发同内容7才继续；另测提前多报文、等待中停止和无关22／21→23运行。结果与预期不符时保留入口和日志，不能回退到共享缓存复用。
