# AI 协作交接单

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6，保留既有修改，无提交推送。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：接收17等待时，无关22及已就绪的21、23不被工作流独占或计算并发名额阻挡。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm，不修改用户方案或操作真实设备。
- 已修改文件：Plugin.SDK/Core/ToolExecutionProfile.cs；Workflow/WorkflowExecutionEngine.cs；ReceiveDataTool.cs；CommunicationReceiveDataToolTests.cs；既有接收工具方案、通信工具链实施计划及CURRENT_TASK.md。
- 已完成：新增ExternalDataWait，仅由真实来源目录确认被动设备接收后声明。就绪队列将该类节点排除普通计算名额，允许与普通独占工具共存，仍严格检查真实前驱；没有修改绑定或画布。主动设备读取、变量读取、未知来源及旧插件保持原保护。SDK新上下文重载默认转发旧方法，兼容现有工具声明。UI不承担调度，工作流不访问WPF。
- 未完成：用户原方案现场、VS设计器及高DPI人工验收。此前3项扩展自动化失败未修复，本批未重跑；B3窗口和统一执行入口仍后置。没有宣称治理所有等待工具或跨工作流设备资源互斥。
- 已执行构建与测试：标准全量Release0错误1155警告；标准UI Release0错误1064警告。最终核心UI157项、SDK含资源租约97项、脚本17项通过。接收专项26项含于核心批次。新增真实5等待者、变量读取、两级独占组装及真正依赖消费者到达后继续测试；主动／未知／变量不放宽声明测试。首轮Binary直接组装样例被预检拒绝，改为Binary解析后通过。证据artifacts/validation/receive-scheduling-20261014下receive-fixed.trx、regression.trx、sdk.trx、script.trx。
- 人工验收结果：用户确认已关闭，常用Release已更新；SDK与Workflow发布／测试哈希一致。实际Release启动探针退出0、24工具，release-toolbox.json。尚无用户新版本原方案运行反馈。
- 已知风险或禁止改动：ExternalDataWait只能用于不请求设备、不访问共享算法状态的缓存读取和外部发布等待，不可泛化到主动读取或任意异步工具。普通独占工具之间仍互斥。等待无新增超时，停止令牌仍为退出路径。数据来源变更及失效按已有读取校验处理，不回退旧会话值。
- 下一步：用户重启常用Release，先不发送TCP数据，确认17执行中，22及满足自身依赖的21→23继续；24／25仍等待17数据。发送7后确认对应分支继续，另测等待中停止。不符合时保留当前执行入口与完整日志，排查依赖图和具体来源声明，不全局关闭互斥。
