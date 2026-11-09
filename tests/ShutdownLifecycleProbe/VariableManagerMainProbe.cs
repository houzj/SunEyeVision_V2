using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.References.Variables;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Shell.Controls.Shell;

internal static class VariableManagerMainProbe
{
 internal static void RunWindow()
 {
  RootCollectionMainProbe.AdditionalScenario=Exercise;
  try{RootCollectionMainProbe.RunWindow();}
  finally{RootCollectionMainProbe.AdditionalScenario=null;}
 }
 private static IEnumerable<T> Visuals<T>(DependencyObject node)where T:DependencyObject
 {if(node is T match)yield return match;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Visuals<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
 private static void Check(bool test,string text){if(!test)throw new InvalidOperationException(text);}
 private static object? Call(object target,string name,params object?[] args)=>target.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(method=>method.Name==name&&method.GetParameters().Length==args.Length).Invoke(target,args);
 private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Render(FrameworkElement element,string path)
 {element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);Console.WriteLine("variable visual: "+path);}
 private static void Exercise(MainWindow main,DocumentSession session,string root)
 {
  var vm=(MainWindowViewModel)main.DataContext;Exception? failure=null;bool shown=false;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
  timer.Tick+=(_,_)=>
  {
   var window=System.Windows.Application.Current.Windows.OfType<GlobalVariableManagerDialog>().SingleOrDefault();
   if(window is null||!window.IsLoaded)return;timer.Stop();shown=true;
   try
   {
    Check(ReferenceEquals(window.Owner,main),"真实管理器没有主窗owner。");
    Check(window.Content is VariableManagerView,"真实主窗未装配公共原件管理视图。");
    var variables=(GlobalVariableManagerViewModel)window.DataContext;
    for(var i=0;i<14;i++)variables.AddVariableCommand.Execute(null);Pump();
    Check(variables.PageCount==12&&variables.CurrentPageNum==2&&variables.ItemsTotalCount==14,"真实分页没有接私有草稿。");
    variables.CurrentPageNum=1;Pump();
    var first=variables.CurrentVariables.First();var name=Visuals<TextBox>(window).First(box=>box.Name=="VariableNameEditor"&&ReferenceEquals(box.DataContext,first));name.Text="主窗保留输入";
    var boolean=variables.CurrentVariables[1];boolean.Type="Boolean";Call(variables,"SetDraftValue",boolean,false);Pump();
    var actualRoot=session.GetWorkflows().First();var actualBlob=session.GetNodes(actualRoot.Id).Single(node=>node.ToolType=="BlobAnalysis");var output=session.GetNodeResultPresentation(actualRoot.Id,actualBlob).Schema.Outputs.First(port=>port.Type.Shape==ValueShape.Scalar&&port.Type.Scalar==ScalarKind.Boolean);
    // Accept the variable declaration first, then stage a fresh pending name in the same registered manager.
    session.Editing.Apply();Pump();
    var actual=session.Document.GlobalVariables.Single(item=>item.Id==boolean.Id);
    var pending=variables.CurrentVariables.First(item=>item.Id==first.Id);name=Visuals<TextBox>(window).First(box=>box.Name=="VariableNameEditor"&&ReferenceEquals(box.DataContext,pending));name.Text="结果绑定同时保存文本";
    var request=new ProducerBinding{ReferenceExpression=ParameterSettingCanonical.Serialize(new OutputReference(actualBlob.Id,output.Id,output.Type,scopeId:actualRoot.Id,storage:output.Storage)),WorkflowName=actualRoot.Name,NodeName=actualBlob.DispName,ResultDisplayName=output.Id};
    Check((bool)Call(vm,"IsResultVariableCandidate",session,request,actual)!,"真实正式Boolean输出候选被错误拒绝。");
    var history=session.HistoryEntries.Count;var args=new object?[]{session,request,actual,null};
    Check((bool)Call(vm,"TryBindResultVariable",args)!,"真实结果绑定未接受："+args[3]);Pump();
    Check(session.HistoryEntries.Count==history+1,"结果绑定与待提交文本没有同一次历史。");
    Check(session.Document.GlobalVariables.Single(item=>item.Id==first.Id).Name=="结果绑定同时保存文本","结果绑定绕过管理器待提交文本。");
    Check(actual.ProducerBinding?.ReferenceExpression==request.ReferenceExpression,"结果producer没有写实际变量。");
    var sourceRow=variables.CurrentVariables.Single(item=>item.Id==actual.Id);
    Check(Visuals<TextBlock>(window).Any(text=>text.Name=="InputSourceText"&&ReferenceEquals(text.DataContext,sourceRow)&&text.Text==sourceRow.InputSourceDisplayText),"输入来源没有显示正式producer路径。");
    Check(variables.IsValid&&!variables.HasPendingChanges,"结果回执没有重基当前变量窗。");
    Check(!variables.ImportCommand.CanExecute(null)&&!variables.ExportCommand.CanExecute(null),"缺基础文件入口被错误开放。");
    Render(window,Path.Combine(root,"variables.png"));
    Console.WriteLine("variables main: actual command/view/paging/producer+raw text single receipt/fixed theme");
   }
   catch(Exception error){failure=error;}
   finally{window.Close();}
  };
  timer.Start();vm.ShowGlobalVariableManagerCommand.Execute(null);timer.Stop();
  if(failure is not null)throw new Exception("真实主窗变量生产链失败",failure);
  Check(shown,"真实主窗命令未打开变量窗。");Check(session.Editing.RegisteredScopeCount==0,"关闭变量窗留下编辑参与者。");
  var savedTab=vm.WorkflowTabViewModel.SelectedTab!;var sourceRoot=session.GetWorkflows().First();var sourceNode=session.GetNodes(sourceRoot.Id).Single(node=>node.ToolType=="BlobAnalysis");
  vm.SetSelectedTab(vm.WorkflowTabViewModel.Tabs.Single(tab=>tab.Id==sourceRoot.Id));vm.SelectedNode=sourceNode;
  var run=(Task)Call(vm,"ExecuteRunWorkflow")!;var deadline=DateTime.UtcNow.AddSeconds(20);while(!run.IsCompleted){if(DateTime.UtcNow>deadline)throw new TimeoutException("变量来源实际主窗运行未结束。");Pump();Thread.Sleep(1);}run.GetAwaiter().GetResult();Pump();
  var booleanVariable=session.Document.GlobalVariables.Single(variable=>variable.Type=="Boolean");
  var booleanPort=session.GetNodeResultPresentation(sourceRoot.Id,sourceNode).Schema.Outputs.First(port=>port.Type.Shape==ValueShape.Scalar&&port.Type.Scalar==ScalarKind.Boolean);
  using(var reader=session.AcquirePublishedResult(sourceRoot.Id,sourceNode.Id))
  {Check(reader?.Snapshot.IsSuccess==true,"变量来源真实HALCON运行失败。");var expected=reader!.Values!.Read(booleanPort.Id).GetScalar<bool>();Check(booleanVariable.CaptureValue().Value is bool actual&&actual==expected,"结果producer没有发布实际Boolean值。");}
  FlatResultRow FindRow()=>vm.FlatResultRows.Single(row=>row.IsBindable&&row.BindingInfo?.Reference.PortId==booleanPort.Id&&row.BindingInfo.Reference.NodeId==sourceNode.Id);
  var results=(ImageResultControl)main.FindName("ImageResultHost");Call(results,"EditResultVariable",FindRow(),true);Pump();Check(booleanVariable.ProducerBinding is null,"实际结果区解除没有提交。");
  Exception? selectionFailure=null;bool selected=false;var selector=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
  selector.Tick+=(_,_)=>
  {
   var dialog=System.Windows.Application.Current.Windows.OfType<VariableSelectionDialog>().SingleOrDefault();if(dialog is null||!dialog.IsLoaded)return;selector.Stop();
   try{Check(dialog.BindItems.Count==1,"正式结果资格没有过滤不匹配字符串变量。");var item=dialog.BindItems.Single();Check(item.Reference.VariableId==booleanVariable.Id,"选择器返回的实际变量身份不正确。");((ListView)dialog.FindName("globalVarList")).SelectedItem=item;selected=true;Call(dialog,"ConfirmSelection");}
   catch(Exception error){selectionFailure=error;dialog.Close();}
  };
  selector.Start();Call(results,"EditResultVariable",FindRow(),false);selector.Stop();if(selectionFailure is not null)throw selectionFailure;Check(selected&&booleanVariable.ProducerBinding is not null,"真实结果区与公共选择器没有提交绑定。");
  vm.SetSelectedTab(savedTab);Pump();Console.WriteLine("variables main: actual HALCON producer value, result control unbind/select/filter/rebind");
  Console.WriteLine("variables main: closed editor and child subscriptions; root probe continues save/reopen/real run/shutdown");
 }
}
