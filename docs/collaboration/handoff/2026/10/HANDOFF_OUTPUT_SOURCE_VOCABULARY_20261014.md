# 输出来源规范类型清理交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：继续收敛前批记录的脚本／子程序输出选源误写CLR显示类型名问题。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留已有修改，不改用户方案与设备。
- 已修改文件：src/Plugin.SDK/Models/SubroutineOutputConfig.cs；tools/SunEyeVision.Tool.Script/Models/ScriptOutputConfig.cs；tests/SunEyeVision.Tool.Script.Tests/ScriptBindingContractB2Tests.cs；CURRENT_TASK.md及本记录。
- 已完成：两处选源写规范SDK词汇，覆盖图像、数值、字符串输出的类型可识别、脚本字段生成、真实冻结契约和字段身份保留；模型范围同类直接误写扫描无剩余命中。不改变积累或数组提升行为。
- 未完成：完整数组／积累形状治理、重复投影单一权威迁移、B3窗口与各执行入口接入；用户真实方案、设计器、高DPI人工验收待反馈。
- 已执行构建与测试：最终脚本17项、UI145项通过；标准全量Release0错误992警告，UI0错误1064警告。SDK测试发布初次不同，标准重建后二者哈希一致，17＋145项重新通过；最终常用Release启动退出0、24工具。diff检查通过，不初始化硬件。
- 人工验收结果：待用户反馈，没有运行用户真实方案。
- 已知风险或禁止改动：不将本批词汇修复当全面集合契约治理，不绕过未知类型校验、不猜绑、不恢复旧报文缓存语义。
- 下一步：用户继续验证原方案；收敛回归后再按计划推进B3，而非同时切换全部执行入口。
- 证据：artifacts/validation/output-source-vocabulary-20261014（output.trx、standard-output.trx、standard-ui.trx、release-toolbox.json）。
