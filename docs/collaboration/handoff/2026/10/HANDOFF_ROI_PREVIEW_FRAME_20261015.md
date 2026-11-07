# 实际预览帧桥接交接

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：实际预览切图但参数图像来源不变时，阻止使用错误帧进行位置修正编辑。
- 允许修改目录：src/Plugin.SDK、src/UI、tools/SunEyeVision.Tool.CircleCircleMeasure、tests、docs。
- 已修改文件：PreviewFrameProvenance.cs、VisionImageToBitmapSourceConverter.cs、PositionCorrectionResolution.cs、MeasureElementsEditorControl.xaml.cs、圆圆调试控件；PreviewFrameProvenanceTests.cs、ImageFrameProvenanceTests.cs、CorrectionEditingTests.cs；主方案、CURRENT_TASK.md及本交接。
- 已完成：统一转换器附加转换时帧身份并拒绝转换中换帧；SDK UI弱位图元数据；纯身份预览校验；圆圆实际错帧暂停、拖动回滚、正确帧恢复、空图暂停和未知位图兼容。执行快照不被预览校验改变。
- 未完成：未知／直接Bitmap旁路验证、无通知的原地改像素、运行轮次／模板基准版本、统一事务历史、历史图层混帧治理及其他工具推广。
- 已执行构建与测试：全量Release0错误1041警告、UI0错误1064警告；最终circle-final.trx为15通过、sdk.trx为76通过、ui.trx为174通过；实际Release探针退出0，SDK发布测试哈希一致，diff检查通过。证据artifacts/validation/roi-preview-frame-20261015。
- 人工验收结果：真实旧方案、图像场景、VS设计器及高DPI待用户验收。
- 已知风险或禁止改动：兼容未验证不能宣称已验证；预览错帧只暂停编辑，不替代后台输入校验。不修改plans/plan.json、真实方案和设备，不提前启用B3；此前3项扩展自动化失败未修复且本批未重跑。
- 下一步：补轮次／基准身份及统一历史，再推广其他ROI工具；保留绑定不改参数规则。
