# D06 结果变量绑定 UI 端点整链候选交接

- 基线：0acde8c4；分支 codex/result-binding-ui-endpoints；独立 workflow-overview worktree。
- 状态：候选 diff，未提交、未集成、未语义编译或执行；等待 root 全批合同原子切换。用户已授权完整批实施；未保留旧身份兼容桥。

## 实施链

1. ResultPresentationModel 增加不可变 ResultDeclarationAddress：仅保存 RootPort、MemberFieldPath 和样本标志，不保存 publication、lease、InputValues 或资源。声明路径不解析 UI Key。OutputReference 的 Type/Storage 始终为根端口声明。
2. 声明态和成功空的 sequence<Record> 可由独立 All 成员组展开正式成员，按现128页上限惰性投影；无样本仍有入口。观察样本及其子字段全为不可绑定显示值，不能把 [n] 偷换为 All。All 行与操作说明明确写 All，既不扫描值也不伪造聚合结果。多维/资源/record 全局目标无存储时明确禁用。
3. Runtime 从 publication.Schema 与当前方案真实根/节点创建绑定元数据，不再依赖空的 Runtime Fields 或 Guid。旧反射 Geometry 字段不生成正式端点。
4. ImageResultControl 使用真实 GlobalVariableSelectDialog，候选经 GlobalProducerBindingRules.Validate 过滤。对话框返回复核相同 ViewModel、session、publication/currentness、row 及 endpoint；不重查 latest。接纳与解绑同用 BeginVariableEdit/CommitBatch，一次配置历史。唯一持久来源为 ReferenceExpression；WorkflowName/NodeName/ResultDisplayName 仅显示。
5. 表达式→变量名的不可变元数据通过 FlatResultRow.BindingDisplayName 通知真实原模板，btnDel 可达；未绑定显示 btnLink。解绑校验仍为本结果的严格表达式，不能误删其它来源。
6. RefreshCurrentResultBindings 复用同一 current publication、schema 和 Presenter 既有 reader；不增 lease。MainWindowProductionEditing.RefreshProductionPublication 与 MainWindowDocumentHistory.DocumentHistoryChanged 各仅增加一行调用，保原历史/owner/epoch/窗口身份门禁。

## 文件边界

- src/Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/ResultPresentationModel.cs
- src/UI/Services/Monitoring/ResultPresentationPresenter.cs
- src/UI/Shell/Controls/Shell/ImageResultControl.xaml.cs
- src/UI/Shell/Windows/MainWindowViewModel.cs：仅绑定相关局部及旧假几何端点退出。
- src/UI/Shell/Windows/MainWindowResultBindingEditing.cs
- src/UI/Shell/Windows/MainWindowResultPublication.cs：绑定参数及 metadata 刷新。
- src/UI/Shell/Windows/MainWindowProductionEditing.cs：仅刷新一行，不覆盖 root 正修窗口 guard。
- src/UI/Shell/Windows/MainWindowDocumentHistory.cs：仅刷新一行。
- tests/SunEyeVision.UI.Tests/ResultPresentationModelTests.cs
- tests/SunEyeVision.UI.Tests/ResultPresentationPresenterTests.cs
- 新 tests/SunEyeVision.UI.Tests/ResultVariableBindingProductionTests.cs

未修改 Workflow/Manager/通信/Root资源/DeclaredPropertyList/共享API/Catalog/主题；未改 RootWorkspaceHostTests、NativeResultCompositionTests 或源交互测试。

## 验收源码与依赖

- Model：带标点的稳定字段ID保持原成员路径，Root Type/Storage 不变；All 未读值，不保索引。
- Presenter：保原分页/当前性/释放范围，新增声明态 All、资源禁用、真实样本与 All 端点隔离；原 record 根错误可绑定断言改为成员端点。
- 新专用生产测试：真实组件 loader/ImageLoad Runtime，真实结果控件与变量模态选择、绑定、原 btnDel 模板可达、Undo/Redo 同 publication、保存重开、解绑/Undo、取消、模态期 publication 关闭拒绝、非法端口拒绝，历史不变断言。
- 依赖主线整批提供 ProducerBinding.ReferenceExpression/SourceReference 新模型；GlobalProducerBindingRules.TryDescribeTarget(string,out Port,out Type,out string) 和 Validate(Solution,ProducerBinding,GlobalVariable)。不为本候选添加旧字段桥。
- 变量目标列表使用既有 SDK 词目及其数组形态向唯一 TryDescribeTarget 查询；最终实际变量准入始终以 Validate 为准。若共享 helper 有正式支持词目枚举，可直接替换候选枚举，不复制目标 Port 规则。
- 主线需将新专用测试类纳入正式回归入口；本路未擅改共享脚本入口。

## 验证与未验

11个候选 C# 文件以进程已加载 Roslyn 纯语法解析为0错；git diff --check通过。不是语义构建成绩。未构建私有 Release、未复制 DLL、未执行测试或正式程序，真实 VS/人工验收未做；必须待共同合同集成后按正式同源 Release 验证。
