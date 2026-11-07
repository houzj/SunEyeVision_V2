# 接收读取与事件分支一致性修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：保证接收工具独立于事件条件读取同一连接当前数据，修复可复现的展示配置误清空记录及误导报错。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留已有修改，不操作用户方案或真实设备。
- 已修改文件：src/UI/Services/Communication/CommunicationApplicationService.cs；src/Plugin.SDK/Models/Communication/CommunicationReceiveService.cs；tests/SunEyeVision.UI.Tests/CommunicationReceiveDataToolTests.cs；既有接收方案、工具链计划、CURRENT_TASK.md及本交接。
- 已完成：共享接收发布仍先于事件判断；设备Name／Description不再进入失效签名，来源显示变化不判为数据配置变化，主动读取校验采用相同判据。真实TCP无事件手动执行原可用，红测证明设备展示编辑后丢数据，修复后保留同一记录；端口变化、断线仍使旧数据不可读。无记录提示设备、当前无数据及已知清空原因。
- 未完成：用户现场原因的唯一性确认、真实方案／设计器／高DPI验收，以及3项扩展自动化测试失败；B3仍未接入。更广泛目标配置失效边界尚未全面治理。
- 已执行构建与测试：修改前真实TCP红测抛原“完整报文”错误；修复后接收专项18项、核心UI148项、SDK95项、脚本17项通过，批次存在重叠。全量Release0错误1155警告，UI0错误1064警告。扩展联合170通过3失败，三项独立重跑仍失败：两项SolutionManager未初始化，一项启动等待超时，未修复，不声称基线已证明相同失败。常用Release已更新，SDK和UI测试／发布哈希一致；实际启动退出0、24工具，diff检查通过。
- 人工验收结果：待用户反馈，没有读取或写入真实设备；TCP测试仅使用本机模拟端。
- 已知风险或禁止改动：事件面板可能保留历史显示，不能据此认定当前记录一定有效。没有数据不等于格式错误；不再读socket、不伪造成功、不返回断线前旧数据、不引入触发样本锁定或有效期。展示编辑红测是已证明缺陷，但不是用户现场唯一原因的证明。
- 下一步：用户重开常用Release，用相同连接收到数据后手动执行接收工具，验证不依赖事件与触发条件；扩展自动化三项失败另行补全宿主前提并定位启动超时后复验，不能直接跳过。
- 证据：artifacts/validation/receive-decoupling-20261014（before.trx、after.trx、core-regression.trx、regression.trx、automation-isolated.trx、sdk.trx、script.trx、release-toolbox.json）。
