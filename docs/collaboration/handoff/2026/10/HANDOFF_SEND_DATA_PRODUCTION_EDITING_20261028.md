# 发送数据正式编辑联合事务交接

- 基线提交：24615965；前批三夹具修复固定8ad550f8，旧D06候选已另存c6583855且不得重复应用。
- 当前分支：codex/d06-validation-ui-fixtures。
- 当前执行者：Codex / source_editor，独立workflow-overview worktree唯一写者。
- 任务目标：发送数据工具正式编辑使用同一私有节点和变量候选，完整准备、调试、接纳、取消、历史与保存链；不迁视觉、不修改SDK收据实现、不扩Group。

## 已有—差异—决策

既有NodeConfigurationEditHost.Compose支持节点与变量同次发布，同参数签名可合并；变量Freeze、HasPendingChanges、RebaseAcceptedDraft已有。旧SendDataEditor直接经live服务Configure变量，且正式EditorDraftAdapter未准入；仅在选择/删除时更新生产者，直接改名称/类型或模式后Apply会漏同步。当前复用原发送协议、目标类型规则、原行控件及Host事务，不建第二套参数或发送协议。

## 本批文件及完整接线

1. src/UI/Services/Editing/EditorDraftAdapter.cs：明确准入SendData托管行，复用IBindingInputProvider/IOutputContractProvider与原参数Validate；无资源槽伪造。
2. src/UI/Services/Editing/ProductionEditorContext.cs：lazy变量草稿及GetCommunicationConfiguration；候选source.Parameters/ParamSettings始终指向唯一nodeDraft。Prepare/Validate/Commit/Matches/接纳后重取或保留较新编辑的Rebase/Discard完整覆盖变量草稿。
3. src/Workflow/DocumentEditSession.cs：ForNodesAndVariables冻结现节点与变量提案，仍由同一Host.ValidateBatch/CommitBatch接受一次历史。
4. src/UI/Services/Communication/WorkflowCommunicationSendService.cs：CreateConfigurationView(Func<Solution?>)复用原实例typed Configure/Variable Prepare；Device/Event委托原live实例保设备连接/配置/生命周期核验。所有Prepare及Submit额外核同一私有配置实例/编辑会话；旧准备项在Apply、关闭或换节点后拒绝。
5. src/UI/Shell/Windows/MainWindowProductionEditing.cs：只在SendData正式context装上下文service；保原session/node guard、注册/关闭/发布接线。未改Main其他局部。
6. tools/SunEyeVision.Tool.SendData/Views/SendDataEditor.xaml.cs：原视觉不动；ValidateParameters及正式提交参与者统一按当前Rows生成声明。Configure拒绝缺正式draft；Variable配置仅私有变量草稿，Device/Event清本源绑定亦在同一事务。不直接写live变量。
7. tests/SunEyeVision.UI.Tests/SendDataProductionEditorTests.cs：4项真实Main工厂/原标准窗口/原工具编辑器回归（见下）。
8. tests/SunEyeVision.UI.Tests/CommunicationSendDataToolTests.cs：按主线新SDK终态收据合同，保首提交1/第二项不发送；取消结果所有正式RuntimeValue为空且Resources/DeclaredOutput为空；SideEffectReceipt保不可变字节、清投影列表不改事实，并用真正ParameterResolver拒绝取消源。

## 回归及边界

- Real_send_window_uses_one_private_source_and_variable_candidate_then_one_history_and_saved_endpoint：真实Main私有工厂装生产service→实际SendDataEditor/DefaultDebugWindow Loaded→private Variable Prepare/Submit不改live→Apply一次history→Undo/Redo节点与变量共同恢复→SolutionManager.SaveCurrentDocument/Repository.Load保稳定端点和行ID，设计初值不被运行改写。
- Invalid_private_rows_do_not_partially_publish_and_mode_change_unbinds_in_the_same_history：重复目标整批拒绝且保草稿；修正后可接纳；Variable→Device解绑和节点模式同一历史，Undo共同恢复。
- Closing_real_send_window_cancels_private_mapping_and_rejects_old_debug_plan：实际Closed退役context，私有映射不进入live，旧service/Prepare计划拒绝再提交。
- Accepted_variable_notification_preserves_newer_private_rows_and_mapping_for_next_atomic_apply：正式变量配置通知期间编辑新行名，首批只接纳准备版本，较新Rows/mapping保留私有且下一Apply一次接纳。
- 继续保原首提交取消断言范围；主线另有SDK8项事实复制/错身份/清理与捕获异常回归，非本批重复实现。

## 未完成与限制

- DocumentEditSession仍明确拒绝同一次AcceptAll超过一个变量提案；多个SendData窗口或变量管理器并开时不会静默覆盖，需关闭/取消其他变量编辑范围后再整体保存。父任务已确认本批保明确冲突诊断，不新增多写者合并架构。
- 正式UI/full编译、GlobalBinding轨、ImageSources关联轨、实际VS设计器及人工验收均由主线集中安排。本支不写Release、不复制DLL、不构建私有产物。
- 新测试依赖主线ToolSideEffectReceipt/SendDataResults取消输出收口候选，单独取本批不能用旧SDK合同解释为已集成。
- 本批只做必要纯语法与git diff检查；纯语法0错不是语义编译或运行成绩。
- CURRENT_TASK及原主方案由root集中更新，本交接不替代阶段文档与正式验收。
