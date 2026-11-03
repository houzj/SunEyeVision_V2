using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;

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
    new ToolConfiguration("TransactionProbe", new FieldSettings(TransactionProbeDefinition.Schema.Inputs,
        new Dictionary<string, ParameterSetting>())));

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
Console.WriteLine("8 个事务场景通过；真实工具、持久化、UI及完整运行仍待整批验证。");
return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

public sealed class TransactionProbeDefinition : IToolDefinition<WorkspaceProbeSettings, WorkspaceProbeOutput>, IToolSettingsFields, IToolCatalogProvider
{
    private readonly WorkspaceProbeDefinition _inner = new();
    public static readonly ToolSchema Schema = new(
        new[] { new Port("input.image", TypeRef.ResourceOf(ResourceKind.Image), Required: false, EditModes: FieldEditModes.Reference) },
        WorkspaceProbeDefinition.Schema.Outputs, workflowImageInputId: "input.image");
    public string ToolId => "TransactionProbe";
    public ToolCatalogDescriptor Catalog => _inner.Catalog;
    public ToolSchema Describe() => Schema;
    public ToolSchema SettingsSchema => _inner.SettingsSchema;
    public InputValues ReadSettings(object settings) => _inner.ReadSettings(settings);
    public object MaterializeSettings(InputValues values) => _inner.MaterializeSettings(values);
    public IReadOnlyList<Diagnostic> Validate(WorkspaceProbeSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<WorkspaceProbeSettings, WorkspaceProbeOutput> CreateSession() => throw new NotSupportedException("仅事务准入探针。");
}
