# B08 数值引用整控件实施设计（2026-11-02）

## 产品基线及原清单
唯一候选目录 blob-reference-chain-20261101，分支 codex/b08-numeric-reference-20261102，精确基线93b9b683f1dd849b89f6e776839fe1a79b028f66；旧候选5b9387a4完整保留，不新增worktree。已读CURRENT_TASK/取证约定及原A00–A23。完整逐项矩阵见 B08_NUMERIC_REFERENCE_WHOLE_MATRIX_20261102.json，保留原要求与oracle，历史current acceptance的914b2173不冒称当前版本证据。

## 整批需求与证据决策
本批是同一真实Blob窗口的完整已支持组合：低阈值常量/输出引用/变量（含固定/动态索引），面积/圆度/矩形度两端，实际本地ROI或已具备的逐叶继承，正式图文样式；贯通私有调试、当前值/解绑、未失焦合法/非法输入原子捕获、确认取消、历史、保存重开对象执行、来源失效恢复、帧/旧owner退役。既有完整专项复用，不以一个按钮或测试计数代表整体。

原依据：B08_NATIVE_DECISION_20261030/B08解绑与触发文本迁移决策-20261030.md及普通参数回读SDK桥、具体Blob参数对象分册；B08_ORIGINAL_INDEX_PROTOCOL/INTEGER_INDEX_PATH/FLOAT_TARGET_PARSE/SHAPE_FILTER_FLOAT、来源资格分册、B08_ORIGINAL_REFERENCE_PREFERENCE_BOUNDARY_20261031.md。原恢复值/所有分支时序未证；禁止把默认100、最近结果或旧常量标成VM恢复值。原方案偏好UserData读写有静态链，磁盘/Dirty/Undo未证且本方无正式持久owner，保留会话临时状态，不扩平台。

当前正式证据分层：低阈值真实窗9组（B08_REFERENCE_WINDOW_CHAIN_FORMAL_20261101）、三筛选范围各16组（BLOB_RANGE_PRODUCTION_FORMAL_20261101）、C07中英真实树事件（C07_BLOB_EXPANDED_REFERENCE_FORMAL_20261101）、本地/继承/只读预览以及图文/同次ROI结果显示正式回执。它们证明各自范围，不能补写成同窗混合或整控件全24项已经通过。

## 完整生产调用链与单一所有权
1. RuntimeToolConfiguration.Fields是关系/常量单owner；ReadCurrentParameter的不可变参数当前值是已接纳纯值槽，不是第二配置或结果缓存。DisplaySettings只编辑保存，不进入算法参数解析。
2. RuntimeNodeConfigurationControl→DeclaredSettingsView/DeclaredFieldsView→DeclaredFieldEditor/NativeRangePresenter→DeclaredFieldState原子提案；ProductionEditorContext/NodeConfigurationEditHost负责字段及整组准入、依赖图、正式发布与历史。沿现有活动字段捕获，不丢休眠pending，不让其误阻本次；重新启用仍严格验证。
3. ReferenceFieldSession/DeclaredReferenceTree/ReferenceWindowOwner提供真实前序/变量/索引候选，引用保存完整scope/node/port/member/storage/type/selection身份。五整数首项例外仅其明确合同，浮点保持StrictExactlyOne；不把Int64→Float64提升反向推广。
4. RuntimeDebugInvocation及正常AlgorithmNode执行共用冻结解析/参数接纳回执，私有DebugOwner只写原草稿代次；NodeConfigurationEditHost正式接纳不生成编辑历史。参数已接纳和算法成功分开；不以草稿当前值充算法消费证据。
5. DeclaredFieldState.ConstantBackup在生产host存在时只读owner当前值；无当前值不回退旧backup/default。TryUseConstant将当前值转Constant并删除非常量槽；无值只进入显式新常量输入，不隐式清原关系。解绑前/后及窗口重绑的通知/错误都需实测。
6. 确认走真实Window.TryConfirm/ProductionEditorContext，取消不发布；Undo/Redo/Copy/RuntimeFieldJson保存配置关系与当前值。重开对象必须进入正式WorkflowExecutionEngine，核ThresholdMinUsed和实际筛选对象/面积，而非只比较JSON。
7. 图文使用同次publication+当前合法样式；ROI本地/继承用原owner及严格Frame/Domain/lease。合法混合UI不得触发隐式算法运行；失败/切图/关闭清旧权威且旧回调不写新代。

## 本方策略与外部基础保护
- 未消费解绑保留owner种入的常量A；消费后解绑取同owner已接纳B；无当前值拒伪造默认。以上是本方明确当前值合同，不冒称VM所有解绑分支。
- Single为现有支持模式。其他阈值、几何组合生产者、图像/屏蔽继承、位置、未具备显示输出、动态文本位置按当前保护，不为整窗测试强行开放。
- A20未声明输出/Group迁移不造候选；A21偏好持久化不新增方案字段；连续调试非视觉会话缺失单列，不借旧SDK循环。自然系统焦点/物理键鼠及VS不由事件注入代证。

## 测试先行的整批验收矩阵
T1 同窗构造混合：原低阈值173引用＋面积双端20..30＋浮点独立端点＋本地检测/屏蔽＋正式图文样式，私有调试核ThresholdMinUsed及独立区域像素/筛选oracle；当前值只归草稿，正式/文件/历史不变。
T2 未消费A→绑定B→解绑=A；消费B→解绑=B；改来源/索引未再消费仍取已接纳B；无当前值不能伪造旧backup/default。分别核原toggle、setting、current槽与实际输出。
T3 合法未失焦文本整窗确认保存重开；非法阈值/两端/索引任一pending时整组拒绝且不半写，原文可修复；不RaiseEvent(LostFocus)冒充自然焦点。
T4 实际确认取消/Apply后新草稿代、Undo/Redo/Copy与保存重开执行。ROI本地/继承、图文由已验证链复用，至少两种真实组合，组合型来源无正式生产者保持空候选。
T5 来源删/断边/类型或索引失效保关系，真实执行/捕获拒绝且不发布新结果；Undo恢复；冻结后变量变化只影响下一次。异常需核特定诊断，不catch任意异常当通过。
T6 真实窗口关闭、同ID重开/新context后缓存旧tree/request/field回调不可写新草稿、不清新窗；帧变更及只读预览/结果layer按原proof退役。复用C07/ROI已有专项，新增只补混合组合边界。
T7 源码冻结后标准UI/全量候选构建；独立编译专轨，隐藏CreateDesktop+STA，不切用户桌面；实际Loaded DLL、测试源与源码SHA逐轨核。失败保留且定位产品/夹具，不盲重跑。

## 共享文件/合同边界（写入前报告）
目前设计不需新增共享产品合同；现owner/当前值/冻结/Host足够，优先测试真实链。可自主写专属 scripts/verification/S4B08NumericReferenceCompositionCases.cs、设计/矩阵；拟给既有Run-S2ReleaseValidation.ps1加立即消费的B08NumericComposition轨（S4、480秒、同产物隔离规则），不扩发布规则。
若测试确证缺陷，最小候选范围拟为 Plugin.Wpf/Adapters/Fields/DeclaredFieldState.cs、NativeNumericCommit.cs、Presenters/Parameters/NativeRangePresenter.cs及引用会话/owner实际相关文件；UI Services/Editing/Nodes/RuntimeNodeConfigurationControl.cs、ProductionEditorContext.cs；Workflow RuntimeToolConfiguration当前值/NodeConfigurationEditHost.ParameterValues.cs、RuntimeDebugInvocation.cs仅在确证消费/接纳错误时报告具体hunk。不得先改这些文件，也不重写现引用/执行架构。

## 当前状态
已确认干净并切基线；仅设计/矩阵，未改产品、未构建/启动测试。共享产品写入待具体缺陷与主线确认，整控件范围不缩成单字段。

## 候选最终结论（013）
产品基线93b9b683未改代码/合同；标准UI及全量候选构建均自然exit0/0错。最终专轨013八组自然exit0，15个Loaded程序集均与候选SHA一致，实际测试SHA120A89A43FFF15E0C88DE937971908FF9D857D0596B4A02A3A4B2F3B5AC76664。当前已支持完整混合生产组合自测范围通过；未把历史版本回执当本版，也不称原24项全交叉闭口。

关键修正全部为夹具：新节点不可克隆旧自动图像OriginId；真实高级区须展开后操作圆度范围；新方案按正式FromBase后的UI投影取节点；Redo精确恢复Fields/Selection/Order但CurrentValues按live种入，不复活历史消费；图像与灰度均有width，必须完整身份明确选output.image。原失败、009历史快照、013候选身份和真实场景截图保留。

实际同窗组合新增AtField/AtVariable动态索引及8×9非方图，明确宽8高9；检测25像素、继承40像素分别实际消费，真实主结果scene显示同次ROI/屏蔽及Area=25。无当前值配置经实际文件OpenSolution建立新DocumentSession进入真实窗；取消空值/非法不发布，显式149才转常量。未失焦以Text属性注入和事件观察为界，不是自然系统键盘焦点。

未新增验证：冻结后变量变化只影响下次、多个参数后项失败的前项回执及算法异常、完整跨文档旧TreeViewItem事件、连续停止、系统剪贴板、其他DPI/VS。A20/A21和缺基础模式/输出继续保护；坐标/像素、VM全部解绑及回读时序不冒称等价。主线正式同版复验后再更新原主方案，本候选只维护本设计/矩阵/交接。
