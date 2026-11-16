using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.UI.Services.Editing;
internal static class Program
{
 private const long OpenBudgetMs=600, TabBudgetMs=150, RowBudgetMs=300, FieldBudgetMs=5;
 [STAThread] private static int Main(string[] args)
 {
  var imagePath=Path.Combine(Path.GetTempPath(),$"SunEyeVision-editor-probe-{Guid.NewGuid():N}.pgm");
  var colorPath=Path.ChangeExtension(imagePath,"png");
  try
  {
   // Load the actual host resource graph, including implicit item styles. A
   // library-only Application misses collisions that occur in the shipped UI.
   // InitializeComponent loads App.xaml; this probe never calls Run/OnStartup.
   var app=new SunEyeVision.UI.App();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
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
   using var inputFactory=new ImageSourceHostInputFactory();
   var graph=session.PrepareGraphEdit(flow.Id);
   var source=session.CreateNode(graph,"ImageLoad",new CanvasPoint(100,100));
   var blob=session.CreateNode(graph,"BlobAnalysis",new CanvasPoint(350,240));
   graph.AddConnection(new ConnectionDefinition("probe-image",source.Id,blob.Id,"output.image","input.image"));
   session.CommitGraphEdit(graph,"连接图像源和 Blob");
   var imageBytes=Enumerable.Repeat((byte)200,64).ToArray();
   File.WriteAllBytes(imagePath,System.Text.Encoding.ASCII.GetBytes("P5\n8 8\n255\n").Concat(imageBytes).ToArray());
   var imageDraft=session.Editing.Host.BeginEdit(flow.Id,source.Id);
   var paths=imageDraft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths");
   if(imageDraft.Configuration.Fields.Get<string>("output.format")!="mono8")
    throw new Exception("新图像源的原件默认像素格式应为 MONO8。");
   imageDraft.ApplyFields(new Dictionary<string,ParameterSetting>
   {
    ["local.paths"]=new ConstantSetting(new SequenceValue(paths.Type,new object?[]{imagePath}))
   });
   session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{imageDraft}));
   var overBudget=new List<string>();
   void Budget(string metric,long elapsed,long limit)
   {
    Console.WriteLine($"[BUDGET] {metric} {elapsed}ms / {limit}ms");
    if(elapsed>limit)overBudget.Add($"{metric} {elapsed}ms > {limit}ms");
   }
   foreach(var (toolId,node) in new[]{("ImageLoad",source),("BlobAnalysis",blob)})
   {
    var openClock=System.Diagnostics.Stopwatch.StartNew();
    var control=new RuntimeNodeConfigurationControl();var window=new RuntimeToolEditorWindow(control){Title=toolId,ShowInTaskbar=false};
    using var context=new ProductionEditorContext(session,flow.Id,node,()=>true,control.BindDraft);
    ToolEditorCommit.AttachDraftContext(control,context);context.PrepareView=()=>control.TryCapture(out var error)?null:error;
    context.HasPendingViewEdits=()=>control.HasPendingEdits;context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
    control.ConfigureDebug(context,()=>inputFactory,()=>true,Console.WriteLine,_=>null,()=>{});
    window.Closed+=(_,_)=>context.Discard();
    try
    {
    var bindClock=System.Diagnostics.Stopwatch.StartNew();
    control.BindDraft(context.PreparedDraft);
    // Use the same readiness boundary as the production host before showing the window.
    Wait(control.WaitForViewReadyAsync());
    bindClock.Stop();
    Console.WriteLine($"[TIMING] {toolId} 参数页绑定返回 {bindClock.ElapsedMilliseconds}ms");
    DumpAssemblyStages(toolId, control, "绑定后");
    var settings = FindDeclaredSettings(control) ?? throw new Exception(toolId+" 缺少正式设置视图。");
    var measured = settings.Content as SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView
     ?? throw new Exception(toolId+" 缺少正式参数树。");
    var tabs = measured.Content as System.Windows.Controls.TabControl ?? throw new Exception(toolId+" 缺少正式页签。");
    var pages = toolId=="BlobAnalysis" ? new[]{"基本参数","运行参数","结果显示"} : new[]{"基本参数"};
    if(!tabs.Items.Cast<System.Windows.Controls.TabItem>().Select(item=>item.Header as string).SequenceEqual(pages))
     throw new Exception(toolId+" 页签声明或顺序不符。");
    void VerifyComposed(int count)
    {
     if(!measured.LastBuildStageMs.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(pages.Take(count)))
      throw new Exception(toolId+" 已组装页与本轮访问页不一致。");
    }
    VerifyComposed(1);
    var rendered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var showClock=new System.Diagnostics.Stopwatch();
    window.ContentRendered+=(_,_)=>{showClock.Stop();openClock.Stop();rendered.TrySetResult();};
    window.SetAssemblyReady();
    showClock.Start();window.Show();Wait(rendered.Task);
    Budget(toolId+" 首次开窗到首帧",openClock.ElapsedMilliseconds,OpenBudgetMs);
    Console.WriteLine($"[TIMING] {toolId} 窗口首次呈现与首个页面组装 {showClock.ElapsedMilliseconds}ms");
    DumpAssemblyStages(toolId, control, "首屏后");
    Pump(DispatcherPriority.SystemIdle);DumpAssemblyStages(toolId, control, "首屏空闲后");
    VerifyComposed(1);
    VerifyPageVisuals(toolId,(FrameworkElement)((TabItem)tabs.Items[0]).Content!);
    for(var sample=0;sample<3;sample++)
    {
     var resourceClock=System.Diagnostics.Stopwatch.StartNew();
     var parsed=SunEyeVision.Plugin.Wpf.EditorResources.Shared;
     resourceClock.Stop();
     Console.WriteLine($"[TIMING] {toolId} 主题字典获取(共享单例) 第{sample+1}次 {resourceClock.ElapsedMilliseconds}ms 字典{parsed.MergedDictionaries.Count}合并");
    }
    var pageItems=tabs.Items.Cast<System.Windows.Controls.TabItem>().ToArray();
    var pageContents=new object?[pageItems.Length];
    pageContents[0]=pageItems[0].Content??throw new Exception(toolId+" 首屏没有正式页面内容。");
    for(var index=1;index<pageItems.Length;index++)
    {
      var tabClock=System.Diagnostics.Stopwatch.StartNew();
      tabs.SelectedIndex=index;Pump();tabClock.Stop();
      Budget($"{toolId} 首次切到第{index+1}页",tabClock.ElapsedMilliseconds,TabBudgetMs);
      VerifyComposed(index+1);
      pageContents[index]=pageItems[index].Content??throw new Exception(toolId+" 切页没有正式页面内容。");
      VerifyPageVisuals(toolId,pageContents[index] as FrameworkElement ?? throw new Exception(toolId+" 页面内容不是FrameworkElement。"));
      DumpAssemblyStages(toolId, control, $"第{index+1}页后");
      Pump(DispatcherPriority.SystemIdle);DumpAssemblyStages(toolId, control, $"第{index+1}页空闲后");
      VerifyComposed(index+1);
    }
    var fieldViews = measured.Fields.Select(field=>(Field:field,View:field.View)).ToArray();
    for(var round=0;round<2;round++)
     for(var index=0;index<pageItems.Length;index++)
     {
      if(tabs.SelectedIndex==index)continue;
      var repeatClock=System.Diagnostics.Stopwatch.StartNew();
      tabs.SelectedIndex=index;Pump();repeatClock.Stop();
      Budget($"{toolId} 第{round+1}轮重复切到第{index+1}页",repeatClock.ElapsedMilliseconds,TabBudgetMs);
      if(!ReferenceEquals(pageContents[index],pageItems[index].Content))
       throw new Exception(toolId+" 重复切页重新组装了页面。");
      if(fieldViews.Any(item=>!ReferenceEquals(item.View,item.Field.View)))
       throw new Exception(toolId+" 重复切页替换了正式字段行。");
      VerifyComposed(pageItems.Length);
     }
    tabs.SelectedIndex=0;Pump();
    if(toolId=="BlobAnalysis")
    {
     if(!measured.LastBuildStageMs.TryGetValue("运行参数",out var runPage))throw new Exception("缺少 Blob 运行参数页物化计时。");
     Budget("Blob 运行参数页行物化",runPage.RowsMs,RowBudgetMs);
    }
    if(measured.LastEditorCtorMs.Count==0)throw new Exception(toolId+" 缺少字段构造计时。");
    Budget(toolId+" 最慢字段构造",measured.LastEditorCtorMs.Values.Max(),FieldBudgetMs);
    if(control.Content is null||!control.IsLoaded)throw new Exception(toolId+"真实控件未加载");
    if(!context.TryValidateEditor(out var error))throw new Exception(toolId+"参数页校验失败："+error);
    if(toolId=="ImageLoad")
    {
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
    var completed=new TaskCompletionSource<RuntimeToolExecutionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnCompleted(object? sender,RuntimeToolExecutionEventArgs result)=>completed.TrySetResult(result);
    window.ToolExecutionCompleted+=OnCompleted;
    try
    {
     var execute = GetFooterButton(window, "Execute");
     GetEnabledCommand(execute).Execute(execute.CommandParameter);
     Wait(completed.Task);
     var receipt=completed.Task.Result;
     if(receipt.Result is not {IsSuccess:true} snapshot)throw new Exception(toolId+" 调试执行失败："+receipt.Error);
     using var reader=snapshot.AcquireReader();
     var imagePort=toolId=="ImageLoad"?"output.image":BlobSchema.Field(6);
     var imageRecord=reader.Values!.Get<RecordValue>(imagePort);
     var pixels=imageRecord.Get<IReadOnlyImage>("data").CopyPixels();
     if(imageRecord.Get<long>("width")!=8L||imageRecord.Get<long>("height")!=8L||imageRecord.Get<long>("pixel-format")!=17301505L)
      throw new Exception(toolId+" 调试发布的图像尺寸或 MONO8 格式字段与本轮图像不一致。");
     var expected=toolId=="ImageLoad"?imageBytes:Enumerable.Range(0,64).Select(i=>i/8==0||i%8==0?(byte)0:(byte)255);
     if(!pixels.SequenceEqual(expected))throw new Exception(toolId+" 调试发布图像像素不符合对照。");
     if(toolId=="BlobAnalysis" &&
        (reader.Values.Get<long>(BlobSchema.ModuleStatusPort)!=1L ||
         !reader.Values.Get<SequenceValue>(BlobSchema.BlobStatusesPort).Cast<long>().SequenceEqual(new[]{1L})))
      throw new Exception("Blob 图像发布与模块、单体判定未从同一结果读取。");
    }
    finally{window.ToolExecutionCompleted-=OnCompleted;}
    var confirm = GetFooterButton(window, "Confirm");
    var confirmCommand = GetEnabledCommand(confirm);
    var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    window.Closed+=(_,_)=>closed.TrySetResult();
    confirmCommand.Execute(confirm.CommandParameter);
    Wait(closed.Task);
    if(window.IsCurrent||context.IsActive||control.Content is not null||control.CurrentDebugSnapshot is not null)
     throw new Exception(toolId+" 关闭后仍保留调试窗口、草稿或发布。");
    if(confirmCommand.CanExecute(confirm.CommandParameter))
     throw new Exception(toolId+" 关闭后已退役的底栏命令仍可执行。");
    Console.WriteLine("PASS "+toolId+"：实际 Session→正式控件→捕获/回执→底栏模板→结果 reader→确定及关窗排空");
    }
    finally{if(window.IsCurrent)Wait(window.CloseAndDrainAsync());}
    if(toolId=="ImageLoad")
    {
     using var batch=session.CaptureRunBatch(new[]{flow.Id},hostFactory:inputFactory);
     var run=batch.RunAsync(Console.WriteLine);Wait(run);
     using var result=run.Result;
     if(!result.Workflows[flow.Id].Success)throw new Exception("正式连线执行未产生 Blob 调试所需的前驱发布。");
    }
   }
   VerifyPackedRgb(session,flow,source,blob,inputFactory,colorPath);
   VerifyPackedRgb(session,flow,source,blob,inputFactory,colorPath,sdk:true);
   VerifyPackedRgb(session,flow,source,blob,inputFactory,colorPath,sdk:true,memory:true);
   foreach(var violation in overBudget)Console.Error.WriteLine("[OVER BUDGET] "+violation);
   Console.WriteLine("范围：直接准入的正式控件和执行链。本方性能预算独立于主窗、发布封包与 VM 实机对照。");
   return overBudget.Count==0?0:1;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
  finally{File.Delete(imagePath);File.Delete(colorPath);}
 }
 private static void VerifyPackedRgb(DocumentSession session,WorkflowDefinition flow,NodeDefinition source,NodeDefinition blob,
     ImageSourceHostInputFactory inputFactory,string path,bool sdk=false,bool memory=false)
 {
  const int side=8;
  var locations=Enumerable.Range(0,side*side).ToArray();
  var rgb=locations.SelectMany(index=>index%side<4?new byte[]{255,0,0}:new byte[]{0,0,255}).ToArray();
  var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
  encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
      side,side,96,96,System.Windows.Media.PixelFormats.Rgb24,null,rgb,side*3)));
  using(var file=File.Create(path))encoder.Save(file);
  var imageDraft=session.Editing.Host.BeginEdit(flow.Id,source.Id);
  var paths=imageDraft.SettingsSchema.Inputs.Single(port=>port.Id=="local.paths");
  imageDraft.ApplyFields(new Dictionary<string,ParameterSetting>
  {
   ["local.paths"]=new ConstantSetting(new SequenceValue(paths.Type,new object?[]{path})),
   ["source.kind"]=new ConstantSetting(sdk?"sdk":"local"),
   ["output.format"]=new ConstantSetting("rgb24"),
   ["output.mono8"]=new ConstantSetting(true)
  });
  var blobDraft=session.Editing.Host.BeginEdit(flow.Id,blob.Id);
  blobDraft.ApplyFields(new Dictionary<string,ParameterSetting>
  {
   ["field:43bda03261df55b88f963bd18f2ba4d9"]=new ConstantSetting(50L)
  });
  session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host,nodes:new[]{imageDraft,blobDraft}));
  IHostInputFactory hostInputs=inputFactory;
  if(memory)
   hostInputs=inputFactory.ForSdkImages(flow.Id,new Dictionary<string,SdkImageInput>
   {
    [source.Id]=new(new ImageLayout(side,side,side*3,PixelLayout.Rgb24),rgb)
   });
  else if(sdk)
   hostInputs=inputFactory.ForSdkFiles(flow.Id,new Dictionary<string,string>{{source.Id,path}});
  using var batch=session.CaptureRunBatch(new[]{flow.Id},hostFactory:hostInputs);
  var run=batch.RunAsync(Console.WriteLine);
  Wait(run);
  using var result=run.Result;
  if(!result.Workflows[flow.Id].Success)throw new Exception("正式 RGB24 → Blob 连线执行失败。");
  using var imageReader=session.AcquirePublishedResult(flow.Id,source.Id)??throw new Exception("缺少 RGB24 图像源结果。");
  using var blobReader=session.AcquirePublishedResult(flow.Id,blob.Id)??throw new Exception("缺少同轮 Blob 结果。");
  var primary=imageReader.Values!.Get<RecordValue>("output.image");
  var gray=imageReader.Values.Get<RecordValue>("output.gray-image");
  if(sdk&&(imageReader.Values.Get<string>("output.source-kind")!="sdk"||
      imageReader.Values.Get<string>("output.file").Length!=0||imageReader.Values.Get<long>("output.sequence")!=0))
   throw new Exception("SDK 图像结果包含本地文件名或本地 SN。");
  var image=primary.Get<IReadOnlyImage>("data");
  var grayImage=gray.Get<IReadOnlyImage>("data");
  if(primary.Get<long>("pixel-format")!=35127316L||image.Layout.Format!=PixelLayout.Rgb24||!image.CopyPixels().SequenceEqual(rgb)
      ||gray.Get<long>("pixel-format")!=17301505L||grayImage.Layout.Format!=PixelLayout.Gray8
      ||!grayImage.CopyPixels().SequenceEqual(locations.Select(index=>index%side<4?(byte)75:(byte)29)))
   throw new Exception("图像源 RGB24 或附加灰度的像素和发布格式码不一致。");
  var binary=blobReader.Values!.Get<RecordValue>(BlobSchema.Field(6)).Get<IReadOnlyImage>("data");
  if(binary.Frame!=image.Frame||!binary.CopyPixels().SequenceEqual(locations.Select(index=>
      index/side>0&&index%side is >0 and <4?(byte)255:(byte)0)))
   throw new Exception("Blob 二值图未消费同一主图帧，或彩色通道的阈值结果不一致。");
  Console.WriteLine($"PASS {(memory?"SDK 内存":sdk?"SDK 文件":"本地图像")} RGB24 → Blob：正式连线、像素格式码、附加灰度、同帧二值图");
 }
 private static Button GetFooterButton(RuntimeToolEditorWindow window, string labelKey)
 {
  Pump();
  if(window.Content is not FrameworkElement frame || frame.FindName("BottomCommands") is not ItemsControl footer)
   throw new Exception(window.Title+" 缺少正式底栏槽。");
  var label=AppLocalizationService.GetLang(labelKey);
  return Descendants<Button>(footer).Single(button=>button.IsVisible && Equals(button.Content,label));
 }
 private static void VerifyPageVisuals(string toolId,FrameworkElement body)
 {
  foreach(var text in Descendants<TextBlock>(body).Where(item=>item.IsVisible))
   if(text.Text.StartsWith("Entry {",StringComparison.Ordinal)||text.Text=="运行时资源，请选择来源")
    throw new Exception(toolId+" 正式参数页含对象全文或旧资源占位："+text.Text);
  foreach(var advanced in Descendants<SunEyeVision.Plugin.Wpf.Presenters.Parameters.NativeAdvancedPresenter>(body).Where(item=>item.IsVisible))
   if(advanced.IsExpanded)throw new Exception(toolId+" 高级参数首次呈现应默认折叠。");
  var checkedItems=0;
  foreach(var combo in Descendants<ComboBox>(body).Where(item=>item.IsVisible&&item.IsEnabled).ToArray())
  {
   var entries=combo.Items.OfType<SunEyeVision.Plugin.Wpf.Fields.FieldRowModel.Entry>().ToArray();
   if(entries.Length==0)continue;
   var accepted=combo.SelectedValue;
   try
   {
    combo.IsDropDownOpen=true;Pump();
    foreach(var entry in entries)
    {
     if(combo.ItemContainerGenerator.ContainerFromItem(entry) is not ComboBoxItem item)
      throw new Exception(toolId+" 枚举展开后没有正式条目容器。");
     item.ApplyTemplate();item.UpdateLayout();
     var labels=Descendants<TextBlock>(item).Select(text=>text.Text).ToArray();
     if(!labels.Contains(entry.DisplayNameALang)||labels.Any(label=>label.StartsWith("Entry {",StringComparison.Ordinal)))
      throw new Exception(toolId+" 原枚举展开项未显示声明标签："+entry.DisplayNameALang);
     checkedItems++;
    }
    if(!Equals(accepted,combo.SelectedValue))throw new Exception(toolId+" 打开候选列表改变了接受值。");
   }
   finally{combo.IsDropDownOpen=false;Pump();}
  }
  Console.WriteLine($"[VISUAL-CONTRACT] {toolId} 正式宿主资源、默认折叠、可见参数文本及{checkedItems}项枚举弹出内容检查");
 }
 private static ICommand GetEnabledCommand(Button button)
 {
  if(button.Command is not { } command || !button.IsEnabled || !command.CanExecute(button.CommandParameter))
   throw new Exception($"底栏按钮 {button.Content} 的模板命令未就绪。");
  return command;
 }
 private static IEnumerable<T> Descendants<T>(DependencyObject value) where T:DependencyObject
 {
  if(value is T match)yield return match;
  for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(value);i++)
   foreach(var child in Descendants<T>(System.Windows.Media.VisualTreeHelper.GetChild(value,i)))yield return child;
 }
 private static void Pump(DispatcherPriority priority=DispatcherPriority.ApplicationIdle){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(priority,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 private static void Wait(Task task)
 {
  var timeout=System.Diagnostics.Stopwatch.StartNew();
  while(!task.IsCompleted)
  {
   if(timeout.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException("正式窗口探针等待超时。");
   Pump(DispatcherPriority.SystemIdle);
   if(!task.IsCompleted)System.Threading.Thread.Sleep(1);
  }
  task.GetAwaiter().GetResult();Pump();
 }
 private static SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView? FindDeclaredSettings(RuntimeNodeConfigurationControl control)
  => control.Content as SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView
   ?? (control.Content as FrameworkElement)?.FindName("Editor") as SunEyeVision.Plugin.Wpf.Presenters.Parameters.DeclaredSettingsView;

 private static void DumpAssemblyStages(string toolId,RuntimeNodeConfigurationControl control,string phase)
 {
  var fields=FindDeclaredSettings(control)?.Content as SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView;
  if(fields is null)return;
  var stages=string.Join(" ",fields.ConstructionStages.Select(mark=>$"{mark.Stage}={mark.Ms}ms"));
  var pages=string.Join(" ",fields.LastBuildStageMs.Select(pair=>$"{pair.Key}[rows={pair.Value.RowsMs}ms,layout={pair.Value.LayoutMs}ms]"));
  var fieldCosts=string.Join(" ",fields.LastFieldBuildMs.Where(pair=>pair.Value>=5).OrderByDescending(pair=>pair.Value).Take(12).Select(pair=>$"{pair.Key}={pair.Value}ms"));
  Console.WriteLine($"[SUBCost] ctorTotal={fields.LastEditorCtorMs.Values.Sum()}ms optionsTotal={fields.LastEditorOptionsMs.Values.Sum()} rowTotal={fields.LastRowAssemblyMs.Values.Sum()} fields={fields.LastEditorCtorMs.Count}");
  var blob=Descendants<SunEyeVision.Tool.BlobAnalysis.Wpf.Views.BlobParameterEditor>(control).FirstOrDefault();
  var composition=blob is null?"":string.Join(" ",blob.CompositionStageMs.Select(pair=>$"{pair.Key}={pair.Value}ms"));
  Console.WriteLine($"[STAGE] {toolId} {phase} 构造[{stages}] 页面[{pages}] 字段[{fieldCosts}] 显隐={fields.VisibilityElapsedMs}ms/{fields.VisibilityUpdateCount}次 组合[{composition}]");
 }
}
