# 变量Save事务与运行准备接缝实施交接

- 基线提交：f7fc6f18586ed2eae03840125ed23f2e52821549；隔离核心临时提交aa95df8b2a38df2e906cda5f0a07204bb1f3d472已审合。集成源码身份以随后固定包receipt的精确commit为准。
- 当前分支：main；执行者：Codex，父/root唯一主树产品writer。f01在隔离worktree实现核心，variable_ui_review仅只读复核。
- 任务目标：修复原FT-H02变量Name失焦正式写入导致Save正确guard拒绝；同Document/Session节点及变量私有草稿Prepare、整体Validate、一次Publish，再沿原Repository保存。沿VARIABLE_SAVE_TRANSACTION_DECISION_20261024.json及主控实际补充准入裁定，不恢复旧patch。
- 允许修改目录：正式决定的Workflow三核心、UI编辑/变量/文档保存、SDK提交接缝与定向测试；主控额外核准SolutionSession持久化/现有UI WorkflowExecutionManager与MainWindow输入捕获必要接缝。不改核心工作流调度、程序集载体/ALC、Blob或全工具迁移。

## 已修改与行为

- Host/Store/GlobalVariable：详HANDOFF_VARIABLE_BATCH_HOST_20261024.md；显式值revision CAS含ABA和no-op，未编辑值保最新，配置UndoRedo不重放值；接受阶段全checkpoint，通知在锁外且失败仅记refresh error。
- GlobalVariableManager ViewModel/Dialog：所有分组、增删、名称/类型/描述、当前值和目标绑定均落私有Configuration；SetValue明确写意图；raw Name/Value按稳定Id保存，非法原文跨隐藏/搜索/切组参与整批校验。成功才刷新草稿，失败保留，关闭废弃私有草稿。
- MainWindowSolutionEditing/ProductionEditing与ProductionEditorContext：节点与变量使用同一Session Host，准备不逐节点Publish；文档入口只调用一次CommitBatch。未迁移legacy有待提交编辑在任何flush前明确拒绝，干净legacy允许；不静默全工具迁移。发布后view失败不倒报事务失败。
- ToolEditorCommit与NumericUpDown：增加private prepare接口、文档参与者收集、legacy baseline与pending检查；原单节点确认路径仍沿所属Host。
- SolutionSession.Serialize：单ValueGate内捕获独立变量集合/metadata/bindings/基础值数组，锁外局部Json Options重定向该次GlobalVariables，不修改共享Options/Resolver，不持锁IO或JSON。
- WorkflowExecutionManager/MainWindow：主控22:00后实际消息核准运行准备接入同文档入口，生产缺入口明确拒绝；RunSingle/RunSolution/相机内部重复提交移除，连续每轮及通信准入各一次；成功后捕获最新同代变量定义和值。旧隔离fixture显式internal兼容标识保原组件验证，生产Main始终配置入口。核心执行层未加入UI回调/新调度。

## 已完成与尚未完成

- 源码与新测试已落盘，有限只读审查指出的Run分叉、变量metadata混代、offscreen非法原文丢失已据正式边界修正。
- 本交接成文时尚未restore/build/实际运行；不能据此称修复PASS。后续真实记录归新fixed root，不覆盖748/098证据或旧FAIL。
- 新核心预计37case，父新增4变量/保存并发、4运行准备、2legacy用例源码；旧Host通知oracle依新正式接受/通知分离修正，接受中途失败回滚仍有独立注入负控。实际数量以TRX为准。
- 受影响旧LocalImage、连续停止、通信准入与展示回归需要重验；旧A11全插件矩阵、旧独验有效成果不重放。
- 设施Prepare/Run/Seal独占新root、精确source archive、逐项目输出/source stamp、offline restore、完整Release、PE/HostDebugRoot/payload SHA门先于实际执行。静态前检仅Parser/26vendor/WPF运行时，不冒可用产品结论。
- 人工验收结果：尚未进行；统一同源Release部署用户指定Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe后集中一次人工/VS，不拆散询问。

## 风险与边界

- 值编辑只既有Integer/Double/String/Boolean；当前值JSON语义保留，不新建InitialValue模型。复杂/native值沿既有borrowed只读/资源合同，未新增owner或声称任意深冻结。
- 单ValueGate快照覆盖变量成员/metadata/值；保存节点配置仍沿原UI文档访问规则，不承诺任意后台线程同时修改整个节点图。
- 同一节点的变量目标绑定和节点参数草稿若均变化且不相同，保守明确拒绝，保持全部草稿；未改变量节点副本不覆盖pending181。
- Type历史恢复与最新运行值不兼容时明确拒绝，不从旧history回灌值。发布后磁盘失败保持dirty/原磁盘，重试无新编辑不推进代；需producer及独测分别确认。
- 原整批验收、正式性能、最终用户Release部署/集中手验/VS仍开放。程序集管理继续等主控，不扩范围。

## 下一步

1. 唯一产品保存点后在独占新源码archive前检、全量Release构建与实际程序验证。
2. 有效TRX/精确源码和产物manifest/PE身份固定后直交独测，跟完原9组实际Save、失败保留、Run、关闭重开及新增隐行原文回归。
3. 按独测实际结果修具体缺陷，经理核原剩余合同，统一最终Release和一次人工/VS清单，不停在本交接。
