# 脚本窗口输入投影契约修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：修复脚本输入显示图像但执行仍报类型未声明的完整窗口提交链。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留全部已有修改，不改用户方案与设备。
- 已修改文件：src/Workflow/BindingRelationCatalog.cs；tests/SunEyeVision.Tool.Script.Tests/ScriptBindingContractB2Tests.cs；CURRENT_TASK.md及本记录。
- 已完成：通用窗口按input1生成ParamSettings副本，不能按顶层不存在的CLR属性推断其类型；当没有同名顶层属性且唯一匹配Input:行时复用真实行AllowedTypes及Active。不绕过预检、不忽略pending。回归含真实脚本行、窗口副本、旧pending标记、正式复查和真实快照EnsureExecutable。
- 未完成：用户真实方案、VS设计器和高DPI人工验收；B3未接入。重复投影向单一权威／稳定字段ID迁移和此前记录的输出词汇与集合形状治理不在完成声明内。
- 已执行构建与测试：隔离和标准脚本14项专项、UI144项回归均通过；全量Release0错误1152警告，UI0错误964警告。用户关闭后常用Release已更新，测试与发布Workflow哈希一致，启动退出0、24工具，不初始化通信；diff检查通过。
- 人工验收结果：待用户反馈，没有实际运行用户方案。
- 已知风险或禁止改动：不把未知类型当object放行，不自动猜源；名字重复不会猜匹配；本次不改变用户绑定值或跳过SDK运行预检。
- 下一步：用户重新打开原方案直接执行，验证旧pending经正式复查清除；仍异常则保留现场继续检查，不要求反复盲目重绑。
- 证据：artifacts/validation/script-row-projection-20261014（projection.trx、ui-regression.trx、deployed-projection.trx、deployed-ui.trx、release-toolbox.json）。
