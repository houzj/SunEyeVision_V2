# D06 正式验证 UI 与发送夹具集中修复交接

- 基线提交：24615965。
- 当前分支：codex/d06-validation-ui-fixtures。
- 当前执行者：source_editor，独立 workflow-overview worktree。
- 旧候选保存点：c6583855（旧14文件完整候选与两份交接，仅保存已转交成果，不是正式验证通过）。
- 允许及实际修改：仅 ResultVariableBindingProductionTests.cs、OutputContractTreeSelectionTests.cs、CommunicationSendDataToolTests.cs 和本交接；没有修改 SendDataTool 或共享产品。

## 证据与完整归因

正式 GlobalBinding attempt-001 报告为257项/20败；ImageSources S4 attempt-004 为176项/2败。只使用这两份正式报告与对应完整堆栈，不套其它产物。

1. OutputContractTreeSelectionTests 两败仍用 SourcePropertyName="latest/new" 判断点路径。当前真实行已经有 typed ResultBindingInfo，正式端口为 name:latest/name:new。本次改核 Reference.PortId、NodeId、ScopeId 和空 MemberFieldPath；保读次数、Background合并、重入时仅一次最终publication、旧端点不存在等断言。
2. Variables_register_single_producer... 裸 CommunicationReceiveService 只有变量Catalog，没有本轮运行值服务；新ReceiveData正确拒绝“宿主未提供当前变量读取服务”。本次接真正 WorkflowGlobalRunValues、WorkflowGlobalProducerOperation、WorkflowGlobalVariableReceiveService 与正式配置/owner请求，保同流逆序节点的正式依赖、CURRENT产值、DESIGN不变及重复producer拒绝。
3. Editor_preserves... 跨STA异常来自 DeferredAppResourceReference。先前 ResultVariableBinding 创建 Application 后在一次性STA结束或阻塞，后续 RenderEditor 在另一个STA构造，继承了旧Application的动态主题资源。本次两者复用已有 ImageThresholdProductionEditorTests.Sta 常驻真实WPF dispatcher，不另造预览、不冻主题或删除主题断言；原暗亮布局、稳定字段ID、添加按钮位置、实际RenderTargetBitmap保持。
4. ResultVariableBindingProductionTests 原60秒总超时没有实际阶段或对话框信息，不能据此证明只是UI环境。新夹具保真实 MainWindow/组件loader/变量ShowDialog/绑定历史保存/原btnDel全部链。Modal driver轮询真实Loaded picker；若正式接纳弹出非预期错误对话框，捕获类型、标题和真实文本后关闭并失败，不让业务拒绝被总超时掩盖。增加具体生产阶段、单modal10秒及整体60秒诊断；尚未实跑，不能宣称该超时已根除。
5. Cancel_after_first_commit... 保原断言原样。SendDataTool在首提交返回后已累计Submitted/Payloads/SentData，取消只清业务输出；但ToolExecutor.cs异步结果返回后见token取消，无条件ReleaseRejectedResult并新建空终态，导致实际提交one而返回Submitted=0；同步Admission也同。根因在共享SDK，不在SendDataTool。已交root协调，不吞token、不假造收据、不降低首提交事实断言。

## 验证与未做

三份C#以已加载Roslyn纯语法0错、git diff --check通过。未构建、未执行测试、不写Release、未私有复制产物。共享SDK取消收据修复归root；SendData旧页正式事务设计与Manual_run_lease归root。真正modal阶段或业务拒绝原因须下一次正式同源回归读取新增诊断后判定，不冒称已验收。
