# ROI编辑上下文失效交接

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：补切图及同数值矩阵换来源时的编辑取消／回滚。
- 允许修改目录：src/Plugin.SDK、tools/SunEyeVision.Tool.CircleCircleMeasure、tests、docs。
- 已修改文件：MeasureElementsEditorControl.xaml.cs、CircleCircleMeasureToolDebugControl.xaml.cs、CorrectionEditingTests.cs、CorrectionPreviewLifecycleTests.cs、主方案、CURRENT_TASK.md及本交接。
- 已完成：SourceImage变更订阅及解除、换画布／关闭取消编辑；圆圆绑定与启用／图像来源变化取消交互；UI线程及迟到事件防护；真实控件与宿主参数回归。
- 未完成：图像帧／运行轮次／基准版本与矩阵配对门禁、统一事务和撤销、完整历史几何快照及其他ROI工具推广。同对象位图内容修改和同来源重建模板不能仅靠本批事件识别。
- 已执行构建与测试：全量Release0错误1041警告、UI0错误1064警告；圆圆13、SDK59、UI172通过；Release探针退出0；SDK哈希一致。证据artifacts/validation/roi-context-20261014。circle-final.trx保留一次WPF并行资源异常，串行集合后circle-verified.trx通过。
- 人工验收结果：真实图像、旧方案、VS设计器及高DPI待验收。
- 已知风险或禁止改动：切图取消旧编辑不代表新矩阵已经对应新图像；不得宣称完整配对完成。不改plans/plan.json和真实方案，不连接设备、不提前启用B3；此前3项扩展自动化失败未修复且本批未重跑。
- 下一步：补图像与匹配输出的版本身份及有效性门禁，再完善编辑事务历史和推广。
