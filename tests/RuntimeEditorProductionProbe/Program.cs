using System.IO;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.UI.Services.Editing;
internal static class Program
{
 [STAThread] private static int Main(string[] args)
 {
  try
  {
   var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(SunEyeVision.Plugin.Wpf.EditorResources.Create());
   var entries=new Dictionary<(string,string),Type>
   {
    [("ImageLoad","Runtime")]=typeof(SunEyeVision.Tool.ImageSource.Runtime.ImageSourceTool),
    [("ImageLoad","Editor")]=typeof(SunEyeVision.Tool.ImageSource.Wpf.ImageSourceEditorFactory),
    [("BlobAnalysis","Runtime")]=typeof(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobTool),
    [("BlobAnalysis","Editor")]=typeof(SunEyeVision.Tool.BlobAnalysis.Wpf.BlobEditorFactory)
   };
   var catalog=System.Xml.Linq.XDocument.Load(Path.Combine(Path.GetFullPath(args[0]),"product/ToolComponents.props"));
   var components=catalog.Descendants("ProductToolComponent").Select(element=>
   {
    string Read(string name)=>(string?)element.Element(name)??"";
    var tool=Read("ToolId");var role=Enum.Parse<ToolComponentRole>(Read("Role"));var type=entries[(tool,Read("Role"))];
    if(type.FullName!=Read("EntryType"))throw new Exception("产品声明入口不一致。");
    return new ToolComponentRegistration(new ToolComponentMetadata(Read("ComponentId"),tool,role,FoundationContract.Major,FoundationContract.Minor,role==ToolComponentRole.Editor?Read("CompanionToolId"):null),type);
   }).ToArray();
   var directory=(RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{components})!;
   using var session=DocumentSession.CreateNew(directory,"正式参数页定位");var flow=session.GetWorkflows().Single();
   foreach(var toolId in new[]{"ImageLoad","BlobAnalysis"})
   {
    var graph=session.PrepareGraphEdit(flow.Id);var node=session.CreateNode(graph,toolId,new(100,100));session.CommitGraphEdit(graph,"添加定位节点");
    var control=new RuntimeNodeConfigurationControl();var window=new RuntimeToolEditorWindow(control){Title=toolId,ShowInTaskbar=false};
    using var context=new ProductionEditorContext(session,flow.Id,node,()=>true,control.BindDraft);
    ToolEditorCommit.AttachDraftContext(control,context);context.PrepareView=()=>control.TryCapture(out var error)?null:error;
    context.HasPendingViewEdits=()=>control.HasPendingEdits;context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
    var bindClock=System.Diagnostics.Stopwatch.StartNew();
    control.BindDraft(context.PreparedDraft);
    bindClock.Stop();
    Console.WriteLine($"[TIMING] {toolId} 参数页绑定返回 {bindClock.ElapsedMilliseconds}ms");
    DumpAssemblyStages(toolId, control, "绑定后");
    var showClock=System.Diagnostics.Stopwatch.StartNew();window.Show();Pump();showClock.Stop();
    Console.WriteLine($"[TIMING] {toolId} 窗口首次呈现与首个页面组装 {showClock.ElapsedMilliseconds}ms");
    DumpAssemblyStages(toolId, control, "首屏后");
    Pump(DispatcherPriority.SystemIdle);DumpAssemblyStages(toolId, control, "首屏空闲后");
    for(var sample=0;sample<3;sample++)
    {
     var resourceClock=System.Diagnostics.Stopwatch.StartNew();
     var parsed=SunEyeVision.Plugin.Wpf.EditorResources.Create();
     resourceClock.Stop();
     Console.WriteLine($"[TIMING] {toolId} 主题字典解析 第{sample+1}次 {resourceClock.ElapsedMilliseconds}ms 字典{parsed.MergedDictionaries.Count}合并");
    }
    var tabs=Descendants<System.Windows.Controls.TabControl>(control).FirstOrDefault();
    if(tabs is not null)
    {
     for(var index=0;index<tabs.Items.Count;index++)
     {
      var tabClock=System.Diagnostics.Stopwatch.StartNew();
      tabs.SelectedIndex=index;Pump();tabClock.Stop();
      Console.WriteLine($"[TIMING] {toolId} 切到第{index+1}页 {tabClock.ElapsedMilliseconds}ms");
      DumpAssemblyStages(toolId, control, $"第{index+1}页后");
      Pump(DispatcherPriority.SystemIdle);DumpAssemblyStages(toolId, control, $"第{index+1}页空闲后");
     }
     tabs.SelectedIndex=0;Pump();
     for(var index=0;index<tabs.Items.Count;index++)
     {
      var repeatClock=System.Diagnostics.Stopwatch.StartNew();
      tabs.SelectedIndex=index;Pump();repeatClock.Stop();
      Console.WriteLine($"[TIMING] {toolId} 重复切到第{index+1}页 {repeatClock.ElapsedMilliseconds}ms");
     }
     tabs.SelectedIndex=0;Pump();
    }
    if(control.Content is null||!control.IsLoaded)throw new Exception(toolId+"真实控件未加载");
    if(!context.TryValidateEditor(out var error))throw new Exception(toolId+"参数页校验失败："+error);
    if(toolId=="ImageLoad")
    {
     var settings=Descendants<SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView>(control).Single();
     var fields=(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView)settings.Content;
     var field=fields.Fields.Single(item=>item.FieldId=="sequence.initial");
     if(!settings.TrySetFields(new Dictionary<string,ParameterSetting>{{field.FieldId,new ConstantSetting(9L)}}))throw new Exception("实际SN字段未接受9");
     var text=field.InputElement as System.Windows.Controls.TextBox??throw new Exception("实际SN字段没有正式原文本控件");
     var body=control.Content;
     EventHandler<DocumentChangeSet>? delayed=null;
     delayed=(_,_)=>{session.Changed-=delayed;text.Text="未完成数值";};session.Changed+=delayed;
     if(!context.TryCommitEditor(out error))throw new Exception("回执期间输入提交失败："+error);
     if(!ReferenceEquals(body,control.Content)||text.Text!="未完成数值"||!control.HasPendingEdits)throw new Exception("提交回执丢失真实原文本或重建了视图");
     text.Text="12";
     if(!context.TryCommitEditor(out error)||node.Configuration.Fields.Get<long>(field.FieldId)!=12L)throw new Exception("重基后真实原文本不能继续提交："+error);
     Console.WriteLine("PASS 实际SN文本在Prepare→回执间未解析输入保留、来源子页原实例重基、继续提交12");
    }
    else if(!context.TryCommitEditor(out error))throw new Exception(toolId+"参数页提交失败："+error);
    context.Discard();control.Dispose();window.Close();Pump();
    Console.WriteLine("PASS "+toolId+"：实际Session→实际节点→正式参数控件→捕获/回执→关闭释放");
   }
   Console.WriteLine("范围：本批重建实际组件直接准入与真实参数视图、正式默认资源；不是完整封包启动、主窗、算法、主题或VS设计器验收");return 0;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
 private static IEnumerable<T> Descendants<T>(DependencyObject value) where T:DependencyObject
 {
  if(value is T match)yield return match;
  for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(value);i++)
   foreach(var child in Descendants<T>(System.Windows.Media.VisualTreeHelper.GetChild(value,i)))yield return child;
 }
 private static void Pump(DispatcherPriority priority=DispatcherPriority.ApplicationIdle){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(priority,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void DumpAssemblyStages(string toolId,DependencyObject control,string phase)
 {
  var fields=Descendants<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>(control).FirstOrDefault();
  if(fields is null)return;
  var stages=string.Join(" ",fields.ConstructionStages.Select(mark=>$"{mark.Stage}={mark.Ms}ms"));
  var pages=string.Join(" ",fields.LastBuildStageMs.Select(pair=>$"{pair.Key}[rows={pair.Value.RowsMs}ms,layout={pair.Value.LayoutMs}ms]"));
  var fieldCosts=string.Join(" ",fields.LastFieldBuildMs.Where(pair=>pair.Value>=5).OrderByDescending(pair=>pair.Value).Take(12).Select(pair=>$"{pair.Key}={pair.Value}ms"));
  var blob=Descendants<SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor>(control).FirstOrDefault();
  var composition=blob is null?"":string.Join(" ",blob.CompositionStageMs.Select(pair=>$"{pair.Key}={pair.Value}ms"));
  Console.WriteLine($"[STAGE] {toolId} {phase} 构造[{stages}] 页面[{pages}] 字段[{fieldCosts}] 显隐={fields.VisibilityElapsedMs}ms/{fields.VisibilityUpdateCount}次 组合[{composition}]");
 }
}
