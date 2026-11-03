using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Services.Images;

/// <summary>One accepted selection, carrying the actual session, node and projection version.</summary>
internal sealed record ImageSourceActivation(DocumentSession Session, WorkflowDefinition Workflow, NodeDefinition Node,
 ImageInputSource Source, ImageInfo Image, int Index, long ProjectionVersion);

/// <summary>Owns the local image strip projection. The document and execution cursor have separate owners.</summary>
internal sealed class ImageSourceProjectionBinding : IDisposable
{
 private readonly DocumentSession _session;
 private readonly WorkflowDefinition _root;
 private readonly NodeDefinition _node;
 private readonly Func<bool> _isCurrent, _canEdit, _canNavigate;
 private readonly Action<DocumentEditReceipt>? _accepted;
 private readonly Func<ImageSourceActivation, Task>? _activate;
 private readonly Func<ImageSourceEditProposal, SourceDisplayPreservationScope?>? _prepareDisplay;
 private readonly Action<string, Exception>? _reportPresentationError;
 private readonly Dispatcher _dispatcher;
 private Func<Window?>? _dialogOwner;
 private bool _disposed, _ownCommit, _refreshQueued, _isLocal;
 internal ImageInputSource Source { get; }
 internal ImageSourceStripSession Strip { get; }
 internal bool CanActivate => _activate is not null && _isLocal && Current() && _canNavigate() && Current();
 internal Exception? LastPresentationError { get; private set; }
 internal bool Owns(DocumentSession session, WorkflowDefinition root, NodeDefinition node) =>
  ReferenceEquals(_session, session) && ReferenceEquals(_root, root) && ReferenceEquals(_node, node) && Current();

 internal ImageSourceProjectionBinding(DocumentSession session, WorkflowDefinition root, NodeDefinition node,
  Func<bool> isCurrent, Func<bool> canEdit, Func<bool> canNavigate, Action<DocumentEditReceipt>? accepted = null,
  Func<ImageSourceActivation, Task>? activate = null, Dispatcher? dispatcher = null,
  Func<ImageSourceEditProposal, SourceDisplayPreservationScope?>? prepareDisplay = null,
  Action<string, Exception>? reportPresentationError = null)
 {
  _session = session ?? throw new ArgumentNullException(nameof(session));
  _root = root ?? throw new ArgumentNullException(nameof(root));
  _node = node ?? throw new ArgumentNullException(nameof(node));
  _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
  _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
  _canNavigate = canNavigate ?? throw new ArgumentNullException(nameof(canNavigate));
  _accepted = accepted; _activate = activate; _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
  _prepareDisplay = prepareDisplay; _reportPresentationError = reportPresentationError;
  _dispatcher.VerifyAccess(); EnsureCurrent();
  if (_session.GetNodeHostInputDeclaration(_root.Id, _node) is not { CapabilityId: "image-source", Version: 1 })
   throw new InvalidOperationException("当前节点未准入图像源宿主能力。");
  Source = new ImageInputSource(_node.Id);
  try
  {
   var initial = ReadPolicy();
   Source.LoadFromPolicy(initial, _session.CreateResourcePathResolver());
   _isLocal = initial.SourceKind == ImageSourceKind.Local;
   Source.IsReadOnly = !_isLocal || !Current() || !_canEdit();
   Source.AttachFormalOwner(); Source.ConfigurationWriter = AcceptEdit;
   Strip = new ImageSourceStripSession(Source, Current, () => CanActivate, ActivateAsync, _dispatcher, () => _dialogOwner?.Invoke());
   _session.Changed += OnDocumentChanged;
  }
  catch(Exception failure)
  {
   try { Source.DetachFormalOwner(); Source.Dispose(); }
   catch(Exception cleanup) { throw new AggregateException("图像源投影初始化和清理均失败。", failure, cleanup); }
   throw;
  }
 }
 private bool Current()
 {
  if (_disposed || !_isCurrent() || _root.IsSubroutine) return false;
  try { return _session.GetWorkflows().Any(flow => ReferenceEquals(flow, _root)) &&
   _session.GetNodes(_root.Id).Any(node => ReferenceEquals(node, _node)) && !_disposed && _isCurrent(); }
  catch (InvalidOperationException) { return false; }
 }
 private void EnsureCurrent(bool edit = false)
 {
  _dispatcher.VerifyAccess();
  if (!Current() || edit && (!_canEdit() || !Current()))
   throw new InvalidOperationException("图像源所属方案、流程、节点或编辑状态已变化。");
 }
 internal void RefreshAvailability()
 {
  _dispatcher.VerifyAccess();
  if (!_disposed)
  {
   Source.IsReadOnly = !_isLocal || !Current() || !_canEdit();
   Strip.NotifyAvailabilityChanged();
  }
 }
 internal void SetDialogOwner(Func<Window?>? owner) { _dispatcher.VerifyAccess(); _dialogOwner = owner; }
 internal void RefreshFromDocument()
 {
  _dispatcher.VerifyAccess(); EnsureCurrent();
  var current = ReadPolicy();
  var displayed = Source.CapturePolicy();
  var resolver = _session.CreateResourcePathResolver();
  var same = displayed.CurrentIndex == current.CurrentIndex && displayed.RunMode == current.RunMode &&
   displayed.AutoSwitchEnabled == current.AutoSwitchEnabled && displayed.FilePaths.SequenceEqual(current.FilePaths) &&
   displayed.ForRunPaths.SequenceEqual(current.ForRunPaths) &&
   Source.Images.Select(image => image.FilePath).SequenceEqual(current.FilePaths.Select(reference =>
    resolver.Resolve("local.paths", reference, ResourcePurpose.FileRead).Address), StringComparer.OrdinalIgnoreCase);
  if (!same) Source.LoadFromPolicy(current, resolver);
  _isLocal = current.SourceKind == ImageSourceKind.Local;
  RefreshAvailability();
 }
 private ImageSourcePolicy ReadPolicy()
 {
  EnsureCurrent();
  var draft = _session.Editing.Host.BeginEdit(_root.Id, _node.Id);
  if (!ReferenceEquals(draft.Node, _node)) throw new InvalidOperationException("图像源节点已被替换。");
  var values = draft.Configuration.Fields.Select(draft.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>());
  return ImageSourcePolicy.ReadFields(_node.Id, values);
 }
 private void AcceptEdit(ImageSourceEditProposal proposal)
 {
  ArgumentNullException.ThrowIfNull(proposal); EnsureCurrent(true);
  if (_ownCommit) throw new InvalidOperationException("图像源编辑不能重入。");
  var draft = _session.Editing.Host.BeginEdit(_root.Id, _node.Id);
  if (!ReferenceEquals(draft.Node, _node)) throw new InvalidOperationException("图像源节点已被替换。");
  var values = draft.Configuration.Fields.Select(draft.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>());
  var policy = ImageSourcePolicy.ReadFields(_node.Id, values);
  if (policy.SourceKind != ImageSourceKind.Local || proposal.Configuration.NodeId != _node.Id)
   throw new InvalidOperationException("当前来源不是本地图像，不能修改列表。");
  var retainDisplay = !proposal.SelectStart && policy.CurrentIndex == proposal.Configuration.CurrentIndex &&
   policy.FilePaths.Count <= proposal.Configuration.FilePaths.Count &&
   policy.FilePaths.SequenceEqual(proposal.Configuration.FilePaths.Take(policy.FilePaths.Count));
  policy.UpdateLocalInput(proposal.Configuration);
  var localFields = new HashSet<string>(StringComparer.Ordinal)
   { "local.paths", "local.selected", "local.start", "local.run-mode", "local.auto-switch" };
  var proposed = policy.ToFieldValues(draft.SettingsSchema).Where(pair => localFields.Contains(pair.Key))
   .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
  if (proposed.Count != localFields.Count) throw new InvalidOperationException("图像源本地列表声明不完整。");
  draft.ApplyFields(proposed);
  var frozen = DocumentEditProposal.Prepare(_session.Editing.Host, nodes: new[] { draft });
  SourceDisplayPreservationScope? display = null;
  if (retainDisplay && _prepareDisplay is not null)
   try { display = _prepareDisplay(proposal); }
   catch (Exception error) { ReportPresentationError("显示留存准备失败", error); }
  _ownCommit = true;
  try
  {
   _session.ValidateEdit(frozen); EnsureCurrent(true);
   var receipt = _session.CommitEdit(frozen);
   try { display?.Accept(receipt.Change?.ExecutionInvalidatedNodes.ToArray() ?? Array.Empty<ScopedNodeIdentity>()); }
   catch (Exception error) { ReportPresentationError("显示留存接纳失败", error); }
   // The accepted transaction is final even if a presentation observer fails.
   try { _accepted?.Invoke(receipt); } catch (Exception error) { ReportPresentationError("显示刷新失败", error); }
  }
  finally
  {
   _ownCommit = false;
   try { display?.Dispose(); }
   catch (Exception error) { ReportPresentationError("显示留存释放失败", error); }
  }
 }
 private void ReportPresentationError(string stage, Exception error)
 {
  LastPresentationError = new InvalidOperationException(stage, error);
  try { _reportPresentationError?.Invoke(stage, error); } catch { }
 }
 private async Task ActivateAsync(ImageInfo image, int index, long version)
 {
  EnsureCurrent();
  if (!CanActivate) throw new InvalidOperationException("图像源运行导航尚未接入或当前不可用。");
  if (Source.EditVersion != version || index < 0 || index >= Source.Count || !ReferenceEquals(Source.Images[index], image))
   throw new InvalidOperationException("图像选择已过期。");
  if (!_isLocal || !Source.CanSelectImage(index)) throw new InvalidOperationException("当前图像不属于可运行选择。");
  var activate = _activate ?? throw new InvalidOperationException("图像源运行导航尚未接入。");
  await activate(new(_session, _root, _node, Source, image, index, version));
 }
 /// <summary>Present an accepted runtime choice without editing the document's configured start.</summary>
 internal bool TryPresentNavigation(ImageSourceActivation selection)
 {
  _dispatcher.VerifyAccess();
  if (!Current() || !ReferenceEquals(selection.Session, _session) || !ReferenceEquals(selection.Workflow, _root) ||
      !ReferenceEquals(selection.Node, _node) || !ReferenceEquals(selection.Source, Source) ||
      Source.EditVersion != selection.ProjectionVersion || selection.Index < 0 || selection.Index >= Source.Count ||
      !ReferenceEquals(Source.Images[selection.Index], selection.Image)) return false;
  return Source.TryPresentRuntimeCursor(selection.ProjectionVersion, selection.Image.Id,
   selection.Image.SourceReference ?? selection.Image.FilePath, selection.Image.FilePath) && Current();
 }
 /// <summary>Project only a still-current receipt from the formal source cursor.</summary>
 internal bool TryPresentCompletedRead(ImageSourceHostInputFactory factory, ImageSourceReadReceipt receipt, long projectionVersion)
 {
  _dispatcher.VerifyAccess();
  ArgumentNullException.ThrowIfNull(factory); ArgumentNullException.ThrowIfNull(receipt);
  if (!Current() || !_isLocal || Source.EditVersion != projectionVersion) return false;
  var context = _session.CaptureHostInputContext(_root.Id, _node);
  if (context is null || !factory.IsReceiptCurrent(context, receipt)) return false;
  var entry = receipt.Read.Entry;
  if (entry.OriginalIndex < 0 || entry.OriginalIndex >= Source.Count) return false;
  var image = Source.Images[entry.OriginalIndex];
  if (!Source.TryPresentRuntimeCursor(projectionVersion, image.Id, entry.ResourceReference, entry.Address)) return false;
  return Current() && factory.IsReceiptCurrent(context, receipt);
 }
 private void OnDocumentChanged(object? sender, DocumentChangeSet change)
 {
  if (_disposed || _ownCommit || change.EditSessionId != _session.EditSessionId) return;
  if (!change.ChangedNodes.Any(item => ReferenceEquals(item.Node, _node) &&
       (item.Aspects & (NodeChangedAspect.Configuration | NodeChangedAspect.Identity)) != 0) &&
      !change.RemovedNodes.Any(item => ReferenceEquals(item.Node, _node))) return;
  if (_refreshQueued || _dispatcher.HasShutdownStarted) return;
  _refreshQueued = true;
  _dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() =>
  {
   _refreshQueued = false;
   if (!Current()) return;
   try
   {
    RefreshFromDocument();
   }
   catch (InvalidOperationException) { Source.IsReadOnly = true; }
  }));
 }
 public void Dispose()
 {
  _dispatcher.VerifyAccess(); if (_disposed) return;
  _disposed = true; _dialogOwner = null; _session.Changed -= OnDocumentChanged; Source.DetachFormalOwner();
  var errors = new List<Exception>();
  try { Strip.Dispose(); } catch (Exception error) { errors.Add(error); }
  try { Source.Dispose(); } catch (Exception error) { errors.Add(error); }
  if (errors.Count != 0) throw new AggregateException("图像源界面释放失败。", errors);
 }
}

