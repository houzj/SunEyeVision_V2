# 常量确认重复持久化修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：修复P1X等合法常量确认后仍无法解除旧重绑错误，继续区分TCP尚未定位的问题。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留全部既有修改，不改用户方案或操作设备。
- 已修改文件：src/Plugin.SDK/UI/Controls/ToolDebugControlBase.cs；tests/SunEyeVision.UI.Tests/ToolEditorCommitTests.cs；CURRENT_TASK.md及本交接。
- 已完成：真实点测量窗口复现常量容器先写设置、内部ParamBinding按空路径又删除设置的重复提交。独立绑定遍历不进入ConfigSetting，子控件交由所属容器提交。确认后四坐标旧标记经正式常量校验解除，冻结预检及SDK运行参数解析通过；不自动接受已解绑后显示的默认常量。
- 未完成：TCP当前会话无接收记录的现场原因、用户实际方案／VS设计器／高DPI验收、前批3项扩展自动化失败；B3未启用。
- 已执行构建与测试：修改前真实窗口红测失败且输出控件全为Constant、提交Settings为空；修改后控件8项、核心UI149项、SDK95项、脚本17项通过（批次重叠不相加）。全量Release0错误1152警告，UI0错误1028警告，常用Release已更新；SDK测试发布哈希一致，启动退出0、24工具，diff检查通过。
- 人工验收结果：待用户反馈。只读原方案发现P1X／P1Y／P2X／P2Y旧标记及空ParamSettings，没有修改或重存方案。
- 已知风险或禁止改动：不得以合法默认值自动抹掉重绑意图；必须用户明确确认常量或合法重绑。TCP报错仍未定位，不能仅凭旧事件界面的7／OK认定新连接已收数据，也不能据空记录断言设备没发数据。本批不改变接收服务。
- 下一步：用户用新Release重新打开点-点测量，确认当前四常量并保存，然后重跑。TCP保留同一连接的新接收时间与手动工具读取结果，继续追踪实际发布／失效链。
- 证据：artifacts/validation/constant-tcp-diagnosis-20261014（constant-confirm.trx、constant-diagnostic.trx、constant-fixed.trx、regression.trx、sdk.trx、script.trx、release-toolbox.json）。
