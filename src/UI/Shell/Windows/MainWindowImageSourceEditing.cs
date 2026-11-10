using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Contracts.Vision;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
 private ImageSourceProjectionBinding? _activeImageSourceBinding;
 internal ImageSourceProjectionBinding? ActiveImageSourceBinding => _activeImageSourceBinding;
 // 投影按源节点常驻缓存：VM 原件的 LocalImageListView 随模块视图持续存在，切换选择不销毁重建，切回零重载。
 private readonly Dictionary<string, ImageSourceProjectionBinding> _imageSourceBindings = new(StringComparer.Ordinal);
 internal Action<DocumentEditReceipt>? ImageSourceEditAccepted { get; set; }
 internal Func<ImageSourceActivation, Task>? ImageSourceActivate { get; set; }
 internal Func<bool>? ImageSourceCanNavigate { get; set; }
 internal Func<ImageSourceEditProposal, SourceDisplayPreservationScope?>? ImageSourcePrepareDisplay { get; set; }
 internal Action<string, Exception>? ImageSourcePresentationError { get; set; }

 /// <summary>Replace the source projection only after resolving actual session, flow and node identities.</summary>
 internal void RefreshFormalImageSourceSelection()
 {
  var session = DocumentManager.CurrentSession;
  var tab = WorkflowTabViewModel.SelectedTab;
  var selected = SelectedNode;
  if (session is null || tab is null || tab.IsInSubroutine || selected is null)
  { ClearFormalImageSourceSelection(); return; }
  try
  {
   var root = session.GetWorkflows().SingleOrDefault(flow => !flow.IsSubroutine && flow.Id == tab.RootWorkflowId)
    ?? throw new InvalidOperationException("当前根流程已退出方案。");
   // 用当前实例解析：SelectedNode 可能是提交前的旧实例，而解析器按引用校验目标的归属。
   selected = FindCurrentNode(selected.Id) ?? selected;
   SweepRemovedImageSourceBindings(session, root.Id);
   var resolved = ImageSourceResolver.Resolve(session, root, selected);
   if (resolved.Kind != ImageSourceResolutionKind.Found || resolved.Source is not { } source)
   {
    // 确实没有上游图像源（None）或本来就没有绑定：按 VM 语义隐藏；
    // 解析异常（Ambiguous/InvalidSelection）保留控件并给出原因，等下次刷新恢复。
    if (resolved.Kind == ImageSourceResolutionKind.None || _activeImageSourceBinding is null)
     ClearFormalImageSourceSelection();
    else
     KeepImageSourceVisibleWithReason("当前节点暂时未解析到图像源（" + resolved.Kind + "），稍后自动恢复。");
    return;
   }
   // 导入入口随选中对象变化：只有选中的就是图像源模块本身时才提供增删图入口（缓存复用下必须动态更新）。
   var providesImport = string.Equals(source.Id, selected.Id, StringComparison.Ordinal);
   if (_imageSourceBindings.TryGetValue(source.Id, out var cached))
   {
    // 缓存命中：复用常驻投影，零重建（缩略图保持已加载，切回瞬间原样显示）。
    SetActiveImageSourceBinding(cached);
    cached.Strip.SetProvidesImageImport(providesImport);
    cached.RefreshFromDocument();
    return;
   }
   // 当前性=会话有效且本投影是活动投影：非活动缓存投影必须自称不可用（只读、不可导航）。
   ImageSourceProjectionBinding? candidate=null;
   bool Current() => ReferenceEquals(DocumentManager.CurrentSession, session) &&
    ReferenceEquals(WorkflowTabViewModel.SelectedTab, tab) && ReferenceEquals(_activeImageSourceBinding, candidate);
   var next = new ImageSourceProjectionBinding(session, root, source, Current,
    () => CanEditRootWorkspace && !IsRunning, () => ImageSourceCanNavigate?.Invoke() == true,
    ImageSourceEditAccepted, ImageSourceActivate,
    prepareDisplay: ImageSourcePrepareDisplay, reportPresentationError: ImageSourcePresentationError,
    providesImageImport: providesImport);
   candidate=next;
   _imageSourceBindings[source.Id] = next;
   SetActiveImageSourceBinding(next);
   // 一次性把这条链的关键状态打进日志：数量为 0 时能直接区分"策略里没图"与"按钮被禁用"。
   AddLog($"[图像源] 绑定 node={source.Id} 选中={selected.Id} 图片数={next.Source.Count} 可编辑={!next.Source.IsReadOnly} 可导入={providesImport}");
  }
  catch (Exception error)
  {
   // 刷新失败时同样保留控件（VM 的控件不会因数据源异常被移除），只说明原因。
   if (_activeImageSourceBinding is not null)
    KeepImageSourceVisibleWithReason("图像源刷新失败：" + error.Message);
   else
   {
    ClearFormalImageSourceSelection();
    StatusText = "图像源不可用：" + error.Message;
   }
   AddLog("[图像源] 选择刷新失败：" + error.Message);
  }
 }

 /// <summary>VM 语义：图像源控件随参数上下文存在，数据暂不可用时保留控件并给出原因，而不是整条移除。</summary>
 private void KeepImageSourceVisibleWithReason(string reason)
 {
  // 投影按源节点常驻缓存：绑定留在缓存中随后复用，这里不再销毁（销毁只发生在节点删除与会话失效）。
  _imageResult.HasActiveImageSource = true;
  StatusText = reason;
  AddLog("[图像源] " + reason);
 }

 internal void ClearFormalImageSourceSelection()
 {
  // 这一条是"确实没有图像源上下文"（未选节点、切到无图像源的节点）的唯一隐藏入口。
  // 投影按源节点常驻缓存：隐藏只清活动指针，缓存绑定保留，切回时零重建复用。
  SetActiveImageSourceBinding(null);
 }

 /// <summary>源节点已删除的缓存投影逐出，避免随删除累积。</summary>
 private void SweepRemovedImageSourceBindings(DocumentSession session, string rootId)
 {
  foreach (var stale in _imageSourceBindings.Values
   .Where(binding => !session.GetNodes(rootId).Any(node => string.Equals(node.Id, binding.Source.NodeId, StringComparison.Ordinal)))
   .ToArray())
   EvictImageSourceBinding(stale);
 }

 private void EvictImageSourceBinding(ImageSourceProjectionBinding binding)
 {
  _imageSourceBindings.Remove(binding.Source.NodeId);
  if (ReferenceEquals(_activeImageSourceBinding, binding)) SetActiveImageSourceBinding(null);
  try { binding.Dispose(); }
  catch (Exception error) { StatusText = "图像源释放失败：" + error.Message; }
  AddLog($"[图像源] 逐出缓存投影 node={binding.Source.NodeId}");
 }

 /// <summary>会话失效（切换/关闭方案、宿主释放）时逐出全部常驻投影。</summary>
 internal void ReleaseImageSourceBindings()
 {
  foreach (var binding in _imageSourceBindings.Values.ToArray())
  {
   try { binding.Dispose(); }
   catch (Exception error) { StatusText = "图像源释放失败：" + error.Message; }
  }
  _imageSourceBindings.Clear();
  SetActiveImageSourceBinding(null);
 }

 /// <summary>会话与"是否显示图像源条"的唯一写入口，避免可见性开关再次与数据链脱节。</summary>
 private void SetActiveImageSourceBinding(ImageSourceProjectionBinding? binding)
 {
  var previous=_activeImageSourceBinding;
  _activeImageSourceBinding = binding;
  _imageResult.HasActiveImageSource = binding is not null;
  OnPropertyChanged(nameof(ActiveImageSourceBinding));
  // 活动切换即当前性变化：两侧投影的可用性（只读/可导航）必须按新活动状态重算。
  if(!ReferenceEquals(previous,binding))
  {
   previous?.RefreshAvailability();
   binding?.RefreshAvailability();
  }
 }
}
