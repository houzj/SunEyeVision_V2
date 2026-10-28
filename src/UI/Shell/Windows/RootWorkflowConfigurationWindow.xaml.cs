using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shared;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class RootWorkflowConfigurationWindow:ThemeDialogWindow
{
 private readonly RootWorkflowConfigurationContext _context;
 private readonly Func<Task> _run;private readonly Action _repeat,_stop;private readonly Func<bool> _running;
 private readonly Func<string,ToolResults?> _readResult;
 private readonly List<DeclaredFieldEditor> _fields=new();
 private readonly DispatcherTimer _refresh=new(){Interval=TimeSpan.FromMilliseconds(160)};
 private bool _rebuilding;
 internal RootWorkflowConfigurationContext Context=>_context;
 internal RootWorkflowConfigurationWindow(RootWorkflowConfigurationContext context,Func<Task> run,Action repeat,Action stop,Func<bool> running,Func<string,ToolResults?> readResult)
 {
  _context=context;_run=run;_repeat=repeat;_stop=stop;_running=running;_readResult=readResult;
  InitializeComponent();Resources.MergedDictionaries.Add(EditorResources.Create());Heading.Text=Title=context.Root.Name+" · 流程配置";
  _context.PrepareView=()=>_fields.FirstOrDefault(f=>f.HasInvalidText)?.Error;
  _context.HasPendingView=()=>_fields.Any(f=>f.HasInvalidText);_context.Changed+=Rebuild;
  _refresh.Tick+=RefreshTick;Loaded+=(_,_)=>{Rebuild();ImageNodes.ItemsSource=_context.Root.Nodes;ImageNodes.SelectedIndex=_context.Root.Nodes.Count>0?0:-1;RefreshPublishedResults();_refresh.Start();};
  Closed+=(_,_)=>RunImageCleanup(new Action[]{()=>{_refresh.Stop();_refresh.Tick-=RefreshTick;},()=>{_context.Changed-=Rebuild;_context.PrepareView=null;_context.HasPendingView=null;},DisposeImagePreview,_context.Dispose,()=>Results.ItemsSource=null,()=>ImageNodes.ItemsSource=null,()=>{foreach(var field in _fields)field.Dispose();_fields.Clear();Parameters.Children.Clear();}});
 }
 private void RefreshTick(object? sender,EventArgs e){if(!_context.IsValid){Close();return;}try{RefreshState();RefreshPublishedResults();}catch(Exception error){Error.Text="已发布结果暂不可读取："+error.Message;ClearImagePreview();}}
 private void RefreshState()
 {
  bool running=_running();bool editable=_context.CanEdit;ConfigurationTabs.IsEnabled=editable;ApplyButton.IsEnabled=AcceptButton.IsEnabled=editable;
  RunOnce.IsEnabled=Repeat.IsEnabled=editable&&_context.Root.Nodes.Count>0;Stop.IsEnabled=running;
  UndoButton.IsEnabled=!running&&!_context.HasPendingChanges&&_context.CanUndo;RedoButton.IsEnabled=!running&&!_context.HasPendingChanges&&_context.CanRedo;
 }
 private void Rebuild()
 {
  if(_rebuilding||!IsInitialized)return;_rebuilding=true;
  try
  {
   foreach(var old in _fields)old.Dispose();Parameters.Children.Clear();_fields.Clear();
   foreach(var proxy in _context.Configuration.RunParameters)
   {
    var row=new DockPanel{Margin=new Thickness(0,3,0,3)};var delete=new Button{Content="×",Width=28,ToolTip="移除该运行参数代理"};DockPanel.SetDock(delete,Dock.Right);delete.Click+=(_,_)=>Safe(()=>_context.Remove(proxy));row.Children.Add(delete);
    try
    {
     var candidate=_context.Resolve(proxy);
     if(candidate.Resource is not null)
     {
      row.Children.Add(ResourceReferenceRow(proxy,candidate));Parameters.Children.Add(row);continue;
     }
     var field=candidate.Field!;
     var editor=new DeclaredFieldEditor(field.Port,_context.Resolve(proxy).Node.DispName+"."+proxy.DisplayName,_context.Read(proxy),setting=>_context.Write(proxy,setting),
      RootWorkflowReferencePicker.Options(_context,proxy),new ConstantSetting(field.Read()),reference=>FormatReference(reference),field.Presentation.ChoiceLabels,field.Presentation);
     editor.Changed+=RefreshState;_fields.Add(editor);row.Children.Add(editor);
    }
    catch(Exception error){row.Children.Add(new TextBlock{Text=proxy.DisplayName+" · "+error.Message,Foreground=System.Windows.Media.Brushes.OrangeRed,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center});}
    Parameters.Children.Add(row);
   }
   if(_context.Configuration.RunParameters.Length==0)Parameters.Children.Add(new TextBlock{Text="点击“编辑”选择已声明的运行参数。",Margin=new Thickness(5,10,5,0),TextWrapping=TextWrapping.Wrap});
   RefreshState();
  }
  finally{_rebuilding=false;}
 }
 private FrameworkElement ResourceReferenceRow(RootParameterProxy proxy,RootParameterCandidate candidate)
 {
  var editor=new DeclaredFieldEditor(candidate.Resource!.Port,candidate.Node.DispName+"."+proxy.DisplayName,
   _context.Read(proxy),setting=>_context.Write(proxy,setting),RootWorkflowReferencePicker.Options(_context,proxy),
   constantBackup:(candidate.Resource.Runtime&&candidate.Resource.IsSettings(candidate.Node)&&(candidate.Resource.Port.EditModes&FieldEditModes.Constant)!=0?new ConstantSetting(candidate.Resource.Literal(candidate.Node)):null),
   formatReference:FormatReference,choiceLabels:candidate.Field!.Presentation.ChoiceLabels,presentation:candidate.Field.Presentation,allowUnboundReference:true);
  editor.Changed+=RefreshState;_fields.Add(editor);return editor;
 }
 private string FormatReference(OutputReference reference)
 {
  var node=_context.Root.Nodes.FirstOrDefault(n=>n.Id==reference.NodeId);var output=_context.Query.FormalOutputContractReader?.Invoke(reference.NodeId)?.FirstOrDefault(o=>BindingClrAdapter.OutputPortId(o)==reference.PortId);
  return (node?.DispName??reference.NodeId)+" · "+(output?.DisplayName??output?.Name??reference.PortId)+(reference.MemberFieldPath.Count==0?"":" / "+string.Join(" / ",reference.MemberFieldPath));
 }
 private void EditParametersClick(object sender,RoutedEventArgs e)=>Safe(()=>
 {
  if(_fields.Any(f=>f.HasInvalidText))throw new InvalidOperationException("请先修正当前参数输入。");
  var list=new ListBox{Margin=new Thickness(12),HorizontalContentAlignment=HorizontalAlignment.Stretch};
  foreach(var candidate in _context.Candidates())list.Items.Add(new ListBoxItem{Content=candidate.Label,Tag=candidate,IsEnabled=candidate.Unavailable==null&&!_context.Configuration.RunParameters.Any(p=>p.NodeId==candidate.Node.Id&&p.FieldId==candidate.FieldId),ToolTip=candidate.Unavailable});
  var panel=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(12)};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);panel.Children.Add(list);
  var picker=new ThemeDialogWindow{Title="编辑运行参数",Owner=this,Width=600,Height=420,Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
  var add=new Button{Content="添加",Width=80,IsDefault=true};var cancel=new Button{Content="取消",Width=80,IsCancel=true,Margin=new Thickness(8,0,0,0)};
  add.Click+=(_,_)=>{if(list.SelectedItem is ListBoxItem{Tag:RootParameterCandidate candidate,IsEnabled:true}){_context.Add(candidate);picker.DialogResult=true;}};buttons.Children.Add(add);buttons.Children.Add(cancel);picker.ShowDialog();
 });
 private bool ApplyDraft(){if(!_context.TryApply(out var error)){Error.Text=error;return false;}Error.Text="";RefreshPublishedResults();return true;}
 private void ApplyClick(object sender,RoutedEventArgs e)=>ApplyDraft();
 private void AcceptClick(object sender,RoutedEventArgs e){if(ApplyDraft())Close();}
 private void CancelClick(object sender,RoutedEventArgs e)=>Close();
 private async void RunClick(object sender,RoutedEventArgs e){if(!ApplyDraft())return;try{RunOnce.IsEnabled=Repeat.IsEnabled=false;await _run();}catch(Exception error){Error.Text=error.Message;}finally{if(IsLoaded){RefreshState();RefreshPublishedResults();}}}
 private void RepeatClick(object sender,RoutedEventArgs e){if(ApplyDraft())Safe(_repeat);RefreshState();}
 private void StopClick(object sender,RoutedEventArgs e)=>Safe(_stop);
 private void UndoClick(object sender,RoutedEventArgs e)=>Safe(_context.Undo);
 private void RedoClick(object sender,RoutedEventArgs e)=>Safe(_context.Redo);
 private void DragTitle(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left)DragMove();}
 private void Safe(Action action){try{action();Error.Text="";}catch(Exception error){Error.Text=error.Message;}RefreshState();}
 private void ImageNodeChanged(object sender,SelectionChangedEventArgs e){if(IsLoaded)RefreshPublishedResults();}
 internal void RefreshPublishedResults()
 {
  var rows=new List<ResultRow>();
  foreach(var node in _context.Root.Nodes)
  {
   var result=_readResult(node.Id);if(result==null)continue;
   foreach(var output in _context.Query.FormalOutputContractReader?.Invoke(node.Id)??Array.Empty<OutputVariable>())
   {
    if(output.Kind!=OutputVariableKind.Data)continue;Port port;try{port=BindingClrAdapter.DescribeOutput(output);}catch(ArgumentException){continue;}
    if(port.Type.Shape!=ValueShape.Scalar)continue;
    object? value;if(!result.TryGetDeclaredOutputValue(port.Id,out value))value=result.GetOutputValue<object>(output.Name);
    var text=value switch{null=>"",string str=>str.Length>1024?str[..1024]+"…":str,bool or byte or short or int or long or float or double or decimal=>Convert.ToString(value,CultureInfo.InvariantCulture)??"",_=>"非标量运行值"};
    rows.Add(new(node.DispName+" · "+(output.DisplayName??output.Name),text));
   }
  }
  Results.ItemsSource=rows;RefreshImagePreview();
 }

 private sealed record ResultRow(string Name,string Value);
}
