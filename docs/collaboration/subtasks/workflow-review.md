# 根流程工作区并行只读核验交接

> 当前纠正（最终候选3be9a94b）：撤回本报告历史上将CategoryId/Relations列为根运行参数缺项的判断。已复核554c1399的完整入口证据：根WorkflowConfigurationWindow为平铺单目标；分类/多关联由GroupRunConfigurationWindow消费。共享DTO有字段不能证明根入口具备该功能，之前源审由模型字段越过消费者推断，结论不成立。历史文字保留仅为审计来源，不再作为根剩余任务；Group继续冻结。输入槽订阅另由本次704de93e真实实现核验，不与分类关系混同。


- 基线提交：adc727ba1eabf1d3af3bbbf2e5d22494fb402597（固定版本）
- 当前分支：codex/workflow-review
- 当前执行者：D 只读核验
- 任务目标：对照恢复项目和R01—R18，核清可实施链与真正基础缺口；不把恢复件等同原厂行为。
- 允许修改目录：仅本文件。产品、测试、共享任务账本和原主方案只读。
- 已修改文件：docs/collaboration/subtasks/workflow-review.md

## 结论与阻断项

1. R08不得声称原厂等待语义已迁移。恢复件 `VisionFramework/SolutionExecutor.cs:20—22,46—54` 创建 CancellationTokenSource/CancelAfter，将取消转为TimedOut，并明确排除用户取消优先。它证明恢复宿主有协作超时实现，不证明原厂 Get/SetProcedureWaitTime 等同整流程执行时限。固定基线 RootWorkspaceSettings 仅有间隔、NG和总览位置，没有有效等待超时字段。当前保持明确禁用占位是诚实范围；若启用须完整设计迟到完成、资源释放、用户取消、通信/相机等待边界。
2. R12确有可实施基础，不能整页判缺基础。当前已有正式运行参数描述符、BindingInputCatalog和多节点候选发布。恢复件 `WorkflowConfigurationWindow.xaml.cs:61—101` 不仅曝光参数，还曝光输入槽，支持订阅选择；`WorkflowConfiguration.cs:4—12` 还保存CategoryId、Relations和稳定目标身份。A批如先接标量参数代理，需保留输入槽/分类/多关联的逐项剩余，不用“运行参数页完成”覆盖差异。此项是完整性检查，不要求恢复被冻结的Group行为。
3. R11根输入/输出与R14局部变量是宿主正式身份链缺口，不是逆向材料缺失。恢复件 `WorkflowConfiguration`、`WorkflowSubscriptions.WorkflowSourceKind`、`WorkflowRunContext` 有独立WorkflowInput/LocalVariable身份、初始化、订阅、当次执行值和结果发布。当前 `DataSourceQueryService` 的GlobalVariables是方案字典，`__SubroutineInputs__`由引擎子调用注入，并非根输入。直接改标题、名称前缀、假节点或借Group绕过都不成立。SDK来源/作用域与根快照未具备且本轮禁止公共扩展，准确禁用占位合理。
4. R13恢复件配置预览消费根Outputs/Displays声明并转换实际图像；当前节点结果/图像缓存有可复用能力，但不能命名为完整根输出/显示映射。节点预览须明确作用域、实际本代结果与图像身份匹配，关闭清引用；共享图像不可由窗口擅自释放。完整根显示声明、报告映射与租约合同暂占位。

## R01—R18固定基线验收矩阵

|项|adc727ba源审状态|集成批必须保留的验收出口|
|---|---|---|
|R01|40px根Tab、四态资源及正式配置红点已接；配置/局部变量仍禁用|暗亮长名、选中运行禁用、整窗物理点击与设计时；R10/R14另列|
|R02|30px选中可见和固定新增/溢出已有|多Tab窄宽Resize、首尾键鼠切换、多DPI|
|R03|根与普通节点目录沿正式身份导航|重名、过期对象、零根、关闭弹层，不扩展Group|
|R04|根复制复用片段和方案级历史|新根/节点/边/参数/引用独立身份、撤销重做、保存重开真实运行；新增曝光ID同步重映射|
|R05|根增改删与零根恢复已有|当前根删除切选、引用清理、UndoRedo、保存；不能仅删Tab|
|R06|不可变根设置和执行代捕获已有|间隔0/非0、取消等待、单次不等待、旧代不被编辑污染|
|R07|真实JudgmentStatus NG聚合已有|成功NG/OK、执行失败、取消分离，重启无旧代资源|
|R08|原厂语义未证；基线无有效实现|明确禁用，不把恢复协作超时称VM等价|
|R09|正式定义/插件/参数/待修绑定判断已有|加载变化、修正刷新、缺插件、空图；不取LastResult作为就绪|
|R10|完整根配置窗尚无|单窗、会话/根对象身份、确认取消、运行门禁、切方案关闭、暗亮实际WPF|
|R11|宿主根输入身份全链未有|占位解释来源、值快照、查询/编译、复制删除所缺合同|
|R12|正式目录/多节点事务可复用，根代理尚无|参数值与根代理同次提交回滚、历史、失效修复、复制映射、保存重开、真实执行；输入槽/关系/分类差异不隐去|
|R13|节点真实结果可用；完整根显示合同缺|真实结果不等同计时；图像代匹配、切根/关闭清理|
|R14|只有方案全局；独立根局部全链缺|准确占位，不假隔离；未来同名隔离/生产消费/执行初快照/删除Undo|
|R15|总览坐标持久化已有，无总览UI|140×120卡、真实计数/耗时来源、移动取消/复制/Undo/保存重开、独立视口|
|R16|搜索/画布/小地图既有|总览往返关闭搜索/详情/拖动，视口独立、订阅清理、多DPI|
|R17|现有根快照只读详情及单窗生命周期|原样式逐态、根计时数据、重复开关/切根关闭；Group选择保持冻结|
|R18|恢复件本身禁用项与未证标题注释|导出/通信/变量导入导出不得造假可用；未证不等于确认不存在|

## 可定位来源与文件身份

恢复项目路径均为 `D:/MyWork/REA逆向测试/src/`，本次只读源码，没有运行原厂或恢复程序：

|文件|SHA256|
|---|---|
|VisionFramework/SolutionExecutor.cs|A49BA2AC2B924D8C5FA16941F138A78F92A6D81E47180753FD95B2D54349FC3B|
|VisionFramework/WorkflowConfiguration.cs|AD59DB622C095AC22C3550C3B3CA4B6491AC31D68108A83E0CAA9C56928E7B1F|
|VisionFramework/WorkflowRunContext.cs|D00F29701F640028040E0F2C3D1AC11246FFB500945246C3424E435741241247|
|VisionEditor/WorkflowConfigurationWindow.xaml.cs|C4550293546A4B0D691A3FE133A16E1E916B479B8B0E243321D804F43C878A50|

本产品来源固定在上述基线：`src/Workflow/RootWorkspaceSettings.cs`、`WorkflowRunConfiguration.cs`、`BindingConfigurationStore.cs`、`NodeConfigurationEditHost.cs`、`DocumentEditSession.cs`，以及 `src/Plugin.SDK/Execution/Parameters/BindingInputCatalog.cs`、`DataSourceQueryService.cs`、`src/Workflow/WorkflowExecutionEngine.cs`。原R表沿用主方案，不另缩范围。

## 验证、剩余与交接

- 已完成：固定基线源审和能力边界复核；R08/R11/R12/R13/R14重点差异已发集成负责人。
- 已执行构建与测试：无。本路线按授权不部署、不复制程序或库、不改测试。基线既有测试记录只是引用，不是本路线重跑结果。
- 人工验收结果：未执行；不声称原厂动态、VS设计时或产品物理交互通过。
- 未完成：等待A/B/C固定提交后，沿新合同、生产消费者和退出清理链做增量评审；最终集成构建和同版Release验收由A承担。
- 已知风险或禁止改动：Group、循环、SDK公共来源/显示合同冻结；不主仓/main同步；不读取浮动源码后宣称固定版验证通过。
- 下一步：A提供固定提交SHA后，仅核对应差异与上述完整功能出口；真实缺基础项在原主方案保留禁用占位和后续依赖。
## 增量核验：总览与底栏固定提交

审查对象：B `9c55b76db846c7b4be5be1a401fb6dbf2b74921c`、`a147ab9dc716cc890d16ceed1c80ea00a494241b`；C `b774f8bd8378e8b5b1b01a06c5f6b8c4c7d2add9`。仅git show固定对象与基线消费者，不读取A浮动修改作为验证。

### 需要集成前修正

**P1：未知计时被显示为0.00。** C的 `WorkflowExecutionSnapshotWindow.xaml` 仅对中文状态“尚未运行”“等待中”设置占位。基线 `WorkflowExecutionSnapshot.UpdateNodeStatus` 可以写入运行中、已取消等状态而没有任何结果/时长，ModuleElapsed和AlgorithmElapsedMs仍是默认0；窗口对这些状态直接显示0.00。根FlowTimeValue绑定非空TimeSpan，在从未运行时同样显示0.00。不是只有等待状态才无计时，失败工具也可能根本未返回算法结果。需要由宿主明确实际时长可用性（nullable/HasTiming等）并触发呈现刷新，保持真实0ms可表达。建议至少覆盖未运行根、正在运行节点、无结果取消/失败、真实0ms、正常完成。

### 已核未发现阻断及待实际验收

- B运行/停止四态DrawingImage键存在；配置PNG normal/hover/click/disable与固定树实际资源对应。展开按钮把缺失hover/disable回退normal，click存在；全局/单个正常禁用原PNG四态键完整。不根据资源名本身判外观达标。
- 总览payload绑定当前控件、文档、修订、稳定RootId和一次token；drop先清token/ghost再调用宿主，关闭/卸载/显式取消清理；五像素吸附与卡片整体5000边界一致。源恢复件亦在首次鼠标移动启动OLE，无阈值不能单独列迁移缺陷。
- 同代SetEntries刷新会清空并重建整个Surface（连OLE源标题和ghost一起移除），虽然token不主动失效，但未看到真正OLE嵌套消息循环/拖动中的同代刷新测试。其现有测试只直接触发Button.Click，不能证明OLE可继续。集成必须实际覆盖：按下待移动期间刷新、OLE进行时计数/耗时刷新、鼠标离开返回、文档修订取消、卸载取消和Drop恰逢模型刷新。此为明确待验证风险，静态证据不足不直接宣称已失败。若宿主高频无差异重建，可避免刷新时重建活动源视觉或按身份更新卡片。
- B DoDragDrop的finally能清理但无catch；原恢复件明确catch异常并写状态。实际COM拖放失败可能从输入事件冒出，应由统一UI异常策略或此处捕获转换为可诊断取消，不能只有清理。无运行异常证据，本次不冒称可稳定复现。
- C窗口Closed清ItemsSource与DataContext而不清共享快照集合；与现有Presenter单窗所有权相容。窗口层没有新增执行/图像资源所有权。切根/切方案实际关闭仍由A宿主链验证。

本次仍未构建、未部署、未运行原厂或产品；真实OLE、多DPI、暗亮布局、VS设计时与用户人工验收均未通过本次源审替代。A固定提交收到后，重点核缺值合同与共享接线，避免重复R全表。

## A固定集成增量核验：ba889d4a

对象：`ba889d4a46c01bfc554775df41ec31aa99ca4261`。范围限根代理事务、结构历史、复制、窗口草稿/结果、OLE延后刷新与计时，不重列原R表或明确后续的分类/多关联/模式chrome。

- 根目录与多节点候选走同一次DocumentEditSession冻结、ValidateBatch、CommitBatch；ComposeRoots核owner/consumed/精确正式对象/根签名及去重。代理值仍存节点，目录不可变，未发现第二套运行值。
- AcceptProjection在写之前完整准备根before/after和节点旧参数/设置，实际根写走raw赋值；失败Restore恢复根及节点，Applied=false阻止失败后根变更通知；通知在接受结束后发出。未发现新增根写在事务成功之前泄露给视图的路径。
- 结构RebaseStructure把历史参数与根目录投影到当前拓扑，保留当前WorkspaceSettings；已删节点代理保留为失效目录而非错误复活节点。该选择与显式修复占位一致，正式就绪检查拒绝失效代理。
- 根复制收集当前根节点ID映射，代理重新分配Guid、映射NodeId；实际参数/引用复制仍沿既有WorkflowFragmentCopy。未知旧来源保留为失效身份，不按名称误配。
- RootWorkflowConfigurationContext校验方案/根/节点精确对象；候选字段和绑定留私有草稿，Apply冻结后一次发布，关闭注销参与者。Undo/Redo按本窗口提交计数和Host版本作范围门禁，避免撤销其他编辑。主窗单窗登记、重复激活、失败释放和窗口Timer退出均有承接。
- 结果读取限定根运行上下文；标量输出由正式目录读取，图像需OutputCache.LastResult与本次上下文结果同对象才显示。窗口不处置共享图像；已冻结Bitmap直接借用、可变Bitmap克隆后冻结。未把计时摘要伪装成完整根显示映射。
- 原增量的计时缺值问题已由HasModuleTiming/HasAlgorithmTiming与FlowElapsedText承接；真实零时长与未提供可区分，Begin清标记，窗口和底栏统一消费。根未运行不再显示0.00。
- OLE同文档同修订统计刷新延后到手势结束，保留源visual和ghost；修订/源失效先清deferred再取消，避免旧投影覆盖新文档。drop先退出手势再交宿主，宿主复核文档身份/修订。DoDragDrop异常已有DragFailed可诊断取消。前次源审的两项静态疑点已承接。

**本次未发现新增足以阻止该集成批继续收尾的明确源码缺陷。** 这不等于完整迁移或人工通过：真实OLE嵌套刷新、物理鼠标、多DPI、整窗主题及VS设计时仍由集成实际验收记录承担；本路线未执行构建/测试。负责人提供的366/367构建与98回归、host-reference-copy结果仅作为其验证报告引用，不记为本路线重跑。未改产品、测试、共享账本；Group和公共合同冻结不变。

## 最终资源订阅与模式差异核验

固定候选：3be9a94b6fd397b0be24912eeae0a3b16c5d6eba；新增源自704de93e、a157e5f0与A MaximumScale合同。仅源审，不重跑构建。

**明确P2缺陷：总览底栏缩放上限与宿主不一致。** `ShellWorkspaceControl.RootPresentation.SynchronizeWorkspaceScale`总览设置MaximumScale=4，RootOverview.Viewport本身支持0.1—4；但`ShellWorkspaceControl.xaml.cs`的WorkspaceScaleRequested总览分支仍`Math.Clamp(scale,.1,2)`。用户Ctrl滚轮可达400%，改用底栏滑块请求300/400%却被截成200%，与控件公开范围矛盾。应将宿主总览分支与4上限统一，并补实际Slider请求>2且根Tab比例不变的专项；现369模式仅1.7不能覆盖此分支。

资源链未发现新增明确阻断：图像订阅写正式ImageSourceRef并清运行时ImageSource，区域写类型化ParamSettings；Reference-only拒常量、拒跨根与子字段/切片，按正式端口类型匹配。临时ValidateReference捕获并finally还原设置、图像来源、来源身份及pending/rebinding标记，未发布正式节点。Unset清来源与对应身份，移除代理恢复打开时绑定；复制继续沿正式资源ID/作用域重映射。资源槽不将运行图像或区域实例持久化为常量。

模式切换集中关闭名称/ID搜索、位置弹层、耗时窗和小地图捕获，取消当前画布/总览手势；切入捕获根视口，切回排队恢复，底栏ExecutionText绑定按模式切断/恢复。MaximumScale DP校验有效范围，ScaleRequested消费仍有上述上限遗漏。真实369反馈仅由集成负责人提供，本路线不据此声明物理交互、多DPI/VS/人工达标。370资源正式运行验证仍由A承担。