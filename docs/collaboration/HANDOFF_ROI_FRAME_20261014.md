# 图像谱系和圆圆错帧门禁交接

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：建立可信输入帧谱系，阻止已知旧图像矩阵用于新图像。
- 允许修改目录：src/Plugin.SDK、tools、tests、docs。
- 已修改文件：ImageFrameProvenance.cs、ToolExecutor.cs、PositionCorrectionResolution.cs；ImageLoad／ImageFilter／Threshold／ImageThreshold／Morphology／ShapeMatch的工具类；ImageFrameProvenanceTests.cs、CorrectionEditingTests.cs；主方案、CURRENT_TASK.md及本交接。
- 已完成：四条同步异步入口记录元数据；图像源新帧与显式保坐标传播；圆圆已知错帧拒绝、当前帧重新匹配恢复、查询跨代等待；未知谱系兼容并明确未验证。弱元数据不改变输出契约或持有图像。
- 未完成：预览Bitmap谱系桥接、运行轮次与模板基准版本、统一撤销、其他位置修正工具接入。未知插件和直接Run调用保持兼容未验证；原地修改图像但绕过声明不能保证。
- 已执行构建与测试：SDK联合76项、圆圆15项、UI172项通过；全量Release0错误1089警告、UI0错误1064警告；实际Release探针退出0，SDK测试发布哈希一致，diff检查通过。证据artifacts/validation/roi-frame-20261014。
- 人工验收结果：真实旧方案和图像、VS设计器及高DPI待用户验收。
- 已知风险或禁止改动：只允许明确保坐标工具传播，不把裁剪／变形输出当同坐标；不得将兼容未验证写成已验证。不改plans/plan.json、真实方案和设备，不提前启用B3；前批3项扩展自动化失败未修复且本批未重跑。
- 下一步：补主预览图像与帧身份桥接、编辑上下文身份，再完善轮次／基准及历史。
