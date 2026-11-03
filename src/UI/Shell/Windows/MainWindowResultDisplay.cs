using System;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Editing;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal sealed record ResultDisplayState(ResultDisplayDescriptor Descriptor,ResultDisplayAppearance Appearance,long Revision,Func<bool> IsCurrent,ResultGeometrySnapshot? Geometry,Func<ResultDisplayTextLease> BorrowText);
 private sealed class DisplayPreview : IDisposable
 {
  internal required MainWindowViewModel Main;
  internal required DocumentSession Session;
  internal required NodeDefinition Node;
  internal required ProductionEditorContext Context;
  internal required RuntimeNodeConfigurationControl Control;
  private bool _disposed;
  internal bool Current=>!_disposed&&ReferenceEquals(Main._displayPreview,this)&&ReferenceEquals(DocumentManager.CurrentSession,Session)
   &&ReferenceEquals(Main.SelectedNode,Node)&&Context.IsValid&&Control.IsLoaded&&Control.IsVisible
   &&!Control.HasPendingEdits&&Control.TryGetResultDisplayBinding(out var node,out _)&&ReferenceEquals(node,Node);
  internal void Edited()=>Main.NotifyResultDisplayChanged();
  internal void Bound(NodeDefinition node,long binding)=>Edited();
  internal void Loaded(object sender,System.Windows.RoutedEventArgs args)=>Edited();
  internal void Visibility(object sender,System.Windows.DependencyPropertyChangedEventArgs args)=>Edited();
  public void Dispose()
  {
   if(_disposed)return;_disposed=true;
   Control.ResultDisplayEdited-=Edited;Control.ResultDisplayBound-=Bound;Control.Loaded-=Loaded;Control.IsVisibleChanged-=Visibility;
   if(ReferenceEquals(Main._displayPreview,this)){Main._displayPreview=null;Main.NotifyResultDisplayChanged();}
  }
 }
 private DisplayPreview? _displayPreview;
 private long _resultDisplayRevision;
 internal event Action? ResultDisplayChanged;
 internal void NotifyResultDisplayChanged()
 {
  ++_resultDisplayRevision;
  foreach(Action callback in ResultDisplayChanged?.GetInvocationList()??Array.Empty<Delegate>())
   try{callback();}catch(Exception error){AddLog("结果显示刷新失败："+error.Message);}
 }
 private IDisposable InstallResultDisplayPreview(DocumentSession document,NodeDefinition node,ProductionEditorContext context,RuntimeNodeConfigurationControl control)
 {
  var previous=_displayPreview;var next=new DisplayPreview{Main=this,Session=document,Node=node,Context=context,Control=control};
  _displayPreview=next;previous?.Dispose();
  control.ResultDisplayEdited+=next.Edited;control.ResultDisplayBound+=next.Bound;control.Loaded+=next.Loaded;control.IsVisibleChanged+=next.Visibility;
  NotifyResultDisplayChanged();return next;
 }
 internal ResultDisplayState? CaptureCurrentResultDisplay(IResultPublication expectedPublication)
 {
  var session=DocumentManager.CurrentSession;var node=SelectedNode;var workflowId=WorkflowTabViewModel.SelectedTab?.CurrentWorkflowId;
  var snapshot=_resultPublicationSnapshot;var revision=_resultDisplayRevision;
  if(session is null||node is null||workflowId is null||snapshot is null||!OwnsSelectedDocumentNode(node)
   ||snapshot.Identity.DocumentSessionId!=session.ResultOwnerId||snapshot.Identity.NodeId!=node.Id||snapshot.SourceWorkflowId!=workflowId
   ||!ReferenceEquals(_resultPublication,expectedPublication)||snapshot.Identity!=expectedPublication.Stamp.Invocation)return null;
  var preview=_displayPreview;
  var draft=preview?.Current==true?preview.Context.PreparedDraft:null;
  var configuration=draft?.Configuration??node.Configuration;
  var presentation=session.GetNodeResultPresentation(workflowId,node,draft);
  if(presentation.Display is not {} descriptor||presentation.Appearance is not {} appearance)return null;
  try{descriptor.Validate(snapshot.Schema);}catch(ArgumentException){return null;}
  bool Current()
  {
   if(revision!=_resultDisplayRevision||!ReferenceEquals(DocumentManager.CurrentSession,session)||!ReferenceEquals(SelectedNode,node)
    ||!ReferenceEquals(_resultPublication,expectedPublication)||!expectedPublication.IsCurrent()||!OwnsSelectedDocumentNode(node))return false;
   if(draft is not null&&(!ReferenceEquals(_displayPreview,preview)||preview?.Current!=true||!ReferenceEquals(preview.Context.PreparedDraft,draft)
    ||!ReferenceEquals(draft.Configuration,configuration)))return false;
   if(draft is null&&!ReferenceEquals(node.Configuration,configuration))return false;
   using var reader=session.AcquirePublishedResult(workflowId,node.Id);
   return reader is not null&&ReferenceEquals(reader.Snapshot,snapshot);
  }
  if(!Current())return null;
  return new(descriptor,appearance,revision,Current,(snapshot.Output as IToolResultGeometryOutput)?.ResultGeometry,()=>
  {
   if(!Current())throw new InvalidOperationException("文字显示来源已失效。");
   var lease=new ResultDisplayTextLease(snapshot,appearance.Text);
   if(Current())return lease;lease.Dispose();throw new InvalidOperationException("文字显示来源已变化。");
  });
 }
}
