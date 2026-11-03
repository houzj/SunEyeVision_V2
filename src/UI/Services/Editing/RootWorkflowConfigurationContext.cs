using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>One root and its node drafts, published through the owning document session.</summary>
internal sealed class RootWorkflowConfigurationContext : IDocumentEditParticipant, IDisposable
{
 private readonly DocumentSession _session;
 private readonly DocumentEditSession _edits;
 private readonly NodeConfigurationEditHost _host;
 private readonly Func<bool> _isCurrent, _canEdit;
 private readonly Action<DocumentEditReceipt> _published;
 private RootWorkflowConfigurationEditDraft _root;
 private readonly Dictionary<string, NodeConfigurationEditDraft> _nodes = new(StringComparer.Ordinal);
 private readonly Dictionary<(string NodeId, string FieldId), ParameterSetting> _opening = new();
 private DocumentEditProposal? _prepared;
 private string[] _preparedNodes = Array.Empty<string>();
 private bool _active = true;
 private int _undoCount, _redoCount;
 private long _historyVersion;
 internal WorkflowDefinition Root { get; }
 internal Func<string?>? PrepareView { get; set; }
 internal Func<bool>? HasPendingView { get; set; }
 internal event Action? Changed;
 public Guid ScopeId { get; } = Guid.NewGuid();
 public string ScopeKey => "root-configuration:" + Root.Id;
 public bool IsValid
 {
  get
  {
   if (!_active || !_isCurrent()) return false;
   try { return _host.OwnsRoot(_root) && _nodes.Values.All(draft => _host.OwnsNode(Root.Id, draft.Node)); }
   catch (InvalidOperationException) { return false; }
  }
 }
 public bool HasPendingChanges => _active && (_host.HasPendingChanges(_root) ||
  _nodes.Values.Any(_host.HasPendingChanges) || HasPendingView?.Invoke() == true);
 internal bool CanEdit => IsValid && _canEdit();
 internal RootWorkflowConfiguration Configuration => _root.Configuration;
 internal ToolSchema? GetSchema(string nodeId) => _session.GetNodeSchema(Root.Id, nodeId);
 internal RootWorkflowConfigurationContext(DocumentSession session, WorkflowDefinition root,
  Func<bool> isCurrent, Func<bool> canEdit, Action<DocumentEditReceipt> published)
 {
  ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(root);
  if (root.IsSubroutine) throw new InvalidOperationException("Group 不属于根配置编辑范围。");
  if (!session.GetWorkflows().Any(item => ReferenceEquals(item, root)))
   throw new InvalidOperationException("根流程不属于当前方案会话。");
  _session = session; Root = root; _edits = session.Editing; _host = _edits.Host;
  _isCurrent = isCurrent; _canEdit = canEdit; _published = published;
  _root = _host.BeginRootEdit(root.Id); _historyVersion = session.ChangeRevision; _edits.Register(this);
 }
 private void Ensure(bool edit = false)
 { if (!IsValid || edit && !_canEdit()) throw new InvalidOperationException("根配置所属方案、流程或编辑状态已经变化。"); }
 private NodeConfigurationEditDraft Candidate(string id)
 {
  Ensure();
  if (_nodes.TryGetValue(id, out var existing)) return existing;
  var node = Root.Nodes.SingleOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("代理来源节点已删除。");
  if (node.ToolType is "Subroutine" or "Condition" || !string.IsNullOrEmpty(node.SubroutineId))
   throw new InvalidOperationException("Group 参数迁移保持暂停。");
  var draft = _host.BeginEdit(Root.Id, id);
  if (!ReferenceEquals(draft.Node, node)) throw new InvalidOperationException("来源节点实例已变化。");
  _nodes.Add(id, draft); return draft;
 }
 internal IReadOnlyList<RootParameterCandidate> Candidates()
 {
  var result = new List<RootParameterCandidate>();
  foreach (var node in Root.Nodes)
  {
   try { result.AddRange(RootParameterCatalog.Describe(Candidate(node.Id))); }
   catch (InvalidOperationException error)
   { result.Add(new RootParameterCandidate(node, "", "", "参数不可用", null, error.Message)); }
  }
  return result;
 }
 internal RootParameterCandidate Resolve(RootParameterProxy proxy) => RootParameterCatalog.Resolve(Candidate(proxy.NodeId), proxy);
 internal RootParameterProxy Add(RootParameterCandidate candidate)
 {
  Ensure(true);
  if (!Root.Nodes.Any(node => ReferenceEquals(node, candidate.Node))) throw new InvalidOperationException("来源节点已变化。");
  if (candidate.Unavailable is not null || candidate.Field is null) throw new InvalidOperationException(candidate.Unavailable ?? "字段不可用。");
  var proxy = new RootParameterProxy(Guid.NewGuid(), candidate.Node.Id, candidate.Node.ToolType, candidate.FieldId, candidate.StoragePath, candidate.DisplayName);
  _ = Resolve(proxy);
  var next = _root.Configuration with { RunParameters = _root.Configuration.RunParameters.Add(proxy) };
  next.Validate(); _root.Configuration = next; Changed?.Invoke(); return proxy;
 }
 internal void Remove(RootParameterProxy proxy)
 {
  Ensure(true);
  if (_nodes.TryGetValue(proxy.NodeId, out var draft) && _opening.TryGetValue((proxy.NodeId, proxy.FieldId), out var opening))
  {
   draft.ApplyFields(new Dictionary<string, ParameterSetting> { [proxy.StoragePath] = opening });
   _opening.Remove((proxy.NodeId, proxy.FieldId));
  }
  _root.Configuration = _root.Configuration with { RunParameters = _root.Configuration.RunParameters.Remove(proxy) }; Changed?.Invoke();
 }
 internal ParameterSetting Read(RootParameterProxy proxy)
 {
  var target = Resolve(proxy); var draft = Candidate(proxy.NodeId);
  return (target.Resource ?? throw new InvalidOperationException("节点未提供正式参数声明。")).Read(draft);
 }
 internal void Write(RootParameterProxy proxy, ParameterSetting setting)
 {
  Ensure(true);
  if (setting is ReferenceSetting or VariableSetting) throw new InvalidOperationException("正式引用来源选择尚未接入。");
  var target = Resolve(proxy); var draft = Candidate(proxy.NodeId);
  var resource = target.Resource ?? throw new InvalidOperationException("节点未提供正式参数声明。");
  _opening.TryAdd((proxy.NodeId, proxy.FieldId), resource.Read(draft));
  resource.Write(draft, setting);
 }
 internal string? ValidateReference(RootParameterProxy proxy, ReferenceSetting reference) => "正式引用来源选择尚未接入。";
 public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
 {
  Ensure(); if (!ReferenceEquals(host, _host)) throw new InvalidOperationException("根配置编辑宿主不一致。");
  var error = PrepareView?.Invoke(); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
  _root.Configuration.Validate(); foreach (var proxy in Configuration.RunParameters) _ = Resolve(proxy);
  var changed = _nodes.Where(pair => _host.HasPendingChanges(pair.Value)).ToArray();
  var proposal = DocumentEditProposal.Prepare(host, changed.Select(pair => pair.Value), new[] { _root }, token: HasPendingChanges);
  _prepared = proposal; _preparedNodes = changed.Select(pair => pair.Key).ToArray();
  return proposal;
 }
 public void Acknowledge(DocumentEditProposal prepared, DocumentEditReceipt receipt)
 {
  if (!ReferenceEquals(receipt.Session, _session)) throw new InvalidOperationException("编辑回执属于其他方案。");
  if (!ReferenceEquals(_prepared, prepared)) throw new InvalidOperationException("未找到已准备的根配置草稿。");
  var included = _preparedNodes; _prepared = null; _preparedNodes = Array.Empty<string>();
  _root = _host.RebaseAcceptedDraft(_root, prepared);
  foreach (var id in _nodes.Keys.Except(included, StringComparer.Ordinal).ToArray())
   if (!_host.HasPendingChanges(_nodes[id])) _nodes.Remove(id);
  foreach (var id in included)
  {
   var draft = _nodes[id];
   var unchanged = prepared.Matches(draft);
   _nodes[id] = _host.RebaseAcceptedDraft(draft, prepared);
   if (unchanged)
    foreach (var key in _opening.Keys.Where(key => key.NodeId == id).ToArray()) _opening.Remove(key);
   else
    foreach (var key in _opening.Keys.Where(key => key.NodeId == id).ToArray())
     _opening[key] = draft.Node.Configuration.Fields.Read(key.FieldId);
  }
  if (receipt.History is not null && prepared.Token is true) { _undoCount++; _redoCount = 0; }
  _historyVersion = _session.ChangeRevision; _published(receipt); Changed?.Invoke();
 }
 internal bool TryApply(out string error)
 { try { Ensure(true); _edits.Apply(ScopeId); error = ""; return true; } catch (Exception failure) { error = failure.Message; return false; } }
 internal bool CanUndo => _undoCount > 0 && _historyVersion == _session.ChangeRevision && _session.CanUndo;
 internal bool CanRedo => _redoCount > 0 && _historyVersion == _session.ChangeRevision && _session.CanRedo;
 internal void Undo()
 {
  Ensure(true); if (HasPendingChanges || !CanUndo) throw new InvalidOperationException("请先应用编辑，或先撤销后续配置修改。");
  if (!_session.Undo()) throw new InvalidOperationException("撤销未执行。");
  _undoCount--; _redoCount++; Reload();
 }
 internal void Redo()
 {
  Ensure(true); if (HasPendingChanges || !CanRedo) throw new InvalidOperationException("重做范围已失效或尚有待提交编辑。");
  if (!_session.Redo()) throw new InvalidOperationException("重做未执行。");
  _redoCount--; _undoCount++; Reload();
 }
 private void Reload()
 { _root = _host.BeginRootEdit(Root.Id); _nodes.Clear(); _opening.Clear(); _historyVersion = _session.ChangeRevision; Changed?.Invoke(); }
 public void Cancel() => Dispose();
 public void Dispose()
 { if (!_active) return; _active = false; _edits.Unregister(ScopeId); _nodes.Clear(); _opening.Clear(); _prepared = null; _preparedNodes = Array.Empty<string>(); Changed = null; }
}
