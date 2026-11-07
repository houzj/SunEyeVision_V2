# 输入模式与设计子程序端口契约修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：处理点-点测量按参数仍被旧绑定阻止、子程序内部图像契约不可用两条回归。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留既有修改，不修改用户方案或操作设备。
- 已修改文件：SDK的IBindingInputProvider.cs、BindingInputCatalog.cs、ToolExecutor.cs；Workflow的BindingRelationCatalog.cs、OutputBindingChangeService.cs；PointPointMeasureParameters.cs；UI测试csproj及ModeAndSubroutineBindingRegressionTests.cs；CURRENT_TASK.md及本记录。
- 已完成：模型声明输入分支活动性，点-点测量按图形／按参数对称启用，停用旧标记不拦截、参数解析不读取停用绑定，切回原模式仍需处理旧标记；错误显示输入键。设计子程序父节点查找优先节点SubroutineId、为空兼容参数ID，修复执行注入前找不到输入端口的问题。
- 未完成：用户真实方案运行、VS设计器及高DPI人工验收；其他工具输入模式的完整适配不在本批完成声明内；B3未接入。
- 已执行构建与测试：新增2项正反例通过，完整UI147项、SDK95项、脚本17项通过。全量Release0错误1155警告，UI0错误1064警告；常用目录更新，SDK及Workflow测试／发布哈希分别一致；启动退出0、24工具，不初始化硬件；diff检查通过。
- 人工验收结果：待用户反馈；原错误未带输入键，不能认定用户具体失配键，若仍有启用分支错误需按新日志继续定位。
- 已知风险或禁止改动：不清除停用旧错误伪装修复、不忽略未知类型、不从WPF控件决定活动性、不恢复旧报文或时效机制。接收无完整报文是独立状态。
- 下一步：重新打开常用Release验证原方案；若点测量仍报错，保留新日志中的具体输入键。通信无报文另查，不以旧样本兜底。
- 证据：artifacts/validation/mode-subroutine-binding-20261014（mode-child.trx、regression.trx、sdk.trx、script.trx、release-toolbox.json）。
