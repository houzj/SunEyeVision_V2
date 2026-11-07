# D07 历史结果实际入口与资源边界核证（只读设计）

审计root基线4f8efb06及当前在途NativeResults接线；核证者在独立workflow-review工作树，仅新增本文。本批不修改产品、不保留运行结果、不改变执行Dispose、不启用历史Tab、不构建或写Release。D07核心切换仍待主线当前审批，本设计不是实施授权，Group不动。

## 结论与范围分层

同一方案编辑会话内，指定运行输出的冻结声明、真实RunIdentity、可独立Retain的资源reader、同一publication树表/图像呈现已经具备；有界会话历史在技术上可复用这些能力，不能概括为“缺数据库所以做不了”。但实际历史列表、选中命令及跨轮持留owner尚未接入。实施它会新增一个长期资源拥有者和运行完成交接，确实改变现行执行结果持有边界，必须作为完整批准批处理，不能只把禁用Tab打开。

跨方案会话持久历史则没有正式存储/恢复合同。本阶段依原目标保持明确占位；不能以会话内Retain冒称跨会话落盘查询、清空、导出已经存在。

## 已有实现—实际行为—差异—决策—验收出口

|实际位置|已有行为与证据|差异及最小决策|验收出口|
|---|---|---|---|
|Plugin.Wpf/Controls/Native/Composition/Results/ResultShell/ResultWorkspaceShell.xaml:166|历史结果Tab明确IsEnabled=False，提示等待正式历史记录/查询/清空/导出；xaml.cs无历史ItemsSource/选择命令/业务存储|这是如实占位，不是隐藏的已接历史列表。将来按原件组合增加独立host内容/选择适配，不在控件内持工作流结果|真实Tab选择指定记录才可算生产入口；当前继续禁用|
|UI/Shell/Windows/MainWindowViewModel.cs:4580 ShowDeclaredRun|从传入的真实ExecutionResult.DeclaredOutputs按完整RunIdentity精确Single选snapshot；设置_declaredHistoricalResult再走ShowDeclaredSnapshot/PresentSnapshot；异常ClearResultDisplay|整个src检索只见声明，无真实调用点。不得以方法存在声称UI可浏览历史；也不能造一个ExecutionResult包装旧snapshot来凑参数|真实列表命令携指定节点调用身份进入同一呈现链；身份不存在必须拒绝|
|MainWindowResultPublication.cs:61 PresentSnapshot、ToolResultViews.cs:15 FromSnapshot|核文档session/实际node实例/workflow/执行owner，借该snapshot.AcquireReader，构造唯一publication；Presenter与实际表各自Borrow|呈现核心可以复用；历史允许原执行admission已退休，但仍须可信历史条目与当前会话/节点归属证明，不得移除正常路径身份门禁|同节点连续两轮指定旧轮值，切回当前不串轮；文档/流程/实例变化拒绝|
|UI/Services/Workflow/WorkflowExecutionSnapshot.cs:11、:128及WorkflowExecutionManager.cs:41/:1041|每root一份长期可变耗时/状态快照；Begin重置，Complete更新当前节点时长并累加CompletedRunCount|不是多个运行记录；节点条目没有完整RunIdentity、输出值或resource reader，不能从次数/时间反推出历史数据|继续用于总览/状态栏耗时；不把它的行接成结果历史|
|WorkflowExecutionSnapshotPresenter.cs、MainWindowViewModel.cs:4203 ShowExecutionSnapshot|状态栏真实入口打开当前root快照详情窗口，复用同一耗时快照，关闭不释放Manager数据|此真实入口是耗时详情，不调用ShowDeclaredRun，也不持历史结果；可复用窗口生命周期思路，不能冒充历史结果已有|总览/耗时原行为不回归|
|Plugin.SDK/Execution/Results/ExecutionResult.cs:15/:28/:67|冻结DeclaredOutputs，按真实身份AcquireDeclaredOutput；Dispose清字典并关NodeResultSnapshot|只保存ExecutionResult对象引用没有用；Disposed后枚举或Acquire直接拒绝。保持现Dispose时机，不无限保留整个run|run退出后仅已合法取得的独立reader仍存活；最后reader释放资源|
|DeclaredOutputSnapshot.cs:39/:58、ResultResourceOwner.cs:14/:23|冻结实际schema/revision/identity/status/values；AcquireReader Retain资源owner。owner根Dispose只退一份，references>0期间仍可再Retain，0以后拒绝|可在run还活时取得history专属reader作保活，再在选择时从同一snapshot借呈现reader；无需新SDK合同、假run或latest查询|早取reader后原result关、下一轮替换仍能显示旧值；迟到首次Acquire必须拒绝|
|WorkflowContext.cs:588 SetNodeResult/:627 RemoveNodeResult/:646 ClearNodeResults|按nodeId只保存最近结果；替换时Retain新、释放旧。CaptureNodeResults另给NodeResultSnapshot独立借阅|这是latest缓存和资源基础，不是多轮历史；不能等替换后再从它补旧轮|新轮缓存覆盖不影响已借旧history；清缓存不越权清其它reader|
|MainWindowViewModel.NativeResults.cs:60–64|历史态只取当前选中snapshot，不借当前上下文的上游新结果拼旧图；结果publication与图像pane独立借阅|这一隔离应保留。历史输出不能冒充当前节点真实冻结input给ROI编辑|旧轮表/图同Frame；不混当轮上游或重新贴Frame；历史ROI默认无当前input授权|
|UI/Shell/Windows/MainWindowViewModel.cs:4507/4529 CalculationResults|当前结果格式化行，Clear后重新填充，无RunIdentity/reader/历史选择|可以复用格式化/主题，不是历史存储|不把显示字符串当可重新解析的权威值|
|Workflow/Solution.cs:119 VersionHistory|方案版本元数据，非执行记录；SolutionRepository持方案/参数/资源引用|不把方案版本、通信收发历史或编辑Undo日志误作结果历史|运行/停止/历史浏览不进入编辑Undo/Redo|

## 正式执行与清理完整路径

1. WorkflowExecutionEngine.cs:838 CaptureDeclaredRunOutputs从实际AlgorithmNode取得声明；只包括已具备AdmittedIdentity和BindingOutputSchema的节点。已有结果用真实DeclaredOutput；无结果时用BindingContext.WithoutResult及实际状态/错误，保真实identity。正常及取消/失败路径:282/:417/:812/:822均有捕获。
2. UI WorkflowExecutionManager.cs:946取得真实ExecutionResult；:950–954现已有await InvokeOnUIAsync完成快照汇总，在result返回调用者之前。单次:200 using，批量:518 using，连续:363 finally统一Dispose，不应为历史功能取消这些清理。
3. Manager的WorkflowExecutionCompleted事件:1352及WorkflowExecutionEventArgs:1499只有workflowId、时间/错误，没有run或节点调用身份；在其订阅里反查Context/latest不能恢复指定旧轮。
4. 节点事件:1370先Retain结果、UI队列回调finally释放；MainWindowViewModel.cs:4237入场核会话和实际节点、写当前context；相机200ms合并会丢弃被替换展示项。它可为单模块记录提供真实采集入口，但单靠它无法收齐未执行节点，且过期admission的排队节点事件会被拒绝，不能当完整run-history来源。
5. 运行完成处如获批准可在既有await UI交接内集中捕获当轮全部真实DeclaredOutputs：先核运行开始捕获的session/root/实际owner/流程与节点实例，逐条取得独立reader，再原子接纳记录；准备、通知、取消或会话切换失败应全部退未接纳reader。继续按原路径归还ExecutionResult，不把运行生命周期移到UI列表。

## 最小会话内适配方案（仅设计，未实现）

- 一个由方案编辑session拥有的有界内存记录集，每条保完整真实RunIdentity、sourceWorkflowId、冻结schema/revision/status/诊断、展示摘要及独立reader。记录不是ExecutionResult、WorkflowContext、Bitmap或反射值字典，也不是可再次执行的状态。
- 成功/部分成功条目的reader在运行完成原owner尚活时取得，记录负责关闭它。呈现选择时从同一snapshot重新借FromSnapshot reader；因记录reader仍保references>0而安全。不要把记录唯一publication直接交当前视图去Retire，否则一次切走就会永久退休该记录。所有输出不重算、不整块复制大数组。
- 失败/超时/取消/未执行按已捕获实际identity及status显示诊断；没有值就没有图像。可复用WithoutValues生成同一实际identity/schema/fields的纯元数据投影，避免给失败态无值记录长期Retain无用资源；不发明未准入节点的RunIdentity。若准备阶段在节点准入前失败，记录root错误摘要即可，节点仅“无调用记录”，不能伪装执行过。
- 列表真实选择命令传记录身份而非行号/节点显示名，复核当前session、root/node实际实例及记录仍在集合。复用PresentSnapshot核心，把指定记录的合法reader/身份交现publication链；不构造假ExecutionResult满足ShowDeclaredRun参数。ShowDeclaredRun可成为该核心的实际run适配入口，但不能再留一条按名字查latest的旧解释链。
- 当前ShowDeclaredRun仅供仍活ExecutionResult；若采用上述记录接口，应把其抽取snapshot与呈现部分集中到同一核心。正式节点路径继续保ExecutionOwnerMatches；debug记录若要求在editor关闭后继续浏览，需要采集时可信session/DebugOwner归属证明和独立历史读权限，当前“必须存在有效editor”的门禁不具备该行为，不能直接放开给所有历史candidate。
- 选择历史设置historical状态，实际新轮继续执行/更新latest context，但不覆盖选中的旧表/图。明确“返回当前”命令清历史态后从当下被选节点的真实context重新呈现；同一节点再点击常会被选择去重，不能依赖切到自身触发恢复。
- 淘汰、清空、删节点/流程、切方案、session关闭、应用关闭：先令记录/选择epoch失效，退休被选publication/pane，再释放对应记录reader；重入的新选择不能被旧清理覆盖。视图借阅与记录借阅各退本owner，最后一个才释放。记录不进文档序列化或编辑历史，不随Undo重放运行资源。
- “有界”须明确条数/保留策略及资源预算。现接口没有统一递归资源字节计量，不能声称仅设20条即严格内存上限；标准image布局可估像素，混合记录/Region/共享资源仍需预算合同或保守拒留策略。新增持留会增加大图/大数组驻留，不是零成本接线，应明确验收预算后再实施。

## 跨会话持久历史的缺口（保持占位）

跨会话需另定版本化记录与schema格式、资源文件和Domain/Frame关联、写入原子性与恢复、稳定文档/节点身份及新会话只读授权、磁盘配额/保留/查询/清空/导出、失败/缺失资源诊断与生命周期。不能持久化本轮ExecutionOwner再在新会话复活admission，也不能把Retain的内存句柄、SDK对象或显示Bitmap写成历史资源。当前SolutionRepository与VersionHistory不提供这些职责；不新增数据库来回避本轮收尾，也不把此缺口套到会话内Reader能力上。

## 后续验收矩阵

- 指定旧轮与新轮值不同：完整RunIdentity选取，退休admission后旧值仍可读，返回当前准确；同nodeId不同workflow/session/实例拒绝。
- 真成功/部分成功/失败/超时/取消/实际未执行与未准入缺记录分开；无值不借新轮图像；旧metadata可看不代表旧工具再次执行。
- 单次、连续、全方案及适用的手动单模块采集齐；相机合并不能误当历史采集；队列中切方案/删节点不得接入旧记录。
- 原result/context/pane先后关闭、记录淘汰/清空、视图借阅仍在、最后reader释放、清理异常/通知重入都精确一次；有限条目及大资源预算明确。
- 历史浏览不改参数、不产生Undo，不向当前global变量生产旧值，不给ROI当前input授权；真实UI列表/命令/表/图同一记录后才报告会话内闭环。

本文仅完成源码及资源边界核证，没有历史列表、持留服务、真实选择命令或跨会话存储实施；未构建、未运行、未VS/人工。主线原方案应把“已有基础/会话内未接线/跨会话未实现”分列，保当前历史Tab占位，不能因可行设计而标完成。
