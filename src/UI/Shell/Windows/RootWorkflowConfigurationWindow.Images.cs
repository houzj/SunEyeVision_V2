using SunEyeVision.Plugin.Runtime;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;
public partial class RootWorkflowConfigurationWindow
{
 private sealed record ImagePortChoice(string NodeId,Port Port,string Label);
 private ResultImagePane? _imagePane;
 private IResultPublication? _imagePublication;
 private DeclaredOutputSnapshot? _imageSnapshot;
 private WorkflowNodeBase? _imageNode;
 private Port? _imagePort;
 private long _imageEpoch;
 private bool _updatingPorts,_refreshingImage,_refreshImageAgain,_closedImages;
 internal ResultImagePane? ImagePreviewPane=>_imagePane;
 private void ImagePortChanged(object sender,SelectionChangedEventArgs args)
 {if(IsLoaded&&!_updatingPorts)RefreshResultView(RefreshImagePreview);}
 private void RefreshImagePreview()
 {
  if(_closedImages)return;
  if(_refreshingImage){_refreshImageAgain=true;return;}
  _refreshImageAgain=false;
  _refreshingImage=true;
  try
  {
   var node=ImageNodes.SelectedItem as WorkflowNodeBase;
   var result=node is null?null:_readResult(node.Id);
   var snapshot=result;
   bool Proven()=>!_closedImages&&_context.IsValid&&node is not null&&result is not null&&snapshot is not null
    &&ReferenceEquals(ImageNodes.SelectedItem,node)
    &&_context.Root.Nodes.Count(n=>n.Id==node.Id)==1&&_context.Root.Nodes.Any(n=>ReferenceEquals(n,node))
    &&ReferenceEquals(_readResult(node.Id),result)&&ReferenceEquals(result,snapshot)
    &&result.ExecutionIdentity is {} identity&&ReferenceEquals(identity.Owner,_context.ExecutionOwner)&&identity.IsCurrent
    &&identity.ToInvocationIdentity(node.Id)==snapshot.Identity&&snapshot.Identity.DocumentSessionId==_context.ExecutionOwner.Id
    &&snapshot.Identity.NodeId==node.Id&&snapshot.SourceWorkflowId==_context.Root.Id&&snapshot.IsComplete;
   var ports=Proven()?snapshot!.Schema.Outputs.Where(p=>p.Type==TypeRef.ResourceOf(ResourceKind.Image)).ToArray():Array.Empty<Port>();
   var prior=ImagePorts.SelectedItem as ImagePortChoice;
   var choices=ImagePorts.ItemsSource as ImagePortChoice[];
   if(choices is null||choices.Length!=ports.Length||choices.Where((choice,index)=>choice.NodeId!=node!.Id||choice.Port!=ports[index]).Any())
   {
    _updatingPorts=true;
    try
    {
     var next=ports.Select(port=>new ImagePortChoice(node!.Id,port,snapshot!.Fields.TryGetValue(port.Id,out var field)?field.DisplayName+" ("+port.Id+")":port.Id)).ToArray();
     ImagePorts.ItemsSource=next;
     ImagePorts.SelectedItem=prior is null?null:next.SingleOrDefault(item=>item.NodeId==prior.NodeId&&item.Port==prior.Port);
    }
    finally{_updatingPorts=false;}
   }
   var selected=ImagePorts.SelectedItem as ImagePortChoice;
   if(!Proven()||selected is null||selected.NodeId!=node!.Id||!ports.Contains(selected.Port))
   {ClearImagePreview();return;}
   if(ReferenceEquals(snapshot,_imageSnapshot)&&ReferenceEquals(node,_imageNode)&&selected.Port==_imagePort&&_imagePublication?.IsCurrent()==true)
   {_imagePane!.RefreshCurrentness();UpdateNoImage();return;}
   ClearImagePreview();
   var epoch=++_imageEpoch;var port=selected.Port;
   bool Current()=>epoch==_imageEpoch&&Proven()&&ImagePorts.SelectedItem is ImagePortChoice choice&&choice.NodeId==node.Id&&choice.Port==port;
   if(!Current())return;
   var publication=ToolResultViews.FromSnapshot(snapshot!,Current);
   if(!Current()){publication.Dispose();return;}
   _imagePublication=publication;_imageSnapshot=snapshot;_imageNode=node;_imagePort=port;
   _imagePane??=new ResultImagePane(Preview);
   if(!_imagePane.TryPresent(publication,new OutputReference(node.Id,port.Id,port.Type,scopeId:_context.Root.Id,storage:port.Storage),_context.Root.Id,Current,out var error))
   {ClearImagePreview();Error.Text="已发布图像不可显示："+error;}
   UpdateNoImage();
  }
  catch(Exception failure)
  {
   try{ClearImagePreview();}
   catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
   throw;
  }
  finally
  {
   _refreshingImage=false;
   if(_refreshImageAgain&&!_closedImages)Dispatcher.BeginInvoke(new Action(()=>{if(!_closedImages)RefreshResultView(RefreshImagePreview);}));
  }
 }
 private void UpdateNoImage()=>NoImage.Visibility=_imagePane?.CurrentFrame is null?Visibility.Visible:Visibility.Collapsed;
 private void ClearImagePreview()
 {
  ++_imageEpoch;var publication=_imagePublication;_imagePublication=null;_imageSnapshot=null;_imageNode=null;_imagePort=null;
  RunImageCleanup(new Action[]{()=>_imagePane?.Clear(),()=>publication?.Dispose(),()=>NoImage.Visibility=Visibility.Visible});
 }
 private void DisposeImagePreview()
 {
  _closedImages=true;++_imageEpoch;var pane=_imagePane;_imagePane=null;
  var publication=_imagePublication;_imagePublication=null;_imageSnapshot=null;_imageNode=null;_imagePort=null;
  RunImageCleanup(new Action[]{()=>pane?.Dispose(),()=>publication?.Dispose(),()=>ImagePorts.ItemsSource=null});
 }
 private static void RunImageCleanup(IEnumerable<Action> actions)
 {
  var errors=new List<Exception>();foreach(var action in actions)try{action();}catch(Exception error){errors.Add(error);}
  if(errors.Count>0)throw new AggregateException("根配置图像清理失败；全部租约均已尝试归还。",errors);
 }
}
