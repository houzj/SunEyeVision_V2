# 脚本输入类型词汇修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：修复脚本图像输入绑定被误报输入类型未声明。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留已有修改，不改用户方案、设备。
- 已修改文件：tools/SunEyeVision.Tool.Script/Models/ScriptInputConfig.cs；tests/SunEyeVision.Tool.Script.Tests/ScriptBindingContractB2Tests.cs及测试csproj；CURRENT_TASK.md及本记录。
- 已完成：选源写SDK规范词汇而不是CLR显示类型；真实脚本模型覆盖图像／字符串／数值／布尔选源、持久化、工作流真实快照后的输入契约检查及字段生成。不弱化预检，不给插件添加Workflow或UI依赖（仅测试工程引用Workflow）。
- 未完成：真实用户方案运行、真实VS设计器、高DPI人工验收；B3未接入。ScriptOutputConfig／SubroutineOutputConfig尚有相同显示类型写入模式，需要单独核对输出、积累和数组形状语义，不把本次输入回归称作全面输出治理完成。
- 已执行构建与测试：脚本专项13项、UI回归144项；标准全量Release0错误992警告，UI0错误1049警告。首次并行UI测试被全量构建共享目录刷新导致文件占用中断，串行重跑144项通过。常用Release已更新，最终部署脚本专项13项通过，实际启动退出0、24工具，不初始化硬件；diff检查通过。
- 人工验收结果：待用户反馈；没有实际运行用户方案。
- 已知风险或禁止改动：不猜绑、不放松不明类型为任意类型；已损坏的类型若未随持久化来源恢复需用户重新选图像来源。
- 下一步：重新打开常用Release，先运行原方案；若旧输入仍保留非法类型，重新选图像来源后再试。证据目录artifacts/validation/script-input-vocabulary-20261014（script-input.trx、deployed-script.trx、ui-regression.trx、release-toolbox.json）。
