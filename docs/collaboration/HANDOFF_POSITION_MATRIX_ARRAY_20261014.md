# 位置修正矩阵数组契约回归交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，无提交推送。
- 任务目标：修复绑定轮廓匹配矩阵数组后预览驱动执行误报位置修正不适配。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留已有改动，不修改用户方案或设备。
- 已修改文件：src/Plugin.SDK/Execution/Parameters/BindingInputCatalog.cs；tests/SunEyeVision.UI.Tests/BindingAdaptationB2Tests.cs；本交接及CURRENT_TASK.md。
- 已完成：专用位置修正输入接受AffineMatrix和AffineMatrix[]，与PositionCorrectionResolver取首矩阵语义一致；不改变通用类型过滤、不弱化其他标量输入、不自动替换绑定。覆盖单值和集合提交及冻结参数校验。
- 未完成：真实用户方案预览驱动执行、VS设计器、高DPI人工验收；B3仍未接入。
- 已执行构建与测试：70项针对性、143项完整回归通过（重叠）；标准全量Release0错误1001警告，UI0错误949警告；用户关闭后常用目录已更新，实际启动探针退出0、24工具。测试与发布SDK哈希一致，diff检查通过。证据artifacts/validation/position-matrix-array-20261014/matrix.trx、regression.trx、release-toolbox.json。
- 人工验收结果：待用户反馈，不声称已经运行用户真实方案。
- 已知风险或禁止改动：误解绑已经清空的引用不能自动恢复；不猜来源、不放宽普通数组到标量、不引入旧报文锁定或时效机制。
- 下一步：用户重新打开常用Release，空引用重新选择轮廓匹配矩阵[]，通过预览图驱动原工作流复测；收敛回归后再推进B3。
