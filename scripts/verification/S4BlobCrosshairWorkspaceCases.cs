using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using Native=SunEyeVision.Plugin.Wpf.Native;
using System.Collections;
using HalconDotNet;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Tool.ImageSource.Runtime;
using System.Windows.Threading;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using SunEyeVision.Workflow;

internal static class S2DocumentEditingCases
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object value,string name)=>value.GetType().GetProperty(name,Flags)?.GetValue(value)??value.GetType().GetField(name,Flags)?.GetValue(value)??throw new Exception("缺少真实宿主成员："+name);
 static void Call(object value,string name,params object?[] args)=>value.GetType().GetMethod(name,Flags)!.Invoke(value,args);
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 static void Until(Func<bool> ready){var timer=Stopwatch.StartNew();while(!ready()){if(timer.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("真实结果宿主未就绪");Pump();Thread.Yield();}}
 static IEnumerable<T> Visuals<T>(DependencyObject root)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Visuals<T>(child))yield return nested;}}

 static int _passed;static IntPtr _desktop;
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,uint access,IntPtr security);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool SetThreadDesktop(IntPtr desktop);
 [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]static extern bool CloseDesktop(IntPtr desktop);
 [STAThread]static int Main(){var result=1;var thread=new Thread(()=>{try{_desktop=CreateDesktop("SunEyeVision-C17-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);if(_desktop==IntPtr.Zero||!SetThreadDesktop(_desktop))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"隐藏Desktop不可用，不回退用户桌面");Console.WriteLine("C17自有隐藏Desktop已建立");Run();result=0;}catch(Exception failure){Console.Error.WriteLine(failure);}finally{Application.Current?.Shutdown();if(_desktop!=IntPtr.Zero)CloseDesktop(_desktop);}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return result;}
 static void Case(string name,Action action){var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"crosshair-progress.log");File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" BEGIN "+name+Environment.NewLine);try{action();_passed++;Console.WriteLine("通过："+name);File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" PASS "+name+Environment.NewLine);}catch(Exception error){File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" FAIL "+name+Environment.NewLine+error);throw;}}
 static void Capture(FrameworkElement view,string name){view.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));encoder.Save(file);}
 static void Run()
 {
  var app=Application.Current??new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var resourcePath in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/CommonStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.UI;component/"+resourcePath,UriKind.Relative)});
  Require(PluginLoader.LoadPlugins(),"正式插件未接纳");ServiceInitializer.InitializeServices();
  var managerField=typeof(ServiceInitializer).GetField("_solutionManager",BindingFlags.Static|BindingFlags.NonPublic)!;var previous=managerField.GetValue(null);
  var document=new Solution{Name="Blob真实结果宿主联动"};var engine=new WorkflowEngine(new CheckLogger());var flow=engine.CreateWorkflow(Guid.NewGuid().ToString("N"),"Blob结果联动");document.Workflows.Add(flow);
  var node=new SunEyeVision.UI.Shared.WorkflowNode("blob","Blob","Blob",BlobTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(BlobTool.Id).WithSettings(new BlobSettings{Threshold=100,IsAreaFilterEnabled=false,OutputGrayImage=true,OutputBinaryImage=true})};flow.Nodes.Add(node);
  var path=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source.png");
  var source=new SunEyeVision.UI.Shared.WorkflowNode("source","图像源","图像源",ImageSourceTool.Id){RuntimeConfiguration=RuntimeToolConfiguration.Create(ImageSourceTool.Id).WithSettings(new ImageSourceSettings{FileReferences=new[]{path},StartIndex=0})};
  flow.Nodes.Insert(0,source);flow.Connections.Add(new WorkflowConnection("source-to-blob",source.Id,node.Id));
  node.RuntimeConfiguration=node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>{{BlobTool.ImageInput,new ReferenceSetting(new OutputReference(source.Id,ImageSourceOutput.Image,ImageSourceOutput.ImageType,new[]{ImageSourceOutput.ImageData},scopeId:flow.Id))}});
  MainWindowViewModel? main=null;ImageResultControl? view=null;Window? window=null;WorkflowContext? context=null;var runs=new List<ExecutionResult>();var borrowed=new List<IDisposable>();IReadOnlyImage? retainedImage=null;
  try
  {
   var manager=new SolutionManager(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"solution-manager"));managerField.SetValue(null,manager);manager.SetCurrentSolution(document,false);
   main=new MainWindowViewModel();main.SelectedNode=node;node.IsSelected=true;
   var formal=main.CreateDataSourceQueryService(flow.Id).FormalOutputContractReader!.Invoke(node.Id);Require(formal.Single(field=>field.PortId==BlobSchema.Field(5)).DisplayName=="灰度图像"&&!formal.Any(field=>field.DisplayName=="Blob图像"),"自动来源/引用树的真实正式目录仍冒称Blob图像");context=(WorkflowContext)Get(main,"CurrentWorkflowContext");var results=(NodeResultManager)Get(main,"_nodeResultManager");
   view=new ImageResultControl{DataContext=main.ImageResult};view.Initialize(main);window=new Window{Content=view,Width=1000,Height=850,ShowActivated=false,ShowInTaskbar=false};window.Show();Pump();
   var shell=(ResultWorkspaceShell)view.FindName("Workspace");shell.IsDoubleImage=true;var presenter=Get(view,"_workspacePresenter");var output=Get(presenter,"OutputPane");var viewport=(ImageViewBox)shell.OutputViewport;
   var executor=new WorkflowExecutionEngine(engine,new PluginManager(),new CheckLogger());
   DeclaredOutputSnapshot Publish(int offset,bool empty=false,bool multiple=false)
   {
    var pixels=new byte[256];if(!empty)for(var y=2;y<10;y++)for(var x=2+offset;x<10+offset;x++)pixels[y*16+x]=200;if(multiple)for(var y=12;y<14;y++)for(var x=1;x<3;x++)pixels[y*16+x]=200;
    var bitmap=BitmapSource.Create(16,16,96,96,PixelFormats.Gray8,null,pixels,16);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    using var image=HalImage.CreateFromBytes(pixels,16,16,1);
    var task=executor.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id,image,sourceInputs:new Dictionary<string,ImageSourceRunInput>{{source.Id,new ImageSourceRunInput(path,offset+1){LocalReadAdmission=_=>path}}}){Configuration=WorkflowRunConfiguration.Capture(new[]{flow}),ExecutionOwner=document.ExecutionOwner,ResourcePaths=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(document)});Until(()=>task.IsCompleted);var run=task.GetAwaiter().GetResult();runs.Add(run);
    Require(run.Success,"真实Blob执行失败："+string.Join(";",run.Errors??new()));var snapshot=(DeclaredOutputSnapshot)run.NodeResults[node.Id].Result!;
    Require(snapshot.IsSuccess&&snapshot.SourceWorkflowId==flow.Id,"Blob没有本流程正式快照");Require(snapshot.Fields[BlobSchema.Field(5)].DisplayName=="灰度图像","真实快照/引用树输出元数据仍冒称Blob图像");
    using(var reader=snapshot.AcquireReader())
    {
     var gray=reader.Values!.Get<IReadOnlyImage>(BlobSchema.Field(5));var binary=reader.Values.Get<IReadOnlyImage>(BlobSchema.Field(6));
     Require(gray.Frame==binary.Frame&&gray.EffectiveDomain.Frame==BlobSchema.ImageFrame&&binary.EffectiveDomain.Frame==BlobSchema.ImageFrame,"灰/二值输出物理帧或声明坐标域不一致");
     Require(gray.Width==16&&gray.Height==16&&gray.CopyPixels().SequenceEqual(pixels)&&binary.CopyPixels().SequenceEqual(pixels.Select(value=>(byte)(value>=100?255:0))),"灰/二值输出改变实际像素或尺寸");
     using var grayDomain=gray.EffectiveDomain.AcquireReader();using var binaryDomain=binary.EffectiveDomain.AcquireReader();var expected=Enumerable.Range(0,16).Select(row=>new RegionRun(row,0,15)).ToArray();
     Require(grayDomain.ReadRuns(0,4096).Span.SequenceEqual(expected)&&binaryDomain.ReadRuns(0,4096).Span.SequenceEqual(expected),"灰/二值输出没有保留真实Domain runs");
    }results.UpdateNodeResult(source,(DeclaredOutputSnapshot)run.NodeResults[source.Id].Result!,false,context);results.UpdateNodeResult(node,snapshot,true,context);Pump();return snapshot;
   }
   IResultPublication Publication()=>(IResultPublication)Get(main,"CurrentResultPublication");
   IToolResultView Table()=>(IToolResultView)Get(main,"_activeResultView");
   void SelectImage(string? port=null,string? selectedNode=null)
   {
    Until(()=>shell.OutputItems.Cast<object>().Any());
    var choice=shell.OutputItems.Cast<object>().Single(item=>Get(item,"Reference") is OutputReference reference&&reference.NodeId==(selectedNode??node.Id)&&reference.PortId==(port??BlobSchema.Field(5)));if(port is null||port==BlobSchema.Field(5))Require(Get(choice,"DisplayName").ToString()!.Contains("灰度图像")&&!Get(choice,"DisplayName").ToString()!.Contains("Blob图像"),"结果图像选择仍冒称Blob绘制图像");shell.SelectedOutputItem=choice;Pump();
   }

   var first=Publish(0);Until(()=>Get(main,"_activeResultView") is IToolResultView);SelectImage();
   var viewportAdapter=(NativeResultViewport)Get(output,"Viewport");var pane=Get(presenter,"_output");
   NativeResultFrameReceipt Receipt()=>(NativeResultFrameReceipt)Get(output,"CurrentFrame");
   IReadOnlyList<ICross> CrossShapes()=>((IEnumerable?)viewport.ShapeList)?.Cast<object>().OfType<ICross>().ToArray()??Array.Empty<ICross>();
   CrosshairSettingsWindow OpenCross()
   {
    Require(shell.CrossLinesCommand?.CanExecute(null)==true,"真实shell十字命令不可用");shell.CrossLinesCommand!.Execute(null);Pump();
    var created=(NativeResultViewport.CrosshairLayer)Get(pane,"_cross");var active=created.CurrentWindow??throw new Exception("真实shell没有打开设置窗");
    Require(active.IsVisible&&ReferenceEquals(active.Owner,window)&&ReferenceEquals(active.DataContext,created),"设置窗没有借用实际Pane owner/模型");return active;
   }
   Native.Numeric.Arranged.SliderArrange Number(Window settings,CrosshairSettings model,string property)
    =>Visuals<Native.Numeric.Arranged.SliderArrange>(settings).Single(control=>ReferenceEquals(control.DataContext,model)&&BindingOperations.GetBinding(control,Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty)?.Path?.Path==property);
   ToggleButton Toggle(Window settings,CrosshairSettings model,string property)
    =>Visuals<ToggleButton>(settings).Single(control=>ReferenceEquals(control.DataContext,model)&&BindingOperations.GetBinding(control,ToggleButton.IsCheckedProperty)?.Path?.Path==property);
   var settings=OpenCross();var crosses=(NativeResultViewport.CrosshairLayer)Get(pane,"_cross");var one=crosses.CrossDatas[0];var two=crosses.CrossDatas[1];
   Case("真实Blob帧经shell命令打开原设置及双十字",()=>{
    using var reader=first.AcquireReader();var gray=reader.Values!.Get<IReadOnlyImage>(BlobSchema.Field(5));Require(Receipt().Frame==gray.Frame&&Receipt().Coordinates==BlobSchema.ImageFrame,"辅助十字未使用实际Blob派生图帧和域");
    Require(crosses.CrossDatas.Count==2&&one.Index==1&&two.Index==2&&one.ImageWidth==16&&one.ImageHeight==16&&one.IsVisible&&!two.IsVisible,"原双十字结构/当前图尺寸或默认显隐不符");
    Require(CrossShapes().Count==2&&Visuals<Native.Display.VMBaseControls.Base.ImageView.Shape.CrossControl>(viewport).Count()==2,"实际视口未接入两个原CrossControl");
   });
   Case("原设置控件编辑双实例、颜色坐标及关闭重开",()=>{
    var originalBinding=Number(settings,one,"CenterX").GetBindingExpression(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty);
    Number(settings,one,"CenterX").SetCurrentValue(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty,3d);Number(settings,one,"CenterY").SetCurrentValue(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty,4d);Number(settings,one,"StrokeThickness").SetCurrentValue(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty,2d);
    Toggle(settings,two,"IsVisible").SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();
    var picker=Visuals<Native.Input.HikPcUI.ListView.ColorPickerControlView>(settings).Single(control=>ReferenceEquals(control.DataContext,one));Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute("#FF0000FF",picker);Pump();
    Require(one.CenterPoint==new Point(3,4)&&one.StrokeThickness==2&&one.Stroke=="#FF0000FF"&&two.IsVisible&&two.Stroke!="#FF0000FF","原控件未回写或双实例污染");
    Require(originalBinding?.Status==BindingStatus.Active&&ReferenceEquals(originalBinding,Number(settings,one,"CenterX").GetBindingExpression(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty)),"编辑替换了正式TwoWay绑定");
    var center=Visuals<Button>(settings).Single(button=>ReferenceEquals(button.Command,one.CenterCommand));Require(center.Command.CanExecute(center.CommandParameter),"实际居中按钮Command不可用");center.Command.Execute(center.CommandParameter);Pump();Require(one.CenterPoint==new Point(8,8),"原居中命令未消费当前真实尺寸");
    Toggle(settings,one,"IsLocked").SetCurrentValue(ToggleButton.IsCheckedProperty,true);Pump();Require(one.IsLocked&&!two.IsLocked,"原锁定没有独立回写");Toggle(settings,one,"IsLocked").SetCurrentValue(ToggleButton.IsCheckedProperty,false);Pump();
    Capture(settings,"crosshair-production-settings.png");var prior=settings;settings.Close();Pump();Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute("#FFFF0000",picker);Require(one.Stroke=="#FF0000FF","关闭后旧色板写入");settings=OpenCross();Require(!ReferenceEquals(prior,settings)&&one.CenterPoint==new Point(8,8),"重开复用旧窗或无故重置");
   });
   Case("真实表高亮与辅助十字层独立共存",()=>{
    object? Field(object instance,string name)
    {
     for(var type=instance.GetType();type is not null;type=type.BaseType)
      if(type.GetField(name,Flags) is {} field)return field.GetValue(instance);
     throw new Exception("缺少图层字段："+name);
    }
    object[] Items(object layer)=>Field(layer,"_prepared") is {} prepared?(object[])Get(prepared,"Items"):Array.Empty<object>();
    object[] Shapes()=>((IEnumerable?)viewport.ShapeList)?.Cast<object>().ToArray()??Array.Empty<object>();
    bool Contains(object[] values,object item)=>values.Any(value=>ReferenceEquals(value,item));
    var table=Table();var grid=Visuals<DataGrid>(table.View).Single();
    var receipt=Receipt();var publication=Publication();var display=Get(pane,"Display");var displayLayer=Get(display,"Layer");
    var originalDisplay=Items(displayLayer);var originalCross=CrossShapes().Cast<object>().ToArray();var selectionLayer=Get(pane,"Selection");
    Require(originalDisplay.Length>0&&originalCross.Length==2,"缺少常驻图文或辅助十字前置");
    ResultSelection? observed=null;
    EventHandler<ResultSelection> handler=(sender,selection)=>{if(ReferenceEquals(sender,table))observed=selection;};
    table.SelectionHighlightChanged+=handler;
    try{grid.SelectedItem=grid.Items[0];Pump();}finally{table.SelectionHighlightChanged-=handler;}
    Require(observed is not null&&observed.Publication==publication.Stamp&&Equals(observed.ElementIdentity,Get(grid.SelectedItem,"ElementIdentity")),"真实表事件的运行或元素身份不符");
    Require(observed!.Targets.Count>0&&observed.Targets.All(target=>target.ImageFrame==receipt.Frame&&target.Coordinates==receipt.Coordinates&&target.Geometry.Count>0),"高亮目标不属当前图像帧或缺少几何");
    var selected=Items(selectionLayer);var combined=Shapes();
    Require(selected.Length>0&&Equals(Field(selectionLayer,"_receipt"),receipt)&&selected.All(item=>Contains(combined,item)),"选择图层未接纳当前帧高亮");
    Require(ReferenceEquals(Get(pane,"Display"),display)&&originalDisplay.All(item=>Contains(combined,item))&&originalCross.All(item=>Contains(combined,item))&&CrossShapes().Count==2,"选择覆盖常驻图文或辅助十字");
    main.ClearResultHighlight();Pump();combined=Shapes();
    Require(Field(selectionLayer,"_prepared") is null&&selected.All(item=>!Contains(combined,item)),"清除高亮未只退役选择图层");
    Require(publication.IsCurrent()&&Receipt()==receipt&&ReferenceEquals(Get(pane,"Display"),display)&&originalDisplay.All(item=>Contains(combined,item))&&originalCross.All(item=>Contains(combined,item))&&CrossShapes().Count==2,"清高亮删除常驻图文、辅助十字或改变帧");
   });
   var firstReceipt=Receipt();var oldWindow=settings;var oldNumber=Number(settings,one,"CenterX");var oldPicker=Visuals<Native.Input.HikPcUI.ListView.ColorPickerControlView>(settings).Single(control=>ReferenceEquals(control.DataContext,one));
   Case("真实灰二值切换保物理帧并退役旧设置叶",()=>{
    SelectImage(BlobSchema.Field(6));Until(()=>Receipt()!=firstReceipt);Require(Receipt().Frame==firstReceipt.Frame&&Receipt().Coordinates==BlobSchema.ImageFrame,"同次灰/二值物理帧或坐标错配");Require(!oldWindow.IsVisible,"换图未关闭旧设置窗");settings=OpenCross();var accepted=one.CenterPoint;var stroke=one.Stroke;oldNumber.SetCurrentValue(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty,99d);Native.Input.HikPcUI.ListView.ColorPickerControlView.SelectColorCommand.Execute("#FFFF0000",oldPicker);Pump();Require(one.CenterPoint==accepted&&one.Stroke==stroke,"旧图设置叶写入当前帧");
   });
   var beforeRerun=Receipt();var retiredWindow=settings;var retiredNumber=Number(settings,one,"CenterX");
   var oldLease=(IDisposable)output.GetType().GetMethod("BorrowCurrentImage",Flags)!.Invoke(output,null)!;borrowed.Add(oldLease);retainedImage=(IReadOnlyImage)Get(oldLease,"Image");
   Case("同尺寸真实重跑以新Run和Frame退役旧窗口",()=>{
    var second=Publish(1);SelectImage();Until(()=>Receipt().Frame!=beforeRerun.Frame);Require(second.Identity!=first.Identity&&Receipt().Frame!=firstReceipt.Frame&&Receipt().Coordinates==BlobSchema.ImageFrame,"重跑混用旧Run/物理帧");Require(!retiredWindow.IsVisible&&!(bool)Get(oldLease,"IsCurrent"),"重跑未退役旧窗口或来源租约");settings=OpenCross();var accepted=one.CenterPoint;retiredNumber.SetCurrentValue(Native.Numeric.Arranged.SliderArrangeBase.StartValueProperty,77d);Pump();Require(one.CenterPoint==accepted,"旧Run数值回调写新帧");
   });
   Case("真实空结果无假Blob行且辅助线仍属视图",()=>{
    var empty=Publish(0,true);SelectImage();Require(Visuals<DataGrid>(Table().View).Single().Items.Count==0,"空算法结果生成假行");settings=OpenCross();Require(CrossShapes().Count==2&&Receipt().Frame!=beforeRerun.Frame,"空图像帧错误或辅助线冒充Blob数据");
   });
   Case("工作区关闭退役命令窗口和当前模型",()=>{
    var command=shell.CrossLinesCommand!;var accepted=one.CenterPoint;var old=settings;view.Shutdown();Pump();Require(!old.IsVisible&&!command.CanExecute(null)&&!one.CenterCommand.CanExecute(null),"Shutdown后旧命令/窗口仍活跃");one.CenterPoint=new Point(99,99);Require(one.CenterPoint==accepted,"退役模型仍可写");Require(!(bool)Get(oldLease,"IsCurrent"),"关闭复活旧租约");oldLease.Dispose();borrowed.Clear();
   });
   Console.WriteLine("C17真实Blob辅助十字生产链："+_passed+"组通过；不代自然输入/原厂导出/ROI或图文owner。");

  }
  finally
  {
   foreach(var lease in borrowed)lease.Dispose();view?.Shutdown();if(window is not null){window.Content=null;window.Close();}if(main is not null){Call(main,"CloseResultPresentation");Call(main,"ReleaseDocumentHost");}context?.ClearNodeResults();foreach(var run in runs)run.Dispose();managerField.SetValue(null,previous);
  }
  Require(retainedImage is not null,"没有捕获真实灰度资源以核释放");var released=false;try{using var pixels=retainedImage!.AcquirePixels();}catch(ObjectDisposedException){released=true;}Require(released,"所有真实发布/图像租约/运行owner释放后仍可借阅旧灰度资源");
 }
 sealed class CheckLogger:ILogger{public ILogger ForSource(string source)=>this;public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All){if(level==LogLevel.Error)Console.Error.WriteLine(message+" "+exception);}}
}
