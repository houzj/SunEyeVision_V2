using SunEyeVision.Plugin.SDK.Execution.Parameters;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Services.Editing;
internal static class RootWorkflowReferencePicker
{
 internal static ReferenceSetting? Pick(Window owner,RootWorkflowConfigurationContext context,RootParameterProxy proxy)
 {
  var query=context.Query;var candidates=new List<DeclaredReferenceCandidate>();var target=proxy.NodeId;
  var names=query.GetAvailableDataSources(target,null,true).Where(s=>!s.SourceNodeId.StartsWith("__",StringComparison.Ordinal)).GroupBy(s=>s.SourceNodeId).ToDictionary(g=>g.Key,g=>g.First().SourceNodeName);
  foreach(var id in query.BindingCandidateNodeIds?.Invoke(target)??names.Keys.ToArray())
   foreach(var output in query.FormalOutputContractReader?.Invoke(id)??Array.Empty<OutputVariable>())
   {
    if(output.Kind!=OutputVariableKind.Data)continue;Port port;try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
    var label=(names.GetValueOrDefault(id)??id)+" · "+(output.DisplayName??output.Name);
    candidates.Add(new(label,id,query.WorkflowId,port,Array.Empty<string>()));Members(port.Type,Array.Empty<string>(),label,id,port);
   }
  void Members(TypeRef type,IReadOnlyList<string> path,string label,string nodeId,Port root)
  {
   while(type.Shape==ValueShape.Sequence)type=type.Element!;if(type.Shape!=ValueShape.Record)return;
   foreach(var field in type.Fields){var next=path.Append(field.Id).ToArray();var caption=label+" / "+field.Id;candidates.Add(new(caption,nodeId,query.WorkflowId,root,next));Members(field.Type,next,caption,nodeId,root);}
  }
  var picker=new ReferenceSelectionControl(candidates,reference=>context.ValidateReference(proxy,reference));
  var panel=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(12)};
  DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(picker);
  var window=new ThemeDialogWindow{Title="选择订阅来源",Width=650,Height=520,Content=panel,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
  ReferenceSetting? selection=null;var accept=new Button{Content="确定",IsDefault=true,MinWidth=80,Margin=new Thickness(4)};var cancel=new Button{Content="取消",IsCancel=true,MinWidth=80,Margin=new Thickness(4)};
  accept.Click+=(_,_)=>{if(picker.TryGetReference(out var reference)){selection=new(reference!);window.DialogResult=true;}};buttons.Children.Add(accept);buttons.Children.Add(cancel);window.ShowDialog();return selection;
 }
}
