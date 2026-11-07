# 图像选择器候选刷新回归交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无提交推送。
- 任务目标：修复图像绑定运行／刷新后消失导致算法收到null图像。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留所有既有修改，不改用户方案或设备。
- 已修改文件：src/Plugin.SDK/UI/Controls/ImageSourceSelector.cs；src/Plugin.SDK/Themes/Generic.xaml；tests/SunEyeVision.UI.Tests/ToolEditorCommitTests.cs；CURRENT_TASK.md及本记录。
- 已完成：真实模板红测证明ListBox.SelectedItem双向绑定把候选重建临时空选择反写模型。改为单向展示，保留既有明确用户选源事件写入；SelectedDataSource用SetCurrentValue保持外部绑定。范围为共享图像选择器全部使用工具，不改变工作流调度或运行图像所有权。
- 未完成：用户真实方案轮廓匹配及连接触发、VS设计器、高DPI人工验收；B3仍未接入。
- 已执行构建与测试：修改前真实模板用例失败于刷新后ImageSource为空（before.trx），修复后控件专项7项通过；最终UI／工具链145项、脚本14项通过。全量Release0错误1152警告，UI0错误1064警告。常用Release已部署，测试与发布SDK哈希一致，实际启动退出0、24工具，不初始化通信；diff检查通过。
- 人工验收结果：待用户反馈，没有运行用户真实方案或连接设备。
- 已知风险或禁止改动：早期SetValue直接丢绑定的假设未被该红测证实，实际失败在模板刷新回写空值，已澄清；不能把图像问题与接收无完整报文混为一谈，不用旧报文兜底，不伪造运行输入；已清空引用不能猜源恢复。
- 下一步：用户重新打开常用Release，对已空图像来源重新选一次后运行，观察刷新后仍保留。接收节点无报文单独核对设备接收状态；不修改接收语义掩盖错误。
- 证据：artifacts/validation/image-selector-binding-20261014（before.trx、after.trx、regression.trx、script.trx、release-toolbox.json）。
