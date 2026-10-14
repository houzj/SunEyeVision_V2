# 子程序输入绑定回归交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：修复子程序矩阵数组输入绑定运行时被误清空。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留已有改动，不改用户方案和设备。
- 已修改文件：src/Plugin.SDK/Models/SubroutineInputConfig.cs、src/Plugin.SDK/Execution/Parameters/BindingInputCatalog.cs、tests/SunEyeVision.UI.Tests/BindingAdaptationB2Tests.cs、CURRENT_TASK.md及本记录。
- 已完成：选源写SDK规范类型词汇而不是CLR显示名；子程序Transform输入承认数组原样传输语义，不自动取第一个矩阵、不改变通用标量校验；覆盖图像规范词汇及矩阵绑定复查、快照和只读执行预检。
- 未完成：用户真实方案运行、真实VS设计器和高DPI人工验收；B3未接入，子程序输出／积累输出形状全面治理不在本批完成声明中。
- 已执行构建与测试：34项专项、144项完整回归及最终标准重建144项通过（批次重叠不相加）；全量Release0错误1160警告，UI0错误1064警告。常用Release启动退出0、24工具；测试与发布SDK哈希一致，diff检查通过。证据artifacts/validation/subroutine-matrix-array-20261014/subroutine.trx、regression.trx、standard-regression.trx、release-toolbox.json。
- 人工验收结果：待用户反馈，没有实际运行用户方案或连接通信设备。
- 已知风险或禁止改动：已经清空的引用需用户重新选择，不能自动猜源；不将矩阵数组压成单值，不放宽脚本与普通工具输入。
- 下一步：重新打开常用Release，恢复已清空的输入绑定后执行原工作流验证。
