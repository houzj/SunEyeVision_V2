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
   var resolved = ImageSourceResolver.Resolve(session, root, selected);
   if (resolved.Kind != ImageSourceResolutionKind.Found || resolved.Source is not { } source)
   { ClearFormalImageSourceSelection(); return; }
   if (_activeImageSourceBinding?.Owns(session, root, source) == true)
   { _activeImageSourceBinding.RefreshFromDocument(); return; }
   ClearFormalImageSourceSelection();
   bool Current() => ReferenceEquals(DocumentManager.CurrentSession, session) &&
    ReferenceEquals(WorkflowTabViewModel.SelectedTab, tab) && ReferenceEquals(SelectedNode, selected);
   SetActiveImageSourceBinding(new ImageSourceProjectionBinding(session, root, source, Current,
    () => CanEditRootWorkspace && !IsRunning, () => ImageSourceCanNavigate?.Invoke() == true,
    ImageSourceEditAccepted, ImageSourceActivate,
    prepareDisplay: ImageSourcePrepareDisplay, reportPresentationError: ImageSourcePresentationError,
    // 只有选中的就是图像源模块本身时才提供增删图入口；选中下游模块只是呈现它引用的上游图像源。
    providesImageImport: ReferenceEquals(source, selected)));
  }
  catch (Exception error)
  {
   ClearFormalImageSourceSelection();
   StatusText = "图像源不可用：" + error.Message;
  }
 }

 internal void ClearFormalImageSourceSelection()
 {
  var old = _activeImageSourceBinding;
  if (old is null) return;
  try { old.Dispose(); }
  catch (Exception error) { StatusText = "图像源释放失败：" + error.Message; }
  SetActiveImageSourceBinding(null);
 }

 /// <summary>会话与"是否显示图像源条"的唯一写入口，避免可见性开关再次与数据链脱节。</summary>
 private void SetActiveImageSourceBinding(ImageSourceProjectionBinding? binding)
 {
  _activeImageSourceBinding = binding;
  _imageResult.HasActiveImageSource = binding is not null;
  OnPropertyChanged(nameof(ActiveImageSourceBinding));
 }
}
