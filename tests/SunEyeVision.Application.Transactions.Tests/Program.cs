using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using System.Collections.Immutable;

// Neutral component/transaction tests. This deliberately does not claim an installed
// ImageLoad/Blob payload or the WPF production manager has passed.
static void Check(bool condition, string message)
{ if (!condition) throw new InvalidOperationException(message); }
static void Reject(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
    throw new InvalidOperationException("应拒绝该操作。");
}
try
{
var component = new ToolComponentRegistration(new ToolComponentMetadata("transaction-probe", "TransactionProbe",
    ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(TransactionProbeDefinition));
// Only the fixture uses reflection to admit the existing neutral test declaration through
// RuntimeComponentAdmission. All graph edits below use the actual public application entry.
var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot)
    .GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { new[] { component } })!;
using var session = DocumentSession.CreateNew(directory);
var flow = session.GetWorkflows().Single();
var notifications = new List<DocumentChangeSet>();
session.Changed += (_, change) => notifications.Add(change);
NodeDefinition Node(string id) => new(id, id, "TransactionProbe",
    new ToolConfiguration("TransactionProbe", new FieldSettings(TransactionProbeDefinition.Schema.Inputs.Concat(TransactionProbeDefinition.Parameters.Inputs),
        new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(1L) })));

Check(session.HistoryEntries.Count == 0 && flow.Nodes.Count == 0, "新方案须有一个真实空流程。");
Check(session.CommitGraphEdit(session.PrepareGraphEdit(flow.Id), "空编辑") is null && notifications.Count == 0,
    "无变化不得生成历史或通知。");
Console.WriteLine("PASS 新建根流程与无变化提交");

var first = Node("first"); var second = Node("second");
var add = session.PrepareGraphEdit(flow.Id);
add.AddNode(first); add.AddNode(second);
session.CommitGraphEdit(add, "添加两个节点");
Check(flow.Nodes.Count == 2 && ReferenceEquals(flow.Nodes[0], first) && session.HistoryEntries.Count == 1,
    "复合编辑须一次安装真实实体并只生成一条历史。");
Console.WriteLine("PASS 真实节点复合提交");

var stale = session.PrepareGraphEdit(flow.Id);
var move = session.PrepareGraphEdit(flow.Id); move.MoveNode(first, new CanvasPoint(45, 87));
session.CommitGraphEdit(move, "移动节点");
Reject(() => session.CommitGraphEdit(stale, "过期草稿"));
Check(first.Position == new CanvasPoint(45, 87) && session.HistoryEntries.Count == 2, "过期拒绝不得破坏正式状态。");
Check(session.Undo() && first.Position == new CanvasPoint(0, 0), "撤销必须还原位置。");
Check(session.Redo() && first.Position == new CanvasPoint(45, 87), "重做必须复原位置。");
Console.WriteLine("PASS 过期草稿拒绝及移动撤销重做");

var edge = new ConnectionDefinition("wire", first.Id, second.Id, "image", "input.image");
var connect = session.PrepareGraphEdit(flow.Id); connect.AddConnection(edge);
session.CommitGraphEdit(connect, "连接节点");
Check(flow.Connections.Count == 1 && second.Configuration.Fields.Read("input.image") is ReferenceSetting,
    "连接须在同一事务生成自动输入引用。");
Check(session.Undo() && flow.Connections.Count == 0 && second.Configuration.Fields.Read("input.image") is not ReferenceSetting,
    "撤销须同步清除自动引用。");
Check(session.Redo() && ReferenceEquals(flow.Connections.Single(), edge), "重做应恢复同一连线实体。");
Console.WriteLine("PASS 连线及自动引用共同回放");

var remove = session.PrepareGraphEdit(flow.Id);
remove.DeleteNodesAndConnections(new[] { first }, Array.Empty<ConnectionDefinition>());
session.CommitGraphEdit(remove, "删除节点");
Check(flow.Nodes.Count == 1 && flow.Connections.Count == 0, "删除节点和连线必须原子完成。");
Check(session.Undo() && ReferenceEquals(flow.Nodes[0], first) && ReferenceEquals(flow.Connections[0], edge),
    "撤销须恢复实际原实体，不按ID造替身。");
Console.WriteLine("PASS 删除及实体身份恢复");

var owner = (ToolExecutionOwner)typeof(DocumentSession).GetProperty("ExecutionOwner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
var root = Guid.NewGuid(); owner.ActivateRoot(root);
try { Reject(() => session.CommitGraphEdit(session.PrepareGraphEdit(flow.Id), "运行中修改")); }
finally { owner.CompleteRoot(root); }
using (var lease = (IDisposable)typeof(ToolExecutionOwner).GetMethod("BeginDocumentMutation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, null)!)
{
    Reject(() => owner.ActivateRoot(Guid.NewGuid()));
    Reject(() => owner.Begin("probe", Guid.NewGuid(), "probe"));
    using var nested = (IDisposable)typeof(ToolExecutionOwner).GetMethod("BeginDocumentMutation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, null)!;
    Reject(() => owner.Retire());
}
Console.WriteLine("PASS 正式运行与编辑排他及嵌套租约");

session.Changed += (_, _) =>
{
    Reject(() => session.PrepareGraphEdit(flow.Id));
    throw new InvalidOperationException("受控观察者失败");
};
var rename = session.PrepareGraphEdit(flow.Id); rename.RenameNode(first, "已提交名称");
var renamed = session.CommitGraphEdit(rename, "重命名节点");
Check(first.DispName == "已提交名称" && session.LastObserverError is AggregateException,
    "观察者失败不得回滚已经提交的图和历史。");
Check(renamed!.ExecutionInvalidatedNodes.Length == 0, "名称变更不能冒充执行配置变更。");
Check(session.Undo() && first.DispName == "", "观察者失败后历史仍应可正常撤销。");
Console.WriteLine("PASS 通知拒绝重入且观察者失败不回滚");
Check(notifications.Zip(notifications.Skip(1), (a, b) => b.Revision == a.Revision + 1).All(value => value),
    "图编辑与历史回放须共用连续文档变更版本。");
session.Dispose();
Reject(() => session.PrepareGraphEdit(flow.Id));
Console.WriteLine("PASS 连续版本与关闭后拒绝");

using var manager = new DocumentSessionManager(directory);
var editedSession = manager.CreateNew();
var editedFlow = editedSession.GetWorkflows().Single();
var editableNode = new NodeDefinition("editable", "editable", "TransactionProbe",
    new ToolConfiguration("TransactionProbe", new FieldSettings(TransactionProbeDefinition.Schema.Inputs.Concat(TransactionProbeDefinition.Parameters.Inputs),
        new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(1L) })));
var addEditable = editedSession.PrepareGraphEdit(editedFlow.Id); addEditable.AddNode(editableNode);
editedSession.CommitGraphEdit(addEditable, "新增可编辑节点");
var host = editedSession.Editing.Host;
var parameterDraft = host.BeginEdit(editedFlow.Id, editableNode.Id);
parameterDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(8L) });
var parameters = DocumentEditProposal.Prepare(host, nodes: new[] { parameterDraft });
editedSession.ValidateEdit(parameters);
Check((long)((ConstantSetting)editableNode.Configuration.Fields.Read("threshold")).Value! == 1L, "验证不能安装草稿。");
var parameterReceipt = editedSession.CommitEdit(parameters);
Check(parameterReceipt.History is not null && (long)((ConstantSetting)editableNode.Configuration.Fields.Read("threshold")).Value! == 8L,
    "参数编辑应通过正式事务安装。");
Reject(() => editedSession.CommitEdit(parameters));
Check(editedSession.Undo() && (long)((ConstantSetting)editableNode.Configuration.Fields.Read("threshold")).Value! == 1L, "参数撤销失败。");
Check(editedSession.Redo(), "参数重做失败。");
Console.WriteLine("PASS 正式参数提案验证提交与统一历史");

using var variables = host.BeginVariableEdit();
var candidateVariable = new GlobalVariable("计数", new long[] { 1, 2 }, "Integer") { Id = "count" };
variables.Variables.Add(candidateVariable);
var variableProposal = DocumentEditProposal.Prepare(host, variables: variables);
var variableReceipt = editedSession.CommitEdit(variableProposal);
var actualVariable = variableReceipt.AcceptedVariables[candidateVariable];
Check(!ReferenceEquals(actualVariable, candidateVariable) && ReferenceEquals(actualVariable, editedSession.Document.GlobalVariables.Single()),
    "回执须返回草稿到正式实例映射。");
actualVariable.Value = new long[] { 7, 8 }; // Unit simulation of a later runtime value, not a real execution claim.
Check(editedSession.Undo() && editedSession.Document.GlobalVariables.Count == 0, "新增变量撤销失败。");
Check(editedSession.Redo() && ReferenceEquals(editedSession.Document.GlobalVariables.Single(), actualVariable) &&
    ((long[])actualVariable.Value!)[0] == 7, "历史不得重放旧变量运行值或另造实际实体。");
Console.WriteLine("PASS 新变量实际实例回执与历史保留运行值");

using var valueDraft = host.BeginVariableEdit();
valueDraft.SetValue(actualVariable.Id, new long[] { 9, 10 });
var valueProposal = DocumentEditProposal.Prepare(host, variables: valueDraft);
var historyCount = editedSession.HistoryEntries.Count;
var valueReceipt = editedSession.CommitEdit(valueProposal);
Check(valueReceipt.History is null && editedSession.HistoryEntries.Count == historyCount && ((long[])actualVariable.Value!)[0] == 9,
    "当前值写入不应生成配置历史。");
using var staleValueDraft = host.BeginVariableEdit(); staleValueDraft.SetValue(actualVariable.Id, new long[] { 11 });
var staleValue = DocumentEditProposal.Prepare(host, variables: staleValueDraft);
actualVariable.Value = new long[] { 12 };
Reject(() => editedSession.CommitEdit(staleValue));
Check(((long[])actualVariable.Value!)[0] == 12 && editedSession.HistoryEntries.Count == historyCount, "值冲突必须整批拒绝且保留正式值。");
Console.WriteLine("PASS 显式值无配置历史与并发修订冲突拒绝");

var rootDraft = host.BeginRootEdit(editedFlow.Id);
rootDraft.Configuration = new RootWorkflowConfiguration { RunParameters = ImmutableArray.Create(
    new RootParameterProxy(Guid.NewGuid(), editableNode.Id, editableNode.ToolType, "threshold", "threshold", "阈值")) };
var combinedNode = host.BeginEdit(editedFlow.Id, editableNode.Id);
combinedNode.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(20L) });
var combined = DocumentEditProposal.Prepare(host, nodes: new[] { combinedNode }, roots: new[] { rootDraft });
var combinedReceipt = editedSession.CommitEdit(combined);
Check(combinedReceipt.Change!.ChangedWorkflows.Length == 1 && editedFlow.RootConfiguration.RunParameters.Length == 1,
    "根配置和节点编辑应共同发布。");
Check(editedSession.Undo() && editedFlow.RootConfiguration.RunParameters.Length == 0 &&
    (long)((ConstantSetting)editableNode.Configuration.Fields.Read("threshold")).Value! == 8L, "复合根配置撤销失败。");
Console.WriteLine("PASS 根配置与参数共同提交和回放");

var managerOwner = (ToolExecutionOwner)typeof(DocumentSession).GetProperty("ExecutionOwner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editedSession)!;
var managerRun = Guid.NewGuid(); managerOwner.ActivateRoot(managerRun);
try { Reject(() => manager.CreateNew()); Check(ReferenceEquals(manager.CurrentSession, editedSession), "活动根运行中替换应保留旧会话。"); }
finally { managerOwner.CompleteRoot(managerRun); }
var replacement = manager.CreateNew();
Check(!ReferenceEquals(replacement, editedSession) && ReferenceEquals(manager.CurrentSession, replacement), "唯一管理器替换失败。");
Console.WriteLine("PASS 活动根流程间隙关闭保护与唯一会话替换");
var createdFlow = replacement.GetWorkflows().Single();
var create = replacement.PrepareGraphEdit(createdFlow.Id);
Reject(() => replacement.CreateNode(create, "missing-tool", new CanvasPoint(1, 2)));
var createdFirst = replacement.CreateNode(create, "TransactionProbe", new CanvasPoint(12, 24));
var createdSecond = replacement.CreateNode(create, "TransactionProbe", new CanvasPoint(24, 48));
Check(createdFlow.Nodes.Count == 0 && createdFirst.GlobalIndex == 1 && createdSecond.GlobalIndex == 2 &&
    createdFirst.LocalIndex == 1 && createdSecond.LocalIndex == 2, "失败创建不得消耗序号，成功创建只能进入私有提案。");
replacement.CommitGraphEdit(create, "可信工具节点创建");
Check(ReferenceEquals(createdFlow.Nodes[0], createdFirst), "节点创建提交须保实际实体。");
Console.WriteLine("PASS 可信目录节点工厂和提案内序号分配");
var activity = replacement.AcquireActivityLease("通信测试驱动");
Reject(() => manager.CreateNew());
Check(ReferenceEquals(manager.CurrentSession, replacement) && replacement.ActiveActivities.Count == 1, "活动尚未排空时必须保留旧会话。");
Task.Run(activity.Dispose).GetAwaiter().GetResult();
Check(replacement.ActiveActivities.Count == 0, "异步排空释放未生效。");
manager.CloseCurrent();
Console.WriteLine("PASS 实际后台活动关闭保护及异步释放");
var lockingSession = manager.CreateNew();
var lockingOwner = (ToolExecutionOwner)typeof(DocumentSession).GetProperty("ExecutionOwner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(lockingSession)!;
var lateIdentity = lockingOwner.Begin("late", Guid.NewGuid(), "test");
using var entered = new ManualResetEventSlim();
using var releaseCallback = new ManualResetEventSlim();
using var finished = new ManualResetEventSlim();
var watchdog = new Thread(() =>
{
    if (!finished.Wait(TimeSpan.FromSeconds(8)))
    { Console.Error.WriteLine("FAIL 运行发布与方案编辑发生锁序死锁。"); Environment.Exit(97); }
}) { IsBackground = true };
watchdog.Start();
var publisher = Task.Run(() => lateIdentity.TryPublish(() =>
{
    entered.Set();
    releaseCallback.Wait();
    _ = lockingSession.GetWorkflows();
}));
Check(entered.Wait(TimeSpan.FromSeconds(2)), "迟到发布没有进入真实owner门禁。");
var releaseTask = Task.Run(() => { Thread.Sleep(100); releaseCallback.Set(); });
var lockingFlow = lockingSession.GetWorkflows().Single();
lockingSession.CommitGraphEdit(lockingSession.PrepareGraphEdit(lockingFlow.Id), "锁序回归");
Check(publisher.GetAwaiter().GetResult(), "已有合法发布应完成后再进入编辑。");
releaseTask.GetAwaiter().GetResult();
finished.Set(); watchdog.Join();
Console.WriteLine("PASS 真实迟到发布回调与文档编辑锁序");
var presentationSession = manager.CreateNew();
var presentationFlow = presentationSession.GetWorkflows().Single();
var presentationDraft = presentationSession.PrepareGraphEdit(presentationFlow.Id);
var presentationNode = presentationSession.CreateNode(presentationDraft, "TransactionProbe", new CanvasPoint(0, 0));
presentationSession.CommitGraphEdit(presentationDraft, "呈现节点");
var presentation = presentationSession.AdmitPresentation(presentationFlow.Id, presentationNode.Id);
Check(presentation.IsCurrent && presentationSession.GetNodeSettingsSchema(presentationFlow.Id, presentationNode.Id)!.Inputs.Any(port => port.Id == "threshold"),
    "呈现必须使用实际节点声明。");
var presentationMove = presentationSession.PrepareGraphEdit(presentationFlow.Id);
presentationMove.MoveNode(presentationNode, new CanvasPoint(2, 3));
presentationSession.CommitGraphEdit(presentationMove, "呈现保持");
Check(presentation.IsCurrent, "位置编辑不得错误退役节点呈现。");
var retirementCalls = 0;
using var presentationRegistration = presentation.RetirementToken.Register(() =>
{
    retirementCalls++;
    Check(!presentation.IsCurrent, "退役回调不得看到仍可用的能力。");
    Reject(() => presentationSession.PrepareGraphEdit(presentationFlow.Id));
    // Another thread must be able to read while cleanup callbacks are running.
    Check(Task.Run(() => presentationSession.GetWorkflows().Count).Wait(TimeSpan.FromSeconds(2)),
        "呈现清理不能在持有方案门锁时调用。");
    throw new InvalidOperationException("受控呈现清理失败");
});
var presentationDelete = presentationSession.PrepareGraphEdit(presentationFlow.Id);
presentationDelete.DeleteNodesAndConnections(new[] { presentationNode }, Array.Empty<ConnectionDefinition>());
presentationSession.CommitGraphEdit(presentationDelete, "删除呈现节点");
Check(retirementCalls == 1 && !presentation.IsCurrent && presentationSession.LastPresentationReleaseError is AggregateException,
    "清理失败不得回滚删除，且必须保留诊断。");
Check(presentationSession.Undo() && ReferenceEquals(presentationFlow.Nodes.Single(), presentationNode) && !presentation.IsCurrent,
    "撤销恢复实体不能复活已退出窗口的呈现能力。");
var restoredPresentation = presentationSession.AdmitPresentation(presentationFlow.Id, presentationNode.Id);
Check(restoredPresentation.IsCurrent, "恢复实体须重新准入独立呈现生命周期。");
manager.CloseCurrent();
Check(!restoredPresentation.IsCurrent && restoredPresentation.RetirementToken.IsCancellationRequested,
    "方案关闭必须退役仍打开的呈现能力。");
Console.WriteLine("PASS 节点呈现准入、删除退役、回调隔离、撤销不复活和方案关闭");
var saveSession = manager.CreateNew();
var saveFlow = saveSession.GetWorkflows().Single();
var saveGraph = saveSession.PrepareGraphEdit(saveFlow.Id);
var saveNode = saveSession.CreateNode(saveGraph, "TransactionProbe", new CanvasPoint(0, 0));
saveSession.CommitGraphEdit(saveGraph, "保存测试节点");
var saveHost = saveSession.Editing.Host;
var saveNodeDraft = saveHost.BeginEdit(saveFlow.Id, saveNode.Id);
var saveRootDraft = saveHost.BeginRootEdit(saveFlow.Id);
saveNodeDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(31L) });
saveRootDraft.Configuration = new RootWorkflowConfiguration { RunParameters = ImmutableArray.Create(
    new RootParameterProxy(Guid.NewGuid(), saveNode.Id, saveNode.ToolType, "threshold", "threshold", "保存阈值")) };
var acknowledgeCount = 0; var editorPending = true;
var saveParticipant = new SaveParticipant("保存参数与根配置",
    () => editorPending,
    host => DocumentEditProposal.Prepare(host, nodes: new[] { saveNodeDraft }, roots: new[] { saveRootDraft }),
    (proposal, receipt) =>
    {
        Check(ReferenceEquals(receipt.Session, saveSession), "回执须属于原会话。");
        saveNodeDraft = saveHost.RebaseAcceptedDraft(saveNodeDraft, proposal);
        saveRootDraft = saveHost.RebaseAcceptedDraft(saveRootDraft, proposal);
        acknowledgeCount++; editorPending = false;
    });
saveSession.Editing.Register(saveParticipant);
var saveRoot = Path.Combine(AppContext.BaseDirectory, "_validation", "accepted-edit-save", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(saveRoot);
var savePath = Path.Combine(saveRoot, "accepted.solution");
var saveTypes = new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>());
var saveOptions = new System.Text.Json.JsonSerializerOptions();
saveSession.SaveAs(savePath, Path.Combine(saveRoot, "backup"), saveRoot, saveTypes, saveOptions, _ => null);
Check(acknowledgeCount == 1 && !saveParticipant.HasPendingChanges && saveFlow.RootConfiguration.RunParameters.Length == 1,
    "另存须整批接受编辑并重基窗口。");
void ChangeSavedThreshold(long value) { saveNodeDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(value) }); editorPending = true; }
ChangeSavedThreshold(32L);
saveSession.Save(savePath, Path.Combine(saveRoot, "backup"), saveTypes, saveOptions);
Check(acknowledgeCount == 2 && !saveParticipant.HasPendingChanges, "保存必须接受重基后的第二次编辑。");
ChangeSavedThreshold(33L);
var copyPath = Path.Combine(saveRoot, "copy.solution");
saveSession.SaveCopy(copyPath, Path.Combine(saveRoot, "copy-backup"), saveRoot, saveTypes, saveOptions, _ => null);
Check(acknowledgeCount == 3 && saveSession.LastWrittenSnapshot!.TargetPath == savePath, "保存副本接受编辑但不改活动文件身份。");
ChangeSavedThreshold(34L);
_ = saveSession.CaptureForSave(saveTypes, saveOptions);
Check(acknowledgeCount == 4 && !saveParticipant.HasPendingChanges, "显式保存捕获须使用同一编辑链。");
ChangeSavedThreshold(35L);
using (var saveCanceled = new CancellationTokenSource())
{
    saveCanceled.Cancel();
    try { saveSession.Save(savePath, Path.Combine(saveRoot, "backup"), saveTypes, saveOptions, cancellationToken: saveCanceled.Token); throw new Exception("应取消保存。"); }
    catch (OperationCanceledException) { }
}
Check(acknowledgeCount == 4 && saveParticipant.HasPendingChanges &&
    (long)((ConstantSetting)saveNode.Configuration.Fields.Read("threshold")).Value! == 34L,
    "预先取消不得接受私有编辑。");
saveSession.Editing.Unregister(saveParticipant.ScopeId);
var reopenedSave = manager.Open(copyPath, _ => saveTypes, _ => null, saveOptions);
var reopenedSaveNode = reopenedSave.GetNodes(saveFlow.Id).Single();
Check((long)((ConstantSetting)reopenedSaveNode.Configuration.Fields.Read("threshold")).Value! == 33L &&
    reopenedSave.GetWorkflows().Single().RootConfiguration.RunParameters.Length == 1,
    "真实重开必须读到副本中的参数与根配置。");
Console.WriteLine("PASS 保存、另存、副本及捕获共同接受编辑回执，取消保留草稿和真实重开");
var failedAckDraft = reopenedSave.Editing.Host.BeginEdit(saveFlow.Id, reopenedSaveNode.Id);
failedAckDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["threshold"] = new ConstantSetting(44L) });
var beforeFailedAck = File.ReadAllBytes(copyPath);
var failedAck = new SaveParticipant("受控回执失败", () => true,
    host => DocumentEditProposal.Prepare(host, nodes: new[] { failedAckDraft }),
    (_, _) => throw new InvalidOperationException("受控窗口刷新失败"));
reopenedSave.Editing.Register(failedAck);
try { reopenedSave.Save(copyPath, Path.Combine(saveRoot, "copy-backup"), saveTypes, saveOptions); throw new Exception("回执失败须报告。"); }
catch (AggregateException) { }
Check(beforeFailedAck.SequenceEqual(File.ReadAllBytes(copyPath)) &&
    (long)((ConstantSetting)reopenedSaveNode.Configuration.Fields.Read("threshold")).Value! == 44L,
    "回执失败不得写文件，也不得伪装回滚已正式接受的编辑。");
reopenedSave.Editing.Unregister(failedAck.ScopeId);
var prepareFailure = new SaveParticipant("受控准备失败", () => true,
    _ => throw new InvalidOperationException("受控准备失败"), (_, _) => throw new Exception("不得确认失败提案"));
reopenedSave.Editing.Register(prepareFailure);
Reject(() => reopenedSave.Save(copyPath, Path.Combine(saveRoot, "copy-backup"), saveTypes, saveOptions));
Check(beforeFailedAck.SequenceEqual(File.ReadAllBytes(copyPath)), "准备失败不得覆盖已有文件。");
reopenedSave.Editing.Unregister(prepareFailure.ScopeId);
reopenedSave.Save(copyPath, Path.Combine(saveRoot, "copy-backup"), saveTypes, saveOptions);
var failedAckRecovered = manager.Open(copyPath, _ => saveTypes, _ => null, saveOptions);
Check((long)((ConstantSetting)failedAckRecovered.GetNodes(saveFlow.Id).Single().Configuration.Fields.Read("threshold")).Value! == 44L,
    "处理失败参与者后须能保存并真实重开已接受编辑。");
Console.WriteLine("PASS 保存准备与回执失败保护原文件，已提交编辑保留并可再次保存重开");
var resourceRegistration = new ToolComponentRegistration(new ToolComponentMetadata("resource-probe", "ResourceProbe",
    ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(ResourceProbeDefinition));
var resourceDirectory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
    .Invoke(null, new object[] { new[] { resourceRegistration } })!;
using var resourceManager = new DocumentSessionManager(resourceDirectory);
var resourceSession = resourceManager.CreateNew();
var resourceFlow = resourceSession.GetWorkflows().Single();
var resourceGraph = resourceSession.PrepareGraphEdit(resourceFlow.Id);
var resourceNode = resourceSession.CreateNode(resourceGraph, "ResourceProbe", new CanvasPoint(0, 0));
resourceSession.CommitGraphEdit(resourceGraph, "资源节点");
var resourceDraft = resourceSession.Editing.Host.BeginEdit(resourceFlow.Id, resourceNode.Id);
resourceDraft.ApplyFields(new Dictionary<string, ParameterSetting> { ["file"] = new ConstantSetting("asset.txt") });
resourceSession.CommitEdit(DocumentEditProposal.Prepare(resourceSession.Editing.Host, nodes: new[] { resourceDraft }));
var resourceRoot = Path.Combine(saveRoot, "resources");
var movedRoot = Path.Combine(resourceRoot, "moved"); var copyRoot = Path.Combine(resourceRoot, "copy");
Directory.CreateDirectory(movedRoot); Directory.CreateDirectory(copyRoot);
var absoluteResource = Path.Combine(resourceRoot, "asset.txt"); File.WriteAllText(absoluteResource, "resource");
var originalPath = Path.Combine(resourceRoot, "original.solution"); var movedPath = Path.Combine(movedRoot, "moved.solution");
resourceSession.SaveAs(originalPath, Path.Combine(resourceRoot, "backup"), resourceRoot, saveTypes, saveOptions, _ => null);
var movedReceipt = resourceSession.SaveAs(movedPath, Path.Combine(resourceRoot, "backup"), resourceRoot, saveTypes, saveOptions, _ => null);
Check(movedReceipt.WrittenSnapshot.ResourcePlan.Single().AbsolutePath == absoluteResource && resourceSession.ResourceBaseDirectory == resourceRoot,
    "另存为不得改变live资源基址。");
Check((string)((ConstantSetting)resourceNode.Configuration.Fields.Read("file")).Value! == "asset.txt" &&
    resourceSession.CreateResourcePathResolver().Resolve("file", "asset.txt", ResourcePurpose.FileRead).Address == absoluteResource,
    "运行与编辑解析须仍指向同一实际资源，不能悄然改写live字段。");
var resourceSavedAgain = resourceSession.Save(movedPath, Path.Combine(resourceRoot, "backup"), saveTypes, saveOptions);
Check(resourceSavedAgain.WrittenSnapshot.ResourcePlan.Single().AbsolutePath == absoluteResource,
    "跨目录另存后再次保存必须保持资源绝对身份。");
Reject(() => resourceSession.Save(movedPath, Path.Combine(resourceRoot, "backup"), saveTypes, saveOptions, sourceDirectory: movedRoot));
var resourceCopyPath = Path.Combine(copyRoot, "copy.solution");
var resourceCopy = resourceSession.SaveCopy(resourceCopyPath, Path.Combine(resourceRoot, "backup"), resourceRoot, saveTypes, saveOptions, _ => null);
Check(resourceCopy.WrittenSnapshot.ResourcePlan.Single().AbsolutePath == absoluteResource && resourceSession.LastWrittenSnapshot!.TargetPath == movedPath,
    "副本不能改变live基址和当前文件。");
Check(resourceSession.CaptureForSave(saveTypes, saveOptions).BindTarget(movedPath).ResourcePlan.Single().AbsolutePath == absoluteResource,
    "显式保存捕获也须从live基址重定位到当前文件。");
var resourceReopened = resourceManager.Open(movedPath, _ => saveTypes, _ => null, saveOptions);
var relativeAfterOpen = (string)((ConstantSetting)resourceReopened.GetNodes(resourceFlow.Id).Single().Configuration.Fields.Read("file")).Value!;
Check(resourceReopened.ResourceBaseDirectory == movedRoot &&
    resourceReopened.CreateResourcePathResolver().Resolve("file", relativeAfterOpen, ResourcePurpose.FileRead).Address == absoluteResource,
    "重开应以实际打开文件目录解释已重定位字段。");
var reopenedResourceSave = resourceReopened.Save(movedPath, Path.Combine(resourceRoot, "backup"), saveTypes, saveOptions);
Check(reopenedResourceSave.WrittenSnapshot.ResourcePlan.Single().AbsolutePath == absoluteResource, "重开后保存应保持资源身份。");
Console.WriteLine("PASS 相对资源另存、重复保存、副本、捕获、实际解析与重开同一语义基址");
Console.WriteLine("20 个事务场景通过；真实工具、WPF及完整运行仍待整批验证。");
return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

public sealed record TransactionSettings : IToolSettings<TransactionSettings>
{
    public long Threshold { get; init; } = 1;
    public TransactionSettings Freeze() => this with { };
}
public sealed class TransactionProbeDefinition : IToolDefinition<TransactionSettings, WorkspaceProbeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    private readonly WorkspaceProbeDefinition _inner = new();
    public static readonly ToolSchema Schema = new(
        new[] { new Port("input.image", TypeRef.ResourceOf(ResourceKind.Image), Required: false, EditModes: FieldEditModes.Reference) },
        WorkspaceProbeDefinition.Schema.Outputs, workflowImageInputId: "input.image");
    public string ToolId => "TransactionProbe";
    public ToolCatalogDescriptor Catalog => _inner.Catalog;
    public ToolSchema Describe() => Schema;
    public static readonly ToolSchema Parameters = new(new[] { new Port("threshold", TypeRef.ScalarOf(ScalarKind.Int64),
        EditModes: FieldEditModes.Constant | FieldEditModes.Reference) }, Array.Empty<Port>());
    public ToolSchema SettingsSchema => Parameters;
    public InputValues ReadSettings(object settings) => new(Parameters.Inputs, new Dictionary<string, object?> { ["threshold"] = ((TransactionSettings)settings).Threshold });
    public object MaterializeSettings(InputValues values) => new TransactionSettings { Threshold = values.Get<long>("threshold") };
    public IReadOnlyList<Diagnostic> Validate(TransactionSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<TransactionSettings, WorkspaceProbeOutput> CreateSession() => throw new NotSupportedException("仅事务准入探针。");
}

public sealed class SaveParticipant(string key, Func<bool> pending,
    Func<NodeConfigurationEditHost, DocumentEditProposal> prepare,
    Action<DocumentEditProposal, DocumentEditReceipt> acknowledge) : IDocumentEditParticipant
{
    public Guid ScopeId { get; } = Guid.NewGuid();
    public string ScopeKey => key;
    public bool HasPendingChanges => pending();
    public bool IsValid => true;
    public DocumentEditProposal Prepare(NodeConfigurationEditHost host) => prepare(host);
    public void Acknowledge(DocumentEditProposal proposal, DocumentEditReceipt receipt) => acknowledge(proposal, receipt);
    public void Cancel() { }
}
public sealed record ResourceProbeSettings : IToolSettings<ResourceProbeSettings>
{
    public string File { get; init; } = "";
    public ResourceProbeSettings Freeze() => this with { };
}
public sealed class ResourceProbeDefinition : IToolDefinition<ResourceProbeSettings, WorkspaceProbeOutput>, IToolSettingsFields,
    IToolCatalogProvider, IToolResourceFieldDeclarations
{
    public string ToolId => "ResourceProbe";
    public ToolCatalogDescriptor Catalog { get; } = new("资源测试", "验证路径语义", "generic", "测试", 0);
    public ToolSchema Describe() => WorkspaceProbeDefinition.Schema;
    public ToolSchema SettingsSchema { get; } = new(new[] { new Port("file", TypeRef.ScalarOf(ScalarKind.Text),
        Required: false, EditModes: FieldEditModes.Constant) }, Array.Empty<Port>());
    public IReadOnlyList<ResourceFieldDeclaration> ResourceFields { get; } = new[] {
        new ResourceFieldDeclaration("file", ResourcePurpose.FileRead, ResourceReferenceCardinality.Scalar) };
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs,
        new Dictionary<string, object?> { ["file"] = ((ResourceProbeSettings)settings).File });
    public object MaterializeSettings(InputValues values) => new ResourceProbeSettings { File = values.Get<string>("file") };
    public IReadOnlyList<Diagnostic> Validate(ResourceProbeSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<ResourceProbeSettings, WorkspaceProbeOutput> CreateSession() => throw new NotSupportedException("资源持久化测试，不执行算法。");
}