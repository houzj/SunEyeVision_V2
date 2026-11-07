# AI 协作交接单

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1，保留既有修改，无提交推送。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入。
- 任务目标：推进ROI优化首批，复现并修复圆—圆调试窗口与执行使用不同矩阵的问题。不将首批宣称为整套R1至R5完成。
- 允许修改目录：本工作区Plugin.SDK、CircleCircleMeasure插件、测试及docs；不改plans/plan.json、用户方案或真实设备。
- 已修改文件：新增PositionCorrectionResolution.cs、PositionCorrectionResolutionTests.cs、CorrectionPreviewLifecycleTests.cs；修改共享MeasureEditorControl、圆圆调试控件及XAML、CircleCircleMeasureTool／Results；同步主方案和CURRENT_TASK。
- 已完成：真实控件红测缓存180／实时-50不一致；实时解析不回退缓存，明确矩阵状态及值复制；圆圆开关、绑定、上游结果更新重查，关闭解订阅。无效时本编辑器隐藏图形并禁逆编辑及删除。圆圆单节点执行副本同源，处理相同路径判等不更新缓存的问题，原参数及圆心／半径不变。Run与结果固定本次矩阵，不支持非保形变换时明确失败。
- 未完成：R1用户原方案三层图像身份与实际截图复现；R2其他工具接入及实图验收；R3坐标归属持久化、首次绑定／换源选择、转换预览、原子提交、撤销与旧版显式确认；R4完整几何／图像／轮次上下文；R5全工具推广。当前先画后绑仍沿用旧解释，不能宣称新转换交互已上线。
- 已执行构建与测试：最终标准全量Release0错误996警告；UI0错误1013警告。UI／通信绑定联合172项、SDK联合160项、圆圆插件8项、脚本17项通过，各批重叠不重复累计。隔离SDK修正36项和圆圆8项为中间批次；最终以sdk-final.trx、ui-regression.trx、circle-final-verified.trx及script.trx为准。preview-red.trx证明原缺陷，preview-green.trx为首轮修复；circle-final.trx曾抓到执行副本缓存不更新，修复后circle-verified.trx及最终8项通过。git diff --check通过。
- 人工验收结果：用户确认关闭，常用Release已更新；SDK及UI测试／发布哈希一致，实际Release探针退出0、24工具。真实旧方案、图像、设计器和高DPI未验收，不能以控件测试代替现场。证据artifacts/validation/roi-correction-20261014。
- 已知风险或禁止改动：不自动转换旧ROI、不在刷新时改参数、不重复修正拟合坐标。当前值快照只冻结矩阵，参数CopyFrom仍为SDK浅复制，不是完整设计参数深冻结；后续R4须治理几何与配置快照。图像／结果轮次不匹配尚无完整门禁。共享不可用保护默认开启，只有圆圆接入，其他工具未全局改变。前批3项扩展自动化失败未解决，本批未重跑。
- 下一步：继续R3显式参考关系事务，再完善R4与R5；部署版本可先验证已有绑定窗口打开／切换开关／上游更新的图形位置及空匹配提示。未锁定截图唯一原因前，保留图像身份与矩阵诊断，不将缓存修复当作所有错位已解决。
